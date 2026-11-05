# Plan 106: Park off-thread callback writes (#634) and recover the mission tick handshake safely

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md`: every WP branch shares it, so the
> integrator records this plan's status and test count when merging. Put the numbers listed under
> "Hand-off report" in your final message instead. Two parts on one branch: Part A (the
> f75288ebf port) lands alone; Part B (the D17 recovery and the bd1ac8292 lesson) builds on it, in
> the same session or a later one. Each part ends on a green tree with its own done criteria.
>
> **Drift check (run first, Step A0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/BehaviorTreeWrapper Main/Features/AdvancedCombat Main/Features/BattleLoadDiagnostics Main/Features/CareerSystem/CareerPerkMissionBehavior.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs Main/Features/FieldCommission/FieldCommissionMeritService.cs Main/Features/MountDespawn Main/Features/Warg/WargMissionBehavior.cs Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs Main/Features/MissionTickGuard Dependencies/Foundation/SaveShieldPolicy.cs Main/SubModule.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs .claude/rules/harmony-patches.md .claude/rules/csharp-architecture.md docs/features docs/reference/harmony-patch-registry.md docs/reviews/lessons/harmony-il.md`
> Earlier WPs of this programme are expected to have changed `Main/SubModule.cs` (WP104, WP105)
> and some `docs/features/*.md` (WP104 edits `battle-load-diagnostics.md` and `crash-report.md`).
> Compare the live files against "Current state" and STOP only on a real mismatch (an anchor in
> Step A0 missing or duplicated, or a writer excerpt below no longer matching).

## Status

- **Priority**: P1 (D21: #634 is defence in depth, the freeze cause is unproven; the D17 recovery
  closes an engine hazard confirmed in v1.4.8 code)
- **Effort**: L overall. Part A: M (a 51-file cherry-pick, 11 conflicted paths, one code
  hand-merge). Part B: M (two new production files, four test files, docs; TDD)
- **Risk**: MED (mission-tick path; Part B writes a private engine field. Both changes act only
  where the game would otherwise stay frozen, and Part B arms only after verifying itself)
- **Depends on**: WP103 (`plans/103-executor-safety-harness.md`), WP105
  (`plans/105-composition-root.md`); through them WP101, WP102 and WP104
- **Category**: bug
- **Wave / lane**: wave 1, lane A, first in the lane (WP107, WP109, WP113, WP117 and WP124 build on
  this WP)
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md`; the commit body is the entry), D2 (hand-wired:
  neither BattleLoadDiagnostics nor the new guard has a 1.5.x module), D7 (from bd1ac8292 only
  the tickCompleted lesson and comments), D8 (subjects carry `v2.0.28`), D9 (no CI: every gate
  runs locally), D16 (no locked RNG; document the multi-thread path instead), D17 (launch-aware
  recovery on `Mission.OnTick`), D19 (git only through the Bash tool), D21 (#634 is P1)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: Part A: port of #634 (and #595, the tripwire it extends). Commit B1 (the recovery)
  is new work with no issue yet: the orchestrator creates the issue before Part B lands. Commit
  B2: port of #965 (the tickCompleted lesson only).

## Why this matters

Players on the public 1.4.8 build report battles that freeze for good while combat audio keeps
playing (brief evidence item 3). Two v1.4.8 mechanisms produce that shape, and 1.4.x defends
against neither:

1. **Off-thread writes.** Bannerlord raises agent callbacks such as `OnAgentRemoved` on the thread
   native chooses. The Harad player's 2026-11-01 log (E4) caught `OnAgentShootMissile`,
   `OnAgentDismount` and `OnAgentPanicked` off the main mission thread (threads 21, 22, 20; main
   17; brief item 5, E4 lines 2265, 2266 and 2986 per V05). Three TAOM handlers write a `Dictionary`
   from such callbacks with no lock (V05 section 2a rows 1 to 3), and five more write a `List`, an
   int, a `Vec3` or the UI. A `Dictionary` corrupted by two writers can spin forever on .NET
   Framework, and a spinning agent-tick thread leaves the next frame waiting. Whether a corrupting
   pair ever occurs is unproven (V05, D21), so 1.5.x's fix `f75288ebf` ports as defence in depth,
   together with its `[MissionStall]` watchdog, which turns the next freeze of this class into a
   stack trace in the player's log.
2. **The tick handshake.** v1.4.8 `Mission.OnTick` clears the private `tickCompleted`
   (`Mission.cs:3740`) before the `OnMissionTick` loop; only the agent tick sets it again (`:3613`);
   the next frame's `WaitTickCompletion` spins with no timeout (`:3585-3591`). On 1.4.x every
   catcher above that window swallows (V04 section 2B), so one exception out of any `OnMissionTick`
   override freezes the game for good. 10 of TAOM's 19 `OnMissionTick` overrides have no `try`
   (V04), and the shipped MCM QA toggle "Throw On Next Mission Tick" throws exactly there. D17 adds
   a launch-aware recovery: restore the flag only when the throwing call launched no agent tick.

Neither mechanism is shown to be the cause of E3's or E4's freezes (V04 section 4, V05 section 6).
After this WP both hazards are closed or instrumented, and a mission-tick throw costs one agent
tick plus a crash report instead of the session.

## Current state

### Executor environment

- Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 (build 119303) at
  `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord`
  (`BANNERLORD_GAME_DIR`). Never write into the game install.
- Base branch: local `bannerlord-1.4.5` after WP101, plus every WP merged before this one (WP102,
  WP103, WP104 and WP105 must be among them).
- Worktree: `C:\Users\mikew\source\repos\taom-port\106`, branch
  `port148/106-offthread-and-tick-safety`. A `cd` does not persist between Bash calls: start EVERY
  command with `cd /c/Users/mikew/source/repos/taom-port/106 &&`.
- Commit message files go in `C:\Users\mikew\source\repos\taom-port\` (outside the worktree).
- Python: `python` (or `/c/Python314/python.exe`).

### Files and roles

| File | Role in this WP |
|---|---|
| `Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs` | Gains `RunOrDefer(site, action)` and a reporter: inline on the main thread, parked elsewhere |
| `Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs`, `BehaviorTreeMissionLogic.cs` | Writer row 1: removal cleanup parked; off-thread report to the file log |
| `Main/Features/AdvancedCombat/{SpatialGrid,AdvancedCombatBehavior,MissionThreadGuard}.cs` | Writer row 6: grid removals parked, applied from the mission tick; report wording |
| `Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs` | Writer row 2: kill record and forget parked |
| `Main/Features/FieldCommission/FieldCommissionMeritService.cs` | Writer row 3: `RegisterKill` takes a lock |
| `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` | Writer rows 4 and 5: fallen-hero teardown and hit notice parked (hand-merge) |
| `Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs`, `Main/Features/Warg/WargMissionBehavior.cs` | Rows 7 and 8: `Interlocked` kill count; dismount reset parked |
| `Main/Features/BattleLoadDiagnostics/*` | Patch91 probes, `MissionTickStallProbe`, `MissionTickStallWatchdog`, `ThreadStackCapture` (new); settings, provider, IoC; `ExitStallSampler` shares the capture |
| `Main/Features/CoopInterop/CoopSettingsRelevance.cs` | New setting listed as instrumentation (co-op local) |
| `Main/SubModule.cs` (single-owner; this WP is its only editor while it runs) | Two blocks in the game-init batch: Patch91 plus watchdog (Part A), Patch91b plus arming (Part B) |
| `Main/Features/MissionTickGuard/*` (new, Part B) | The D17 recovery: pure state machine plus the Patch91b hooks |
| `Dependencies/Foundation/SaveShieldPolicy.cs`, `Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs` | Part B: comments only (bd1ac8292 lesson, re-cited) |
| `.claude/rules/harmony-patches.md`, `.claude/rules/csharp-architecture.md` | The rule lines that document the mechanism this WP ports (per-port procedure exception) |
| Docs: `docs/features/{battle-load-diagnostics,mount-despawn,warg-combat,field-commission,coop-interop,bannerlord-together-compat,crash-report}.md`, new `docs/features/mission-tick-guard.md`, `docs/reference/harmony-patch-registry.md`, new `docs/reviews/rca-offthread-agent-removed-2026-09-22.md`, `docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`, one entry in `docs/reviews/lessons/harmony-il.md` (D7) | Feature docs travel with their features; the registry is the crash-triage lookup |

### 1.4.x excerpts (`origin/bannerlord-1.4.5`, verified this session)

Writers (byte-identical to `f75288ebf^` unless noted): `BehaviorTreeAgentComponent.cs:36-40`
calls `Unschedule(this)` and `DisposeTree(base.Agent)` straight from `OnAgentRemoved` (row 1);
`BehaviorTreeMissionLogic.cs:50-51` builds `new DeferredCallbackQueue()` and reports through
`BTRegister.Logger?.LogMessage` (possibly an on-screen logger); `MountDespawnMissionBehavior.cs:65-66`
writes `_pending[affectedAgent.Index]` and calls `_service.OnMountKilled` (row 2, forget at `:79`);
`FieldCommissionMeritService.cs:83-84` does `TryGetValue` then `_battleKills[troopId] = current + 1`
with no lock (row 3; the file drifted elsewhere); `EnlistmentMeritMissionBehavior.cs:114` is
`_kills++;` (row 7); `SpatialGrid.cs:51-56` removes from the cell lists directly (row 6).
`DeferredCallbackQueue.cs:14-49` has `Count`, `Enqueue`, `Drain`, `Clear` and no `RunOrDefer`;
`MissionThreadGuard.cs:48-60` reports once per site (`ResetForTests` at `:62`).

`CareerPerkMissionBehavior.cs` differs from `f75288ebf^`: 1.4.x keeps the Ammo passive
(`OnAgentBuild` `:112-143`, field `_passives`, constructor parameter `ICareerPassiveService passives`
at `:43`) and lacks 1.5.x's #611 mount recompute and #613 `ResetDiagnostics`. The fallen-hero
teardown is inline in `OnAgentRemoved` (`:167-200`), the hit notice is
`_attributionReporter.ReportHit(hero.StringId, affectedAgent?.Name, damagedHp);` (`:164`), and
`OnEndMission` ends `_loggedMissionStart = false; _activeContexts.Clear();` (`:217-218`).

Game-init wiring on 1b00d1acd: `SubModule.cs:1324-1325` is the once-per-process guard
(`if (_gameInitPatchesApplied) return; _gameInitPatchesApplied = true;`); `:1672-1676` applies
Patch43 in a try/catch and `:1677` is
`IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();`. After WP104 the
try/catch reads `TryPatchCategory("Patch43_BattleLoadDiagnostics");`, and plan 009's source gate
`PatchCategoryApplierTests.MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` fails on
any direct `_harmony.PatchCategory(` call in `Main` (`998ae7e95`, test lines 163-191). TAOM's
Harmony id is `com.taom.mod` (`SubModule.cs:161`).

The QA trigger `CrashReportDevTrigger.cs:21-35` throws `TaomDevTriggerException` from
`OnMissionTick` when "Throw On Next Mission Tick" is on (and rethrows it past its own catch). The
only TAOM patch on `Mission.OnTick` is `Patch35_Mission_OnTick` (a postfix,
`Main/Features/CompanionTactics/FormationPresets/Hooks/Patch35_Mission_OnTick.cs:15-36`, applied at
`SubModule.cs:1575`).

Settings counts: `SettingsFingerprintTests.cs:210-213` pins `TaomSettings` 229/180,
`BattleLoadDiagnosticsSettings` 7/0, `BlowDiagnosticsSettings` 1/0, `CrashReportSettings` 6/0
(total 243, simulation-relevant 180). `EveryDocQuotingTheSettingsCounts_AgreesWithReflection`
(`:216-252`) requires `coop-interop.md` to contain `**<total>**`, `bannerlord-together-compat.md`
to contain ` <total> MCM settings`, and both `**180 are simulation-relevant**`.
`coop-interop.md:310-316` says `**243**`, "7 in" before the class name
`BattleLoadDiagnosticsSettings`, and "The 59 excluded" (stale: `AssertSplit` enforces excluded = total minus relevant = 63);
`bannerlord-together-compat.md:291` says "TAOM's 243 MCM settings".

Not on 1.4.x (checked): `Main/Features/SignatureStrikes` (1.5.x-only, `0700ca901`),
`Dependencies/Foundation/RethrowStackPreserver.cs` (WP107 brings it), any Patch91 or Patch91b, any
write of `tickCompleted`, and a `tickCompleted` entry in `docs/reviews/lessons/harmony-il.md`.

### What the two 1.5.x commits carry (read this session)

- **`f75288ebf`** (51 files). Thirteen code files are identical on 1b00d1acd and `f75288ebf^`
  and apply cleanly; five drifted (`SubModule.cs`, `CareerPerkMissionBehavior.cs`,
  `FieldCommissionMeritService.cs`, `BattleLoadDiagnosticsSettings.cs`,
  `CoopSettingsRelevance.cs`); four are new (Patch91, probe, watchdog, stack capture); one is
  absent here (`SignatureAgentRoster.cs`). Its SubModule hunk is the 13-line Patch91 block after
  `BattleLoadStallWatchdog().Start()`. Its `harmony-patches.md` hunk adds a Finalizer bullet that
  names `RethrowStackPreserver.PreserveForRethrow` and a lesson absent on 1.4.x, and its thread
  table mentions `SignatureAgentRoster`: both must be rewritten (Step A6). New tests: 40 methods
  (3 BT threading, 4 `DeferredCallbackQueueTests`, 3 grid removal, 1 provider, 13 watchdog, 3 stack
  capture, 3 career, 2 field commission, 6 mount despawn, 2 warg). `CareerPerkOffThreadTests`
  builds the behaviour with 1.5.x's `ICareerAgentStatService`; 1.4.x needs `ICareerPassiveService`
  in that position.
- **`bd1ac8292`** reverts co-op rethrows from `6ce20f6ed`, which never reached 1.4.x: 1.4.x
  `SaveShieldPolicy.ShouldSwallow` (`SaveShieldPolicy.cs:30-36`) and Patch69 (`:81` rethrows only
  non-NRE) already equal the reverted state. What carries over is the lesson that a throw out of
  `OnMissionTick` hangs the next frame, the matching comments, and one sentence in
  `harmony-patches.md`; its 1.5.x text cites v1.5.5 lines (`Mission.cs:3749`, `:3594-3599`) that
  must be re-cited to v1.4.8 (`:3740`, `:3585-3591`).
- Later 1.5.x commits on the same files change nothing in the threading fix: `6eb2e1b90` (profiler
  hooks in Patch91, WP117), `efe326242`, `e5f644da4`, `b8b144635`, `fe8f30c7a`, `87f9f862e`
  (perf, WP124), the rest are 1.5.x features.

### Engine facts the code binds (installed v1.4.8 via taom-src, read this session)

From `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.MountAndBlade.Mission.cs` (the v1.4.8 dump agrees):

```csharp
public sealed class Mission : DotNetObject, IMission                  // :23
private bool tickCompleted = true;                                     // :1040 (only writes: :3613, :3740)
[MBCallback(null, false)] internal void TickAgentsAndTeams(float dt, bool tickPaused)  // :1819-1823
public void TickAgentsAndTeamsAsync(float dt)                          // :1825; body MBAPI.IMBMission.TickAgentsAndTeamsAsync(Pointer, dt)
[MBCallback(null, false)] internal void OnPreTick(float dt)            // :3529-3538; calls WaitTickCompletion() first
private void WaitTickCompletion()                                      // :3585-3591; while (!tickCompleted) Thread.Sleep(1);
public void TickAgentsAndTeamsImp(float dt, bool tickPaused)           // :3601-3618; tickCompleted = true at :3613, then AfterAsyncTickTick
public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)  // :3636-3776
    // :3722-3725 return unless CurrentState == State.Continuing; :3740 tickCompleted = false;
    // :3741-3744 MissionBehaviors[i].OnMissionTick(dt) in reverse order;
    // :3768-3775 if (doAsyncAITick) TickAgentsAndTeamsAsync(dt); else TickAgentsAndTeamsImp(dt, tickPaused: false);
public State CurrentState { get; private set; }                       // :1283 (enum State, Continuing at :635)
[MBCallback(null, true)] internal void GetDefendCollisionResults(...)  // :6455-6456
// also [MBCallback(null, true)]: CanPhysicsCollideBetweenTwoEntities :2451, OnFixedTick :3519,
// MissileCalculatePassbySoundParametersCallbackMT :6026; Agent.cs: UpdateMountAgentCache :1813, GetWeaponInaccuracy :1842
```

- `TickAgentsAndTeamsAsync` has exactly one caller, `Mission.cs:3770` (dump grep).
- `MissionState` (`...MissionState.cs`): `public class MissionState : GameState` (`:11`),
  `public Mission CurrentMission { get; private set; }` (`:33`),
  `private void TickMissionAux(float dt, float realDt, bool updateCamera, bool asyncAITick)`
  (`:212-219`: `CurrentMission.Tick(dt); if (_missionTickCount > 2) CurrentMission.OnTick(...)`);
  the normal path passes `asyncAITick: true` (`:201`), fast-forward passes `false` (`:178-196`).
- `MissionBehavior`: `public virtual void OnEndMissionInternal()` calls `OnEndMission()`
  (`:121-124`); `public virtual void OnMissionTick(float dt)` (`:146`).
- `MBRandom.RandomFloat => Random.NextFloat()` over `Game.Current.RandomGenerator`
  (`TaleWorlds.Core.MBRandom.cs:22`, `:32`); `MBFastRandom` keeps four `uint` fields
  (`TaleWorlds.Core.MBFastRandom.cs:22-32`) advanced by `XorShift` (`:186`) with no lock. 1.4.x
  draws it in `TaomCombatMechanicsModel.BuildCrushThroughContext` (`TaomCombatMechanicsModel.cs:177`),
  reached from `MissionCombatMechanicsHelper.cs:311` (dump) under `GetDefendCollisionResults`.
- Dump (`C:\Decompiled_Bannerlord\_categories_v1.4.8`): `DefaultBattleMissionAgentSpawnLogic.OnMissionTick`
  (`:171`) spawns through `CheckDeployment` (`:532`, `SpawnTroops` at `:621`) and
  `CheckReinforcementSpawn` (`:707`) into `MissionBattleSideSpawnContext.SpawnTroops` (`:230`,
  `Mission.SpawnTroop` at `:373`). SandBox `TournamentBehavior.OnMissionTick` (`:149-155`) calls
  `EndCurrentMatch`, which gives the prize (`:247`), the leaderboard entry (`:248`) and
  `OnTournamentFinished` (`:256`) before raising `TournamentEnd` (`:257-260`); `TournamentVM`
  subscribes `OnTournamentEnd` to it (`_modules_build/SandBox__SandBox.ViewModelCollection.cs:4674`).

### Who swallows above the tick window on 1.4.x (V04, code re-read this session)

| Frame (inner to outer) | Catcher | Swallows | Note |
|---|---|---|---|
| `Mission.OnTick` | PatchShield, from a process's second game start (because Patch35 patches it) | `MissingMethod`/`MissingField`/`TypeLoad` only | value-returning finalizer at default priority (`PatchShield.cs:215-216, 244-258`); then strips every non-protected owner's prefixes, postfixes and transpilers, never finalizers (`:376-378`). TAOM's owner `com.taom.mod` is not protected (the list holds `TAOM`, `PatchShieldPolicy.cs:25`; `IsProtectedOwner` is a prefix match, `:84-95`) |
| `MissionState.OnTick` | PatchShield from the second start (Patch43) | missing-API only | |
| `Module.OnApplicationTick` | Patch37, `[HarmonyPriority(800)]` | everything | writes the `[CrashReport]` capture |
| `Managed_ApplicationTick` shim | Native2Managed bridge (247 shims) | everything | until WP107's allowlist |

Each swallow above the clear leaves `tickCompleted` false. The agent-tick job's own throw goes to
the `Mission_TickAgentsAndTeams` shim's bridge finalizer (swallow, same freeze) until WP107.

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types. The pure `TickHandshakeRecovery`
  has no engine type; only the Harmony entry points (`Hooks/`) touch `Mission`.
- ADR-002 thin entry points: each hooks file under 150 lines, logic delegated.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: the failing test first for all new work (Part B; Part A's wiring test).
- MCM json2 rule: rename a persisted setting, never flip its default. The new setting is new
  (default ON); no existing default changes.
- `.claude/rules/harmony-patches.md`: `MovementOrder` postfixes go through the shared deferred
  category (not touched here); finalizer and `__state` pairing as described in Part B.
- `RequireRestart = false` on every value setting (`SettingRequireRestartPostureTests`).

## Commands you will need

Start every command with `cd /c/Users/mikew/source/repos/taom-port/106 &&`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors (a BHA0001 Patch88 warning may print if WP104 did not silence it; ignore it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed:     0`; give the call a 600000 ms timeout |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | as each step states |
| Docs | `python tools/lint_docs.py --summary --dash-base $(git merge-base HEAD bannerlord-1.4.5)` | `dead_links`, `orphan_features`, `missing_features` equal Step A0.6's values; `ai_dashes: 0` |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
CopyModule on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the game. Never
`./build.ps1`. Never two builds at once in this worktree. Always build before a `--no-build` test.

## Scope

**In scope** (the only files you create, modify or delete):

- Part A: the 44 non-bookkeeping paths of `f75288ebf` listed in Step A10 (with
  `CareerPerkOffThreadTests.cs` adapted), plus the new `TAOM.Tests/Features/BattleLoadDiagnostics/Patch91WiringTests.cs`.
- Part B: create `Main/Features/MissionTickGuard/TickHandshakeRecovery.cs`,
  `Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs`,
  `TAOM.Tests/Features/MissionTickGuard/{TickHandshakeRecoveryTests,TickHandshakeRecoveryHarmonyTests,Patch91bMissionTickRecoveryBindingTests}.cs`,
  `docs/features/mission-tick-guard.md`; modify `Main/SubModule.cs`, `docs/features/crash-report.md`,
  `docs/features/battle-load-diagnostics.md`, `docs/reference/harmony-patch-registry.md`,
  `Dependencies/Foundation/SaveShieldPolicy.cs` (comment only),
  `Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs` (comment only),
  `docs/reviews/lessons/harmony-il.md` (one appended entry, D7), `.claude/rules/harmony-patches.md`.
- Single-owner: `Main/SubModule.cs` gets exactly two blocks (Steps A8 and B5). `Main/IoC.cs` is
  NOT touched (the watchdog registers in `BattleLoadDiagnosticsIoC.cs`; the guard needs no service).

**Out of scope** (do not touch):

- `CHANGELOG.md` (D1), `CLAUDE.md`, `AGENTS.md`, `docs/reference/feature-map.md`, `docs/INDEX.md`,
  `tools/README.md`, `docs/reviews/LESSONS-LEARNED.md`, every other lessons file and every other
  `harmony-il.md` entry (WPs 142 to 144 sync these), `docs/adrs/**`, `.claude/settings*.json`,
  `Directory.Build.props`, `plans/README.md`.
- Anything else under `Dependencies/` (WP107 owns it: PatchShield, PatchShieldPolicy, the
  `ExcludedTargetMethods` entries, `RethrowStackPreserver`, the protected-owner list).
- `Main/Features/SignatureStrikes` (must stay absent), co-op code from `bd1ac8292` (D7),
  `TaomCombatMechanicsModel` (D16: no RNG lock), any patch on `Mission.TickAgentsAndTeamsImp` or
  `WaitTickCompletion` beyond Patch91's own, a try/catch sweep of `OnMissionTick` overrides.
- Csproj files (SDK globbing picks up new files; `InternalsVisibleTo` for the tests exists).

## Git workflow

- Create the worktree in Step A0.1. Work only in `C:\Users\mikew\source\repos\taom-port\106`.
  Commit on `port148/106-offthread-and-tick-safety` only. Never push, open a PR, tag, or merge
  into `bannerlord-1.4.5`. Run git only through the Bash tool (D19), never through PowerShell.
- Per-port procedure (binding): `git cherry-pick -n <sha>` only; never let `cherry-pick`,
  `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit (`GIT_EDITOR=true` on this laptop
  would commit the 1.5.x message unedited). Drop bookkeeping hunks: conflicted ones with
  `git checkout --ours <path>` then `git add <path>`, cleanly applied ones with
  `git checkout HEAD <path>`. Never write `git checkout ... -- <path>` or `git restore`: the
  dangerous-git hook asks for confirmation on those forms.
- Stage with explicit `git add <paths>` (never `-A`, `-u` or `.`), then run `git commit -F <file>`
  in the NEXT sequential Bash call: never the same command, never a parallel sibling call. Write
  each message with the Write tool to the file named in its step and pass it as `C:/...`.
- Never `--no-verify`, `--amend`, `-C` or `-c`. A hook that denies a commit: STOP, report verbatim.
- Message template: subject `<type>(<scope>): v2.0.28 - <description>`, at most 72 characters; a
  body for 1.4.x readers wrapped at 72 with no long dashes; trailers `Research:`, `Not-tested:`,
  `Save-compat:` (and `Constraint:`, `Rejected:` when relevant), then one `Ported-from: <40-hex>`
  per included 1.5.x commit. No `Co-Authored-By`, no "Generated with" line. Exact messages below.

## Part A: port `f75288ebf` (park the off-thread writes, Patch91 and the freeze watchdog)

### Step A0: Worktree, prerequisites, baseline

**A0.1**

```bash
cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/106 -b port148/106-offthread-and-tick-safety bannerlord-1.4.5
```

**Verify**: `Preparing worktree (new branch 'port148/106-offthread-and-tick-safety')`. If the
branch or folder already exists: STOP.

**A0.2** Run the drift check in the header. **Verify**: changes only in `Main/SubModule.cs` and in
docs WP104 touched; nothing under `Main/Features/MissionTickGuard`, no
`Main/Features/BattleLoadDiagnostics/MissionTickStall*.cs`. Anything else: read it; A0.3 decides.

**A0.3** Prerequisites and anchors.

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && test -f Main/PatchCategoryApplier.cs && echo applier-ok; \
grep -cF 'private bool TryPatchCategory(string category)' Main/SubModule.cs; \
grep -cF 'public static string ReadSource(string relativePath, bool stripComments = false)' TAOM.Tests/Infrastructure/RepoPaths.cs; \
grep -cF 'MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper' TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs; \
test -f Main/Composition/FeatureModules.cs && echo composition-ok; \
grep -c hookSpecificOutput .claude/hooks/check-commit-subject-version.sh; \
ls .claude/hooks/check-changelog-changed.sh .claude/hooks/check-changelog-updated.sh 2>&1 | grep -c 'No such file'; \
grep -cF 'TryPatchCategory("Patch43_BattleLoadDiagnostics");' Main/SubModule.cs; \
grep -cF 'IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();' Main/SubModule.cs; \
ls -d Main/Features/SignatureStrikes Main/Features/MissionTickGuard Main/Features/BattleLoadDiagnostics/MissionTickStallWatchdog.cs 2>&1 | grep -c 'No such file'
```

**Verify**, in order: `applier-ok`; `1`; `1`; `1`; `composition-ok`; a number of at least `1`;
`2`; `1`; `1`; `3`. Anything else: STOP (a prerequisite WP is not merged, or this WP already ran).

**A0.4** Baseline: Restore, Build, Test. **Verify**: build exit 0; `Failed: 0`. Record `Total`,
`Passed`, `Skipped` as **T0** (WP101's baseline was 9154/9151/3; later WPs added tests, so T0
should equal the latest count the integrator recorded in `plans/README.md`). Any failure: STOP.

**A0.5** Engine spot check (the plan's line numbers):

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && M=$(pwsh -NoProfile -File tools/taom-src.ps1 path TaleWorlds.MountAndBlade.Mission | tail -1) && grep -n "tickCompleted\|public void TickAgentsAndTeamsAsync(float dt)\|public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)" "$M"
```

**Verify**: exactly lines `1040`, `1825`, `3587`, `3613`, `3636`, `3740` (four `tickCompleted`
lines, the two signatures). Different lines or members: STOP (engine changed under the plan).

**A0.6** Docs baseline: run the Docs command; record `dead_links`, `orphan_features`,
`missing_features`.

### Step A1: Re-run the cherry-pick simulation on the real base

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git merge-tree --write-tree --name-only --no-messages --merge-base=f75288ebf^ HEAD f75288ebf; echo "exit=$?"
```

**Verify**: `exit=1`. After the tree id, the conflicted paths. On 1b00d1acd they were exactly:
`CHANGELOG.md`, `CLAUDE.md`, `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs`,
`Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs`,
`TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`,
`docs/features/bannerlord-together-compat.md`, `docs/features/coop-interop.md`,
`docs/features/field-commission.md`, `docs/reference/harmony-patch-registry.md`,
`docs/reviews/LESSONS-LEARNED.md`, `docs/reviews/lessons/harmony-il.md`. On this base
`Main/SubModule.cs` or `docs/features/battle-load-diagnostics.md` may also appear (WP104 edits);
Steps A8 and A5 cover them. Any other code or test path: STOP and
report the list.

### Step A2: Apply and clear the bookkeeping

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git cherry-pick -n f75288ebf; echo "exit=$?"; git diff --name-only --diff-filter=U
```

**Verify**: `exit=1` with CONFLICT lines; the unmerged list matches Step A1's. Then:

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && U=$(git diff --name-only --diff-filter=U); \
for p in CHANGELOG.md CLAUDE.md docs/reviews/LESSONS-LEARNED.md docs/reviews/lessons/harmony-il.md docs/reference/feature-map.md docs/reviews/lessons/adapters-taleworlds-api.md; do \
  if printf '%s\n' "$U" | grep -qxF "$p"; then git checkout --ours "$p" && git add "$p"; else git checkout HEAD "$p"; fi; done; \
git rm -f Main/Features/SignatureStrikes/Hooks/SignatureAgentRoster.cs; ls -d Main/Features/SignatureStrikes 2>&1; \
git diff HEAD --name-only -- CHANGELOG.md CLAUDE.md docs/reviews/LESSONS-LEARNED.md docs/reviews/lessons docs/reference/feature-map.md; \
echo ---; git diff --name-only --diff-filter=U
```

**Verify**: `No such file or directory` for `Main/Features/SignatureStrikes`; nothing printed
before `---` (every bookkeeping path equals HEAD); after `---` the unmerged list holds only
`CareerPerkMissionBehavior.cs`, `SettingsFingerprintTests.cs`, the three feature docs and the
registry (plus `SubModule.cs` or `battle-load-diagnostics.md` if A1 listed them).

### Step A3: Resolve `CareerPerkMissionBehavior.cs` and adapt its test

Take the 1.4.x side, then make exactly these edits (the `f75288ebf` queue hunks without #611/#613):

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git checkout --ours Main/Features/CareerSystem/CareerPerkMissionBehavior.cs
```

1. Add `using BehaviorTreeWrapper;` after `using System.Collections.Generic;`, and
   `using TAOM.Features.AdvancedCombat;` after `using TAOM.Core.Logging;`.
2. After `private bool _buildFailureLogged;` add:
   ```csharp
       // The player's death and score hits are native's to place, and a v1.4.8 player log caught
       // OnAgentRemoved off the main thread (#634): their work is replayed from OnMissionTick.
       private readonly DeferredCallbackQueue _deferred;
   ```
3. Constructor: after `_logger = logger;` add
   `_deferred = new DeferredCallbackQueue(message => _logger?.LogWarning(message));`.
4. `OnMissionTick`: make the first two statements `MissionThreadGuard.MarkMainThread();` and
   `_deferred.Drain();` (before `if (Campaign.Current == null) return;`).
5. `OnScoreHit`: replace `_attributionReporter.ReportHit(hero.StringId, affectedAgent?.Name, damagedHp);` with
   ```csharp
           var heroId = hero.StringId;
           var targetName = affectedAgent?.Name;
           _deferred.RunOrDefer("CareerPerkMissionBehavior.OnScoreHit",
               () => _attributionReporter.ReportHit(heroId, targetName, damagedHp));
   ```
6. `OnAgentRemoved`: replace the whole `if (affectedAgent == mainAgent) { ... }` block with
   `if (affectedAgent == mainAgent) EndAbilitiesForFallenHero(hero.StringId);` and add, right
   after `OnAgentRemoved`:
   ```csharp
       // Empties the list OnMissionTick walks and recomputes other agents' stats, so off the main thread
       // it waits for the next mission tick.
       internal void EndAbilitiesForFallenHero(string heroId) =>
           _deferred.RunOrDefer("CareerPerkMissionBehavior.OnAgentRemoved", () => EndAbilitiesNow(heroId));

       private void EndAbilitiesNow(string heroId)
       {
           // (move the old block's body here unchanged: the "Deep-review 2026-08-05" comment,
           // GetBuffedAllyIndices, ClearBuff(heroId), ClearAllAllyBuffs, _activeContexts.Clear(),
           // and the ally loop calling ally.UpdateAgentProperties() only; hero.StringId becomes heroId)
       }
   ```
   Do NOT add `ally.MountAgent?.UpdateAgentProperties();` (#611, WP129).
7. `OnEndMission`: add `_deferred.Clear();` immediately before the final `_activeContexts.Clear();`.
   Do NOT add the #613 `ResetDiagnostics` block. Keep `OnAgentBuild` and `_passives` untouched.

Then in `TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs` replace
`Substitute.For<ICareerAgentStatService>(),` with `Substitute.For<ICareerPassiveService>(),`
(1.4.x's constructor takes `ICareerPassiveService` in that position; it is in
`TAOM.Features.CareerSystem`, already imported). `git add` both files.

**Verify**: `grep -c 'MountAgent?.UpdateAgentProperties\|ResetDiagnostics\|_agentStats' Main/Features/CareerSystem/CareerPerkMissionBehavior.cs`
prints `0`; `grep -c 'RunOrDefer\|_deferred.Drain\|_deferred.Clear\|MarkMainThread' Main/Features/CareerSystem/CareerPerkMissionBehavior.cs`
prints `5`; `grep -c 'public override void OnAgentBuild' Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` prints `1`.

### Step A4: Resolve the settings counts

1. `git checkout --ours TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, then change
   `AssertSplit(typeof(BattleLoadDiagnosticsSettings), reflected: 7, covered: 0);` to `reflected: 8`.
   If this base shows a number other than 7, add one to it. No other line changes.
2. `git checkout --ours docs/features/coop-interop.md`, then in the "No MCM settings parity"
   bullet make three replacements (left: today's text; right: the new text; excluded = total
   minus relevant, which `AssertSplit` enforces) and keep `**180 are simulation-relevant**`:
   ```text
   **243**                               ->  **244**
   7 in `BattleLoadDiagnosticsSettings`  ->  8 in `BattleLoadDiagnosticsSettings`
   The 59 excluded are                   ->  The 64 excluded (total minus relevant) are
   ```
3. `git checkout --ours docs/features/bannerlord-together-compat.md`, then `TAOM's 243 MCM settings`
   becomes `TAOM's 244 MCM settings`. Keep both `180`s.
4. `CoopSettingsRelevance.cs` and `BattleLoadDiagnosticsSettings.cs` merge cleanly: confirm
   `grep -c 'EnableMissionTickStallSampler' Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs`
   prints `1` for each.

If a different total than 243 is on this base (an earlier WP added a setting), use that total
plus one and recompute the excluded count. `git add` the three files.

### Step A5: Resolve the remaining docs

1. `git checkout --ours docs/features/field-commission.md`, then insert the two `f75288ebf` lines
   verbatim (`git show --format= f75288ebf -- docs/features/field-commission.md` shows them): the
   `FieldCommissionMeritServiceTests.RegisterKill_FromManyThreadsAtOnce_CountsEveryKill` bullet
   right after the `FieldCommissionMeritServiceTests.cs` bullet under "## Tests", and the
   `2026-09-22: RegisterKill takes a lock (#634)` entry after the `2026-09-04` entry under "## Changelog".
2. `git checkout --ours docs/reference/harmony-patch-registry.md`, then insert the
   `## Patch91_MissionTickStall` section from `f75288ebf` (shown by
   `git show --format= f75288ebf -- docs/reference/harmony-patch-registry.md`) after the last
   `## PatchNN_` section (Patch88 on 1b00d1acd, or Patch90 if WP108 already merged) and before
   `## Patch_MissionTime_SetMovementOrder`. In its last paragraph replace:
   ```text
   inside the once-per-process guard, own try/catch.  ->  inside the once-per-process guard, through `TryPatchCategory`.
   **ACTIVE** (2026-09-22).                           ->  **ACTIVE** (ported YYYY-MM-DD).
   ```
   Here and in Step B6, `YYYY-MM-DD` is the date you make the edit.
3. `docs/features/battle-load-diagnostics.md` merges cleanly; in its new wiring sentence replace:
   ```text
   (its own try/catch, inside the once-per-process patch guard)  ->  (through `TryPatchCategory`, inside the once-per-process patch guard)
   ```
   If A1 listed this file, keep HEAD's side of every conflict block and re-add only
   `f75288ebf`'s lines.

`git add` every file touched here. **Verify**: `git diff --name-only --diff-filter=U` prints nothing.

### Step A6: Rewrite the two rule sentences, add the RCA note

1. `.claude/rules/harmony-patches.md` (applied cleanly). Replace the whole new `- **Finalizer**:`
   bullet with (no `RethrowStackPreserver`: it reaches 1.4.x with WP107):
   ```text
   - **Finalizer**: runs after the original on every call, with a null `__exception` when nothing threw. `return null` swallows. Returning the exception makes Harmony `throw` it (whenever any finalizer on the method returns a value), which erases the throw site. An observe-only finalizer should be `void`, which keeps Harmony's `rethrow` and the trace (Patch91's probes are the shape).
   ```
   In the thread section the sentence wraps across two lines; delete only this clause, keeping the
   closing parenthesis after `kill ties`:
   ```text
   ; `SignatureAgentRoster` is a `ConcurrentDictionary`
   ```
2. `.claude/rules/csharp-architecture.md` applied cleanly; leave it as `f75288ebf` wrote it.
3. In `docs/reviews/rca-offthread-agent-removed-2026-09-22.md` (new), insert after the `**Date:**` line:
   ```text

   > Ported to the 1.4.x line by plan 106 (`plans/106-offthread-and-tick-safety.md`). The
   > `SignatureAgentRoster` row and the SignatureStrikes mentions describe a 1.5.x-only feature that
   > 1.4.x never had. The four lessons cited under Prevention reach `docs/reviews/lessons/harmony-il.md`
   > with the programme's lessons sync (plan 142).
   ```

`git add` the two files. **Verify**: `grep -c 'RethrowStackPreserver\|SignatureAgentRoster' .claude/rules/harmony-patches.md`
prints `0`; `grep -c 'Patch91' .claude/rules/harmony-patches.md` prints at least `2`.

### Step A7 (RED): Pin Patch91's wiring

Create `TAOM.Tests/Features/BattleLoadDiagnostics/Patch91WiringTests.cs` (new work; model the
source read on `Patch82MapEventObserverInvariantBindingTests.PatchClass_IsRegisteredInAllThreePlaces`
as WP104 left it, which uses `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)`):

```csharp
using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

/// <summary>
/// Patch91 (#634) is dead code unless both classes carry the category and SubModule applies it
/// through plan 009's guard in the game-init batch, after Patch43, with the watchdog started.
/// </summary>
[TestClass]
public class Patch91WiringTests
{
    [TestMethod]
    public void Patch91_IsAppliedThroughTheGuard_AndTheWatchdogStarts()
    {
        foreach (var patch in new[] { typeof(Mission_TickAgentsAndTeamsImp_StallProbe_Patch), typeof(MissionState_TickMissionAux_StallProbe_Patch) })
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length, $"{patch.Name} lost its target");
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.Contains(categories, "Patch91_MissionTickStall", $"{patch.Name} lost its category");
        }

        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        const string apply = "TryPatchCategory(\"Patch91_MissionTickStall\");";
        const string start = "IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();";
        int patch43 = source.IndexOf("TryPatchCategory(\"Patch43_BattleLoadDiagnostics\");", StringComparison.Ordinal);
        int at = source.IndexOf(apply, StringComparison.Ordinal);
        Assert.IsTrue(patch43 >= 0 && at > patch43, "Patch91 must be applied through TryPatchCategory after Patch43");
        Assert.AreEqual(at, source.LastIndexOf(apply, StringComparison.Ordinal), "Patch91 applied twice");
        Assert.IsTrue(source.IndexOf(start, StringComparison.Ordinal) > at, "the watchdog must start after Patch91 applies");
    }
}
```

If `RepoPaths` lives in another namespace on this base, use the namespace the Patch82 test imports.
If `Main/SubModule.cs` is still unmerged, take HEAD's side now (do not `git add` it yet):
`cd /c/Users/mikew/source/repos/taom-port/106 && git checkout --ours Main/SubModule.cs`.

**Verify**: Build exit 0, then Filtered `Patch91WiringTests`: it FAILS on "Patch91 must be applied
through TryPatchCategory after Patch43" (HEAD's side has no Patch91 block; a clean merge has the
try/catch form). A compile error instead means an earlier resolution step is unfinished: finish it,
rebuild, and confirm this failure before Step A8.

### Step A8 (GREEN for A7): Resolve `Main/SubModule.cs`

The target, directly after the line `IoC.Resolve<Features.BattleLoadDiagnostics.BattleLoadStallWatchdog>().Start();`
and its blank line (in the `OnGameInitializationFinished` batch, after `_gameInitPatchesApplied`):

```csharp
        // Patch91 battle-freeze probes (#634): a player's battle froze with the heap flat and no
        // exception, the shape of an agent tick that never finishes while the next frame waits in
        // Mission.WaitTickCompletion. The probes bracket the agent tick and the main thread's whole
        // mission frame; the watchdog photographs whichever is stuck past 10s. Applied through
        // TryPatchCategory like Patch43: a diagnostic must never take startup down. Inside the
        // once-per-process patch guard; Start() is idempotent regardless.
        TryPatchCategory("Patch91_MissionTickStall");
        IoC.Resolve<Features.BattleLoadDiagnostics.MissionTickStallWatchdog>().Start();
```

If Step A7 took HEAD's side, insert the target block at that anchor. If the file merged cleanly
with `try { _harmony.PatchCategory("Patch91_MissionTickStall"); } catch ...`, replace that try/catch
and its comment with the target block. Then `git add Main/SubModule.cs`.

**Verify**: `grep -cF 'TryPatchCategory("Patch91_MissionTickStall");' Main/SubModule.cs` prints `1`;
`grep -c '_harmony.PatchCategory("Patch91' Main/SubModule.cs` prints `0`;
`git diff --cached HEAD -- Main/SubModule.cs | grep -c '^[-+] '` prints `8` (only the added lines).
Build; Filtered `"FullyQualifiedName~Patch91WiringTests|FullyQualifiedName~PatchCategoryApplierTests"`:
`Failed: 0` (GREEN).

### Step A9: Build and test

Build. **Verify**: exit 0, 0 errors (CS0618 is suppressed inside `ThreadStackCapture`; no new
warning text matters). Then Filtered with
`"FullyQualifiedName~MissionTickStallWatchdogTests|FullyQualifiedName~ThreadStackCaptureTests|FullyQualifiedName~BattleLoadDiagnosticsSettingsProviderTests|FullyQualifiedName~Patch91WiringTests|FullyQualifiedName~DeferredCallbackQueueTests|FullyQualifiedName~BehaviorTreeAgentComponentThreadingTests|FullyQualifiedName~SpatialGridRemovalTests|FullyQualifiedName~CareerPerkOffThreadTests|FullyQualifiedName~FieldCommissionMeritServiceTests|FullyQualifiedName~MountDespawnOffThreadTests|FullyQualifiedName~WargOffThreadTests|FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~PatchCategoryApplierTests|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~MissionThreadGuardTests|FullyQualifiedName~SettingRequireRestartPostureTests"`.
**Verify**: `Failed: 0`. Then the full Test. **Verify**: `Failed: 0`, `Total` = T0 + 41.

### Step A10: Commit A

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git add .claude/rules/csharp-architecture.md .claude/rules/harmony-patches.md Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs Main/Features/AdvancedCombat/MissionThreadGuard.cs Main/Features/AdvancedCombat/SpatialGrid.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/ExitStallSampler.cs Main/Features/BattleLoadDiagnostics/Hooks/Patch91_MissionTickStallProbes.cs Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs Main/Features/BattleLoadDiagnostics/MissionTickStallProbe.cs Main/Features/BattleLoadDiagnostics/MissionTickStallWatchdog.cs Main/Features/BattleLoadDiagnostics/ThreadStackCapture.cs Main/Features/CareerSystem/CareerPerkMissionBehavior.cs Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/Features/Enlistment/Hooks/EnlistmentMeritMissionBehavior.cs Main/Features/FieldCommission/FieldCommissionMeritService.cs Main/Features/MountDespawn/Hooks/MountDespawnMissionBehavior.cs Main/Features/Warg/WargMissionBehavior.cs Main/SubModule.cs TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeAgentComponentThreadingTests.cs TAOM.Tests/BehaviorTreeWrapper/DeferredCallbackQueueTests.cs TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/MissionTickStallWatchdogTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/Patch91WiringTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/ThreadStackCaptureTests.cs TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/FieldCommission/FieldCommissionMeritServiceTests.cs TAOM.Tests/Features/MountDespawn/MountDespawnOffThreadTests.cs TAOM.Tests/Features/Warg/WargOffThreadTests.cs docs/features/bannerlord-together-compat.md docs/features/battle-load-diagnostics.md docs/features/coop-interop.md docs/features/field-commission.md docs/features/mount-despawn.md docs/features/warg-combat.md docs/reference/harmony-patch-registry.md docs/reviews/rca-offthread-agent-removed-2026-09-22.md docs/reviews/rca-warg-clip-on-horse-2026-09-13.md && git status --porcelain
```

**Verify**: exactly 45 lines, all staged (`M `, `A `), none with a second-column mark, no
`CHANGELOG.md`, no `CLAUDE.md`, nothing under `Main/Features/SignatureStrikes`. Write
`C:\Users\mikew\source\repos\taom-port\106-msg-A.txt` with exactly:

```text
fix(mission): v2.0.28 - park off-thread callback writes (#634)

Bannerlord raises OnAgentRemoved, OnAgentDeleted, OnAgentDismount and
other agent callbacks on the thread it chooses, and 1.4.8 player logs
caught them off the main mission thread. TAOM handlers wrote
main-thread collections from them with no lock, which can corrupt a
Dictionary and stall the agent tick that the next frame waits for.

Those writes now run inline on the main thread and are parked for the
owner's next mission tick anywhere else: the behaviour-tree
component's cleanup, mount despawn's kill record and forget, the
career teardown when the player falls and its hit notice, SpatialGrid
removals and the warg dismount reset. Field commission kills take a
lock (tied kills keep first-kill order) and the enlistment kill count
is Interlocked. Off-thread reports go to the file log at WARNING
instead of an on-screen message.

The freeze cause is not proven, so Patch91 brackets the agent tick and
the main mission frame, and a watchdog writes each stuck thread's
stack to the TAOM log as [MissionStall] at 10, 20 and 40 seconds. New
MCM toggle Enable Battle Freeze Sampler, default on.

Research: taom-src v1.4.8 Mission.cs 1040, 3585-3591, 3601-3618,
3636-3776; MissionState.cs 212-219; MissionBehavior.cs 121-124
Not-tested: Thread.Suspend capture on a live thread; the off-thread
races themselves (a live Agent cannot be built in tests)
Save-compat: no saved state touched
Ported-from: f75288ebf5c6866b01e5c578aa232c123c9aa27b
```

Then, in the NEXT Bash call:

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git commit -F C:/Users/mikew/source/repos/taom-port/106-msg-A.txt && git log -1 --format='%h %s' && git status --porcelain
```

**Verify**: one new commit with that subject; `git status --porcelain` prints nothing.

### Part A done criteria

- [ ] Build exits 0; full Test `Failed: 0` with `Total` = T0 + 41.
- [ ] `grep -cF 'TryPatchCategory("Patch91_MissionTickStall");' Main/SubModule.cs` prints `1`.
- [ ] `ls Main/Features/SignatureStrikes 2>&1 | grep -c 'No such file'` prints `1`.
- [ ] `git diff --name-only HEAD~1 HEAD | wc -l` prints `45`; `git diff --name-only HEAD~1 HEAD | grep -cE '^(CHANGELOG.md|CLAUDE.md|docs/reference/feature-map.md|docs/reviews/lessons/)'` prints `0`.
- [ ] Docs command: `dead_links`, `orphan_features`, `missing_features` equal A0.6; `ai_dashes: 0`.
- [ ] `git log -1 --format=%s | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'` prints nothing.

## Part B: the D17 tick-handshake recovery (Patch91b) and the bd1ac8292 lesson

Design (D17, V04 section 3), so no step needs a judgment call:

- **Prefix** on `Mission.OnTick`, `Priority.First`: `__state = armed && tickCompleted` (the flag
  read at entry); the state machine clears its launch mark. A false flag at entry means a previous
  agent tick may still run, so that call is never recovered.
- **Launch mark**: a void **finalizer** (not a prefix) on `Mission.TickAgentsAndTeamsAsync`,
  `Priority.First`. PatchShield strips prefixes, postfixes and transpilers but never finalizers
  (`PatchShield.cs:376-378`), and a finalizer runs whether or not the launch call threw.
- **Patch order (load-bearing)**: the launch class must be patched BEFORE the `Mission.OnTick`
  class. `TickAgentsAndTeamsAsync` is 18 bytes of IL, small enough to inline. Measured in plan
  review (scratch net472 x64 project, Lib.Harmony 2.4.2, a stand-in with the same IL shape, 5000
  calls): target patched first or `OnTick` rebuilt after it, 5000 marks; `OnTick` last rebuilt
  before the target was patched, 0 marks. A missing mark would let a throw from a postfix after
  the launch restore the flag over a running job, the case D17 forbids. `PatchCategoryIndex.Apply`
  patches classes in assembly order, which is source order inside one file, so the launch class
  stays first in `Patch91b_MissionTickRecovery.cs`; binding test 6 pins it.
- **Finalizer** on `Mission.OnTick`, void, `Priority.First` (it must see the exception before
  PatchShield's default-priority finalizer swallows a missing-API one): restore `tickCompleted =
  true` only when the call entered armed, threw, launched no agent tick and left the flag false.
  Void keeps Harmony's `rethrow`, so Patch37 still reports the exception with its stack. Never
  restore after a launch (D17): then the flag is false because the job still runs, and a restore
  would let the next frame's behaviours overlap it.
- **Arming**: `MissionTickRecoveryWiring.VerifyAndArm` runs right after the category applies and
  arms only when both patches appear in `Harmony.GetPatchInfo` and the private field resolves.
  It never throws (a throw there would skip later categories of the game-init batch).
- No MCM toggle: the guard acts only in a state that otherwise never ends. Category
  `Patch91b_MissionTickRecovery` (a sibling of Patch91; a letter suffix cannot collide with a
  future 1.5.x number), feature folder `Main/Features/MissionTickGuard`, hand-wired (D2: no 1.5.x
  module exists).

### Step B1 (RED): State-machine tests

Create `TAOM.Tests/Features/MissionTickGuard/TickHandshakeRecoveryTests.cs`, namespace
`TAOM.Tests.Features.MissionTickGuard`, `[TestClass] public class TickHandshakeRecoveryTests`,
with a helper `Armed()` returning `new TickHandshakeRecovery()` after asserting
`Arm(fieldResolved: true, launchMarkInstalled: true, recoveryInstalled: true)` is true, and these
11 `[TestMethod]`s (no `DataRow`; every row except the first two starts from `Armed()`):

| Test | Arrange and act | Assert |
|---|---|---|
| `Exit_WhenNeverArmed_NeverRestores` | new instance, `s = Enter(true)` | `s` false; `Exit(s, threw: true, tickCompletedNow: false)` is `None` |
| `Arm_WithAnyPieceMissing_StaysOff` | `Arm(false,true,true)`, `Arm(true,false,true)`, `Arm(true,true,false)` | each returns false; `Armed` false |
| `Exit_ThrowAfterTheClearWithNoLaunch_RestoresAndLogsTheFirst` | `Armed()`, `s = Enter(true)` | `Exit(s, true, false)` is `RestoreAndLog`; `RestoreCount` 1 |
| `Exit_ThrowAfterTheLaunch_NeverRestores` | `s = Enter(true)`, `MarkLaunch()` | `Exit(s, true, false)` is `None`; `RestoreCount` 0 |
| `Enter_WithTheFlagAlreadyFalse_DisarmsTheCall` | `s = Enter(false)` | `s` false; `Exit(s, true, false)` is `None` |
| `Exit_WithoutAnException_DoesNothing` | `s = Enter(true)` | `Exit(s, false, false)` is `None` |
| `Exit_WithTheFlagAlreadyTrue_DoesNothing` | `s = Enter(true)` | `Exit(s, true, true)` is `None` |
| `TheLaunchMark_LastsOneCall` | call 1: `Enter(true)`, `MarkLaunch()`, `Exit(s1, false, false)`; call 2: `s2 = Enter(true)` | `Exit(s2, true, false)` is `RestoreAndLog` |
| `Exit_ClearsTheLaunchMark_EvenWhenTheCallWasNotArmed` | `MarkLaunch()`, `Exit(false, true, false)` | that is `None`; then `Exit(true, true, false)` is `RestoreAndLog` |
| `ShouldLog_FirstSecondAndPowersOfTen` | `TickHandshakeRecovery.ShouldLog(n)` | true for 1, 2, 10, 100, 1000; false for 0, 3, 9, 11, 20, 99, 101 |
| `Exit_CountsEveryRestore_AndLogsOnlyTheThrottledOnes` | ten armed throwing calls without launch | decisions 1 and 2 `RestoreAndLog`, 3 to 9 `Restore`, 10 `RestoreAndLog`; `RestoreCount` 10 |

**Verify**: Build fails with CS0246 for `TickHandshakeRecovery` (RED; CS0234 for its namespace may
print beside it).

### Step B2 (GREEN): The state machine

Create `Main/Features/MissionTickGuard/TickHandshakeRecovery.cs`:

```csharp
using System.Threading;

namespace TAOM.Features.MissionTickGuard;

/// <summary>What the Mission.OnTick finalizer must do after one call (Patch91b).</summary>
public enum RecoveryDecision { None, Restore, RestoreAndLog }

/// <summary>
/// Patch91b's state machine (port decision D17). Pure: no engine types. v1.4.8 Mission.OnTick clears
/// tickCompleted (Mission.cs:3740), runs every OnMissionTick, then starts the agent tick (:3768-3775);
/// only the agent tick sets the flag again (:3613) and the next frame spins until it does
/// (WaitTickCompletion, :3585-3591). Restoring is safe only when the call began with the flag set,
/// cleared it and launched no agent tick: then no job is in flight. Main thread only.
/// </summary>
public sealed class TickHandshakeRecovery
{
    public static TickHandshakeRecovery Instance { get; } = new TickHandshakeRecovery();

    private bool _launchedThisCall;
    private long _restores;

    internal TickHandshakeRecovery() { }

    public bool Armed { get; private set; }

    public long RestoreCount => Interlocked.Read(ref _restores);

    /// <summary>Arms only when the private flag resolved and both patches are verified in place.</summary>
    public bool Arm(bool fieldResolved, bool launchMarkInstalled, bool recoveryInstalled)
    {
        Armed = fieldResolved && launchMarkInstalled && recoveryInstalled;
        return Armed;
    }

    /// <summary>Mission.OnTick prefix. Returns Harmony's __state: true when this call may be recovered.</summary>
    public bool Enter(bool tickCompletedAtEntry)
    {
        _launchedThisCall = false;
        return Armed && tickCompletedAtEntry;
    }

    /// <summary>TickAgentsAndTeamsAsync finalizer: this OnTick call reached the agent-tick launch.</summary>
    public void MarkLaunch() => _launchedThisCall = true;

    /// <summary>Mission.OnTick finalizer, on every call. Always clears the launch mark.</summary>
    public RecoveryDecision Exit(bool enteredArmed, bool threw, bool tickCompletedNow)
    {
        bool launched = _launchedThisCall;
        _launchedThisCall = false;
        if (!enteredArmed || !threw || launched || tickCompletedNow) return RecoveryDecision.None;

        long count = Interlocked.Increment(ref _restores);
        return ShouldLog(count) ? RecoveryDecision.RestoreAndLog : RecoveryDecision.Restore;
    }

    /// <summary>Occurrences 1 and 2, then every power of ten (10, 100, 1000...).</summary>
    internal static bool ShouldLog(long occurrence)
    {
        if (occurrence <= 2) return occurrence >= 1;
        while (occurrence % 10 == 0) occurrence /= 10;
        return occurrence == 1;
    }
}
```

**Verify**: Build exit 0; Filtered `TickHandshakeRecoveryTests`: 11 passed, `Failed: 0`.

### Step B3: Pin the Harmony 2.4.2 behaviour the design relies on

Create `TAOM.Tests/Features/MissionTickGuard/TickHandshakeRecoveryHarmonyTests.cs` (precedent for
real Harmony in tests: `TAOM.Tests/Features/CrashReport/HarmonyCorrelationCollectorTests.cs`). A
stand-in for `Mission` and test-local mirrors of the production patch bodies drive the production
state machine:

```csharp
using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionTickGuard;

namespace TAOM.Tests.Features.MissionTickGuard;

/// <summary>What Patch91b assumes about Harmony 2.4.2, on a stand-in shaped like v1.4.8 Mission.OnTick.</summary>
[TestClass]
public class TickHandshakeRecoveryHarmonyTests
{
    private const string Owner = "taom.tests.tick-recovery";
    private static TickHandshakeRecovery _recovery = null!;
    private Harmony _harmony = null!;

    public sealed class FakeMission
    {
        public bool tickCompleted = true;
        public bool ThrowBeforeLaunch;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void OnTick(bool doAsyncAITick)
        {
            tickCompleted = false;
            if (ThrowBeforeLaunch) throw new InvalidOperationException("an OnMissionTick threw");
            if (doAsyncAITick) TickAgentsAndTeamsAsync();   // the job runs on; the flag stays false
            else tickCompleted = true;                      // an inline agent tick reached :3613
        }

        // No NoInlining here, on purpose: the real method is small enough to inline, and
        // PatchRecovery's order (this target first) is what keeps the mark firing.
        public void TickAgentsAndTeamsAsync() { }
    }

    public static class Patches
    {
        public static void Prefix(FakeMission __instance, out bool __state) => __state = _recovery.Enter(__instance.tickCompleted);

        public static void Finalizer(FakeMission __instance, Exception? __exception, bool __state)
        {
            if (_recovery.Exit(__state, __exception != null, __instance.tickCompleted) != RecoveryDecision.None)
                __instance.tickCompleted = true;
        }

        public static void LaunchMark() => _recovery.MarkLaunch();
        public static void ThrowingPostfix() => throw new InvalidOperationException("a postfix threw after the launch");
        public static Exception? SwallowingFinalizer(Exception? __exception) => null;   // PatchShield's shape
    }

    [TestInitialize]
    public void Setup()
    {
        _recovery = new TickHandshakeRecovery();
        _recovery.Arm(true, true, true);
        _harmony = new Harmony(Owner);
    }

    [TestCleanup]
    public void Cleanup() => _harmony.UnpatchAll(Owner);

    private static HarmonyMethod At(string name, int priority) =>
        new HarmonyMethod(AccessTools.Method(typeof(Patches), name)) { priority = priority };

    private void PatchRecovery(bool withPrefix)
    {
        _harmony.Patch(AccessTools.Method(typeof(FakeMission), nameof(FakeMission.TickAgentsAndTeamsAsync)),
            finalizer: At(nameof(Patches.LaunchMark), Priority.First));
        _harmony.Patch(AccessTools.Method(typeof(FakeMission), nameof(FakeMission.OnTick)),
            prefix: withPrefix ? At(nameof(Patches.Prefix), Priority.First) : null,
            finalizer: At(nameof(Patches.Finalizer), Priority.First));
    }
}
```

Add four `[TestMethod]`s (`fake` is a new `FakeMission`; `<OnTick>` is
`AccessTools.Method(typeof(FakeMission), nameof(FakeMission.OnTick))`):

1. `AThrowBeforeTheLaunch_RestoresTheFlag_AndStillReachesTheCaller`: `PatchRecovery(true)`;
   `ThrowBeforeLaunch = true`; `Assert.ThrowsException<InvalidOperationException>(() => fake.OnTick(true))`;
   `Assert.IsTrue(fake.tickCompleted)`.
2. `AThrowFromAPostfixAfterTheLaunch_LeavesTheFlagDown`: `PatchRecovery(true)` plus
   `_harmony.Patch(<OnTick>, postfix: At(nameof(Patches.ThrowingPostfix), Priority.Normal))`;
   `ThrowBeforeLaunch = false`; the call throws; `Assert.IsFalse(fake.tickCompleted)`.
3. `ALowerPrioritySwallow_StillLetsTheRecoveryRunFirst`: `PatchRecovery(true)` plus
   `_harmony.Patch(<OnTick>, finalizer: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(Patches.SwallowingFinalizer))))`
   (default priority); `ThrowBeforeLaunch = true`; `fake.OnTick(true)` does NOT throw;
   `Assert.IsTrue(fake.tickCompleted)`.
4. `WithoutItsPrefix_TheFinalizerNeverRestores`: `PatchRecovery(false)`; `ThrowBeforeLaunch = true`;
   the call throws; `Assert.IsFalse(fake.tickCompleted)`.

**Verify**: Build; Filtered `TickHandshakeRecoveryHarmonyTests`: 4 passed. If any fails, Harmony
does not behave as the design assumes: STOP (do not change priorities or switch to a prefix
marker to make it pass).

### Step B4 (RED): Binding and registration tests

Create `TAOM.Tests/Features/MissionTickGuard/Patch91bMissionTickRecoveryBindingTests.cs`
(pattern: `TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs` for
`GameAssemblies.EnsureLoaded` and `Assert.Inconclusive`; `TAOM.Tests/Features/WandererAllegiance/WandererAllegianceBindingTests.cs`
for `PatchProcessor.ReadMethodBody`). Usings: `System`, `System.Collections.Generic`, `System.Linq`,
`System.Reflection`, `System.Reflection.Emit`, `HarmonyLib`, MSTest, `TaleWorlds.MountAndBlade`,
`TAOM.Features.MissionTickGuard.Hooks`, `TAOM.Tests.Infrastructure` (`RepoPaths`),
`TAOM.Tests.Migration` (`GameAssemblies`). Copy `[ClassInitialize]` and `RequireGame()` from the
Patch82 test. Helpers:

```csharp
private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
private static List<KeyValuePair<OpCode, object>> Body(MethodBase m) => PatchProcessor.ReadMethodBody(m).ToList();
private static bool StoresFlag(KeyValuePair<OpCode, object> op) =>
    op.Key == OpCodes.Stfld && op.Value is FieldInfo f && f.Name == "tickCompleted" && f.DeclaringType == typeof(Mission);
private static bool Calls(KeyValuePair<OpCode, object> op, string name) =>
    (op.Key == OpCodes.Call || op.Key == OpCodes.Callvirt) && op.Value is MethodBase m && m.Name == name;
private static MethodInfo OnTick() =>
    AccessTools.Method(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) });
```

Six tests; the first five carry `[TestCategory("BindingVerification")]` and call `RequireGame()`:

1. `OnTick_IsThePublicNonVirtualFourArgumentMethod`: non-null, `IsPublic`, not static, not
   virtual, returns `void`, and `typeof(Mission).GetMethods(Declared).Count(m => m.Name == "OnTick")` is 1.
2. `TickAgentsAndTeamsAsync_IsPublicWithOneFloat`: `AccessTools.Method(typeof(Mission), "TickAgentsAndTeamsAsync", new[] { typeof(float) })`
   non-null, public, instance, `void`.
3. `TickCompleted_IsAPrivateInstanceBool_ThatResolvesAsAFieldRef`: `GetField("tickCompleted",
   Instance|NonPublic)` non-null, `IsPrivate`, `FieldType == typeof(bool)`; and
   `AccessTools.FieldRefAccess<Mission, bool>("tickCompleted")` returns non-null.
4. `OnTick_ClearsTheFlagOnce_ThenRunsTheBehaviours_ThenStartsTheAgentTick`: in `Body(OnTick())`
   exactly one `StoresFlag` index `c`, preceded by `OpCodes.Ldc_I4_0`; the first
   `Calls(.., "OnMissionTick")` index `b` > `c`; exactly one `Calls(.., "TickAgentsAndTeamsAsync")`
   and exactly one `Calls(.., "TickAgentsAndTeamsImp")`, both > `b`.
5. `OnlyTheAgentTickSetsTheFlag_AndNoOtherMethodWritesIt`: `TickAgentsAndTeamsImp(float, bool)`
   has exactly one `StoresFlag`, preceded by `OpCodes.Ldc_I4_1`; over
   `typeof(Mission).GetMethods(Declared)`, skipping abstract, generic-definition and body-less
   methods and any whose `Body` throws, assert at least 200 methods were read and exactly 2
   `StoresFlag` in total.
6. `PatchClasses_AreRegisteredInAllThreePlaces` (no game needed): both
   `Mission_OnTick_TickRecovery_Patch` and `Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch`
   carry one `[HarmonyPatch]` and `[HarmonyPatchCategory("Patch91b_MissionTickRecovery")]`; in
   `RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)` the indexes satisfy
   `TryPatchCategory("Patch91_MissionTickStall");` < `TryPatchCategory("Patch91b_MissionTickRecovery");`
   < `MissionTickRecoveryWiring.VerifyAndArm(`, and each of the last two appears once; and in
   `AccessTools.GetTypesFromAssembly(typeof(Mission_OnTick_TickRecovery_Patch).Assembly).ToList()`
   the index of `Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch` is at least 0 and below the index
   of `Mission_OnTick_TickRecovery_Patch` (the launch target must be patched first: see "Patch
   order" at the top of Part B).

Each assertion message names the engine fact it pins (for example "Mission.OnTick no longer clears
tickCompleted exactly once: re-derive Patch91b against the new engine").

**Verify**: Build fails with CS0246 for the patch classes (RED; CS0234 for the `Hooks` namespace may
print beside it).

### Step B5 (GREEN): The hooks, the wiring and the SubModule block

Create `Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs`:

```csharp
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.MissionTickGuard.Hooks;

// Patch91b (port decision D17). Installed v1.4.8 (taom-src): public void Mission.OnTick(float, float,
// bool, bool) (Mission.cs:3636); public void Mission.TickAgentsAndTeamsAsync(float) (:1825, one caller,
// :3770); private bool Mission.tickCompleted (:1040, written only at :3613 and :3740). The rule and
// why it is safe: TickHandshakeRecovery and docs/features/mission-tick-guard.md.

/// <summary>Arms Patch91b after its category applied. Never throws into the game-init batch.</summary>
public static class MissionTickRecoveryWiring
{
    public const string Category = "Patch91b_MissionTickRecovery";
    private const string Tag = "[TickRecovery]";
    private static IModLogger? _logger;

    internal static AccessTools.FieldRef<Mission, bool>? TickCompleted { get; private set; }

    public static void VerifyAndArm(IModLogger logger)
    {
        _logger = logger;
        try
        {
            AccessTools.FieldRef<Mission, bool>? field = null;
            string fieldNote = "resolved";
            try { field = AccessTools.FieldRefAccess<Mission, bool>("tickCompleted"); }
            catch (Exception ex) { fieldNote = $"{ex.GetType().Name}: {ex.Message}"; }

            bool launchMark = HasPatch(LaunchTarget(), typeof(Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch), needPrefix: false);
            bool recovery = HasPatch(OnTickTarget(), typeof(Mission_OnTick_TickRecovery_Patch), needPrefix: true);

            TickCompleted = field;
            if (TickHandshakeRecovery.Instance.Arm(field != null, launchMark, recovery))
                logger.LogInfo($"{Tag} armed: a throw out of Mission.OnTick that launched no agent tick no longer leaves the next frame waiting");
            else
                logger.LogWarning($"{Tag} off (field {fieldNote}, launch mark {launchMark}, OnTick prefix and finalizer {recovery}): a throw out of a mission tick can still freeze the game");
        }
        catch (Exception ex)
        {
            TickHandshakeRecovery.Instance.Arm(false, false, false);
            try { logger.LogWarning($"{Tag} off: arming threw {ex.GetType().Name}: {ex.Message}"); }
            catch { /* never throw into the game-init batch */ }
        }
    }

    internal static void LogRestore(Exception? exception, long count)
    {
        try { _logger?.LogError($"{Tag} Mission.OnTick threw {exception?.GetType().Name}: {exception?.Message} after it cleared tickCompleted and before the agent tick started; restored the flag so the next frame does not wait forever. This frame's agent tick is skipped (restore #{count})."); }
        catch { /* a log failure must never replace the mission's exception */ }
    }

    private static MethodBase? OnTickTarget() => AccessTools.Method(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) });
    private static MethodBase? LaunchTarget() => AccessTools.Method(typeof(Mission), nameof(Mission.TickAgentsAndTeamsAsync), new[] { typeof(float) });

    private static bool HasPatch(MethodBase? target, Type patchClass, bool needPrefix)
    {
        if (target == null) return false;
        var info = Harmony.GetPatchInfo(target);
        if (info == null) return false;
        bool finalizer = info.Finalizers.Any(p => p.PatchMethod?.DeclaringType == patchClass);
        bool prefix = !needPrefix || info.Prefixes.Any(p => p.PatchMethod?.DeclaringType == patchClass);
        return finalizer && prefix;
    }
}

// The launch mark is a finalizer, not a prefix: PatchShield's rescue path strips prefixes,
// postfixes and transpilers but never finalizers, and a finalizer runs even if the launch threw.
// Keep this class ABOVE the OnTick class: categories apply in assembly order, and this small target
// can be inlined into an OnTick rebuilt before it was patched, which would lose the mark.
[HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsAsync), new[] { typeof(float) })]
[HarmonyPatchCategory(MissionTickRecoveryWiring.Category)]
public static class Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch
{
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer() => TickHandshakeRecovery.Instance.MarkLaunch();
}

// Prefix and finalizer share Harmony's __state, so they must stay in this one class.
[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) })]
[HarmonyPatchCategory(MissionTickRecoveryWiring.Category)]
public static class Mission_OnTick_TickRecovery_Patch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(Mission __instance, out bool __state)
    {
        var field = MissionTickRecoveryWiring.TickCompleted;
        __state = field != null && TickHandshakeRecovery.Instance.Enter(field(__instance));
    }

    // Void, so Harmony rethrows the original exception and Patch37 reports it with its stack.
    // Priority.First, so it sees the exception before PatchShield's finalizer can swallow it.
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    public static void Finalizer(Mission __instance, Exception? __exception, bool __state)
    {
        var field = MissionTickRecoveryWiring.TickCompleted;
        if (field == null) return;
        var decision = TickHandshakeRecovery.Instance.Exit(__state, __exception != null, field(__instance));
        if (decision == RecoveryDecision.None) return;
        field(__instance) = true;
        if (decision == RecoveryDecision.RestoreAndLog)
            MissionTickRecoveryWiring.LogRestore(__exception, TickHandshakeRecovery.Instance.RestoreCount);
    }
}
```

In `Main/SubModule.cs`, directly after the Patch91 block from Step A8 (after its
`MissionTickStallWatchdog>().Start();` line and a blank line), add:

```csharp
        // Patch91b tick-handshake recovery (port decision D17): Mission.OnTick clears tickCompleted
        // before the behaviour loop and only the agent tick sets it again, so an exception out of any
        // OnMissionTick, swallowed above, froze the next frame forever. The recovery restores the flag
        // only when that call launched no agent tick, and VerifyAndArm arms it only after finding both
        // patches and the private field. VerifyAndArm never throws into this batch.
        TryPatchCategory("Patch91b_MissionTickRecovery");
        Features.MissionTickGuard.Hooks.MissionTickRecoveryWiring.VerifyAndArm(IoC.Resolve<IModLogger>());
```

**Verify**: Build exit 0; Filtered
`"FullyQualifiedName~Patch91bMissionTickRecoveryBindingTests|FullyQualifiedName~TickHandshakeRecovery|FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~PatchCategoryApplierTests|FullyQualifiedName~Patch91WiringTests"`:
`Failed: 0`, and the six binding tests passed (not Inconclusive). `wc -l Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs` is under 150.
If test 4 or 5 fails: STOP (the engine facts the recovery rests on changed).

### Step B6: Docs for the recovery

1. Create `docs/features/mission-tick-guard.md` with the sections of `docs/features/TEMPLATE.md`
   and this content (no long dashes):
   - **Overview**: one exception in a mission tick no longer freezes the game; the frame's agent
     tick is skipped and the exception still reaches the crash report.
   - **Why This Exists**: vanilla v1.4.8 (the `Mission.cs` lines in this plan's Engine facts:
     `:3740` clear, `:3741-3744` behaviours, `:3768-3775` launch, `:3613` set, `:3585-3591` wait,
     `:3529-3538` `OnPreTick`); the 1.4.x swallowers (the table in Current state); without it, a
     throw from any `OnMissionTick` (vanilla, TAOM or another mod) or the QA toggle freezes the
     game with audio playing.
   - **Architecture**: the design bullets at the top of Part B; a component diagram of
     `SubModule` -> `TryPatchCategory` -> `VerifyAndArm` -> the two patch classes ->
     `TickHandshakeRecovery`; the decision cells (entered armed, threw, launched, flag now) with
     the one cell that restores.
   - **Configuration**: none, by design; the three log lines: `[TickRecovery] armed: ...` (INFO,
     once per process at game start), `[TickRecovery] off (...)` (WARNING; the guard is inert),
     `[TickRecovery] Mission.OnTick threw ...` (ERROR, occurrences 1, 2, 10, 100...).
   - **Performance**: one flag read in the prefix, one in the finalizer, one bool write at the launch
     mark on each `Mission.OnTick`; no allocation.
   - **Key Files**, **Tests** (the three test classes and what each pins), **Dependencies**
     (Patch37 still reports; Patch35 shares the target; PatchShield wraps both targets from a
     process's second game start and runs after the recovery).
   - **How to verify in game**: In-game check 1 of this plan.
   - **Limitations**: a stuck agent tick (Patch91's `[MissionStall]` names it); an exception on
     the agent-tick thread (until WP107 the Native2Managed bridge swallows it and the game still
     freezes; after WP107 it reaches native); a throw before the clear or after the launch needs
     no restore; a throw every frame skips every agent tick (degraded play, menus still work);
     after PatchShield swallows a missing-API exception on `Mission.OnTick` it strips TAOM's
     prefix there (owner `com.taom.mod` is not on its protected list), so later calls are not
     recovered (WP107 handoff); a foreign `Priority.First` finalizer registered earlier that
     swallows hides the exception from the guard.
   - **Changelog**: the port date, D17; **GitHub Issue**: pending (orchestrator).
2. `docs/reference/harmony-patch-registry.md`: after the `## Patch91_MissionTickStall` section add:
   ```text
   ## Patch91b_MissionTickRecovery

   **Targets:** `public void Mission.OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)` (v1.4.8 `Mission.cs:3636`): a prefix and a void finalizer, both `Priority.First`, in `Mission_OnTick_TickRecovery_Patch`; `public void Mission.TickAgentsAndTeamsAsync(float dt)` (`:1825`): a void finalizer, `Priority.First`, in `Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch`. Both in `Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs`; writes the private `Mission.tickCompleted` (`:1040`) through a cached `AccessTools.FieldRefAccess`.

   Tick-handshake recovery (port decision D17). `Mission.OnTick` clears `tickCompleted` (`:3740`), runs every `OnMissionTick` and only then starts the agent tick (`:3768-3775`); only the agent tick sets the flag again (`:3613`) and the next frame's `OnPreTick` spins in `WaitTickCompletion` (`:3585-3591`). Every catcher above that window swallows on 1.4.x, so one exception out of an `OnMissionTick` froze the game. The prefix records whether the flag was set at entry (`__state`); the launch finalizer marks the agent-tick launch; the `OnTick` finalizer sets the flag back only when the call threw, began with the flag set, launched no agent tick and left it false, then logs a throttled `[TickRecovery]` ERROR. The exception still reaches Patch37. `MissionTickRecoveryWiring.VerifyAndArm`, called right after `TryPatchCategory` in the game-init batch (after Patch91, once per process), arms only when both patches are in `Harmony.GetPatchInfo` and the field resolved; otherwise it logs `[TickRecovery] off` and stays inert. PatchShield wraps both targets from a process's second game start and runs after the recovery. Feature: [mission-tick-guard.md](../features/mission-tick-guard.md). **ACTIVE** (ported YYYY-MM-DD).
   ```
3. `docs/features/crash-report.md`, How-To "Trigger a test crash": after step 4 insert a new step
   5 and renumber "The MCM toggle auto-resets to OFF." to 6:
   ```text
   5. Mission-tick trigger only: the battle keeps running after you close the dialog, and the TAOM log holds one `[TickRecovery]` ERROR line: the throw left `Mission.OnTick` after the engine cleared `tickCompleted`, and Patch91b restored it ([mission-tick-guard.md](mission-tick-guard.md)). A frozen game here means the guard is off; look for `[TickRecovery] off` near the top of the log.
   ```
4. `docs/features/battle-load-diagnostics.md`: at the end of the "battle-freeze sampler" section
   (the one Part A added) append this paragraph:
   ```text
   Its sibling Patch91b ([mission-tick-guard.md](mission-tick-guard.md)) recovers the handshake when an exception leaves `Mission.OnTick` before the agent tick starts; this sampler photographs what it cannot recover, such as a stuck agent tick.
   ```

**Verify**: Docs command: `dead_links`, `orphan_features`, `missing_features` equal A0.6;
`ai_dashes: 0`.

### Step B7: Commit B1

Full Test. **Verify**: `Failed: 0`, `Total` = T0 + 41 + 21.

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git add Main/Features/MissionTickGuard/TickHandshakeRecovery.cs Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs Main/SubModule.cs TAOM.Tests/Features/MissionTickGuard/TickHandshakeRecoveryTests.cs TAOM.Tests/Features/MissionTickGuard/TickHandshakeRecoveryHarmonyTests.cs TAOM.Tests/Features/MissionTickGuard/Patch91bMissionTickRecoveryBindingTests.cs docs/features/mission-tick-guard.md docs/features/crash-report.md docs/features/battle-load-diagnostics.md docs/reference/harmony-patch-registry.md && git status --porcelain
```

**Verify**: exactly those 10 paths, all staged. Write `C:\Users\mikew\source\repos\taom-port\106-msg-B1.txt`:

```text
fix(mission): v2.0.28 - restore tickCompleted after a mission-tick throw

Mission.OnTick clears tickCompleted before it runs the mission
behaviours and starts the agent tick last; only the agent tick sets
the flag again, and the next frame waits for it with no timeout. On
1.4.x every catcher above swallows, so one exception out of any
OnMissionTick froze the battle for good with audio still playing. The
QA toggle Throw On Next Mission Tick is exactly that throw.

Patch91b notes whether the flag was set when OnTick began and marks
the agent-tick launch from a finalizer on TickAgentsAndTeamsAsync.
When OnTick then exits by exception with the flag down and no launch,
it sets the flag back and logs one [TickRecovery] error; the exception
still reaches the crash report. It never restores after a launch, so
behaviours cannot overlap a running agent tick, and it arms only when
both patches and the private field are verified at game start.

Research: taom-src v1.4.8 Mission.cs 1040, 1825, 3529-3538,
3585-3591, 3601-3618, 3636-3776; MissionState.cs 201, 212-219
Constraint: Mission is sealed and tickCompleted private; written
through a cached AccessTools.FieldRefAccess
Rejected: restoring on any exception (unsafe once the agent tick has
launched)
Not-tested: the live engine path; the in-game check is the QA toggle
Save-compat: no saved state touched
```

Then, in the NEXT Bash call:
`cd /c/Users/mikew/source/repos/taom-port/106 && git commit -F C:/Users/mikew/source/repos/taom-port/106-msg-B1.txt && git log -1 --format='%h %s' && git status --porcelain`.
**Verify**: the new commit; nothing left.

### Step B8: The bd1ac8292 lesson, the swallow-site comments and the thread-table row

Every block below is shown at list indentation: match the indentation of the lines around the
insertion point in the real file.

1. `Dependencies/Foundation/SaveShieldPolicy.cs` (comment only): replace the two summary lines
   starting `/// MISSION-INIT keeps swallowing in every case` with:
   ```csharp
   /// MISSION-INIT keeps swallowing in every case, co-op included. The fault is local and
   /// non-authoritative, and a rethrow would not surface it: <c>Mission.SpawnTroop</c> runs inside
   /// an <c>OnMissionTick</c> (the battle spawn logic's deployment and reinforcement checks), after
   /// <c>Mission.OnTick</c> cleared <c>tickCompleted</c> (v1.4.8 <c>Mission.cs:3740</c>). The next
   /// catcher above is Patch37 on <c>Module.OnApplicationTick</c>, which swallows, so the agent tick
   /// that sets the flag again (<c>:3613</c>) never starts and the next frame's
   /// <c>WaitTickCompletion</c> (<c>:3585-3591</c>) would wait forever. TAOM's Patch91b restores the
   /// flag in that case, but a swallow here still costs one spawn where a rethrow costs a whole
   /// agent tick.
   ```
2. `Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs` (comment only): after the comment
   line `// eats the engine-drift trinity and rethrows everything else.` add:
   ```csharp
        //
        // This event can fire inside a mission tick (SandBox TournamentBehavior.OnMissionTick ->
        // EndCurrentMatch -> TournamentEnd, v1.4.8 TournamentBehavior.cs:149-155 and :257-260), after
        // Mission.OnTick cleared tickCompleted (Mission.cs:3740). A rethrow there lands on Patch37 at
        // Module.OnApplicationTick, which swallows, so the agent tick that sets the flag again never
        // starts; Patch91b restores the flag, at the cost of that frame's agent tick.
   ```
   and replace the line `// The tournament screen survives with an incomplete winner panel.` with:
   ```csharp
        // The tournament screen survives with an incomplete winner panel. That is all the swallowed NRE
        // costs: the prize (TournamentBehavior.cs:247), the leaderboard entry (:248) and
        // OnTournamentFinished (:256) all run before the event.
   ```
3. `docs/reviews/lessons/harmony-il.md` (D7): append at the end of the file:
   ```text

   ### Before a swallow becomes a rethrow, follow the exception to its next catcher and name the frame it leaves (tick handshake, port of bd1ac8292)
   v1.4.8 `Mission.OnTick` clears `tickCompleted` (`Mission.cs:3740`), runs every `OnMissionTick` (3741-3744) and only then starts the agent tick (3768-3775); only the agent tick sets the flag again (3613), and the next frame's `OnPreTick` spins in `WaitTickCompletion` (3585-3591) until it does. Every catcher above that window swallows on 1.4.x (Patch37 on `Module.OnApplicationTick`, the Native2Managed bridge, PatchShield's missing-API swallow from a process's second game start), so an exception out of an `OnMissionTick` froze the game for good. On the 1.5.x line two co-op rethrows, SaveShield's `SpawnTroop` and `SetMissionMode` targets and Patch69 on `TournamentVM.OnTournamentEnd`, which its v1.5.5 review found reachable inside a mission tick (re-traced on v1.4.8 for `SpawnTroop` and `OnTournamentEnd`), would have turned a cosmetic fault into that hang; `bd1ac8292` reverted them.
   - **Why missed:** the change was specified as "rethrow so the failure is visible", and nobody traced where the rethrow would land.
   - **Prevent:** before a finalizer or shield hands back an exception it used to swallow, or before code that can throw is added inside a mission tick, list every frame between the throw and the next catcher from the installed IL and say what each leaves half done. Inside a mission tick a rethrow is never safer than a swallow. Patch91b (`docs/features/mission-tick-guard.md`) now turns that hang into one skipped agent tick plus a crash report when the throw launched no agent tick; it does not make a rethrow free.
   - **Source:** 1.5.x `bd1ac8292` (RCA `docs/reviews/rca-coop-safety-nets-2026-10-21.md` on `origin/bannerlord-1.5.x`, finding 1); plan 106.
   ```
4. `.claude/rules/harmony-patches.md`:
   - Append to the end of the Finalizer bullet (same line, after one space):
     ```text
     Before a finalizer or shield turns a swallow into a rethrow, follow the exception to its next catcher: an exception out of any `OnMissionTick` leaves `Mission.OnTick` after it cleared `tickCompleted` (v1.4.8 `Mission.cs:3740`), and every catcher above swallows, so without Patch91b the next frame waits in `WaitTickCompletion` forever (`lessons/harmony-il.md`, tick handshake).
     ```
   - In the thread table, insert this row between the TWParallel worker-pool row and the
     "Either: native decides" row:
     ```text
     | Possibly several threads at once (`[MBCallback(null, true)]`) | `Mission.GetDefendCollisionResults` (v1.4.8 `Mission.cs:6455-6456`), which reaches `AgentApplyDamageModel.DecideCrushedThrough` (`MissionCombatMechanicsHelper.cs:311`) and so `TaomCombatMechanicsModel.DecideCrushedThrough`; also `Mission.CanPhysicsCollideBetweenTwoEntities` (`:2451`), `Mission.OnFixedTick` (`:3519`), `Mission.MissileCalculatePassbySoundParametersCallbackMT` (`:6026`), `Agent.UpdateMountAgentCache` (`Agent.cs:1813`) and `Agent.GetWeaponInaccuracy` (`:1842`). The engine marks these multi-thread callable and no managed code reads the flag, so treat each as able to run on several threads at once and never as the main thread |
     ```
   - Replace `in the last three rows may run concurrently` with `in the last four rows may run concurrently`.
   - The sentence ending `line names the frame.` (in the "frame order matters" paragraph): append,
     after one space:
     ```text
     An exception that leaves `Mission.OnTick` between the clear and the agent-tick launch would freeze it the same way; Patch91b restores the flag in that case only, never after a launch.
     ```
   - After the paragraph that follows the table, add this paragraph:
     ```text
     `MBRandom.RandomFloat` on the defend-collision path draws from the shared `Game.Current.RandomGenerator` (`MBRandom.cs:22`, `:32`), an `MBFastRandom` whose four `uint` fields advance with no lock (`MBFastRandom.cs:22-32`, `XorShift` at `:186`). Concurrent draws only duplicate or skip values and cannot reach a stuck state, so the crush roll (`TaomCombatMechanicsModel.cs:177`) stays unlocked (port decision D16); any other write on that path takes a lock.
     ```

**Verify**: Build exit 0 (the two code edits are comments); Filtered
`"FullyQualifiedName~SaveShieldPolicyTests|FullyQualifiedName~Patch69"`: `Failed: 0`;
`git diff --stat` lists exactly these four files; `git diff Dependencies Main | grep '^[-+] ' | grep -vE '^[-+]\s*(//|///)' | grep -c .`
prints `0` (comment lines only); Docs command `ai_dashes: 0`.

### Step B9: Commit B2

```bash
cd /c/Users/mikew/source/repos/taom-port/106 && git add Dependencies/Foundation/SaveShieldPolicy.cs Main/Features/Arena/Hooks/Patch69_TournamentEndGuard.cs docs/reviews/lessons/harmony-il.md .claude/rules/harmony-patches.md && git status --porcelain
```

**Verify**: those four paths, staged. Write `C:\Users\mikew\source\repos\taom-port\106-msg-B2.txt`:

```text
docs(mission): v2.0.28 - tick handshake lesson and swallow-site notes

Records why a swallow inside a mission tick must not become a
rethrow. SaveShield's mission-start swallow and the tournament end
guard both sit inside an OnMissionTick, after Mission.OnTick cleared
tickCompleted, and every catcher above them swallows. Adds the
lesson, the comments at both sites and the harmony rule sentence,
re-cited to the v1.4.8 engine. Comments only: no behaviour changes,
so no paired TAOM.Dependencies release is needed.

The harmony rule's thread table also gains a row for the callbacks
the engine marks multi-thread callable, among them the defend
collision callback that reaches the combat mechanics crush roll. Its
MBRandom draw stays unlocked: concurrent draws only duplicate or skip
values.

Research: v1.4.8 dump TournamentBehavior.cs 149-155, 247-260;
DefaultBattleMissionAgentSpawnLogic.cs 171, 532-642, 707;
MissionBattleSideSpawnContext.cs 230, 373; taom-src Mission.cs
6455-6456, MBRandom.cs 22 and 32, MBFastRandom.cs 22-32 and 186
Ported-from: bd1ac829249a8333d7198b7a433071a2c1cecc52
```

The type changes from the 1.5.x `fix(coop)` to `docs` on purpose: only comments, a lesson and a
rule sentence land on 1.4.x (D7). Then, in the NEXT Bash call:
`cd /c/Users/mikew/source/repos/taom-port/106 && git commit -F C:/Users/mikew/source/repos/taom-port/106-msg-B2.txt && git log -1 --format='%h %s' && git status --porcelain`.

### Step B10: Review gate, final verification, hand-off

1. Run the Done criteria below; every one must hold.
2. Review gate: run `/deep-review` on `git diff $(git merge-base HEAD bannerlord-1.4.5)..HEAD`
   (C# across many files and a single-owner file); ask it to probe the spots under "Maintenance
   notes". Act only on findings that change runtime behaviour or break this plan; land each fix
   as one extra commit `fix(mission): v2.0.28 - review follow-ups` (test first, same procedure).
   Never rewrite earlier commits. If your session cannot invoke skills, say so in the hand-off
   and the integrator runs it. Never run `/review-codex` or `/codex-verify` (paid; maintainer only).
3. Write the hand-off report.

### Part B done criteria

- [ ] Build exits 0; full Test `Failed: 0` with `Total` = T0 + 62 (41 from Part A, 21 from Part B).
- [ ] `grep -cF 'TryPatchCategory("Patch91b_MissionTickRecovery");' Main/SubModule.cs` prints `1`;
      `grep -cF 'MissionTickRecoveryWiring.VerifyAndArm(' Main/SubModule.cs` prints `1`.
- [ ] `grep -rlF 'FieldRefAccess<Mission, bool>("tickCompleted")' Main` prints only
      `Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs`;
      `grep -c 'HarmonyPriority(Priority.First)' Main/Features/MissionTickGuard/Hooks/Patch91b_MissionTickRecovery.cs` prints `3`.
- [ ] `grep -rn 'TaleWorlds' Main/Features/MissionTickGuard/TickHandshakeRecovery.cs` prints nothing (ADR-007).
- [ ] `git diff --name-only $(git merge-base HEAD bannerlord-1.4.5)..HEAD | sort -u | wc -l` prints
      `55`; the same list shows no `CHANGELOG.md`, `CLAUDE.md`, `feature-map.md`, `LESSONS-LEARNED.md`
      and, under `Dependencies/`, only `Dependencies/Foundation/SaveShieldPolicy.cs`.
- [ ] `git log --format=%s $(git merge-base HEAD bannerlord-1.4.5)..HEAD | awk 'length($0) > 72 || $0 !~ / v2\.0\.28 - /'`
      prints nothing (three subjects, more only for review follow-ups).
- [ ] Docs command: `dead_links`, `orphan_features`, `missing_features` equal A0.6; `ai_dashes: 0`.
- [ ] `git status --porcelain` prints nothing.

## Disposition (every commit in the pack, plus the related items the notes assign)

Cherry-pick simulation re-run this session:
`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`.

| 1.5.x commit | Subject | Simulation (exit 1 = conflict) | Disposition |
|---|---|---|---|
| `f75288ebf` | fix(mission): v2.0.30 - park off-thread callback writes (#634) | 1: `CHANGELOG.md`, `CLAUDE.md`, `CareerPerkMissionBehavior.cs`, `SignatureAgentRoster.cs`, `SettingsFingerprintTests.cs`, `bannerlord-together-compat.md`, `coop-interop.md`, `field-commission.md`, `harmony-patch-registry.md`, `LESSONS-LEARNED.md`, `lessons/harmony-il.md` | **Ported** (Part A, `cherry-pick -n`, commit A). Hunks: all code and tests kept (career file hand-merged without #611/#613; `CareerPerkOffThreadTests` adapted to `ICareerPassiveService`; Patch91 applied through `TryPatchCategory`); feature docs, registry, both RCAs and the two rule files kept and adapted (Steps A4 to A6); `SignatureAgentRoster.cs` dropped (feature absent, `0700ca901`); `CHANGELOG.md` dropped (D1); `CLAUDE.md`, `feature-map.md`, `LESSONS-LEARNED.md`, `lessons/harmony-il.md`, `lessons/adapters-taleworlds-api.md` dropped (WP142/WP143) |
| `bd1ac8292` | fix(coop): v2.0.35 - revert co-op rethrows that could hang a battle | 1: `.claude/rules/harmony-patches.md`, `CollectAssemblyTypesPolicy.cs`, `CollectAssemblyTypesShim.cs`, `SaveShield.cs`, `SaveShieldPolicy.cs`, `ShieldCoverage.cs`, `Patch69_TournamentEndGuard.cs`, `CoopSessionProvider.cs`, six test files, `bannerlord-together-compat.md`, `coop-interop.md`, `coop-compat-gap-ledger.md`, `lessons/harmony-il.md`, `lessons/misc.md` | **Adapted** (commit B2, hand-written): the tickCompleted lesson (rewritten for 1.4.x, D7), the `SaveShieldPolicy` and Patch69 comments and the rule sentence, re-cited to v1.4.8. **Skipped** (D7): every co-op change (`CollectAssemblyTypesPolicy` narrowing, `CoopSessionProvider` bind, PatchShield removal path, `ShieldCoverage`), the behaviour-neutral `ShouldSwallow` extraction (no 1.4.x need: both files already equal the reverted state), the misc.md lesson (co-op ledger, WP142 may take it), the RCA file, the `paths:` line adding `Dependencies/Foundation/**` (WP143) |

Related items named in this WP's notes, not ported here:

| Item | Disposition |
|---|---|
| `6ce20f6ed` (prereq of `bd1ac8292`) | Not ported (D7): its co-op rethrows never reached 1.4.x |
| `9f58c58de` (locked crush-hold RNG) | Not ported (D16). Replaced by the documentation row in Step B8 (K1) |
| `6eb2e1b90` (profiler hooks in Patch91; PatchShield exclusion) | WP117 (profiler half); its `TickAgentsAndTeamsImp` exclusion entry is WP107's |
| `42624b95b`, `8e6b0935c`, `8c84fa20e`, `9e8c99bdb` (FREEZE-THREADING-01 fix a, plan 006) | WP107 |
| `30abf550b` (generic `ExcludedTargetMethods`) | WP107 |
| `efe326242`, `e5f644da4`, `b8b144635`, `fe8f30c7a`, `87f9f862e` (FREEZE-THREADING-03 perf follow-ups) | WP124, which must keep `SpatialGrid.Remove` parked and off-thread queries non-rebuilding (V05 section 4) |
| `2b1d1ef96` (#611), `7ede923be` (#613) | WP129: their lines go into the methods Step A3 creates |
| `0700ca901` (SignatureStrikes) | OUT (1.5.x-only feature) |
| `af990ca76` toolchain doc Mono row (V05 item 4) | WP117 or WP144 |

Findings this WP owns: **FREEZE-THREADING-01**: fix (b) is Part B (as redesigned by V04 and D17;
the lens's unconditional restore is rejected); fix (a) is WP107; fix (c), a try/catch in each of
TAOM's 10 unguarded `OnMissionTick` overrides, is not in D17 and is deferred (Maintenance notes).
**FREEZE-THREADING-03** and **RUNTIME-PERF-04**: Part A (all nine V05 rows except row 10, which is
D16's documentation row).

## Test plan

- Part A (41): the 40 `f75288ebf` methods (see "What the two 1.5.x commits carry") plus
  `Patch91WiringTests`. Part B (21): Steps B1 (11 decision cells), B3 (4 Harmony behaviours) and
  B4 (6 engine facts and registration). Patterns: `MountDespawnOffThreadTests` (record on a
  worker, drain on the tick), `IlCallScanner` routing pins (`WargOffThreadTests`).
- Not unit-testable (commit trailers): the live engine path of both patches, `Thread.Suspend`
  capture on a live thread, the off-thread races themselves.
- Verification: full Test `Total` = T0 + 41 after Part A and T0 + 62 after Part B, `Failed: 0`.

## STOP conditions

Stop and report (do not improvise) if:

- Step A0.3 fails (a prerequisite WP is missing: `PatchCategoryApplier`, `TryPatchCategory`,
  `RepoPaths.ReadSource`, plan 009's source gate, `Main/Composition`, WP103's
  `hookSpecificOutput`, or a CHANGELOG hook still present), or `Main/Features/SignatureStrikes`,
  `Main/Features/MissionTickGuard` or `MissionTickStallWatchdog.cs` already exists.
- Step A0.5 shows engine lines or members other than the plan's, or any binding test in Step B5
  (tests 4 and 5 above all) fails: the facts the recovery's safety rests on have changed.
- Step A1 lists a conflicted code or test path not named in this plan.
- The baseline (A0.4) or any full suite after a commit shows a failure that this plan does not
  explain; or a step's verification fails twice after a reasonable fix attempt.
- `TickHandshakeRecoveryHarmonyTests` fails (Harmony 2.4.2 does not order or rethrow as assumed).
  Do not change priorities, switch the launch mark to a prefix, or catch the exception instead.
- The settings census does not line up after Step A4 in a way that a "+1" does not explain
  (for example the simulation-relevant count moves from 180): the new setting must be excluded.
- The work seems to need any `Dependencies/` edit other than the `SaveShieldPolicy.cs` comment, a
  patch on `TickAgentsAndTeamsImp` or `WaitTickCompletion`, a `Main/IoC.cs` edit, an MCM default
  flip, or any file outside Scope.
- A hook denies a commit (report its message verbatim; never bypass it).

## In-game checks owed (maintainer)

Executors cannot run the game. Logs: the session's `taom_debug_<timestamp>.log` in TAOM's `Logs`
folder (`Main/Core/Logging/FileLogger.cs:26-40`). Use a build where TAOM and TAOM.Dependencies come
from one build (the `SaveShieldPolicy` edit is a comment, but the release rule is general).

0. Optional, BEFORE merging this WP (proves the hazard on v1.4.8, V04 section 4): in a custom
   battle, Mod Options, TAOM Crash Report page, QA Dev Triggers group (both titles carry a long dash
   in game), turn on "Throw On Next Mission Tick". Expected if the hazard is real: the game freezes
   with audio playing; kill it. If it does not freeze, report that (V04 recommends `/investigate`).
1. After the merge, the same toggle: the crash-report inquiry appears, the battle keeps running
   after you close it, and the log holds `[TickRecovery] armed` (at game start) and one
   `[TickRecovery] Mission.OnTick threw TaomDevTriggerException ...` ERROR. Repeat once more in the
   same battle: a second ERROR line (occurrence 2 is logged).
2. A large field battle with reinforcements and at least one warg rider, 15 minutes: no freeze;
   any off-thread report reads "parked for the next mission tick, which is expected" at WARNING;
   no on-screen "[TAOM] ... ran off the main mission thread" message.
3. Normal battle, siege, tournament and their exits: no `[MissionStall]` line (no false alarm on
   teardown) and no `[TickRecovery] Mission.OnTick threw` line. The TAOM Battle Load Diagnostics
   MCM page, group Exit Stall Sampler, shows "Enable Battle Freeze Sampler", ON.
4. A career hero with an active ability falls (or is knocked out) in battle: buffed allies return
   to normal speed and damage (the parked teardown ran).
5. A cavalry battle: dead mounts still fade after the configured delay.
6. A won field battle with field commission eligible troops: promotion offers still appear.

## Hand-off report

Report: the branch; each commit's short SHA and subject; T0 (total, passed, skipped) and the
counts after Part A and Part B; the Step A1 output; which conflicts needed more than this plan
says; the settings numbers used in Step A4; whether the six binding tests passed or were
Inconclusive; the `/deep-review` outcome (or that the session could not run it); the in-game
checks owed above.

## Maintenance notes

- **WP107 (merges after this WP) must:** (1) add `TaleWorlds.MountAndBlade.Mission.TickAgentsAndTeamsImp`
  to PatchShield's `ExcludedTargetMethods` in the SAME merge that drops `Mission_TickAgentsAndTeams`
  from the Native2Managed allowlist. Until then a missing-API swallow on Patch91's target ends the
  agent tick before `tickCompleted = true`, exactly as today's bridge swallow does, so this WP adds
  no new freeze path in that window (K3 section 2.4, V04). (2) Consider protecting TAOM's own
  Harmony owner: `PatchShieldPolicy.CompiledProtectedOwnerPrefixes` holds `TAOM` (`:25`) but
  TAOM.dll patches as `com.taom.mod` (`SubModule.cs:161`), so the rescue path (`PatchShield.cs:335-391`)
  strips TAOM's prefixes, postfixes and transpilers from any method where it swallowed a
  missing-API exception (Patch35's postfix and Patch91b's prefix on `Mission.OnTick` included).
  Found by code reading this session; UNVERIFIED in game; new finding for the orchestrator.
  (3) When `RethrowStackPreserver` lands, restore the `harmony-patches.md` Finalizer sentence that
  Step A6 removed.
- **WP117** ports `6eb2e1b90`, which adds `MissionTickProfilerHooks` calls inside Patch91's prefix and
  finalizer: hand-merge onto this WP's file. Patch91b's binding tests read ORIGINAL IL
  (`PatchProcessor.ReadMethodBody`), so a profiler transpiler on `Mission.OnTick` does not break them.
- **WP113** reuses `ThreadStackCapture` for its main-thread heartbeat watchdog.
- **WP124**: when porting `efe326242`, keep `SpatialGrid.Remove` parked through `_pendingRemovals`
  and keep off-thread queries from rebuilding the grid (V05 section 4).
- **WP129**: #611 (`2b1d1ef96`) adds `ally.MountAgent?.UpdateAgentProperties();` inside
  `EndAbilitiesNow`'s ally loop; #613 (`7ede923be`) replaces the constructor's
  `ICareerPassiveService` with `ICareerAgentStatService`, moves the Ammo `OnAgentBuild` out and adds
  `ResetDiagnostics` to `OnEndMission`; `CareerPerkOffThreadTests` then takes the 1.5.x substitute back.
- **WP142**: sync `f75288ebf`'s four `harmony-il.md` lessons, its `adapters-taleworlds-api.md` edit and
  the `LESSONS-LEARNED.md` counts (+1 for this WP's entry); skip `bd1ac8292`'s `harmony-il.md` entry
  (this WP landed the 1.4.x version); add feature-map rows for MissionTickGuard (Patch91b) and the
  BattleLoadDiagnostics freeze sampler (Patch91).
- **WP143**: `CLAUDE.md` category count +2 (Patch91, Patch91b) and the Traps row "Agent slots recycle;
  Agent.Tick and callbacks are off-thread" (`f75288ebf` wording); `harmony-patches.md` `paths:` gains
  `Dependencies/Foundation/**` (`bd1ac8292`).
- **Deferred (new work, orchestrator decides):** FREEZE-THREADING-01 fix (c), a try/catch in each
  of TAOM's 10 unguarded `OnMissionTick` overrides (V04 section 2 lists them). With Patch91b a throw
  there costs one agent tick per throwing frame; a behaviour that throws every frame stops every
  agent tick. Route 2 (an exception on the agent-tick thread) stays a WP107 allowlist decision; a
  restore on `TickAgentsAndTeamsImp` was not taken (D17 restores only on `Mission.OnTick`).
- **What a reviewer should probe:** (1) the prefix reads the flag before anything else and cannot
  throw; (2) the `OnTick` finalizer is void, `Priority.First`, cannot throw, and restores in one
  cell only; (3) the launch mark is a finalizer, its class is patched before the `OnTick` class
  (Part B "Patch order"), and `Exit` clears it on every call; (4)
  `VerifyAndArm` runs after `TryPatchCategory`, requires both patches and the field, and never
  throws; (5) each #634 queue owner marks the main thread at the top of its `OnMissionTick`, drains
  there, and clears parked work at mission end; (6) `MountDespawnMissionBehavior.ForgetAgent` drains
  before forgetting on the main thread; (7) no `TaleWorlds` type in `TickHandshakeRecovery`.
- **Behaviour changes to expect:** off-thread removals and resets apply on the next mission tick
  (one frame later); off-thread reports are file-log WARNINGs, so E4-style `[ERROR]` lines read
  differently after this WP.
