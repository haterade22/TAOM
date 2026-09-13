# Smart Cavalry AI

## Overview

When the player orders a cavalry formation to Charge (F3) or ChargeToTarget in an open-field battle,
intercept the order and run a coordinated hit-and-run cycle: line up, charge, ride through, reform on
the far side facing the enemy, and charge again at the nearest live enemy formation until none is
left or the player gives any other order. Optionally reroute around friendly infantry on the charge
line first. Player-commanded formations only; the team AI's formations keep vanilla behaviour.

MCM: **Battle Tactics / Smart Cavalry**. Ships OFF by default until the in-game smoke below passes.

## Why This Exists

- **Vanilla behaviour:** cavalry charges as a clump, stops on first contact with infantry, gets stuck
  in melee, and tramples friendly units in the way.
- **TAOM requirement:** cavalry should hit and run as a clean line, pass through, reform, and come
  again; in mixed-army Middle-earth battles where Rohirrim or Easterling cavalry support friendly
  Gondorian or Dol Guldur infantry, riders should route around their allies, not over them.
- **v1 (2026-05-06 to 2026-09-13) never worked.** Player reports: "cav stop in the middle of the
  enemy or don't react at all", "you have to double charge them (F3 twice)". Root causes, all
  verified on the installed v1.4.8 DLL, are recorded in
  [#586](https://github.com/haterade22/TAOM/issues/586) and
  [`rca-smart-cavalry-2026-09-13.md`](../reviews/rca-smart-cavalry-2026-09-13.md); the short form is
  under "Engine facts the design rests on" below.

## Architecture

### Design challenge

1. **Single-source MovementOrder side channel.** The player's F3 reaches
   `Formation.SetMovementOrder`. The feature must intercept just the cavalry path and stay out of
   everything else: the postfix bails in O(1) on non-player-team formations, AI-controlled formations,
   the feature toggle, and its own re-entered writes.
2. **The engine keeps issuing orders too.** `BannerBearerLogic` re-issues a formation's current order
   when a bearer dies; `Formation.Tick` substitutes a plain Charge when a `ChargeToTarget` target
   empties; `Team.Tick` issues Retreat to a routed side. The machine has to tell these from the
   player's own orders (see "Cancel and re-issue" below).
3. **Nothing may hold riders still.** Every state that holds a Move order has a dwell budget, and
   every exit that gives up hands the formation back to a vanilla Charge, so no code path can leave
   riders standing in or near the enemy.
4. **ADR-007.** The service holds only adapter interfaces and opaque target tokens; the adapters are
   the only layer that touches `Formation`, `MovementOrder`, `Team`, `Agent`.

### The cycle

```
player F3 on player-team cavalry (Patch31 postfix)
   |
   v
[reroute needed?] --yes--> Rerouting: Move to waypoint ------ arrived or 12 s ---------+
   | no                                                                                 |
   v                                                                                    v
Forming: SetPositioning(line 5 m ahead) + Move  --aligned or MaxLineUpSeconds-->  Charging: ChargeToTarget
                                                                                       |
                                              contact: dot(targetLive - centroid, dir) < 10 m
                                                                                       v
Reforming: hold the line  <--- arrived (10 m) ---  PassingThrough: Move to reform point, line facing back
   |                                                              |  10 s -> HandOff
   +--aligned or MaxLineUpSeconds--> nearest live enemy? --yes--> Forming (next cycle)
                                                          --no--> HandOff
```

**HandOff:** `IssueCharge()` (a vanilla free Charge) and state Idle. Reached from every give-up: no
enemy formation left, the engine refusing a Move (invalid world position), PassingThrough bogged down
for 10 s, the feature toggled off mid-cycle, or the target sitting on top of the formation so no line
can be drawn.

**Charge now:** a second F3 (or any charge order the postfix accepts) while a cycle is running does
not reset the line-up. It re-points the target and jumps straight to Charging.

**Cancel and re-issue:** any non-charge order from the player on a formation mid-cycle, and any order
at all on an `IsAIControlled` formation, cancels the cycle without issuing anything, so the newer
order stands (toggle or no toggle; only starting a cycle needs the feature on). The postfix compares
the order in force before the call (captured by a prefix) with the incoming one: same kind, and for
a Move the same spot within 1 cm (the engine's own `AreOrdersPracticallySame` allows a metre, which
a player click could fall inside). An identical re-issue is engine housekeeping and does not cancel.

**Targeted charge (Patch31b):** the player's "charge THAT formation" is a plain Charge followed by
`SetTargetFormation(target)`. Patch31 starts the cycle at the nearest enemy; the postfix on
`SetTargetFormation` re-points it at the chosen one: a Forming line is redrawn toward it, a Charging
formation re-issues its charge at it.

**Dismount and empty:** a formation that stops being cavalry mid-cycle (riders dismounted or lost
their mounts) is handed back to a vanilla Charge; a formation that emptied has its cycle forgotten so
`HasActiveCycles` does not keep the tick alive for it.

### Per-state orders and exits

| State | Order issued on entry | Exit |
|-------|----------------------|------|
| Forming | `ApplyChargeLine(centroid + dir*5, dir, spacing)` then `IssueMoveTo(line)` (Move is `Hold` state: riders take their arrangement slots; Stop is `StandGround` and never forms a line) | aligned OR `MaxLineUpSeconds` -> Charging. Target gone -> new line at the nearest enemy; none -> HandOff. Move refused -> HandOff |
| Charging | `IssueChargeToTarget(token)`; `ChargeDirection` frozen at line time | `along = dot(targetLive - centroid, dir) < 10 m` -> PassingThrough. Target gone -> re-target nearest and re-issue; none -> HandOff. No timeout: a Charge-family order never leaves riders standing |
| PassingThrough | reform point = `centroid + dir*(max(along,0) + depth + ReformDistance)`, where `depth` is how far the target's riders extend past its centre along `dir` (`GetTargetDepthAlong`, so a deep column or a line turned sideways does not swallow the point); `ApplyChargeLine(reform, -dir, spacing)`; `IssueMoveTo(reform)` | centroid within 10 m -> Reforming. 10 s -> HandOff. Move refused -> HandOff |
| Reforming | none (the Move already holds the line, facing the enemy) | aligned OR `MaxLineUpSeconds` -> next cycle at the nearest live enemy, rerouting around friendlies like the first one; none -> HandOff |
| Rerouting | `IssueMoveTo(waypoint)` from `CavalryPathPlanner` | within 10 m OR 12 s -> new line at the live target; target gone -> nearest; none -> HandOff |

**Alignment** (`FormationAdapter.IsAligned`, decision in `LineAlignment`): the MEAN distance from each
rider to its own arrangement slot (`Formation.Arrangement.GetWorldPositionOfUnitOrDefault(unit)`) is
under `2 m + 8 m * (1 - strictness)`: 10 m at strictness 0, 4.4 m at the 0.7 default, 2 m at 1.
Arrangement-agnostic (Line, Skein, Wedge), meaningful only under a Move order, which is the only time
the machine asks. Mean rather than max so one straggler cannot hold the charge; the dwell budget is
the floor for everything else. Read from the arrangement, not `Formation.GetOrderPositionOfUnit`,
which is prefixed by MixedFormations' Patch30, sends detached units to their detachment frame, and
falls back to the rider's own position (distance zero) when a slot is off the navmesh.

**Geometry trade-off (frozen direction).** Contact is measured along the direction the line was drawn
with, so a flank charge registers when it crosses the plane through the enemy's centre, and the
reform point keeps the riders' lateral offset instead of converging on the enemy centre. An enemy
that moves laterally during the charge is followed by vanilla's own `ChargeToTarget` steering, so the
centroid follows it and the reform point still lands `ReformDistance` past the plane through the live
enemy centre. The point can land inside an enemy deeper than `ReformDistance` along that axis (a
column, or a line that turned 90 degrees mid-charge); raise the MCM distance for such fights.

### Component diagram

```
 Player charge order ----> Formation.SetMovementOrder
                              |  [HarmonyPrefix]  captures the previous order (__state)
                              |  [HarmonyPostfix] Patch31_FormationSetMovementOrder
                              |     gates: recursion guard, player team, IsAIControlled,
                              |            charge kind, identical re-issue, toggle, cavalry
                              v
   ICavalryChargeService.HandleChargeOrder  (Idle: reroute or line; mid-cycle: charge now)
   ICavalryChargeService.CancelCharge       (other orders, AI-controlled)
 ... then, for a targeted charge:
 OrderController -------> Formation.SetTargetFormation(target)
                              |  [HarmonyPostfix] Patch31b_FormationSetTargetFormation
                              v
   ICavalryChargeService.RetargetCycle      (re-point the running cycle at the chosen target)
                              ^
 SmartCavalryAIMissionBehavior.OnMissionTick  (every frame, every player-team cavalry formation)
                              |
                              v
   ICavalryChargeService.Tick  -> Update<State>  -> ICavalryCommandAdapter (Move / ChargeToTarget /
                                                       Charge / SetPositioning, all under the
                                                       recursion guard)
                                                  -> IBattlefieldQueryAdapter (nearest enemy,
                                                       ground height, friendlies)
```

## Configuration

All knobs live in MCM under **Battle Tactics / Smart Cavalry** (GroupOrder 22). Every one is
`RequireRestart = false`.

| MCM key | Type | Range / Default | Effect |
|---------|------|-----------------|--------|
| `EnableSmartCavalryAI` | bool | false | Master toggle. Off = vanilla. Turning it off mid-cycle hands any running cycle back to a vanilla Charge on the next tick. |
| `SmartCavalryAvoidFriendlies` | bool | true | Reroute around friendly non-cavalry formations on the charge line, and nudge riders away from friendly infantry within 3 m (player-commanded formations only). |
| `SmartCavalryChargeStrictness` | float [0..1] | 0.7 | Alignment tolerance for Forming and Reforming: mean slot distance under `2 + 8*(1-s)` metres. |
| `SmartCavalryReformDistance` | float [10..80] m | 25 | Metres past the enemy's plane where the riders pull up and reform. |
| `SmartCavalryLineSpacing` | float [0.8..3.0] | 1.2 | Passed to `Formation.SetPositioning` as `unitSpacing` after `Math.Round`, so it acts as an absolute spacing index (1, 2 or 3), not a multiplier. Known defect, out of #586's scope. |
| `SmartCavalryMaxLineUpSeconds` | float [1..15] s | 4 | Longest Forming or Reforming holds before proceeding regardless of alignment. The floor that keeps a line-up from ever freezing the formation. |
| `SmartCavalryDebug` | bool | false | Also log every intercepted charge order and the resulting state to the TAOM log file. State transitions are logged regardless. Nothing is drawn on the HUD. |

### Dead-setting audit

| Setting | Consumer |
|---------|----------|
| `EnableSmartCavalryAI` | `Patch31` postfix, `SmartCavalryAIMissionBehavior.OnMissionTick`, `CavalryChargeService.HandleChargeOrder` and `.Tick` (the disabled-mid-cycle hand-off) |
| `SmartCavalryAvoidFriendlies` | `CavalryChargeService.HandleChargeOrder` (path planner) and `SmartCavalryAIMissionBehavior.OnMissionTick` (collision avoidance) |
| `SmartCavalryChargeStrictness` | `CavalryChargeService.UpdateForming` and `UpdateReforming` |
| `SmartCavalryReformDistance` | `CavalryChargeService.UpdateCharging` (contact) |
| `SmartCavalryLineSpacing` | `CavalryChargeService.LineSpacing()`, used by `InitiateLineCharge` and the reform line |
| `SmartCavalryMaxLineUpSeconds` | `CavalryChargeService.UpdateForming` and `UpdateReforming` |
| `SmartCavalryDebug` | `Patch31` postfix (one log line per intercepted order) |

Settings-consumed tests in `CavalryChargeServiceTests` pin each read. The knob also counts toward the
co-op settings fingerprint (225 `TaomSettings` properties, 180 simulation-relevant; see
[coop-interop.md](coop-interop.md)).

## Engine facts the design rests on (v1.4.8, installed DLL)

| Fact | Where | Consequence |
|------|-------|-------------|
| `MovementOrderStop` is `MovementStateEnum.StandGround`, and `GetOrderPositionOfUnit` returns the rider's OWN position for StandGround | `MovementOrder.cs:143-144`, `Formation.cs:1262` | A Stop never moves riders into a line. v1 issued Stop for its line-up and its reform; riders froze where they stood. Line-ups are Moves. |
| `MovementOrderMove` is `Hold`; `GetOrderPositionOfUnit` returns the arrangement slot; `OnApply(Move)` calls `SetPositioning(position)` only | `MovementOrder.cs:145-147, 690-691`, `Formation.cs:1258-1259, 1206-1211` | `SetPositioning(pos, dir, spacing)` then Move keeps the direction and spacing and forms the line. |
| `Vec2.RightVec()` is `(y, -x)` | `Vec2.cs:277` | v1's `IsAligned` measured spread ALONG the line and required 1.5 m; impossible for more than two riders. |
| `ChargeToTarget` is inapplicable when the target has no units; the order degrades to a hold at the order position, and `Formation.Tick` substitutes a plain Charge | `MovementOrder.cs:816-817, 536-538, 1107-1115`, `Formation.cs:2291-2295` | The service re-targets on target death itself; the engine's substitute Charge re-enters Patch31 and lands in "charge now", the same re-target. |
| `Formation.IsAIControlled` is false for every formation of a player general who has not delegated; `Team.DelegateCommandToAI` sets it true for all; `Formation.RemoveUnit` sets it true when a player formation empties and nothing resets it on refill | `Formation.cs:172, 2201-2203`, `Team.cs:447-473` | The machine stays out of AI-commanded formations. A player formation that emptied mid-battle stays AI-commanded (vanilla), so the machine stays out of it for the rest of the battle too. |
| `BannerBearerLogic.FormationBannerController.RepositionFormation` re-issues the current order | `BannerBearerLogic.cs:157` | Fires on every bearer death. Without the identical-re-issue test it cancelled a running cycle. |
| `FormationAI.TickOccasionally` issues the AI's movement order only when `IsAIControlled` (there is no `SetCurrentOrder`) | `FormationAI.cs:284-290` | The team AI never orders a player-commanded formation, so a non-charge order on one is the player's. |
| The player's targeted charge is `SetMovementOrder(MovementOrderCharge)` THEN `SetTargetFormation(target)`; `MovementOrderChargeToTarget` is not on the player path | `OrderController.cs:812-817` | Patch31 sees a plain Charge and starts at the nearest enemy; Patch31b, a postfix on `SetTargetFormation`, re-points the cycle at the formation the player chose. |
| Every `SetMovementOrder` ends with `SetTargetFormation(null)`, which pushes target index -1 to every rider | `Formation.cs:714, 222-238` | A `ChargeToTarget` alone is a free charge at anyone. `IssueChargeToTarget` re-sets the native target after the order, as vanilla does. |
| `Formation.Tick` re-applies the retained `FacingOrder`'s direction through `SetPositioning` every tick | `Formation.cs:2311-2314` | A direction written through `SetPositioning` alone lasts one tick. `ApplyChargeLine` installs `FacingOrderLookAtDirection` with every line. |
| `ColumnFormation.GetWorldPositionOfUnitOrDefault` is null for every unit | `ColumnFormation.cs:663-666, 727-730` | Under a Column arrangement alignment cannot be measured; `IsAligned` returns false and the line-up budget decides. |
| `Formation.SetMovementOrder(MovementOrder input)`: Harmony binds `input` by name; `MovementOrderEnum.Charge = 2`, `ChargeToTarget = 3`, `Move = 7`, `Stop = 9` | `Formation.cs:685`, `MovementOrder.cs:12-25` | Pinned by `SmartCavalryAIBindingTests` together with every other member the postfix and adapters read. |

## Key files

| File | Purpose |
|------|---------|
| `Main/Features/SmartCavalryAI/CavalryChargeService.cs` | The state machine: entries (`HandleChargeOrder`, `ChargeNow`, `BeginReroute`, `InitiateLineCharge`), per-state ticks, `StartNextLineCharge`, `HandOff`, `Cancel`, `HasActiveCycles`. Logs every transition. |
| `Main/Features/SmartCavalryAI/LineAlignment.cs` | Pure alignment decision and tolerance curve. |
| `Main/Features/SmartCavalryAI/CavalryPathPlanner.cs` | Pure reroute math (unchanged in v2 except the NaN-safe distance gate). |
| `Main/Features/SmartCavalryAI/SmartCavalryAISettingsProvider.cs` | `TaomSettings.Instance` wrapper with `SettingClamp` defaults. |
| `Main/Features/SmartCavalryAI/SmartCavalryRecursionGuard.cs` | Thread-local depth counter raised around every write the feature issues. |
| `Main/Features/SmartCavalryAI/Models/CavalryFormationState.cs` | Per-formation state: `State`, `StateEnteredTime`, `TargetToken`, `ChargeDirection`, `ReformPoint`, `ReroutePoint`. |
| `Main/Features/SmartCavalryAI/Hooks/Patch31_FormationSetMovementOrder.cs` | Prefix (previous order) + postfix (gates, target resolution, cancel, hand-off to the service). |
| `Main/Features/SmartCavalryAI/Hooks/Patch31b_FormationSetTargetFormation.cs` | Postfix on `SetTargetFormation`: the second half of the player's targeted charge, re-points a running cycle. |
| `Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs` | Per-frame driver, friendly collision avoidance, `OnEndMission` cleanup. |
| `Main/Adapters/IFormationAdapter.cs`, `FormationAdapter.cs` | `RepresentativeIsCavalry`, `IsAIControlled`, `CurrentPosition`, `IsAligned`. |
| `Main/Adapters/ICavalryCommandAdapter.cs`, `CavalryCommandAdapter.cs` | `IssueMoveTo` (bool), `IssueChargeToTarget` (order + native target), `IssueCharge`, `IssueStop`, `ApplyChargeLine` (positioning + facing order), `IsTargetAlive`, `TryGetTargetPosition`, `GetTargetDepthAlong`. |
| `Main/Adapters/IBattlefieldQueryAdapter.cs`, `BattlefieldQueryAdapter.cs` | `IsFieldBattle`, `TryGetNearestEnemyFormation`, friendlies, ground height, nearby agents. |

## Tests

`TAOM.Tests/Features/SmartCavalryAI/`, MSTest + NSubstitute, 106 tests:

| File | Covers |
|------|--------|
| `CavalryChargeServiceTests.cs` (77) | Every entry, transition, timeout and exit in the table above; charge-now from Forming, Reforming, Rerouting and Charging; the targeted charge's re-point (`RetargetCycle`) while Forming and while Charging; cancel, AI-controlled cancel, no-longer-cavalry hand-off, disabled-mid-cycle hand-off and the ownership order between them; live-target contact, flank offset, lateral enemy movement, target depth (and a NaN depth); repeat cycles rerouting around friendlies; NaN target; every setting read. |
| `LineAlignmentTests.cs` (12) | Tolerance curve at 0 / 0.7 / 1 and out of range; mean vs max; NaN and infinity fail the gate. |
| `CavalryPathPlannerTests.cs` (13) | Reroute filters and waypoint math; NaN target. |
| `SmartCavalryAIBindingTests.cs` (4, `BindingVerification`) | `SetMovementOrder`'s and `SetTargetFormation`'s parameter names; enum values 2/3/7/9; every engine member the two postfix bodies and the adapters read (arrangement slots, facing order, native target, re-issue comparison, team queries). |

`SharedMovementOrderPostfixTests` (CompanionTactics) pins that Patch31 stays in the shared
`Patch_MissionTime_SetMovementOrder` category and never touches Patch35's stance state.

## Known limitations

- **Not smoke-tested in game yet.** The state machine is pinned by unit tests against mocked
  adapters; the engine semantics are verified by decompile and by the binding tests. The checklist
  below is owed before the toggle defaults on.
- **An emptied player formation stays AI-commanded** (vanilla `Formation.RemoveUnit`), so refilling
  it through the troop-transfer screen does not bring it back under the machine until the next battle.
- **Frozen charge direction** (see the geometry note). The reform point clears the target's riders
  along the charge axis by `ReformDistance`, measured from the rider that extends furthest; a
  formation that moves after contact is not re-measured.
- **A player Move within 1 cm of the machine's own Move point** reads as a re-issue and does not
  cancel; the machine's next order then displaces it. A click does not land within a centimetre.
- **Column arrangement:** the engine reports no slot for any unit, so alignment cannot be measured
  and the line-up runs to `MaxLineUpSeconds` every time.
- **Reforming holds the line facing the enemy under a Move order**, so between arrival and the next
  charge the riders defend rather than attack. Bounded by `MaxLineUpSeconds`.
- **`SmartCavalryLineSpacing` is an absolute spacing index**, not the multiplier its label claims.
- **Toggling the feature off mid-cycle issues one last order** (the vanilla Charge the player asked
  for with F3) so the riders are not left on a Move nobody lifts.
- **Single-player only.** `Mission.PlayerTeam` is null in spectator and custom-battle missions; the
  postfix and the behavior bail out.

## In-game smoke (owed)

MCM `Enable Smart Cavalry AI` on, `Smart Cavalry Debug Mode` on, field battle with 20 or more riders:

1. One F3: the order UI reads Move, the riders shuffle into line, and within 4 s the UI reads Charge.
   No second press.
2. The riders ride through, pull up about 25 m past the enemy facing back, line up, and charge again.
   `rgl_log.txt` shows `[SmartCavalryAI]` lines Forming -> Charging -> PassingThrough -> Reforming ->
   Forming.
3. F1 Follow-me mid-cycle: the riders come to you and nothing pulls them away (a cancel is logged).
4. Kill or rout the targeted formation mid-charge: the riders re-target instead of riding back to hold.
5. F6 delegate to the AI, or an enlisted battle: no line-ups, no `[SmartCavalryAI]` order lines.
6. Toggle the feature off: vanilla F3.

## Performance

`OnMissionTick` iterates the player team's formations every frame; with the feature off and no cycle
running it returns after one settings read and one lock. Per cavalry formation per frame: two cached
adapter lookups, one dictionary lookup under the service lock, and in Forming/Reforming one pass over
the riders reading their arrangement slots (no allocation, no engine call through Patch30). The
postfix runs once per order: for a non-charge order on a mid-cycle formation it costs one struct
compare and one lock; for a charge order it scans the enemy formations once for the nearest.

## History

- 2026-09-13, #586: state machine v2. Move-based line-up, arrangement-slot alignment, frozen-direction
  contact, reform point past the live enemy, hit-and-run loop, dwell budgets and HandOff, charge-now
  on a second F3, cancel on other orders and on AI-controlled formations, identical-re-issue test,
  `SmartCavalryMaxLineUpSeconds`. Deep review (5 agents) and Codex pass; RCA
  [`rca-smart-cavalry-2026-09-13.md`](../reviews/rca-smart-cavalry-2026-09-13.md).
- 2026-07-16, #349: open-field-only gate (`IsFieldBattle`) on the service, the tick and the postfix.
- 2026-05-13, #155: `_lock` around the per-formation state.
- 2026-05-07: Patch31 deferred into the shared `Patch_MissionTime_SetMovementOrder` category
  (`MovementOrder.cctor` reads `Mission.Current.CurrentTime`).
- 2026-05-06, #112: the port. Codex adversarial review fixed NaN propagation through `Clamp` and the
  MixedFormations cavalry hand-off.

## GitHub issues

- [#586](https://github.com/haterade22/TAOM/issues/586) state machine v2 (this design).
- [#349](https://github.com/haterade22/TAOM/issues/349) siege guard.
- [#112](https://github.com/haterade22/TAOM/issues/112) the port.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
