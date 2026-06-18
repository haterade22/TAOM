using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.ShaderPrecompilation.Domain;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ShaderPrecompilation;

// Orchestrates the shader walk: item 0 = all-characters battle, then one pass per battle scene.
// Driven once per frame from SubModule.OnApplicationTick (the global heartbeat that survives the
// menu<->battle transitions). Per-item compile detection uses the unit-tested ShaderPrecompileDecider;
// this class owns the OUTER state machine + the engine calls (StartNewGame / EndGame / shader count)
// and is therefore a boundary (ADR-008, verified in-game).
public sealed class ShaderPrecompileRunner
{
    private enum RunState { Idle, Starting, Running, Ending, Complete }

    // Safety bounds (ms).
    private const long StartTimeoutMs = 120_000;  // item never reached "rendering" — abort it
    private const long EndSettleMs    = 1_500;    // after EndGame, wait for the menu to stabilize
    // EndGame() is async; on the clean path the engine cleans the state stack and Game.Current goes
    // null (Codex traced EndGame -> Mission.EndMission -> MissionState CleanStates -> Game destroyed),
    // which is the normal exit via `atMenu`. This is only a LAST-RESORT backstop for a stuck teardown
    // — kept generous so it never force-starts the next item WHILE teardown is still in progress
    // (a short timeout would stack a new game on an uncleaned stack). TickEnding logs the live state
    // at 1 Hz so the first real walk confirms the clean path fires well before this (issue #287).
    private const long EndTimeoutMs   = 90_000;

    // The currently-running instance, for the static game-manager callbacks. Only one walk at a time.
    private static ShaderPrecompileRunner _active;

    private readonly IShaderPrecompilationService _service;
    private readonly IPrecompileSceneProvider _sceneProvider;
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

    public ShaderPrecompileRunner(IShaderPrecompilationService service, IPrecompileSceneProvider sceneProvider, IModLogger logger)
    {
        _service = service;
        _sceneProvider = sceneProvider;
        _logger = logger;
    }

    public bool IsActive => _state != RunState.Idle && _state != RunState.Complete;

    // Single-line status for the loading-screen patch + the in-menu/in-mission reporter.
    public string StatusLine { get; private set; } = string.Empty;

    public void Begin()
    {
        if (IsActive) { _logger?.LogWarning("[ShaderPrecompilation] walk already running — ignoring Begin"); return; }
        _active = this;
        _plan = ShaderPrecompilePlanner.BuildPlan(_sceneProvider.GetScenes());
        _index = 0;
        _walkStartedMs = NowMs();
        _logger?.LogInfo($"[ShaderPrecompilation] === WALK START — {_plan.Count} items ({_plan.Count - 1} scenes + 1 character battle) ===");
        StartCurrentItem();
    }

    private void StartCurrentItem()
    {
        var item = _plan[_index];
        _decider.ResetForItem();
        _lastRemaining = -1;
        int gen = ++_generation;
        EnterState(RunState.Starting);
        UpdateStatus(item, -1, NowMs());
        _logger?.LogInfo($"[ShaderPrecompilation] --- item {_index + 1}/{_plan.Count}: {item.Description} ---");
        try
        {
            MBGameManager.StartNewGame(new TaomShaderGameManager(item, gen, _service, _logger));
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[ShaderPrecompilation] StartNewGame threw for item {_index + 1}: {ex.Message} — skipping");
            BeginEnd();
        }
    }

    // ---- static callbacks from the per-item game manager (carry the item generation) ---- //
    public static void NotifyItemRendering(int generation) => _active?.OnItemRendering(generation);
    public static void NotifyItemFailed(int generation) => _active?.OnItemFailed(generation);

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
        if (generation != _generation) return;
        _logger?.LogWarning($"[ShaderPrecompilation] item {_index + 1} failed to start — advancing");
        BeginEnd();
    }

    // ---- per-frame driver (SubModule.OnApplicationTick) ---- //
    public void Tick()
    {
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
            BeginEnd();
            return;
        }
        // Tick the loading clock ~1/s so the scene-load phase shows a moving timer, not a frozen 0s.
        if (now - _lastStatusMs >= 1000) UpdateStatus(_plan[_index], -1, now);
    }

    private void TickRunning()
    {
        long now = NowMs();
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
            _logger?.LogWarning($"[ShaderPrecompilation] item {_index + 1} hit per-item timeout after {Sec(itemElapsed)}s — advancing");
            BeginEnd();
        }
    }

    private void BeginEnd()
    {
        EnterState(RunState.Ending);
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

        if ((atMenu && sinceEnd >= EndSettleMs) || sinceEnd >= EndTimeoutMs)
        {
            _logger?.LogInfo($"[ShaderPrecompilation] Ending item {_index + 1} resolved via {(atMenu ? "clean-menu" : "timeout")} at {Sec(sinceEnd)}s");
            _index++;
            if (_index < _plan.Count) StartCurrentItem();
            else Finish();
        }
    }

    private void Finish()
    {
        EnterState(RunState.Complete);
        long total = NowMs() - _walkStartedMs;
        StatusLine = $"Shader pre-compilation COMPLETE — {_plan.Count} items in {FormatElapsed(Sec(total))}. You can play now.";
        _logger?.LogInfo($"[ShaderPrecompilation] === WALK COMPLETE — {_plan.Count} items in {FormatElapsed(Sec(total))} ===");
        // IsActive flips false here, so show the completion line directly (the tick won't fire again).
        try { InformationManager.DisplayMessage(new InformationMessage(StatusLine)); } catch { }
        _active = null;
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
                     $"(item {FormatElapsed(itemSec)}, total {FormatElapsed(totalSec)})";
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
