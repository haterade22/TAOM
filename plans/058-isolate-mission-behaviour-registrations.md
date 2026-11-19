# Plan 058: SubModule: isolate each hand-wired mission behaviour registration so one throw drops only its own behaviour

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**:
> `git diff --stat c6a82aa04..HEAD -- Main/SubModule.cs Main/Composition/FeatureModuleHooks.cs Main/Composition/MissionBehaviorBuild.cs TAOM.Tests/Composition/MissionBehaviorBuildTests.cs TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs TAOM.Tests/Composition/FeatureModulesTests.cs TAOM.Tests/Features/CoopInterop/Missions/CoopMissionRolePolicyTests.cs TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAddFilterPinTests.cs TAOM.Tests/Features/MissionPerf/MissionTickProfilerWiringTests.cs TAOM.Tests/Features/Animalia/AnimaliaWiringTests.cs TAOM.Tests/Features/DwarfAttacks/DwarfAttacksWiringTests.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs Main/Features/MissionStartGuard`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. `Main/SubModule.cs` is edited often: a change outside
> lines 2219 to 2401 (the excerpts below) is fine if every excerpt still matches, with line numbers
> shifted by the same amount. `Main/Features/MissionStartGuard` is read, not edited: a change there
> matters only if the `Report` signature or the `StartCall.OnMissionBehaviorInitialize` value quoted
> below changed.

## Status

- **Priority**: P2
- **Effort**: M
- **Risk**: MED (every TAOM mission behaviour passes through the edited method at every mission start)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `c6a82aa04`, 2026-11-19
- **Baseline at that commit**: build exit 0; dotnet `Failed: 3, Passed: 42943, Skipped: 9` (42955 total).
  Failing: `EveryKey_IsSeededInAllTwelveLanguageFiles`, `EveryLanguage_DeclaresARowForEveryEnglishKey`,
  `NoTranslatedString_MixesWritingSystems` (the three language-file tests: the translation XMLs belong to
  the project lead and lag the English keys; expected, not yours). Python suite: not touched by this plan.
- **Issue**: #785 (Harness and tools milestone)

## Why this matters

`SubModule.AddTaomMissionBehaviors` registers about thirty TAOM mission behaviours by hand, and each
call builds its behaviour in the call's own argument list (`AddTaomBehavior(new X(IoC.Resolve<...>()))`).
A throw while building one behaviour (a service that is not registered, a config that fails to load,
a constructor that resolves a missing service) leaves the method, so every registration after it is
skipped: the feature modules' behaviours (skeleton watch and others), the behaviour dump
(`MissionDiagnosticBehavior`), the CrashReport dev trigger and the career perks. Today, with the
"Survive Mission Start Failures" setting on (the default), the mission-start guard (Patch103) catches
that throw, writes a `[MissionStartGuard]` error line, shows the player a short message ("TAOM failed in
OnMissionBehaviorInitialize (...). The mission goes on, with that step cut short.") and the battle then
starts without every behaviour after the failed one; with the setting off, the throw makes the engine
load the same mission again. After this plan, each hand-wired behaviour is built inside its own factory.
A failed build writes one `[MissionWiring]` line naming the behaviour and the exception, is reported to
the mission-start guard exactly as the escaping throw was (same error line, same short message, same
per-mission summary), and the remaining registrations still run. No gameplay changes: the same
behaviours are added under the same conditions, in the same order.

## Current state

### Files and roles

- `Main/SubModule.cs`: the module entry point. `OnMissionBehaviorInitialize` (line 2175) wraps
  `AddTaomMissionBehaviors` (line 2222) in a try/finally; `AddTaomMissionBehaviors` holds the local
  function `AddTaomBehavior` (line 2266) and every hand-wired registration (lines 2288 to 2398).
  **Single-owner file**: this plan lists every edit to it exactly; make no other edit.
- `Main/Composition/FeatureModuleHooks.cs`: `AddMissionBehaviors(Mission, Action<MissionBehavior>)`
  (line 111) builds each feature module's behaviours and hands them to the delegate SubModule passes,
  inside `runner.Run("mission start", includeParked: false, failClosed: false, ...)` (lines 119 to 126).
- `Main/Composition/ModuleRunner.cs`: already isolates each feature module. `Run` (lines 63 to 91) wraps
  each module's action in `try { action(module); } catch (Exception ex)`, logs
  `[Module] {id} failed in {step}` and continues; it rethrows only for a save-owning module when
  `failClosed` is true, and the mission-start call passes `failClosed: false`. This is the precedent for
  the change.
- `Main/Features/MissionStartGuard/`: the mission-start guard. Read only; this plan calls its existing
  `IMissionStartGuardService.Report` and changes nothing in the folder.
- `Main/Composition/MissionBehaviorBuild.cs`: NEW, the engine-free isolation helper this plan adds.

### The wrapper, unchanged by this plan (`Main/SubModule.cs:2203-2216`)

```csharp
        try
        {
            AddTaomMissionBehaviors(mission);
        }
        finally
        {
            // One closer per mission: with the guard off, a throw makes the engine load the same Mission
            // again, and a second closer would stamp BattlePlayable twice (Codex pass 2).
            var loadDiagnostics = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
            if (loadDiagnostics != null
                && mission.GetMissionBehavior<Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior>() == null)
                mission.AddMissionBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(
                    loadDiagnostics, IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>()));
        }
```

The guard comment on the method (`Main/SubModule.cs:2219-2221`) stays as it is:

```csharp
    // TAOM's mission wiring: the first mission's patch application, then every TAOM mission behavior.
    // Runs inside OnMissionBehaviorInitialize's try, so the loading window's closer exists whatever
    // this throws.
```

### The local add function today (`Main/SubModule.cs:2263-2280`)

```csharp
        // Stamps the name BEFORE handing the behavior to the engine: the ctors here are trivial,
        // but AddMissionBehavior does real engine registration, so a fault there leaves the
        // culprit's name as the last line rather than forcing source archaeology on the order.
        void AddTaomBehavior(MissionBehavior behavior)
        {
            // Co-op (#967, C7): an Unsafe behavior is not added in a session. Agent ownership is unknown here (co-op
            // registers agents after they are built), so an AgentOwner behavior checks it live or relies on the
            // invariant its row in docs/features/coop-interop.md names.
            if (!Features.CoopInterop.Missions.CoopMissionRolePolicy.ShouldAdd(behavior.GetType(), coopSessionActive))
            {
                try { IoC.Resolve<IModLogger>()?.LogInfo($"[CoopMission] skipped {behavior.GetType().Name} (Unsafe in a co-op session)"); } catch { /* diagnostic only */ }
                return;
            }

            try { battleLoadDiagSvc?.LogTaomBehaviorAdded(behavior.GetType().Name); } catch { /* diagnostic only */ }
            mission.AddMissionBehavior(behavior);
            taomBehaviorCount++;
        }
```

### The registrations today (representative excerpts; every line is listed in Step 5)

`Main/SubModule.cs:2288-2289`:

```csharp
        if (Features.ShaderPrecompilation.ShaderPrecompileRunner.TryClaimMission(mission))
            AddTaomBehavior(new Features.ShaderPrecompilation.ShaderPrecompilePlayerAgentGuard(IoC.Resolve<IModLogger>()));
```

`Main/SubModule.cs:2296-2299` (lines 2296 to 2307 and 2309 to 2311 all have this one-line shape):

```csharp
        AddTaomBehavior(new BehaviorTreeMissionLogic());
        AddTaomBehavior(new AdvancedCombatBehavior());
        AddTaomBehavior(new WargMissionBehavior());
        AddTaomBehavior(new SpiderMissionBehavior());
```

`Main/SubModule.cs:2320-2324`, a registration that resolves services and reads config in its argument
list (a throw here skips everything below it):

```csharp
        AddTaomBehavior(new Features.Enlistment.Hooks.EnlistmentMeritMissionBehavior(
            IoC.Resolve<Features.Enlistment.IEnlistmentStateQuery>(),
            IoC.Resolve<Features.Enlistment.Content.IBattleMeritAccumulator>(),
            IoC.Resolve<Features.Enlistment.Content.IEnlistmentContentStore>(),
            IoC.Resolve<Features.Enlistment.Content.IEnlistmentContentConfigProvider>().GetConfig().MeritScoring));
```

`Main/SubModule.cs:2367-2369`, the feature-module hand-off:

```csharp
        // Feature modules' mission behaviors: after the feature behaviors above, before the kernel tail
        // below (MissionDiagnostic, the CrashReport dev trigger, CareerPerk).
        FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);
```

`Main/SubModule.cs:2376-2380`, three locals resolved OUTSIDE any registration, then a condition:

```csharp
        var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
        var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
        var diagLogger = IoC.Resolve<IModLogger>();
        if (diagSvc != null && raceMgr != null && diagLogger != null)
            AddTaomBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
```

`Main/SubModule.cs:2385-2401`, the dev trigger, the career perks (a local plus a condition) and the
closing bracket stamp:

```csharp
        AddTaomBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());

        var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
        if (careerAbilityService != null && Campaign.Current != null)
        {
            AddTaomBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
                IoC.Resolve<ICareerDataService>(),
                careerAbilityService,
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
                IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>(),
                IoC.Resolve<Features.CareerSystem.ICareerConfigProvider>(),
                IoC.Resolve<IModLogger>()));
        }

        try { battleLoadDiagSvc?.LogTaomBehaviorsDone(taomBehaviorCount); } catch { /* diagnostic only */ }
    }
```

`git grep -n "AddTaomBehavior(new" -- Main` lists exactly 30 lines, all in `Main/SubModule.cs`: 2289,
2296 to 2307 (12 lines), 2309, 2310, 2311, 2314, 2320, 2329, 2335, 2340, 2343, 2349, 2352, 2356, 2359,
2362, 2380, 2385, 2390.

### Why the services can throw

`IoC.Resolve<T>()` is `return _container.Resolve<T>();` (`Main/IoC.cs:280-283`), DryIoc's resolve, which
throws for a service that is not registered (the test message at
`TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs:29` relies on this: "Without it the
SubModule's IoC.Resolve<IDeadMountDespawnService>() throws at mission start."). Several behaviour
constructors resolve services themselves (`TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs:40-41`:
"SiegeDismountMissionBehavior's ctor IoC.Resolve<ISiegeDismountService>() throws the first time a mission
spawns").

### The mission-start guard today: what a throw out of this method produces

`Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs:35-39` wraps SubModule's
`OnMissionBehaviorInitialize`; `Survives` asks the service, which returns the setting
`SurviveMissionStartFailures` (default `true`, `Main/Features/CrashReport/CrashReportSettings.cs:43`):

```csharp
    internal static void OnMissionBehaviorInitialize(MBSubModuleBase module, Mission mission)
    {
        try { module.OnMissionBehaviorInitialize(mission); }
        catch (Exception ex) when (Survives(ex)) { Note(StartCall.OnMissionBehaviorInitialize, module, ex); }
    }
```

`Note` (`MissionStartGuardCalls.cs:187-198`) calls the service's `Report` with the owner's full type
name and assembly name:

```csharp
            var type = owner?.GetType();
            Service?.Report(call, type?.FullName ?? type?.Name ?? "<null>", type == null ? "unknown" : AssemblyNameOf(type), ex);
```

`MissionStartGuardService.Report` (`Main/Features/MissionStartGuard/MissionStartGuardService.cs:83-92`)
writes an ERROR line for each of the first ten per mission and shows a notice through
`ShowNoticeIfAllowed` (`:154-162`: not on a dedicated server, at most `MaxNoticesPerMission = 3` per
mission, `:31`); `EndMissionStart` later writes the per-mission summary when anything was caught:

```csharp
    public void Report(StartCall call, string ownerType, string ownerAssembly, Exception exception)
    {
        _caught++;
        if (_caught <= MaxFullLogsPerMission)
            Safe(() => _logger.LogError(MissionStartGuardLines.BuildCaughtLine(call, ownerType, ownerAssembly, exception)));
        else if (_caught == MaxFullLogsPerMission + 1)
            Safe(() => _logger.LogWarning(MissionStartGuardLines.BuildSuppressedWarning(MaxFullLogsPerMission)));

        ShowNoticeIfAllowed(call, ownerAssembly, exception);
    }
```

The notice is `{=taom_mission_start_guard_notice}{MODULE} failed in {CALL} ({EXCEPTION}). The mission goes
on, with that step cut short.` (`Main/Adapters/MissionStartGuardAdapter.cs:17`), with MODULE the owner
assembly (`TAOM`). `IMissionStartGuardService` is registered as a singleton by
`Main/Features/MissionStartGuard/MissionStartGuardModule.cs:32`, and the same instance is handed to
`MissionStartGuardCalls` (`:36`). `StartCall` is `public enum` in
`Main/Features/MissionStartGuard/Models/StartCall.cs` with the member `OnMissionBehaviorInitialize`.
`Report` does not read the setting; only `ShouldSurvive` does.

If isolation caught the build failure and only logged, the player would lose that notice. This plan
keeps it: the isolated build reports each failure to the same `Report`, with the failed behaviour as the
owner.

### Engine fact: what `Mission.AddMissionBehavior` does (v1.5.7)

From `pwsh tools/taom-src.ps1 path Mission` (decompiled `TaleWorlds.MountAndBlade.Mission.cs:4680-4694`
for the installed v1.5.7):

```csharp
	public void AddMissionBehavior(MissionBehavior missionBehavior)
	{
		MissionBehaviors.Add(missionBehavior);
		missionBehavior.Mission = this;
		switch (missionBehavior.BehaviorType)
		{
		case MissionBehaviorType.Logic:
			MissionLogics.Add(missionBehavior as MissionLogic);
			break;
		case MissionBehaviorType.Other:
			_otherMissionBehaviors.Add(missionBehavior);
			break;
		}
		missionBehavior.OnCreated();
	}
```

The behaviour is in the mission's list BEFORE `OnCreated` runs. So the isolation covers only the BUILD
(the factory). `mission.AddMissionBehavior` stays outside it: a throw from `OnCreated` leaves a
half-added behaviour that ticks, and that throw must still reach the mission-start guard, as today.
This is a design decision of this plan; do not widen the catch.

### Design (the shape you will build)

1. A new engine-free helper `TAOM.Composition.MissionBehaviorBuild.Isolated<T>(Func<T?> create,
   Action<string> logError, Action<string, string, Exception> reportFailure) where T : class` returns
   what `create` returns, or null when `create` throws. On a throw it logs one line naming
   `typeof(T).Name` and the exception, then calls `reportFailure` with `typeof(T).FullName` (or `Name`),
   the type's assembly name and the exception. A throw from either callback is swallowed, and one
   failing callback does not stop the other.
2. In `AddTaomMissionBehaviors`, the existing local function is renamed `AddBuiltTaomBehavior` with its
   body unchanged (co-op filter, `[BattleLoad]` stamp, engine add, count). The feature-module hand-off
   passes `AddBuiltTaomBehavior`, because `ModuleRunner` already isolates each module and
   `FeatureModuleHooks` builds a module's behaviours before handing them over
   (`Main/Composition/FeatureModuleHooks.cs:119-126`).
3. A new generic local function `AddTaomBehavior<T>(System.Func<T?> create) where T : MissionBehavior`
   builds through `MissionBehaviorBuild.Isolated`, with the TAOM logger as `logError` and
   `IoC.Resolve<IMissionStartGuardService>()?.Report(StartCall.OnMissionBehaviorInitialize, ...)` as
   `reportFailure`, and hands a non-null result to `AddBuiltTaomBehavior`. C# local functions cannot be
   overloaded, which is why the instance form gets a new name.
4. Every hand-wired registration becomes `AddTaomBehavior(() => new X(...))`. The two registrations with
   shared locals and an `if` (MissionDiagnostic, CareerPerk) move their resolves and their condition,
   verbatim, inside a block-bodied factory that returns null when the condition is false. A null
   result adds nothing, logs nothing and reports nothing, so the conditions behave exactly as today.
5. The generic parameter is inferred from the lambda's return type, so `typeof(T).Name` names the
   behaviour even when its constructor throws (a `Func<MissionBehavior>` could not).
6. The isolation does not read the "Survive Mission Start Failures" setting: a failed TAOM build is
   always contained and reported. With the setting off, a build failure therefore no longer makes the
   engine load the mission again, and the guard's notice still shows (`Report` does not read the
   setting). The setting keeps governing every throw the guard itself wraps, including a throw from
   `mission.AddMissionBehavior`.

What stays OUTSIDE any factory, deliberately: the preamble before the first registration
(`_missionTimePatchesApplied` block, the dedicated-server check at 2246, `battleLoadDiagSvc` at 2256,
`coopSessionActive` at 2261) and the `TryClaimMission` condition at 2288 (pure in-memory TAOM state; it
claims the mission, so it keeps its exact position). A throw there still skips the whole method and
reaches the guard, as today; isolating the preamble is out of scope.

### Tests that pin today's call text (they change in Step 6)

Found with `git grep -n "AddTaomBehavior" -- TAOM.Tests` and a scan of every test string literal
contained in `Main/SubModule.cs:2219-2401`:

| File:line | Pinned text today |
|---|---|
| `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionRolePolicyTests.cs:104` | regex `AddTaomBehavior\(\s*new\s+([\w.]+)\(`, count 30 at :106 |
| `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionRolePolicyTests.cs:117` | regex `AddTaomBehavior\(\s*\w+\s*\)`, count 0 |
| `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAddFilterPinTests.cs:19-20` | `void AddTaomBehavior(MissionBehavior behavior)` |
| `TAOM.Tests/Composition/FeatureModulesTests.cs:293` | `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);` |
| `TAOM.Tests/Features/MissionPerf/MissionTickProfilerWiringTests.cs:18` | `AddTaomBehavior(new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(` |
| `TAOM.Tests/Features/MissionPerf/MissionTickProfilerWiringTests.cs:101-102` | the heartbeat add and the feature-module hand-off |
| `TAOM.Tests/Features/Animalia/AnimaliaWiringTests.cs:28` | `AddTaomBehavior(new Features.Animalia.AnimaliaMissionBehavior());` |
| `TAOM.Tests/Features/DwarfAttacks/DwarfAttacksWiringTests.cs:62` | `AddTaomBehavior(new Features.DwarfAttacks.DwarfAttacksMissionBehavior());` |
| `TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs:13, 51` | `AddTaomBehavior(new SiegeDismountMissionBehavior());` (comment and assertion) |
| `TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs:11` | `AddTaomBehavior(new SiegePropDiagnostics.Hooks.SiegePropDiagnosticsMissionBehavior());`, in a comment only (its assertion at :34 pins `SiegePropDiagnosticsMissionBehavior());`, which still matches) |

Pins that keep passing unchanged (their text survives the edit): `BattleLoadCloserWiringTests` (anchors
`private void AddTaomMissionBehaviors(Mission mission)`, `ShaderPrecompileRunner.TryClaimMission(mission)`,
the closer), `SubModuleMissionServerGuardTests`, `SubModuleMissionViewWiringTests`,
`MountDespawnWiringTests` (pins `new Features.MountDespawn.Hooks.MountDespawnMissionBehavior(`),
`SignatureStrikesBindingTests` (pins `new Features.SignatureStrikes.Hooks.SignatureStrikesMissionLogic(`).
`FeatureModulesTests.AssertNotHandWiredToo` matches `new X(` or `Resolve<X>(` only for behaviour and
model types a module declares; `IMissionStartGuardService` is a service, so the new resolve does not
trip it.

### Blast radius

`python tools/graphify_taom.py status --brief` at planning time: `graphify code graph: MISSING`, so the
graph was not used. `git grep -n -w "AddTaomMissionBehaviors" -- Main TAOM.Tests Client`: the call at
`Main/SubModule.cs:2205`, the declaration at :2222, and the tests named above
(`BattleLoadCloserWiringTests`, `SubModuleMissionServerGuardTests` doc comment, a comment in
`StoryMissionWiringPinTests`). `FeatureModuleHooks.AddMissionBehaviors` keeps its signature; its other
caller is `TAOM.Tests/Composition/FeatureModuleHooksTests.cs:98` (the four-argument overload, untouched).
`MissionBehaviorBuild` is new and has no callers yet. `IMissionStartGuardService` gains a caller and no
change.

### Conventions that bind this change

- **ADR-002 thin entry points**: entry points under 150 lines that delegate. `SubModule` derives from
  `MBSubModuleBase`, which `EntryPointLineCountGateTests` does not count (its entry points are
  `CampaignBehaviorBase`, `MissionBehavior`, `MissionLogic`, `MissionView` subclasses, `Taom*Model` and
  Harmony patch classes), so the gate stays green; it must still pass in the full run.
- **ADR-007 adapters**: services never take sealed TaleWorlds types. `MissionBehaviorBuild` is
  generic over `class` and names no engine type, so it needs no adapter and runs in the unit-test host.
- **ADR-008 testability**: every decision path has a behaviour test. The decision (build, catch, log,
  report, return null) lives in `MissionBehaviorBuild` with behaviour tests; SubModule gets source pins
  for the wiring only (`.claude/rules/tests.md`: "A source-text test pins wiring only ... The decision
  itself goes in a pure type with behaviour tests").
- **ADR-013 server-safe core**: core names no client-only assembly. Nothing added here does.
  `SubModuleServerGuardTests.SubModuleMethods_ReferenceNoClientAssemblyDirectly` walks SubModule's
  methods and its lambdas' closure classes; the moved constructor calls name the same core types as
  today, and `IMissionStartGuardService` and `StartCall` are core types in `Main`.
- **`csharp-architecture.md` "Outcome lines"**: an empty catch calls `Outcomes.Swallowed` or carries
  `// swallow-ok: <reason>`. The new catch logs and reports; its two inner catches around the callbacks
  carry `// swallow-ok:`.
- **Banned constructs (ADR-003, 004, 005)**: no `#region`, no `[Obsolete]`, no `#if`.
- **Lessons**: `docs/reviews/lessons/build-tooling-workflow.md` "A wiring gate must exercise the
  wiring; a source-text scan only pins the idiom" (hence the behaviour tests beside the pins) and "A
  ratchet entry must pin the measured VALUE, and a gate that inspected nothing must fail" (hence each pin
  asserts it found at least one call).
- `Main/SubModule.cs` is CRLF. Edit it with the Edit tool only; never `sed -i`.

## Step 0

None: no protected file changes.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests |
| One test class | the same, plus `--filter "FullyQualifiedName~<ClassName>"` | the named tests run, with the exact count given in the step; a filter matching nothing proves nothing |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python tools/validate_moduledata.py` | not needed: no data file changes |

Both flags go on build AND test, and prefix dotnet with the TEMP and TMP your dispatch rules give
(`TEMP="<tmp>" TMP="<tmp>" dotnet ...`). Never `./build.ps1`: it deploys into the game install.
`python tools/graphify_taom.py tests "<Type>"` prints the filter for the tests a change reaches; do not
run its `refresh`.

## Scope

**In scope** (the only files you modify or create, 13 paths):
- `Main/SubModule.cs` (single-owner: only the exact edits in Step 5)
- `Main/Composition/MissionBehaviorBuild.cs` (new)
- `Main/Composition/FeatureModuleHooks.cs` (one doc-comment line, Step 5)
- `TAOM.Tests/Composition/MissionBehaviorBuildTests.cs` (new)
- `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs` (new)
- `TAOM.Tests/Composition/FeatureModulesTests.cs`
- `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionRolePolicyTests.cs`
- `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAddFilterPinTests.cs`
- `TAOM.Tests/Features/MissionPerf/MissionTickProfilerWiringTests.cs`
- `TAOM.Tests/Features/Animalia/AnimaliaWiringTests.cs`
- `TAOM.Tests/Features/DwarfAttacks/DwarfAttacksWiringTests.cs`
- `TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs`
- `TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs` (comment only)

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`: single-owner; no registration is needed.
- `Main/Features/MissionStartGuard/**` and `Main/Adapters/MissionStartGuardAdapter.cs`: called as they
  are; no new method, no new text.
- `OnMissionBehaviorInitialize` (lines 2175 to 2217): the try/finally and the closer stay exactly as
  they are; `BattleLoadCloserWiringTests` pins them.
- The preamble of `AddTaomMissionBehaviors` (lines 2224 to 2261) and the `TryClaimMission` condition.
- `Main/Features/CoopInterop/Missions/CoopMissionRoles.cs` (its comments say "added outside
  AddTaomBehavior", which stays true) and every other `Main/Features/**` comment that names
  `AddTaomBehavior`.
- `docs/**`, `tools/**`, `.claude/**`, `*.csproj`, `*.props`, `CHANGELOG.md`, `plans/README.md`.
- Any player-facing text: the new line is a log line only, and the guard's notice is existing,
  already localized text.
- The gates themselves. Never turn a gate green by editing it beyond the exact pin updates in Step 6:
  no deleted or `[Ignore]`d test, no lowered count (the role table count stays 30), no loosened
  assertion. STOP and report instead.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version value=...>` in `Main/_Module/SubModule.xml` when you commit (it was `v2.0.40` at planning;
  a hook refuses any other). Use: `fix(mission): <version> - one failed behavior no longer drops the rest`
  (68 characters with `v2.0.40`).
- The body is the changelog entry, wrapped at 72: what changed and why, for a reader of the release
  note. Never edit `CHANGELOG.md`. No AI attribution trailer. Add
  `Constraint: the isolation does not read the Survive Mission Start Failures setting`,
  `Not-tested: in game, no mission started with a failing build` and
  `Refs: #785`. Write the message to a file with the Write tool and run `git commit -F "<file>"`.
- Stage the 13 in-scope paths explicitly by name (never `-A`, `-u` or `.`).

## Steps

### Step 1: record the base

Run the drift check above. Create the TEMP folder your dispatch rules give. Run the build and the full
suite before any edit, saving the build output to `build-base.txt` in your scratch folder (Step 5
compares warnings against it):

```
TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId= > "<scratch>/build-base.txt" 2>&1
TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
```

Write the build exit code, the test totals line and the failing test names into your report.

**Verify**: build exit 0; totals `Failed: 3, Passed: 42943, Skipped: 9`, and the three failures are
the language-file tests named in Status. A different set is explained in your report before you go on
(another commit since `c6a82aa04` may have changed it); a failure in any class this plan touches is a
STOP.

### Step 2: RED, the wiring pins

Create `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs` with exactly this content
(the `Body` helper is copied from `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs`,
whose mutation-test pattern this file follows):

```csharp
using System;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Composition;

/// <summary>
/// #785: every hand-wired registration in <c>SubModule.AddTaomMissionBehaviors</c> builds its behavior inside a
/// factory that <c>AddTaomBehavior</c> runs through <c>MissionBehaviorBuild.Isolated</c>, so a throw while building
/// one behavior drops only that behavior and is reported to the mission-start guard. Wiring pins only: the isolation
/// itself is MissionBehaviorBuildTests.
/// </summary>
[TestClass]
public class SubModuleMissionBehaviorIsolationTests
{
    private const string Registrations = "private void AddTaomMissionBehaviors(Mission mission)";
    private const string FactoryAdd = "void AddTaomBehavior<T>(System.Func<T?> create) where T : MissionBehavior";
    private static readonly Regex Call = new(@"\bAddTaomBehavior\s*\(");

    private static string Source() => RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

    private static string Body(string src, string signature)
    {
        var start = src.IndexOf(signature, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, signature + " moved or was renamed.");
        var next = Regex.Match(src.Substring(start + signature.Length), @"\n    (?:public|private|protected|internal) ");
        Assert.IsTrue(next.Success, "The member after " + signature + " moved.");
        return src.Substring(start, signature.Length + next.Index);
    }

    // The index of the bracket that closes the one at openAt. Comments are already blanked, and no string literal
    // in the pinned code holds a bracket.
    private static int Close(string text, int openAt, char open, char close)
    {
        var depth = 0;
        for (var i = openAt; i < text.Length; i++)
        {
            if (text[i] == open) depth++;
            else if (text[i] == close && --depth == 0) return i;
        }

        Assert.Fail("Unbalanced " + open + close + " after index " + openAt + ".");
        return -1;
    }

    private static void AssertEveryCallPassesAFactory(string src)
    {
        var body = Body(src, Registrations);
        var calls = Call.Matches(body);
        Assert.IsTrue(calls.Count > 0, "No AddTaomBehavior call found: the pin inspected nothing.");
        foreach (Match call in calls)
            Assert.IsTrue(Regex.IsMatch(body.Substring(call.Index + call.Length), @"^\s*\(\)\s*=>"),
                "Every AddTaomBehavior call passes a factory lambda; found: "
                + body.Substring(call.Index, Math.Min(90, body.Length - call.Index)));
    }

    private static void AssertNothingIsBuiltOutsideAFactory(string src)
    {
        var body = Body(src, Registrations);
        var calls = Call.Matches(body);
        Assert.IsTrue(calls.Count > 0, "No AddTaomBehavior call found: the pin inspected nothing.");
        var end = body.IndexOf("LogTaomBehaviorsDone", StringComparison.Ordinal);
        Assert.IsTrue(end > calls[calls.Count - 1].Index, "LogTaomBehaviorsDone moved before the last registration.");

        // Everything from the first registration to the closing stamp, minus the registrations themselves.
        var residue = new StringBuilder();
        var at = calls[0].Index;
        foreach (Match call in calls)
        {
            if (call.Index < at) continue;
            residue.Append(body, at, call.Index - at);
            at = Close(body, call.Index + call.Length - 1, '(', ')') + 1;
        }

        residue.Append(body, at, end - at);
        var text = residue.ToString();
        Assert.IsFalse(text.Contains("IoC.Resolve<"), "A service is resolved between registrations, outside a factory: " + text);
        Assert.IsFalse(Regex.IsMatch(text, @"\bnew\s"), "Something is built between registrations, outside a factory: " + text);
    }

    private static void AssertTheBuildIsIsolatedAndTheAddIsNot(string src)
    {
        var body = Body(src, Registrations);
        var start = body.IndexOf(FactoryAdd, StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "The factory form of AddTaomBehavior is gone: " + FactoryAdd);
        var open = body.IndexOf('{', start);
        var local = body.Substring(open, Close(body, open, '{', '}') - open + 1);

        var isolated = local.IndexOf("MissionBehaviorBuild.Isolated(create,", StringComparison.Ordinal);
        Assert.IsTrue(isolated >= 0, "AddTaomBehavior builds through MissionBehaviorBuild.Isolated.");
        var isolatedEnd = Close(local, local.IndexOf('(', isolated), '(', ')');

        var report = local.IndexOf("IMissionStartGuardService>()?.Report(", StringComparison.Ordinal);
        Assert.IsTrue(report > isolated && report < isolatedEnd,
            "A failed build is reported to the mission-start guard from inside the isolated call, so the player still sees its notice.");
        Assert.IsTrue(local.IndexOf("StartCall.OnMissionBehaviorInitialize", StringComparison.Ordinal) > report,
            "The guard report names the OnMissionBehaviorInitialize start call, as the guard's own catch does.");

        var add = local.IndexOf("AddBuiltTaomBehavior(behavior)", StringComparison.Ordinal);
        Assert.IsTrue(add > isolatedEnd,
            "The engine add runs after the isolated build, outside it: a throw from Mission.AddMissionBehavior must still reach the mission-start guard.");
    }

    [TestMethod]
    public void AddTaomMissionBehaviors_EveryAddTaomBehaviorCall_PassesAFactory()
    {
        AssertEveryCallPassesAFactory(Source());
    }

    [TestMethod]
    public void AddTaomMissionBehaviors_BetweenRegistrations_NothingIsResolvedOrBuilt()
    {
        AssertNothingIsBuiltOutsideAFactory(Source());
    }

    [TestMethod]
    public void AddTaomBehavior_BuildsInIsolation_ReportsToTheGuard_AndAddsToTheMissionOutsideIt()
    {
        AssertTheBuildIsIsolatedAndTheAddIsNot(Source());
    }

    [TestMethod]
    public void Pin_FailsWhenACallPassesABuiltBehavior()
    {
        var src = Source();
        var mutated = src.Replace("AddTaomBehavior(() => new BehaviorTreeMissionLogic());", "AddTaomBehavior(new BehaviorTreeMissionLogic());");
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the registration's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertEveryCallPassesAFactory(mutated));
    }

    [TestMethod]
    public void Pin_FailsWhenAServiceIsResolvedBetweenRegistrations()
    {
        var src = Source();
        const string anchor = "AddTaomBehavior(() => new AdvancedCombatBehavior());";
        var mutated = src.Replace(anchor, "var early = IoC.Resolve<IModLogger>(); " + anchor);
        Assert.AreNotEqual(src, mutated, "The mutation found nothing to change: the registration's shape changed.");
        Assert.ThrowsException<AssertFailedException>(() => AssertNothingIsBuiltOutsideAFactory(mutated));
    }
}
```

This file deliberately holds the old call text once, in `Pin_FailsWhenACallPassesABuiltBehavior`'s
mutation string; the Done criteria's grep excludes it.

Run it:

```
TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~SubModuleMissionBehaviorIsolationTests"
```

**Verify**: the test project compiles, and the run reports `Failed: 5, Passed: 0` for this class. The
failures must be: the factory pin naming a call that is not a factory (on the base, the first match is
`AddTaomBehavior(MissionBehavior behavior)`), the residue pin naming `IoC.Resolve<`, the isolation pin
saying the factory form is gone, and both `Pin_Fails...` tests failing on "The mutation found nothing to
change". Quote the totals line in your report. (These five were simulated against the base source at
planning time with the same regexes and fail as described.)

### Step 3: RED, the behaviour tests for the isolation helper

Create `TAOM.Tests/Composition/MissionBehaviorBuildTests.cs` (pattern: `TAOM.Tests/Composition/ModuleRunnerTests.cs`,
MSTest, `MethodName_StateUnderTest_ExpectedBehavior` names):

```csharp
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;

namespace TAOM.Tests.Composition;

/// <summary>
/// #785: the isolation SubModule.AddTaomBehavior uses for each hand-wired mission behavior. A throw while building
/// one is logged with the behavior's type name, reported to the mission-start guard with the type's full name and
/// assembly, and yields null, so the caller adds nothing and goes on to the next.
/// </summary>
[TestClass]
public class MissionBehaviorBuildTests
{
    private sealed class Probe
    {
    }

    private readonly List<string> _log = new();
    private readonly List<(string OwnerType, string OwnerAssembly, Exception Thrown)> _reports = new();

    private void Report(string ownerType, string ownerAssembly, Exception thrown) =>
        _reports.Add((ownerType, ownerAssembly, thrown));

    [TestMethod]
    public void Isolated_FactoryReturnsAnInstance_ReturnsItAndLogsAndReportsNothing()
    {
        var probe = new Probe();

        var built = MissionBehaviorBuild.Isolated(() => probe, _log.Add, Report);

        Assert.AreSame(probe, built);
        Assert.AreEqual(0, _log.Count);
        Assert.AreEqual(0, _reports.Count);
    }

    [TestMethod]
    public void Isolated_FactoryThrows_ReturnsNullLogsOnceAndReportsTheTypeAndTheException()
    {
        var thrown = new InvalidOperationException("no IDeadMountDespawnService registered");

        var built = MissionBehaviorBuild.Isolated<Probe>(() => throw thrown, _log.Add, Report);

        Assert.IsNull(built);
        Assert.AreEqual(1, _log.Count);
        StringAssert.Contains(_log[0], "[MissionWiring] Probe was not added");
        StringAssert.Contains(_log[0], "no IDeadMountDespawnService registered");
        Assert.AreEqual(1, _reports.Count);
        Assert.AreEqual(typeof(Probe).FullName, _reports[0].OwnerType);
        Assert.AreEqual(typeof(Probe).Assembly.GetName().Name, _reports[0].OwnerAssembly);
        Assert.AreSame(thrown, _reports[0].Thrown);
    }

    [TestMethod]
    public void Isolated_FactoryReturnsNull_ReturnsNullAndLogsAndReportsNothing()
    {
        var built = MissionBehaviorBuild.Isolated<Probe>(() => null, _log.Add, Report);

        Assert.IsNull(built);
        Assert.AreEqual(0, _log.Count);
        Assert.AreEqual(0, _reports.Count);
    }

    [TestMethod]
    public void Isolated_LogThrows_StillReportsToTheGuard()
    {
        var built = MissionBehaviorBuild.Isolated<Probe>(
            () => throw new InvalidOperationException("build"), _ => throw new InvalidOperationException("log"), Report);

        Assert.IsNull(built);
        Assert.AreEqual(1, _reports.Count);
    }

    [TestMethod]
    public void Isolated_FactoryLogAndReportAllThrow_ReturnsNullWithoutThrowing()
    {
        var built = MissionBehaviorBuild.Isolated<Probe>(
            () => throw new InvalidOperationException("build"),
            _ => throw new InvalidOperationException("log"),
            (_, _, _) => throw new InvalidOperationException("report"));

        Assert.IsNull(built);
    }
}
```

Build the project that holds the test:

```
TEMP="<tmp>" TMP="<tmp>" dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
```

**Verify**: the build fails, and every error is a "name does not exist" error (CS0103) naming
`MissionBehaviorBuild` in `MissionBehaviorBuildTests.cs`. Any other error is a typo of yours: fix it
and rebuild until only those errors remain. Quote the error count in your report.

### Step 4: GREEN, the isolation helper

Create `Main/Composition/MissionBehaviorBuild.cs` with exactly this content:

```csharp
using System;

namespace TAOM.Composition;

/// <summary>
/// Builds one hand-wired mission behavior in isolation (#785). SubModule.AddTaomMissionBehaviors registers each TAOM
/// mission behavior through a factory: a throw while building one (a service that is not registered, a config that
/// fails to load) is logged with the behavior's type name and reported to the mission-start guard, and that behavior
/// is left out, so the registrations after it still run. The caller adds the result to the mission outside this call:
/// Mission.AddMissionBehavior lists the behavior before it calls OnCreated, so a throw there must still reach the
/// mission-start guard. Engine-free and unit-tested (MissionBehaviorBuildTests).
/// </summary>
internal static class MissionBehaviorBuild
{
    /// <summary>
    /// What <paramref name="create"/> returns, or null when it throws. A throw is logged through
    /// <paramref name="logError"/> and handed to <paramref name="reportFailure"/> as (owner type, owner assembly,
    /// exception), the arguments IMissionStartGuardService.Report takes after the start call.
    /// </summary>
    internal static T? Isolated<T>(Func<T?> create, Action<string> logError, Action<string, string, Exception> reportFailure)
        where T : class
    {
        try
        {
            return create();
        }
        catch (Exception ex)
        {
            var type = typeof(T);
            try
            {
                logError($"[MissionWiring] {type.Name} was not added: building it threw, and the other TAOM mission behaviors are still added. {ex}");
            }
            catch
            {
                // swallow-ok: the log line must never be what breaks the mission wiring.
            }

            try
            {
                reportFailure(type.FullName ?? type.Name, type.Assembly.GetName().Name ?? "unknown", ex);
            }
            catch
            {
                // swallow-ok: the guard's report must never be what breaks the mission wiring.
            }

            return null;
        }
    }
}
```

Run:

```
TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MissionBehaviorBuildTests"
```

**Verify**: `Passed: 5`, `Failed: 0` for this filter. Then break the helper on purpose once (change the
one `return null;` in the outer catch to `throw;`), run the same filter and confirm `Failed: 3, Passed: 2`
(the three failures are `Isolated_FactoryThrows_ReturnsNullLogsOnceAndReportsTheTypeAndTheException`,
`Isolated_LogThrows_StillReportsToTheGuard` and
`Isolated_FactoryLogAndReportAllThrow_ReturnsNullWithoutThrowing`), then restore the line with the Edit
tool and re-run to `Passed: 5`. Quote all three totals lines.

### Step 5: the SubModule edit (single-owner file: these edits and no others)

Make the edits in this order with the Edit tool, in `Main/SubModule.cs`.

**5a. Rename the instance add.** Replace the line (2266)

```csharp
        void AddTaomBehavior(MissionBehavior behavior)
```

with

```csharp
        void AddBuiltTaomBehavior(MissionBehavior behavior)
```

Its body (lines 2267 to 2280) and the three comment lines above it stay byte for byte.

**5b. Add the factory form.** Immediately after the closing brace of `AddBuiltTaomBehavior` (the `}` on
line 2280, followed by a blank line and the `// Deployment fallback guard` comment), insert, with one
blank line before and one after:

```csharp
        // #785: every hand-wired registration builds its behavior in a factory, so a throw while building one
        // (a service that is not registered, a config that fails to load) drops only that behavior and the rest
        // are still added. The throw is logged and reported to the mission-start guard, which writes its error
        // line and shows the player its short notice. A factory that returns null adds nothing. The engine add
        // stays outside the isolation: Mission.AddMissionBehavior lists the behavior before it calls OnCreated,
        // so a throw there must still reach the mission-start guard.
        void AddTaomBehavior<T>(System.Func<T?> create) where T : MissionBehavior
        {
            var behavior = MissionBehaviorBuild.Isolated(create,
                message => IoC.Resolve<IModLogger>()?.LogError(message),
                (ownerType, ownerAssembly, ex) => IoC.Resolve<Features.MissionStartGuard.IMissionStartGuardService>()?.Report(
                    Features.MissionStartGuard.Models.StartCall.OnMissionBehaviorInitialize, ownerType, ownerAssembly, ex));
            if (behavior != null)
                AddBuiltTaomBehavior(behavior);
        }
```

`System.Func` is written in full because `Main/SubModule.cs` has no `using System;` (it already writes
`catch (System.Exception ex)`); do not add a using. `MissionBehaviorBuild` resolves through the existing
`using TAOM.Composition;` (line 22), and `Features.MissionStartGuard...` resolves from the file's
`TAOM` namespace like every other `Features.` reference in it. If the guard module is not registered,
`IoC.Resolve` throws inside the callback and the helper swallows it; the `[MissionWiring]` line is still
written.

**5c. The feature-module hand-off.** Replace

```csharp
        // Feature modules' mission behaviors: after the feature behaviors above, before the kernel tail
        // below (MissionDiagnostic, the CrashReport dev trigger, CareerPerk).
        FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);
```

with

```csharp
        // Feature modules' mission behaviors: after the feature behaviors above, before the kernel tail
        // below (MissionDiagnostic, the CrashReport dev trigger, CareerPerk). ModuleRunner isolates each
        // module, so these arrive already built.
        FeatureModuleHooks.AddMissionBehaviors(mission, AddBuiltTaomBehavior);
```

**5d. MissionDiagnostic.** Replace exactly these five lines (2376 to 2380; the five comment lines above
them stay):

```csharp
        var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
        var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
        var diagLogger = IoC.Resolve<IModLogger>();
        if (diagSvc != null && raceMgr != null && diagLogger != null)
            AddTaomBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
```

with

```csharp
        AddTaomBehavior(() =>
        {
            var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
            var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
            var diagLogger = IoC.Resolve<IModLogger>();
            return diagSvc != null && raceMgr != null && diagLogger != null
                ? new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger)
                : null;
        });
```

**5e. CareerPerk.** Replace exactly these lines (2387 to 2398):

```csharp
        var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
        if (careerAbilityService != null && Campaign.Current != null)
        {
            AddTaomBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
                IoC.Resolve<ICareerDataService>(),
                careerAbilityService,
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
                IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>(),
                IoC.Resolve<Features.CareerSystem.ICareerConfigProvider>(),
                IoC.Resolve<IModLogger>()));
        }
```

with

```csharp
        AddTaomBehavior(() =>
        {
            var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
            if (careerAbilityService != null && Campaign.Current != null)
            {
                return new Features.CareerSystem.CareerPerkMissionBehavior(
                    IoC.Resolve<ICareerDataService>(),
                    careerAbilityService,
                    IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
                    IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
                    IoC.Resolve<Features.CareerSystem.Abilities.ICareerAgentStatService>(),
                    IoC.Resolve<Features.CareerSystem.ICareerConfigProvider>(),
                    IoC.Resolve<IModLogger>());
            }

            return null;
        });
```

**5f. The 28 one-expression registrations.** With the Edit tool and `replace_all: true` on
`Main/SubModule.cs`, replace `AddTaomBehavior(new ` (with its trailing space) by
`AddTaomBehavior(() => new `. After 5d and 5e this string occurs only on the 28 remaining registration
lines (the original lines 2289, 2296 to 2307, 2309 to 2311, 2314, 2320, 2329, 2335, 2340, 2343, 2349,
2352, 2356, 2359, 2362, 2385). Their argument lists, the comments between them and their order do not
change. The `if (...TryClaimMission(mission))` line above the first one stays.

**5g. FeatureModuleHooks doc comment.** In `Main/Composition/FeatureModuleHooks.cs` line 110, replace

```csharp
    /// <summary>OnMissionBehaviorInitialize: hands each behavior to SubModule's AddTaomBehavior, which stamps [BattleLoad].</summary>
```

with

```csharp
    /// <summary>OnMissionBehaviorInitialize: hands each behavior to SubModule's AddBuiltTaomBehavior, which stamps [BattleLoad].</summary>
```

**Verify** (each must hold before you go on):
- `git grep -c "AddTaomBehavior(new" -- Main/SubModule.cs` prints nothing (no match) and exits 1.
- `git grep -c "AddTaomBehavior(() =>" -- Main/SubModule.cs` prints `Main/SubModule.cs:30`.
- `git grep -c "AddTaomBehavior(() => new " -- Main/SubModule.cs` prints `Main/SubModule.cs:28`.
- `git grep -n "AddBuiltTaomBehavior" -- Main` lists exactly four lines: the declaration and the call in
  `AddTaomBehavior<T>` and the hand-off in `Main/SubModule.cs`, and the doc comment in
  `Main/Composition/FeatureModuleHooks.cs`.
- `TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
  with 0 errors. Its output holds no warning located in `MissionBehaviorBuild.cs`, and no warning
  located in a `SubModule.cs` line inside `AddTaomMissionBehaviors` that `build-base.txt` from Step 1
  does not also show (a warning the base build already had is not yours; list it in your report). A
  nullable warning (CS86xx, for example CS8631 or CS8619) at 5b, 5d or 5e is a STOP.
- The Step 2 filter now reports `Passed: 5, Failed: 0`.
- Run the full suite. It must report exactly 3 + 7 failures: the three base language-file tests and
  these seven pins of the old call text, which Step 6 updates:
  `Table_CoversEveryBehaviorTheModAdds_NoMissingNoStale`,
  `AddTaomBehavior_FiltersByRoleBeforeAddingAnything_WithTheSessionReadOnce`,
  `Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock`,
  `SubModule_AddsTheProfilerBehavior_RightAfterTheHeartbeat`,
  `SubModule_AddsTheAnimaliaMissionBehavior`, `Feature_IsRegisteredAndAddedToMissions`
  (DwarfAttacks), `MainSubModule_AddsSiegeDismountMissionBehaviorOnMissionInit`. Quote the totals line.
  Any other new failure is a STOP.

### Step 6: update the seven pins to the new call shape (and two comments)

Each change below swaps the old call text for the new one and nothing else. No count, order or message
meaning changes; the role table count stays 30.

1. `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionRolePolicyTests.cs`, in
   `Table_CoversEveryBehaviorTheModAdds_NoMissingNoStale`:
   - Line 104: replace the pattern `@"AddTaomBehavior\(\s*new\s+([\w.]+)\("` with
     `@"AddTaomBehavior\(\s*\(\)\s*=>(?:(?!AddTaomBehavior)[^}])*?\bnew\s+([\w.]+)\("`. It captures the
     first `new` type inside each factory, without crossing a `}` or the next registration. Simulated at
     planning time on the post-Step-5 source: 30 matches, and the captured names are the same 30, in the
     same order, as the old pattern finds on the base.
   - Line 106: keep `30`; change the message to
     `"AddTaomBehavior(() => new ...) call sites in SubModule.cs changed; recount and update the table"`.
   - After the existing `variableAdds` assertion (lines 117 to 119), add these two statements, indented
     8 spaces to match line 117:

     ```csharp
     int builtAdds = Regex.Matches(subModule, @"AddBuiltTaomBehavior\(\s*\w+\s*\)").Count;
     Assert.AreEqual(1, builtAdds,
         "AddBuiltTaomBehavior(variable) belongs only inside AddTaomBehavior: a behavior handed to it anywhere else escapes this table's count");
     ```

     The one expected match is `AddBuiltTaomBehavior(behavior)` inside the factory form (the declaration
     `AddBuiltTaomBehavior(MissionBehavior behavior)` has two words in its parentheses and does not
     match). This keeps the variable-add guard the rename would otherwise bypass.
2. `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionAddFilterPinTests.cs` lines 19 and 20: the
   anchor becomes `"void AddBuiltTaomBehavior(MissionBehavior behavior)"` and the message
   `"AddBuiltTaomBehavior is gone; update this pin."`. Keep the 900-character window: the body it must
   contain ends 876 characters after the renamed anchor (measured at planning on the comment-stripped,
   LF-normalised source; 5b's insertion comes after that body, so it does not move it).
3. `TAOM.Tests/Composition/FeatureModulesTests.cs` line 293: `"FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);"`
   becomes `"FeatureModuleHooks.AddMissionBehaviors(mission, AddBuiltTaomBehavior);"`.
4. `TAOM.Tests/Features/MissionPerf/MissionTickProfilerWiringTests.cs`:
   - line 18: `BehaviorAdd = "AddTaomBehavior(() => new Features.MissionPerf.Hooks.MissionTickProfilerBehavior(";`
   - line 101: `"AddTaomBehavior(() => new Features.MissionPerf.Hooks.MissionPerfHeartbeatBehavior("`
   - line 102: `"FeatureModuleHooks.AddMissionBehaviors(mission, AddBuiltTaomBehavior);"`
5. `TAOM.Tests/Features/Animalia/AnimaliaWiringTests.cs` line 28:
   `"AddTaomBehavior(() => new Features.Animalia.AnimaliaMissionBehavior());"`.
6. `TAOM.Tests/Features/DwarfAttacks/DwarfAttacksWiringTests.cs` line 62:
   `"AddTaomBehavior(() => new Features.DwarfAttacks.DwarfAttacksMissionBehavior());"`.
7. `TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs`: line 51's literal becomes
   `"AddTaomBehavior(() => new SiegeDismountMissionBehavior());"`, and the comment on line 13 becomes
   `//     AddTaomBehavior(() => new SiegeDismountMissionBehavior());`.
8. `TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs` line 11 (comment only):
   `//     AddTaomBehavior(() => new SiegePropDiagnostics.Hooks.SiegePropDiagnosticsMissionBehavior());`.

Run the eight pin methods:

```
TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Table_CoversEveryBehaviorTheModAdds_NoMissingNoStale|FullyQualifiedName~AddTaomBehavior_FiltersByRoleBeforeAddingAnything_WithTheSessionReadOnce|FullyQualifiedName~Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock|FullyQualifiedName~SubModule_AddsTheProfilerBehavior_RightAfterTheHeartbeat|FullyQualifiedName~SubModule_AddsTheAnimaliaMissionBehavior|FullyQualifiedName~Feature_IsRegisteredAndAddedToMissions|FullyQualifiedName~MainSubModule_AddsSiegeDismountMissionBehaviorOnMissionInit|FullyQualifiedName~MainSubModule_AddsSiegePropDiagnosticsMissionBehaviorOnMissionInit"
```

**Verify**: `Passed: 8, Failed: 0` (each name occurs once in the suite, checked at planning). Then
prove the role pin still guards: temporarily change one registration in `Main/SubModule.cs` from
`AddTaomBehavior(() => new WargMissionBehavior());` to
`var warg = new WargMissionBehavior(); AddTaomBehavior(() => warg);` (a behaviour built outside its
factory, which the new regex must not count), run the
`Table_CoversEveryBehaviorTheModAdds_NoMissingNoStale` filter alone and confirm it fails on the count
(29, simulated at planning), then restore the line with the Edit tool and confirm it passes. Quote both
totals lines. (Do not commit the temporary change.)

### Step 7: full verification and commit

1. `TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`: exit 0.
2. `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
   `Failed: 3, Passed: 42953, Skipped: 9` (the base's 42943 plus the 10 new tests: 5 in
   `MissionBehaviorBuildTests`, 5 in `SubModuleMissionBehaviorIsolationTests`), with the same three
   language-file failures as Step 1. If Step 1 recorded different base totals, the expectation is that
   base plus 10 passed with the same failure set.
3. `TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests|FullyQualifiedName~BattleLoadCloserWiringTests|FullyQualifiedName~SubModuleServerGuardTests|FullyQualifiedName~SubModuleMissionServerGuardTests|FullyQualifiedName~SubModuleMissionViewWiringTests"`:
   `Failed: 0` and `Passed:` greater than 0 (a run that prints "No test matches" proves nothing and is a
   STOP). Quote the totals line; some of these classes hold `RequiresGame` tests, so a nonzero
   `Skipped:` is expected.
4. `python tools/lint_docs.py --fail-on-drift`: exit 0 (this plan changes no markdown; a failure that
   the base also has is reported, not fixed).
5. `git status --porcelain`: the 13 in-scope paths (three of them `??` new files before staging:
   `Main/Composition/MissionBehaviorBuild.cs`, `TAOM.Tests/Composition/MissionBehaviorBuildTests.cs`,
   `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs`) and no other path outside
   `plans/` (the orchestrator may keep untracked files there; leave them alone).
6. Write the commit message to a file in your scratch folder with the Write tool, stage the 13 paths by
   name, and commit with `git commit -F "<file>"`. Suggested body (rewrap at 72, check it against the
   final code):

   ```
   A TAOM mission behavior that failed to build at mission start (a
   service that is not registered, a config that fails to load) used to
   stop SubModule's hand-wired registrations at that point. The
   mission-start guard caught the throw, logged it and showed a short
   message, and the battle then started without every behavior after
   the failed one: the feature modules' behaviors, the behavior dump and
   the career perks among them.

   Each hand-wired registration now builds its behavior in its own
   factory. A failed build writes one [MissionWiring] line to the TAOM
   log naming the behavior, is reported to the mission-start guard as
   before (the same error line and short message, now naming the failed
   behavior in the log), and the remaining behaviors are still added.
   With the guard's setting off, such a failure no longer makes the
   game load the mission again. The conditions, order and engine add
   are unchanged; a throw from the engine add itself still reaches the
   mission-start guard.

   Constraint: the isolation does not read the Survive Mission Start Failures setting
   Refs: #785
   Not-tested: in game, no mission started with a failing build
   ```

**Verify**: `git log -1 --format=%s` shows the subject from "Git workflow" with the current version,
and `git show --stat HEAD` lists exactly the 13 in-scope paths.

## Test plan

- New `TAOM.Tests/Composition/MissionBehaviorBuildTests.cs` (5 tests, behaviour): an instance passes
  through with no log and no report; a throwing factory yields null, one log line naming the type and
  the exception, and one report with the type's full name, its assembly and the same exception (the
  regression this plan fixes, at the decision level, and the guard notice it keeps); a null factory
  yields null with no log and no report (the MissionDiagnostic and CareerPerk conditions); a throwing
  log callback does not stop the report; throwing log and report callbacks do not escape.
- New `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs` (5 tests, wiring pins): every
  `AddTaomBehavior(` call in `AddTaomMissionBehaviors` passes a `() =>` factory; nothing is resolved or
  built between registrations; the factory form builds through `MissionBehaviorBuild.Isolated`, reports
  to `IMissionStartGuardService.Report` with `StartCall.OnMissionBehaviorInitialize` from inside that
  call, and adds outside it; two mutation tests prove the first two pins fail on the defect they guard.
  Pattern: `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs`.
- Existing pins updated in Step 6 keep their guarantees: the role table still sees all 30 hand-wired
  types and now also rejects a variable handed to `AddBuiltTaomBehavior` elsewhere.
- Not testable without the game, for the `Not-tested:` trailer: a live mission start with a failing
  build (the guard's on-screen notice, the engine add, the `[BattleLoad]` stamps and the co-op filter
  running on the remaining behaviours). The add path is unchanged by this plan apart from the
  function's name; the guard's `Report` is unchanged and has its own tests
  (`MissionStartGuardServiceTests`).
- No engine type is used by a new test, so no `[TestCategory("RequiresGame")]` and no RefAsm step.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0
- [ ] The full suite reports the base totals plus 10 passed (`Failed: 3, Passed: 42953, Skipped: 9` from
      the planned-at base), the failures being the same three language-file tests
- [ ] The filtered runs of Steps 2, 4 and 6 report `Passed: 5`, `Passed: 5` and `Passed: 8`
- [ ] After the commit, `git grep -n "AddTaomBehavior(new" -- Main TAOM.Tests ':!TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs'`
      returns nothing, and `git grep -c "AddTaomBehavior(new" -- TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs`
      prints `TAOM.Tests/Composition/SubModuleMissionBehaviorIsolationTests.cs:1` (the mutation string in
      `Pin_FailsWhenACallPassesABuiltBehavior`, which must keep the old shape)
- [ ] `git grep -n "AddMissionBehaviors(mission, AddTaomBehavior)" -- Main TAOM.Tests` returns nothing
- [ ] `git grep -c "AddTaomBehavior(() =>" -- Main/SubModule.cs` prints `Main/SubModule.cs:30`
- [ ] `git grep -n "MissionBehaviorBuild.Isolated(" -- Main` lists exactly one line, in `Main/SubModule.cs`
- [ ] `git grep -n "StartCall.OnMissionBehaviorInitialize" -- Main/SubModule.cs` lists exactly one line
- [ ] `git status --porcelain` lists no path outside `plans/` after the commit, and `git show --stat HEAD`
      lists only the 13 in-scope paths
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (the comment in 5b against `MissionBehaviorBuild` and `MissionStartGuardService.Report`,
      the comment in 5c against `ModuleRunner.Run` and `FeatureModuleHooks.AddMissionBehaviors`, the
      counts 30 and 28, the 876 window measurement)

Mentions of the old call shape outside `Main/` and `TAOM.Tests/` are left for the orchestrator (see
below); `git grep -n "AddTaomBehavior(new"` over the whole repo will still list them, and it lists the
one deliberate line in `SubModuleMissionBehaviorIsolationTests.cs`.

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts (beyond a uniform line shift),
  including `IMissionStartGuardService.Report(StartCall, string, string, Exception)` and the
  `StartCall.OnMissionBehaviorInitialize` member.
- `git grep -c "AddTaomBehavior(new" -- Main/SubModule.cs` is not 30 before Step 5, or any
  `AddTaomBehavior(new` sits outside `AddTaomMissionBehaviors`: someone added a registration elsewhere
  and the plan's counts are wrong.
- The build fails at a block-bodied factory (5d or 5e) with a type-inference error (CS0411), shows a
  nullable warning (CS86xx) at 5b, 5d or 5e, or fails with any error you cannot fix without changing
  the prescribed shape. Do not add explicit type arguments (`AddTaomBehavior<X>(...)`): the role-table
  regex and the factory pin rely on `AddTaomBehavior(() =>`.
- Step 5's full run shows a failing test other than the base three and the seven named pins.
- A fix would need you to wrap `mission.AddMissionBehavior` in the catch, to move the `TryClaimMission`
  condition or the preamble, or to edit `OnMissionBehaviorInitialize`.
- A fix would need `Main/IoC.cs`, `Main/Features/MissionStartGuard/**`, a csproj, a props file,
  `tools/`, `.claude/` or `docs/`.
- `EntryPointLineCountGateTests`, `SubModuleServerGuardTests` or `BattleLoadCloserWiringTests` fails.
- A step's verification fails twice after a reasonable fix.
- The assumption "C# infers `T` from the lambda's return type for `AddTaomBehavior<T>(System.Func<T?>)`,
  including the two block lambdas that return a behaviour or `null`" is false.

## Orchestrator steps (not the executor's)

- Issue: #785. After merge, close it with `triage-needs-ingame` if the maintainer wants a live mission
  start checked (the behaviour set in a normal battle is unchanged; the failure path is not reproducible
  on demand).
- Review: `/deep-review` tier 1 (mission start) before merge. Probe the spots in "Maintenance notes".
- `/localize`: none (log line only; the guard notice reuses the existing
  `taom_mission_start_guard_notice` key).
- Docs (outside the executor's paths), one pass after merge:
  - `docs/features/mission-start-guard.md` lines 30 to 35 say a throw in TAOM's mission wiring "cuts the
    rest of that wiring short" and that isolating each registration is still to do. Replace that with:
    a failed build of one TAOM behaviour is contained by `MissionBehaviorBuild` and reported to the
    guard (same error line and notice, owner = the behaviour's type), the rest are still added, and
    this containment does not read the "Survive Mission Start Failures" setting; a throw from the
    preamble or from `mission.AddMissionBehavior` still reaches the guard as before.
  - `docs/features/battle-load-diagnostics.md` lines 41 and 597 call `AddTaomBehavior` the helper that
    stamps each behaviour; the stamp now lives in `AddBuiltTaomBehavior`, and a failed build logs
    `[MissionWiring]`. Add that sentence.
  - `docs/features/dread-aura.md:347`, `docs/features/banner-bearers.md:93` and
    `docs/reference/engine/campaign-to-mission-bridge.md:77` quote `AddTaomBehavior(new ...)`; update
    the quoted shape to `AddTaomBehavior(() => new ...)`. Historical review records, the changelog
    archive and older plans keep their text.
- No feature doc or feature-map row: this is a fix to the wiring, not a feature.

## After merge: the maintainer's actions

None beyond a normal pull. No hook, setting or save-format change.

## Maintenance notes

- A new hand-wired mission behaviour is written `AddTaomBehavior(() => new X(...));`. A behaviour that
  needs a condition puts the condition and its resolves inside a block-bodied factory that returns null,
  as MissionDiagnostic and CareerPerk do. `SubModuleMissionBehaviorIsolationTests` fails on any other
  shape, and `CoopMissionRolePolicyTests` still needs the new type in `CoopMissionRoles` and the count
  raised.
- Review probes: (1) the catch must cover only the build, never `mission.AddMissionBehavior`
  (`OnCreated` runs after the list add, quoted above); (2) the order of the 30 registrations is
  unchanged, because tick order is reverse registration order (comment at `Main/SubModule.cs:2291-2295`);
  (3) the co-op filter still runs on every behaviour, built or module-supplied, through
  `AddBuiltTaomBehavior`; (4) each condition moved into a factory is the original expression,
  verbatim; (5) the factory closures capture `mission` only through `AddBuiltTaomBehavior`, and none
  outlives the method; (6) a failed build still reaches `IMissionStartGuardService.Report` with
  `StartCall.OnMissionBehaviorInitialize`, so the player notice, the error line and the per-mission
  summary survive; the owner it names is now the failed behaviour's type, not `TAOM.SubModule`;
  (7) the isolation ignores the "Survive Mission Start Failures" setting by design (Design item 6): with
  the setting off, a build failure is contained and the guard's notice still shows, where before the
  engine reloaded the mission.
- Deferred: the preamble (`IsDedicatedServer`, `battleLoadDiagSvc`, `coopSessionActive` resolves) and
  `TryClaimMission` stay unisolated; a throw there still skips every registration and reaches the guard,
  as today. Isolating them is a possible follow-up; it is not needed to close #785.
