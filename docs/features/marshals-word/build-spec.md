# Marshal's Word: build specification

This file is the contract for the Marshal's Word feature. It pins names, folders, default numbers and
engine seams, so that stages written by different agents fit together. When code and this file disagree,
fix the code, or record the change under "Deviations" at the end with the reason.

Its camera and control half, the Seat of Seeing, has its own contract in
[../seat-of-seeing/build-spec.md](../seat-of-seeing/build-spec.md). Sections 2 and 3 of that file (layers,
folders, build rules, how a TAOM game key is read) apply here too, with `MarshalsWord` in place of
`SeatOfSeeing` and `Marshal` in place of `Seat` in type names.

## 1. What the feature is

**Marshal's Word** gives the player richer orders for formations in a battle. A formation is called a
**company** in this file and in the code's domain types.

| Stage | Name in play | What the player gets |
|---|---|---|
| M1 | Orders in turn | Hold the queue key while giving an order, and the company carries it out after its current orders. Flags show the road ahead |
| M2 | Keep the array | Several selected companies move as one body: they keep their places relative to each other, and they arrive together |
| M3 | Loose on my word | Archers draw and hold until the player gives the word, or until enough of them are ready |
| M4 | Mark and show | A company can mark an enemy company as its target and keep its own movement order. Outlines show who is selected and who is marked |
| M5 | Hollow square, hold the line, small repairs | A square with a chosen width, a shield line that will not break ranks to chase, and spacing limits that a single rider cannot spoil |
| M6 | The panel | The new orders appear as entries in the order panel |

Every stage is off until the player uses a key or picks an order, except M2 and the M5 repairs, which have
their own settings.

Named here so that nobody builds them by accident, because they are not in this contract: a skill gate on
any order, a queue for siege engine use orders, a ground marker mesh, a flank clamp at terrain edges, and
any order state that survives a mission.

## 2. Where it runs

Marshal's Word needs a screen, a player team and a single-player mission, so one client view gates it all.

- `Client/Features/MarshalsWord/MarshalView.cs` is a `MissionView` with `[DefaultView]`. It gets a `Local`
  row in `CoopMissionRoles.cs`, the matching row in `ClientViewRows_MatchTheClientTypeNames` and the doc's
  table. See `BattleActionBarMissionView.cs:13-16` for why it must be a default view.
- **One session, built once.** `MarshalView` builds one concrete `MarshalSession` in `OnMissionScreenInitialize`, as `SeatView` builds its session, and `MarshalSession` lives
  beside the view. A null session field is final, and every view callback starts from that test. The view still gets `OnMissionTick` after `OnMissionScreenFinalize`
  (MissionScreen:4241-4245 only calls that hook and `UnregisterView`), so a session built lazily in a tick could begin again in a mission that is ending. No test fakes the
  session, so it has no interface. "The driver" in this file is the session's method that `OnMissionTick` calls.
- At the first `OnMissionTick`, and at the first `OnMissionTick` after each `OnMissionModeChange` until the answer is yes, the view reads
  `SeatMissionPolicy.Decide(SeatMissionClassifier.ReadFacts(mission, logger))` and keeps the answer until the next mode change. The mode
  change callback only sets a volatile flag, because the thread that raises it is not proved. The behaviour list is complete by the first
  tick, and the kind is read fresh because it depends on `Mission.Mode` (Seat contract section 4), so nothing here caches the kind. These
  reads are few, so the foreign-camera walk in `ReadFacts` needs no cache of its own. Do not reuse `SeatView`'s cached answer: it freezes
  the kind on the mode at its first read.
  Marshal's Word is live when `MarshalEnabled` holds,
  `SeatPermissions.Orders` holds (a Battle or Hideout kind with no other camera in the mission),
  `GameNetwork.IsSessionActive` is false and `Mission.PlayerTeam` exists. A mission can make its player team after the first tick with no
  mode change, so while the answer is yes and the session is not live, each `OnMissionTick` tests `Mission.PlayerTeam`, one managed read.
  The view then calls `MarshalSession.Begin()`.
  With no screen there is no view, so a dedicated server never runs any of this, except 9.3. Once live, the session stays live for the mission, unless the fault latch below ends it.
- **Teardown has two paths, and every stage here honours both:** `OnMissionScreenFinalize` (the end-mission path) and `OnRemoveBehavior`
  (`MissionState.OnFinalize` removes the behaviours with no `EndMission`; `docs/reference/engine/mission-and-missionbehavior-lifecycle.md`
  lines 100-123). Each hook calls one safe-to-repeat finish on the session, then sets the session field to null, because the screen can still
  tick a removed view. Anything that outlives a frame or that a patch reads clears in that finish: a flag pool, a held key, a static, an event
  handler. The finish makes no native call: the agents and the flags go with the mission.
- **One fault latch.** Every session entry (the mission tick, the screen tick, the capture) runs in one try. A throw latches `MarshalHooks.IsLive` off for the mission, clears the hooks, and writes
  one `IOutcomeLog` fail line, and in the capture it answers "not queued" so that vanilla runs. An order issued on the master controller can throw inside `Mission.Tick` (section 5.3, rule 2 shows one way), and `SeatSession` wraps
  each tick path the same way. The M2 prefix (6.1) and the native callback (7.2) keep their own latch, because each has a different safe state.
- **Command stays vanilla's question.** Pressed keys (543, 544, 545) act only on a selection, and they test `Mission.PlayerTeam.IsPlayerGeneral || IsPlayerSergeant`, the question
  `MissionOrderVM.CheckCanBeOpened` asks at 758. That already covers an enlisted soldier stripped of command, because `EnlistmentBattleRoleMissionBehavior.cs:78` calls `team.SetPlayerRole(false, false)`, so
  `BattleCommandPolicy` gains no consumer here. The capture and the M2 prefix need no test, because every formation is then under AI.
  Host Review keeps the feature, except the panel provider in stage M6.
- `MarshalHooks.IsLive` is what every patch reads first. It is a plain `volatile bool` static, and a patch does nothing when it is false. Two exceptions, each with its own gate: 9.1's two small prefixes test only the
  arrangement type, and 9.3's transpilers hold no test at all.
- **All main-thread work runs in two ticks of this view.** `OnMissionScreenTick` reads keys once per real frame
  and draws. It overlaps the AI job, so it touches no shared engine state and only writes TAOM fields that
  nothing off the main thread reads. The capture prefixes run inside it too, so they only read, and a request they store waits for the next `OnMissionTick`. `OnMissionTick` runs in the window where no agent job runs
  (Mission.cs:3540-3542, 3595-3600, 3750-3785), so it issues orders and adds components. Fast time repeats
  `OnMissionTick` in one frame, so nothing in it reads a key.
- Marshal's Word is off in a network session in this contract. Answer the four questions of
  `docs/features/coop-interop.md` "Four questions, not one" in the feature doc. 9.3 answers them for itself.

Layers follow ADR-007 and the Seat contract. `Formation` is sealed, so the domain types never see it. A company
is an `int`, `Formation.Index` (a public readonly field, Formation.cs:92), and adapters map it back through the
player team, which is why a change of player team clears every queue (5.3, rule 6). All rules live in `Main/Features/MarshalsWord/Domain/`, BCL only, with a gate test.

Patches live in `Main/Features/MarshalsWord/Hooks/` under category `Patch141_MarshalsWord`. Add a
`## Patch141_MarshalsWord` section to `docs/reference/harmony-patch-registry.md`
(`PatchCategoryNumberTests`). Read `docs/reviews/lessons/harmony-il.md` first. None of these patches names a
client-only assembly until M6. Harmony binds parameter names, so a binding test pins every target and name.
The feature module declares this category at `ApplyPhase.FirstMission`, as `FordsOfWarModule` does, because
`MovementOrder`'s type initializer reads `Mission.Current`, and a patch applied earlier can reach it through JIT
preparation (`Main/SubModule.cs:2117-2125`). Every prefix that can skip the original needs a stance in
`CoopVetoClassificationTests`, which fails hard without one. A new native write calls `MissionThreadGuard.NoteCall`.

**TAOM patches on the same engine members.** Read each before you build the stage that meets it:

| Engine member | TAOM patch | What it means here |
|---|---|---|
| `Formation.SetMovementOrder` | `Patch31` (prefix, postfix), `Patch35` (postfix) | Each queued step reads as a new order: `Patch31` cancels a charge cycle and `Patch35` clears a stance. That is the intended result of a player's order. The method can run off the main thread, and this feature only calls it from `OnMissionTick` |
| `Formation.SetTargetFormation` | `Patch31b` | A mark reaches it as a player target (8.1) |
| `Formation.GetOrderPositionOfUnit` | `Patch30` | Its prefix can answer with a mixed layout on workers, in field battles only (`docs/features/mixed-formations.md:34`). So the array preview and a soldier's real slot can differ in a mixed company. Accept this and name it in the feature doc. The slot clamp (9.2) reads the same member the soldiers follow |
| `Formation.UnitDiameter` | `Patch92` scope | 6.3 and 9.1 |

`Main/Features/CultureDoctrine/Hooks/Tactics/VolleyControl.cs` drives AI formations only, and it gives fire at will once on a player takeover. Read
it before M3, and keep the two apart by the AI test. `Main/Features/CompanionTactics/BattleActionBar/TroopStanceManager.cs` and
`Hooks/Behaviors/WallStances.cs` hold parked display stances with similar names. Do not reuse their names, and do not change what they map.

## 3. Keys

A new category `TaomMarshalHotKeyCategory`, same pattern and rules as the Seat's. Ids start at 540. Strings are
`str_key_name.TaomMarshalHotKeyCategory_<id>`, with the `(TAOM)` suffix.

| Id | Name | Default | How it reads | Stage |
|---|---|---|---|---|
| 540 | Give the order in turn | `LeftShift` | Held | M1 |
| 541 | Mark, do not move | `LeftControl` | Held | M4 |
| 542 | Keep the widths | `LeftAlt` | Held | M2 |
| 543 | Change the archers' fire | `Comma` | Pressed | M3 |
| 544 | Give the word | `M` | Pressed | M3 |
| 545 | Hold the line | `Period` | Pressed | M5 |

The pressed defaults follow the rule in Seat contract section 3: a default stays clear of every key the mission's scene layer reads. `N` and `I`
fail it, because they are the encyclopedia and inventory keys of `GenericCampaignPanelsGameKeyCategory` (39 and 38), which
`MissionSingleplayerViewHandler` reads in every campaign mission. `Comma`, `Period` and `M` are in none of the eight categories that section names. `M` and `Period` do appear in `DebugHotKeyCategory` (612, 664), which no scene layer registers. A test pins these three
pressed defaults against those eight categories, looping over every registered key, the same way as the Seat's.

**Held keys are the exception,** because a modifier is worth its side effects. All of these act only while the panel is open, none corrupts state, and each is accepted. The feature doc names them, and a `[[human]]` row judges them:

- **540, `LeftShift`,** is also game key 24 (`Zoom`). On the hero's camera, holding it zooms to full in about 0.2 s, and past 0.66 it also changes the mouse sensitivity (MissionScreen:3119, 2973-2976). The order ray follows the screen
  centre, which the zoom does not move. In the Seat, `UpdateCamera` does not run, so the view does not zoom, and the key flies faster instead.
- **541, `LeftControl`,** has three vanilla readers. A ground drag lays out horizontally, which also turns Keep the array off for that drag (OrderTroopPlacer:396). The number keys select no company (GauntletOrderUIHandler:415). A click on a card adds it to the selection
  (MissionOrderTroopControllerVM:617). So a player who selects with Control and still holds it on the release marks instead of charging.
- **542, `LeftAlt`,** is also game key 5 (`ShowIndicators`), so holding it shows the banners as well, which is a help.

Held keys read the bound key with the static `Input.IsKeyDown`, the way `ManyHammersCalls.ShiftHeld` does, and
only while `Mission.IsOrderMenuOpen` holds. Pressed keys test the static `Input.IsKeyPressed` first, then the view's own
`Input.IsKeyPressed`, which goes quiet behind a menu but gets a new array from `Input.GetClickKeys()` on each call (Seat contract section 3).
A frame with no live session reads no key. `FordsCommandHotkeys` also uses `M`, but the Fords mission has no Marshal's Word: it has
its own camera view, so `SeatMissionFacts.ForeignCamera` turns this feature off there too. Realm Borders uses `M` on the campaign map only.

## 4. Settings

A provider with a lazy accessor that joins `HotPathSettingsProvidersTests`. MCM group "Marshal's Word" in
`Main/Features/TaomSettings.cs`. Never flip a default under an unchanged name once it has shipped.

| Setting | Default | Stage |
|---|---|---|
| `MarshalEnabled` | true | all |
| `MarshalKeepArray` | true | M2 |
| `MarshalArriveTogether` | true | M2 |
| `MarshalOutlines` | true | M4 |
| `MarshalHollowSquare` | true | M5 |
| `MarshalSpacingRepair` | true | M5 |

## 5. Stage M1: orders in turn

### 5.1 The engine facts this rests on

- A player order reaches one of six public virtual methods on `OrderController`: `SetOrder` (232),
  `SetOrderWithAgent` (731), `SetOrderWithPosition` (756), `SetOrderWithFormation` (800),
  `SetOrderWithTwoPositions` (981) and `SetOrderWithOrderableObject` (1008).
- Each runs `BeforeSetOrder`, applies the order to `SelectedFormations`, runs `AfterSetOrder` and fires
  `OnOrderIssued`. `AfterSetOrder` (682-710) forces a movement update on each unit and spreads the decide times.
  No other path gives those for free.
- `Team.MasterOrderController` (Team.cs:102) is an `OrderController` with a null `Owner` that exists on every
  team, and vanilla uses it to order a team from a mission logic (`HideoutAmbushMissionController.cs:693-694`).
  With a null owner `IsFormationSelectable` only asks for a living unit (174-181) and `AfterSetOrder` plays no
  gesture (706). Its selection is separate from the player's, so the panel never sees it. Those vanilla callers order the enemy team, and both deployment handlers
  use `PlayerOrderController` for the player's team, so this path on the player's own team is proved only by reading. The `queue_two_moves` step (section 11) proves it.
- **Never construct an `OrderController` for the player's team.** Its constructor writes `PlayerOwner = owner` on
  every formation when `Team.IsPlayerGeneral` holds (68-74), and `PlayerOwner = null` hands them all to the AI
  (Formation.cs, `PlayerOwner` setter).
- **`BeforeSetOrder` takes a company off AI control** when it has a `PlayerOwner` (OrderController:722-727), on the master controller too. So the driver tests AI control before each start (5.3, rule 2).
- `OnOrderIssued` of the player's controller fires only for orders that went through it, so a step re-issued on
  the master controller does not look like a new player order. Two vanilla listeners miss a step for the same reason, and both go in the feature doc as accepted limits. `AgentVictoryLogic` (114, 119-128): a step that starts during a victory
  cheer does not end the cheer for those soldiers. `MissionOrderTroopControllerVM` (444, 790): a step that starts with the panel open leaves the panel's active icons on the old order until it reopens (MissionOrderVM:736). `Team.OnOrderIssued` does see steps (Team:296).
- `MoveToLineSegment` fires per company with `OrderType.Move` (1578, 1590, 1599), so the event names each company.

### 5.2 Capture

One Harmony prefix class, `MarshalOrderCapturePatch`, on all six methods. Each prefix does this and nothing else:

```csharp
if (!MarshalHooks.IsLive || !MarshalHooks.QueueArmed || __instance != MarshalHooks.PlayerController) return true;
return !MarshalHooks.TryCapture(new CapturedOrder(...));   // true means it was captured, so skip vanilla
```

`QueueArmed` is written once per frame by `MarshalView`: key 540 is down and `Mission.IsOrderMenuOpen` holds. `MarshalHooks.KeepWidthsHeld` is written beside it for key 542 (6.2). Both clear in the finish.
`PlayerController` is the player team's `PlayerOrderController`, refreshed when the player team changes.

`TryCapture` declines, and vanilla runs at once, when any of these holds. A decline runs a direct order, so rule 6 clears the queues of all selected companies:

- No company is selected, or a selected company is under AI control. A delegated company takes direct orders.
- The order type is not on the allow list below.
- `Mission.Mode` is Deployment, for the reason 6.1 gives. `OrderOfBattleVM` listens to the player controller's event only (721), and a queued placement would wait on an arrival that deployment's teleport decides.

A selected company that already holds `MaxStepsPerCompany` steps swallows the order: `TryCapture` returns true with nothing queued, and one message says the queue is full. A decline here would run the order now, walk the company away and wipe its 12 steps.

| Allowed | Method | The step ends when |
|---|---|---|
| `Move`, `MoveToLineSegment`, `MoveToLineSegmentWithHorizontalLayout`, `PointDefence` | Position, two positions, orderable | Arrival (5.4) |
| `Charge` or `Advance` with a target company | `SetOrderWithFormation` | The target has no units |
| `AttackEntity` | Orderable | `OrderEnum` is no longer `AttackEntity`, because `Formation.Tick` swaps a spent order for its substitute (Formation.cs:2466-2470), or the patience timer ends |
| `Charge`, `StandYourGround`, `Advance`, `FallBack`, `Retreat`, `AdvanceTenPaces`, `FallBackTenPaces` with no target | `SetOrder` | At once |
| `LookAtEnemy`, `HoldFire`, `FireAtWill`, `Mount`, `Dismount`, every `Arrangement*`, `FormDeep`, `FormWide`, `FormWider` | `SetOrder` | At once |
| `LookAtDirection` | `SetOrderWithPosition` | At once |
| Mark, added by M4 (8.1) | `SetOrderWithFormation` with key 541 | At once |

Everything else passes straight to vanilla, and that includes `FollowMe`, `FollowEntity`, `Use`, `Transfer`,
`AIControlOn`, `AIControlOff` and `FormCustom`. `FollowMe` and `FollowEntity` have no end. `Use` toggles and
depends on state at the click. `FormCustom` reads `Agent.Main.Position` (OrderController.cs:1623), and in the Seat
that agent can be gone.

The capture only reads, because it runs in the screen tick (OrderTroopPlacer:576-601; GauntletOrderUIHandler:162-169 into 351). It stores the payload (order type, method, positions, target) as an adapter
type that the domain holds as an opaque `object`.

**Empty queue while a Move is under way.** A step captured for a company with no queued step carries a mark, `NeedsWaitTest`. The driver reads that mark in `OnMissionTick` before it starts the step: if the company's current order is a
Move and `Arrived` is false, it adds one implicit step ahead of it that only waits for arrival. Otherwise the new step would start on
the next tick and cancel the walk the player just ordered. This test runs in the driver because `Arrived` reads values that `Formation.Tick` writes on the AI job (2444).

**Scripted orders.** Missions issue orders through the same controller (`HideoutAmbushMissionController` 311, 692, 999; `HideoutMissionController:555`; `LordsHallFightMissionController:283`). With key 540 held and the panel open at that moment, such an order queues. Accept this, with no guard, and name it in the feature doc.

### 5.3 The queue

`Main/Features/MarshalsWord/Domain/MarshalOrderQueue.cs`, pure and single threaded (main thread only).

```csharp
internal enum StepEnd { AtOnce, Arrival, TargetGone, EntityOrderEnded, ImplicitArrival }

internal sealed class QueueStep
{
    internal readonly long Id;
    internal readonly List<int> Companies;   // live members; a company leaves when it drops out
    internal readonly StepEnd End;
    internal readonly object Payload;        // opaque to the domain
    internal bool NeedsWaitTest;
    internal bool Started;
    internal double StartedAt, Patience;
}
```

Each company has a FIFO of `QueueStep` references. A step shared by several companies appears in each one's list.

Rules, each with a unit test over a fake adapter (`IMarshalOrderAdapter`):

1. **A step starts** when it is at the head of every member's list. This one barrier is the whole group rule: if two
   companies share steps A and C and only one has step B in between, the other waits at C until B is done.
2. **Starting** runs inside one `OnMissionTick`. First the driver re-tests every member for units above 0 and not `IsAIControlled`, and removes a failing member. Without this, a step that starts after the hero falls, and before the next slow pass, takes the company back off
   AI control (Mission:3041, Team:483, OrderController:722-727), and no player remains to order it. The driver then selects the members on the master controller, issues the stored order, and clears the selection. If no member is selected after the select calls, the
   step is dropped with no order, because `SetOrderWithOrderableObject` indexes `SelectedFormations[0]` with no test (1010). If the payload is an orderable with `GetOrder(side) == OrderType.None`, or a target
   company with no units, the step is dropped without an order.
3. **A member finishes** by the step's `End` rule, and then its list advances. Other members may still be walking.
4. **Patience.** A step with an `Arrival`, `ImplicitArrival` or `EntityOrderEnded` end gives up at `max(MinPatience, PatiencePerEta * distance / speed)`, taken at the start as the largest over its members, and
   logs a line. `distance` runs from the company's `CurrentPosition` to its `OrderPosition` or to the entity. `speed` is `max(Formation.CachedMovementSpeed, 1)`, because that value has a floor of only 0.1 (Formation:1450, 1469). A company stuck behind a wall must not hold the army's queue forever.
   A `TargetGone` step has no patience: the player asked for a charge until the target is gone.
5. **A company drops out** when it has no units or is under AI control. The driver checks both on its slow pass and before each start (rule 2), so no
   formation event is needed. Thread of `Formation.OnUnitRemoved` is unproven, and this avoids the question. An
   emptied step is removed.
6. **A direct player order clears that company's queue.** Subscribe to the player controller's `OnOrderIssued`. Clear
   for every order type except firing, riding, arrangement and form orders, because those do not contradict a walk.
   **A change of player team** (`Mission.Teams.OnPlayerTeamChanged`) clears every queue and any march, because each stored `int` would name a formation of the new team (Team:625-627). It then re-subscribes and refreshes `PlayerController`. Unsubscribe at the end.
7. The queue ends with the mission. Nothing is saved.

| Constant | Value |
|---|---|
| `MaxStepsPerCompany` | 12 |
| `SlowPassSeconds` | 0.25 of mission time |
| `MinPatience` | 25 s |
| `PatiencePerEta` | 2.5 |
| `ArrivalSlack` | 3 m |

### 5.4 Arrival

`IMarshalOrderAdapter.Arrived(company)` reads only cached values, so it costs almost nothing:
`OrderPositionIsValid`, and `Formation.CurrentPosition` within `ArrivalSlack` of `OrderPosition`. `CurrentPosition` is the engine's own estimate of the company's order point: the cached average position moved by the
local average (Formation:567), or the vanguard of a column (556-565). So it needs no allowance for depth, and a company that turned to face back along its walk is not called arrived a depth too early, which a test on the plain average would do.
An arrival read right after a step start does not see the old position, because `MovementOrder.OnApply` calls `SetPositioning` at once for a Move (690-691). A Move skipped at OrderController:994-995 gives no false arrival, and that step falls to patience.

### 5.5 Show the road

`MarshalView` draws one vanilla flag per queued position of the selected companies while the panel is open, and hides
the flags when it closes. Build each flag as `OrderTroopPlacer.AddOrderPositionEntity` does (507-511): `GameEntity.CreateEmpty(scene)`, then `EntityFlags |= EntityFlags.NotAffectedBySeason`, then
`AddComponent(MetaMesh.GetCopy("order_flag_small"))`, then `SetVisibilityExcludeParents(false)`. That name is a metamesh and not a prefab. Keep a small pool,
and place each flag at the ground height of the step's middle point. Refresh on a queue change and at 4 Hz. At the end of the
mission drop the pool with no native call: the flags go with the scene, and vanilla's `OrderTroopPlacer` has no teardown for its own.

### 5.6 Log lines

`IOutcomeLog` with `tools/ingame_checks/0.toml`, the same draft manifest as the Seat, because one held issue covers both features (Seat
contract, Deviations row 8). Each stage adds its checks there. M1 checks: step captured, step started, step finished with its reason, queue cleared by a direct order, capture declined, session fault.

The arrival check passes on `reason=arrival` only. Patience has its own line and a number: a `Fail` when patience ended more than one step in four, once at least 8 steps have finished, because a broken arrival test would otherwise read as many finished steps. `TargetGone` and
`EntityOrderEnded` need a kill or a siege, so NOT_SEEN is acceptable for them after a field battle. Each pass and fail case has a fixture.

## 6. Stage M2: keep the array

### 6.1 The seam

`OrderController.SimulateNewOrderWithPositionAndDirectionAux` (private static, 1206) is the single place that both the
preview (through the public static at 1296) and the commit (through `MoveToLineSegment` at 1541) go through. One prefix
there replaces the layout and hands back the same outputs, so all of vanilla's commit code still runs: the Move, facing
and width orders, the events and `AfterSetOrder`.

Parameter names for the prefix: `formations`, `simulationFormations`, `formationLineBegin`, `formationLineEnd`,
`isSimulatingAgentFrames`, `simulationAgentFrames`, `isSimulatingFormationChanges`, `simulationFormationChanges`,
`isLineShort`, `isFormationLayoutVertical`. Pin them in the binding test.

The prefix takes over only when all of these hold; otherwise it returns true and vanilla runs:

- `MarshalHooks.IsLive` and `MarshalKeepArray`.
- `isFormationLayoutVertical` is true, two or more companies are given, all belong to the player team, and none is under AI control that an order would not lift. Read that test as `IsAIControlled && PlayerOwner == null`. The commit's `BeforeSetOrder` runs first (OrderController:984, 724-727)
  and the preview runs none (OrderTroopPlacer:406), so a plain `IsAIControlled` would let the preview draw vanilla's flags and the commit lay out the array. A source pin names line 724.
- `Mission.Current.Mode` is not Deployment. Deployment has its own position rules (`SimulateNewOrderWithFrameAndWidth`
  line 1401).

A throw inside the prefix latches the takeover off for the mission and falls to vanilla **before any output is written**:
solve first, then build outputs, and let vanilla run on any fault.

### 6.2 The solver

`Main/Features/MarshalsWord/Domain/MarshalArraySolver.cs`, pure, over plain structs, with golden tests. It is TAOM's own
method. Nothing here reads engine types.

```csharp
internal readonly record struct ArrayCompany(int Company, float X, float Y, float Width, float MinWidth, float MaxWidth, float Depth, int Units, bool Column);
internal readonly record struct ArrayPlacement(int Company, float X, float Y, float Width);
```

1. **Shape.** The group's origin is the unit-weighted centre of the companies' positions. Its forward is the direction of
   the company with the most units. For each company store `Across`, its offset along the group's right, and `Along`, its
   offset along forward. Right is forward turned 90 degrees clockwise, the same turn the engine uses (1318).
2. **A click** (the short line test below). No turn and no width change. Each company moves by the same offset, so that the
   group's origin lands on the clicked point, which is `formationLineBegin`. Outputs: `isLineShort = true` and each
   company's own current width, so `MoveToLineSegment`'s short branch moves it and leaves its width (1564-1570).
3. **A drag.** New right is the line's direction, and new forward is `(-right.y, right.x)` (1318). The player draws the front of the group, so the anchor differs from a click's: the centre of the group's width lands on the
   line's middle, and the largest `Along` lands on the line, so the front of the group lies on what the player drew.
   - `Span` is the group's width: the largest `Across + Width / 2` less the smallest `Across - Width / 2`.
   - `Stretch = LineLength / Span`. With `KeepWidthsHeld`, `Stretch = 1`, so the group only turns. The prefix reads that static, because keys are read in the screen tick (section 2).
   - Each company's new width is `Clamp(Width * Stretch, MinWidth, MaxWidth)`. A `Column` keeps its width (6.3).
   - Each company's new `Across` is `Across * Stretch`. `Along` does not change, so the rear stays behind the front.
   - **Shoulder pass.** Walk the companies in `Across` order. If this company's depth band `[Along - Depth, Along]` overlaps an
     earlier one's, and their widths now overlap, push this company right until the gap is `OrderController.FormationGapInLine`.
     Then shift all by the average push so that the group stays centred. One pass, because there are at most ten companies.
   - Outputs: `isLineShort = false`.
4. **The short line test** is vanilla's, restated in pure code: the line is short when its length is under the bipedal radius,
   or under the sum of `MinimumWidth` plus the gaps (1208-1221).

Golden tests pin: a click keeps every offset; a line drawn from flank to flank along the current front changes nothing; the front of the group lies on the line after a drag; a turn of 90
degrees maps front to the new front; a stretch keeps a rear company behind; a clamp at `MaxWidth` does not shift the rest;
two side by side companies never overlap after the pass; every float output is finite for zero, huge and one-company inputs.

**What counts as a company's position.** `OrderPosition` when `OrderPositionIsValid` and its movement state is `Hold`, so that a
second order during a walk keeps the array the player last ordered. Otherwise `Formation.CurrentPosition`. Its direction is `Formation.Direction`. These are read on the main thread at the time of the call.

### 6.3 Building the outputs

The prefix writes what vanilla's `SimulateNewOrderWithFrameAndWidth` writes (1397-1441), with public calls only, so no reflection
site is needed:

- Each position comes from `Mission.Current.GetStraightPathToTarget(target2D, formationLineBegin)`, as at 1237-1242, so it stays on the mesh.
- The fit test is the loop at 1334-1357, over the public `Formation.GetUnitPositionWithIndexAccordingToNewOrder` overload with
  `actualWidth`. It must stay the public overload, because `Patch92_TrollFormationSpacing` opens its scope there, and
  `Formation.GetHashCode` throws on a team-less copy (registry section Patch92). Replace the simulation copy when spacing differs,
  as at 1336-1339. Never use a simulation copy as a dictionary key. The simulation copy comes from `simulationFormations?[f]`, as at 1256.
- `simulationAgentFrames`, when requested, gets one entry per unit index from the overload at line 1410.
- `simulationFormationChanges`, when requested, gets `(formation, unitSpacingReduction, width, position, direction)`.
- A column passes its `Arrangement.FlankWidth` as width. A custom form order that differs by over 0.01 transposes a column
  (FormOrder.cs:89-93), and the long line branch always applies one (OrderController.cs:1576). So a drag leaves a column as it is.

Accepted limit, in the feature doc: on a click, `MoveToLineSegment` turns every company to the largest one's direction when that
company has a set direction (1594-1597). A group with mixed directions lines up on a click. That is fine, and it is vanilla's choice.

Solver quality near a wall is not proved: `GetStraightPathToTarget` from one begin point can clip several companies to one hit. The game run looks for this, and any change goes under Deviations.

### 6.4 Arriving together

`MarshalArriveTogether`. When the prefix runs a commit (`isSimulatingFormationChanges`) with two or more companies, it records them as
one march, with each company's target position. The Move itself applies after the prefix returns (OrderController:1574, 1588, 1596), so nothing compares with that order yet.

`MarshalMarch` runs on the driver's slow pass:

1. For each company: `Remaining = max(0, distance from CurrentPosition to OrderPosition)`,
   `Speed = max(CachedMovementSpeed, 1)`, `Eta = Remaining / Speed`. Record the company's first time at `Remaining` 0, for the log.
2. `Longest` is the largest Eta. The march ends and the table is cleared when `Longest` is under 2 s, or when any company's `OrderPosition` is more than 1 m from its stored target, which means a later order moved it.
3. Each company's pace is `Clamp(Eta / Longest, MinPace, 1)`, with `MinPace = 0.3`.
4. Publish a new immutable array of `(Formation, float)` with one reference write. Clear it with a null.

The engine reads pace through one postfix on the public `HumanAIComponent.GetDesiredSpeedInFormation(bool isCharging)` (551):

```csharp
if (isCharging) return;
var table = MarshalHooks.Paces;           // one volatile read of an immutable array, null when no march runs
if (table == null) return;
... find __instance's Agent.Formation by reference, multiply __result, clamp to 0.2 .. 1
```

**This postfix runs on three threads:** workers, the AI thread (`Formation.Tick` forces movement updates, Formation.cs:2497-2504) and the
main thread (`AfterSetOrder`, OrderController.cs:695-699). The array is the only thing it reads besides `Agent.Formation`, which vanilla
reads on the same line. `Agent` is a protected field, so read it through the `___Agent` injection. No lock, no dictionary, no allocation, no
log line, no settings read.

`GetFormationFrame` asks this method for a multiplier only in the Hold state and only for a soldier with `ShouldCatchUpWithFormation`
(622-627, 639-640). A soldier far from their place stays at full speed. A column never catches up (694), so it keeps its pace. Both are fine.
`CachedMovementSpeed` comes from a maximum speed and not from velocity (Formation:1463, 1482), so a paced company does not read its own slowing. For riders that value comes from a native call that nobody has read, so the first run with cavalry reads the spread below.

### 6.5 Log lines

Array solved (click or drag), fallback to vanilla, march started. The march's end writes `spread=<s> longest=<s>`: `spread` is the time between the first and the last company's first arrival, and `longest` is the first pass's `Longest`. The line passes when `spread <= 2 + 0.25 * longest`, which a group without pace does not reach once speeds differ. A march that a later order ended writes a `Skip`. The session computes this and writes the check line, so that the manifest reads a verdict and not a trend.

## 7. Stage M3: loose on my word

**This stage rests on a belief that only the game can prove, so it carries its own proof first.**

What a native read settles (`python tools/native_decompile.py --rva 0x69FD10`, the AI's aim state on v1.5.6):

- On each pass of its aim state, the AI raises `AttackDown` in its stored result (`|= 0x200`). It looses by dropping the attack bits (`& 0xfffffc3f`) and moving to a wait state. It abandons by dropping them and adding `0x4000`.
- Native copies that stored result over the agent's flags just before the callback, so a write lasts one tick and a hold must repeat every call.
- The AI's cycle does not wait for a hold. It leaves the wait state and returns to aiming, so its stored bit comes back while a soldier holds. So `Attacking` means nothing while Holding, and a release must clear the bits itself: leaving them alone at that moment usually looses nothing.

What only the game can prove: that setting `AttackDown` each tick keeps a bow or crossbow drawn with no shot, that one tick of cleared bits looses it, and what `0x4000` does beside `AttackDown`.

**Build this slice first: the component, `Step`, the driver, keys 543 and 544 and the proof log, with Massed held back.** A builder does not run the game: the orchestrator runs the slice through the field trial (section 11). The stop rule: in that run a Holding soldier's bow stays drawn and no shot leaves, and then Release looses within a second. If that is false, stop and record it under Deviations.

### 7.1 The engine facts

- `AgentComponent.OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2)` (AgentComponent.cs:81) is reached through
  `Agent.OnAIInputSet` (Agent.cs:1660-1666).
- From the mission tick it runs on the main thread, serially, after the managed `OnPreTick` has waited for the last agent job. So
  there it never overlaps `OnMissionTick` or the AI job. Native also reaches the same reader from `Agent.PreloadForRendering` (managed callers Agent:3739 in `UpdateSpawnEquipmentAndRefreshVisuals`, Mission:4056, MissionAgentHandler:742). A soldier in a mode could reach that route only through an equipment refresh. Nobody traced a caller that does, and the try in 7.2 covers it.
- Native calls it only for an active, AI-controlled, unridden agent whose flag is on, and only when the tick's dt is above zero.
- `Agent.SetHasOnAiInputSetCallback(bool)` (1654) sets the flag. Always call it with true when you want it. Do not rely on the default.
- Every component on the agent gets the call, and it costs a native-to-managed trip per AI agent with the flag on. So the flag stays off unless the
  soldier's company has a rule that needs the callback.
- `Agent.AddComponent` does not call `Initialize` (Agent.cs:4409-4420), and `BuildAgent` calls it only for a component
  added before the agent is built. The driver adds this component to a living soldier, so it sets itself up in its
  constructor or on its first call, and it never depends on `Initialize`.

### 7.2 Fire modes

```csharp
internal enum FireMode { Free, OnWord, Massed }
internal enum DrawState { Idle, Drawing, Holding }
internal enum DrawAction { Leave, Hold, Release }
```

Key 543 cycles the selected companies: Free, OnWord, Massed, Free. Key 544 gives the word to the selected companies. Each change shows one
message. The press is read in `OnMissionScreenTick` and stored. `OnMissionTick` applies it, because adding a component is engine state. Both keys skip a company under AI control, with a `Skip` line, because the team AI commands it and `VolleyControl` writes its firing orders.

**The driver**, on a 1 Hz pass in `OnMissionTick`, walks the units of each company with a rule that needs the callback. In this stage that means a mode other than Free, and M5 adds hold on (9.2). It adds `MarshalFireComponent`
to a unit with none and turns the flag on. It keeps a list of agents it has touched, by reference. For those no longer in such a company it turns the flag off and calls the component's main-thread `Reset()`, because native never calls a component with the flag off, so its state would freeze at Holding. It tests `AgentSlotIdentity.IsCurrentOccupant` first. Never key on `Agent.Index`.

**A company under AI control has no mode.** The same pass sets such a company to Free. After the hero falls or the player delegates, nobody can give the word, so it would otherwise loose one arrow per `HoldLimit` for the rest of the battle (Team:479-485). The mode does not come back when the player retakes the company: the player picks it again.

**The rule**, pure, in `Domain/MarshalFireRules.cs`:

```csharp
internal readonly record struct DrawInput(bool Attacking, bool Cancelling, bool StandAside, bool WordDue, double Now);
internal static DrawStep Step(DrawState state, double since, in DrawInput input);   // returns next state, since, and the action
```

| State | Input | Next | Action |
|---|---|---|---|
| Holding | `StandAside` | Idle | Release |
| Idle or Drawing | `StandAside` | Idle | Leave |
| Idle | Attacking | Drawing | Leave |
| Idle | not Attacking | Idle | Leave |
| Drawing | Attacking | Drawing | Leave |
| Drawing | not Attacking, Cancelling | Idle | Leave |
| Drawing | not Attacking, not Cancelling | Holding | Hold |
| Holding | not `WordDue` and held under `HoldLimit` | Holding | Hold |
| Holding | `WordDue`, or held `HoldLimit` | Idle | Release |

`Attacking` is `movementFlag & Agent.MovementControlFlag.AttackMask` non-zero. `Cancelling` is the same with `DefendMask`. **Leave** writes nothing. **Hold** clears `AttackMask` and `DefendMask`, then adds `AttackDown`, so that an abandon bit does not stand beside the draw. **Release** clears `AttackMask`. The AI keeps its own wish to draw and aim, so this stage changes no behaviour weights and writes no `CanAttack`.

`StandAside` is read at most 3 times a second per soldier, because it makes native calls, and the component keeps the answer in a field: the company's mode is Free, no formation, no ranged weapon wielded, company on `HoldFire`,
detached, using a game object, or a target within `CloseQuarter`. Mode Free stands aside so that a change to Free looses a Holding soldier at the next read and not a tick earlier.

`WordDue` is `wordTime >= since && wordTime + jitter <= now`. **A word releases only a soldier who was already Holding when it was given,** so a soldier who starts to hold after the word waits for the next one. The word time starts at negative infinity and resets on each mode change, so a company with no word yet holds. Jitter is drawn once when the component is built, from 0 to `WordJitter`, so that a volley sounds like
men and not a machine. The component reads the company's `FireMode` and word time from a main-thread array sized `FormationClass.NumberOfAllFormations` and indexed by `Formation.Index`, because a soldier can stand in the General or Bodyguard formation (8 and 9).

**The callback has its own fault latch.** The body of `OnAIInputSet` runs in one try, because `Agent.OnAIInputSet` has none (1660-1666) and its shim has no finalizer (`docs/reference/harmony-patch-registry.md:309`), so a throw would unwind through the native mission tick. A catch sets one static off, reports once through `IOutcomeLog.Swallowed`, and writes nothing to the flags, then or after, so that the AI's own flags stand. This is a hot path: counters only, no log line.

**Massed.** In `OnMissionTick`, at 5 Hz, for each company in Massed mode, count from the touched list. `Holding` is soldiers in that state. `Eligible` is soldiers not standing aside, less Drawing soldiers whose draw began more than `DrawLimit` ago, because a soldier with no view of a target stops counting after that. Idle soldiers count in `Eligible`, so one ready soldier among thirty reads 1 in 30. When `Holding` is at least one, `Holding / Eligible` reaches `ReadyShare`, and `WordGap` has
passed since the last word, give the word. In `Domain/MarshalFireRules.cs`, with tests, including the 1 in 30 case. Group by nothing else in this contract.

| Constant | Value |
|---|---|
| `HoldLimit` | 14 s |
| `CloseQuarter` | 7 m |
| `WordJitter` | 0.3 s |
| `ReadyShare` | 0.7 |
| `DrawLimit` | 6 s |
| `WordGap` | 2.5 s |

**The word against engine fire orders.** A company told to hold fire stands aside. So a change to OnWord or Massed also gives the company fire at will through the master
controller, because otherwise the new mode would look broken. An engine hold fire order given later wins, and the company stands aside until the player orders fire at will or
changes the mode again. `Formation.AddUnit` and `RemoveUnit` reset an agent's firing order (2319, 2382), so this stage never writes a per-agent firing order.

**The finish** (section 2) makes no native call, because `RaceAbilityVisuals.Clear` shows why and the flags go with the agents. It drops the list and the array, and it writes one last summary line for each company with a mode, because that can be the only evidence of a word given just before the battle ended. `OnAgentShootMissile` returns at once with no session.

### 7.3 The proof log

Land this first, and leave it in. `MarshalView` overrides `OnAgentShootMissile` (MissionBehavior:240, seven parameters). The callback counts only a shooter on the player team whose company has a mode, because `Formation.Index` repeats across teams. It reads `shooter.Formation` and the component's fields, takes no lock and adds to counters with `Interlocked`, because its thread is observed off main (`mission-frame-threads-and-native-costs.md:119`).

| Component state at the shot | Counter |
|---|---|
| Holding | `shotsWhileHolding` |
| Idle, with a Release in the last 2 s | `shotsAfterWord`, from a time that Release writes |
| Anything else (standing aside, Drawing) | `shotsFree` |

`OnMissionTick` reads the counters and writes. Each word writes one line, and each company with a mode writes one summary at most every 5 s:

`[Marshal] word company=<n> time=<t> holding=<h>`

`[Marshal] fire company=<n> mode=<m> drawing=<a> holding=<b> shotsWhileHolding=<c> shotsAfterWord=<d> shotsFree=<e> shotsBeforeFirstWord=<f>`

The session judges these and writes `IOutcomeLog` check lines, so that a manifest row can read a verdict. The summary alone cannot fail: if soldiers hold for one tick and then fire, it still shows `holding` above zero and `shotsAfterWord` rising.

- `taom.marshals-word.volley-hold`: a company with at least 5 Holding soldiers through a 5 s window with no word. Pass when `shotsWhileHolding` in that window is at most 5 percent of the Holding count. Fail above that.
- `taom.marshals-word.volley-release`: at a word with at least 5 Holding soldiers. Pass when at least 70 percent of them loose within `WordJitter + 1` s. Fail below that.

Each pass and fail case has a fixture.

### 7.4 What this stage does not touch

No `AgentFlag.CanAttack`, no AI behaviour weights, no movement input and no standing rule. The archers keep walking when ordered and may draw on the march.

## 8. Stage M4: mark and show

### 8.1 Mark, do not move

Vanilla charges a focused enemy on a left release (GauntletOrderUIHandler.cs:333-345), through an order that calls `SetOrderWithFormation(Charge, target)`. This stage changes the capture prefix of 5.2:

```csharp
if (!MarshalHooks.IsLive || !(MarshalHooks.QueueArmed || MarshalHooks.MarkArmed) || __instance != MarshalHooks.PlayerController) return true;
```

`MarkArmed` is key 541 down and `Mission.IsOrderMenuOpen`, written beside `QueueArmed`. `TryCapture` then handles a mark, a queue and both. When `MarkArmed` holds and the order is that call:

- Capture stores one mark request for each selected company under the player's control, and returns true, so vanilla does not run. With no such company it declines, so vanilla runs and the click is not swallowed.
- The next `OnMissionTick` applies each request: `formation.SetTargetFormation(target)` (Formation.cs:839), with `MissionThreadGuard.NoteCall`. Leave its movement order alone. The setter makes one native write per soldier (241-250), and `Patch31b` can re-order a cavalry company from its postfix, so this does not run in the screen tick, as vanilla's own targeted charge does.
- With key 540 also held, capture queues a step whose payload has the kind `Mark`, with `StepEnd.AtOnce`. Starting that step runs `SetTargetFormation` on each member and issues no order, and it selects nothing, because the stored call would otherwise charge, which is the one thing a mark exists to avoid.

**Any `Formation.SetMovementOrder` clears a mark,** because it ends with `SetTargetFormation(null)` (Formation.cs:734). That is the intended rule, and the feature doc lists the routes, because some do not look like movement orders: `OrderController.TryCancelStopOrder` (1716-1724) on a company on Stop, which `SetOrder` calls for `LookAtEnemy` (303), every arrangement and the form orders (355 to 425); `BannerBearerLogic:157`; `Formation.Tick`'s substitute loop (2466-2470); `Formation.OnDeploymentFinished`. Fire orders do not clear it (308-319). So in a queue a mark placed before a movement step is lost when that step starts, and one placed after it stays.
The engine clears a dead target on its own (2471-2474).

**Who wins against the smart cavalry cycle.** The player. `Patch31b` postfixes `SetTargetFormation` and re-points a running cycle at the chosen formation, so this needs no new code.
A queued Move goes through `Formation.SetMovementOrder` like any player order (OrderController.SetOrder and `MoveToLineSegment` both call that one method, `SetOrder` at 248-253), and it cancels the cycle (`docs/reference/harmony-patch-registry.md`, section Patch31). No unit test can prove the master path reaches a patch, so one in-game row does: a queued Move on a cycling cavalry company logs the cancel.

**Prove the effect.** The only read of what a mark does is its writer, the native `SetTargetFormationIndex` on the agent's AI. Nobody has read how the AI uses that value on a non-charge order, and vanilla only sets it with a charge or an advance (OrderController:811-824). At 1 Hz, for each marked company, sample at most 20 soldiers on `GetTargetAgent()` and write:

`[Marshal] mark company=<n> target=<m> onTarget=<a> of=<b>`

`taom.marshals-word.mark-effect` passes when at least half of the sampled soldiers with a target aim at the marked company, for a ranged company on Stop and for a melee company on a Move, each its own key. A false result stops the stage, as in section 7, and goes under Deviations.

### 8.2 Outlines

`MarshalOutlineService` runs in `OnMissionTick` at 4 Hz, while it should show or while its dictionary is not empty. It should show when `MarshalOutlines` holds and either the panel is open or game key 5 (ShowIndicators) is down. The screen tick reads that key and stores one bool, because nothing in `OnMissionTick` reads a key (section 2). When it should not show, the wanted set is empty, and that produces the clear pass: nothing else clears a contour, because vanilla's own contour view is inert (`IsEnabled = false`). A test covers the panel closing and the setting going off in a battle.

The plan is pure. Given the wanted colour of each unit, it returns who to paint and who to clear, against a dictionary of what it last painted, keyed by `Agent` reference. `Domain/` gets
the diff as a generic over `TKey`, tested with `int` and called with `Agent`, so no id is invented (never `Agent.Index`). Only agents it painted are ever cleared.

| Who | Colour |
|---|---|
| A selected company's units | `0xFFD9B44A` |
| An enemy company that a selected company marks or charges | `0xFFB03A2E` |

Paint with `agent.AgentVisuals.SetContourColor(color, true)`, and clear with a null, behind the guard `RaceAbilityVisuals.Paint` uses: `AgentSlotIdentity.IsCurrentOccupant`, not deleted, valid visuals
(`Main/Features/RaceAbilities/Hooks/RaceAbilityVisuals.cs:120-127`). A mount has its own visuals: paint the rider's mount as well. When the panel closes, clear everything within one pass. At the end of the mission
drop the dictionary with no native calls, as `RaceAbilityVisuals.Clear` does.

An agent carrying a race ability outline can be repainted by either writer. Accept that in this contract and name it in the feature doc. `RaceAbilitiesWiringTests` pins one write site per file, so this is a separate file.

## 9. Stage M5: hollow square, hold the line, small repairs

### 9.1 Hollow square

Vanilla's square order builds a `RectilinearSchiltronFormation`, which packs itself and ignores width (ArrangementOrder.cs:121; FormOrder.cs:95-98). The engine also has the plain `SquareFormation`, which keeps a chosen side
width (SquareFormation.cs:16-25, 63-79; FormOrder.cs:120-133) and has a working `Clone` (53-56). `Formation.Rearrange` and the two simulation methods test arrangements by exact type, and `Clone` keeps them consistent (Formation.cs:2550, 3117, 3172). **Use the plain engine class. Write no subclass.** The two blemishes a subclass could fix (the middle slot, the corner soldiers' facing) do not pay for the code.

**No shipped mission runs this class.** Its only construction is its own `Clone` (searched: the engine cache and the two view assemblies, with `ArrangementOrder.cs:121`'s subclass as the known hit), and `FormOrder` catches the only subclass first, so the branch at 120-133 has never run in vanilla. So the stage proves it. When a square forms, and on each unit change at most once a second, write:

`[Marshal] square company=<n> side=<w> files=<f> ranks=<r> units=<u> placed=<PositionedUnitCount> laidOut=<bool>`

`taom.marshals-word.square` passes when `placed == units`. If the class fails, stop and record it under Deviations.

Prefix on `ArrangementOrder.GetArrangement` (struct method, 114, shape `ref ArrangementOrder __instance, Formation formation, ref IFormationArrangement __result`): when `MarshalHollowSquare` and `MarshalHooks.IsLive` hold, the order is `Square`, and the formation is on the player team and not under AI control, return `new SquareFormation(formation)`. A simulation copy takes its arrangement through `Clone` (Formation:3124, 3179), so this prefix never sees one, and a null `Team` is a plain vanilla fall-through.

Two engine lines would break it, so each gets one small prefix, gated on `Arrangement.GetType() == typeof(SquareFormation)`, so that vanilla squares and every other arrangement run vanilla's code:

- `Formation.ReapplyFormOrder` (private, 2914-2922) copies `Arrangement.FlankWidth` into the custom width. A square's flank width is its perimeter (SquareFormation.cs:77), so the square would grow each time a unit joins or leaves. Keep the side: under the same test as line 2917, set `CustomFlankWidth = Math.Max(Arrangement.Width, Arrangement.MinimumWidth)`, call `SetFormOrder(formOrder, false)`, and return false. The floor matters: a square full to its last rank leaves a new unit unpositioned (LineFormation:557-583, SquareFormation:227-230), and such a unit stands at the order's centre (Formation:1331-1345), so the side must grow when units exceed its capacity, and a test pins that. It can run on either thread, so it reads only the formation it is given.
- `Formation.CalculateDesiredWidth` (private, 2924-2927) builds a line's flank width from `_desiredFileCount`, which then reads as a side. On a square the field means something else again, because `SetFormOrder` writes it from `CustomFlankWidth` (776). So no conversion from that field holds. Return `Clamp(Arrangement.Width, MinimumWidth, MaximumWidth)`: at line 758 the new square has already taken the old line's files (LineFormation:2498-2508), so its `Width` already holds the side with the perimeter kept. A square entered from a column starts packed, because `ColumnFormation` is not a `LineFormation`: the feature doc says so. This body is small enough for the JIT to inline, and a patch on an inlined method never runs, so the proof line must show the side right after the order. If it does not, record that under Deviations with what fixed it.

**A mixed company.** `Patch30` answers with the service's plane position for any laid-out formation in a field battle, and it reads no arrangement. So in a company of melee and ranged, which is the default state, the men form rows and the width has no effect on them. Vanilla's schiltron has the same limit today. Test with a pure infantry company, print `laidOut=` in the proof line, and name this in the feature doc and the in-game row.

Read `Formation.SetArrangementOrder` (742-770) and record in the feature doc what a switch from line to square does with this. Test with trolls, because `Patch92` changes `UnitDiameter`.

### 9.2 Hold the line

Key 545 toggles a company. A shielded foot soldier in a shield wall, circle or square, under an order that is not a charge, stops attacking and stays in their slot while enemies press. The soldier strikes back for `StrikeBackSeconds` after a real wound.

**This rule also rests on a belief that only the game can prove, so it opens as section 7 does.** Nothing in the engine or TAOM clears `AgentFlag.CanAttack` at run time. Its managed readers are `ClimbingMachineDetachment` (241, 266), `HumanAIComponent:229`, `Mission` (4897, 6291) and `AttackInformation:236`, none on the attack path, and nobody traced the native readers. So nobody has shown that the flag stops an AI that already holds a target, or that clearing it mid-swing is safe (`docs/features/spider.md:202` records a native CTD on a nearby path). Land one toggle and a log line first: attacks counted per company with the flag off, against a control company. The orchestrator runs it. If it fails, stop and record it under Deviations.

It uses the same component as M3, so `MarshalFireComponent` becomes `MarshalWordComponent`, and both rules run in one `OnAIInputSet`. The driver's walk and its flag test (7.2) now read "a fire mode other than Free, or hold on", and M3's rule does nothing on a Free company, because it stands aside. If M3 stopped at its proof, the component stays for this stage under the new name. One owner writes `AgentFlag.CanAttack` (`GetAgentFlags`, `SetAgentFlags`). Volley never writes it, so there is no second writer.

- **Qualifies,** read at most 3 times a second: the arrangement; the company's `OrderEnum` is not `Charge`, `ChargeToTarget` or `Retreat`; `!Formation.IsAIControlled`; a carried shield (`HasShieldCached`, which does not mean wielded); no mount; no ranged weapon wielded; not `IsDetachedFromFormation`; not using a game object; and `agent != Mission.MainAgent`. The AI test matters most: after the hero falls the team goes to the AI (Mission:3041, Team:483), which keeps a company in a wall under a Move or an Advance, so without it every soldier keeps the flag off for the rest of the battle with nobody left to press the key. A test pins that.
- **While qualified:** `CanAttack` off. `Agent.SetFiringOrder(HoldYourFire)` per agent, and restore from the formation on leaving. `Formation.AddUnit` and `RemoveUnit` overwrite it (2319, 2382), and so does a change to the company's firing order (801-804), so the 3 Hz read writes it again whenever it differs.
- **The slot clamp.** `CanAttack` off does not stop feet. The 3 Hz read stores the soldier's slot and whether the soldier is more than `SlotSlack` from it, because `Agent.Frame` is a native read (Agent:953-960) and `GetOrderPositionOfUnit` runs `Patch30`.
  - The slot is `Formation.GetOrderPositionOfUnit(agent)`. Under `StandGround` that member returns the soldier's own position (Formation:1403-1404, MovementOrder:143-144), and Stop is the most natural order for a line that must hold. So store the position once as an anchor under that state, and keep it until the order changes.
  - A position whose `IsValid` is false (Formation:1378-1385, 1401-1402) stores no slot, because `Invalid` is NaN (Vec3:627) and `OnAIInputSet` hands `inputVector` back to native by ref.
  - Per callback, and only with a slot stored, beyond the slack, and `inputVector` non-zero: if `Vec2.Dot(input.Normalized, toSlot.Normalized) < -SlotAwayDot`, replace `inputVector` with a local vector toward the slot, of length at most 1. Convert with `Agent.Frame.rotation`. So a sidestep along the line stays, and a walk away from the slot does not. The pure helper returns its input unchanged for a NaN, with a test.
  - That `inputVector` is in the agent's local frame, and that replacing it moves an AI soldier at all, are assumptions to prove in game. Log the distance to the slot with the order state beside it, so that a 0 under Stop does not read as a pass.
- **Strike back:** `AgentComponent.OnHit` records the time when `damage > 0 && !b.IsMissile`. A weapon parry reaches `OnHit` with no damage (Mission:5537-5550, 5585-5587), and it is not stopped by a shield, so the test uses the `damage` parameter. That callback can run on either thread, so write one `double` field, and read it on the main thread. In Host Review, `Mission.DisableDying` skips this path (Agent:5525-5527), so a soldier never strikes back there: the feature doc says so.
- **Turning it off** walks the units and restores `CanAttack` and the firing order: when hold is toggled off, and when the company's `OrderEnum` is `Charge` or `ChargeToTarget`. At the finish no native call runs (section 2).
- **`OnFormationSet`** runs inside the `Formation` setter, from `OnFleeing`, `OnRemove` and mass transfers (Agent:1187, 5168, 5182; Team:563), on either thread (`mission-frame-threads-and-native-costs.md:119`). So it writes only one field that expires the 3 Hz read, with no native call. The next `OnAIInputSet` or the driver's pass restores on the main thread. An agent being removed needs no restore.
- **A soldier who stops being AI controlled.** The callback stops with the controller (7.1), so nothing in it can restore them. The driver's 1 Hz pass gives an agent it touched that is no longer AI controlled their `CanAttack` and firing order back. Seat stages S4 and S5 can reach this.

| Constant | Value |
|---|---|
| `StrikeBackSeconds` | 4 s |
| `SlotAwayDot` | 0.3 |
| `SlotSlack` | 0.25 m |

### 9.3 Spacing repair

Vanilla's four spacing limits (`MinimumDistance`, `MaximumDistance`, `MinimumInterval`, `MaximumInterval`, Formation.cs:587-595) test `HasAnyMountedUnit`, so one rider gives an infantry company cavalry limits. The live values (`UnitDiameter`, `Interval`, `Distance`, Formation.cs:220, 520, 544) test `CalculateHasSignificantNumberOfMounted`. Make the limits agree with the live values. The rest of those getters is already right, including dismount and arrangement spacing.

The three public `GetUnitPositionWithIndexAccordingToNewOrder` overloads (1683-1695) also pass `HasAnyMountedUnit`, and the simulation copy takes it into `_overridenHasAnyMountedUnit` (3119), which both predicates answer first (408, 532). If only the limits changed, the order flags and the width handed to `SetFormOrder` (OrderController:1566, 1576) would keep cavalry spacing while the men form infantry spacing.

So the change is one transpiler per method, seven in all, replacing the one call to `get_HasAnyMountedUnit` with `get_CalculateHasSignificantNumberOfMounted`. Read `docs/reviews/lessons/harmony-il.md` and pin the call count in a binding test. Three readers stay on the old value, and the feature doc names them: the iterator at 3174, `OrderController:1363` and the spawn frames at `Mission:3975`.

Apply it at patch time only when `MarshalEnabled` and `MarshalSpacingRepair` hold, `ICoopPresenceProvider.IsCoopActive` is false and `IDedicatedServerProvider.IsDedicatedServer` is false. All are process-lifetime reads (`docs/features/coop-interop.md`), and the setting's hint says a change takes a restart. A co-op host and client must not compute different widths, so the repair stays off wherever a second machine could disagree. It does not read `IsLive`, and it applies to every team in a single-player mission, because the fix is a base-game consistency fix. Test with `Patch92`, which postfixes `UnitDiameter`, and read the registry section first. The getters run on workers, so no state and no setting read in the path.

A getter this small can be inlined, and a patch on an inlined method never runs. So one in-game line prints the limits and the live spacing for a company with one rider, and `taom.marshals-word.spacing` passes when the limits read as infantry. If they do not, record that under Deviations.

### 9.4 Throwers on an advance

Vanilla sends a thrower company to melee range on an Advance order (MovementOrder.cs:1170-1178), because `IsRangedFormation` reads a non-consumable ranged test. Build this only after 9.1 to 9.3 are proven, and only if the night leaves room. For an order with a position, vanilla's `GetPositionAux` runs about once per `Formation.Tick` on the AI thread, and workers reach it only for a unit with no stored slot. So a rule there costs per company and per tick. TAOM's rule: on the main thread's slow pass, compute a stand point for a player company whose units are mostly throwers and hold ammunition, at the median of its missile range in front of the target, and publish it in an immutable table. A prefix on `MovementOrder.GetPositionAux` for `Advance` (shape `ref MovementOrder __instance, Formation f, ref WorldPosition __result`) reads that one point, and it yields to vanilla in Deployment (1138-1141). It must not run a native call on the AI thread beyond what vanilla does at that depth (`GetNavMeshMT`, MovementOrder.cs:589). `MovementOrder`'s type initializer reads `Mission.Current`, so this patch joins `Patch_MissionTime_SetMovementOrder` timing. Log a counter of points that had no navmesh.

## 10. Stage M6: the panel

Hotkeys come first, and every order above works with no panel entry. This stage adds the entries.

`VisualOrderFactory.GetOrders` walks providers from last to first, and the first available one supplies every order (VisualOrderFactory.cs:40-49). So a TAOM provider subclasses `DefaultVisualOrderProvider` (`TaleWorlds.MountAndBlade.View.VisualOrders`, client-only, so it lives in `Client/Features/MarshalsWord/`) and is registered through the composition the way `HostReviewVisualOrderProvider` is.

**`IsAvailable` is `base.IsAvailable() && MarshalHooks.IsLive && !Mission.Current.HasMissionBehavior<HideoutMissionController>()`.**

- The base answers false in a friendly mission (DefaultVisualOrderProvider:16-23), which covers a town, an arena and the host review mission (`docs/features/host-review.md:22`). So no Host Review test is needed. `HostReviewVisualOrderProvider`'s one-line override is the wrong shape to copy here.
- `IsLive` takes the entries away with `MarshalEnabled` off, in a session and under a foreign camera. A fall-through gives vanilla's provider.
- The hideout test matters because TAOM loads after Sandbox (`Main/_Module/SubModule.xml:24`), so TAOM's provider answers before `SandBox.View.OrderProviders.HideoutVisualOrderProvider`, whose set has no Advance on purpose. The keys still work in a hideout.
- The providers that exist: Native's default, Sandbox's hideout and TAOM's Host Review (searched: every module DLL's metadata for `VisualOrderProvider`). NavalDLC has its own, and it is listed as incompatible (`Main/_Module/SubModule.xml:54`). A provider registered earlier loses to TAOM's, so a new vanilla one must join this list. Another mod's provider registered later wins whole, and the keys still work.

`GetOrders` copies `base.GetOrders()` into a new list and adds one set, a `GenericVisualOrderSet` holding fire mode, give the word and hold the line, whose last order is `new ReturnVisualOrder()`, with vanilla icon ids. Read `DefaultVisualOrderProvider` and `VisualOrder` (the overridable members are at lines 19-38) first. Game keys 69 to 77 map to indices 0 to 8 (GauntletOrderUIHandler:414-452), and F9 closes the panel when a single Return set exists (489).

| `BannerlordConfig.OrderLayoutType` | Base sets on a keyboard | Where the new set goes | Its F key |
|---|---|---|---|
| 1, Legacy | Eight, the last a single Return (130-143) | Before the trailing Return | F8 |
| Any other | Nine, all keys used (79-90) | Appended | None: keys 543 to 545 are its shortcuts |
| Any, on a gamepad | Three | Appended | None |

**A click stores a press.** `ExecuteOrder` runs in the handler's screen tick (470-471), so it only sets the same stored press the keys set, for `orderController.SelectedFormations`, and `OnMissionTick` applies it. `OnGetFormationHasOrder` reads TAOM state only. A source pin keeps it so.

The engine tests these strings and types by value, so never rename or drop a base set: the string id `order_movement_charge` (GauntletOrderUIHandler.cs:582), the set ids `order_type_movement` and `order_type_facing` (178-179), the icon id `order_type_facing` (MissionOrderVM.cs:97), the icon ids `order_movement_advance` and charge (MissionOrderTroopControllerVM.cs:815, 839), and the `ReturnVisualOrder` type tests (GauntletOrderUIHandler.cs:461-496, MissionOrderVM.cs:538). `BannerlordConfig.OrderType` picks the bar or the radial movie and `OrderLayoutType` picks the sets, and any pairing is possible, so a `[[human]]` row tests each of the four pairs. Nobody has checked a movie's room for one more set. Deployment always uses the radial movie (MissionGauntletSingleplayerOrderUIHandler:129). The panel's `OrderSets` list is empty until the first open (MissionOrderVM.cs:715), which is one more reason the keys never go through it.

Clickable buttons, cards and a marked-target icon are not in this stage. A later stage can take the panel through `[OverrideView]` on `MissionGauntletSingleplayerOrderUIHandler` (it has the attribute itself, line 17) with a `MissionOrderVM` subclass and TAOM-named prefabs. TAOM already sets `UIConfig.DoNotUseGeneratedPrefabs = true` (`Dependencies/SubModule.cs:42`), so TAOM's prefab XML loads with no further work. Do not change that flag. `[OverrideView]` does not reach `OrderTroopPlacer` in campaign or custom battle, because those mission lists construct it directly, so never plan on a placer subclass.

## 11. Definition of done for each stage

- Pure tests go first and stay green. A gate test keeps `Main/Features/MarshalsWord/Domain/` free of `using TaleWorlds`.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` is green, including composition and binding gates.
- `docs/features/marshals-word.md` from `TEMPLATE.md`, a feature-map row, a `Patch141_MarshalsWord` registry section, in-game check rows, and localized strings through the registration steps of `.claude/skills/localize/SKILL.md`, with translation left to the backlog.
- Every in-game check has a log line that proves pass or fail, and a judged check line where a trend cannot fail.

**Field trial.** These steps join the script of Seat contract section 12, in the same line format, and the wrapper judges them the same way. The script finds the view with `Mission.GetMissionBehavior<MarshalView>()`. The view gives the trial the same members its keys use, so a step exercises the same path: one member sets each armed static, and another stores each press. The steps issue real orders through `PlayerOrderController`, with the panel open through the Seat's trial member, because the capture needs it. Rows in `SeatTrialSteps` follow section 12's rules for real time and for which tick writes. A builder does not run the game, and the orchestrator runs these.

| Stage | Steps and pass rules |
|---|---|
| M1 | `queue_two_moves`: a Move, then a queued Move for one company. The first check line reads captured, the company arrives at the first point and then at the second, in that order, and both steps end with `reason=arrival`. `direct_order_clears`: a queued step, then a direct Move: the queue is empty |
| M2 | `array_keeps_offsets`: two companies, then a drag on their own front. After it, each company's offset from the other is within 3 m of what it was. `arrive_together`: the check line of 6.5 passes |
| M3 | `volley_holds`: the archers in OnWord against a target in range: the check line `volley-hold` passes, and the facts name how many held. `volley_looses`: the word: the check line `volley-release` passes. Massed gets its own step only after the stop rule in section 7 clears |
| M4 | `mark_keeps_order`: a company on Stop marks an enemy company: `TargetFormation` is that company, `OrderEnum` is still Stop, and the facts carry `onTarget`. `outline_clears`: the panel closes: the service's dictionary is empty within one pass |
| M5 | `square_side_kept`: a pure infantry company in a square at a chosen side, then one unit removed: the side is within one unit's width, and `placed == units`. `hold_line_flag`: hold on, then a charge: the flag is back on every soldier within 1 s |
| M6 | None. `[[human]]` rows only, because no script can judge a movie's layout |

## Deviations

Record each change to this contract here with its reason.

1. **Before any build, from the Seat's S1 review.** Section 2: the mode change callback only sets a flag, nothing caches the kind or the
   foreign-camera walk, and the player team is tested on each tick until the session is live. Section 3: a pressed key tests the static first,
   because the view's own test allocates. Section 5.5: the end of the mission drops the flag pool with no native call. Section 7.2: a soldier
   with no formation stands aside. Section 7.3: the shot callback only adds to counters, because its thread is not proved. Section 8.2: the
   screen tick reads game key 5, because nothing in `OnMissionTick` reads a key.
2. **Corrections after a read of every stage against the engine, before any build, 2026-11-04.** The defects this read found, each of which would have shipped:
   - **M1.** The flag entity was a prefab lookup, and that name is a metamesh (5.5). A step could start on an empty selection and throw inside `Mission.Tick`, so section 2 has one fault latch and rule 2 tests first. A step starting after the hero fell took the company back off AI control for nobody (rule 2). A full queue declined, which ran the order and wiped the queue, so it now swallows the order. Deployment declines. The arrival test used the plain average and half a depth, so it fired a depth early on a company facing back, and it now uses `CurrentPosition` (5.4, 6.2, 6.4). Patience covered only one end kind, and it named no distance or speed (rule 4). A change of player team now clears every queue (rule 6). The implicit wait moved to the driver, because the capture only reads.
   - **M2.** The preview and the commit read AI control at different moments, so the flags could lie (6.1). On a drag the unit-weighted origin overran the line that the player drew, so a drag anchors on the span's centre and the front (6.2). The march compared with a Move that did not exist yet, and its spread could not fail (6.4, 6.5).
   - **M3.** A word released every later soldier, because nothing compared it with the start of the hold, and a new company never held at all (7.2). The native read shows that the AI's draw bit returns during a hold, so a release must clear the bits itself, and `Step` now returns an action. Massed had no stated denominator, so one ready soldier of thirty met the share. A mode now ends when the company goes to the AI, and a change to Free resets the state. The array is sized for all ten formations, and the callback has its own latch. The proof log's pass rule could not fail, so the session now judges it (7.3).
   - **M4.** A mark could not fire, because the prefix's first line tested only key 540 (8.1). It wrote per-soldier native state from the screen tick, so it now waits for `OnMissionTick`. A queued mark would have charged, so a `Mark` payload issues no order. The outlines could not clear, because the gate that stopped the service also stopped its clearing pass (8.2). A stage whose effect nobody has read now has a check that can stop it.
   - **M5.** A company on hold stayed disarmed after the hero fell, so `IsAIControlled` is in Qualifies. An invalid slot would have passed NaN to native. Under Stop the slot is the soldier's own position, so the clamp could never fire, and an anchor replaces it. `OnFormationSet` made native writes on any thread, including during removal, so it now only expires a field. The flag and the slot read run at 3 Hz, because the clamp's reads cost a native trip and a `Patch30` ground probe per callback. The clamp's sign is a formula. A parry counted as a wound, so the test uses damage. The square's side had no floor, so reinforcements stood in the middle, and the width conversion read a field that a square overwrites. The hold rule and the plain square each have a proof first, because no vanilla code exercises either. The spacing repair now covers the preview too, and it stays off where a co-op host and client could disagree.
   - **M6.** TAOM's provider answered before vanilla's hideout set, and it would have shown Advance in every hideout. The availability rule is now one formula. A click stores a press, because `ExecuteOrder` runs in the screen tick. The set's position is pinned, because a set past the F key count has no shortcut, and in Legacy it would have sat where F9 closes the panel.
   - **Decisions taken with these corrections, each reversible:** `LeftShift` stays on key 540 with its zoom named, because it is the convention for queueing, and the zoom does not move the order ray (section 3). `LeftControl` stays on key 541 with its three readers named. A mode does not return when a company comes back from the AI. A mark holds only until the next movement order, by any route. 9.3 is off under co-op and on a dedicated server.
   - Section 11 gained the field trial table, because section 12 of the Seat contract named these steps and nothing gave their pass rules.
