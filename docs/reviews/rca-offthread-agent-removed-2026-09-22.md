# RCA: battle freeze with engine callbacks off the main thread (#634)

**Date:** 2026-09-22 · **Issue:** #634 (follows #592, #595) · **Status:** cause NOT proven; every engine-callback writer hardened, freeze instrumented

## Symptom

A player froze mid-battle on Bannerlord v1.4.8 running TAOM v2.0.29 (patreon build `c5b84fb4`, the tip of
`bannerlord-1.4.5`). The battle was a large field battle: Erebor, 51 men, defending Grymmclúd, with 950
attackers and 1,408 defenders involved and one warg carrying a behaviour tree. It became playable at
02:26:41. The last game-thread activity was at 02:33:11 to 02:34:48. After that, the timer-driven
`[MemSample]` kept logging for five minutes with the heap flat at 624 MB and private MB rising exactly
1 MB per 30 s: no exception and no crash report. The process was alive and the game thread was not.

The only other signal was the #595 tripwire, `MissionThreadGuard`, reporting six engine callbacks off the
main thread (main 36; threads 41/54/56/57): `OnAgentShootMissile`, `OnAgentDismount`,
`OnAgentAlarmedStateChanged`, `OnObjectUsed`, `OnObjectStoppedBeingUsed` and `OnAgentRemoved`. The ids
prove "not the main thread"; they cannot say whether each was the asynchronous agent-tick thread or a
pool worker.

## Engine frame

These lines are in `_shipping_build_v1.4.8/TaleWorlds.MountAndBlade.cs`, and v1.5.3 is identical:

- `MissionState.TickMissionAux` calls the native `Mission.Tick`, then `Mission.OnTick` (v1.5.3
  `MissionState.cs:212-219`).
- `Mission.OnTick` (55988-56018) sets `tickCompleted = false`, runs every `OnMissionTick`, and LAST calls
  `TickAgentsAndTeamsAsync`.
- `TickAgentsAndTeamsImp` (55849-55866) runs `Agent.Tick` for every agent and `Team.Tick`, sets
  `tickCompleted = true`, then runs `AfterAsyncTickTick`.
- The next frame's `[MBCallback] OnPreTick` (55776) calls `WaitTickCompletion`
  (`while (!tickCompleted) Thread.Sleep(1)`) before any `OnPreMissionTick`.

An agent tick that spins or blocks therefore freezes the game with exactly this shape: the main thread
asleep in a 1 ms loop, the heap flat, nothing thrown.

`Mission.OnAgentRemoved` runs every behaviour's `OnAgentRemoved` (55253) and then
`affectedAgent.OnRemove()` (55272), which calls every `AgentComponent.OnAgentRemoved`. Vanilla's own
handlers mutate `_allAgents`, `Team._activeAgents` and `_mountsWithoutRiders` with no lock, so if they
are safe, native must serialise these callbacks with the agent tick rather than the main thread. Nothing
managed proves which, and two threads raising callbacks at once is still an open candidate (see Cause).

## What TAOM had wrong

The committed thread map (`.claude/rules/harmony-patches.md`), `csharp-architecture.md`, the #592 RCA and
two docs derived from it all listed `OnAgentRemoved`/`OnAgentDeleted` as main-thread callbacks. No managed
code proves that, and this log falsifies it. The #595 audit reviewed every `OnAgentRemoved` handler under
that assumption. The census below is the final one, after the deep review widened it:

| Writer | State | Reader on the main thread | Fix |
|---|---|---|---|
| `BehaviorTreeAgentComponent.OnAgentRemoved` | `BehaviorTreeMissionLogic._scheduled` (List), `trees` (Dictionary) | `OnMissionTick`, every listener lookup | parked via `RunOnMissionThread` |
| `MountDespawnMissionBehavior.OnAgentRemoved` / `OnAgentDeleted` | `_pending`, `DeadMountDespawnService._deathTimes` (Dictionary) | `OnMissionTick` sweep | parked; forget drains first |
| `FieldCommissionMeritService.RegisterKill` | `_battleKills` (Dictionary) | a concurrent removal | lock |
| `CareerPerkMissionBehavior.OnAgentRemoved` (player falls) | `_activeContexts` (List), other agents' `UpdateAgentProperties` | `OnMissionTick` walks the list | parked |
| `CareerPerkMissionBehavior.OnScoreHit` | attribution notice, `InformationManager.DisplayMessage` | UI | parked |
| `AdvancedCombatBehavior.OnAgentDeleted` -> `SpatialGrid.Remove` | grid cell lists | tree scans on the mission tick | parked, applied from `OnMissionTick` |
| `SignatureAgentRoster` (`Remove` from `OnAgentDeleted`, `TryGet` from `OnMeleeHit`) | `_entries` (Dictionary) | `TryRegister` from `OnAgentBuild` | `ConcurrentDictionary` |
| `EnlistmentMeritMissionBehavior.OnAgentRemoved` | `_kills` | a concurrent removal | `Interlocked` |
| `WargMissionBehavior.OnAgentDismount` (thread proven by the log) | the controller's `CustomLookDir` | `WargRiderHandManager.Tick` | parked |

## Cause

**Not proven.** A .NET Framework `Dictionary` corrupted by concurrent writers can spin forever, and a
reader of one being mutated can spin too, which matches the log. The warg tree was live, so the first
writer above was reachable. But no managed second party that overlaps it was found: `OnMissionTick` never
overlaps the same frame's agent tick. The remaining candidates are a main-thread view or UI tick touching
one of these collections during the async window, a main-thread callback raised by native inside
`Mission.Tick` during that window, or native raising callbacks from two threads at once. None is shown.

## What changed

- `DeferredCallbackQueue.RunOrDefer` runs a write inline on the main thread and parks it for the next
  `OnMissionTick` anywhere else. Every writer marked "parked" above routes through it; each queue's owner
  marks the main thread itself at the top of its tick, so the deferral never leans on another behavior's
  registration order. FIFO keeps the tree component's cleanup behind the logic's own parked callbacks for
  the same removal, the order the main thread would use.
- Off-thread reports go to the file log at WARNING. `BehaviorTreeMissionLogic` used `BTRegister.Logger`,
  which can be the on-screen `BannerlordLogger`, from the reporting thread.
- `RegisterKill` takes a lock (its map's insertion order breaks kill ties in `EndBattle`, so a hash-ordered
  concurrent map would change which tied troop is offered promotion); `SignatureAgentRoster` is a
  `ConcurrentDictionary` (its register is one atomic `TryAdd`); the Enlistment kill count uses
  `Interlocked`. Unsynchronised, 20,000 kills of one troop registered from four threads counted 9,709.
- **Patch91 and `MissionTickStallWatchdog`:** probes bracket `Mission.TickAgentsAndTeamsImp` (the agent
  tick) and `MissionState.TickMissionAux` (the main thread's whole frame, native `Mission.Tick` included),
  the latter armed only while the mission is `Continuing`. A 1 s timer photographs each thread whose tick
  is in flight past 10/20/40 s as a `[MissionStall]` ERROR with its managed stack, one stack per thread.
  The next freeze names its own frame without a dump.
- The thread map, the architecture rule, the #592 RCA, the warg-combat doc, the #592 lesson and the
  CLAUDE.md trap row now say native decides the thread.

## Deep review findings (2026-09-22, 4 + 2 lenses)

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `CareerPerkMissionBehavior.OnAgentRemoved` clears a List the tick walks and recomputes other agents' stats off-thread | Threading | The first audit listed it; I set it aside because "a List cannot spin", judging by the incident's mechanism instead of the rule the change defined | Lesson: hardening a defect class covers every member (`lessons/harmony-il.md`) |
| 2 | LOW-MED | Four more writers of the class: `SpatialGrid.Remove`, SignatureStrikes roster, Enlistment `_kills`, Warg dismount | Threading | The census predated the thread-map change and was never re-run for the callbacks whose row moved | Same lesson: re-run the handler census for every moved row |
| 3 | MED | Watchdog blind to a main-thread hang inside native `Mission.Tick` | Instrumentation | Probe placed on the method I knew (`Mission.OnTick`) without reading its caller | Lesson: bracket the caller that owns the whole frame |
| 4 | MED | Teardown inside `Mission.OnTick` would false-alarm in Custom Battles and with the master toggle off | Instrumentation | Stand-down keyed on the exit-window latch, whose opener is campaign-and-master-only (the "Latches & Toggle Gates" rule's first point) | Same lesson: gate on engine state (`CurrentState == Continuing`) |
| 5 | MED | New toggle's relation to the master toggle undocumented; siblings disagree | Design | Not decided explicitly | Decided: independent (crash forensics), stated in the hint and doc |
| 6 | LOW | Deferral relied on BTML/AdvancedCombat having marked the main thread | Robustness | Invariant inherited from #595 without a test | Each queue owner marks; test ticks an unmarked guard first |
| 7 | LOW | Off-thread report at ERROR with regression wording; BTML's reporter could post a UI message from a worker | Logging | Reporter chosen for where the text lands, not for which thread calls it | Lesson: report delegates go to the locked file log |
| 8 | LOW | "Four volatile writes per frame" (six) | Docs | Counted one probe's writes, not two | Recounted from code |
| 9 | LOW | Thread-map row named `ItemPickupTick` as the `UseGameObject` caller | Docs | Summarised from a report without opening the method | Corrected from `HumanAIComponent.cs` |
| 10 | LOW-MED | "Native serialises these with `Agent.Tick`" and "on worker threads" stated as fact | Docs | The fix for an inference-stated-as-fact restated new inferences as fact | Extended the #634 lesson: a correction states only what the evidence shows |
| 11 | MED | Four surviving "main thread" claims (warg-combat, the #592 RCA twice, the #592 lesson) | Docs | Per-line grep; one claim wraps across two lines | Line-joined sweep before closing a doc correction |
| 12 | LOW | Untested: mission-end drop of parked kills, `Retire`'s work, `Start()` idempotency, the capture's guards, the provider default | Tests | Tests covered the routing, not the edges around it | Tests added |
| 13 | LOW | Stale counts: mount-despawn tests, battle-load tests/wiring/hook count, field-commission note, no feature-map row for BattleLoadDiagnostics, CLAUDE.md trap row | Docs | Counts quoted from memory of the folder | Recounted with `grep -c`; row added |
| 14 | LOW | "20,000 kills" was the per-troop figure | Docs | Paraphrased the test | Reworded |

Wave 2 (efficiency, design) found no defect. Applied, all behaviour-preserving: the watchdog as two named
slots instead of a probe list with a nesting scan; `ThreadStackCapture.Capture` as try/finally (the walk's
exception keeps its own stack trace); the SignatureStrikes roster as a `ConcurrentDictionary`; the queue owning
its reporter; `FormatFrames` without a per-frame string. The same proposal for field commission's kill map
was applied and reverted: the convergence pass showed `EndBattle` breaks kill ties in the map's insertion
order (`OrderByDescending` is stable), which a hash-ordered map scrambles, so the offer a tied troop gets
would have changed. `EndBattle_TiedKills_OffersFollowFirstKillOrder` now pins it (RED against the
concurrent map). Follow-up, pre-existing:
`SpatialGrid.Instance` is never cleared between missions, so the last battle's grid stays reachable on the
map until the next mission replaces it.

Not fixed here: `BehaviorTreeMissionLogic.OnAgentDeleted` dispatches `OnSelfAlarmedStateChanged` where
`SubscriptionPossibilities.OnAgentDeleted` exists (pre-existing, no subscriber to either, so no behaviour
changes). Recorded for Mike; no issue filed while he is away.

## Prevention

- Four lessons in `docs/reviews/lessons/harmony-il.md` (the thread map is a claim; census the whole
  class; bracket the frame and gate on engine state; report delegates go to the file log).
- The thread-map rule routes engine-callback writes through `RunOrDefer` or a lock by default and names
  the owner-marks and WARNING-to-file conventions.
- `[MissionStall]` turns the next freeze of this class into a stack in the player's log.

## Open

- The corrupting pair is unidentified. The next `[MissionStall]` line, or a `procdump -ma` of a frozen
  process, settles it.
- In-game: a large battle with reinforcements and a warg line for 15 minutes, expecting the off-thread
  WARNING lines (engine behaviour) and no freeze.
- The `bannerlord-1.4.5` port builds on the laptop only.
