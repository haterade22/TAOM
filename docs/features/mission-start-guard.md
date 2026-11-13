# Mission-start guard

## Overview

A battle (or any mission) whose start calls throw no longer loads again every frame for ever. One Harmony transpiler on
`Mission.AfterStart` wraps the six start calls into the behaviours, submodules and mission objects of the mission; a
call that throws is logged with the type and the module that own it, shown once on screen and cut short there. The
owner stays in the mission and gets its later calls, and the mission starts. The MCM toggle "Survive Mission Start
Failures" (CrashReport page, Master group, default on) turns it off again.

Adapted from yotthani's VanillaTuning `mission-start-guard` (MIT, (c) 2026 yotthani); TAOM wraps the call sites once
inside `Mission.AfterStart` instead of attaching a finalizer to each behaviour type. Provenance:
[provenance-register.md](../reference/provenance-register.md), adoption record
[adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md).

## Why This Exists

- **Vanilla behavior:** `Mission.AfterStart` (v1.5.4, decompile `Mission.cs:3815-3852`) runs, in order, every
  submodule's `OnBeforeMissionBehaviorInitialize`, every behaviour's `OnBehaviorInitialize`, every submodule's
  `OnMissionBehaviorInitialize`, every behaviour's `EarlyStart`, the spawn-path selector and deployment plan
  initialisers, every behaviour's `AfterStart`, every mission object's `AfterMissionStart`, the weather model, and only
  then sets `CurrentState = State.Continuing`. Its one caller in `TaleWorlds.MountAndBlade` is
  `MissionState.FinishMissionLoading` (`MissionState.cs:345`), which clears `_missionInitializing` first. A throw out of
  any of those calls therefore leaves the state at `Initializing`, and `MissionState.TickLoading` (`:221-233`) calls
  `LoadMission` and `Mission.Initialize` again on the next frame, then `AfterStart` again: the same throw, for ever.
  It is the only caller: the 2026-10-08 review read the IL of ten module assemblies (SandBox, StoryMode, NavalDLC,
  CustomBattle, Multiplayer and their view assemblies, CampaignSystem) and found no other call to `Mission.AfterStart`.
  The rerun also calls every submodule's `OnMissionBehaviorInitialize` on the same `Mission` again, so the submodules
  add their behaviours a second time.
- **TAOM requirement:** TAOM's own behaviours guard themselves (#699), but any vanilla or other-mod behaviour, submodule
  or mission object can still loop the game, and TAOM cannot guard what it does not own. TAOM's own
  `SubModule.OnMissionBehaviorInitialize` is one of the wrapped calls too, so a throw in TAOM's mission wiring is
  survived and cuts the rest of that wiring short: every TAOM behaviour registered after the throw is missing from that
  mission (the skeleton watch, the behaviour dump and the career perks among them; isolating each registration is issue
  draft 11). The BattleLoad loading window's closer is registered in a `finally` around all of that wiring (the first
  mission's patch application included), so it exists whatever the wiring did and ticks first, and the window
  still closes on the first tick (2026-10-08 review).
- **Without this feature:** the loading screen never ends, and the player can only kill the game. In TAOM the first
  escaped throw also reaches TAOM's crash capture (Patch37), which writes a report and a crash bundle and shows its
  "TAOM caught a crash" dialog; repeats of the same exception are logged only at occurrences 1, 2, 10, 100 and so on.

## Architecture

### Design Challenge

The six calls sit inside one engine method, run once per mission, and a wrapped call must behave exactly like the
engine's when nothing throws (and when the toggle is off, throw the same exception object with the same stack). The
guard covers only those six. The spawn-path selector, the deployment plan and the weather model, which the same method
also calls, stay unwrapped, and a mod can supply the last two (the plan is a `MissionDeploymentPlanningLogic`
behaviour, the weather an `ApplyWeatherEffectsModel`); a throw there still loops, and the finalizer says so in the log.
A throw from a behaviour's `OnMissionScreenPreLoad`, which `MissionState.LoadMission` calls before `AfterStart`, also
loops, and the finalizer never sees it.

### Solution Approach

`Mission_AfterStart_MissionStartGuard_Patch` (Patch103, category `Patch103_MissionStartGuard`, applied at
`ApplyPhase.GameInit`, which precedes every mission) has three parts:

| Part | What it does |
|---|---|
| Transpiler | Reuses `TickProfilerTranspiler.Rewrite` (the profiler patches' soft-failing call-site swap): each of six `callvirt` sites becomes a static `call` of a same-named helper on `MissionStartGuardCalls`, instance first and then the arguments. Nothing is inserted or removed, so labels and the `foreach` blocks stay as they were. If any site is not found exactly once, the whole method stays vanilla |
| Prefix (void) | Tells the service a mission start begins, passing the `Mission` as an opaque token; a new token resets the per-mission counts. It is also the guard's canary (below) |
| Finalizer (void) | Observe-only: logs an exception that still left `Mission.AfterStart`, at most three per process, then hands the service the live swap count and asks for the per-mission summary. Void, so Harmony keeps `rethrow` and the engine's stack. Priority `First` so it runs ahead of PatchShield's finalizer |

The six swapped calls, each present exactly once in the installed v1.5.4 body (`MissionStartGuardBindingTests` reads
the real IL):

| Engine call | Helper |
|---|---|
| `MBSubModuleBase.OnBeforeMissionBehaviorInitialize(Mission)` | `MissionStartGuardCalls.OnBeforeMissionBehaviorInitialize` |
| `MissionBehavior.OnBehaviorInitialize()` | `OnBehaviorInitialize` |
| `MBSubModuleBase.OnMissionBehaviorInitialize(Mission)` | `OnMissionBehaviorInitialize` |
| `MissionBehavior.EarlyStart()` | `EarlyStart` |
| `MissionBehavior.AfterStart()` | `AfterStart` |
| `MissionObject.AfterMissionStart()` | `AfterMissionStart` |

The six helpers and the swap table live in `Hooks/` with the patch (`MissionStartGuardCalls.cs`,
`MissionStartGuardSwaps.cs`). Each helper is the call inside a `try` with an exception filter:
`catch (Exception ex) when (Survives(ex)) { Note(...); }`. The filter runs before any frame unwinds, so when
`ShouldSurvive` says no (toggle off, `OutOfMemoryException`, no service yet) nothing is caught and the exception
reaches its old catcher with its original stack. `ShouldSurvive` is false for `OutOfMemoryException` always; every
other exception is survived while the toggle is on.

`MissionStartGuardService` (pure, behind `IMissionStartGuardAdapter` for the on-screen line; main thread only, so it
takes no lock) decides and reports:

- **Log:** one ERROR line per survived throw, tag `[MissionStartGuard]`, naming the call, the owning type, its
  assembly and the full `exception.ToString()`. The first 10 per mission are logged in full; later ones are counted
  and one WARNING says so. A null owner (a null behaviour that another module added) is reported as `<null>`.
- **On screen:** at most 3 messages per mission, one sentence, localized (`taom_mission_start_guard_notice`, variables
  `MODULE` (the assembly name), `CALL` and `EXCEPTION` (the type name)). None on a dedicated server.
- **VanillaTuning beside TAOM:** with VanillaTuning v0.1.0, TAOM shows its message whether or not yotthani's VanillaTuning is loaded. VanillaTuning
  puts a finalizer on each behaviour type's own start methods, which runs inside the call, so any exception that reaches
  TAOM's wrap is one VanillaTuning did not catch and did not show (2026-10-08 review; an earlier build suppressed TAOM's
  message when VanillaTuning was loaded, which left the player with none). VanillaTuning v0.1.1 stands aside on
  `TAOM.Features.MissionStartGuard.MissionStartGuardModule`, so TAOM's wrap is then the only guard (yotthani,
  `yotthani/bannerlord` `HoN/VanillaTuning/Core/TaomOverlap.cs`@784fef93 lines 21 and 27-33; code read, VanillaTuning
  v0.1.1, game 1.5.5).
- **Summary:** when a mission start caught anything, one WARNING at the end of `AfterStart` with the count and how many
  messages showed. Nothing is written for a mission that caught nothing.
- **Install line:** `[MissionStartGuard] ON: 6 call sites wrapped in Mission.AfterStart (toggle on)`, or a WARNING
  `OFF: wrapped N of 6 call sites ... so it stays vanilla` when the transpiler soft-failed. Written from
  `MissionStartGuardModule.OnPhase(GameInit)`, after the category applied.

A reload of the same mission (an escaped throw) keeps its budgets: the reset keys on the `Mission` object, held weakly,
not on every `AfterStart`.

**The canary.** PatchShield, from a process's first game start, can strip TAOM's prefix and transpiler on
`Mission.AfterStart` after it swallows a missing-method, missing-field or type-load exception there (it strips an
owner's prefixes, postfixes and transpilers, never its finalizers, and `com.taom.mod` is not a protected owner). The
prefix (priority `First`: ahead of every prefix at a lower priority) hands the finalizer "this call's prefix
ran" through Harmony's `__state`, one value per call; the finalizer checks, at every mission start, that flag and that
the live swap count is 6 (a later Harmony rebuild of the method reruns the transpiler, which can soft-fail). Either
failing writes one WARNING per process: `the guard's prefix did not run before this mission start ...` or `only N of 6
start calls are wrapped ...`. The install soft-fail warning shares the same once-per-process latch. A process-wide flag
was replaced on 2026-10-08: Harmony reruns every finalizer when a later one throws, and the rerun read the flag the
first run had cleared, a false warning that also spent the latch. A `[HarmonyCleanup]` method sets the swap count to 0
when the patch class fails to apply after the transpiler ran, so the install line then says OFF.

### Component Diagram

```
MissionStartGuardModule (GameInit: Patch103, install line)
        |
Mission_AfterStart_MissionStartGuard_Patch
  Transpiler -> MissionStartGuardSwaps (6 CallSwaps) -> TickProfilerTranspiler.Rewrite
  Prefix     -> IMissionStartGuardService.BeginMission (also the canary)
  Finalizer  -> ReportEscaped, EndMissionStart(live swap count)
        |
Hooks/MissionStartGuardCalls (6 helpers: try / catch when; 3 also check the list size)
        |
IMissionStartGuardService  (MissionStartGuardService)
   |            |               |
IModLogger  IMissionStartGuardAdapter   IMissionStartGuardSettingsProvider
            (DisplayMessage)              (CrashReportSettings.Instance)
```

### The list-size check (#1028)

`Mission.AfterStart` walks `MissionBehaviors` with `foreach` around `EarlyStart` and `AfterStart`, and `MissionObjects` around
`AfterMissionStart`. A start call that changes the list it is being walked from (an add, a remove, an indexer write, a sort,
an add paired with a remove) makes the engine's next step through that list throw "Collection was modified" from the
enumerator itself, outside every try the six helpers hold. The guard cannot survive that throw, and the engine loads the
mission again. Before its call each of the three helpers takes a `Watch`: the list, its size and a `List<T>` enumerator.
After the call it steps a copy of that enumerator, which throws exactly when the engine's own next step will. When it does,
`IMissionStartGuardService.ReportListChange` writes one ERROR line: the list, the call, the owner's type and assembly, both
sizes and, when the list grew, the newest item (at most five per mission). A healthy start never writes it, and it adds no
patch. The owner is the call during which the change happened: it may only have called code (a mod's listener, for
example) that made the change. It exists to name the culprit of #1028 (a stealth hideout raid that loops the loading
screen, crash bundle `taom_crash_20261111_145332_395a9270`); remove it when the issue closes. It does not see a change made
outside the three loops' calls, such as in `_battleSpawnPathSelector.Initialize()` or `_deploymentPlan.Initialize()`,
which run between the loops: if the line never appears and the exception recurs, look there next.

### Deferred adds (#1028)

The list-size check names the culprit; this part removes the loop for an add made through `Mission.AddMissionBehavior`.
Crash bundle `taom_crash_20261112_161559_ca0697d0` (an arena practice fight started from the Minas Tirith town menu; the
fight started from the arena master's dialogue worked) logged `MissionBehaviors changed during AfterStart of
SandBox.Missions.MissionLogics.CampaignMissionComponent ... size 99 to 100, newest item DualWield.DualWieldMissionBehavior`.
The engine then loaded the mission again, and the first tick threw a `NullReferenceException` in
`MissionMainAgentInteractionComponent.FocusTick`.

The chain, read from the installed v1.5.7 sources: `CampaignMissionComponent.AfterStart` raises
`CampaignEvents.AfterMissionStarted` (`CampaignMissionComponent.cs:140-144`). On the town-menu path
`ArenaMasterCampaignBehavior.AfterMissionStarted` (`ArenaMasterCampaignBehavior.cs:368-376`, flag set at `:385`) sets
`MissionMode.Battle` and calls `ArenaPracticeFightMissionController.StartPlayerPractice()`, which spawns the player and six
fighters inside the walk. DualWield's `Mission.SpawnAgent` postfix then adds its behaviour, because its static `IsActive` is
false and the mode is now a combat mode (`DualWieldSpawnPatches.cs:486-488`, `DualWieldSubModule.cs:179-181`; DualWield
v0.10.5, decompiled). The dialogue path calls `StartPlayerPractice` from `StartPlayerPracticeAfterConversationEnd`
(`ArenaMasterCampaignBehavior.cs:298-306`), after the mission has started, so nothing is added inside the walk. Every
listener of `AfterMissionStarted` runs inside the walk.

`Mission_AddMissionBehavior_MissionStartGuard_Patch` (same category) is a bool prefix on `Mission.AddMissionBehavior`. The
`EarlyStart` and `AfterStart` helpers open a window around their call (`MissionBehaviorDeferral.Open`, thread-static). They
close it right after a normal or survived return; a throw that is not survived leaves it open until Patch103's finalizer
resets it. Inside the window the prefix queues the behaviour and skips the engine's add. Patch103's `AfterStart` finalizer
applies the queue (`MissionStartGuardCalls.FlushDeferredAdds`) through the engine's own `AddMissionBehavior`, in add order,
with the window closed, on a normal end and after a throw alike. One INFO line per queued add (at most five per mission)
names the call, the owner and the type. The same toggle gates it.

The window never covers `OnBehaviorInitialize` (an index loop over the live count, so an add there is safe and the new
behaviour gets every start call) or `OnMissionBehaviorInitialize` (before the walk). That exclusion is load-bearing: vanilla
adds there on purpose (four `OnMissionStarted` listeners, see the registry), and so do TAOM's `AddTaomBehavior` and DualWield's
own `OnMissionBehaviorInitialize`.

What a queued behaviour lacks until the flush: it is not in `MissionBehaviors`, its `Mission` is null and `OnCreated` has not
run, and it misses `OnBehaviorInitialize`, `EarlyStart` and `AfterStart` of this start. `GetMissionBehavior` does not find it,
so DualWield's spawn postfix (`DualWieldSpawnPatches.cs:494`) skips registering the agents spawned in that gap: in the bundle
the player's practice agent and the six opponents. A respawn from `OnMissionTick` comes after the flush, and DualWield
re-registers a player whose kit can dual wield, so the loss is the first batch only. DualWield overrides no start call.

Limits, none seen with an installed mod:
- A mod that guards its add only with `GetMissionBehavior<T>() == null` queues one copy per spawn, and the flush adds all of
  them. DualWield is safe because its constructor sets `IsActive`.
- A foreign void prefix or any postfix on `AddMissionBehavior` runs twice, once for the skipped call and once at the flush.
- A remove, or any other direct change to `MissionBehaviors` inside the walk, still reloads the mission.
- PatchShield can shield this target from a process's first game start (TAOM's pass right after its GameInit batch, 2026-11-13). A missing-API throw from a behaviour's `OnCreated`
  would let it strip TAOM's prefix, and the deferral would end without a warning (the canary watches `AfterStart` only).

## Configuration

MCM, CrashReport page, group "Master", `Survive Mission Start Failures`, default on, no restart: it is read when an
exception arrives, through `MissionStartGuardSettingsProvider`, which falls back to on when MCM is not ready. The
page's master toggle "Enable Crash Capture" does not gate it: turning crash capture off must not bring the endless
reload back (2026-10-08 review). It is classified as instrumentation in `CoopSettingsRelevance` (crash containment, like "Enable Crash Capture"): a peer that
differs hangs on its own load and simulates nothing differently.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MissionStartGuard/MissionStartGuardModule.cs` | Registrations, Patch103 at GameInit, the install line |
| `Main/Features/MissionStartGuard/Hooks/Mission_AfterStart_MissionStartGuard_Patch.cs` | Prefix, transpiler, finalizer |
| `Main/Features/MissionStartGuard/Hooks/MissionStartGuardSwaps.cs` | The six `CallSwap`s and the last swap count |
| `Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs` | The six helpers |
| `Main/Features/MissionStartGuard/MissionStartGuardService.cs` | Decisions, budgets, log and message |
| `Main/Features/MissionStartGuard/MissionStartGuardLines.cs` | The log line wording |
| `Main/Features/MissionStartGuard/Models/StartCall.cs` | The six call names |
| `Main/Adapters/MissionStartGuardAdapter.cs` | The localized on-screen line |
| `Main/Features/CrashReport/CrashReportSettings.cs` | The MCM toggle |
| `Main/Features/MissionPerf/TickProfilerTranspiler.cs` | The shared soft-failing call-site swap |
| `Main/Features/MissionStartGuard/Hooks/Mission_AddMissionBehavior_MissionStartGuard_Patch.cs` | The prefix that queues an add made during the walk (#1028) |
| `Main/Features/MissionStartGuard/Hooks/MissionBehaviorDeferral.cs` | The thread-static window and queue |
| `Main/Features/MissionStartGuard/MissionStartGuardChecks.cs` | The two in-game `[Check]` ids and their pass or fail rules (#1028) |
| `tools/ingame_checks/1028.toml` | The manifest `tools/ingame_verdict.py` judges #1028 by |

## Dependencies

- `IMissionStartGuardSettingsProvider`: the toggle.
- `IMissionStartGuardAdapter`: `InformationManager.DisplayMessage`.
- `IDedicatedServerProvider` (CoopInterop): no message on a dedicated server.
- `IModLogger`: `taom_debug.log`.
- `IOutcomeLog`: the in-game `[Check]` lines of #1028 (once per mission).

## PatchShield and co-op

`Mission.AfterStart` is also patched by Patch43 (BattleLoadDiagnostics), so PatchShield attaches its
finalizer to it from a process's first game start (TAOM runs its own pass right after its GameInit batch, 2026-11-13).
PatchShield's `ShieldFinalizerVoid` has no priority attribute (400); the guard's void finalizer runs
first at 800 and only observes, so the shield still decides alone: it swallows a missing-method, missing-field or
type-load exception and rethrows any other with the stack preserved. Because PatchShield's finalizer returns a value,
Harmony ends the wrapper with `throw`, not `rethrow`, from that point on. A wrapped call catches a throw before it
reaches the shield, so with the toggle on the shield sees only what the wrap does not cover. `Mission.AfterStart`
stays under the shield (once per mission, main thread; decision D13). The shield's strip is what the canary watches
for (above). Census (2026-10-08): of the 242 installed module assemblies scanned (TAOM's own, `0Harmony`, `System.*`
and `Microsoft.*` skipped), only Coop's `GameInterface.dll` and `Missions.dll` both reference Harmony and mention
`AfterStart`, and both only override `MissionBehavior.AfterStart`; no installed module patches `Mission.AfterStart`, so no other transpiler there can lose its anchor to the six swapped
`callvirt`s. No bool prefix exists, so
`CoopVetoClassificationTests` has nothing to classify; for co-op the patch changes nothing unless a start call throws,
which would otherwise loop the mission.

## Tests

- `TAOM.Tests/Features/MissionStartGuard/MissionStartGuardServiceTests.cs`: 53 tests. Toggle off, out of memory,
  settings throwing; the log line content; the 10-line cap; the 3-message budget and its reset by a new mission, not
  by the same one; dedicated server; a failing logger or adapter; the summary; the escaped-throw cap; the install
  lines; the canary (a call whose prefix did not run warns once, a live count of 5 warns once, a normal start is
  silent, a second loss in the same process writes nothing more); the list-change line and its five-line cap, reset by a new
  mission and kept by the same one.
- `MissionStartGuardHookTests.cs`: 14 tests. The prefix hands its instance to the service and sets `__state` even with
  no service or a throwing one; the finalizer reports an escaped throw before the end and passes `__state` and the live
  swap count; `Cleanup` zeroes the count only after a failed apply; a throwing service escapes neither.
- `MissionStartGuardCanaryTests.cs`: 5 tests, through real Harmony on a dummy method: a later finalizer that throws makes
  Harmony rerun Patch103's finalizer, and nothing is warned; a prefix stripped after such a rerun is still reported once;
  a void cleanup taking the exception is called on a failed apply and Harmony still throws; Patch103's cleanup zeroes
  the swap count only on failure; a healthy call writes nothing.
- `MissionStartGuardCallsTests.cs`: 30 tests (needs the game assemblies). A normal call runs once; a throwing call is
  reported with the right call, type and assembly; a null owner is reported once as `<null>`; the guard saying no
  rethrows the same exception object with its
  stack; no service, a throwing filter, a throwing report; out of memory through the real service; and the IL of all
  six helpers (one exception filter, the engine call inside it). `MissionObject` cannot be allocated in a test host, so
  the sixth helper has no behavioural subject. Ten more tests give a behavior that adds, removes or overwrites a list item,
  or adds and removes one (both sizes and, when the list grew, the newest item are reported), a call that changes nothing (on a filled and an empty list), no
  current mission, a throwing report, the watches reading the mission's own lists, and the objects list's mapping; they
  allocate a `Mission` without its constructor.
- `MissionStartGuardBindingTests.cs`: 18 tests. The target and the six call signatures; each call exactly once in the
  installed `Mission.AfterStart`; the rewrite swaps six sites with no warning and is idempotent; the real patch class
  applied to the real method builds a replacement the JIT accepts; parameter names (`__state` included), void finalizer
  and priorities; the static void `[HarmonyCleanup]` method; the
  category and phase; PatchShield's reach; the MCM attribute; the provider default.
- Test counts: `grep -c "\[TestMethod\]" <file>`, 2026-11-12.
- `MissionBehaviorDeferralTests.cs`: 20 tests. The window and queue (open only for the mission and the call, closed after, a null mission opens none),
  the flush (order, window closed first, one failing add does not lose the rest), the toggle, and the Harmony prefix skipping
  or passing the add. `MissionStartGuardCallsTests.cs` adds the window around `EarlyStart` and `AfterStart` and never around
  `OnBehaviorInitialize` or the two submodule calls (`AfterMissionStart` cannot be allocated in a test host), the finalizer
  applying the queue to a real `Mission` on a normal end and after a throw, and the prefix dropping a stale queue. `MissionStartGuardBindingTests.cs` applies the
  real patch to the installed engine and checks an add inside and outside the window.
- `MissionStartGuardManifestTests.cs`: 2 tests. Both check ids are legal outcome ids, and `tools/ingame_checks/1028.toml` names them.
  One of them renders the lines through the real `OutcomeLog` and pins their exact text; `tools/tests/test_ingame_verdict.py`
  (`Issue1028Tests`) judges the same literals against the real manifest. The service tests cover the outcome lines: a pass when every queued add landed, a fail with both counts when one did not, nothing
  for an empty queue, one key per mission (kept when the same mission loads again), a fail for every list change even past the
  ERROR line's cap, and an outcome log that throws.
- `MissionStartGuardModuleTests.cs`: 5 tests. The service resolves from a real container, the helpers are handed it,
  and the install line is the ON line after a six-site swap and the warning after a soft fail.

## How to check it in game

CrashReport's QA toggle "Throw On Next Mission AfterStart" (`CrashReportDevTrigger.cs`) throws from a TAOM mission
behaviour's `AfterStart`, the #699 shape. Turn it on and start a battle.

1. Guard on (default): the battle starts, one message names TAOM's module and `AfterStart`, and
   `taom_debug.log` holds one `[MissionStartGuard] AfterStart threw in ...CrashReportDevTriggerMissionBehavior` ERROR
   line and one `survived 1 failed call(s)` WARNING.
2. Guard off (`Survive Mission Start Failures` unticked; `Enable Crash Capture` stays on, which the trigger needs): the
   throw escapes once and the finalizer logs one `Mission.AfterStart threw and was not survived` line. TAOM's crash
   capture then catches it (Patch37's finalizer on `Module.OnApplicationTick`): a `[CrashReport]` report in
   `taom_debug.log`, a crash bundle and the "TAOM caught a crash" dialog. Then the engine loads the mission again, and
   the second load runs every submodule's `OnMissionBehaviorInitialize` on the same mission a second time. The trigger
   resets itself before it throws, but #699's log showed later passes failing before TAOM's behaviours ran, so record,
   rather than assume, what the second load does: whether the battle starts, whether it loads again, and how many
   `not survived` lines appear (a battle with TAOM's behaviours added twice is not tested).

A `[MissionStartGuard] ... changed MissionBehaviors from N to M` ERROR line names the owner of a list change (#1028); a
healthy start never writes it.

**#1028, the practice fight.** Start a Practice Fight from the Minas Tirith town arena menu (not through the arena master's
dialogue) with DualWield on. The fight loads once and plays. The log holds one INFO line "was added to MissionBehaviors during
... the add is queued" and one `[Check] id=taom.mission-start-guard.deferred-add result=pass queued=N added=N`.
`python tools/ingame_verdict.py --issue 1028` judges the session: PASS on that line, FAIL on a `result=fail` line of the
deferred-add or list-change check or on a "threw and was not survived" line, NOT REACHED when the path was not played or the
build predates the fix. A mission start that loops anywhere in the session reads FAIL, whoever caused it (a list change in either list the engine
walks, or a "not survived" throw), so a loop the fix does not cover is not hidden. The load-once look stays a person's check.

Also check one Custom Battle after any change to the wrapped calls.

## Performance

Each helper adds one static call and a `try` with an exception filter per start call, once per mission. The three list
helpers also build a `Watch` (two reads of `Mission.Current`, a `Count` read and a struct copy, no allocation) and step an
enumerator copy afterwards. A call that changed nothing allocates nothing; a reported change allocates its log line.
`AfterMissionStart` runs per mission object (thousands in a siege scene), so that helper is the only one whose count is
large; its cost is not measured. Nothing runs per frame.

## Changelog

- 2026-10-08: added, adapted from yotthani's VanillaTuning `mission-start-guard`.
- 2026-11-12: a behaviour added while `EarlyStart` or `AfterStart` runs is queued until `AfterStart` ends (#1028).

## GitHub Issue

- **Issue:** #775 (draft item 1 in [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)); #1028 (the list-size
  check and the deferred add).
- **Status:** built and unit-tested. The in-game check of the deferred add was waived by the maintainer on 2026-11-12, so
  `tools/ingame_checks/1028.toml` reads NOT REACHED until a session plays the path (a Practice Fight from the town arena menu with
  DualWield on). The Codex pre-review was waived the same day.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/crash-report.md](./crash-report.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/engine/mission-and-missionbehavior-lifecycle.md](../reference/engine/mission-and-missionbehavior-lifecycle.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/harmony-patch-registry.md](../reference/harmony-patch-registry.md)

<!-- backlinks-end -->
