# Plan 012: Trace a loading-window lower only when the window actually drops, not on every frame

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. The orchestrator maintains `plans/README.md`
> for this run: do NOT edit it; report your status in your final message.
>
> **Where you work (read this twice).** All work happens in ONE worktree:
> `E:\repos\wt-012-loading-window-trace` (Git Bash form `E:/repos/wt-012-loading-window-trace`,
> called **W** below). The main checkout `E:\repos\TAOM` holds another session's uncommitted edits
> (to `Main/SubModule.cs`, `Main/IoC.cs`, `CHANGELOG.md`, `docs/ai-includes/orientation.md` and
> many creature files); touching it can commit their work. Your shell's working directory resets
> to `E:\repos\TAOM` on EVERY call, so a bare relative path lands in the wrong tree. Therefore,
> without exception:
> - Every shell call starts with `cd E:/repos/wt-012-loading-window-trace && `.
> - Every git call is `git -C E:/repos/wt-012-loading-window-trace ...` (the only exceptions are the
>   drift check below and the Step 0 `worktree add`, which name `E:/repos/TAOM` on purpose and write
>   nothing into its files).
> - Every Read, Edit or Write path is absolute and starts with `E:\repos\wt-012-loading-window-trace\`.
>   Every repo-relative path in this plan (for example `Main/Features/MapLoadDiagnostics/MapLoadTracer.cs`)
>   means that path under W. Never edit a path under `E:\repos\TAOM\`.
> - If the orchestrator gave you a different worktree cut from `b2e387db`, substitute its absolute
>   path for W everywhere.
>
> **Shell**: run every command in this plan in **Git Bash (the Bash tool)**, not PowerShell.
>
> **Drift check (run first)**:
> `git -C E:/repos/TAOM diff --stat b2e387db..bannerlord-1.5.x -- Main/Features/MapLoadDiagnostics TAOM.Tests/Features/MapLoadDiagnostics docs/features/map-load-diagnostics.md docs/reference/harmony-patch-registry.md`
> Expected: no output (verified empty on 2026-09-23 against `4b5662b2`, the branch tip then). It
> compares commits only and writes nothing. You work on a branch cut from `b2e387db`, so the
> excerpts below are exact for your worktree. If the command prints any file, the trunk moved under
> this plan: finish on your branch anyway, but list those files in your final report so the merge
> can be planned. If an excerpt below does not match YOUR worktree, that is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S (one new 10-line class, one 40-line patch file rewritten, two test files, two doc edits)
- **Risk**: LOW (diagnostic logging only; no gameplay, save or UI behaviour changes)
- **Depends on**: none
- **Category**: perf
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

A Harmony postfix written to trace "a handful" of loading-window transitions per session runs on
every rendered frame of the main menu, the party screen and character creation, because the engine
calls `LoadingWindow.DisableGlobalLoadingWindow()` unconditionally from those screens' frame ticks.
Each call walks 12 managed stack frames with reflection and writes a line through `FileLogger.LogInfo`,
which flushes to disk synchronously on the game thread. Measured on the desktop: one 35-minute session
wrote an 84 MB log of which 262,763 of 265,061 lines are `LOADING-WINDOW lowered` (one per
rendered frame: about 125 a second averaged over the session, peaking near 360), and one session
with the main menu left open for three hours wrote a 1.16 GB log with 4,095,062 of them. FileLogger keeps 30 logs, and crash bundles zip the whole log. After this plan a
lower is traced only when the window was actually up before the call and is down after it: the same
35-minute session would log 7 lowered lines instead of 262,763, and the diagnostic question the trace
exists for ("who raised the window, and did a matching lower ever come back") is answered exactly as
before.

## Current state

### Files and their roles

- `Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` (36 lines): two Harmony
  patch classes, `LoadingWindow_Enable_Patch` and `LoadingWindow_Disable_Patch`, category
  `Patch89_MapLoadDiagnostics_Lifecycle`. The Disable postfix is the per-frame cost. **You rewrite
  the Disable class and its part of the header comment; the Enable class stays byte-identical.**
- `Main/Features/MapLoadDiagnostics/MapLoadTracer.cs` (83 lines): static trace sink. Read only; do
  not modify.
- `Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs`: **new**, the pure decision.
- `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs`: **new**.
- `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs`: **new**.
- `docs/features/map-load-diagnostics.md` lines 43-49 and `docs/reference/harmony-patch-registry.md`
  line 1024: prose that must describe the new behaviour.
- `Main/SubModule.cs` lines 385-420: applies the category. **No edit needed** (see "Single-owner files").
- `Main/Core/Logging/FileLogger.cs`: the durable logger. Read only; do not modify.

### Excerpt: `Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` (whole file at `b2e387db`)

```csharp
using HarmonyLib;
using TaleWorlds.Engine;

namespace TAOM.Features.MapLoadDiagnostics.Hooks;

/// <summary>
/// Traces every raise and lower of the global loading window, WITH the managed caller chain.
///
/// <para>
/// This is the central question of the v1.5.0 map-load stall. The heartbeat proved the map runs at
/// 85 fps with a 5 ms campaign tick and nothing spawning, while <c>loadingWindow</c> stays true
/// indefinitely. TAOM makes no calls to either method, so whatever raises it is vanilla, and the
/// interesting fact is which vanilla path did so and whether its matching lower ever runs.
/// </para>
///
/// <para>
/// Caller chains are affordable here because these fire a handful of times per session, not per
/// frame.
/// </para>
/// </summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.EnableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch89_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Enable_Patch
{
    [HarmonyPostfix]
    public static void Postfix() => MapLoadTracer.TraceWithCallers("LOADING-WINDOW raised");
}

/// <summary>Counterpart to <see cref="LoadingWindow_Enable_Patch"/>; its absence is the symptom.</summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.DisableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch89_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Disable_Patch
{
    [HarmonyPostfix]
    public static void Postfix() => MapLoadTracer.TraceWithCallers("LOADING-WINDOW lowered");
}
```

### Excerpt: `Main/Features/MapLoadDiagnostics/MapLoadTracer.cs:27-31` and `:62-82` (the doc comment at `:57-61` is omitted)

```csharp
    private static IModLogger _logger;
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static int _seq;

    public static void Initialize(IModLogger logger) => _logger = logger;
...
    public static void TraceWithCallers(string evt, int frames = 10)
    {
        var logger = _logger;
        if (logger == null) return;
        try
        {
            var sb = new StringBuilder();
            // Skip frame 0 (this method) and frame 1 (the patch postfix itself).
            var st = new StackTrace(fNeedFileInfo: false);
            var count = Math.Min(frames + 2, st.FrameCount);
            for (int i = 2; i < count; i++)
            {
                var m = st.GetFrame(i)?.GetMethod();
                if (m == null) continue;
                if (sb.Length > 0) sb.Append(" < ");
                sb.Append(m.DeclaringType?.Name ?? "?").Append('.').Append(m.Name);
            }
            Trace(evt, "callers: " + (sb.Length > 0 ? sb.ToString() : "<none>"));
        }
        catch { Trace(evt, "callers: <unavailable>"); }
    }
```

`Trace` (`:42-55`) formats `[MapLoad] #{n} t={ms}ms {evt} :: {detail}` and calls `logger.LogInfo(line)`.
**Load-bearing:** `TraceWithCallers` skips exactly two frames, assuming it is called directly from the
patch postfix. Keep the `MapLoadTracer.TraceWithCallers(...)` call **inside the Postfix body**; never
move it into the gate class or another helper, or the caller chain loses a real frame.

### Excerpt: `Main/Core/Logging/FileLogger.cs:85-95` (why each line is expensive)

```csharp
    public void LogInfo(string message) => Enqueue("INFO", message, durable: true);
    public void LogDebug(string message) => Enqueue("DEBUG", message, durable: false);
...
    private void Enqueue(string level, string message, bool durable)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _queue.Enqueue($"[{timestamp}] [{level}] {message}");
        if (durable) Drain();
    }
```

`Drain` (`:99-136`) takes `lock (_writeLock)`, writes every queued line and calls `_logFile.Flush()`
(`:124`) on the calling thread, which here is the game's UI thread.

### Excerpt: `Main/SubModule.cs:393-410` at `b2e387db` (read with `git show b2e387db:Main/SubModule.cs`)

```csharp
            var mapLoadLogger = IoC.Resolve<IModLogger>();
            Features.MapLoadDiagnostics.Hooks.Campaign_RealTick_MapLoad_Patch.Initialize(
                IoC.Resolve<Features.MapLoadDiagnostics.IMapLoadHeartbeatService>(), mapLoadLogger);
            Features.MapLoadDiagnostics.MapLoadTracer.Initialize(mapLoadLogger);
            _harmony.PatchCategory("Patch89_MapLoadDiagnostics");
...
            foreach (var traceCategory in new[]
            {
                "Patch89_MapLoadDiagnostics_Lifecycle",
                "Patch89_MapLoadDiagnostics_MapScreen",
                "Patch89_MapLoadDiagnostics_SceneReady",
            })
            {
                try { _harmony.PatchCategory(traceCategory); }
```

Harmony's `PatchCategory` applies every `[HarmonyPrefix]` and `[HarmonyPostfix]` in a class carrying
the category attribute, so a Prefix added to `LoadingWindow_Disable_Patch` is applied by the existing
line 410 with no SubModule change. The category is applied for every player with no MCM or debug gate.

### Engine facts (v1.5.3, from `pwsh tools/taom-src.ps1 path TaleWorlds.Engine.LoadingWindow`, cache file `C:\Users\mikew\.taom-src\v1.5.3\TaleWorlds.Engine.LoadingWindow.cs`)

Note: `taom-src path LoadingWindow` (short name) fails with "Type 'LoadingWindow' not found"; the
fully qualified name `TaleWorlds.Engine.LoadingWindow` works.

```csharp
public static class LoadingWindow
{
	public static bool IsLoadingWindowActive { get; private set; }            // line 5
	public static ILoadingWindowManager LoadingWindowManager { get; private set; }
...
	public static void DisableGlobalLoadingWindow()                           // lines 31-44
	{
		if (LoadingWindowManager != null)
		{
			if (IsLoadingWindowActive)
			{
				LoadingWindowManager.DisableLoadingWindow();
				Utilities.DisableGlobalLoadingWindow();
				Utilities.OnLoadingWindowDisabled();
			}
			IsLoadingWindowActive = false;
			Utilities.DebugSetGlobalLoadingWindowState(newState: false);
		}
	}

	public static void EnableGlobalLoadingWindow()                            // lines 46-58
	{
		if (LoadingWindowManager != null)
		{
			IsLoadingWindowActive = true;
			...
```

What this means for the fix:

- The only real work in `DisableGlobalLoadingWindow` happens when `IsLoadingWindowActive` was `true`
  on entry (and the manager is non-null). Every other call is a no-op lower.
- The method sets `IsLoadingWindowActive = false` unconditionally (line 41) before any postfix runs,
  so a postfix alone cannot tell a real lower from a no-op. The pre-call value must be captured in a
  **Prefix** and handed to the Postfix through Harmony's `__state` parameter.
- A real lower is therefore exactly: flag `true` before the call AND `false` after it. If the manager
  is null the flag stays `true` after the call; that is not a lower and must not be traced.
- `IsLoadingWindowActive` is a plain managed auto-property with an empty static constructor on the
  class (lines 9-11), so reading it is cheap and has no native call.
- Callers, proven by the log's caller chains and by
  `SandBox.GauntletUI.GauntletPartyScreen.OnFrameTick` (decompiled lines 113-119, calls
  `LoadingWindow.DisableGlobalLoadingWindow()` unconditionally): `GauntletPartyScreen.OnFrameTick`,
  `MBInitialScreenBase.OnFrameTick` (main menu) and `BodyGeneratorView.OnTick` (character creation).

### Measured log evidence (desktop, read only, `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\Logs`)

- `taom_debug_2026-09-23_13-43-40.log`: 83,803,347 bytes, 265,061 lines;
  `grep -c "LOADING-WINDOW lowered"` = 262,763; `grep -c "LOADING-WINDOW raised"` = 8. Lowered lines
  that directly follow a raised line (the real transitions this plan keeps) = 7 (`grep "LOADING-WINDOW" <log> | awk '/raised/{up=1;next} /lowered/{if(up){n++;up=0}} END{print n}'`).
- `taom_debug_2026-09-20_10-06-26.log`: 1,158,497,918 bytes; 4,095,062 lowered lines, main menu open
  about three hours.
- Typical per-frame line: `[INFO] [MapLoad] #5 t=16687ms LOADING-WINDOW lowered :: callers: MBInitialScreenBase.OnFrameTick < ScreenBase.FrameTick < ...`

You do not need to read these logs; they are the reason for the plan, not a verification step.

### Harmony facts

- The `Prefix(out bool __state)` / `Postfix(bool __state)` pair is established TAOM precedent:
  `Main/Features/EconomyDiagnostics/Hooks/TownGoldFlowTagPatches.cs:34-38`:

  ```csharp
        [HarmonyPrefix]
        public static void Prefix(out bool __state) => __state = TownGoldFlowScope.TryEnter(TownGoldFlow.DailyMint);

        [HarmonyPostfix]
        public static void Postfix(bool __state) => TownGoldFlowScope.Exit(__state);
  ```

  The parameter name must be exactly `__state` (two underscores, lower case); Harmony matches it by
  name. `TAOM.Tests/Migration/HarmonyFieldInjectionNamingTests.cs:37` already lists `__state` as a
  legal injection name.
- If another mod's prefix on the same method skips the original, our Postfix sees the flag still
  `true`, the gate returns `false`, and nothing is logged. That is the safe direction (fewer lines).
  Whether Harmony 2.4.2 still runs our Prefix after another prefix returned `false` is UNVERIFIED
  and does not matter: an un-run Prefix leaves `__state` at its default `false`, which also logs nothing.

### Conventions that bind this change

- **ADR-002 (thin entry points, under 150 lines)**: a Harmony patch holds no logic; it reads the
  engine at the boundary and delegates the decision. The rewritten patch file stays under 60 lines.
- **ADR-007 (adapters)**: services and decision code never take sealed TaleWorlds types. The gate
  takes two `bool`s; only the patch reads `LoadingWindow.IsLoadingWindowActive`. No adapter is needed
  for one static bool read inside an entry point.
- **ADR-008 (testability)**: services and decision code must be 100% unit testable without game
  initialization (no TaleWorlds statics inside them), which is why the gate takes plain `bool`s.
  Entry points are exempt, but this plan tests the Postfix too because it is cheap.
- **ADR-003 / ADR-004 / ADR-005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **`.claude/rules/csharp-architecture.md`**: "HarmonyPatch ... THIN (<150 lines, no logic)", "Convert
  at boundary: adapt sealed types in the entry point, not deep in services".
- **Pure static decision precedent**: `MapLoadTracer` itself is a static class used from patches
  ("resolving through IoC is forbidden" in patches on engine lifecycle methods, its doc comment
  `:20-22`). The gate follows the same static shape; do not register it in IoC.
- **Test style**: MSTest + NSubstitute, one `[TestClass]` per file, file-scoped namespace. Structural
  pattern: `TAOM.Tests/Features/MapLoadDiagnostics/MapLoadDiagnosticsBehaviorTests.cs` (whole file):

  ```csharp
  using Microsoft.VisualStudio.TestTools.UnitTesting;
  using NSubstitute;
  using TAOM.Features.MapLoadDiagnostics;

  namespace TAOM.Tests.Features.MapLoadDiagnostics;

  [TestClass]
  public class MapLoadDiagnosticsBehaviorTests
  {
      [TestMethod]
      public void OnSessionLaunched_ResetsTheHeartbeatBaseline()
      {
          var heartbeat = Substitute.For<IMapLoadHeartbeatService>();
          var sut = new MapLoadDiagnosticsBehavior(heartbeat);

          sut.OnSessionLaunched(null);

          heartbeat.Received(1).ResetForNewSession();
      }
  }
  ```
- `TaleWorlds.*.dll` are copied into the test bin (`TAOM.Tests/Migration/GameAssemblies.cs:12-14`
  says so), so a test can reference `TaleWorlds.Engine.LoadingWindow` directly. MSTest parallelization
  is not enabled in `TAOM.Tests` (`git grep Parallelize -- TAOM.Tests` finds nothing), so a test may
  set `MapLoadTracer`'s static logger as long as it resets it in `[TestCleanup]`. No existing test
  touches `MapLoadTracer` (`git grep MapLoadTracer -- TAOM.Tests` finds nothing at `b2e387db`).

### Decisions already taken (do NOT change them)

- **Keep `LogInfo` (durable) for all `[MapLoad]` traces.** Routing the tracer through `LogDebug`
  was considered and rejected for this plan: the trace exists to survive a hang or native crash
  (FileLogger's header comment, `FileLogger.cs:8-11`: DEBUG lines can be lost when the process dies),
  and after this fix the loading-window lines are rare, so durability costs nothing measurable.
- **Do not touch `LoadingWindow_Enable_Patch`.** Raises were 8 in 35 minutes; they are the
  evidence the feature exists for.
- **Do not add a static "last raised" flag.** The engine's own `IsLoadingWindowActive`, captured in a
  Prefix, is authoritative; a TAOM mirror flag could desynchronise.
- **Do not change `FileLogger` retention, crash-bundle log copying or the log-tail collector.** Those
  are separate findings.

### Test baseline at `b2e387db` (so you do not chase known failures)

The orchestrator's run at `b2e387db`: 10,239 tests, 10,235 passed, 2 failed, 2 not executed. The 2
failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (Elk tests) and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (`AnimaliaMountWiringTests`). Both read the
live, unversioned Armory install that another session is editing right now; neither is caused by or
related to this plan, so they may pass, fail, or change while you work. Do not investigate or fix
them. The 2 not executed are deliberate `[Ignore]`s (`WargAttack_FastWarg_InvokesRunningAttack`,
`WargAttack_SlowWarg_InvokesStandingAttack`). The rule for every later full run: the failures must be
a subset of those 2 Armory tests plus whatever Step 0 recorded.

## Commands you will need

Every command runs in Git Bash and starts with `cd E:/repos/wt-012-loading-window-trace && `. Never
run `./build.ps1`, never launch the game, never write under `E:\Steam\` or `E:\repos\TAOM\`.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `cd E:/repos/wt-012-loading-window-trace && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test (full) | `cd E:/repos/wt-012-loading-window-trace && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | a `Total tests:` line; failures a subset of the baseline rule above. About 10,239 tests after a cold build: call the Bash tool with `timeout: 600000` (or `run_in_background: true` and wait); the default 120 s is too short (exact duration UNVERIFIED) |
| Test (filtered) | `cd E:/repos/wt-012-loading-window-trace && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | `Failed: 0`, `Skipped: 0` |
| Data | `cd E:/repos/wt-012-loading-window-trace && python tools/validate_moduledata.py \| grep "error(s)"` | one summary line such as `0 error(s), 1591 warning(s)`; the error count is no higher than the Step 0 value (this plan changes no data, but the validator also reads the live Armory that another session is editing) |
| Docs | `cd E:/repos/wt-012-loading-window-trace && python tools/lint_docs.py \| grep "Dead links:"` | `- Dead links: **0**` |
| Tree guard | `git -C E:/repos/wt-012-loading-window-trace rev-parse --abbrev-ref HEAD` | `plan-012-loading-window-trace` |

## Scope

Every path below is relative to W (`E:\repos\wt-012-loading-window-trace\`).

**In scope** (the only files you may modify or create):

- `Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs` (create)
- `Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` (Disable class and the
  header comment's second `<para>` only)
- `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs` (create)
- `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs` (create)
- `docs/features/map-load-diagnostics.md` (lines 43-49 only)
- `docs/reference/harmony-patch-registry.md` (line 1024, one phrase only)

**Single-owner files**: `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`:
**recommend, don't edit.** None needs a change: the Prefix is applied by the existing
`_harmony.PatchCategory(traceCategory)` at `Main/SubModule.cs:410`, the gate is static (no IoC
registration), and SDK-style `Main/TAOM.csproj` picks up the new `.cs` file by globbing. If the build
reports the new file is not compiled, STOP and report the line
`<Compile Include="Features\MapLoadDiagnostics\LoadingWindowTraceGate.cs" />` for the owner to add.

**Out of scope** (do NOT touch, even though they look related):

- `Main/Features/MapLoadDiagnostics/MapLoadTracer.cs` (the two-frame skip must stay as is).
- `Main/Core/Logging/FileLogger.cs`, `Main/Features/CrashReport/**` (log retention and bundle size are
  separate findings).
- `LoadingWindow_Enable_Patch` (in the same file) and every other `Patch89_*` patch.
- `CHANGELOG.md` and `docs/ai-includes/orientation.md`: another session has uncommitted edits to them
  in the main checkout. Give the CHANGELOG text in your final report instead (Step 6).
- `docs/reference/taleworlds-api-snapshot/patch-targets.md`: its row is per class and target
  (`LoadingWindow_Disable_Patch` -> `DisableGlobalLoadingWindow()`), which this plan does not change.

## Git workflow

- **Worktree and branch** (Step 0):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-012-loading-window-trace -b plan-012-loading-window-trace b2e387db`.
  This creates W and the branch; it does not modify the main checkout's files.
- **Before every commit**, run both and confirm the exact results:
  1. The Tree guard prints `plan-012-loading-window-trace`.
  2. `git -C E:/repos/wt-012-loading-window-trace diff --cached --name-only` lists exactly the files
     the step names, nothing else.
- **Stage and commit** with `git -C E:/repos/wt-012-loading-window-trace add <path> <path>` then
  `git -C E:/repos/wt-012-loading-window-trace commit -m "<subject>" -m "<body>"`. Explicit paths
  only; never `git add -A`, `git add .` or `git commit -a`.
- **Commit subject:** `<type>(map-load): v<version> - <description>`, at most 72 characters, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` (it reads `v2.0.30` at
  `b2e387db`, line 6; re-read it). Body wrapped at 72, prose without em or en dashes. After each
  commit, `git -C E:/repos/wt-012-loading-window-trace log -1 --format=%b | awk 'length($0)>72{n++} END{print n+0}'`
  prints `0`.
- **No AI attribution trailer** (no `Co-Authored-By`). Optional trailers: `Not-tested:`, `Research:`.
- **Never push**, never open a PR, never merge. You commit C# without `/deep-review` because you
  cannot invoke skills; the orchestrator runs `/deep-review` on this branch before any merge
  (the CLAUDE.md gate).
- Check each subject length:
  `git -C E:/repos/wt-012-loading-window-trace log -1 --format=%s | python -X utf8 -c "import sys; s=sys.stdin.read().strip(); print(len(s), s)"`
  prints a number at most 72.

## Steps

### Step 0: Create the worktree and record the baseline

1. Run the drift check (top of this file) and note its output.
2. Create the worktree (command in Git workflow). If it fails because the directory or the branch
   already exists, STOP (see STOP conditions). Run the Tree guard: it prints `plan-012-loading-window-trace`.
3. Confirm the excerpt. `git -C E:/repos/wt-012-loading-window-trace log -1 --format=%h` prints
   `b2e387db`, and
   `cd E:/repos/wt-012-loading-window-trace && sed -n 35p Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs | grep -c 'public static void Postfix() => MapLoadTracer.TraceWithCallers("LOADING-WINDOW lowered");'`
   prints `1`.
4. Confirm the engine fact. `taom-src` prints a Windows path, so keep the quotes exactly:
   `cd E:/repos/wt-012-loading-window-trace && sed -n '35p;41p' "$(pwsh tools/taom-src.ps1 path TaleWorlds.Engine.LoadingWindow 2>/dev/null | tail -1)" | tr -d ' \t\r'`
   prints exactly two lines, `if(IsLoadingWindowActive)` then `IsLoadingWindowActive=false;` (the
   guarded work, then the unconditional clear, as in the engine excerpt above).
5. Record the data baseline: run the Data command and note the error count (it was `0 error(s)` on
   2026-09-23).
6. Run Test (full) with `timeout: 600000`. Write down every failing test name; this is the Step 0 list.
   - Any failure in a class under `TAOM.Tests/Features/MapLoadDiagnostics/` or in
     `HarmonyFieldInjectionNamingTests` / `HarmonyPatchBindingTests`: STOP.
   - Any other failure beyond the two Armory tests: record it, do not fix it, continue.

**Verify**: the Tree guard prints the branch name; item 3 prints `b2e387db` and `1`; item 4 prints
the two lines; item 5 printed an error count; the full run printed a `Total tests:` line and you have
the Step 0 list.

### Step 1 (RED): Pin the transition decision

Create `E:\repos\wt-012-loading-window-trace\TAOM.Tests\Features\MapLoadDiagnostics\LoadingWindowTraceGateTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MapLoadDiagnostics;

namespace TAOM.Tests.Features.MapLoadDiagnostics;

/// <summary>
/// The engine lowers the loading window from the main menu, party screen and character creation on
/// every frame, and clears the flag whether or not it was up. Only a true-to-false change is a real
/// lower worth a caller chain; everything else is the per-frame no-op that filled 84 MB logs.
/// </summary>
[TestClass]
public class LoadingWindowTraceGateTests
{
    [TestMethod]
    public void IsRealLower_WindowWasUpAndIsNowDown_ReturnsTrue()
        => Assert.IsTrue(LoadingWindowTraceGate.IsRealLower(wasActive: true, isActiveNow: false));

    [TestMethod]
    public void IsRealLower_WindowWasAlreadyDown_ReturnsFalse()
        => Assert.IsFalse(LoadingWindowTraceGate.IsRealLower(wasActive: false, isActiveNow: false));

    [TestMethod]
    public void IsRealLower_WindowWasUpAndStayedUp_ReturnsFalse()
        => Assert.IsFalse(LoadingWindowTraceGate.IsRealLower(wasActive: true, isActiveNow: true));

    [TestMethod]
    public void IsRealLower_WindowWasDownAndIsNowUp_ReturnsFalse()
        => Assert.IsFalse(LoadingWindowTraceGate.IsRealLower(wasActive: false, isActiveNow: true));
}
```

The four cells are the whole (before x after) table. Cell 2 is the per-frame case this plan removes;
cell 3 is a null `LoadingWindowManager` or another mod's prefix skipping the original.

**Verify**: Test (filtered) with `LoadingWindowTraceGateTests` fails to compile with `CS0103`
(`The name 'LoadingWindowTraceGate' does not exist in the current context`). That compile failure is
the RED. Any other error: STOP.

### Step 2 (GREEN): Add the gate

Create `E:\repos\wt-012-loading-window-trace\Main\Features\MapLoadDiagnostics\LoadingWindowTraceGate.cs`:

```csharp
namespace TAOM.Features.MapLoadDiagnostics;

/// <summary>
/// Decides whether a call to <c>LoadingWindow.DisableGlobalLoadingWindow</c> actually lowered the
/// window. The engine calls it every frame from several screens' frame ticks and clears
/// <c>IsLoadingWindowActive</c> unconditionally, so only the before and after values together can
/// tell a real lower from a no-op. Pure, so the patch stays a thin boundary (ADR-002).
/// </summary>
public static class LoadingWindowTraceGate
{
    public static bool IsRealLower(bool wasActive, bool isActiveNow) => wasActive && !isActiveNow;
}
```

**Verify**: Build exits 0 with `0 Error(s)`; Test (filtered) with `LoadingWindowTraceGateTests`
reports `Passed: 4`, `Failed: 0`.

Commit (paths: the two files from Steps 1 and 2), for example subject
`perf(map-load): v2.0.30 - add the loading-window real-lower gate`.

### Step 3 (RED): Pin the patch's Prefix and Postfix

Create `E:\repos\wt-012-loading-window-trace\TAOM.Tests\Features\MapLoadDiagnostics\LoadingWindowDisablePatchTests.cs`:

```csharp
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TAOM.Core.Logging;
using TAOM.Features.MapLoadDiagnostics;
using TAOM.Features.MapLoadDiagnostics.Hooks;

namespace TAOM.Tests.Features.MapLoadDiagnostics;

/// <summary>
/// The Disable postfix must log only a real lower. The engine clears the flag before any postfix
/// runs, so the pre-call value travels from a Prefix through Harmony's <c>__state</c>; Harmony binds
/// that parameter by exact name, so its shape is pinned here as well as its behaviour.
/// </summary>
[TestClass]
public class LoadingWindowDisablePatchTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        MapLoadTracer.Initialize(_logger);
    }

    [TestCleanup]
    public void Cleanup() => MapLoadTracer.Initialize(null!);

    [TestMethod]
    public void Prefix_CapturesTheFlagIntoAnOutBoolNamedState()
    {
        var prefix = typeof(LoadingWindow_Disable_Patch).GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(prefix, "LoadingWindow_Disable_Patch has no public static Prefix.");
        var parameters = prefix!.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.AreEqual("__state", parameters[0].Name);
        Assert.IsTrue(parameters[0].IsOut, "__state must be an out parameter on the Prefix.");
        Assert.AreEqual(typeof(bool).MakeByRefType(), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Postfix_TakesTheCapturedStateByValue()
    {
        var postfix = typeof(LoadingWindow_Disable_Patch).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(postfix, "LoadingWindow_Disable_Patch has no public static Postfix.");
        var parameters = postfix!.GetParameters();
        Assert.AreEqual(1, parameters.Length);
        Assert.AreEqual("__state", parameters[0].Name);
        Assert.AreEqual(typeof(bool), parameters[0].ParameterType);
    }

    [TestMethod]
    public void Postfix_WhenTheWindowWasAlreadyDown_LogsNothing()
    {
        LoadingWindow_Disable_Patch.Postfix(__state: false);

        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers()
    {
        // Never raised in the test host: no LoadingWindowManager exists, so Enable cannot set it.
        Assert.IsFalse(LoadingWindow.IsLoadingWindowActive, "precondition: engine flag is down");

        LoadingWindow_Disable_Patch.Postfix(__state: true);

        _logger.Received(1).LogInfo(Arg.Is<string>(s =>
            s.Contains("LOADING-WINDOW lowered") && s.Contains("callers:")));
    }
}
```

**Verify**: Test (filtered) with `LoadingWindowDisablePatchTests` fails to compile with `CS1501`
(`No overload for method 'Postfix' takes 1 arguments`) on the two `Postfix(__state: ...)` calls, or
`CS1739` (`does not have a parameter named '__state'`). That is the RED. Any other error: STOP.

### Step 4 (GREEN): Capture the flag in a Prefix and trace only a real lower

Edit `E:\repos\wt-012-loading-window-trace\Main\Features\MapLoadDiagnostics\Hooks\LoadingWindow_Transitions_Patch.cs`.

(a) Replace the header comment's second paragraph (currently lines 16-19):

```csharp
/// <para>
/// Caller chains are affordable here because these fire a handful of times per session, not per
/// frame.
/// </para>
```

with:

```csharp
/// <para>
/// Caller chains are affordable only on real transitions. Raises are rare, but the engine calls
/// <c>DisableGlobalLoadingWindow</c> on every frame of the main menu, the party screen and character
/// creation, and clears the flag whether or not the window was up. The Disable patch therefore
/// captures the flag in a Prefix and traces only a true-to-false change (otherwise one no-op lower
/// per rendered frame, each a stack walk and a flushed log line).
/// </para>
```

(b) Replace the whole `LoadingWindow_Disable_Patch` class (currently lines 29-36) with:

```csharp
/// <summary>Counterpart to <see cref="LoadingWindow_Enable_Patch"/>; its absence is the symptom.</summary>
[HarmonyPatch(typeof(LoadingWindow), nameof(LoadingWindow.DisableGlobalLoadingWindow))]
[HarmonyPatchCategory("Patch89_MapLoadDiagnostics_Lifecycle")]
public static class LoadingWindow_Disable_Patch
{
    [HarmonyPrefix]
    public static void Prefix(out bool __state) => __state = LoadingWindow.IsLoadingWindowActive;

    // TraceWithCallers skips two frames (itself and this Postfix), so it must be called from here
    // directly, never through a helper.
    [HarmonyPostfix]
    public static void Postfix(bool __state)
    {
        if (LoadingWindowTraceGate.IsRealLower(__state, LoadingWindow.IsLoadingWindowActive))
            MapLoadTracer.TraceWithCallers("LOADING-WINDOW lowered");
    }
}
```

`LoadingWindowTraceGate` lives in namespace `TAOM.Features.MapLoadDiagnostics`, the parent of this
file's `TAOM.Features.MapLoadDiagnostics.Hooks`, so no new `using` is needed (the file already calls
`MapLoadTracer` from that namespace the same way). Leave `LoadingWindow_Enable_Patch` untouched.

**Verify**:
- Build exits 0 with `0 Error(s)`.
- Test (filtered) with `LoadingWindowDisablePatchTests` reports `Passed: 4`, `Failed: 0`.
- Test (filtered) with `LoadingWindowTraceGateTests` still reports `Passed: 4`.
- `cd E:/repos/wt-012-loading-window-trace && wc -l Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` prints a number under 60.
- `cd E:/repos/wt-012-loading-window-trace && git diff b2e387db -- Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs | grep "^[-+]" | grep -c "LOADING-WINDOW raised\|class LoadingWindow_Enable_Patch"` prints `0` (the Enable class did not change).

If `Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers` or
`Postfix_WhenTheWindowWasAlreadyDown_LogsNothing` throws `FileNotFoundException`,
`TypeLoadException` or `TypeInitializationException` mentioning `TaleWorlds.Engine`: add to the test
class, as `TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs` does,

```csharp
    [ClassInitialize]
    public static void Init(TestContext _) => TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();
```

and re-run once. If it still throws, STOP (see STOP conditions).

Commit (paths: the patch file and `LoadingWindowDisablePatchTests.cs`), for example subject
`perf(map-load): v2.0.30 - log a loading-window lower only when it drops`
(71 characters; check with the length command).

### Step 5: Make the docs describe the new behaviour

(a) `E:\repos\wt-012-loading-window-trace\docs\features\map-load-diagnostics.md`, lines 43-49 read:

```text
**A lifecycle trace**, each line carrying a sequence number and a millisecond offset so the log reads
as a timeline: every game-state push, pop, clean and initialize with the resulting stack; the map
state and map screen seams bracketed ENTER/EXIT; the first completed map frame; and every raise and
lower of the global loading window **with its managed caller chain**.

The caller chain is what solved it. It is affordable because those transitions fire a handful of
times, unlike the per-frame work around them.
```

Replace them with:

```text
**A lifecycle trace**, each line carrying a sequence number and a millisecond offset so the log reads
as a timeline: every game-state push, pop, clean and initialize with the resulting stack; the map
state and map screen seams bracketed ENTER/EXIT; the first completed map frame; and every raise of
the global loading window, and every lower that actually took it down, **with its managed caller
chain**.

The caller chain is what solved it. It is affordable because real transitions fire a handful of
times. The engine also calls `LoadingWindow.DisableGlobalLoadingWindow()` on every frame of the main
menu, the party screen and character creation, whether or not the window is up, so the Disable
patch captures `IsLoadingWindowActive` in a Prefix and traces only a true-to-false change
(`LoadingWindowTraceGate`). In v2.0.29 and v2.0.30, before this guard, those no-op lowers wrote one
line per rendered frame (up to about 360 a second): 84 MB in a 35-minute session, 1.16 GB with the
main menu left open for three hours.
```

(b) `E:\repos\wt-012-loading-window-trace\docs\reference\harmony-patch-registry.md`, line 1024: replace
the phrase `and every raise and lower of the global loading window with its managed caller chain.`
with `and every raise of the global loading window, and every lower that actually took it down (a Prefix captures IsLoadingWindowActive, because the engine calls the lower every frame on several screens), with its managed caller chain.`
Change nothing else on that line.

**Verify**:
- `cd E:/repos/wt-012-loading-window-trace && grep -c "every lower that actually took it down" docs/features/map-load-diagnostics.md docs/reference/harmony-patch-registry.md` prints `1` for each file.
- `cd E:/repos/wt-012-loading-window-trace && git diff b2e387db -- docs/ | python -X utf8 -c "import sys; print(sum(1 for l in sys.stdin if l.startswith('+') and ('\u2013' in l or '\u2014' in l)))"`
  prints `0` (no em or en dash added; `grep -P` with `\x{2014}` fails in Git Bash, so use this form).
- The Docs command prints `- Dead links: **0**`.

Commit (paths: the two docs), for example subject
`docs(map-load): v2.0.30 - loading-window lowers trace on real drops`.

### Step 6: Final verification and report

Run every Done-criteria command. Then put these in your final report (do not write them to files):

- **CHANGELOG entry** for the orchestrator to add (no dashes): "The map-load diagnostics no longer
  write a log line on every frame of the main menu, the party screen and character creation. The
  engine lowers the loading window on each of those frames even when it is already down, and TAOM
  traced every call with a stack walk and a flushed write (84 MB in a 35-minute session, 1.16 GB
  with the main menu left open for three hours). A lower is now traced only when the window was
  actually up."
- **Lesson** to propose for `docs/reviews/lessons/harmony-il.md` (the orchestrator appends it): "An
  engine method that clears state unconditionally cannot be split into real and no-op calls from a
  postfix; capture the pre-call value in a Prefix through `__state` (plan 012,
  `LoadingWindowDisablePatchTests`)."
- The commit hashes and subjects, the drift-check output, the Step 0 failure list, the final test
  counts and failure names, and the owed in-game check (Maintenance notes).

## Test plan

- **New tests** (8), all in `TAOM.Tests/Features/MapLoadDiagnostics/`:
  - `LoadingWindowTraceGateTests` (4): the full (before x after) table of `IsRealLower`:
    (true,false) true; (false,false) false, the per-frame case; (true,true) false, null manager or a
    skipped original; (false,true) false.
  - `LoadingWindowDisablePatchTests` (4): Prefix shape (`out bool __state`), Postfix shape
    (`bool __state`), Postfix with `false` logs nothing, Postfix with `true` and the engine flag down
    logs exactly one `LogInfo` containing `LOADING-WINDOW lowered` and `callers:`.
- **Structural pattern**: `TAOM.Tests/Features/MapLoadDiagnostics/MapLoadDiagnosticsBehaviorTests.cs`
  (MSTest + NSubstitute, file-scoped namespace).
- **Untestable here** (commit trailer `Not-tested:`): that Harmony applies the Prefix and hands
  `__state` to the Postfix at runtime inside the game, and the frame-time saving. Both need an
  in-game session.
- **Verification**: Test (full) completes with failures a subset of the baseline rule, and the run
  includes the 8 new tests passing.

## Done criteria

ALL must hold (every command starts with `cd E:/repos/wt-012-loading-window-trace && `):

- [ ] Build exits 0 with `0 Error(s)`.
- [ ] Test (full) finishes with failures a subset of {the 2 Armory tests} plus the Step 0 list.
- [ ] Test (filtered) with `LoadingWindowTraceGateTests` prints `Failed: 0` and `Passed: 4`, and
      Test (filtered) with `LoadingWindowDisablePatchTests` prints `Failed: 0` and `Passed: 4`
      (the full run's console shows only totals, so these two runs are the per-class proof).
- [ ] `grep -n "HarmonyPrefix" Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` prints exactly one line.
- [ ] `grep -n "public static void Postfix() => MapLoadTracer.TraceWithCallers(\"LOADING-WINDOW lowered\")" Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` prints nothing.
- [ ] `grep -n "LoadingWindowTraceGate.IsRealLower" Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs` prints exactly one line.
- [ ] `git -C E:/repos/wt-012-loading-window-trace diff --name-only b2e387db` lists exactly these six paths:
      `Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs`,
      `Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs`,
      `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs`,
      `TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs`,
      `docs/features/map-load-diagnostics.md`, `docs/reference/harmony-patch-registry.md`.
- [ ] `git -C E:/repos/wt-012-loading-window-trace status --porcelain` prints nothing (all committed).
- [ ] Every commit subject is at most 72 characters (length command) and no commit carries `Co-Authored-By`
      (`git -C E:/repos/wt-012-loading-window-trace log b2e387db..HEAD --format=%B | grep -c Co-Authored-By` prints `0`).
- [ ] The Data command's error count is no higher than the Step 0 value; the Docs command prints
      `- Dead links: **0**`.
- [ ] `git -C E:/repos/wt-012-loading-window-trace log b2e387db..HEAD --format=%b | awk 'length($0)>72{n++} END{print n+0}'` prints `0`.

## STOP conditions

Stop and report back (do not improvise) if:

- The Disable class or the engine's `DisableGlobalLoadingWindow` body does not match the excerpts
  (Step 0, items 3 and 4). In particular, if the engine no longer clears `IsLoadingWindowActive`
  unconditionally, or the property is renamed or no longer public, the Prefix design must be re-planned.
- The Step 1 or Step 3 RED fails with anything other than the named compile errors (for example the
  Step 3 file already compiles, which would mean someone changed the patch).
- Reading `LoadingWindow.IsLoadingWindowActive` in the test host still throws after adding the
  `GameAssemblies.EnsureLoaded()` class initializer. Report the exception; do not delete or
  `[Ignore]` the two behavioural tests, and do not make them `Assert.Inconclusive`.
- `Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers` fails its precondition
  (`IsLoadingWindowActive` is `true` in the test host): some other test raised the engine flag;
  report which, do not reset the engine flag by reflection.
- The fix appears to need `MapLoadTracer.cs`, `FileLogger.cs`, `Main/SubModule.cs`, `Main/IoC.cs`,
  `Main/TAOM.csproj` or any file outside Scope.
- `HarmonyFieldInjectionNamingTests`, `HarmonyPatchBindingTests` or any other test under
  `TAOM.Tests/Migration/` newly fails after Step 4 (the patch shape broke a binding gate).
- A step's verification fails twice after a reasonable fix attempt.
- `git worktree add` fails because `E:/repos/wt-012-loading-window-trace` or the branch
  `plan-012-loading-window-trace` already exists (for example on a re-run). Report it; do not delete
  or reuse either.
- A Read, Edit, Write or `cd` into W is denied by permissions (W is outside the project directory).
- A commit is denied by a hook. The PreToolUse commit hooks read the main checkout's index, not W's,
  so another session's staging there can deny your commit. Report the hook's message verbatim.
- In each of the last three cases, never fall back to editing, staging or committing under
  `E:\repos\TAOM`.

## Maintenance notes

- **Owed in-game check** (label `triage-needs-ingame` when the issue closes): launch, sit on the main
  menu for one minute, open the party screen for one minute, start and load a campaign. In the new
  `taom_debug_*.log`, `grep -c "LOADING-WINDOW lowered"` should be a single-digit number, each such
  line should follow a `LOADING-WINDOW raised` line, and the file should stay in the hundreds of KB
  rather than tens of MB. A guess, not something to test against: the first element of a lowered
  line's caller chain may change (for example to Harmony's replacement method, a name like
  `DisableGlobalLoadingWindow_PatchN`) because the Postfix is no longer a one-line expression and
  JIT inlining may differ. A different first frame is not a failure; the check is that the chain
  still names the real caller further along.
- **What a reviewer should probe**: the exact `__state` spelling and `out` modifier (Harmony binds by
  name; a typo silently gives the Postfix a default `false` and the trace goes quiet); that
  `TraceWithCallers` is still called directly from the Postfix; that the Enable patch is unchanged.
- **Interactions**: any future patch on `LoadingWindow.DisableGlobalLoadingWindow` (the Bannerlord
  Coop mod has a prefix on it, per `docs/raw/bannerlordcoop/verifier-verdicts.json`) can leave the
  flag `true` after the call; the gate then logs nothing, which is correct.
- **Deferred, separate findings**: `FileLogger` keeps 30 logs with no size cap, and
  `Main/Features/CrashReport/Rendering/CrashBundleWriter.cs:46` zips the whole live log into every
  crash bundle; a one-off per-frame logger elsewhere would still produce gigabyte logs. Routing all
  `[MapLoad]` traces through `LogDebug` was rejected here (durability is the point of the trace).
