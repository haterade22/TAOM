# Plan 107: Make crash capture thread-aware on 16 allowlisted shims, keep throw sites, and take PatchShield off the shims and the per-call lookup (plans 006, 007, 034)

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. The work is split into Parts A, B and C; each Part ends green with its
> own done criteria, so a Part may end a session. Do not edit `plans/README.md`: every WP branch
> shares it, so the integrator records this plan's status and test count when merging. Put the
> numbers listed under "Hand-off report" in your final message.
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Dependencies/Foundation Dependencies/SubModule.cs Main/Features/CrashReport Main/SubModule.cs Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs Main/Features/AdvancedCombat/CustomAttacksUtils.cs TAOM.Tests/Features/CrashReport TAOM.Tests/Infrastructure/Dependencies TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Migration/HarmonyPatchBindingTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadStallWatchdogTests.cs TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs docs/features/crash-report.md docs/features/mcm.md docs/features/arena.md docs/features/battle-load-diagnostics.md docs/migration/dr3-maintenance.md docs/reference/harmony-patch-registry.md docs/reference/engine/submodule-lifecycle-and-harmony.md docs/reference/taleworlds-api-snapshot .claude/rules/harmony-patches.md`
> Earlier WPs are expected to have changed some of these (Step 0.2 lists which). Compare the live
> files against "Current state" and STOP only on a real mismatch (Step 0.4).

## Status

- **Priority**: P1
- **Effort**: L (eight commits in three Parts; most code is transplanted from 1.5.x end states)
- **Risk**: MED. Changes TAOM.Dependencies (a release must ship both modules from one build, #371);
  edits the single-owner `Main/SubModule.cs`; the allowlist changes what happens to a throw inside
  231 engine callbacks (it reaches the engine instead of being swallowed).
- **Depends on**: WP106 (`plans/106-offthread-and-tick-safety.md`: Patch91 on
  `Mission.TickAgentsAndTeamsImp` from `f75288ebf`, and the launch-aware tick recovery). Through
  WP106 also WP101 to WP105; this plan needs WP104's `TryPatchCategory` (plan 009).
- **Category**: perf, bug (diagnostics and crash robustness)
- **Wave / lane**: wave 1, lane B
- **Executor**: laptop
- **Decisions applied**: D1 (no plan stages `CHANGELOG.md`), D2 (CrashReport is a 1.4.x hand-wired
  feature with no 1.5.x module, so it keeps its hand wiring), D7 (the co-op pair `6ce20f6ed` and
  `bd1ac8292` is not taken), D8 (subjects carry `v2.0.28`), D9 (no CI: every gate runs locally),
  D10, D17 (the tick-handshake remedy is WP106's; this plan restores no `tickCompleted`), D19 (git
  only through the Bash tool), D21 (`f75288ebf` is merged before this).
- **Decisions made in this plan** (under the maintainer's 2026-11-04 delegation; not questions):
  - **107-1** Adopt the 1.5.x 16-entry allowlist unchanged. `Mission_TickAgentsAndTeams`,
    `Mission_OnPreTick`, `Agent_OnDismount`, `Agent_OnMount` and `Agent_OnAgentAlarmedStateChanged`
    stay off it. Today the agent-tick shim's bridge swallows a throw from the agent tick and leaves
    `tickCompleted` false, so the next frame waits forever (V04 route 2). Off the list, the exception
    goes to native instead, and what native does with it is UNVERIFIED (V04). This is the deep
    dive's FREEZE-THREADING-01 fix (a). Restoring the flag after an agent-tick throw (V04's other
    option, the agent-tick half of fix (b)) is new work no line has (Maintenance notes).
    WP106 (#634) parks the off-thread callback writes that could throw there. E3 and E4 hold no
    capture or suppression line at all (V04).
  - **107-2** The battle-load stall watchdog's bundle request passes `offMainThread: true` (new work,
    deep-dive finding FREEZE-THREADING-05): no Mission or Campaign reads and no inquiry from the
    timer thread; the marker line and the phase log stay the primary signal.
  - **107-3** Part C ports `c79a58521`'s combat-log fix and creature-behaviour pin, because this WP
    owns that commit. Its Elk, Animalia, rider-blow (#643) and content parts are not ported; its
    harness half is WP103's.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: Port of #650 (plan 006), #651 (plan 007), #716 (plan 034) and #331 (the hot-layer rule
  plan 007 extends). New work (107-2 and the Part C test seam) has no issue: the orchestrator
  creates it before implementation lands.

## Why this matters

Every launch of the public 1.4.8 build logs `[CrashReport] Native2Managed: attached 247
Finalizer(s)` (E3, taom_debug 2026-11-01_20-00-00, line 4): TAOM wraps every static of the engine's
three `*CallbacksGenerated` classes, per-agent and per-hit callbacks included. From the first game
start PatchShield (TAOM.Dependencies) wraps the same shims again with a finalizer that binds
`__originalMethod`, which makes the patched method call `MethodBase.GetMethodFromHandle` on every
call (1.5.x plan 034 measured about 63 ns and 241 bytes allocated per call, 1,145 ns with threads
contending; unmeasured on 1.4.8). This laptop's diag.log (2026-09-02, a pre-port
1.4.x build) shows that pass attaching `+354 new`. Worse for freezes: a capture on the asynchronous
agent thread is treated as main-thread and runs the Mission and Campaign collectors and
`InformationManager.ShowInquiry` there, and E4 (lines 2265, 2266, 2986) shows TAOM mission callbacks
on worker threads on 1.4.8. Both MCM capture toggles are dead (read before MCM builds its settings),
four Patch37 finalizers can never fire, and every exception that crosses PatchShield or SaveShield
loses its throw site, merging distinct crashes under one signature (1.4.8 player bundle `2d446100`).
After this plan: 16 shims, thread-aware captures, live toggles, PatchShield off the shims and the
per-call lookup, throw sites kept, and no missing-API swallow that can skip the agent tick's
completion flag.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
  (`BANNERLORD_GAME_DIR`). Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101 plus every WP merged before this one (WP104 and
  WP106 among them).
- Worktree: `C:\Users\mikew\source\repos\taom-port\107`, branch
  `port148/107-crash-capture-and-patchshield`. It is outside the session's working directory, so a
  `cd` does not persist: start EVERY command with `cd /c/Users/mikew/source/repos/taom-port/107 &&`.
- Scratch files (commit messages, merge inputs): `C:\Users\mikew\source\repos\taom-port\107-tmp\`,
  outside the worktree so they can never be staged.
- `core.autocrlf=true` (system gitconfig): worktree text files are CRLF, `git show <sha>:<path> >
  <path>` writes LF, `git add` normalises both. "LF will be replaced by CRLF" warnings are noise.
- Python: `python` (or `/c/Python314/python.exe`).
- Evidence labels: E3 and E4 are two 1.4.8 player logs (`taom_debug` 2026-11-01_20-00-00 and
  20-16-56), not in the repo; every fact needed from them is quoted here. V04 is the tick-handshake
  report `plans/_audit/2026-11-04-port148/verify/V04-tick-handshake-and-start-swallow.md` in the
  main checkout (untracked, so absent from this worktree). You need neither to execute.

### Files and their 1.4.x state (`origin/bannerlord-1.4.5`)

| File | 1.4.x state |
|---|---|
| `Main/SubModule.cs` (single-owner) | Crash install `:161-177`, gated on two `CrashReportSettings.Instance?.X ?? true` reads. After WP104 the same block wraps the `Subscribe`/`AttachAll` pair in `if (TryPatchCategory("Patch37_CrashReport"))` (`998ae7e95:Main/SubModule.cs:206-224`); the gates are unchanged |
| `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs` | 247-shim sweep `:61-95`; bridge passes no thread verdict `:119-123`; its comment claims finalizers cost about zero unless something throws `:16-18` (wrong for Harmony 2.4.2) |
| `.../Hooks/Patch37_CrashReport.cs` | Nine finalizers; four on base virtuals no override runs through: `:45-52`, `:63-70`, `:104-111`, `:113-120` |
| `.../Hooks/AppDomainExceptionHook.cs` | Off-thread mark in `ex.Data` inside a swallowing catch `:65-69` (lost when `Data` is read-only, as for the preallocated OOM) |
| `.../Hooks/CrashReportPatchHelper.cs` | `HandleAndSwallow` `:29-53` hands back the raw exception on four paths |
| `.../CrashReportService.cs` | Suppression line on every occurrence `:108-115`; off-thread read from `Data` `:121`, `:161-171`; inquiry `:138-146` |
| `.../CrashReportSettings.cs` | The two capture toggles lack `RequireRestart = false`; their hints promise restart semantics |
| `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs` | Thread-pool `Timer` `:81`; bundle request `:189-193` passes no thread verdict |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | `TakeDamage` builds `CombatLogData` positionally `:206`: `knockDown` lands in `crushedThrough`, and `victim.RiderAgent == victim` |
| `Dependencies/Foundation/PatchShield.cs` | Inline namespace list `:60-69`; finalizers `(MethodBase __originalMethod, Exception __exception)` `:244`, `:255` return the exception unpreserved; swallow line every occurrence `:287` |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | 153 lines; no namespace or method exclusion members |
| `Dependencies/Foundation/SaveShield.cs` | Unpreserved hand-back in its finalizer |
| `Dependencies/SubModule.cs` | Pass 1 `:234`, pass 2 `:288`; says pass 2 runs at the "main menu" `:268-276`; no `OnBeforeMissionBehaviorInitialize` |

### End-state sources on 1.5.x (verified this session)

- Plans 006 and 007 branched from `7f02fc8d3` (after `c79a58521`) and merged through `d90b4be31`
  (007; bookkeeping-only resolutions) and `5f6a96d2e` (006; own resolution in `Main/SubModule.cs`),
  then `88408869f` (comments after both). `88408869f` is the end state for both.
- In `7f02fc8d3..88408869f` every commit touching `Main/Features/CrashReport`,
  `TAOM.Tests/Features/CrashReport`, `SettingRequireRestartPostureTests.cs` or the Dependencies shield
  files is a plan 006/007 commit, so `git diff 7f02fc8d3 88408869f` over those paths is exactly the
  plans' net change. `Main/SubModule.cs` and `HarmonyPatchBindingTests.cs` are not clean (plans 008,
  009, 018): those two are hand-edited.
- For every file this plan overwrites or patches strictly, the 1.4.x tip equals the 1.5.x
  pre-series state (`git diff c79a58521^ origin/bannerlord-1.4.5` is empty for them). In the
  CrashReport folders the only 1.5.x commit 1.4.x lacks is `c80006994` (WP115's diag.log tail:
  `LogTailCollector`, `LogTailSnapshot`, `CrashReportIoC`, `CrashBundleWriter`, one line of
  `CrashReportService.cs`, six of `PlainTextCrashReportRenderer.cs`, and their tests); no hunk here
  overlaps it.
- Every patch the steps apply (A1, A2, A3, A4.1, A6.2, B2, B3.1, C3) passed `git apply --check`
  against `1b00d1acd`'s files this session, and A6.1's three-way merge was clean with `1b00d1acd` as
  ours (`git merge-file -p --object-id`).
- After `5ee49d05f`, 1.5.x changed `PatchShield.cs` only by a comment (`48d66b59a`, WP136's).
  `RethrowStackPreserver.cs`, `LogRepeatLimiter.cs`, `ShieldCoverage.cs` and `DiagLog.cs` are
  unchanged since `5ee49d05f`. Its `PatchShieldPolicy.cs` also carries entries owned by other WPs
  (Patch92 and Patch93 for WP130, `WaitTickCompletion`/`TickComponents`/`Campaign.Tick`/
  `CampaignEvents.Tick` for WP117, load stamps for WP118): this plan keeps only the mechanism and
  `Mission.TickAgentsAndTeamsImp` (`6eb2e1b90`).

### Engine and library facts the ported code binds (read this session)

Installed v1.4.8 through `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>`:

- The 16 allowlist names are `internal static` shims: six in `ManagedCallbacks.EngineCallbacksGenerated`
  (`TaleWorlds.Engine.AutoGenerated`, 85 shims), ten in `ManagedCallbacks.CoreCallbacksGenerated`
  (`TaleWorlds.MountAndBlade.AutoGenerated`, 111 shims); the five left off (`Mission_TickAgentsAndTeams`,
  `Mission_OnPreTick`, `Agent_OnDismount`, `Agent_OnMount`, `Agent_OnAgentAlarmedStateChanged`) are
  `internal static` in `CoreCallbacksGenerated` too. `Native2ManagedTargetsTests` pins the 16 at run time. The dump's `ManagedCallbacks`
  namespace holds 87 classes (78 `ScriptingInterfaceOf*`, three each of `CallbackManager`,
  `ScriptingInterfaceObjects` and `*CallbacksGenerated`).
- Patch37 survivors are non-virtual: `internal static Managed.ApplicationTick(float)` (:290),
  `internal Module.OnApplicationTick(float)` (:463), `public static ScreenManager.Tick(float)` (:284),
  `private static ScreenManager.Update()` (:695), `internal Mission.Tick(float)` (:1830). The four removed are
  base virtuals: `ScriptComponentBehavior.OnTick` (:215, a `Debug.FailedAssert` body),
  `MissionView.OnMissionScreenTick` (:21, empty), `MissionBehavior.OnMissionTick` (:146, empty),
  `MBSubModuleBase.OnSubModuleLoad` (:8, empty).
- `MBSubModuleBase.OnBeforeMissionBehaviorInitialize(Mission)` is `public virtual` (:105).
  `MBGameManager.OnGameInitializationFinished` fans out to every submodule (:110-114); `EditorGame.cs:32` calls it.
  `Module.OnApplicationTick` reaches `SetInitialModuleScreenAsRootScreen` (:750-758, which runs every
  `OnBeforeInitialModuleScreenSetAsRoot`); `OnSubModuleUnloaded` runs through `Module.FinalizeCurrentModule` (:1289)
  from the `CoreManaged_Finalize` shim.
- Tick handshake (Mission.cs): `private bool tickCompleted = true;` :1040; `WaitTickCompletion()` :3585-3591
  spins on `Thread.Sleep(1)`, no timeout; `public void TickAgentsAndTeamsImp(float dt, bool tickPaused)` :3601
  sets it at :3613; `OnTick` clears it at :3740 and launches the agent tick at :3768-3775.
  `MissionState.TickMissionAux` (:212) gets `asyncAITick: false` in fast-forward (:178, :189, :196).
- Doc re-cites: `Mission.OnAgentRemoved` behaviour loop :3003-3006, `_activeAgents.Remove` :3023,
  `OnRemove` :3024; `Mission.MissileHitCallback` writes `extraHitParticleIndex = -1` (:5727) right after
  `DecideMissileWeaponFlags` (:5726), which `TaomCombatMechanicsModel` overrides on 1.4.x (:128). Nothing
  assigns `BannerlordTableauManager.RequestCallback` except `null` (:25); the six `TableauView.AddTableau`
  callers (Banner, BasicCharacter, BrightnessDemo, Character, Item tableaus in Native View; MapConversation in
  SandBox.View) exist in the v1.4.8 `_modules_build` dump.
- `CombatLogData`: constructor :367 (parameter names as Step C2 uses them); it sets `DamageType = Blunt`
  (:385); `DamageType` and `CrushedThrough` are public writable fields; `IsVictimRiderAgentSameAsAttackerAgent`
  and `IsFatalDamage` public; `CrushedThrough` adds the "crushed through" text (:185).
  `MissionCombatMechanicsHelper.GetAttackCollisionResults` (:175) is the only copy of the real type (:192);
  vanilla passes `defenderAgent.RiderAgent == attackerAgent` for the rider argument (Mission.cs:6462).
- Harmony 2.4.2 as TAOM.Dependencies ships it (`C:\Decompiled_Bannerlord\_modules_build\TAOM.Dependencies__0Harmony.cs`;
  E3 line 44 logs `0Harmony v2.4.2.0`): `GetOriginalMethodFromStackframe(StackFrame)` :6583; `Patches.Owners`
  :7543; `HarmonyMethod(Type, string, Type[] = null)` :6763; finalizers run on the success path too (:3085-3100).
- MCMv5 5.12.1 as shipped (`..._modules_build\TAOM.Dependencies__MCMv5.cs`): `BaseSettingsProvider.Instance` is
  assigned only in `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot` (:196-207), and
  `GlobalSettings<T>.Instance` reads it (:6329), so any `CrashReportSettings.Instance` read in `OnSubModuleLoad`
  is null.
- `RuntimeLog.Path` walks three folders up from `TAOM.Dependencies.dll` (`RuntimeLog.cs:36-62`): in the test
  host the new shield tests write `TAOM.Tests\bin\diag.log` in the worktree, never the game install.

### Build facts

- SDK-style projects with default globbing: new `.cs` files need no csproj edit. `InternalsVisibleTo
  TAOM.Tests` exists in `Dependencies/TAOM.Dependencies.csproj:32` and `Main/TAOM.csproj:117-125`.
  `TAOM.Tests/TAOM.Tests.csproj:31-33` copies every game `TaleWorlds.*.dll` into the test bin. No test
  parallelization on 1.4.x.
- `ICrashReportService` has one implementation and three callers (`AppDomainExceptionHook.cs:71`,
  `CrashReportPatchHelper.cs:48`, `BattleLoadStallWatchdog.cs:189`). `OffMainThreadDataKey` and
  `PatchShield.ShieldedCount` have no consumer outside the rewritten files.

### Conventions that bind this change

- **ADR-007**: services never touch sealed TaleWorlds types. The hooks, PatchShield and
  `CustomAttacksUtils` are engine-boundary code that already uses engine types; add no adapters.
- **ADR-002** thin entry points (under 150 lines, delegate): `SubModule.cs` only loses two gates.
- **ADR-003** no `#region`; **ADR-004** no `[Obsolete]`; **ADR-005** no `#if DEBUG` outside IoC.
- **TDD**: failing test first. Transplants land their tests first (the build fails: RED), then the
  code (GREEN). New work (Steps A5, C1) starts with a test.
- **MCM json2 rule** (rename a persisted setting, never flip its default): nothing is renamed or
  re-defaulted; only `RequireRestart` and hint texts change, which MCM does not persist.
- **`.claude/rules/harmony-patches.md`**: `MovementOrder` postfixes use the shared deferred
  category (not touched); a value-returning finalizer hands its exception back through
  `RethrowStackPreserver.PreserveForRethrow` (Step A2 restores that clause in WP106's bullet).

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/107 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0`; use a 600000 ms timeout |
| Filtered | the Test command plus `--filter "<expr>"` | as each step states |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links`, `orphan_features` equal L0 (Step 0.6); `ai_dashes: 0` |
| Snapshot | `pwsh -NoProfile -File tools/snapshot_api_surface.ps1 -Check` | exit 0 when the committed snapshot matches TAOM.dll |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
CopyModule on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game install.
Never `./build.ps1`; never two builds at once in this worktree; build before every `--no-build` run.

## Scope

**In scope** (the only files you create or modify):

- Part A code: create `Dependencies/Foundation/RethrowStackPreserver.cs` and
  `Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs`; modify `Dependencies/Foundation/PatchShield.cs`,
  `Dependencies/Foundation/SaveShield.cs`, `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs` and
  under `Main/Features/CrashReport/`: `CrashBundleThrottle.cs`, `CrashReportService.cs`, `CrashReportSettings.cs`,
  `ICrashReportService.cs`, `Collectors/CrashSignatureCalculator.cs`, `Rendering/PlainTextCrashReportRenderer.cs`,
  `DevTriggers/CrashReportApplicationTickTrigger.cs`, `Hooks/AppDomainExceptionHook.cs`,
  `Hooks/CrashReportPatchHelper.cs`, `Hooks/Native2ManagedPatcher.cs`, `Hooks/Patch37_CrashReport.cs`.
- Part A single-owner: `Main/SubModule.cs`, only the crash install block and one comment sentence
  above it (Step A4.3). `Main/IoC.cs` is not touched.
- Part A tests: create `TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs` and, under
  `TAOM.Tests/Features/CrashReport/`, `AppDomainExceptionHookTests.cs`, `CrashReportPatchHelperTests.cs`,
  `Native2ManagedBridgeTests.cs`, `Native2ManagedTargetsTests.cs`, `Patch37TargetShapeTests.cs`,
  `RecordingCrashService.cs`, `RethrowProbe.cs`; modify `CrashSignatureCalculatorTests.cs`,
  `PlainTextCrashReportRendererTests.cs`, `CrashBundleThrottleTests.cs`,
  `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs`,
  `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (two `private` keywords only),
  `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadStallWatchdogTests.cs`.
- Part A docs: create `docs/reviews/rca-shield-rethrow-stack-2026-09-22.md`; modify
  `docs/features/crash-report.md`, `docs/features/mcm.md`, `docs/features/battle-load-diagnostics.md` (one item),
  `docs/reference/harmony-patch-registry.md` (Patch37 section only),
  `docs/reference/taleworlds-api-snapshot/patch-targets.md` and `reflection-sites.md`,
  `docs/reference/engine/submodule-lifecycle-and-harmony.md`, `.claude/rules/harmony-patches.md` (the
  Finalizer bullet only).
- Part B: create `Dependencies/Foundation/ShieldCoverage.cs`, `Dependencies/Foundation/LogRepeatLimiter.cs` and,
  under `TAOM.Tests/Infrastructure/Dependencies/`, `ShieldCoverageTests.cs`, `PatchShieldFinalizerTests.cs`,
  `LogRepeatLimiterTests.cs`, `DiagLogTests.cs`, `PatchShieldAgentTickExclusionBindingTests.cs`; modify
  `Dependencies/Foundation/PatchShield.cs`, `PatchShieldPolicy.cs`, `DiagLog.cs`, `RethrowStackPreserver.cs`,
  `Dependencies/SubModule.cs`, `PatchShieldPolicyTests.cs`; docs `docs/migration/dr3-maintenance.md`,
  `docs/features/crash-report.md` (two bullets), `docs/features/arena.md` (one line),
  `docs/reference/engine/submodule-lifecycle-and-harmony.md` (one token).
- Part C: `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`,
  `TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs`,
  `TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs`.

**Out of scope** (do not touch):

- `CHANGELOG.md` (D1), `CLAUDE.md`, `AGENTS.md`, `docs/reviews/lessons/**`, `LESSONS-LEARNED.md`,
  `REVIEW-LOG.md`, `docs/INDEX.md`, `docs/reference/feature-map.md`, `tools/README.md` (WPs 142 to 144),
  `docs/adrs/**` and `.claude/settings*.json` (maintainer), `plans/README.md` (integrator), every other
  `.claude/` file.
- `Main/IoC.cs`, every csproj, `Directory.Build.props`.
- Exclusion entries owned by other WPs and their tests; the rest of `c79a58521`, `30abf550b`,
  `6eb2e1b90`; `43ca92ee9`, `48d66b59a`, `c80006994`/`743ce2e1d`/plan 017, `649cc30ff`/`ecaa67d26`,
  `b0154d86e`; co-op files (D7).
- `docs/features/hero-race.md` and `docs/reference/engine/gauntletui-viewmodel-screen.md`: the 1.5.x hunks
  edit paragraphs that do not exist on 1.4.x.
- Any `tickCompleted` restore (D17, WP106), any new MCM setting, any save-format change.

## Git workflow

- Create the worktree in Step 0.1; work only there; commit on
  `port148/107-crash-capture-and-patchshield` only. Never push, open a PR, tag, or merge into
  `bannerlord-1.4.5`. Run git only through the Bash tool (D19).
- Per-port procedure as it applies here: plans 006 and 007 merged on 1.5.x with their own
  resolutions, so they come from their post-merge end state (`88408869f`), never by replaying
  commits. `c79a58521` (208 files) comes as a path-limited patch of its crash files. `5ee49d05f`
  comes as whole-file copies of its end state, trimmed. Nothing runs `git cherry-pick`. Each patch is
  applied with `git apply` after `git apply --check`; if a check fails, see the step's fallback.
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>` in
  the NEXT sequential Bash call (never the same command or a parallel call). Write each message with
  the Write tool to the file the step names and pass the path in `C:/...` form.
- Never `--no-verify`, `--amend`, `-C` or `-c`. If a hook denies a commit, STOP and report its message.
- Message template: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters; body
  for 1.4.x readers wrapped at 72, no long dashes; `Research:`/`Not-tested:` trailers when relevant;
  one `Ported-from: <40-hex sha>` per 1.5.x commit; no `Co-Authored-By`, no "Generated with" line.
  The exact messages are in the steps.

## Steps

### Step 0: Worktree, prerequisites, baseline

**0.1**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/107 -b port148/107-crash-capture-and-patchshield bannerlord-1.4.5 && mkdir -p /c/Users/mikew/source/repos/taom-port/107-tmp
```

**Verify**: `Preparing worktree (new branch 'port148/107-crash-capture-and-patchshield')`. If the
branch or folder already exists: STOP.

**0.2 Drift check** (header command). Expected earlier-WP changes (read in plans 104 to 106):
`Main/SubModule.cs` (WP104 to WP106); `docs/features/crash-report.md` and
`docs/features/battle-load-diagnostics.md` (WP104 and WP106);
`docs/reference/engine/submodule-lifecycle-and-harmony.md` (WP104);
`.claude/rules/harmony-patches.md` and `docs/reference/harmony-patch-registry.md` (WP106). WP106 also
edits a comment in `Dependencies/Foundation/SaveShieldPolicy.cs`, which no step here touches.
Anything else: read it, then let 0.4 decide.

**0.3 Prerequisites.**

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && \
grep -cF 'private bool TryPatchCategory(string category)' Main/SubModule.cs; \
grep -cF 'TryPatchCategory("Patch37_CrashReport")' Main/SubModule.cs; \
grep -cF '(TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true' Main/SubModule.cs; \
grep -rln 'TickAgentsAndTeamsImp' Main --include=*.cs | xargs -r grep -l 'HarmonyPatch'; \
ls Dependencies/Foundation/RethrowStackPreserver.cs Dependencies/Foundation/ShieldCoverage.cs Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs 2>&1 | grep -c 'No such file'; \
grep -c 'ExcludedTargetMethods\|IsExcludedTargetNamespace' Dependencies/Foundation/PatchShieldPolicy.cs
```

**Verify**: `1`, `1`, `1`; at least one file listed (WP106's Patch91, normally
`Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs`); `3`; `0`. Anything
else: STOP.

**0.4 Pre-image identity** (files this plan overwrites or patches strictly):

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && git diff --quiet 1b00d1acd -- \
 Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/PatchShieldPolicy.cs Dependencies/Foundation/DiagLog.cs \
 Dependencies/Foundation/SaveShield.cs Dependencies/SubModule.cs \
 Main/Features/CrashReport/Hooks Main/Features/CrashReport/CrashBundleThrottle.cs Main/Features/CrashReport/CrashReportSettings.cs \
 Main/Features/CrashReport/ICrashReportService.cs Main/Features/CrashReport/Collectors/CrashSignatureCalculator.cs \
 Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs \
 TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs TAOM.Tests/Features/CrashReport/CrashSignatureCalculatorTests.cs \
 TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs \
 Main/Features/AdvancedCombat/CustomAttacksUtils.cs TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs \
 TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs && echo same
```

**Verify**: `same`. Otherwise run it without `--quiet`, report which WP changed what, and STOP.

**0.5 Baseline.** Restore, Build, Test. **Verify**: build exit 0, `Failed: 0`. Record `Total`,
`Passed`, `Skipped` as **T0** (WP101 baseline 9154/9151/3; WPs 102 to 106 added tests). Then run the
Snapshot command; record its exit code as **S0**.

**0.6** Run the Docs command; record `dead_links` and `orphan_features` as **L0**.

---

## Part A: thread-aware crash capture on 16 shims, throw sites kept

### Step A1 (RED): Rethrow-preserver tests (`c79a58521`, crash half)

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && P="TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs TAOM.Tests/Features/CrashReport/CrashSignatureCalculatorTests.cs TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs" && git diff c79a58521^ c79a58521 -- $P | git apply --check && git diff c79a58521^ c79a58521 -- $P | git apply && git status --porcelain
```

**Verify**: one new and two modified test files. Build. **Verify (RED)**: the build fails and every
`error CS` line names `RethrowStackPreserver` or `RethrownCount`.

### Step A2 (GREEN): The rethrow preserver, the RCA and the rule

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && P="Dependencies/Foundation/RethrowStackPreserver.cs Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/SaveShield.cs Main/Features/CrashReport/Collectors/CrashSignatureCalculator.cs Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs docs/reviews/rca-shield-rethrow-stack-2026-09-22.md docs/reference/engine/submodule-lifecycle-and-harmony.md" && git diff c79a58521^ c79a58521 -- $P | git apply --check && git diff c79a58521^ c79a58521 -- $P | git apply && git status --porcelain
```

Fallback: if the check fails, its error names the file; an earlier WP edited a line near a hunk
(the check passed on `1b00d1acd` this session). Drop that path from `P`, apply the rest, then apply
that one file with `git diff c79a58521^ c79a58521 -- <path> | git apply --3way` and keep both sides
(the earlier WP's text and this commit's change). `--3way` stages the file, so run
`git restore --staged <path>` afterwards: Commit A's `git add` stays the only staging step.

This brings `RethrowStackPreserver` (moves the trace into `_remoteStackTraceString`, adds a marker
naming the rethrowing method, records the first five original frames under
`Data["TAOM.ThrowSite"]`, idempotent, fails open), the shields' preserved hand-backs and rethrow
counter, the per-level throw site in the crash signature, the report's "last segment only" note,
the RCA (its trigger is a 1.4.8 bundle, TAOM v2.0.28 build `8dabf4a6`) and the engine page's
Finalizer bullet.

**Rule bullet** (WP106's maintenance notes ask for it). WP106 adds a `- **Finalizer**:` bullet to
`.claude/rules/harmony-patches.md` (section `## Patch Types`) with `f75288ebf`'s
`RethrowStackPreserver` clause taken out, because the type did not exist yet. Restore it: in that
bullet replace `which erases the throw site. An observe-only` with
``which erases the throw site, so hand it back as `return RethrowStackPreserver.PreserveForRethrow(__exception, null);` (`docs/reviews/rca-shield-rethrow-stack-2026-09-22.md`). An observe-only``.
If the bullet already names `RethrowStackPreserver`, change nothing. If the file has no Finalizer
bullet at all, add this after the `- **Transpiler**` bullet:

```markdown
- **Finalizer**: runs after the original on every call, with a null `__exception` when nothing threw. `return null` swallows. Returning the exception makes Harmony `throw` it (whenever any finalizer on the method returns a value), which erases the throw site, so hand it back as `return RethrowStackPreserver.PreserveForRethrow(__exception, null);`. An observe-only finalizer should be `void`, which keeps Harmony's `rethrow` and the trace. Why: `docs/reviews/rca-shield-rethrow-stack-2026-09-22.md`.
```

Build, then filtered
`--filter "FullyQualifiedName~RethrowStackPreserverTests|FullyQualifiedName~CrashSignatureCalculatorTests|FullyQualifiedName~PlainTextCrashReportRendererTests"`.
**Verify (GREEN)**: 0 build errors; `Failed: 0`, at least 44 passed (19 + 15 + 10). Full Test:
`Failed: 0`, `Total` = T0 + 26. Docs command: replace each long dash it reports in a line this step
changed (the lifecycle page's Finalizer bullet keeps a pre-existing one after `**Finalizer**`) with a
colon or comma; re-run until `ai_dashes: 0`; L0 unchanged.

**Commit A.**

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && git add Dependencies/Foundation/RethrowStackPreserver.cs Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/SaveShield.cs Main/Features/CrashReport/Collectors/CrashSignatureCalculator.cs Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs TAOM.Tests/Features/CrashReport/CrashSignatureCalculatorTests.cs TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs docs/reviews/rca-shield-rethrow-stack-2026-09-22.md docs/reference/engine/submodule-lifecycle-and-harmony.md .claude/rules/harmony-patches.md && git status --porcelain
```

(Leave `.claude/rules/harmony-patches.md` out if the bullet already named the type.) **Verify**:
only those paths staged.
Write `C:\Users\mikew\source\repos\taom-port\107-tmp\msg-A.txt`:

```text
fix(crash-report): v2.0.28 - crash reports keep the throw site

When PatchShield or SaveShield handed an exception back to Harmony,
Harmony rethrew it with `throw`, which replaced its stack trace with
the frames from the shielded method outward. A 1.4.8 player bundle
(2d446100) showed a childbirth failure as five frames ending at
MapState.OnTick, and different crashes through one shielded method
shared one crash signature.

The shields now move the original trace aside before the rethrow
(RethrowStackPreserver, new in TAOM.Dependencies). A report prints the
real throw site first, with one marker line for each shielded method
the exception crossed, and keeps the first five frames of the original
throw under TAOM.ThrowSite. The crash signature and the report's frame
sections use them, so two different throws through one frame get two
signatures. A crash that crossed a shield gets a new signature once;
crashes that never crossed one keep theirs.

TAOM.Dependencies changed: a release must ship TAOM and
TAOM.Dependencies from one build.

Research: Harmony 2.4.2 as shipped in TAOM.Dependencies (finalizers run
on every call; a value-returning finalizer set ends in throw)
Not-tested: a shielded rethrow in a live game
Ported-from: c79a585218ad22841c8db1f273e013385512b332
```

In the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && git commit -F C:/Users/mikew/source/repos/taom-port/107-tmp/msg-A.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: one new commit with that subject; empty status. Every later commit uses this form with
its own message file.

### Step A3 (RED): Plan 006 tests (end state `88408869f`)

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && P="TAOM.Tests/Features/CrashReport TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs" && git diff 7f02fc8d3 88408869f -- $P | git apply --check && git diff 7f02fc8d3 88408869f -- $P | git apply && git status --porcelain
```

**Verify**: new `AppDomainExceptionHookTests.cs`, `CrashReportPatchHelperTests.cs`,
`Native2ManagedBridgeTests.cs`, `Native2ManagedTargetsTests.cs`, `Patch37TargetShapeTests.cs`,
`RecordingCrashService.cs`, `RethrowProbe.cs`; modified `CrashBundleThrottleTests.cs`,
`SettingRequireRestartPostureTests.cs`. Re-cite one comment: `Native2ManagedTargetsTests.cs` line 59,
`which nothing in v1.5.3 arms.` becomes `which nothing in v1.4.8 arms.` (verified).

Build. **Verify (RED)**: the `error CS` lines name only `Native2ManagedTargets`, `HandleOrPassThrough`,
`MainThreadId`, `IsOffMainThread`, `OnUnhandled` (inaccessible), `IsLoggedOccurrence`, the
three-argument `HandleException` (with CS0535 on `RecordingCrashService`) and `DiscoverPatchTypes` /
`MergeSpec` (CS0122).

### Step A4 (GREEN): Plan 006 code and the unconditional install

**A4.1**

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && git diff 7f02fc8d3 88408869f -- Main/Features/CrashReport | git apply --check && git diff 7f02fc8d3 88408869f -- Main/Features/CrashReport | git apply && git status --porcelain Main/Features/CrashReport
```

**Verify**: `Hooks/Native2ManagedTargets.cs` new; `CrashBundleThrottle.cs`, `CrashReportService.cs`,
`CrashReportSettings.cs`, `ICrashReportService.cs`, `DevTriggers/CrashReportApplicationTickTrigger.cs`,
`Hooks/AppDomainExceptionHook.cs`, `Hooks/CrashReportPatchHelper.cs`, `Hooks/Native2ManagedPatcher.cs`,
`Hooks/Patch37_CrashReport.cs` modified. In `Hooks/Native2ManagedTargets.cs`: line 14
`// (plans/_audit/2026-09-23-opus/followup-patch-tax.md).` becomes
`// (1.5.x audit record plans/_audit/2026-09-23-opus/followup-patch-tax.md).`; line 40
`nothing in v1.5.3 assigns` becomes `nothing in v1.4.8 assigns`.

**A4.2** In `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (1.4.x lines 80 and 162): make
`DiscoverPatchTypes(out int typesFailedToLoad)` and `MergeSpec(Type patchType)` `internal static`
instead of `private static`, and put
`    // Shared with Patch37TargetShapeTests so the suite derives a patch target one way only.` on the
line above `DiscoverPatchTypes`. Nothing else in that file.

**A4.3 (single-owner file)** In `Main/SubModule.cs`, replace the whole block from
`if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)` to its
closing brace with exactly this (the `5f6a96d2e` merge resolution):

```csharp
        // Installed unconditionally. CrashReportSettings.Instance is always null here: MCM builds
        // its settings provider in its own OnBeforeInitialModuleScreenSetAsRoot, after every
        // OnSubModuleLoad, so an MCM gate at this point always took its fallback. The toggles are
        // read at capture time instead (CrashReportPatchHelper.HandleAndSwallow,
        // Native2ManagedBridge.Finalizer, AppDomainExceptionHook).
        try
        {
            if (TryPatchCategory("Patch37_CrashReport"))
            {
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
            }
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogError($"[CrashReport] init failed: {ex.GetType().Name}: {ex.Message}");
        }
```

The comment above the block (WP104's text, plan 104 step E2) still says Patch37 has an
`OnSubModuleLoad` finalizer, which A4.1 removed. Replace these three lines:

```text
        // live as early as possible. It does NOT cover this method's own body: its
        // MBSubModuleBase.OnSubModuleLoad finalizer patches the base method, and this override is
        // already on the stack when it attaches. A throw from here leaves the engine's
```

with:

```text
        // live as early as possible. It does NOT cover this method's own body: no Patch37
        // finalizer wraps an OnSubModuleLoad override, and this method is already on the stack
        // when the category attaches. A throw from here leaves the engine's
```

No other edit to `Main/SubModule.cs`.

Build, then filtered
`--filter "FullyQualifiedName~TAOM.Tests.Features.CrashReport|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~RethrowStackPreserverTests"`.
**Verify (GREEN)**: 0 errors; `Failed: 0`; the BindingVerification tests
`Native2ManagedTargetsTests.All_ResolvesEveryShimAgainstTheInstalledEngine` and
`Patch37TargetShapeTests.EveryPatch37Target_IsNotAnOverridableVirtual` PASS (not inconclusive). Full
Test: `Failed: 0`, `Total` = T0 + 63. (The new `CrashBundleThrottleTests` method has 12 data rows. If
the runner reports 11 fewer, it counts that method once: record the observed total and use that
convention for every later total.) Then:

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && grep -cE '^\s+\((Engine|Core)Assembly, (Engine|Core)Callbacks, "' Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs; grep -c 'Mission_TickAgentsAndTeams\|Mission_OnPreTick\|Agent_OnDismount' Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs; grep -c '\[HarmonyPatch(typeof(' Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs; grep -c 'EnableCrashCapture\|EnableNativeToManagedCapture' Main/SubModule.cs
```

**Verify**: `16`, `0`, `5`, `0`.

**Commit B.** `git add Main/Features/CrashReport/CrashBundleThrottle.cs Main/Features/CrashReport/CrashReportService.cs Main/Features/CrashReport/CrashReportSettings.cs Main/Features/CrashReport/ICrashReportService.cs Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs Main/SubModule.cs TAOM.Tests/Features/CrashReport TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`;
**verify** only those staged. `107-tmp\msg-B.txt`:

```text
perf(crash-report): v2.0.28 - capture wraps 16 callback shims, not 247

Crash capture patched every static method of the engine's three
*CallbacksGenerated classes at launch: 247 finalizers in a 1.4.8
player's log, on per-agent and per-hit callbacks. It now patches an
allowlist of 16: the screen early, late and input ticks, the
scene-script tick, thumbnail and tableau rendering, and ten mission
combat callbacks that reach TAOM code. A throw inside one of the other
231 is no longer swallowed by TAOM; in the agent tick a swallow left
the next frame waiting forever.

A capture on a thread other than the main one now skips the Mission
and Campaign collectors and the crash inquiry, even for the runtime's
preallocated out-of-memory exception. Both MCM toggles (Enable Crash
Capture, Enable Native-to-Managed Capture) now apply without a
restart: they were read at launch, before MCM had built its settings.
Four finalizers that could never fire are gone, a crash that repeats
every frame logs at occurrences 1, 2, 10, 100 and so on, and a
handed-back exception keeps its throw site.

Needs TAOM.Dependencies from the same build (RethrowStackPreserver).

Research: taom-src v1.4.8 ManagedCallbacks.EngineCallbacksGenerated and
CoreCallbacksGenerated (all 16 names), Managed, Module, ScreenManager,
Mission, ScriptComponentBehavior, MissionView, MissionBehavior,
MBSubModuleBase; MCMv5 5.12.1 MCMSubModule
Not-tested: the boot line, a live combat-callback capture, live toggles
Ported-from: 8bd44b92456917597c3d70c00f6f02e2829f5464
Ported-from: 1d94df9c48666490fdf2124df123ebf7b6c46ed4
Ported-from: 9cfb91b151c6b0bd8434bba6f4682710a2cd7e7e
Ported-from: fb52e619d1af26f7fb978b58f4aba0eaa04a83ce
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 70727529052be7948305405087633d600a08ca0e
Ported-from: 42624b95bc1c065a456e72fec4b715e39c90410a
Ported-from: 8e6b0935cab35e31b127d5877d1b1aa93a98d932
Ported-from: 8c84fa20e972521ddd79c67f9153cb0ceb8d9fa2
Ported-from: 9e8c99bdb21de8f31e1d5ecb2bc94bb1c3784e1a
Ported-from: 5f6a96d2eb678143444b6113d5b91534a6fd931f
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Step A5 (RED then GREEN): The stall watchdog's bundle takes the off-thread path (new work, 107-2)

**RED.** Append inside the class in
`TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadStallWatchdogTests.cs` (it reuses Part A's
`RecordingCrashService`, named in full, so no `using` is added):

```csharp
    [TestMethod]
    public void RequestStallBundle_CapturesOffTheMainThread_WithTheWatchdogOrigin()
    {
        // The watchdog polls from a thread-pool Timer, never the main thread, so its capture must take
        // the reduced path (no Mission or Campaign reads, no inquiry) like every other off-main capture.
        var service = new TAOM.Tests.Features.CrashReport.RecordingCrashService();

        BattleLoadStallWatchdog.RequestStallBundle(service, "Mission load stalled >300s; last X");

        Assert.AreEqual(1, service.Calls.Count);
        Assert.AreEqual("BattleLoadStallWatchdog", service.Calls[0].Origin);
        Assert.IsTrue(service.Calls[0].OffMainThread, "the watchdog never runs on the main thread");
        Assert.IsInstanceOfType(service.Calls[0].Exception, typeof(BattleLoadStallException));
        StringAssert.Contains(service.Calls[0].Exception.Message, "Mission load stalled");
    }
```

Build. **Verify (RED)**: one `error CS0117` naming `RequestStallBundle`.

**GREEN.** In `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs`:
1. After `FormatChurnToken` (1.4.x `:221-222`) add:

```csharp
    // The watchdog polls from a thread-pool Timer, never the main thread, so its capture takes the
    // service's reduced path: no Mission or Campaign collector reads live engine state from this
    // thread and no inquiry is built off the UI thread, the rule for every off-main capture.
    internal static string? RequestStallBundle(ICrashReportService crashReport, string message)
        => crashReport.HandleException(new BattleLoadStallException(message), "BattleLoadStallWatchdog", offMainThread: true);
```

2. In `Poll()` (1.4.x `:189-193`) replace `_crashReport.HandleException(new BattleLoadStallException(<message>), "BattleLoadStallWatchdog")`
   with `RequestStallBundle(_crashReport, <message>)`, keeping `var zip =` and the message expression.
3. Replace the header comment lines saying collectors "read live mission state; from this thread
   while the main thread is frozen they may return partial data" (1.4.x `:16-18`) with:

```csharp
// The bundle request tells CrashReportService it runs off the main thread, so the Mission and
// Campaign collectors are skipped and no inquiry opens; the marker and the already-flushed phase
// log are the primary signal and the bundle is a bonus.
```

If WP113 reshaped `Poll()`, change the single `_crashReport.HandleException(` call the same way; if it
is gone, STOP.

In `docs/features/battle-load-diagnostics.md`, replace the numbered item that starts
`2. **Best-effort:**` (1.4.x line 296) with:

```markdown
2. **Best-effort:** calls `BattleLoadStallWatchdog.RequestStallBundle`, which passes `ICrashReportService.HandleException(new BattleLoadStallException(...), "BattleLoadStallWatchdog", offMainThread: true)` so the user can ship the log in one action. The capture runs on the timer thread, so it takes the service's reduced path: the bundle records its Mission and Campaign sections as skipped, and no inquiry opens (the service builds none off the main thread). The marker and the flushed phase log are the primary signal.
```

and in the following paragraph replace `is unit-tested; the timer/CrashReport plumbing is not (game-only).`
with ``is unit-tested, and so is `RequestStallBundle` with its off-main flag; the timer is not (game-only).``

Build; filtered `FullyQualifiedName~BattleLoadStallWatchdogTests`: `Failed: 0`. Full Test: `Total` =
T0 + 64. Docs command: `ai_dashes: 0`, L0 unchanged.

**Commit C.** `git add Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadStallWatchdogTests.cs docs/features/battle-load-diagnostics.md`;
`107-tmp\msg-C.txt`:

```text
fix(battle-load): v2.0.28 - watchdog bundles skip main-thread reads

The battle-load stall watchdog runs on a thread-pool timer. When it
asked for a crash bundle, the capture ran the Mission and Campaign
collectors and built the crash inquiry from that timer thread. It now
tells the crash service the capture is off the main thread, so the
bundle skips those two sections and no inquiry opens. The STILL
LOADING marker and the battle-load phase log stay the primary signal.

Not-tested: a stalled battle load in game
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Step A6: Crash-capture docs at the plan 006 state

**A6.1 Feature doc, three-way merge.** The 1.4.x file differs from the 1.5.x pre-series file only by
`c80006994`'s two diag.log lines, so the merge carries plan 006, `c79a58521` and plan 009 doc changes
and keeps the base's version of lines this WP does not own:

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && T=/c/Users/mikew/source/repos/taom-port/107-tmp && git show c79a58521^:docs/features/crash-report.md > $T/cr.base.md && git show 88408869f:docs/features/crash-report.md > $T/cr.theirs.md && sed -i 's/\r$//' docs/features/crash-report.md && git merge-file -L ours -L base -L theirs docs/features/crash-report.md $T/cr.base.md $T/cr.theirs.md; echo "conflicts=$?"
```

**Verify**: expect `conflicts=1` (a dry run with `1b00d1acd` as ours was clean; the one block comes
from WP104, which rewrote the `MBSubModuleBase.OnSubModuleLoad` chicken-and-egg bullet that
`88408869f` replaces; `conflicts=0` needs no resolution). Resolve it with theirs' bullet starting
``- **Other mods' `OnSubModuleLoad` throws are not captured.**``, and after its sentence ending
`those throws land in vanilla or BUTR.` insert WP104's sentence starting `On v1.4.8 the throw then
leaves`. Any other block: `ours` inside the `<!-- backlinks-start` region and for plan 009 sentences
WP104 worded; `theirs` for Patch37, Native2Managed, the allowlist, toggles, off-main captures, the
throw site and the 2026-09-24 changelog entries. WP106's new How-To step 5 merges without a
conflict (`88408869f` leaves that list alone). Delete every conflict marker. Then apply these 1.4.x
facts (verified this session):
1. The table header `Managed target (v1.5.3)` becomes `Managed target (v1.4.8)`.
2. `Mission_MeleeHitCallback` row: delete `` `OnMeleeHit` (`SignatureStrikesMissionLogic`); ``.
3. `Mission_OnAgentDeleted` row: delete `` , `SignatureStrikesMissionLogic` ``.
4. `Mission_OnAgentShootMissile` row: delete `` , `ElephantMissionBehavior` `` (no such override on 1.4.x).
5. `The trace (installed v1.5.3)` becomes `The trace (installed v1.4.8)`.
6. `` (`Mission.cs:3019-3040`) `` becomes `` (v1.4.8 `Mission.cs:3003-3024`) ``;
   `` (`Mission.cs:5789-5790`) `` becomes `` (v1.4.8 `Mission.cs:5726-5727`) ``.
7. In the bullet `- **Most native-to-managed callbacks are no longer wrapped.**`, both `v1.5.3` become `v1.4.8`.
8. Every `([followup-patch-tax.md](../../plans/_audit/2026-09-23-opus/followup-patch-tax.md))` becomes
   `` (1.5.x audit record `plans/_audit/2026-09-23-opus/followup-patch-tax.md`; a 1.4.8 player log shows the 247-shim attach one second after the line before it) ``.
9. As the first bullet under `## Changelog`:
   `- <today, YYYY-MM-DD>: **Ported to the 1.4.x line** (Bannerlord v1.4.8, port148 WP107): plan 006 and the shield rethrow fix. The 2026-09-24 entries below describe the 1.5.x change set this port carries.`

**A6.2 MCM doc.**
`git diff c79a58521 88408869f -- docs/features/mcm.md | git apply --check && git diff c79a58521 88408869f -- docs/features/mcm.md | git apply`

**A6.3 Registry.** In `docs/reference/harmony-patch-registry.md`, section `## Patch37_CrashReport`:
replace the paragraph starting `**Target:** 9 Finalizers` with line 268 of
`git show 88408869f:docs/reference/harmony-patch-registry.md`, and the paragraph starting
`TAOM's crash-capture pipeline.` with line 270 of that file. Nothing else in the registry.

**A6.4 Reflection sites** (`docs/reference/taleworlds-api-snapshot/reflection-sites.md`): insert
line 21 of `git show 88408869f:docs/reference/taleworlds-api-snapshot/reflection-sites.md` (it starts
`The one hand-attached exception:`) as its own paragraph after the paragraph that starts
``Every `[HarmonyPatch(...)]` target``; replace the row that starts
`` | `CrashReport/Hooks/Native2ManagedPatcher.cs:41,52` `` with line 116 of the same file.

**A6.5 Patch-target snapshot.** If S0 was 0: run `pwsh -NoProfile -File tools/snapshot_api_surface.ps1`;
`git diff --stat -- docs/reference/taleworlds-api-snapshot/gamemodel-bases.md docs/reference/taleworlds-api-snapshot/patch-targets.md` must list only `patch-targets.md` (the generator writes those two; `reflection-sites.md` already shows A6.4's edit and is not part of this check), with
exactly the rows of `MBSubModuleBaseOnSubModuleLoadFinalizer`, `MissionBehaviorOnMissionTickFinalizer`,
`MissionViewOnMissionScreenTickFinalizer` and `ScriptComponentBehaviorOnTickFinalizer` removed and the
`Patches:` count 4 lower. If S0 was 1, or anything else changed: `git checkout docs/reference/taleworlds-api-snapshot/gamemodel-bases.md docs/reference/taleworlds-api-snapshot/patch-targets.md`,
then delete those four rows by hand and lower `Patches:` by 4.

**A6.6 Lifecycle page.** In `docs/reference/engine/submodule-lifecycle-and-harmony.md` replace the
parenthetical ``(some conditionally, e.g. `Patch37_CrashReport` :NNN)`` (any line number) with
``(some conditionally; `Patch37_CrashReport` has been applied unconditionally since the plan 006 port, because no MCM setting is readable in `OnSubModuleLoad`)``,
and in the `## Evidence` line replace `+ conditional :109 CrashReport` (or WP104's equivalent) with
`; CrashReport unconditional since the plan 006 port`.

Docs command: fix every reported long dash in a changed line (comma, colon, semicolon or
parentheses) and every new dead link, until `ai_dashes: 0` and L0 holds. **Verify**:
`git diff --stat` lists only the six docs of A6.

**Commit D.** `git add docs/features/crash-report.md docs/features/mcm.md docs/reference/harmony-patch-registry.md docs/reference/taleworlds-api-snapshot/reflection-sites.md docs/reference/taleworlds-api-snapshot/patch-targets.md docs/reference/engine/submodule-lifecycle-and-harmony.md`;
`107-tmp\msg-D.txt`:

```text
docs(crash-report): v2.0.28 - document the allowlist and live toggles

The crash-report feature doc, the MCM doc, the Patch37 registry entry,
the reflection-site catalogue, the patch-target snapshot and the
SubModule lifecycle page now describe crash capture after the plan 006
port: five lifecycle finalizers, 16 allowlisted callback shims, live
toggles, off-main-thread captures and the throw site kept on every
hand-back. Engine facts are re-cited to v1.4.8.

Ported-from: 6fe83bcad7be5c9c97fbdfc717a774453b3af6ed
Ported-from: 65b691b6a745d622afa66eca8e39a3751219a047
Ported-from: 70727529052be7948305405087633d600a08ca0e
Ported-from: 42624b95bc1c065a456e72fec4b715e39c90410a
Ported-from: 8e6b0935cab35e31b127d5877d1b1aa93a98d932
Ported-from: 8c84fa20e972521ddd79c67f9153cb0ceb8d9fa2
Ported-from: 9e8c99bdb21de8f31e1d5ecb2bc94bb1c3784e1a
Ported-from: 5f6a96d2eb678143444b6113d5b91534a6fd931f
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Part A done criteria

- [ ] Build exit 0; Test `Failed: 0`, `Total` = T0 + 64.
- [ ] `git diff --quiet 88408869f HEAD -- Main/Features/CrashReport/CrashBundleThrottle.cs Main/Features/CrashReport/CrashReportSettings.cs Main/Features/CrashReport/ICrashReportService.cs Main/Features/CrashReport/DevTriggers/CrashReportApplicationTickTrigger.cs Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs Main/Features/CrashReport/Hooks/Patch37_CrashReport.cs TAOM.Tests/Features/CrashReport/AppDomainExceptionHookTests.cs TAOM.Tests/Features/CrashReport/CrashReportPatchHelperTests.cs TAOM.Tests/Features/CrashReport/Native2ManagedBridgeTests.cs TAOM.Tests/Features/CrashReport/Patch37TargetShapeTests.cs TAOM.Tests/Features/CrashReport/RecordingCrashService.cs TAOM.Tests/Features/CrashReport/RethrowProbe.cs TAOM.Tests/Features/CrashReport/CrashBundleThrottleTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs && echo same` prints `same`.
- [ ] `git diff 88408869f HEAD -- Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs | grep -cE '^[-+][^-+]'` prints `4`; for `TAOM.Tests/Features/CrashReport/Native2ManagedTargetsTests.cs` it prints `2`.
- [ ] `git diff --quiet c79a58521 HEAD -- Dependencies/Foundation/RethrowStackPreserver.cs TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs docs/reviews/rca-shield-rethrow-stack-2026-09-22.md && echo same` prints `same`.
- [ ] Step A4's four greps print `16`, `0`, `5`, `0`; Docs command `ai_dashes: 0` and L0 holds.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'` prints nothing; `git status --porcelain` prints nothing.

---

## Part B: PatchShield off the shims, a method list, no per-call lookup

### Step B1 (RED): Shield tests at the end state (`5ee49d05f`), trimmed for 1.4.x

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && for f in ShieldCoverageTests PatchShieldFinalizerTests LogRepeatLimiterTests DiagLogTests PatchShieldPolicyTests; do git show 5ee49d05f:TAOM.Tests/Infrastructure/Dependencies/$f.cs > TAOM.Tests/Infrastructure/Dependencies/$f.cs; done && git status --porcelain TAOM.Tests/Infrastructure/Dependencies
```

Edit the copied `PatchShieldPolicyTests.cs` (line numbers of the `5ee49d05f` file):
1. Line 214: `// v1.5.3). Re-shielding them cost` becomes `// v1.4.8 before the plan 006 allowlist). Re-shielding them cost`
   (rest of the line unchanged).
2. Replace lines 247 to 306 (from `// --- IsExcludedTargetMethod: Patch92's per-unit Formation hot members`
   through the end of `IsExcludedTargetMethod_MissionOnTickOnPreTickAndSpawnAgent_ReturnFalse`) with:

```csharp
    // --- IsExcludedTargetMethod: the method-level exclusion list ----------------------------------
    // One entry on this line: the agent tick. PatchShieldAgentTickExclusionBindingTests walks the
    // installed engine's real target; the cases below need no game.

    [TestMethod]
    public void IsExcludedTargetMethod_AnotherFormationMethod_ReturnsFalse()
    {
        // The exclusion is per-method, not per-type: an unlisted method stays shielded.
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Formation", "SetMovementOrder"));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_NullOrEmptyParts_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(null, "get_UnitDiameter"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Formation", null));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(null, null));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(string.Empty, "get_UnitDiameter"));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Formation", string.Empty));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_AgentTick_ReturnsTrue()
    {
        // A swallow at the agent tick would skip `tickCompleted = true` (v1.4.8 Mission.cs:3613), and the
        // next WaitTickCompletion (Mission.cs:3585-3591, no timeout) would spin forever.
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", "TickAgentsAndTeamsImp"));
    }

    [TestMethod]
    public void IsExcludedTargetMethod_MissionOnTickOnPreTickAndSpawnAgent_ReturnFalse()
    {
        // Only the agent tick is excluded; a foreign patch on these keeps the shield.
        foreach (var name in new[] { "OnTick", "OnPreTick", "SpawnAgent" })
        {
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod("TaleWorlds.MountAndBlade.Mission", name),
                $"Mission.{name} must stay shielded");
        }
    }
```

Create `TAOM.Tests/Infrastructure/Dependencies/PatchShieldAgentTickExclusionBindingTests.cs`:

```csharp
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure.Dependencies;

// Pins PatchShieldPolicy's agent-tick entry against the installed engine. Patch91 brackets
// Mission.TickAgentsAndTeamsImp for every player; a shield finalizer there could swallow a missing-API
// exception before `tickCompleted = true` (v1.4.8 Mission.cs:3613) and leave the next frame's
// WaitTickCompletion spinning. An engine rename would make the string entry match nothing and bring
// the shield back silently; this test fails instead.
[TestClass]
public class PatchShieldAgentTickExclusionBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentTickTarget_IsOnPatchShieldsExclusionList()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = AccessTools.Method(typeof(Mission), "TickAgentsAndTeamsImp", new[] { typeof(float), typeof(bool) });

        Assert.IsNotNull(target, "Mission.TickAgentsAndTeamsImp(float, bool) did not resolve on the installed engine");
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
            $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
    }
}
```

Build. **Verify (RED)**: the `error CS` lines name only `ShieldCoverage`, `IsExcludedTargetNamespace`,
`IsExcludedTargetMethod`, `FormatShieldPassSummary`, `FormatHotMethodSkip`, `LogRepeatLimiter`,
`TryLog`, `AttachedCount`, `SeenCount`, `ResolveShieldedOriginal`, `UnresolvedOriginalCount`,
`FormatUnresolvedOriginal`, `FormatRepeatedSwallow`, `FormatRepeatedSwallowSoFar` or
`WriteRepeatCheckpoint`.

### Step B2 (GREEN): Plan 007, the method list and plan 034

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && for f in PatchShield PatchShieldPolicy ShieldCoverage LogRepeatLimiter DiagLog RethrowStackPreserver; do git show 5ee49d05f:Dependencies/Foundation/$f.cs > Dependencies/Foundation/$f.cs; done && git diff c79a58521 5ee49d05f -- Dependencies/SubModule.cs | git apply --check && git diff c79a58521 5ee49d05f -- Dependencies/SubModule.cs | git apply && git status --porcelain Dependencies
```

No earlier plan edits `Dependencies/SubModule.cs` and Step 0.4 proved it unchanged, so if its check
fails: STOP.

Then trim and re-cite (line numbers of the copied `5ee49d05f` files):

1. `Dependencies/Foundation/PatchShield.cs` lines 52-54 become:

```csharp
    // The target exclusion lists live in PatchShieldPolicy: by namespace
    // (ExcludedTargetNamespacePrefixes, #331, plan 007) and by declaring-type+method
    // (ExcludedTargetMethods: members whose namespace is not excluded as a whole).
```

2. `Dependencies/Foundation/PatchShield.cs` line 213: `// Never shield the excluded hot layers: the Gauntlet/2D UI (#331 round 2: a`
   becomes `// Never shield the excluded targets: the Gauntlet/2D UI (#331 round 2: a`; line 216:
   `// capture already wraps (plan 007), and Patch92's per-unit Formation members. See` becomes
   `// capture already wraps (plan 007), and the methods on ExcludedTargetMethods. See` (indentation
   unchanged).

3. `Dependencies/Foundation/PatchShieldPolicy.cs` lines 106-108 become:

```csharp
        // The prefix is the whole namespace, not only the three shims: it also holds the engine's
        // 78 managed-to-native ScriptingInterfaceOf* wrappers plus CallbackManager and
        // ScriptingInterfaceObjects (87 classes in v1.4.8), which Native2Managed does NOT wrap.
```

4. `Dependencies/Foundation/PatchShieldPolicy.cs` lines 125-215 (the `/// <summary>` starting
   `Method-level hot-target exclusion list` through the `};` closing `ExcludedTargetMethods`) become:

```csharp
    /// <summary>
    /// Method-level exclusion list: "&lt;FullTypeName&gt;.&lt;MethodName&gt;" entries for engine members whose
    /// namespace is not excluded as a whole but which PatchShield's finalizer must not wrap, either for what a
    /// swallow there would break or, in an entry a later change adds, for the per-call cost on a hot target.
    /// One entry covers every overload of its name. PatchShield skips an excluded method for every owner, so a
    /// third-party patch on one of these methods also loses the rescue; PatchShield writes one diag.log line per
    /// skipped method (<see cref="FormatHotMethodSkip"/>). Each entry names the test that walks its real target
    /// through <see cref="IsExcludedTargetMethod"/>.
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedTargetMethods = new[]
    {
        // Mission.TickAgentsAndTeamsImp carries Patch91's stall-probe bracket for every player. With a shield
        // finalizer on it, a swallowed missing-API exception from the agent or team ticks would skip
        // `tickCompleted = true` (v1.4.8 Mission.cs:3613), and the next WaitTickCompletion (Mission.cs:3585-3591,
        // no timeout) would spin forever; excluded, the exception leaves the method instead. On the
        // asynchronous call it reaches the native job thread through the generated shim
        // Mission_TickAgentsAndTeams, which carries no TAOM finalizer since the plan 006 allowlist (what native
        // does then is UNVERIFIED); on the inline call (fast-forward passes asyncAITick false,
        // MissionState.cs:178-196) it reaches Mission.OnTick. Given up, for any owner's patch on it: the
        // missing-API swallow and the strip of the offending patch.
        // Mission.OnTick and Mission.OnPreTick are deliberately NOT listed: a throw that leaves OnTick after it
        // clears tickCompleted (Mission.cs:3740) and before the agent tick sets it again hangs the next frame
        // whichever way this list reads. The launch-aware tick recovery on Mission.OnTick (decision D17 of the
        // 2026-11-04 port programme) is the remedy for that, not an exclusion.
        // PatchShieldAgentTickExclusionBindingTests walks the real target; PatchShieldPolicyTests pins it
        // without the game.
        "TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp",
    };
```

5. `Dependencies/SubModule.cs`, in the `OnGameInitializationFinished` summary comment: replace the
   line `    /// TAOM's late batch, about +140 attaches).` with
   `    /// TAOM's late batch: +119 attaches in this laptop's 1.4.x diag.log of 2026-09-02).`

Build; filtered `FullyQualifiedName~TAOM.Tests.Infrastructure.Dependencies`. **Verify (GREEN)**: 0
errors; `Failed: 0`; `PatchShieldAgentTickExclusionBindingTests` and
`IsExcludedTargetNamespace_InstalledCallbackShimTypes_ReturnsTrue` PASS (not inconclusive). Full Test:
`Failed: 0`, `Total` = T0 + 130. Then:

```bash
cd /c/Users/mikew/source/repos/taom-port/107 && grep -cE 'private static Exception\? Shield\w+\(Exception __exception\)' Dependencies/Foundation/PatchShield.cs; grep -c 'MethodBase __originalMethod' Dependencies/Foundation/PatchShield.cs; grep -cE '^\s+"TaleWorlds\.' Dependencies/Foundation/PatchShieldPolicy.cs; cat Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/PatchShieldPolicy.cs | grep -cE 'Patch9[2-9]|Patch10[0-9]|Formation\.|CreatureBandit|v1\.5\.3'; grep -c 'PatchShield.WriteRepeatCheckpoint()' Dependencies/SubModule.cs; grep -c 'main menu reached' Dependencies/SubModule.cs; grep -cE '^\s*\[(TestMethod|DataTestMethod)\]' TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs
```

**Verify**: `2`, `0`, `4` (three Gauntlet prefixes and the one method entry), `0`, `1`, `0`, `30`.

**Commit E.** `git add Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/PatchShieldPolicy.cs Dependencies/Foundation/ShieldCoverage.cs Dependencies/Foundation/LogRepeatLimiter.cs Dependencies/Foundation/DiagLog.cs Dependencies/Foundation/RethrowStackPreserver.cs Dependencies/SubModule.cs TAOM.Tests/Infrastructure/Dependencies`;
`107-tmp\msg-E.txt`:

```text
perf(patchshield): v2.0.28 - skip callback shims, no per-call lookup

PatchShield (TAOM.Dependencies) no longer wraps the engine's
native-to-managed callback shims. Its second pass at the first game
start attached a finalizer to each: this laptop's diag.log of
2026-09-02 shows that pass attaching 354 finalizers, about 247 of them
on the shims. Crash capture already covers the shims it needs.

Its finalizers no longer take Harmony's original-method parameter,
which made every shielded method call GetMethodFromHandle on every
call; after a throw the shield finds the method from the stack. What
it swallows, rethrows and unpatches is unchanged. A method-level
exclusion list joins the namespace list, starting with
Mission.TickAgentsAndTeamsImp: a swallow there would skip the line
that marks the agent tick done and freeze the next frame.

diag.log: the pass line reports seen and attached methods and its
time; a skipped method is named once; a repeating swallow line is
written once and counted, with a running count at each mission start
and a total at a clean exit; pass 2 is said to run at game start.

TAOM.Dependencies changed: a release must ship TAOM and
TAOM.Dependencies from one build.

Research: Harmony 2.4.2 (as shipped) GetOriginalMethodFromStackframe,
Patches.Owners; taom-src v1.4.8 Mission.TickAgentsAndTeamsImp,
MBGameManager.OnGameInitializationFinished,
MBSubModuleBase.OnBeforeMissionBehaviorInitialize
Not-tested: pass timing and the skip line in a live diag.log
Ported-from: 2a3f71b12b95be0a7c7f46583ca99de20faafe0f
Ported-from: 73792944591e214595515f7c68bbb7c3265b0e83
Ported-from: 578ac7d617f1fd0ba62827c9f6960611708c4cb4
Ported-from: 31a31f16b4ca3ac8384066b23968beac26e38afd
Ported-from: 0bf2409e668db7382e02ea3d0ce2162ddb65cacb
Ported-from: 900920dca4daadbc2684c80e2f5c997f2235b395
Ported-from: 88408869fbbfcc7d1aa68c145b71fa9dd8645dd4
Ported-from: 30abf550b7fa5613984bc3ec55cde80aa86f4ba1
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618
Ported-from: d90b4be31ad6988ac120d355747d5ebb0926e4df
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Step B3: TAOM.Dependencies docs at the end state

**B3.1 Maintenance doc** (every commit in `c79a58521^..5ee49d05f` touching it is this plan's):
`git diff c79a58521^ 5ee49d05f -- docs/migration/dr3-maintenance.md | git apply --check && git diff c79a58521^ 5ee49d05f -- docs/migration/dr3-maintenance.md | git apply`.
Then in the paragraph starting `**Callback shims excluded (2026-09-23, plan 007):**`:
- replace ``88 classes in v1.5.3: the three `*CallbacksGenerated` shims plus 79 managed-to-native `ScriptingInterfaceOf*` wrappers, `CallbackManager` and `ScriptingInterfaceObjects` ``
  with ``87 classes in v1.4.8: the three `*CallbacksGenerated` shims plus 78 managed-to-native `ScriptingInterfaceOf*` wrappers and three each of `CallbackManager` and `ScriptingInterfaceObjects` ``;
- delete the sentence that starts `With Native2Managed capture off at launch the shims carry no TAOM finalizer at all`
  (since Part A, `AttachAll` always runs).
The class count must read 20 (1.4.x had 17; this WP adds `RethrowStackPreserver`, `ShieldCoverage`,
`LogRepeatLimiter`); `ls Dependencies/Foundation/*.cs | wc -l` prints `19` (one file holds two types).

**B3.2 Crash-report performance.** In `docs/features/crash-report.md`, replace the lines starting
`- **Boot cost:` and `- **Steady-state cost:` with lines 282 and 283 of
`git show 5ee49d05f:docs/features/crash-report.md`, then redo A6.1 item 8 on the new Boot cost line.
Under `## Changelog`, after the A6.1 bullet, add:
``- <today, YYYY-MM-DD>: **Ported plans 007 and 034 to the 1.4.x line**: PatchShield skips the `ManagedCallbacks` shims, and its finalizer finds the shielded method only after a throw.``

**B3.3 Arena doc.** In `docs/features/arena.md` line 10 (the #331 bullet): after
`` `PatchShield.ExcludedTargetNamespacePrefixes` `` insert
`` (now `PatchShieldPolicy.ExcludedTargetNamespacePrefixes`, plan 007) ``, and replace
`adding ~50µs of reflection per call.` with
`adding a reflection lookup to every call (about 63 ns each, measured in isolation by plan 034, which also removed it).`
This rewrites the line, so replace every long dash in it with a colon, comma or parentheses.

**B3.4 Lifecycle page.** In the Finalizer bullet of
`docs/reference/engine/submodule-lifecycle-and-harmony.md`, replace
`PreserveForRethrow(__exception, __originalMethod);` with `PreserveForRethrow(__exception, site);`.

Docs command: fix dashes and dead links in changed lines until `ai_dashes: 0` and L0 holds.
**Verify**: `git diff --stat` lists only the four docs of B3.

**Commit F.** `git add docs/migration/dr3-maintenance.md docs/features/crash-report.md docs/features/arena.md docs/reference/engine/submodule-lifecycle-and-harmony.md`;
`107-tmp\msg-F.txt`:

```text
docs(patchshield): v2.0.28 - shim exclusion, method list, repeat counts

The TAOM.Dependencies maintenance doc now covers the rethrow stack
preserver, the callback-shim exclusion, the method-level list, the
seen and attached pass counts, repeat counting and the stack lookup.
The crash-report performance notes and the arena doc's #331 entry
follow plans 007 and 034. Class counts and engine facts are re-cited
to v1.4.8.

Ported-from: c79a585218ad22841c8db1f273e013385512b332
Ported-from: 73792944591e214595515f7c68bbb7c3265b0e83
Ported-from: 578ac7d617f1fd0ba62827c9f6960611708c4cb4
Ported-from: 31a31f16b4ca3ac8384066b23968beac26e38afd
Ported-from: 0bf2409e668db7382e02ea3d0ce2162ddb65cacb
Ported-from: 900920dca4daadbc2684c80e2f5c997f2235b395
Ported-from: 88408869fbbfcc7d1aa68c145b71fa9dd8645dd4
Ported-from: 5ee49d05f1d24e060ecf597cdd75724f1d545618
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Part B done criteria

- [ ] Build exit 0; Test `Failed: 0`, `Total` = T0 + 130.
- [ ] `git diff --quiet 5ee49d05f HEAD -- Dependencies/Foundation/ShieldCoverage.cs Dependencies/Foundation/LogRepeatLimiter.cs Dependencies/Foundation/DiagLog.cs Dependencies/Foundation/RethrowStackPreserver.cs TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs TAOM.Tests/Infrastructure/Dependencies/PatchShieldFinalizerTests.cs TAOM.Tests/Infrastructure/Dependencies/LogRepeatLimiterTests.cs TAOM.Tests/Infrastructure/Dependencies/DiagLogTests.cs && echo same` prints `same`.
- [ ] `git diff 5ee49d05f HEAD -- Dependencies/Foundation/PatchShield.cs | grep -cE '^[-+][^-+]'` prints `10` (three lines at 52-54 and two at 213 and 216, each removed and added).
- [ ] Step B2's greps print `2`, `0`, `4`, `0`, `1`, `0`, `30`; Docs command `ai_dashes: 0`, L0 holds;
      subjects and status as in Part A.

---

## Part C: creature combat-log record and behaviour pin (`c79a58521` items 3 and 4)

### Step C1 (RED): Pin the synthetic combat-log record

Append inside the class of `TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs` (the
engine types are named in full, so no `using` is added):

```csharp
    // TakeDamage registers a blow directly. v1.4.8: the CombatLogData constructor sets DamageType = Blunt
    // (CombatLogData.cs:385) and only MissionCombatMechanicsHelper.GetAttackCollisionResults copies the real
    // type in (:192), which a synthetic blow never runs; crushedThrough prints "crushed through" (:185).
    private static TaleWorlds.MountAndBlade.CombatLogData Compose(bool victimRiderIsAttacker = false, bool victimIsDead = false)
        => CustomAttacksUtils.ComposeSyntheticCombatLog(
            attackerIsHuman: false, attackerIsMine: false, attackerHasRider: true, attackerRiderIsMine: true,
            attackerIsMount: true, victimIsHuman: true, victimIsMine: false, victimIsDead: victimIsDead,
            victimHasRider: false, victimRiderIsMine: false, victimIsMount: false,
            victimRiderIsAttacker: victimRiderIsAttacker);

    [TestMethod]
    public void ComposeSyntheticCombatLog_NeverReportsACrushThrough()
        => Assert.IsFalse(Compose().CrushedThrough, "a synthetic blow never breaks a guard; a knockdown is not a crush-through");

    [TestMethod]
    public void ComposeSyntheticCombatLog_LogsPierce_NotTheConstructorsBlunt()
        => Assert.AreEqual(TaleWorlds.Core.DamageTypes.Pierce, Compose().DamageType);

    [TestMethod]
    public void ComposeSyntheticCombatLog_PassesTheRiderComparisonAndTheVictimsDeath()
    {
        Assert.IsTrue(Compose(victimRiderIsAttacker: true).IsVictimRiderAgentSameAsAttackerAgent);
        Assert.IsFalse(Compose(victimRiderIsAttacker: false).IsVictimRiderAgentSameAsAttackerAgent);
        Assert.IsTrue(Compose(victimIsDead: true).IsFatalDamage);
    }
```

Build. **Verify (RED)**: one `error CS0117` naming `ComposeSyntheticCombatLog`.

### Step C2 (GREEN): Named arguments for the record

In `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`:
1. After `TakeDamage(Agent victim, Agent attacker, ...)` add:

```csharp
    // The combat-log record for a blow TakeDamage registers directly. Named arguments, because fifteen of
    // the constructor's parameters are bools read by position: crushedThrough prints "crushed through"
    // (v1.4.8 CombatLogData.cs:185) and used to receive knockDown, so every creature knockdown the player saw
    // read as a broken guard; the rider check compared the victim with itself where vanilla compares the
    // victim's rider with the attacker (Mission.cs:6462). The constructor sets DamageType to Blunt
    // (CombatLogData.cs:385) and only GetAttackCollisionResults copies the real type in
    // (MissionCombatMechanicsHelper.cs:192), which a synthetic blow never runs. Pure; pinned by
    // CustomAttacksUtilsTests.
    internal static CombatLogData ComposeSyntheticCombatLog(
        bool attackerIsHuman, bool attackerIsMine, bool attackerHasRider, bool attackerRiderIsMine, bool attackerIsMount,
        bool victimIsHuman, bool victimIsMine, bool victimIsDead, bool victimHasRider, bool victimRiderIsMine,
        bool victimIsMount, bool victimRiderIsAttacker)
    {
        var log = new CombatLogData(
            isVictimAgentSameAsAttackerAgent: false,
            isAttackerAgentHuman: attackerIsHuman,
            isAttackerAgentMine: attackerIsMine,
            doesAttackerAgentHaveRiderAgent: attackerHasRider,
            isAttackerAgentRiderAgentMine: attackerRiderIsMine,
            isAttackerAgentMount: attackerIsMount,
            isVictimAgentHuman: victimIsHuman,
            isVictimAgentMine: victimIsMine,
            isVictimAgentDead: victimIsDead,
            doesVictimAgentHaveRiderAgent: victimHasRider,
            isVictimAgentRiderAgentIsMine: victimRiderIsMine,
            isVictimAgentMount: victimIsMount,
            missionObjectHit: null,
            isVictimRiderAgentSameAsAttackerAgent: victimRiderIsAttacker,
            crushedThrough: false,
            chamber: false,
            distance: 0f);
        log.DamageType = DamageTypes.Pierce;
        return log;
    }
```

2. Replace the line `CombatLogData combatLogData = new(false, attacker.IsHuman, ...);` (1.4.x `:206`) with:

```csharp
        CombatLogData combatLogData = ComposeSyntheticCombatLog(
            attackerIsHuman: attacker.IsHuman, attackerIsMine: attacker.IsMine,
            attackerHasRider: attacker.RiderAgent != null,
            attackerRiderIsMine: attacker.RiderAgent != null && attacker.RiderAgent.IsMine,
            attackerIsMount: attacker.IsMount, victimIsHuman: victim.IsHuman, victimIsMine: victim.IsMine,
            victimIsDead: victim.Health <= 0f, victimHasRider: victim.RiderAgent != null,
            victimRiderIsMine: victim.RiderAgent != null && victim.RiderAgent.IsMine,
            victimIsMount: victim.IsMount, victimRiderIsAttacker: victim.RiderAgent == attacker);
```

3. Replace the comment block starting `// The 3 positional ints below map to` (1.4.x `:179-185`) with:

```csharp
        // The 3 positional ints below map to (affectorWeaponSlotOrMissileIndex, StrikeType, DamageType), and
        // DamageType matches blow.DamageType (LOTRAOM had `2`, Blunt; plan file 2026-05-27). The combat log does not
        // read it: vanilla copies the collision type into CombatLogData.DamageType inside
        // MissionCombatMechanicsHelper.GetAttackCollisionResults (v1.4.8 :192), which a synthetic blow never runs,
        // and the CombatLogData constructor sets Blunt (CombatLogData.cs:385). ComposeSyntheticCombatLog sets the
        // log's type after the constructor.
```

Build; filtered `FullyQualifiedName~CustomAttacksUtilsTests`: `Failed: 0` (13 tests). Full Test:
`Total` = T0 + 133.

**Commit G.** `git add Main/Features/AdvancedCombat/CustomAttacksUtils.cs TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsTests.cs`;
`107-tmp\msg-G.txt`:

```text
fix(combat): v2.0.28 - creature blows log Pierce, never a crush-through

A blow TAOM registers directly (creature attacks through
CustomAttacksUtils.TakeDamage) built its combat-log record with
positional arguments: knockDown went into crushedThrough, so every
creature knockdown the player saw read "Crushed through!", and the
rider check compared the victim with itself. The constructor also sets
the damage type to Blunt, and only the engine's own collision code
copies the real type in, so every creature blow logged as Blunt.

The record is now built with named arguments: never a crush-through,
the victim's rider compared with the attacker as vanilla does, and the
damage type set to Pierce to match the blow.

Research: taom-src v1.4.8 CombatLogData (constructor :367, Blunt at
:385, crush text at :185), MissionCombatMechanicsHelper :192,
Mission.GetDefendCollisionResults :6462
Not-tested: the combat log in a live battle
Ported-from: c79a585218ad22841c8db1f273e013385512b332
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Step C3: Pin the creature behaviours to MissionLogic

`git diff c79a58521^ c79a58521 -- TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs | git apply --check`,
then the same without `--check`. Delete the line `            typeof(TAOM.Features.Elk.ElkMissionBehavior),`
(Elk is 1.5.x-only). On 1.4.x only `WarRamMissionBehavior.cs:15-17` carries the rule in its doc
comment, so replace the new test's four comment lines (from `// The creature features' own mission
behaviors carry` through `(#636 added the elk).`) with:

```csharp
        // WarRamMissionBehavior's doc comment states the rule ("MUST be : MissionLogic, NEVER : MissionBehavior
        // ... pinned by BehaviorTreeMissionLogicInheritanceTests"); this is that pin, for all five creature
        // behaviours. Each overrides OnMissionTick, so a MissionBehavior-only base would be the looter-battle
        // NRE again. Add a new creature's behaviour here when it is written.
```

This pins behaviour that is already correct on 1.4.x (`WargMissionBehavior.cs:14`,
`SpiderMissionBehavior.cs:22`, `ElephantMissionBehavior.cs:27`, `MumakilMissionBehavior.cs:19`,
`WarRamMissionBehavior.cs:32` all derive from `MissionLogic`), so it passes at once; that is expected
for a regression pin.

Build; filtered `FullyQualifiedName~BehaviorTreeMissionLogicInheritanceTests`: `Failed: 0`, 2 passed.
Full Test: `Total` = T0 + 134.

**Commit H.** `git add TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs`;
`107-tmp\msg-H.txt`:

```text
test(creatures): v2.0.28 - pin creature behaviours to MissionLogic

The warg, spider, elephant, mumakil and war ram mission behaviours
each override OnMissionTick and must derive from MissionLogic, never
MissionBehavior alone (the looter-battle NRE). Only the war ram's doc
comment said so, and the test it named covered only
BehaviorTreeMissionLogic. A test now pins all five.

Ported-from: c79a585218ad22841c8db1f273e013385512b332
```

Commit in the NEXT Bash call. **Verify**: one new commit; empty status.

### Part C done criteria

- [ ] Build exit 0; Test `Failed: 0`, `Total` = T0 + 134.
- [ ] `grep -c 'knockDown, false, 0f' Main/Features/AdvancedCombat/CustomAttacksUtils.cs` prints `0`;
      `grep -c 'Elk' TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs` prints `0`.

### Step Z: Review gate and hand-off

1. Run the full Done criteria.
2. If your session can invoke skills, run `/deep-review` (C# across both TAOM modules and one
   single-owner file) and point it at "Maintenance notes". Act only on findings that change runtime
   behaviour or break this plan; land each fix as one extra commit
   `fix(crash-report): v2.0.28 - review follow-ups` (or `fix(patchshield): ...`), test first, and
   re-run the Done criteria. Never rewrite earlier commits. If you cannot invoke skills, say so; the
   integrator runs it. Never run `/review-codex` or `/codex-verify` (paid; maintainer only).
3. Write the hand-off report.

## Disposition (every commit in the pack)

Simulation re-run this session:
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.
A single commit replayed alone conflicts because its series predecessors are absent; the end-state
method avoids that (its patches were dry-run with `git apply --check` this session).

| 1.5.x commit | Subject | Simulation (code paths) | Disposition |
|---|---|---|---|
| `c79a58521` | Add scripts for generating and validating animal animation clips | 1: `CustomAttacksUtils.cs`, `CareerAgentStatService.cs`, `ICareerAgentStatService.cs`, `ElephantLikeAttackTasks.cs`, `ElephantLikeCombatProfile.cs`, `MumakilMissionBehavior.cs`, `taom_career_starting_equipment.xml`, four tests, tools; crash files clean | **Adapted, three slices**: crash half in A (preserver, shield rethrows, signature, renderer, tests, RCA, engine Finalizer bullet); combat-log fix hand re-implemented in G (the 1.5.x hunk sits on `ComposeBlowFlags` and Elk parameters); inheritance pin minus Elk in H. Harness half: WP103. Not ported: Elk, Animalia, `IsRegisteringSyntheticBlow`, rider-owned blows (#643), MountChargeMultiplier, content XML, tools, ADR-011, its Patch15 registry paragraph, lessons |
| `2a3f71b12` | perf(patchshield): v2.0.30 - skip the ManagedCallbacks callback shims | 0 | **Transplanted** (E) |
| `737929445` | docs(patchshield): v2.0.30 - pass 2 runs at game start, not the menu | 1: `dr3-maintenance.md`, `v1.5.2-impact.md` | **Transplanted**: `Dependencies/SubModule.cs` (E), dr3 (F); `v1.5.2-impact.md` absent on 1.4.x |
| `578ac7d61` | fix(patchshield): v2.0.30 - review follow-ups for plan 007 | 1: `PatchShield.cs`, `PatchShieldPolicy.cs`, `Dependencies/SubModule.cs`, `PatchShieldPolicyTests.cs` | **Transplanted** (E, F; shim BindingVerification test included; counts re-cited to v1.4.8: 87 classes, 78 wrappers); `arena.md` in F; review record, prompt, RCA, REVIEW-LOG, lessons skipped |
| `31a31f16b` | fix(patchshield): v2.0.30 - convergence fixes for plan 007 | 1: `Dependencies/SubModule.cs` | **Transplanted** (E, F); "+140" replaced by this laptop's +119 |
| `0bf2409e6` | fix(patchshield): v2.0.30 - apply maintainer decisions for plan 007 | 1: `PatchShield.cs`, `PatchShieldPolicy.cs`, `Dependencies/SubModule.cs`, `PatchShieldPolicyTests.cs` | **Transplanted** (E: `ShieldCoverage`, seen/attached, timed pass line; F) |
| `900920dca` | fix(patchshield): v2.0.30 - review follow-ups for plan 007 | 1: `PatchShield.cs`, `PatchShieldPolicyTests.cs`, `ShieldCoverageTests.cs` | **Transplanted** (E, F); class count re-derived as 20 |
| `88408869f` | docs(patchshield): v2.0.30 - shim comments after merging 006 and 007 | 1: `PatchShieldPolicy.cs`, `dr3-maintenance.md` | **Transplanted** (E, F); also the plan 006/007 end-state reference |
| `0ad253d5c` | docs(changelog): v2.0.30 - PatchShield skips the callback shims | 1: `CHANGELOG.md` only | **Skipped**: CHANGELOG (D1) |
| `b0a93fb4c` | fix(patchshield): v2.0.30 - convergence fixes for plan 007 | 1: CHANGELOG and review records only | **Skipped**: bookkeeping |
| `8bd44b924` | fix(crash-report): v2.0.30 - drop four finalizers on empty virtuals | 0 | **Transplanted** (B) |
| `1d94df9c4` | perf(crash-report): v2.0.30 - allowlist six callback shims, not 247 | 0 | **Transplanted** at the 16-entry end state (B); decision 107-1 |
| `9cfb91b15` | fix(crash-report): v2.0.30 - make both capture toggles live | 0 | **Transplanted** (B); `SubModule.cs` by hand with the `5f6a96d2e` resolution |
| `fb52e619d` | perf(crash-report): v2.0.30 - log repeat suppressions at powers of ten | 0 | **Transplanted** (B); creates `CrashBundleThrottle.IsLoggedOccurrence` for WP109 |
| `6fe83bcad` | docs(crash-report): v2.0.30 - correct capture cost and coverage | 1: `crash-report.md`, `hero-race.md`, `gauntletui-viewmodel-screen.md`, `patch-targets.md` | **Transplanted** (D); `hero-race.md` and `gauntletui-viewmodel-screen.md` hunks skipped (their paragraphs do not exist on 1.4.x; the `MapConversationTableau.OnTick` under `ScreenManager.Tick` chain does hold on v1.4.8) |
| `65b691b6a` | fix(crash-report): v2.0.30 - review follow-ups for plan 006 | 1: `CrashReportSettings.cs`, `Native2ManagedPatcher.cs`, `Native2ManagedTargets.cs`, `Patch37_CrashReport.cs`, four tests, `HarmonyPatchBindingTests.cs` | **Transplanted** (B, D); `HarmonyPatchBindingTests` limited to the two `internal` helpers |
| `707275290` | fix(crash-report): v2.0.30 - convergence fixes for plan 006 | 1: `Native2ManagedTargets.cs` | **Transplanted** (B, D); dispatch claim verified on v1.4.8 |
| `42624b95b` | fix(crash-report): v2.0.30 - apply maintainer decisions for plan 006 | 1: `Native2ManagedPatcher.cs`, `Native2ManagedTargets.cs`, two tests | **Transplanted** (B, D) |
| `8e6b0935c` | fix(crash-report): v2.0.30 - apply maintainer decisions for plan 006 | 1: same four | **Transplanted** (B, D) |
| `8c84fa20e` | fix(crash-report): v2.0.30 - review follow-ups for plan 006 | 1: `CrashReportSettings.cs`, `AppDomainExceptionHook.cs`, `CrashReportPatchHelper.cs`, `Native2ManagedPatcher.cs`, `Native2ManagedTargets.cs`, four tests | **Transplanted** (B, D) |
| `9e8c99bdb` | fix(crash-report): v2.0.30 - convergence fixes for plan 006 | 1: `AppDomainExceptionHook.cs`, `Native2ManagedPatcher.cs`, three tests | **Transplanted** (B, D) |
| `5ee49d05f` | perf(patchshield): v2.0.32 - find the original method only on a throw | 1: `PatchShield.cs`, `PatchShieldPolicy.cs`, `RethrowStackPreserver.cs`, `CreatureBanditsWiringTests.cs`, `Patch92BindingTests.cs` | **Transplanted** (E as trimmed whole-file copies; F). Test edits to absent files skipped; `troll-brute-force.md`, `coop-interop.md`, `bannerlord-together-compat.md` hunks skipped (absent text or co-op, D7) |
| `30abf550b` (shared, WP130) | Add unit tests for various tools and functionalities | 1: many | **Mechanism only** (E): `IsExcludedTargetMethod`, the list, `IsExcludedTarget` consulting it, two policy tests. Patch92 entries and the rest stay with WP130 |
| `6eb2e1b90` (entry, WP117) | feat(mission-perf): v2.0.32 - per-behaviour tick profiler and hitch log | 1: many | **Agent-tick entry, `FormatHotMethodSkip`, `LogHotMethodSkip`, two tests** (E), comment rewritten for v1.4.8. The profiler stays with WP117 |
| merge `d90b4be31` | merge(improve): v2.0.30 - plan 007 PatchShield skips callback shims (#651) | not simulated | **End-state source**; its resolutions are bookkeeping |
| merge `5f6a96d2e` | merge(improve): v2.0.30 - plan 006 crash-capture boot cost (#650) | not simulated | **End-state source**; its `SubModule.cs` resolution is A4.3, its doc resolutions arrive with A6.1 |

Related, not taken: `43ca92ee9` (WP117; this plan mirrors only its removal of the duplicate
`IsExcludedTargetMethod_MissionOnTickAndOnPreTick_ReturnsFalse`), `daba6f0a0`, `f6bf0e588` (WP117),
`346f3e093` (WP118), `943ab74c0` (WP125), `d469914ab` (WP130), `48d66b59a` (WP136),
`6ce20f6ed`/`bd1ac8292` (D7; WP106 takes the lesson), `4e408e0e0` (WP137), `c80006994`/`743ce2e1d`/
`ff84e1b88`/`db1a7166b` (WP115), `649cc30ff`/`ecaa67d26` (WP109), `b0154d86e` (WP130), `f75288ebf`
(WP106, a dependency).

## Test plan

- Part A (64 counted results): `RethrowStackPreserverTests` 19 (real in-process Harmony patches),
  `CrashSignatureCalculatorTests` +5, `PlainTextCrashReportRendererTests` +2, `AppDomainExceptionHookTests` 3,
  `CrashReportPatchHelperTests` 5, `Native2ManagedBridgeTests` 10, `Native2ManagedTargetsTests` 6 (one
  BindingVerification), `Patch37TargetShapeTests` 1 (BindingVerification), `CrashBundleThrottleTests` +12 data
  rows, `BattleLoadStallWatchdogTests` +1 (new).
- Part B (66): `PatchShieldPolicyTests` 16 to 30, `ShieldCoverageTests` 4, `PatchShieldFinalizerTests` 28 (one
  `RequiresGame`), `LogRepeatLimiterTests` 16, `DiagLogTests` 3, `PatchShieldAgentTickExclusionBindingTests` 1.
- Part C (4): `CustomAttacksUtilsTests` +3, `BehaviorTreeMissionLogicInheritanceTests` +1.
- Cells: both toggles on and off; service reachable, unreachable, throwing, re-entered; main thread,
  worker, unset id, read-only `Data`; throw site kept on every hand-back; allowlist and resolve skips;
  suppression cadence; PatchShield swallow, rethrow, strip, nested shields, unresolved original, repeat
  counts (failed writes, 256-line cap, checkpoint); both exclusion lists; watchdog flag; combat-log record.
- Not unit-testable (in the `Not-tested:` trailers): boot attach line and pass-2 timing, a live off-thread
  capture, the live toggles, the on-screen combat log, a stalled load.
- Patterns: `Native2ManagedBridgeTests` with `RecordingCrashService`; `PatchShieldFinalizerTests` for real
  Harmony patches on scratch targets.

## Done criteria

Run after Step C3 (and after any review follow-up). ALL must hold:

- [ ] `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors.
- [ ] `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` reports `Failed: 0`,
      `Total` = T0 + 134 (Step A4's convention) and `Skipped` = T0's skipped.
- [ ] The Part A, B and C done criteria still hold, except that
      `Dependencies/Foundation/RethrowStackPreserver.cs` now matches `5ee49d05f` (Part B's check), not
      `c79a58521` (`5ee49d05f` changed it).
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD` lists only in-scope files;
      `grep -c CHANGELOG.md` on that list prints `0`.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
      prints nothing (eight subjects, more only for review follow-ups).
- [ ] Docs command: `ai_dashes: 0`; `dead_links`, `orphan_features` equal L0. `git status --porcelain` empty.
- [ ] The hand-off records the release pairing: commits A and E change TAOM.Dependencies, so any release
      containing this branch ships `TAOM.dll` and `TAOM.Dependencies.dll` from one build (#371). A new
      `TAOM.dll` beside a stale `TAOM.Dependencies.dll` (the public build pairs a 2026-09-30 TAOM with a
      2026-09-14 Dependencies, E3 line 2) fails at the first callback capture, because the bridge calls
      `RethrowStackPreserver`, which the stale DLL lacks.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0.3 fails (`TryPatchCategory` absent: WP104 not merged; no Harmony patch on
  `TickAgentsAndTeamsImp`: WP106 not merged; a file of this WP already exists), or Step 0.4 prints
  anything but `same`.
- A `git apply --check` fails and the `--3way` fallback leaves a conflict in C# code the step does not
  describe.
- A RED step does not fail as described, or a GREEN step leaves a failure other than those named.
- `Native2ManagedTargetsTests.All_ResolvesEveryShimAgainstTheInstalledEngine`,
  `Patch37TargetShapeTests.EveryPatch37Target_IsNotAnOverridableVirtual`,
  `IsExcludedTargetNamespace_InstalledCallbackShimTypes_ReturnsTrue` or
  `PatchShieldAgentTickExclusionBindingTests` is inconclusive or fails: an engine fact above does not
  hold on this install. Report the message; do not decompile and improvise.
- A new test fails with a `TypeLoadException`, `FileNotFoundException` or a native exception instead of
  an assertion. Never add `[Ignore]` or a category to hide it.
- The work seems to need a file outside Scope (`Main/IoC.cs`, a csproj, `.claude/settings*.json`, another
  `Main/SubModule.cs` line, an exclusion entry beyond `Mission.TickAgentsAndTeamsImp`), or the
  `SubModule.cs` crash block or the comment above it differs from what A4.3 quotes.
- The Docs command reports a new dead link or orphan you cannot fix inside the touched docs.
- A hook denies a commit (report its message verbatim), or a verification fails twice after a
  reasonable fix attempt.

## In-game checks owed (maintainer)

On the merged branch, once, with TAOM and TAOM.Dependencies deployed from one build:

1. **Pairing**: the TAOM log (`taom_debug_<timestamp>.log`) has a `[BuildStamp]` line without `MISMATCH`.
2. **Boot**: `[CrashReport] main thread id N recorded at Subscribe()` and
   `[CrashReport] Native2Managed: attached 16 of 16 Finalizer(s) in X ms` (was `attached 247 Finalizer(s)`),
   no `target not found, skipped` line. Note X.
3. **First game start**: `<game>\Modules\TAOM.Dependencies\diag.log` shows
   `OnGameInitializationFinished: entered (game start: campaign, custom battle or editor)`, then
   `[PatchShield] shield pass: +N new, ... (seen: S, attached: A) in T ms (P ms/attach)` with N about 247
   lower than the `+354 new` this laptop logged on 2026-09-02, and one
   `not shielding TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp (ExcludedTargetMethods, a hot target), patched by ...` line.
4. **Capture**: in Mod Options, TAOM's crash-report settings, the QA dev-trigger group, set "Throw On Next
   Application Tick" once: one bundle and the inquiry; its `report.txt` has Stack Frames. Set it again: the
   TAOM log shows `occurrence #2, bundle suppressed (logged at 1, 2, 10, 100, ...)` and no second bundle.
   Do not use "Throw On Next Mission Tick" here until WP106's recovery is verified (on v1.4.8 a swallowed
   mission-tick throw can hang the next frame).
5. **Battle**: a 500-agent field battle to the end, then a clean exit: no new `[CrashReport]` line, no
   `could not tell which shielded method` line in diag.log, and at exit
   `SESSION SUMMARY: shielded A of S patched method(s) seen, ... rethrew R with the stack preserved`.
6. **Combat log (Part C)**: an elephant or mumak trample that knocks the player's character down
   (on 1.4.x only `ElephantLikeAttackTasks` passes `knockDown` to `TakeDamage`) shows no "Crushed
   through!"; a warg or spider bite on the player's character reads as pierce damage.
7. **Hints**: both capture toggles' hints say they take effect immediately.

## Hand-off report

Status (DONE, or which Parts landed and what blocked the rest); branch name; each commit's short SHA and subject; T0, S0, L0 and the final counts; any row of an older plan in `plans/README.md` the integrator should update (read it from the main checkout; report, never edit); the data-row
convention seen in A4; how A6.1's conflict was resolved; whether and how Step A2 changed
`.claude/rules/harmony-patches.md`; any `--3way` resolution; the `/deep-review` outcome (or that the
session could not run it); the release-pairing note; the in-game checks owed.

## Maintenance notes

- **Later WPs add exclusion entries, one each, with a binding test**: WP117 (`WaitTickCompletion`,
  `TickComponents`, `Campaign.Tick`, `CampaignEvents.Tick`, and `43ca92ee9`'s comment consolidation),
  WP118 (`MBObjectManager.LoadXML`/`CreateMergedXmlFile` and the `CampaignEventDispatcher` handlers; V08
  asks it to probe `IsExcludedTargetMethod` before applying Patch99 so a stale Dependencies DLL cannot
  strip it), WP125 (`GameMenuVM.OnFrameTick`), WP130 (Patch92 and Patch93 targets, if those features
  port). None edits the mechanism.
- **Other consumers**: WP109's `649cc30ff` calls `CrashBundleThrottle.IsLoggedOccurrence`; WP112's
  finalizer narrowing and every value-returning finalizer call `RethrowStackPreserver.PreserveForRethrow`;
  WP130's `b0154d86e` edits `Native2ManagedTargets.cs`; WP136 adds `48d66b59a`.
- **Allowlist trade-off (107-1)**: a managed throw in one of the 231 unwrapped shims now reaches the
  engine; whether it unwinds into a Patch37 finalizer such as `Mission.Tick` or ends the process is
  UNVERIFIED. If a bundle or a Windows event shows such a crash, add that one shim to
  `Native2ManagedTargets` with its reason and its literal name in `Native2ManagedTargetsTests`, never the
  whole sweep. `Mission_TickAgentsAndTeams` stays off: a swallow there freezes the battle. Mission-start
  throws are a separate path this plan leaves alone: SaveShield's MISSION-INIT finalizer on
  `MissionState.FinishMissionLoading` (`Dependencies/Foundation/SaveShield.cs:84`) is the innermost
  swallower and logs only to `Modules/TAOM.Dependencies/diag.log` (V04), so a missing crash bundle after
  a failed mission start proves nothing; read diag.log too (WP109 owns that path).
- **Deep-dive findings closed here**: FREEZE-THREADING-05 (A4, A5), RUNTIME-PERF-03, LOAD-TIME-06 and
  RELEASE-INTEGRITY-07 (Parts A and B), LOAD-TIME-03 part 2 (Part B; part 1, the two Patch61 finalizers,
  is WP114's Step B4).
- **Merge the branch whole.** Commit B takes `Mission_TickAgentsAndTeams` off capture and commit E
  adds the PatchShield exclusion that WP106's maintenance notes ask to land in the same merge; never
  take B without E.
- **Watchdog bundle (107-2)** records its Mission and Campaign sections as skipped; the STILL LOADING
  marker and the `[BattleLoad]` phase log remain. If triage misses mission state, collect it on the
  main thread, never from the timer.
- **WP142**: the ported RCA's Lessons section names two entries `c79a58521` added to
  `docs/reviews/lessons/harmony-il.md` and `build-tooling-workflow.md`; they reach 1.4.x with
  WP142's lessons sync, not here.
- **1.5.x moved during planning** (`e45f6df2c` to `ca1cba35f`): its only touch of this plan's files is a
  Patch141 exclusion entry (`a4317fd8b`, a commander feature, not ported) and a teardown paragraph in
  `harmony-patches.md` (`f2ee9a16f`).
- **Skip-line wording**: `FormatHotMethodSkip` (1.5.x text, pinned by the ported tests) says "a hot
  target" for every entry, though the agent tick is excluded for the swallow, not the cost. Cosmetic.
- **Not fixed here**: PatchShield still wraps `Mission.OnTick` and `MissionState.TickMissionAux` when
  anything patches them (WP106's recovery is the remedy for an OnTick throw after the flag clears);
  the strip is not culprit-only and `com.taom.mod` is not a protected owner; `ResolveShieldedOriginal`
  never ran on Mono (Proton).
- **Reviewer probes**: (1) `Native2ManagedBridge.HandleOrPassThrough` has one exit and preserves every
  non-null result; (2) `AppDomainExceptionHook.IsOffMainThread` treats an unset id (0) as off-main;
  (3) `SubModule.cs` applies Patch37 first and reads no MCM value; (4) `ExcludedTargetMethods` holds
  exactly the agent tick, with v1.4.8 citations; (5) `ResolveShieldedOriginal` judges one frame and never
  climbs; (6) `LogRepeatLimiter` retries failed writes and never drops a line past its table;
  (7) `RequestStallBundle` is the watchdog's only path into the crash service; (8)
  `ComposeSyntheticCombatLog` changes only `crushedThrough`, the rider comparison and the damage type.
- **Deferred**: recovering the handshake after an agent-tick throw (V04's void `Priority.First`
  finalizer on `TickAgentsAndTeamsImp` that restores `tickCompleted`, FREEZE-THREADING-01 fix (b)'s
  agent-tick half; WP106 lands a launch-aware `Mission.OnTick` version, D17; new work, orchestrator
  decides); an in-game per-call cost measurement (the 1.5.x figures are a benchmark); a mission-tick
  dev-trigger run after WP106 lands; whether TAOM's own Harmony owner should be protected.
