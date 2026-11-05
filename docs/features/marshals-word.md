# Marshal's Word

## Overview

Marshal's Word gives the player richer orders for companies (formations) in a battle. This file covers the two stages built so far, of
the six in [marshals-word/build-spec.md](marshals-word/build-spec.md). **M1, Orders in turn:** hold the queue key (Give the order in turn,
default Left Shift) while giving an order with the order panel open, and the company carries it out after its current orders. Flags on the
ground show the road ahead. **M2, Keep the array:** several selected companies given one click or drag move as one body, keep their places
relative to each other, and walk at paces that bring them in together. Stages M3 (archers loose on the word), M4 (mark and outlines), M5
(hollow square, hold the line, spacing repair) and M6 (the panel entries) are not built, and their keys and settings are not registered.

## Why This Exists

- **Vanilla behavior:** an order runs the moment it is given and replaces the company's current one. A drag for several companies lays them
  out side by side whatever shape they stood in.
- **TAOM requirement:** TAOM fields large armies of mixed companies on large fields. The player needs to chain a march, a turn and a charge
  without waiting beside the panel, and to move a battle line without rebuilding it.
- **Without this feature:** the player either babysits every company or re-orders each one after each arrival.

## Architecture

### Design Challenge

- A queued order must be issued later through the same code a player's click uses, or `BeforeSetOrder`, `AfterSetOrder`, the order events and
  every TAOM patch on `Formation.SetMovementOrder` (Patch31, Patch35) would not run. The engine has an `OrderController` with no owner on every
  team, the master controller (`Team.MasterOrderController`), which vanilla itself uses to order a team from mission logic. The queue issues
  on it. Nobody may construct a controller for the player's team: its constructor hands every formation to the player owner.
- The layout of a drag is private static arithmetic that both the preview and the commit use (`SimulateNewOrderWithPositionAndDirectionAux`),
  so one prefix there changes both, and vanilla's commit code still runs.
- The pace a soldier walks at is a multiplier from `HumanAIComponent.GetDesiredSpeedInFormation`, asked on the AI thread and the workers.

### Solution Approach

`MarshalView` is a `[DefaultView]` in TAOM.Client. It builds one `MarshalSession` in `OnMissionScreenInitialize` and never again; a null
session field is final, because the mission still calls `OnMissionTick` after `OnMissionScreenFinalize` (MissionScreen.cs:4241-4245 only
unregisters from the screen; Mission.cs:3751-3754 walks the behaviours), and `RemoveMissionBehavior` leaves the view in the screen's list
(Mission.cs:4708-4725). Both teardown paths (`OnMissionScreenFinalize` and
`OnRemoveBehavior`) call one safe-to-repeat `Finish()` and drop the session. The view reads the two held keys in the screen tick, only with
the order panel open and only while the session is live, and forwards the mission tick and the mode change.

- **Going live** (`MarshalSession`): at the first mission tick, and after each mode change until the answer is yes, it asks
  `MarshalEnabled`, then `SeatMissionPolicy` (a Battle or Hideout kind with no foreign camera) and the network fact (`GameNetwork.IsSessionActive` or a live co-op session, `SeatMissionClassifier.InNetworkSession`). While the
  answer is yes and there is no player team yet, each tick tests the team. Once live it stays live unless the fault latch ends it.
- **The fault latch:** the screen tick, the mission tick and the capture each run in one try. A throw clears `MarshalHooks`, writes one
  `fault` fail line, shows one message, and a capture then answers "not queued", so vanilla runs the order. The other entries have no try of
  their own: `ArraySolved`, `ArrayFault` and `MarchRequested` run inside the array prefix's try or catch, and the team-change and
  direct-order callbacks call only list work, the outcome log and a pace publication, none of which throws.
- **Capture** (`MarshalOrderCapturePatch`, Patch141): a prefix on each of the six `OrderController` order methods. It returns true at its first
  test unless the session is live, the queue key is down with the panel open, and the instance is the player's own controller. Otherwise
  `MarshalQueueDriver.Capture` stores the order as a step (reading only: the capture runs in the screen tick) or declines with a reason.
  An order on the allow list ends at once, on arrival, when its target company is gone, or when an entity order is spent.
- **The queue** (`MarshalOrderQueue`, pure, in `Domain/`): one FIFO per company, a step shared by several companies in each list. A step
  starts only when it heads every member's list. A company that left (no units, or under AI control) leaves every step. A direct player order
  to a company clears its queue, except firing, riding, arrangement and form orders, and a change of the player team clears every queue.
- **The driver** (`MarshalQueueDriver`, once per 0.25 s of mission time in `OnMissionTick`): re-tests every member, adds an implicit wait for
  a Move already under way, selects the members on the master controller, issues the stored call, clears the selection, and finishes steps by
  arrival (`CurrentPosition` within 3 m of the order point), target gone, entity order ended, or patience
  (`max(25 s, 2.5 * distance / speed)`, the largest over the members).
- **The array** (`MarshalArrayPatch` and `MarshalArraySolver`): for two or more companies of the player's team in a vertical layout outside
  Deployment, the solver (pure, doubles inside) turns a click into the same offset for every company, and a drag into a turn, a stretch of
  widths and spacing, a shoulder pass, and a placement that puts the front of the group on the drawn line. `MarshalArrayLayout` then builds
  the outputs with public engine calls only (the position from `Mission.GetStraightPathToTarget`, the fit loop over the public
  `GetUnitPositionWithIndexAccordingToNewOrder` overload that Patch92 scopes). It solves first and writes no output until everything is built.
- **The march** (`MarshalMarch`, `MarshalMarchDriver`, `MarshalPacePatch`): a commit of two or more companies starts a march. On each slow
  pass each company's pace is `clamp(eta / longest, 0.3, 1)`, published as one immutable table by one reference write. The postfix on
  `GetDesiredSpeedInFormation` multiplies the multiplier by the pace (clamped to 0.2 to 1) and takes no lock, allocates nothing and logs nothing.
  The march ends, with a skip line that names the reason, when:
  - the longest time left is under 2 s (the one end that can write a verdict, see How to verify; a march whose companies changed between
    two passes writes `company-left` and no verdict);
  - a company leaves its Move: its order is no longer a Move, or the AI took it, or its order point moved more than 1 m from the stored one.
    Charge, Stop and Retreat carry no position and leave the order point where it was (MovementOrder.cs:178 and Formation.cs:862), so
    `MarshalOrderAdapter.ReadMarch` reports such a company with the largest possible shift;
  - fewer than two companies are left (`ReadMarch` leaves out a company with no units, because the team no longer ticks it, Team.cs:616-622);
  - it runs past `max(25 s, 2.5 * first longest)` from its first pass, the queue's own patience, so one stuck company does not keep the
    others slow for the rest of the battle;
  - a reading is not a finite number (a missing formation reports one): the march ends with reason `invalid` and no verdict;
  - a new array commit takes over (`replaced`), the Arrive Together setting goes off (`setting-off`), or the player's team changes
    (`team-changed`, because each company number names a formation of the old team).

  The spread the verdict measures counts the companies of the latest pass, so a company that was wiped out leaves the march and its last
  estimate is forgotten. The proof values come from the first pass, which still counted that company, so a march whose companies changed
  between two passes (one left, one left and came back, or one took another's place) writes `reason=company-left` and no verdict.

### Component Diagram

```
MarshalView (screen tick: keys, panel; mission tick; both teardowns)
        |
  MarshalSession ---- MarshalHooks (statics the patches read) <---- MarshalOrderCapturePatch / MarshalArrayPatch / MarshalPacePatch
   |        |                                                              |                 |
   |  MarshalQueueDriver --> MarshalOrderQueue (Domain)         MarshalArrayLayout --> MarshalArraySolver (Domain)
   |  MarshalMarchDriver --> MarshalMarch (Domain)
   |        |
IMarshalScreenAdapter     IMarshalOrderAdapter (master controller, formations, paces)
(messages, flags)         MarshalOrderPayload (the captured call)
```

## Configuration

MCM group "Marshal's Word", all defaults on, read once a frame through a lazy accessor (`MarshalSettingsProvider`).

| Setting | Default | Effect |
|---|---|---|
| `MarshalEnabled` | on | Read when the session decides whether to go live. Off, none of this acts from the next battle on; a battle already running keeps the feature (build-spec Deviations, row 3) |
| `MarshalKeepArray` | on | A click or drag moves several companies as one body. Off, vanilla lays them out |
| `MarshalArriveTogether` | on | Companies ordered together walk at paces that bring them in together. Needs Keep the Array |

Keys (Options > Keybindings > Action, category `TaomMarshalHotKeyCategory`, ids from 540): **540 Give the order in turn**, default Left Shift,
held. **542 Keep the widths**, default Left Alt, held: a drag turns the group without stretching it. Marshal's Word reads both only while
the order panel is open; they are vanilla's keys too, so they still act with the panel closed (zoom, banners), and Left Alt has one more
vanilla reader, `GauntletInformationView.cs:61`, which extends the on-screen message timer. A held key is the contract's accepted exception to the rule that a default key stays clear of every key the scene layer reads:
Left Shift is also game key 24 (Zoom), so on the hero's camera holding it zooms; Left Alt is game key 5 (ShowIndicators), so it shows the
banners. Ids 541 and 543 to 545 are reserved for the later stages and registered by them.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MarshalsWord/Domain/` | Pure rules: `MarshalOrderQueue` and `QueueStep`, `MarshalArraySolver`, `MarshalMarch`, `MarshalChecks`, `MarshalTuning` (BCL only, gated) |
| `Main/Features/MarshalsWord/MarshalsWordModule.cs` | Feature module: services, the keys, `Patch141_MarshalsWord` at the first mission |
| `Main/Features/MarshalsWord/MarshalQueueDriver.cs`, `MarshalMarchDriver.cs`, `CaptureResult.cs` | The slow passes, over `IMarshalOrderAdapter`, and what a capture answers |
| `Main/Features/MarshalsWord/MarshalHooks.cs`, `MarshalPaceTable.cs`, `IMarshalHookSink.cs` | The statics the patches read, the pace table, the sink interface |
| `Main/Features/MarshalsWord/Hooks/` | The three patch classes, and `MarshalArrayLayout.cs`, the static helper that only the array patch calls |
| `Main/Adapters/IMarshalOrderAdapter.cs`, `MarshalOrderAdapter.cs`, `MarshalOrderPayload.cs`, `MarshalCall.cs` | The engine side |
| `Client/Features/MarshalsWord/MarshalView.cs`, `MarshalSession.cs`, `MarshalScreenInput.cs` | The view, the session and what one screen frame read |
| `Client/Adapters/IMarshalScreenAdapter.cs`, `MarshalScreenAdapter.cs`, `MarshalFlagPoint.cs` | Messages and the flag pool |
| `tools/ingame_checks/0.toml` | The draft manifest, shared with the Seat of Seeing |

## Dependencies

- `SeatMissionPolicy` and `SeatMissionClassifier` (Seat of Seeing): the mission kind and the foreign-camera rule. The Seat is not required at run time.
- `MissionThreadGuard`, `IOutcomeLog`, `OutcomeCounter`.
- Patch31, Patch31b, Patch35, Patch30 and Patch92, read below.

## Tests

`TAOM.Tests/Features/MarshalsWord/`: `MarshalOrderQueueTests` (the lists, the barrier, drop-out, clearing, patience), `MarshalArraySolverTests`
(golden: a click keeps every offset, a flank-to-flank line changes nothing, the front lies on the line, a 90 degree turn, a stretch keeps the
rear behind, a clamp does not shift the rest, no overlap after the pass, finite outputs for zero, huge and one-company inputs),
`MarshalMarchTests` (paces, the five ends of a march, the spread allowance, and a simulated march for each walk of the proof table), `MarshalQueueDriverTests`,
`MarshalMarchDriverTests`, `MarshalSessionTests` (live, arm, fault latch and the flags it hides, both teardowns, events, flags),
`MarshalOrderPayloadTests` (the allow list row by row, the widths key a drag records, the flag height), `MarshalPatchTests` (bindings,
parameter names, drive each prefix, the pace table), `MarshalAdapterPinTests` (the engine-facing adapter statements a test cannot run, each
with its engine line), `MarshalsWordWiringTests` (the Domain gate, the module, the view's forwards), `MarshalsWordChecksRenderTests` (the
[Check] lines the code writes against the fixtures and the manifest), `TaomMarshalHotKeyCategoryTests`, `MarshalSettingsProviderTests`.
`tools/tests/test_ingame_verdict.py` holds `MarshalsWordManifestTests` and nineteen fixtures.

## How to verify in game

**Preconditions:** a single-player field battle (a Custom Battle works), the order panel, a build with this feature, `Marshal's Word` on.

1. Select one company, open the panel, hold Left Shift and give a Move, release, hold again and give a second Move. Flags mark both points.
   The log shows `[Check] id=taom.marshals-word.captured`, then `started` (one line per mission and end kind, so the first step only), and a
   `finished ... reason=arrival end=Arrival` line for each step, two for two Moves, each ending `behind=0` or `behind=1`. The `finished`
   check passes on a line that ends `behind=1`: the last member of the Move ahead of that step arrived while that step had not started. A lone
   Move writes `behind=0`, and so do a step behind an instant, dropped or timed-out step, a step behind a Move that one member arrived at and
   another did not, and the implicit wait ahead of a step behind a walk, so lone Moves read NOT REACHED.
2. Queue a Move, then give the company a direct Move without the key: `[Check] id=taom.marshals-word.cleared ... steps=1`.
3. With no company selected, hold the key and give an order: it runs at once and `declined ... reason=none-selected` is logged.
4. Select two companies and click once, then drag once, on the ground: `array ... kind=click` and `array ... kind=drag`. Put the two
   companies in Line at clearly different distances from the drag, about 2.5:1 (100 m and 40 m): a `march` line and later a
   `march-spread ... spread=<s> longest=<s> shortest=<s>` line follow. `longest` and `shortest` are the first pass's largest and smallest
   time to come within the 3 m arrival slack, and `spread` is the time between the first and the last company to do so. The check passes
   when `spread` is at most `2 + 0.25 * longest + max(0, longest - shortest / 0.3)`. It proves the pace only when `longest` is at least
   15 s and `longest - shortest` is over 1.4 times that allowance, so that a group with no pace would clearly fail; equal walks, walks such
   as 80 m and 10 m, or a far walk under 15 s write a skip line with `reason=no-test` and the check reads NOT REACHED. Riders, and anything above 6 m/s,
   need longer walks than 100 m and 40 m to prove anything.
5. `python tools/ingame_verdict.py --issue 0` reads this session. The manifest is shared with the Seat of Seeing, so a session of one
   feature alone exits 4, because the other feature's checks are NOT REACHED; `--sessions 2` over one session of each feature exits 0.
6. The field trial steps (`queue_two_moves`, `direct_order_clears`, `array_keeps_offsets`, `arrive_together`) are written by the orchestrator once the
   harness lands; `MarshalView` gives them `TrialHoldQueue`, `TrialHoldWidths` and `Session`. Nothing has run in the game.

## Co-op: the four questions

Marshal's Word is off whenever a network session is active (the engine's flag or a live co-op session, `SeatMissionClassifier.InNetworkSession`), so the answers are short. **Presence** (`IsCoopActive`): the patches are applied
for every player, and each returns at its first test unless the session is live, which it never is in a session. **Session and role:** no
session means the local machine owns the whole simulation; a session stops the feature before it begins. **Dedicated server:** no screen, so no
view and no session; the three patches are applied but see `IsLive` false. **Whose party:** the player's own team in a single-player mission.
`MarshalView` is a `Local` row in `CoopMissionRoles`. The three settings are listed as non-simulation in `CoopSettingsRelevance`.

## Accepted limits and traps

- **A scripted order queues.** With the key held and the panel open at that moment, an order a mission issues on the player's controller
  (a hideout ambush's own charge, `HideoutMissionController` and `LordsHallFightMissionController`) is captured like a click.
- **Listeners that miss a step.** A step issued on the master controller does not fire the player controller's `OnOrderIssued`. So a step
  that starts during a victory cheer does not end the cheer (`AgentVictoryLogic`), one that starts with the panel open leaves the panel's
  active icons on the old order until it reopens, and a step that charges does not raise the battle music (`MusicBattleMissionView`,
  MusicBattleMissionView.cs:66, 69-78). `Team.OnOrderIssued` does see steps.
- **Queued orders are silent.** A click ends in `AfterSetOrder`, which for a controller with an owner calls `PlayOrderGestures`
  (OrderController.cs:706): one `Owner.MakeVoice` line per order type (437-510), the hero's voice. The capture skips the whole method, and a
  step runs on the master controller, which has no owner, so a step gets no voice, neither when it is given nor when it starts.
- **A direct order by any other route** (a console command, another mod calling `Formation.SetMovementOrder`) fires no event on the player's
  controller and so does not clear the queue; the queue then orders the company again on its next step.
- **Mixed companies.** Patch30 answers the slot of a soldier with the mixed layout for any laid-out formation in a field battle, so for a
  company of melee and ranged the array preview and the real slots can differ. A pure infantry company agrees. A stretched mixed company
  may also read about 3.7 m short of its order point and end its step by patience: the mixed slots keep the file count of the first layout
  (LayoutPositioner.cs:27-29, FormationLayoutService.cs:272-273), so for 120 men first laid out 30 m wide and stretched to 60 m the average
  depth is about 5.3 m on mixed slots against 1.6 m in vanilla's arrangement, over the 3 m arrival slack. That is arithmetic from the read
  constants, UNVERIFIED; a field-trial row looks for it.
- **The preview can differ from the commit after a shrink.** After a placement that had to shrink a company, the preview reads live widths
  (OrderController.cs:1206-1236) and the commit restores the controller's private records (1531, 1535, 1561, 1569). Vanilla's own preview has
  the same gap, and reading those records needs a reflection site for a rare case, so it stays a limit.
- **A wall line keeps the array.** A click or a drag on a wall section reaches `SetOrderWithTwoPositions` as a plain line order:
  `OrderFlag.cs:343` stops only on an `IOrderable`, and `WallSegment` is not one, so the placer takes `OrderTroopPlacer.cs:406, 411, 415`.
  The `MoveToLineSegment` case of `SetOrderWithOrderableObject` (OrderController.cs:1055-1068) has no producer in this engine: every
  `GetOrder` in `TaleWorlds.MountAndBlade.dll` returns `None`, `FollowEntity`, `Use`, `AttackEntity` or `Move`. So the takeover keeps the
  array on a wall too, and a rear company stays behind the line the player drew; the preview shows that before the click. Keep the Array
  off lays such a line as in the base game.
- **A click turns the group.** On a click, `MoveToLineSegment` turns every company to the largest one's direction when that company has one
  set (OrderController.cs:1594-1597). That is vanilla's choice.
- **Solver quality near a wall** is not proved: `GetStraightPathToTarget` from one begin point can clip several companies to one hit.
- **Other patches on the order methods.** A prefix that skips a method does not stop the postfixes on it (Harmony emits them after the skip). No TAOM patch targets `OrderController` (checked in `patch-targets.md`), so a captured order reaches no TAOM postfix until its step is issued; another mod's postfix on one of the six methods would run for the captured call as well.
- **The queue is not saved.** It ends with the mission.
- **The spread check uses the arrival slack.** "At the point" is within 3 m of the order point, and `longest`, `shortest` and the spread all
  use it: the first two are the first pass's times to come within the slack, and a company that has not reached it when the march ends
  counts at `now` plus its time to the slack, so the measure and its control mean one arrival. The pace, the end of the march and its
  patience stay on the time to the order point. This row cannot judge a march whose near company stopped short of the slack, because that
  company counts at the end of the march however long ago it stopped.
- **What the proof cannot separate.** The margin covers companies that beat their first read by one factor, up to about 1.35 on the walks swept,
  which is above the 1.25 a Line company can reach, and a far company at 1.1 with a near company at 1. A far company further above its read than the near one can still read PASS with no pace. So can a
  company in Shield Wall, Square or Circle that runs untidy, because it loses a restriction of 0.3 or 0.5 (`ArrangementOrder.cs:90, 94`). This
  check is for companies in Line.
- **A working pace can fail.** The pace equalizes the time to the order point from the first read, and the check measures the slack, so two
  cases can carry the spread over the allowance. A near company that reads up to about 1.5 m/s on a walk up to about 18 m can do so at its read
  or on either side of it. A far company under its first read with a near company over it, by about 1.37 between them, can do so on ordinary
  walks, most of all near 3:1. Such a FAIL is true of the arrival: the companies did not arrive together. Read `shortest`, the walk and the
  speeds before you blame the pace.
- **A rejoining unit can end a step early.** A line order skips a company with no undetached unit (OrderController.cs:994), and `Arrived`
  reads the undetached count so that such a step falls to patience. A unit that rejoins on the old order point makes `Arrived` true against
  that old point first, so the step writes `reason=arrival` and the next step starts with no walk. Patience starts that next step with no
  walk as well, so the rejoin changes how soon that happens, the word on the line and the patience count: a step that would have timed
  out counts as an arrival, so the rejoin can turn a patience FAIL into a PASS.
- **Interference read** (build-time, from the source, nothing run): Patch31 and Patch35 postfix `Formation.SetMovementOrder`, so each queued
  Move cancels a charge cycle and clears a stance, as a player's order does. A queued Charge or Advance with a target company calls
  `SetTargetFormation` (OrderController.cs:816, 823), so Patch31b re-points a cycle for it as for a player's. `SmartCavalryRecursionGuard` is raised only around the cavalry service's own calls, and those do not go through an
  `OrderController`, so neither side meets the other's prefix. `MixedFormationsMissionBehavior` and the action bar only read
  `PlayerOrderController.SelectedFormations`. The formation presets patch the Order of Battle screen and issue no order through any controller (searched for the controller, `SetOrder`, `SetMovementOrder` and `OnOrderIssued` under `Main/Features/CompanionTactics`). The console
  cheats issue no controller order. `EnlistmentBattleRoleMissionBehavior` takes command away with `SetPlayerRole(false, false)`, which puts
  every formation under AI control, so the queue drops every company. Patch92 is scoped by the public overload the array layout calls.

## Performance

A frame with no live session reads no key. A live frame reads two keys only with the panel open, copies two settings and, with the panel open,
rebuilds the flags at most four times a second while any step is queued (an empty queue draws once and stops). The capture prefixes cost a
field read each. The array prefix runs at 30 Hz while a drag is drawn with the mouse held (OrderTroopPlacer.cs:300, 799), with an
`OutcomeCounter` increment and no string; the queue driver returns at once while no company holds a step. The pace postfix is one volatile read when no march runs.

## Changelog

- 2026-11-04: stages M1 and M2 built from the contract. Not yet run in the game.
- 2026-11-04: first review applied: a march ends when a member leaves its Move, is emptied or runs past its patience; the spread check says
  `no-test` when the walks prove nothing; a queued drag keeps its widths key; the siege gate is gone (a wall line is a plain line order); a
  fault hides the flags; the wording of three texts changed.
- 2026-11-04: second review applied: the spread counts only the companies of the latest pass, so a wiped-out company no longer writes a false
  `march-spread` FAIL; `MarshalArrayAdapter` became `MarshalArrayLayout` in `Hooks/`; the queue driver skips an empty queue; a Move's or a
  line's flag point is read once; the wording and the gaps in the tests were repaired.
- 2026-11-04: re-review applied: the spread check measures its proof values and its estimates against one arrival, the 3 m slack; a
  proof needs a longest of 10 s and a 1.15 margin; a march that lost a company writes `company-left`; a change of team writes
  `team-changed`; the `finished` line says `behind=1` for a step queued behind another, and that one line is the check; two limits are named
  (a near company stopped short of the slack, a unit that rejoins on the old order point).
- 2026-11-04: third re-review applied: the `finished` line says `behind=1` when a member arrived at the Move ahead of a step that had not
  started, so a step behind an instant, dropped or timed-out step, or one that started at the same time, writes `behind=0`; the spread proof
  needs a longest of 15 s and a margin of 1.4, with the two engine numbers behind the margin named; a march whose companies changed between
  two passes (one left, one left and came back, one replaced another) writes `company-left`; the limits of the proof and of a working pace on
  a very slow, very short walk are named; the rejoin limit now says it can turn a patience FAIL into a PASS.
- 2026-11-04: fourth re-review applied: the hand-over waits for the last member of a shared step, so `behind=1` is written only when the last
  member of the Move ahead arrived; the reason for the 1.4 margin is corrected and no number changed; the two ways a working pace can fail
  are named; the spread row asks for companies in Line and for longer walks above 6 m/s.

## GitHub Issue

- **Issue:** none yet. The draft manifest `tools/ingame_checks/0.toml` is shared with the Seat of Seeing and waits for one issue number.
- **Status:** Open.
