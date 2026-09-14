# RCA: Warg bite clip requested on a horse, and the creature trees on the wrong thread (#592, #595), 2026-09-13

## Top-line

A player on TAOM v2.0.27 (Bannerlord v1.4.8.119303) froze mid-battle three times in one evening and
sent all three pairs of logs. The first pair ended on `as_horse does not contain act_warg_attack_running`:
TAOM had asked a cavalry horse to play the warg bite clip. That is a real defect, fixed here, but
the second pair, taken with dead-mount despawn switched off, showed the same freeze with no such
line, and settled what the line meant. The third pair, still with dead-mount despawn off and with no
spider in the battle, froze the same way with four warg trees and nothing else of TAOM's running,
which removes the spider and the stale-handle line as necessary conditions and leaves the second
mechanism on its own.

Two mechanisms, both TAOM's:

1. **Stale agent handles.** `MissionAdapterFactory` cached adapters by `Agent.Index`; the engine
   hands a deleted agent's index to the next agent it builds; a deleted managed `Agent` keeps its
   native pointers on the recycled slot. A reinforcement horse in a dead warg's slot got the warg's
   adapter and a warg behavior tree (first session). In the second session dead spiders' adapters
   drove live spiders: half the bites in the freezing battle carried the code's fallback damage, the
   signature of an attacker whose managed `Health` is zero, and every one landed on the spider's own
   army because a deleted agent's `Team` is null.
2. **The wrong thread.** Single-player ticks agents on an asynchronous AI thread, and TAOM's creature
   trees ran inside that tick. The spider's tree registered blows from it, which runs the engine's
   whole hit pipeline and TAOM's own tree-logic collections on that thread while the main thread's
   agent-removed callbacks write the same collections. Every creature tree read `SpatialGrid` from
   it while the main thread rebuilt the grid in place every two seconds, and the warg's tree called
   the native `SetActionChannel` and appended to the bone-check list from it. An unsynchronised
   dictionary read racing a
   removal can spin forever; the async tick then never completes and the main thread waits for it.
   That is the freeze: no exception, no allocation, process alive, worker pool spinning.

The first mechanism multiplies the second: every stale-handle bite is another blow registered on
the wrong thread against the spider's own side. A same-day audit of `Main/` for both classes found
nine more sites (#595, "Audit" below); none was an active freeze, every one was the same shape.

## Evidence

### First session (`taom_debug_2026-09-13_17-53-44.log`, `rgl_log_27236.txt`)

| Time | Log | Fact |
|---|---|---|
| 17:59 to 18:09 | both | Battle 1: 46 Fell Wargs, a spider, 166 late-attached war rams, chargers. The spider bit lancers, rams and a companion, never a warg. Ended normally. No `does not contain` line. |
| 18:12:31 | taom | Battle 2: 103 warg trees attached. |
| 18:16:22 to 18:16:24 | rgl | Reinforcement wave: nine `GetRandomBodyPropertyForTroop` lines. The spider late-attached at 18:16:24. |
| 18:16:29 | taom | The spider bit `Wadar Hotblood` and a `[Dol Guldur] Fell Warg` on its own side. |
| 18:16:33.887 | rgl | `as_horse does not contain act_warg_attack_running`. Last line of the session. |
| 18:16:38 to 18:27:50 | taom | `[MemSample]` kept ticking every 30 s with private memory and managed heap frozen at 16,490 MB and 596 MB. 11 GB of RAM free. |

### Second session (`taom_debug_2026-09-13_20-36-14.log`, `rgl_log_3936.txt`), dead-mount despawn off

| Time | Log | Fact |
|---|---|---|
| 20:39 to 20:45 | taom | Battle 1: 26 spider trees late-attached, 0 alive at the end. 17 spider hits, all with a rider, all formula damage. |
| 20:47:17 | taom | Battle 2 on the same scene. Spider late-attached at 20:51:15. |
| 20:52:38 | taom | First fallback bite: `damage=20 ... damager=spider-self`. From here 51 of the battle's 106 hits are the flat 20, all from riderless attackers, all on Dol Guldur troops and one `[Isengard] Warg`: the spider's own side. |
| 20:52:53 | taom | Last gameplay lines: a pounce with six fallback bites, then `[Spider] BT cleanup for Giant Spider`. |
| 20:52:54 | rgl | Last line, a level-up message. No `does not contain` line anywhere in the file. |
| 20:52:58 to 20:56:28 | taom | `[MemSample]` frozen at 19,494 MB and 579 MB. Process alive. |

The fallback is `SpiderAttackService.HandleSpiderTargetHit:63-68`: `damager = attacker.RiderAgent ??
attacker; if (damager.Health <= 0) { damager = target; damage = 20; }`. A live spider cannot have
`Health <= 0`. An adapter wrapping a DEAD spider whose slot a live spider now occupies reports
`IsActive()` true through the recycled slot and `Health` zero from the managed field, and its
`Team` is null (`Mission.OnAgentDeleted` sets it), so `IsSameTeam` fails against everyone.

### Third session (`taom_debug_2026-09-13_21-55-54.log`, `rgl_log_33092.txt`), dead-mount despawn off, no spider

| Time | Log | Fact |
|---|---|---|
| 01:38:18 | taom | Field battle on `battle_terrain_biome_053`, joined from a siege camp. Trees: 4 wargs, war rams late-attached; 0 spiders, elephants, mumakil. Career `silvan_archer`, ability created, no activation logged. |
| 01:38:21 to 01:43:57 | taom | Nothing but `[MemSample]` every 30 s: no hit, no cleanup, no error. |
| 01:43:45 to 01:43:52 | rgl | Four player orders through the order menu, each the full vanilla sequence: formation selected, time speed 0.25, `SetOrderWithTwoPositions MoveToLineSegment`, `After set order loop complete`, time speed reverted. |
| 01:43:57.377 | rgl | Last line: `Infantry added to selected formations`, then `Updated mission time speed ... 0.25`. The order itself never printed. No `does not contain` line, no exception; `Formation order position is not valid` only at 01:38:47 (formations 4 to 7, as in two earlier battles that ended normally). |
| 01:43:58 to 01:46:28 | taom | `[MemSample]` `heapMB=711` for the rest of the file, working set within 30 MB, 19.7 GB physical free. Process alive, no managed allocation on the game thread. |

The order menu is not the cause; it is the last thing the main thread printed before it stopped.
The shape is the shape of the first two freezes: the main thread stops, the heap stops moving, no
exception, the logger thread lives. What TAOM ran on the asynchronous agent tick in this battle was
the four warg trees and nothing else: `NoEnemyCloseDecorator` and `PeriodicallyCheckIfCanAttackAnyone`
call `SpatialGrid.GetNearAliveAgentsInRange` on every evaluation, and `WargAttackTask` ->
`AgentAdapter.CustomAttack` calls the native `SetActionChannel` and appends to `BoneCollisionService`'s
list, while the main thread rebuilt the grid every two seconds (`Grid.Clear()` in place, then
refill) and walked that list every frame. No spider, no stale-handle line. The hang dump is still
the decisive evidence; every one of those paths runs on the main thread in this fix, and the grid
now rebuilds by replacing the map.

## Mechanism 1: stale handles

Engine facts, decompiled from the installed v1.4.8:

- `Agent.State` reads native memory through `_statePointer` captured in the constructor
  (`Agent.cs:1529-1533,1566-1571`); `IsActive()` is `State == Active` (`Agent.cs:3294`);
  `IsFadingOut`, `MovementVelocity`, `Position`, `ActionSet` and `SetActionChannel` all go through `GetPtr()`.
- `Mission.OnAgentDeleted` (`Mission.cs:2972-2985`) sets `State = Deleted`, calls every behavior's
  `OnAgentDeleted`, drops the agent from `_allAgents`, nulls `Team`, and leaves the managed object
  alive with `Monster`, `Name`, `Character` and `Health` (a managed field, `Agent.cs:568`) intact.
  It never calls `Agent.OnRemove`, so an `AgentComponent.OnAgentRemoved` does not fire on that path.
- `Agent.GetHashCode()` returns `_creationIndex`, a per-mission monotonic counter
  (`Agent.cs:4992`, `Mission.cs:4058-4059`); `Agent` does not override `Equals`.
- Index allocation is native; `FindAgentWithIndex` is a native lookup of the slot's current managed
  occupant (`Mission.cs:1634,5150`), which vanilla itself calls with indices from blows and
  network messages. That lookup is the one question a stale handle cannot answer, and it is what
  `AgentSlotIdentity.IsCurrentOccupant` asks.
- `OnAgentBuild` fires for late-spawned mounts (`Mission.cs:4346,4360`).

Only `WargAttackService.WargAttack` requests `act_warg_attack_running`
(`Main/Features/Warg/WargAttackService.cs:133`). `WargMissionBehavior.OnAgentBuild` decided whether
a late-spawned agent was a warg by asking the cached adapter's `IsWarg()`, which reads the wrapped
agent's managed `Monster.StringId`. The chain in the first session: warg W (index N) dies; despawn
deletes it; horse H spawns into N; `OnAgentBuild(H)` gets W's adapter; `IsWarg()` reads W's
`Monster`; a warg tree is attached to H; its rider charges; `WargAttackTask` resolves W's adapter;
`IsActive()` and velocity read H's slot; `W.SetActionChannel(act_warg_attack_running)` lands on H.

When a live creature inherits a dead agent's slot the aliasing is invisible for movement and
animation, since the pointers hit the right slot; only the managed fields lie. That is why the
feature looked correct for five months, and why the second session's spiders kept attacking while
reporting a dead attacker.

## Mechanism 2: the asynchronous agent tick

- `MissionState.TickMission` passes `asyncAITick: true` on the normal single-player path
  (`MissionState.cs:201`); the paused, fixed-step and loading branches pass false.
- `Mission.OnTick` (`Mission.cs:3740-3774`) sets `tickCompleted = false`, runs every behavior's
  `OnMissionTick`, then `TickAgentsAndTeamsAsync(dt)`, a native call.
- Native calls back `Mission.TickAgentsAndTeams` (`[MBCallback]`, `Mission.cs:1818-1822`) on
  another thread; `TickAgentsAndTeamsImp` (`:3601-3617`) runs `TWParallel.For(AgentTickMT)`
  (`Agent.TickParallel` on the worker pool: `AgentComponent.OnTickParallel`, and for AI agents
  `HumanAIComponent.ParallelUpdateFormationMovement` -> `Agent.GetBaseFormationFrame` ->
  `Formation.GetOrderPositionOfUnit`), then `Agent.Tick` for every agent (`Agent.cs:4746-4770`,
  every `AgentComponent.OnTick`, `TickAsAI`), then `Team.Tick` (`TeamAI`, the retreat branch's
  `SetMovementOrder` for any team, `Formation.Tick`), sets `tickCompleted = true`, then every
  SubModule's `AfterAsyncTickTick`.
- The main thread's next `OnPreTick` (`[MBCallback]`, `Mission.cs:3529-3533`) spins in
  `WaitTickCompletion` (`:3585-3590`, `Thread.Sleep(1)` loop) until the flag is set.
- `Mission.OnAgentRemoved` and `OnAgentDeleted` are `[MBCallback]`s (`Mission.cs:2971,2989`)
  entered from native combat processing on the main thread, during the window in which the async
  tick runs. `Mission.OnAgentHit` (`:5600-5616`), `OnAgentRemoved` (`:2990-3029`) and
  `Mission.SpawnAgent`'s `OnAgentBuild` loop (`:4358-4361`) iterate `MissionBehaviors` with no
  catch: an exception from one behavior skips every later one and propagates toward native.
- `CommonAIComponent.OnTick` runs inside `Agent.Tick` on the asynchronous thread and calls `Panic()`
  -> `Mission.OnAgentPanicked` synchronously (`CommonAIComponent.cs:119-135`, `Mission.cs:6708-6714`),
  so every behavior's `OnAgentPanicked` arrives on that thread; `OnAgentFleeing` is raised from
  `MissionAgentPanicHandler.OnPreMissionTick`, on the main thread. Which thread a native `[MBCallback]`
  such as `Agent.OnAgentAlarmedStateChanged` uses is not established (Codex review 109).

TAOM's `BehaviorTreeAgentComponent.OnTick` ran each creature tree inside `Agent.Tick`. The spider's
`RadialStrike` (`AgentAdapter.cs`) invokes `HandleSpiderTargetHit` synchronously per target, which
calls `CustomAttacksUtils.TakeDamage` -> `Mission.RegisterBlow` -> `Agent.HandleBlow` ->
`Mission.MakeSound`, `Mission.OnAgentHit` (every behavior, including vanilla `BattleAgentLogic`'s
campaign XP and TAOM's `BehaviorTreeMissionLogic.OnAgentHit`), `Die` -> `OnAgentRemoved`. Vanilla's
one managed caller of `RegisterBlow` is the drowning check in `Mission.OnTick` (`Mission.cs:3684`), on
the main thread; nothing vanilla registers a blow from the agent tick. `BehaviorTreeMissionLogic` keeps
`trees` (a `Dictionary<Agent, BehaviorTree>`), `actions` and a shared `_tempMatched` scratch list
with a comment that assumes single-threaded dispatch; `OnAgentHit` on the async thread reads them,
`OnAgentRemoved` -> `DisposeTree` -> `trees.Remove` on the main thread writes them. The last line
of the second freeze is that removal.

The warg is not a control. Its bite damage is applied by the bone check inside
`AdvancedCombatBehavior.OnMissionTick`, on the main thread, but its tree ran on the asynchronous tick
too: `NoEnemyCloseDecorator` and `PeriodicallyCheckIfCanAttackAnyone` read `SpatialGrid` on every
evaluation while the main thread rebuilt it in place every two seconds, and `CustomAttack` called the
native `SetActionChannel` from that thread and appended to the bone-check list the main thread walks
every frame. The third session froze with four warg trees and nothing else.

## Why it was missed

| # | What was known | What happened |
|---|---|---|
| 1 | April 2026 Codex HIGH: the cache is a process singleton keyed by mission-scoped `Agent.Index`. | The finding named cross-mission reuse. Commit `6962f40f` added `ClearCache()` at mission end and closed it. The intra-mission case was never asked about. |
| 2 | 2026-09-03, `mount-despawn.md`: "Agent indices are reused", and the feature deliberately avoided this cache for that reason. | The hazard was documented as avoided by the new feature, not fixed in the store every other creature feature used. |
| 3 | 2026-06-25 crash: a horse "caught mid-teardown", `IsHuman`/`IsMount` "reading INVERTED off the freed native struct" (`CustomAttacksUtils.cs:147-161`). | Filed as teardown timing. A dead human's managed `Agent` reading a horse's flags through a recycled slot is mechanism 1; a torn read across two threads is mechanism 2. |
| 4 | 2026-05-24: the manual main-thread tick in `WargMissionBehavior.OnMissionTick` was removed as "2x ticks per frame", keeping the engine-driven component tick. | The engine's tick site was assumed to be the right home for mod logic. Nobody opened `TickAgentsAndTeamsImp` to see which thread runs it. The wrong copy was deleted. |
| 5 | The warg's damage path (bone check on `OnMissionTick`) never froze; the spider's (synchronous radial strike in the tree) did, twice. | The asymmetry was not asked about until the second log removed the `as_horse` line from the picture. |
| 6 | Patch35's own comment (2026-06) named the async tick and the dictionary corruption it causes, and gated on the player team. | A team filter was taken for a thread filter. The engine issues the player's own team's orders on that tick whenever a formation is AI-controlled. |

## Deep review (five agents, a focused re-review, then a second five-agent pass)

Standards, compatibility (11 engine members verified against the installed DLLs, 0 incompatible)
and completeness passed. Performance and data flow returned the findings below; all confirmed
against the source and fixed in the same session. A second five-agent pass over the finished
changeset added rows 11 to 16: compatibility again found 0 incompatible members and left two
native claims UNVERIFIED (that a fade-out reaches only `OnAgentDeleted`, and the thread behind
`Agent.UpdateAgentStats`); data flow found the remaining holders.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The first cut fixed the cache and the attach decision, but a `BoneCheck` holds target adapters across frames, and every liveness guard a stale target passes reads the recycled slot. `Health` is managed and only helps when the agent died in combat; an agent deleted alive keeps a positive `Health`, so `TakeDamage` could hand `RegisterBlow` a dead managed `Agent` whose `Index` now names a different live agent. | Data flow (handle lifetime) | The analysis stopped at the cache; nobody enumerated every holder of an adapter. | `AgentSlotIdentity.IsCurrentOccupant`: `Mission.FindAgentWithIndex(agent.Index)` must still return that object. Folded into `AgentAdapter.IsActive()` and asked again in `TakeDamage`, sample-logged. |
| 2 | MED | `AgentAdapter.ProjectAgent` was the one `SetActionChannel` in the class left outside the new clip-exists gate. | Consistency of a new guard | The guard was added at the two attack entry points named in the log line. | Every `SetActionChannel` in `AgentAdapter` goes through `HasClipForAction`. |
| 3 | MED | The cache's lifecycle sat in `WargMissionBehavior` although Spider shares the cache. | Ownership | The eviction was added next to the existing `ClearCache`, whose owner was chosen by proximity. | `AdvancedCombatBehavior` owns build, delete and mission end for the cache. |
| 4 | LOW | "Logs the first index reuse of every mission" was only true for reused indices whose new occupant somebody adapted. | Telemetry accuracy | The reuse was detected on the add path because that is where the cache lived. | Reuse is counted in `OnAgentBuild`, which fires for every agent including mounts. |
| 5 | MED | `GetAgentAdapter` allocated a closure on every call, hits included. Not a regression. | Performance | Copied the shape of the code it replaced. | `TryGet` then `Add`; a hit allocates nothing. |
| 6 | MED | The two creature shadow lists prune on the raw engine `IsActive()`, which a reused slot defeats the same way. | Same defect class, third holder | The raw-`Agent` lists were not enumerated. | Both prunes require `AgentSlotIdentity.IsCurrentOccupant`. |
| 7 | LOW | The `AgentVisuals` getter ran the native slot lookup a second time per target per frame. | Performance | Consistency applied without counting calls on the per-frame path. | `AgentVisuals` keeps the raw engine check; the callers' `IsActive()` carries the identity. |
| 8 | MED | (review of the threading change) The new scheduler's liveness guard was the engine's `IsActive()` alone, which answers for a recycled slot's new tenant; a component left scheduled for a deleted agent (fade-out never calls the component's `OnAgentRemoved`) would run its tree against the wrong agent. Not reachable today, since the only TAOM fade follows a kill. | Same defect class, fourth holder | The guard copied `Agent.Tick`'s own check. | `TickOnMissionThread` also requires `AgentSlotIdentity.IsCurrentOccupant`. |
| 9 | LOW | `BehaviorTreeMissionLogic` hands every hit dispatch the same scratch list and a comment promised no listener re-enters; a listener that lands a blow would clear the list under the outer walk. Holds today. | Unenforced invariant | The invariant was documented, not defended. | `FindCalledListeners` gives a re-entrant call its own list; `NotifyAll` tracks the outer dispatch. |
| 10 | LOW | The tree tick now precedes the bone-check tick in the same frame (reverse registration order), so a bite's bone check starts one frame earlier. | Ordering | Not asked. | Noted in `warg-combat.md`; the window is progress-based and unchanged. |
| 11 | MED | The caster's own restore closure refreshed `_agent` through a handle that may name a recycled slot, so an expired buff would push the dead hero's stats onto whoever inherited the index. The ally closure five lines above it had been gated; this one had not. | Same defect class, fifth holder | The audit fixed the closure the reviewer named and stopped there. | `UpdateAgentProperties` only for a live caster that still owns its index; the tracker entry is keyed by hero id and is always subtracted. |
| 12 | MED | `TaomHowdahStandingPoint.ReleaseAgent` drops the rider at `elephantAgent.Position`, and the machine released seats before clearing that handle, so a recycled slot could place riders at a stranger's feet. | Same defect class, read at the point of use | The guard was added one call above the read. | The read itself requires slot identity: a dead elephant still owning its slot gives the corpse position, a recycled handle falls back to the rider's own. |
| 13 | LOW | A tree component schedules itself in its constructor, before `AddComponent`; an attach that threw would leave it ticking with no `OnAgentRemoved` to unschedule it. `Agent.AddComponent` is a list add, so the window is theoretical. | Lifecycle symmetry | The catch was added for the engine's unguarded spawn loop, not for the scheduler. | Both attach sites hoist the component and, on a throw, unschedule and dispose exactly as `OnAgentRemoved` does. |
| 14 | LOW | `NotifyAll` consumed its once-per-type flag before checking whether a logger existed, so a listener that threw before `BTRegister.Logger` was set would never be reported. | Ordering of a one-shot | The `?.` read as safe. | The flag is taken only when a logger can carry the message. |
| 15 | LOW | Seven comments in files this change rewrote still carried long dashes, and `MixedFormationsMissionBehavior` had grown to 157 lines. | Standards | The dash scan covered markdown; the line count was not re-measured after the `OnAgentDeleted` override. | Comments repunctuated; the layout label switch moved to `Models/FormationLayoutLabels.cs` (5 tests). |
| 16 | (none) | Swapping the two registrations to restore the old bone-check order would also swap the `OnAgentDeleted` order between the cache owner and the tree logic. | Ordering | (not a miss) | Left as is; put to Codex as a suspect with the decompile targets. |

Why each agent missed finding 1: standards checks rules, not lifetimes; compatibility verifies
members, not who holds them; completeness checks artefacts; performance costs calls. Only the
data-flow pass asks "who else holds this handle, and for how long". None of the seven asked which
thread the tree ran on; that took the second log. Both questions are now rule 5e of that agent.

## Codex review 109 (gpt-6-astra, ultra, 2026-09-13)

Prompt `docs/reviews/codex-adversarial-creature-handles-2026-09-13.prompt.md` with nine Known
Suspects; raw output `docs/reviews/raw/codex-adversarial-creature-handles-2026-09-13.md`. Verdict:
issues found, one P2, no P1. Codex decompiled the caller chains from the installed DLLs, walked one
frame per thread, tabulated thirty retained handles, and compiled the production `LayoutPositioner`
into a PowerShell harness to print the collision it claimed. Its sandbox cannot evaluate MSBuild
(the Windows SDK lookup is denied), so it ran neither build nor tests and said so. Every finding was
re-read against the source before it was acted on.

| # | Codex | Mine | Agree | Verdict and fix |
|---|---|---|---|---|
| F1 | P2 | P2 | yes | `ForgetAgent` dropped the mapping and left `NextMeleeIndex` and `NextRangedIndex` growing, so every casualty's replacement took a fresh counter value: a row deeper each time, and in the front/back layouts on top of the other class's rows (ten melee in row 0, ten ranged in row 1, one melee death: the replacement lands on (1, -5), the first archer's slot). Reproduced by the harness and now by `RepeatedTurnover_KeepsEveryLiveUnitOnAUniqueSlotInsideTheInitialFootprint`. Fix: `SlotAssignment` records each unit's class and holds a vacated slot for the next unit of that class; `AssignNextSlot` reclaims before any counter advances. |
| O1 | P3 | P2 | yes, raised | `CommonAIComponent.OnTick` runs inside the asynchronous agent tick and calls `Mission.OnAgentPanicked` synchronously (`CommonAIComponent.cs:119-135`), so `BehaviorTreeMissionLogic.OnAgentPanicked` read `actions` off the main thread while `Subscribe` and `UnSubscribe` write it there. No subscriber exists today, but the read is the defect class of the freeze itself. Fix: every engine callback into the tree logic asks `MissionThreadGuard.IsOnMainThread` and, when off it, parks itself in `DeferredCallbackQueue` (reported once per site by the tripwire) for the top of the next `OnMissionTick`. |
| O2 | P3 | P3 | yes | The howdah seat's own rider (`MovingAgent`) was checked with the raw `IsActive()` before every teleport and release, and `SpatialGrid` kept deleted agents until the next rebuild, reading their position through the recycled slot. Fix: the seat requires slot identity for its rider and reads the elephant only through a live handle; `AdvancedCombatBehavior.OnAgentDeleted` evicts from the grid. The warg blackboard's `AgentHitBy` and the player controller's target are gated downstream by the adapter's `IsActive()` and are left as they are. |
| O3 | P3 | P3 | yes | Five global listener loops (fleeing, panicked, removed, missile, object disabled) bypassed the per-listener catch. Fix: every walk goes through `NotifyAll`. |
| S1 | precaution | taken | yes | Registration order: `AdvancedCombatBehavior` registers after the tree logic again, so bone checks tick before trees and a bite is first checked next frame, the order that shipped for months. Whether `GetCurrentAction(0)` reflects `SetActionChannel` in the same frame stays UNVERIFIED and no longer matters. Codex disputed that the deletion order between the two matters (separate stores), which is what had held the swap back. |
| obs | P3 | rejected | no | `CareerAbilityBuffTracker.GetBuff` returns the live object after releasing the lock, so a multi-field read can be torn. Not intended as a snapshot: the fields are independent deltas read into stats that the next refresh recomputes, and a copy per read would allocate on every stat refresh. Documented here, not changed. |
| obs | P3 | fixed | yes | `MissionAdapterFactory` did `TryGet` then `Add`, so two concurrent misses could wrap one agent twice. `AgentAdapterCache.GetOrAdd` decides under the lock with one cached delegate; 2 tests. |
| obs | P3 | fixed | yes | The warg's first-tick scan lacked the late-attach rollback. Both sites share `TryAttachWargTree`. |

Known Suspects: 1 ordering confirmed and the bite failure unverified (swap taken); 2 disputed
(`Agent.AddComponent` is a list add; the rollback stays as symmetry); 3 UNVERIFIED (whether
`Mission.FindAgentWithIndex` returns null for a freed, unreused slot; in-game check under Open);
4 disputed (`CareerPerkMissionBehavior` clears buffs and contexts at mission end, `:213-218`);
5 confirmed (F1); 6 collection locking confirmed, "main thread only" disputed (O1); 7 disputed for
all four lifecycle cases (both bookkeeping choices leave the tracker consistent); 8 disputed
(release before clear is right for a dead elephant that still owns its slot; the seat's rider was
the real gap, O2); 9 disputed (either deletion order is equivalent). Codex also corrected two
sentences of this document: vanilla's one managed `RegisterBlow` caller is the drowning check in
`Mission.OnTick`, on the main thread, and `Agent.TickParallel` itself plays an action (the
player's cheer cancel) on the worker path.

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| F1 | Eviction without reclaim grew the footprint | Logic error (allocator state) | The #595 fix removed the mapping and stopped; nobody asked what the allocator does with the next request, and the test oracle ("a different coordinate") accepted the defect. | Vacancy reclaim per class; a turnover test asserting uniqueness and footprint over 100 cycles; lesson in `lessons/state-lifecycle-save.md`: an evicted slot goes back to its allocator. |
| O1 | An engine callback on the asynchronous tick | Wrong thread | The thread map was built from `Mission.OnTick` and the native callbacks; `Mission.OnAgentPanicked` is a plain managed call whose caller is an `AgentComponent.OnTick`, and nobody opened the callers of the thirteen overrides. | `DeferredCallbackQueue` and `IsOnMainThread` in the tree logic; thread-map rows for `OnAgentPanicked` (async) and `OnAgentFleeing` (main); lesson in `lessons/adapters-taleworlds-api.md`: a callback's thread is its caller's. |
| O2 | The seat's own rider ungated; the grid's stale entries | Same defect class | The audit gated the elephant handle the reviewer named and not the second handle in the same class; the grid's rebuild cadence was read as eviction. | `IsCurrentOccupant` on the rider; `SpatialGrid.Remove` on deletion; the grid rebuilds by replacement and carries the tripwire. |
| O3 | Global loops outside the catch | Consistency of a new guard | The catch was added where the second review pointed and the loops that never called it were not enumerated. | One dispatch helper for every walk. |

## Audit of the two defect classes across `Main/` (#595)

Three reviewers, one per class plus callback phase; every claim below was re-read against the
source or the decompile before it was acted on.

| # | Sev | Where | Bug | Fix |
|---|---|---|---|---|
| A1 | MED | `MixedFormations/FormationLayoutService.cs`, `Models/SlotAssignment.cs` | `ByAgentIndex` keyed by `Agent.Index` for the whole mission, never evicted: a reinforcement inheriting a dead unit's index took its slot. | `ForgetAgent` from `MixedFormationsMissionBehavior.OnAgentDeleted`, under the existing lock. 3 tests. |
| A2 | MED | `CareerSystem/Abilities/MissionAbilityExecutionContext.cs` | The AoE restore closure subtracted by its captured index regardless of who held the index at expiry. | `AgentSlotIdentity.IsCurrentOccupant(ally)` gates the subtraction. |
| A3 | MED | `Elephant/TaomHowdahMachine.cs` | The platform followed `elephantAgent.Position` with only a null check; a reused slot would drag it after a stranger. | Drop the handle, release and clear the seats when the elephant is not alive and the slot's occupant. |
| A4 | LOW | `DreadAura/Hooks/DreadSourceTracker.cs` | `Prune` trusted the engine's `IsActive()`; masked by the pulse runner's null-`Team` early-out. | Prune on slot identity too. |
| T1 | MED | `CompanionTactics/.../Patch35_Formation_SetMovementOrder.cs`, `TroopStanceManager.cs` | The `PlayerTeam` filter was not a thread filter: `Team.Tick`'s retreat branch and `Formation.Tick`'s substitute orders issue the player's own orders on the async tick when a formation is AI-controlled; the store had no lock and the UI writes it. | `TroopStanceManager` locks. 1 test. |
| T2 | MED | `CareerSystem/Abilities/CareerAbilityBuffTracker.cs` | Static dictionaries, no lock, read behind `TaomAgentStatCalculateModel` through `Agent.UpdateAgentProperties`, one of whose callers is a native callback whose thread the decompile cannot show. | Lock. 4 tests. |
| T3 | info | `MixedFormations/Hooks/Patch30_FormationGetOrderPositionOfUnit.cs` | Runs on the TWParallel worker pool (`HumanAIComponent.ParallelUpdateFormationMovement` -> `Agent.GetBaseFormationFrame` -> `Formation.GetOrderPositionOfUnit`), not only the async thread. Safe because the service locks. | Recorded in the registry and the feature doc so the lock is never removed. |
| P1 | MED | `BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs` | `NotifyAll` had no per-listener catch inside the engine's unguarded behavior loops; a throwing listener would skip every later behavior for that agent and propagate toward native (the class already crashed once: `Agent_CheckToDropFlaggedItem_Guard_Patch`). | Catch per listener, log once per listener type. |
| P2 | MED | Six `OnAgentBuild` overrides | Unguarded inside `Mission.SpawnAgent`'s unguarded loop; an exception aborts the spawn wave for later behaviors. | `CreatureTreeTracker.TryAttach` catches (four creatures); `AdvancedCombatBehavior` and `CareerPerkMissionBehavior` catch; all log once. |
| P3 | LOW | `Elephant/ElephantMissionBehavior.cs` (dormant), `Adapters/HeroCommissionAdapter.cs` | Re-enabling the howdah crew spawn from `OnAgentBuild` would re-enter `SpawnAgent`; `FadeOut(hideMount: true)` never raises `OnAgentRemoved` for the mount. | Comments naming the rule at both sites. |

Checked and safe: `MountDespawn` (evicts on every deletion), stores keyed by `Formation` objects
(`TroopStanceManager`, `CavalryChargeService`, `FormationLayoutService`'s outer maps), `SpatialGrid`
(rebuilt each update), the bone-check service (reverse-index removal), every `OnMissionTick` that
enumerates `AllAgents` (read-only), no nested kill inside a hit or removal callback, no TAOM
`OnTickParallel` or `AfterAsyncTickTick`. `docs/audits/cluster-cross-feature.md` rows 2 and 21 and
`cluster-harmony-patches.md` B1/B2 were stale and are marked resolved.

## Root-cause pattern

Every finding in this document is one of two questions never asked: **who else holds this handle,
and for how long**, and **which thread runs this code**. Both were answered once, locally, and the
answer was written next to the feature that dodged the hazard instead of into the store or the
rule every feature shares. The engine gives no error for either: a recycled slot reads as a live
agent, and an off-thread write reads as working code until two threads meet in the same bucket.

## Prevention

- **Facts where the next feature reads them.** `.claude/rules/csharp-architecture.md` "Mission-scope
  agent handles and the engine's threads" (loads on every C# file); `.claude/rules/harmony-patches.md`
  "Which thread runs your target" with the verified thread map; `.claude/rules/adapters.md` "Agent handles".
- **The review asks both questions.** `/deep-review` Agent 5 rule 5e enumerates every holder of an
  agent handle and the thread of every mission-time code path.
- **Two helpers, one shape.** `AgentSlotIdentity.IsCurrentOccupant` for every held handle;
  `MissionThreadGuard.NoteCall` for every native write. A new holder or a new off-thread write that
  skips them is the review finding.
- **The engine fact is proven in every battle log.** `[AdapterCache] agent index N reused` fires on
  the first reuse of each mission; `[TAOM] ... ran off the main mission thread` fires if a blow or
  creature action ever leaves the main thread again.
- **Tests pin the policy.** Reference identity and eviction (`AgentAdapterCacheTests`), the once-per-
  mission reuse log (`MissionAdapterFactoryTests`), the thread tripwire (`MissionThreadGuardTests`),
  slot forgetting (`FormationLayoutServiceTests`), and the two locked stores under contention.
- **Lessons appended** to `docs/reviews/lessons/adapters-taleworlds-api.md`: the recycled-slot handle,
  the asynchronous tick, and "hunt the class, not the instance".

## Fix

Mechanism 1:

- `Main/Adapters/AgentAdapterCache.cs` (new, pure, locked): adapters keyed by the agent object with
  an explicit reference comparer; `Evict` remembers the freed index; `NoteBuilt` reports and counts
  the build that lands on one. 16 tests.
- `Main/Adapters/MissionAdapterFactory.cs`: `GetOrAdd` under the cache's lock with one cached
  delegate; `OnAgentBuilt` logs the first reuse of every mission; `Evict`; `ClearCache` logs the
  count. 6 tests on bare uninitialized `Agent` objects.
- `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs`: owns the cache lifecycle and marks the
  main thread each tick; `OnAgentBuild` catches.
- `Main/Features/AdvancedCombat/AgentSlotIdentity.cs` (new): `IsCurrentOccupant(Agent)`; used by
  `AgentAdapter.IsActive()`, `CustomAttacksUtils.TakeDamage`, both creature shadow-list prunes, the
  tree scheduler, the dread tracker, both career restore closures, the howdah machine and its
  seats' release.
- `Main/Adapters/AgentAdapter.cs`: every `SetActionChannel` asks `MBActionSet.CheckActionAnimationClipExists`
  first and logs the agent, monster and set once.
- `Main/Features/Warg/WargMissionBehavior.cs`: attach decisions read `WargConfig.IsWargMonster(agent.Monster?.StringId)`.

Mechanism 2:

- `Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs`: `OnTick` is a no-op; the component
  schedules itself with `BehaviorTreeMissionLogic` on construction and unschedules on
  `OnAgentRemoved`; `TickOnMissionThread` keeps the cadence rule and requires slot identity.
- `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs`: `OnMissionTick` ticks every scheduled
  component from a snapshot; hit dispatch defends against re-entry and catches per listener; every
  engine callback that arrives off the main thread is parked in `DeferredCallbackQueue` (new, pure,
  6 tests) and replayed at the top of the next tick; every listener walk goes through that dispatch.
- `Main/Features/AdvancedCombat/SpatialGrid.cs`: rebuilt by replacing the map, never cleared under a
  reader; `Remove` on `OnAgentDeleted`; the tripwire on rebuild and query.
- `Main/SubModule.cs`: `AdvancedCombatBehavior` registers after the tree logic, so bone checks tick
  before trees and a bite is first checked next frame.
- `Main/Features/AdvancedCombat/MissionThreadGuard.cs` (new, pure): `MarkMainThread`, `NoteCall`
  reporting once per site; wired into `TakeDamage` and `AgentAdapter.HasClipForAction`. 5 tests.

Audit (#595): the table above.

Codex review 109: `SlotAssignment` reclaims vacated slots per class (4 tests, plus 3 in the layout
service and positioner); the howdah seat gates its own rider; both warg attach sites share one
helper; `MissionThreadGuard.IsOnMainThread` (3 tests).

Suite: 9045 passed, 2 skipped, 0 failed.

## Open

- The player's hang dump. Expected now: the main thread inside `Mission.OnPreTick` ->
  `WaitTickCompletion`, and the async tick thread inside a `Dictionary` lookup or TAOM tree code
  under `Mission.TickAgentsAndTeams`. That confirms mechanism 2 directly; a main thread inside
  `IMBAgent.SetActionChannel` would point back at the mismatched clip instead.
- In-game confirmation on the dev machine: a custom battle with spiders and wargs against cavalry,
  reinforcements on both sides, dead-mount despawn on. Expected in `taom_debug`:
  `[AdapterCache] agent index N reused`, no `damager=spider-self damage=20` bites on the spider's
  own side, no `[TAOM] ... ran off the main mission thread` line. Expected in `rgl_log`: no
  `does not contain` line.
- The outside analysis the player received also blamed a TAOM/TAOM.Dependencies build MISMATCH.
  `git diff v2.0.26 v2.0.27 -- Dependencies/` is empty; the line is a 12 hour build-time heuristic
  (`BuildStampReport.cs:41,53`). Follow-up #593.
- The spider biting a riderless mount on its own side is a separate team-rule defect. Follow-up #594.
- The spider's usage set carries strike rows only, no fall rows (the warg's has nine). Not shown to
  matter; noted for the creature authoring checklist.
- Codex suspect 3, UNVERIFIED: whether `Mission.FindAgentWithIndex` returns null for a freed,
  unreused slot or the old managed object. The in-game battle answers it: a deleted agent's
  `IsCurrentOccupant` must read false before its index is reused (`[AdapterCache]` reuse line after a
  despawn with no stale bite in between).
- Codex suspect 1, UNVERIFIED: whether `GetCurrentAction(0)` reflects `SetActionChannel` in the same
  frame. The registration order restored the next-frame first check, so warg bites do not depend on
  it; the in-game battle should still show `[Warg]` hits landing.

## Files

| File | Change |
|---|---|
| `Main/Adapters/AgentAdapterCache.cs` | New reference-identity cache with eviction, reuse count, one lock; `GetOrAdd` builds once under it |
| `Main/Adapters/MissionAdapterFactory.cs` | `GetOrAdd` with a cached delegate; `OnAgentBuilt`; `Evict`; reuse telemetry |
| `Main/Adapters/IMissionAdapterFactory.cs` | `OnAgentBuilt(Agent)`, `Evict(Agent)` |
| `Main/Adapters/AgentAdapter.cs` | Slot identity in `IsActive()`; clip-exists guard and thread tripwire on every `SetActionChannel` |
| `Main/Adapters/HeroCommissionAdapter.cs` | Phase comment at the fade |
| `Main/Features/AdvancedCombat/AgentSlotIdentity.cs` | New: `IsCurrentOccupant` |
| `Main/Features/AdvancedCombat/MissionThreadGuard.cs` | New: main-thread mark, `IsOnMainThread`, once-per-site off-thread report |
| `Main/Features/AdvancedCombat/SpatialGrid.cs` | Rebuild by replacement; `Remove` on deletion; tripwire |
| `Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs` | Owns the cache lifecycle; marks the main thread; `OnAgentBuild` catches; evicts the grid on deletion |
| `Main/Features/AdvancedCombat/CustomAttacksUtils.cs` | Slot identity and thread tripwire before `RegisterBlow` |
| `Main/Features/AdvancedCombat/CreatureTreeTracker.cs` | Prune by slot identity; attach catches, and unschedules on a throw |
| `Main/Features/Warg/WargMissionBehavior.cs` | Attach by `Monster` through one `TryAttachWargTree` helper that unschedules on a throw; cache calls removed; prune by slot identity |
| `Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs` | Engine tick no-op; schedules with the mission logic; slot identity |
| `Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs` | Ticks every scheduled tree from `OnMissionTick`; re-entrancy guard; per-listener catch on every walk; off-thread callbacks deferred |
| `Main/BehaviorTreeWrapper/DeferredCallbackQueue.cs` | New, pure: the parked callbacks |
| `Main/SubModule.cs` | Bone checks registered to tick before trees |
| `Main/Features/MixedFormations/FormationLayoutService.cs`, `IFormationLayoutService.cs`, `LayoutPositioner.cs`, `Models/SlotAssignment.cs`, `Hooks/MixedFormationsMissionBehavior.cs`, `Models/FormationLayoutLabels.cs` | `ForgetAgent` on deletion returns the slot to its class; `AssignNextSlot` reclaims it; the label switch moved out of the behavior |
| `Main/Features/CareerSystem/Abilities/MissionAbilityExecutionContext.cs`, `CareerAbilityBuffTracker.cs`, `CareerPerkMissionBehavior.cs` | Restore-closure identity (ally and caster); lock; `OnAgentBuild` catches |
| `Main/Features/Elephant/TaomHowdahMachine.cs`, `TaomHowdahStandingPoint.cs`, `ElephantMissionBehavior.cs` | Drop a dead or displaced elephant handle; a seat reads the elephant, and moves or releases its own rider, only through a handle that owns its slot; re-enable rule |
| `Main/Features/DreadAura/Hooks/DreadSourceTracker.cs` | Prune by slot identity |
| `Main/Features/CompanionTactics/BattleActionBar/TroopStanceManager.cs` | Lock |
| `Main/Features/Spider/SpiderBehaviorTree.cs` | Comment corrected |
| `TAOM.Tests/Adapters/AgentAdapterCacheTests.cs`, `MissionAdapterFactoryTests.cs` | 18 + 6 tests |
| `TAOM.Tests/Features/AdvancedCombat/MissionThreadGuardTests.cs` | 8 tests |
| `TAOM.Tests/BehaviorTreeWrapper/DeferredCallbackQueueTests.cs` | 6 tests |
| `TAOM.Tests/Features/MixedFormations/FormationLayoutServiceTests.cs`, `LayoutPositionerTests.cs`, `SlotAssignmentTests.cs`, `FormationLayoutLabelsTests.cs` | 5 + 1 + 4 + 5 tests |
| `TAOM.Tests/Features/CompanionTactics/BattleActionBar/TroopStanceManagerTests.cs`, `TAOM.Tests/Features/CareerSystem/CareerAbilityBuffTrackerTests.cs` | 1 + 4 tests |
| `.claude/rules/csharp-architecture.md`, `harmony-patches.md`, `adapters.md`; `.claude/skills/deep-review/SKILL.md` | Prevention |
| `docs/features/warg-combat.md`, `mount-despawn.md`, `mixed-formations.md`, `companion-tactics.md`, `career-system.md`, `elephant.md`, `dread-aura.md` | Feature notes |
| `docs/reference/harmony-patch-registry.md`, `docs/audits/cluster-cross-feature.md`, `cluster-harmony-patches.md` | Thread notes; stale rows resolved |
| `docs/reviews/lessons/adapters-taleworlds-api.md` | Three lessons appended |
