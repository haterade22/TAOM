using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.ShaderPrecompilation.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ShaderPrecompilation;

// Orchestrates the shader walk: the character batches (item 0 discovers the roster and re-plans the
// rest, see OnRosterDiscovered), then one pass per battle scene.
// Driven once per frame from SubModule.OnApplicationTick (the global heartbeat that survives the
// menu<->battle transitions). Per-item compile detection uses the unit-tested ShaderPrecompileDecider;
// this class owns the OUTER state machine + the engine calls (StartNewGame / EndGame / shader count)
// and is therefore a boundary (ADR-008, verified in-game).
public sealed class ShaderPrecompileRunner
{
    private enum RunState { Idle, Starting, Running, Ending, Complete }

    // Safety bounds (ms).
    // An item that never reaches "rendering" is abandoned after this long. Every item pays a full
    // CustomGame module-data load (TAOM's ~5,000 characters and ~6,000 items) before its manager can
    // report, and batch 1 also discovers the roster, so this is bounded by the slowest disk, not by
    // the scene. A premature abort on batch 1 would discard the whole character phase (only batch 1
    // can re-plan), so the bound is deliberately generous; Ctrl+Shift+K remains the human escape.
    private const long StartTimeoutMs = 600_000;  // 10 min
    private const long EndSettleMs    = 1_500;    // after EndGame, wait for the menu to stabilize
    // EndGame() is async; on the clean path the engine cleans the state stack and Game.Current goes
    // null (Codex traced EndGame -> Mission.EndMission -> MissionState CleanStates -> Game destroyed),
    // which is the normal exit via `atMenu`. This is only a LAST-RESORT backstop for a stuck teardown
    // — kept generous so it never force-starts the next item WHILE teardown is still in progress
    // (a short timeout would stack a new game on an uncleaned stack). TickEnding logs the live state
    // at 1 Hz so the first real walk confirms the clean path fires well before this (issue #287).
    private const long EndTimeoutMs   = 90_000;

    // Per-item-kind decider caps. A character batch legitimately compiles for many minutes on a cold
    // cache, so it keeps generous caps (churn backstop OFF: it can compile continuously for a long
    // stretch). The frozen-count guard is the real stuck detector and does not scale with batch size
    // (one shader hung is one shader hung); the absolute cap is only a backstop, and a tight one on a
    // slow HDD machine would recreate silent under-coverage, the defect #560 removes. A single scene
    // pass should never take minutes, so it gets tight caps + the churn backstop. This is the 1.4.7
    // stall bound: when the native shader counter churns without ever settling to zero, a scene pass
    // can no longer trap the walk for the full default (the "stuck for hours" report).
    private const long CharBattlePerItemMs    = 3_600_000;     // 60 min absolute per batch
    private const long CharBattleNoProgressMs = 900_000;       // 15 min frozen-count
    private const long CharBattleMaxActiveMs  = long.MaxValue; // churn backstop disabled for the batches
    private const long ScenePerItemMs         = 480_000;       // 8 min absolute
    private const long SceneNoProgressMs      = 180_000;       // 3 min frozen-count
    private const long SceneMaxActiveMs       = 360_000;       // 6 min continuous-nonzero (churn)

    // The currently-running instance, for the static game-manager callbacks. Only one walk at a time.
    private static ShaderPrecompileRunner _active;

    private readonly IShaderPrecompilationService _service;
    private readonly IPrecompileSceneProvider _sceneProvider;
    private readonly IShaderPrecompileCrashGuard _crashGuard;
    private readonly IModLogger _logger;
    private readonly ShaderPrecompileDecider _decider = new();

    private RunState _state = RunState.Idle;
    private IReadOnlyList<PrecompileItem> _plan = Array.Empty<PrecompileItem>();
    private int _index;
    private long _itemStartedMs;     // when the current item began rendering
    private long _stateEnteredMs;    // when we entered the current state (for Start/End timeouts)
    private long _walkStartedMs;
    private int _lastRemaining = -1;
    private long _lastEndLogMs;
    private long _lastStatusMs;      // last time StatusLine was recomputed — drives the ~1s live refresh
    // Monotonic id per started item. A game manager captures it and echoes it in its callback, so a
    // late callback from a previously-started (timed-out) item cannot flip the CURRENT item to Running.
    private int _generation;
    // The scene passes kept after the crash-guard filter, held so the roster re-plan (below) can
    // rebuild the full plan without re-reading the config or the MCM toggle mid-walk.
    private IReadOnlyList<string> _scenes = Array.Empty<string>();
    // Items that timed out, failed to start or were aborted by the decider. Reported on completion so
    // a partial walk never reads as full coverage.
    private int _abortedItems;
    // Set once batch 1 has handed the roster over and the batches are planned. A walk that ends without
    // it compiled no troop shaders at all, and Finish() must say so instead of "COMPLETE".
    private bool _rosterDiscovered;
    // The shader battle this item opened, claimed by SubModule.OnMissionBehaviorInitialize through
    // TryClaimMission. Only that mission gets the deployment guard, and only its exit is ours to act on.
    private Mission _ownedMission;
    // Ctrl+Shift+K sets this; the teardown then runs through the normal Ending state and TickEnding
    // finishes the walk as cancelled once the game is really gone. Never a second EndGame().
    private bool _cancelRequested;

    public ShaderPrecompileRunner(IShaderPrecompilationService service, IPrecompileSceneProvider sceneProvider,
        IShaderPrecompileCrashGuard crashGuard, IModLogger logger)
    {
        _service = service;
        _sceneProvider = sceneProvider;
        _crashGuard = crashGuard;
        _logger = logger;
    }

    public bool IsActive => _state != RunState.Idle && _state != RunState.Complete;

    // SubModule.OnMissionBehaviorInitialize asks this for EVERY mission that initializes. True only for
    // the walk's own battle: the first mission initialized while an item is Starting or Running (never
    // during the between-item teardown, when the main menu can already be interactive). A second,
    // different mission during the same item means the player left the shader battle and started their
    // own from the custom-battle screen; the walk stands down without touching it (no EndGame), so that
    // battle never receives the guard and is never ended by the runner (Codex review 2026-09-11, F4).
    // True while a shader battle can be initializing or running. BannerBearerAssignmentMissionLogic
    // reads it to skip banner assignment inside the walk's battles; mission-scoped decisions use
    // TryClaimMission instead.
    public static bool IsWalkInProgress =>
        _active != null && (_active._state == RunState.Starting || _active._state == RunState.Running);

    public static bool TryClaimMission(Mission mission)
    {
        var r = _active;
        if (r == null || mission == null) return false;
        if (r._state != RunState.Starting && r._state != RunState.Running) return false;
        if (r._ownedMission == null) { r._ownedMission = mission; return true; }
        if (ReferenceEquals(r._ownedMission, mission)) return true;
        r.Abandon($"another mission started while item {r._index + 1} was running");
        return false;
    }

    // Single-line status for the loading-screen patch + the in-menu/in-mission reporter.
    public string StatusLine { get; private set; } = string.Empty;

    public void Begin()
    {
        if (IsActive) { _logger?.LogWarning("[ShaderPrecompilation] walk already running — ignoring Begin"); return; }
        _active = this;
        // Quiet the battle-load stall watchdog for the whole walk — item 1 (all-troops, cold cache)
        // legitimately loads for many minutes and would otherwise trip the 300s stall crash-bundle.
        BattleLoadStallWatchdog.SuppressStallDetection = true;
        // Self-heal against a scene that hard-crashed a prior walk's process (GPU-specific native AV
        // during load — e.g. fords_of_isen on the pbr_terrain input-layout-9 compile): the guard
        // records that scene and we drop it from the plan so the walk can complete.
        var skip = new HashSet<string>(_crashGuard.ConsumeAndGetSkipSet(), StringComparer.OrdinalIgnoreCase);
        if (skip.Count > 0) ShowCrashCaptureToast(skip.Count);
        // Scene passes (terrain/atmosphere) are the GPU-crash-prone part (#287). They run only when the MCM
        // "Include Scene Passes" toggle is on; off means an empty scene list and the character batches only,
        // with no file edits and no waiting for the native shader-compile guard.
        // We still consume the crash guard's inflight marker above so a prior crash is recorded regardless.
        // Off by default (#560): the property was renamed so the json2-persisted `true` of the old
        // toggle cannot reach it, and an unreadable settings page reads as off, never on.
        bool includeScenePasses = TAOM.Features.TaomSettings.Instance?.EnableShaderPrecompileScenePasses ?? false;
        IReadOnlyList<string> scenes = includeScenePasses ? _sceneProvider.GetScenes() : Array.Empty<string>();
        if (!includeScenePasses)
            _logger?.LogInfo("[ShaderPrecompilation] scene passes are off (MCM Graphics/Shader Precompilation, default off): running the character batches only");
        if (skip.Count > 0)
            scenes = scenes.Where(s => !skip.Contains(s)).ToList();
        _scenes = scenes;
        // The roster is not readable here: MBObjectManager exists only inside a Game, and the walk
        // starts at the main menu. Item 0 discovers it and NotifyRosterDiscovered re-plans the batches.
        _plan = ShaderPrecompilePlanner.BuildBootstrapPlan(_scenes);
        _index = 0;
        _abortedItems = 0;
        _rosterDiscovered = false;
        _ownedMission = null;
        _cancelRequested = false;
        _walkStartedMs = NowMs();
        _logger?.LogInfo($"[ShaderPrecompilation] === WALK START: character batches (roster discovered on first load) + {_scenes.Count} scene passes ===");
        StartCurrentItem();
    }

    private void StartCurrentItem()
    {
        var item = _plan[_index];
        if (item.Kind == PrecompileItemKind.CharacterBattle)
            _decider.ResetForItem(CharBattlePerItemMs, CharBattleNoProgressMs, CharBattleMaxActiveMs);
        else
            _decider.ResetForItem(ScenePerItemMs, SceneNoProgressMs, SceneMaxActiveMs);
        _lastRemaining = -1;
        _ownedMission = null;
        int gen = ++_generation;
        EnterState(RunState.Starting);
        UpdateStatus(item, -1, NowMs());
        _logger?.LogInfo($"[ShaderPrecompilation] --- item {_index + 1}/{_plan.Count}: {item.Description} ---");
        // Record the scene we're about to load so a hard process crash during its load leaves a survivor
        // marker the next walk records + skips. Scene passes ONLY: a character batch is never marked and
        // never auto-skipped, so a hard crash inside one restarts the walk from batch 1 next time rather
        // than silently dropping coverage (the skip list is for GPU-specific scene crashes, #287).
        if (item.Kind == PrecompileItemKind.ScenePass) _crashGuard.MarkLoading(item.SceneId);
        try
        {
            MBGameManager.StartNewGame(new TaomShaderGameManager(item, gen, _service, _logger));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] StartNewGame threw for item {_index + 1}: {ex.Message} — skipping");
            _abortedItems++;
            BeginEnd();
        }
    }

    // ---- static callbacks from the per-item game manager (carry the item generation) ---- //
    public static void NotifyItemRendering(int generation) => _active?.OnItemRendering(generation);
    public static void NotifyItemFailed(int generation) => _active?.OnItemFailed(generation);
    public static void NotifyRosterDiscovered(int generation, IReadOnlyList<string> roster) => _active?.OnRosterDiscovered(generation, roster);

    private void OnItemRendering(int generation)
    {
        // Ignore a late callback from a previously-started (timed-out) item — only the current
        // generation's manager, while we are still Starting, may flip THIS item to Running.
        // (Codex CONFIRMED stale-callback: the bare `_state==Starting` guard couldn't tell item N
        // from item N+1 when N's late OnLoadFinished arrived during N+1's Starting window.)
        if (generation != _generation || _state != RunState.Starting) return;
        _itemStartedMs = NowMs();
        EnterState(RunState.Running);
        _logger?.LogInfo($"[ShaderPrecompilation] item {_index + 1} rendering — watching shader count");
    }

    private void OnItemFailed(int generation)
    {
        // Mirror OnItemRendering's guard: only the CURRENT item, while still Starting, may act on a
        // failure callback. A late callback from a timed-out item — or one arriving after we already
        // advanced to Ending — must be ignored, else it re-enters BeginEnd and resets the Ending timer
        // (deep-review 2026-06-18 Agent 5; same stale-callback class as the generation tag).
        if (generation != _generation || _state != RunState.Starting) return;
        _logger?.LogWarning($"[ShaderPrecompilation] item {_index + 1} failed to start — advancing");
        _abortedItems++;
        BeginEnd();
    }

    // The bootstrap batch (item 0) is the first place the roster can be read. Re-plan the remaining
    // batches from it; the scene passes stay where they were. Same stale-callback guard as the two
    // notifies above, plus `_index == 0`: only the bootstrap item, while it is still Starting, may re-plan.
    private void OnRosterDiscovered(int generation, IReadOnlyList<string> roster)
    {
        if (generation != _generation || _state != RunState.Starting || _index != 0) return;
        var plan = ShaderPrecompilePlanner.BuildPlan(roster, _scenes);
        if (plan.Count == 0 || plan[0].Kind != PrecompileItemKind.CharacterBattle)
        {
            _logger?.LogError($"[ShaderPrecompilation] roster discovery returned {roster?.Count ?? 0} usable characters; keeping the bootstrap plan (the batch will fail and the walk advances to the scene passes)");
            return;
        }
        _plan = plan;
        _rosterDiscovered = true;
        int batches = plan.Count(p => p.Kind == PrecompileItemKind.CharacterBattle);
        _logger?.LogInfo($"[ShaderPrecompilation] roster: {roster.Count} characters, {batches} batches of up to {ShaderPrecompilePlanner.DefaultCharacterBatchSize}; plan now {plan.Count} items");
        UpdateStatus(_plan[_index], -1, NowMs());
    }

    // ---- per-frame driver (SubModule.OnApplicationTick) ---- //
    public void Tick()
    {
        // Escape hatch: a held Ctrl+Shift+K cancels a stuck walk without killing the game. Uses the
        // IMMEDIATE key state (not the buffered poll) because this runs from OnApplicationTick, outside
        // the map input layer (the NavalTravel precedent). Checked before the state switch so it fires
        // in any state (loading or rendering).
        if (IsActive && !_cancelRequested && IsCancelHotkeyDown()) Cancel();
        try
        {
            switch (_state)
            {
                case RunState.Starting: TickStarting(); break;
                case RunState.Running:  TickRunning();  break;
                case RunState.Ending:   TickEnding();   break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] runner tick error: {ex.Message}");
        }
    }

    private void TickStarting()
    {
        long now = NowMs();
        if (now - _stateEnteredMs >= StartTimeoutMs)
        {
            _logger?.LogWarning($"[ShaderPrecompilation] item {_index + 1} never started rendering in {StartTimeoutMs / 1000}s — advancing");
            _abortedItems++;
            BeginEnd();
            return;
        }
        // Tick the loading clock ~1/s so the scene-load phase shows a moving timer, not a frozen 0s.
        if (now - _lastStatusMs >= 1000) UpdateStatus(_plan[_index], -1, now);
    }

    private void TickRunning()
    {
        long now = NowMs();
        // The player left the shader battle (Esc, retreat, scoreboard exit): Mission.Current is null or no
        // longer ours. The item is over; tear the game down so the walk continues from the main menu.
        if (_ownedMission != null && !ReferenceEquals(Mission.Current, _ownedMission))
        {
            _logger?.LogWarning($"[ShaderPrecompilation] item {_index + 1}: the shader battle ended outside the walk; advancing");
            _abortedItems++;
            BeginEnd();
            return;
        }
        int remaining = Utilities.GetNumberOfShaderCompilationsInProgress();
        long itemElapsed = now - _itemStartedMs;
        // Refresh on a shader-count change OR a ~1s tick so the item/total clocks advance smoothly.
        if (remaining != _lastRemaining || now - _lastStatusMs >= 1000) { UpdateStatus(_plan[_index], remaining, now); _lastRemaining = remaining; }

        // The scene hasn't rendered (so shaders haven't queued) while the loading window is up —
        // the decider counts the "nothing to compile" grace from first render, not from StartGame.
        var action = _decider.Decide(remaining, itemElapsed, now, LoadingWindow.IsLoadingWindowActive);
        if (action == PrecompileAction.AdvanceItem)
        {
            _logger?.LogInfo($"[ShaderPrecompilation] item {_index + 1} done (compiled, settled) after {Sec(itemElapsed)}s");
            BeginEnd();
        }
        else if (action == PrecompileAction.AbortItem)
        {
            _logger?.LogWarning($"[ShaderPrecompilation] item {_index + 1} aborted ({_decider.LastAbortReason}) after {Sec(itemElapsed)}s, remaining={remaining} — advancing");
            _abortedItems++;
            BeginEnd();
        }
    }

    private void BeginEnd()
    {
        EnterState(RunState.Ending);
        // MBGameManager.EndGame() is async void and dereferences Game.Current once no manager is
        // current; with no game there is nothing to end and the call would crash the process past the
        // caller's catch (Codex review 2026-09-11, F1). TickEnding sees the menu and advances.
        if (Game.Current == null) { _logger?.LogInfo($"[ShaderPrecompilation] item {_index + 1}: no game to end, already at the menu"); return; }
        try { MBGameManager.EndGame(); }
        catch (Exception ex) { _logger?.LogWarning($"[ShaderPrecompilation] EndGame threw: {ex.Message}"); }
    }

    private void TickEnding()
    {
        long now = NowMs();
        long sinceEnd = now - _stateEnteredMs;
        bool gameNull = Game.Current == null;
        bool loading = LoadingWindow.IsLoadingWindowActive;
        bool atMenu = gameNull && !loading;

        // 1 Hz instrumentation: resolves the open question of whether Game.Current actually nulls
        // between items, or this state always exits via the EndTimeoutMs backstop.
        if (now - _lastEndLogMs >= 1000)
        {
            _lastEndLogMs = now;
            _logger?.LogInfo($"[ShaderPrecompilation] Ending item {_index + 1}: Game.Current==null={gameNull}, loading={loading}, sinceEnd={Sec(sinceEnd)}s");
        }

        if (atMenu && sinceEnd >= EndSettleMs)
        {
            // Item fully resolved (load + compile + teardown) without crashing the process — clear the
            // inflight marker so this scene is NOT recorded as crashed. A hard crash anywhere earlier in
            // the item's lifecycle never reaches here, leaving the marker for the crash guard to find.
            _crashGuard.ClearLoading();
            _ownedMission = null;
            _logger?.LogInfo($"[ShaderPrecompilation] Ending item {_index + 1} resolved via clean-menu at {Sec(sinceEnd)}s");
            if (_cancelRequested) { FinishCancelled(); return; }
            _index++;
            if (_index < _plan.Count) StartCurrentItem();
            else Finish();
        }
        else if (sinceEnd >= EndTimeoutMs)
        {
            // The game did not go away. Starting another item here would push a second game onto a
            // state stack that still holds the first one, whose loading callbacks can still fire
            // (Codex review 2026-09-11, F3). Stop the walk and tell the player instead.
            _crashGuard.ClearLoading();
            StopWalk($"the previous battle did not shut down within {EndTimeoutMs / 1000}s (Game.Current==null={gameNull}, loading={loading})");
        }
    }

    private void Finish()
    {
        _crashGuard.ClearLoading();  // belt-and-suspenders — the last item's resolution already cleared it
        BattleLoadStallWatchdog.SuppressStallDetection = false;  // walk over — re-arm the stall watchdog for real battles
        _ownedMission = null;
        EnterState(RunState.Complete);
        long total = NowMs() - _walkStartedMs;
        string aborted = _abortedItems == 0 ? "0 aborted" : $"{_abortedItems} aborted, see the log";
        if (_rosterDiscovered)
        {
            StatusLine = $"Shader pre-compilation COMPLETE: {_plan.Count} items, {aborted}, in {FormatElapsed(Sec(total))}. You can play now.";
            _logger?.LogInfo($"[ShaderPrecompilation] === WALK COMPLETE: {_plan.Count} items, {_abortedItems} aborted in {FormatElapsed(Sec(total))} ===");
        }
        else
        {
            // Batch 1 never handed the roster over (it timed out, failed to start, or found no
            // characters), so no troop shader was compiled and nothing was re-planned. Say so: the
            // whole point of the walk is the troop pass, and "COMPLETE" here would be a lie.
            StatusLine = $"Shader pre-compilation INCOMPLETE: no troop shaders were compiled because the first batch never loaded ({aborted}, {FormatElapsed(Sec(total))}). Check the log and run it again.";
            _logger?.LogError($"[ShaderPrecompilation] === WALK INCOMPLETE: the roster was never discovered, no troop shaders compiled; {_plan.Count} items, {_abortedItems} aborted in {FormatElapsed(Sec(total))} ===");
        }
        // IsActive flips false here, so show the completion line directly (the tick won't fire again).
        try { InformationManager.DisplayMessage(new InformationMessage(StatusLine)); } catch { }
        _active = null;
    }

    // User-triggered escape hatch (Ctrl+Shift+K) for a walk that's taking too long. A cancellation is a
    // REQUEST: it enters the same Ending state the per-item teardown uses, and TickEnding finishes the
    // walk as cancelled once the game is really gone. Idempotent, never a second EndGame(), and never
    // an EndGame() with no game (Codex review 2026-09-11, F1); the runner stays active until then, so a
    // fresh Begin() cannot start a game on top of one still tearing down.
    public void Cancel()
    {
        if (!IsActive || _cancelRequested) return;
        _cancelRequested = true;
        _logger?.LogWarning($"[ShaderPrecompilation] walk CANCELLED by user at item {_index + 1}/{_plan.Count} (state {_state}); tearing down");
        StatusLine = "Shader pre-compilation cancelling...";
        if (_state == RunState.Ending) return;   // teardown already requested; TickEnding will finish as cancelled
        BeginEnd();
    }

    private void FinishCancelled()
    {
        _crashGuard.ClearLoading();                              // don't record the in-flight scene as a crash
        BattleLoadStallWatchdog.SuppressStallDetection = false;  // re-arm the stall watchdog for real battles
        _ownedMission = null;
        _cancelRequested = false;
        EnterState(RunState.Idle);                               // IsActive -> false; a fresh Begin() may run later
        StatusLine = "Shader pre-compilation cancelled.";
        _logger?.LogInfo("[ShaderPrecompilation] === WALK CANCELLED ===");
        try { InformationManager.DisplayMessage(new InformationMessage(StatusLine)); } catch { }
        _active = null;
    }

    // The walk stops without touching the current game: the player took over (another mission started)
    // or the game refused to shut down. Nothing is torn down and nothing else is started.
    private void Abandon(string reason) => StopWalk(reason, "Shader pre-compilation stopped: another battle was started. Run it again from the main menu.");

    private void StopWalk(string reason, string status = null)
    {
        _logger?.LogError($"[ShaderPrecompilation] === WALK STOPPED at item {_index + 1}/{_plan.Count}: {reason} ===");
        BattleLoadStallWatchdog.SuppressStallDetection = false;
        _ownedMission = null;
        _cancelRequested = false;
        EnterState(RunState.Idle);
        StatusLine = status ?? $"Shader pre-compilation stopped: {reason}. Quit to the main menu and run it again.";
        try { InformationManager.DisplayMessage(new InformationMessage(StatusLine)); } catch { }
        _active = null;
    }

    private static bool IsCancelHotkeyDown()
    {
        try
        {
            return Input.IsKeyDownImmediate(InputKey.LeftControl)
                && Input.IsKeyDownImmediate(InputKey.LeftShift)
                && Input.IsKeyDownImmediate(InputKey.K);
        }
        catch { return false; }  // input layer not ready (e.g. mid-load) — never break the walk over a poll
    }

    // One concise in-game pointer at walk start when a prior scene hard-crashed: the native fault address
    // (Windows Event Log) is the one thing we need to actually fix it (#287). Best-effort — never break the walk.
    private void ShowCrashCaptureToast(int skippedCount)
    {
        try
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"{skippedCount} scene(s) crashed your GPU on a previous shader pre-compile. To help fix it, send the " +
                "latest Bannerlord 'Application Error' from Windows Event Viewer (eventvwr.msc -> Windows Logs -> " +
                "Application) to the TAOM author. Details are in the Logs folder.",
                new Color(1f, 0.7f, 0.3f)));
        }
        catch { /* never break the walk over a toast */ }
    }

    private void EnterState(RunState s) { _state = s; _stateEnteredMs = NowMs(); }

    private void UpdateStatus(PrecompileItem item, int remaining, long now)
    {
        // Running: item clock counts from first render. Starting (scene loading): it counts from when
        // the item entered Starting, so the "loading" phase shows a moving timer instead of a frozen 0s.
        int itemSec = _state == RunState.Running ? Sec(now - _itemStartedMs)
                    : _state == RunState.Starting ? Sec(now - _stateEnteredMs)
                    : 0;
        int totalSec = Sec(now - _walkStartedMs);
        string rem = remaining < 0 ? "loading" : $"{remaining} shaders";
        StatusLine = $"Pre-compiling shaders — {_index + 1}/{_plan.Count}: {item.Description} — {rem} " +
                     $"(item {FormatElapsed(itemSec)}, total {FormatElapsed(totalSec)}) — Ctrl+Shift+K to cancel";
        _lastStatusMs = now;
    }

    private static long NowMs() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
    private static int Sec(long ms) => (int)(ms / 1000);
    private static string FormatElapsed(int seconds)
    {
        int h = seconds / 3600, m = (seconds % 3600) / 60, s = seconds % 60;
        return h > 0 ? $"{h}h {m}m {s}s" : (m > 0 ? $"{m}m {s}s" : $"{s}s");
    }
}
