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
  The view then calls `IMarshalSession.Begin()`.
  With no screen there is no view, so a dedicated server never runs any of this. Once live, the session stays live for the mission.
- **Teardown has two paths, and every stage here honours both:** `OnMissionScreenFinalize` (the end-mission path) and `OnRemoveBehavior`
  (`MissionState.OnFinalize` removes the behaviours with no `EndMission`; `docs/reference/engine/mission-and-missionbehavior-lifecycle.md`
  lines 100-123). Each hook calls one safe-to-repeat finish on the session, then sets the session field to null, because the screen can still
  tick a removed view. Anything that outlives a frame or that a patch reads clears in that finish: a flag pool, a held key, a static, an event
  handler.
- **Command stays vanilla's question.** An enlisted soldier stripped of command gets no Marshal's Word. Read
  `Main/Features/Enlistment/BattleCommandPolicy.cs` ("one predicate, three consumers") and add this feature as a consumer when the keys or
  the capture could otherwise act. Pressed keys (543, 544, 545) act only on a selection, so they also test that the player may command now.
  Host Review keeps the feature, except the panel provider in stage M6.
- `IMarshalSession.IsLive` is what every patch reads first. It is a plain `volatile bool` field behind a static
  hook, and a patch does nothing when it is false.
- **All main-thread work runs in two ticks of this view.** `OnMissionScreenTick` reads keys once per real frame
  and draws. It overlaps the AI job, so it touches no shared engine state and only writes TAOM fields that
  nothing off the main thread reads. `OnMissionTick` runs in the window where no agent job runs
  (Mission.cs:3540-3542, 3595-3600, 3750-3785), so it issues orders and adds components. Fast time repeats
  `OnMissionTick` in one frame, so nothing in it reads a key.
- Marshal's Word is off in a network session in this contract. Answer the four questions of
  `docs/features/coop-interop.md` "Four questions, not one" in the feature doc.

Layers follow ADR-007 and the Seat contract. `Formation` is sealed, so the domain types never see it. A company
is an `int`, `Formation.Index` (a public readonly field, Formation.cs:92), and adapters map it back through the
player team. All rules live in `Main/Features/MarshalsWord/Domain/`, BCL only, with a gate test.

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

The defaults follow the rule in Seat contract section 3: a default stays clear of every key the mission's scene layer reads. `N` and `I`
fail it, because they are the encyclopedia and inventory keys of `GenericCampaignPanelsGameKeyCategory` (39 and 38), which
`MissionSingleplayerViewHandler` reads in every campaign mission. `Comma` and `Period` have no reader on that layer, and `M` is in no
engine category. A test pins these three pressed defaults against the engine categories, the same way as the Seat's.

Held keys read the bound key with the static `Input.IsKeyDown`, the way `ManyHammersCalls.ShiftHeld` does, and
only while `Mission.IsOrderMenuOpen` holds. Pressed keys test the static `Input.IsKeyPressed` first, then the view's own
`Input.IsKeyPressed`, which goes quiet behind a menu but gets a new array from `Input.GetClickKeys()` on each call (Seat contract section 3).
A frame with no live session reads no key. `FordsCommandHotkeys` also uses `M`, but the Fords mission has no Marshal's Word: it has
its own camera view, so `SeatMissionFacts.ForeignCamera` turns this feature off there too. Realm Borders uses `M` on the campaign map only.
Game key 5 (ShowIndicators) also defaults to `LeftAlt`, so holding key 542 shows the banners as well. That is harmless and a help. Holding
`LeftShift` in the Seat also flies faster (game key 24), and that is harmless too.

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
  gesture (706). Its selection is separate from the player's, so the panel never sees it.
- **Never construct an `OrderController` for the player's team.** Its constructor writes `PlayerOwner = owner` on
  every formation when `Team.IsPlayerGeneral` holds (68-74), and `PlayerOwner = null` hands them all to the AI
  (Formation.cs, `PlayerOwner` setter).
- `OnOrderIssued` of the player's controller fires only for orders that went through it, so a step re-issued on
  the master controller does not look like a new player order.
- `MoveToLineSegment` fires per company with `OrderType.Move` (1578, 1590, 1599), so the event names each company.

### 5.2 Capture

One Harmony prefix class, `MarshalOrderCapturePatch`, on all six methods. Each prefix does this and nothing else:

```csharp
if (!MarshalHooks.IsLive || !MarshalHooks.QueueArmed || __instance != MarshalHooks.PlayerController) return true;
return !MarshalHooks.TryCapture(new CapturedOrder(...));   // true means it was queued, so skip vanilla
```

`QueueArmed` is written once per frame by `MarshalView`: key 540 is down and `Mission.IsOrderMenuOpen` holds.
`PlayerController` is the player team's `PlayerOrderController`, refreshed when the player team changes.

`TryCapture` declines, and vanilla runs at once, when any of these holds:

- No company is selected, or a selected company is under AI control. A delegated company takes direct orders.
- The order type is not on the allow list below.
- A company already holds `MaxStepsPerCompany` steps (one message says so).

| Allowed | Method | The step ends when |
|---|---|---|
| `Move`, `MoveToLineSegment`, `MoveToLineSegmentWithHorizontalLayout`, `PointDefence` | Position, two positions, orderable | Arrival (5.4) |
| `Charge` or `Advance` with a target company | `SetOrderWithFormation` | The target has no units |
| `AttackEntity` | Orderable | `OrderEnum` is no longer `AttackEntity`, because `Formation.Tick` swaps a spent order for its substitute (Formation.cs:2466-2470), or the patience timer ends |
| `Charge`, `StandYourGround`, `Advance`, `FallBack`, `Retreat`, `AdvanceTenPaces`, `FallBackTenPaces` with no target | `SetOrder` | At once |
| `LookAtEnemy`, `HoldFire`, `FireAtWill`, `Mount`, `Dismount`, every `Arrangement*`, `FormDeep`, `FormWide`, `FormWider` | `SetOrder` | At once |
| `LookAtDirection` | `SetOrderWithPosition` | At once |
| Mark (M4) | `SetOrderWithFormation` with key 541 | At once |

Everything else passes straight to vanilla, and that includes `FollowMe`, `FollowEntity`, `Use`, `Transfer`,
`AIControlOn`, `AIControlOff` and `FormCustom`. `FollowMe` and `FollowEntity` have no end. `Use` toggles and
depends on state at the click. `FormCustom` reads `Agent.Main.Position` (OrderController.cs:1623), and in the Seat
that agent can be gone.

The capture touches no engine state. It stores the payload (order type, method, positions, target) as an adapter
type that the domain holds as an opaque `object`.

**Empty queue while a Move is under way.** If a captured company has no queued step and its current order is a
Move it has not finished, add one implicit step that only waits for arrival. Otherwise the new step would start on
the next tick and cancel the walk the player just ordered.

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
    internal bool Started;
    internal double StartedAt, Patience;
}
```

Each company has a FIFO of `QueueStep` references. A step shared by several companies appears in each one's list.

Rules, each with a unit test over a fake adapter (`IMarshalOrderAdapter`):

1. **A step starts** when it is at the head of every member's list. This one barrier is the whole group rule: if two
   companies share steps A and C and only one has step B in between, the other waits at C until B is done.
2. **Starting** selects the members on the master controller, issues the stored order, and clears the selection,
   all inside one `OnMissionTick`. If the payload is an orderable with `GetOrder(side) == OrderType.None`, or a target
   company with no units, the step is dropped without an order.
3. **A member finishes** by the step's `End` rule, and then its list advances. Other members may still be walking.
4. **Patience.** A step with an `Arrival` end gives up at `max(MinPatience, PatiencePerEta * distance / speed)` and
   logs a counter. A company stuck behind a wall must not hold the army's queue forever.
5. **A company drops out** when it has no units or is under AI control. The driver checks both on its slow pass, so no
   formation event is needed. Thread of `Formation.OnUnitRemoved` is unproven, and this avoids the question. An
   emptied step is removed.
6. **A direct player order clears that company's queue.** Subscribe to the player controller's `OnOrderIssued`. Clear
   for every order type except firing, riding, arrangement and form orders, because those do not contradict a walk.
   Re-subscribe when the player team changes (`Mission.Teams.OnPlayerTeamChanged`), and unsubscribe at the end.
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
`OrderPositionIsValid`, and the distance from `CachedAveragePosition` to `OrderPosition` at or below
`ArrivalSlack + Depth / 2`. `OrderPosition` is the front centre of the company and the units stand behind it, so the
average sits about half a depth back (OrderController.cs:1389-1393 steps ranks back from that point). A `Column`
passes the same test, because its depth covers the snake.

### 5.5 Show the road

`MarshalView` draws one vanilla flag per queued position of the selected companies while the panel is open, and hides
the flags when it closes. Use `GameEntity.Instantiate(scene, "order_flag_small", false)` the way
`OrderTroopPlacer.AddOrderPositionEntity` does (503-530), with `EntityFlags.NotAffectedBySeason`. Keep a small pool,
and place each flag at the ground height of the step's middle point. Refresh on a queue change and at 4 Hz. At the end of the
mission drop the pool with no native call: the flags go with the scene, and vanilla's `OrderTroopPlacer` has no teardown for its own.

### 5.6 Log lines

`IOutcomeLog` with `tools/ingame_checks/0.toml`, the same draft manifest as the Seat, because one held issue covers both features (Seat
contract, Deviations row 8). Each stage adds its checks there. M1 checks: step captured, step started, step finished by
each end rule, patience spent (expected rare), queue cleared by a direct order, capture declined.

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
- `isFormationLayoutVertical` is true, two or more companies are given, and all belong to the player team, none under AI
  control.
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
3. **A drag.** New right is the line's direction, and new forward is `(-right.y, right.x)` (1318). The group's origin lands on
   the line's middle.
   - `Span` is the group's width: the largest `Across + Width / 2` less the smallest `Across - Width / 2`.
   - `Stretch = LineLength / Span`. With key 542 held, `Stretch = 1`, so the group only turns.
   - Each company's new width is `Clamp(Width * Stretch, MinWidth, MaxWidth)`. A `Column` keeps its width (6.3).
   - Each company's new `Across` is `Across * Stretch`. `Along` does not change, so the rear stays behind the front.
   - **Shoulder pass.** Walk the companies in `Across` order. If this company's depth band `[Along - Depth, Along]` overlaps an
     earlier one's, and their widths now overlap, push this company right until the gap is `OrderController.FormationGapInLine`.
     Then shift all by the average push so that the group stays centred. One pass, because there are at most ten companies.
   - Outputs: `isLineShort = false`.
4. **The short line test** is vanilla's, restated in pure code: the line is short when its length is under the bipedal radius,
   or under the sum of `MinimumWidth` plus the gaps (1208-1221).

Golden tests pin: a click keeps every offset; a drag equal to the current span and direction changes nothing; a turn of 90
degrees maps front to the new front; a stretch keeps a rear company behind; a clamp at `MaxWidth` does not shift the rest;
two side by side companies never overlap after the pass; every float output is finite for zero, huge and one-company inputs.

**What counts as a company's position.** `OrderPosition` when `OrderPositionIsValid` and its movement state is `Hold`, so that a
second order during a walk keeps the array the player last ordered. Otherwise `CachedAveragePosition` moved forward by half the
depth. Its direction is `Formation.Direction`. These are read on the main thread at the time of the call.

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

### 6.4 Arriving together

`MarshalArriveTogether`. When the prefix runs a commit (`isSimulatingFormationChanges`) with two or more companies, it records them as
one march.

`MarshalMarch` runs on the driver's slow pass (2 Hz is enough):

1. For each company: `Remaining = max(0, distance from CachedAveragePosition to OrderPosition - Depth / 2)`,
   `Speed = max(CachedMovementSpeed, 1)`, `Eta = Remaining / Speed`.
2. `Longest` is the largest Eta. When `Longest` is under 2 s, or any company's movement order is no longer that Move, the march ends
   and the table is cleared.
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
(622-627, 639-640). A soldier far from his place stays at full speed. A column never catches up (694), so it keeps its pace. Both are fine.

### 6.5 Log lines

Array solved (click or drag), fallback to vanilla, march started, march ended and its spread of arrival times in seconds.

## 7. Stage M3: loose on my word

**This stage has one unproven engine belief, so it carries its own proof first.** The belief is that the AI looses a shot by dropping the
attack bits in its movement flags, and that a component holds the draw by setting them again. Native copies the AI's stored result over the
agent's flags just before the callback (so a write lasts one tick and a hold must repeat every call), but where the AI fills that result was
not read. Build the proof log first, run it in game, and only then finish this stage. If the belief is false, stop and record it under
Deviations.

### 7.1 The engine facts

- `AgentComponent.OnAIInputSet(ref EventControlFlag, ref MovementControlFlag, ref Vec2)` (AgentComponent.cs:81) is reached through
  `Agent.OnAIInputSet` (Agent.cs:1660-1666).
- It runs on the main thread, serially, inside the native mission tick, after the managed `OnPreTick` has waited for the last agent job. So
  it never overlaps `OnMissionTick` or the AI job.
- Native calls it only for an active, AI-controlled, unridden agent whose flag is on, and only when the tick's dt is above zero.
- `Agent.SetHasOnAiInputSetCallback(bool)` (1654) sets the flag. Always call it with true when you want it. Do not rely on the default.
- Every component on the agent gets the call, and it costs a native-to-managed trip per AI agent with the flag on. So the flag stays off unless the
  soldier's company has a fire mode.
- `MissionWeapon.IsReloading` and `CurrentUsageItem` are managed reads. A bow has one reload phase, and crossbows have two.
- `Agent.AddComponent` does not call `Initialize` (Agent.cs:4409-4420), and `BuildAgent` calls it only for a component
  added before the agent is built. The driver adds this component to a living soldier, so it sets itself up in its
  constructor or on its first call, and it never depends on `Initialize`.

### 7.2 Fire modes

```csharp
internal enum FireMode { Free, OnWord, Massed }
internal enum DrawState { Idle, Drawing, Holding }
```

Key 543 cycles the selected companies: Free, OnWord, Massed, Free. Key 544 gives the word to the selected companies. Each change shows one
message. The press is read in `OnMissionScreenTick` and stored. `OnMissionTick` applies it, because adding a component is engine state.

**The driver**, on a 1 Hz pass in `OnMissionTick`, walks the units of each company with a mode other than Free. It adds `MarshalFireComponent`
to a unit with none and turns the flag on. It keeps a list of agents it has touched, by reference, and turns the flag off for those no longer in a
company with a mode. It tests `AgentSlotIdentity.IsCurrentOccupant` first. Never key on `Agent.Index`.

**The rule**, pure, in `Domain/MarshalFireRules.cs`:

```csharp
internal readonly record struct DrawInput(bool Attacking, bool Cancelling, bool StandAside, bool WordDue, double Now);
internal static DrawStep Step(DrawState state, double since, in DrawInput input);   // returns next state, since, and whether to set the attack bit
```

| State | Input | Next | Set attack bit |
|---|---|---|---|
| Any | `StandAside` | Idle | no |
| Idle | Attacking | Drawing | no |
| Drawing | Attacking | Drawing | no |
| Drawing | not Attacking, Cancelling | Idle | no |
| Drawing | not Attacking, not Cancelling | Holding | yes |
| Holding | not `WordDue` and held under `HoldLimit` | Holding | yes |
| Holding | `WordDue`, or held `HoldLimit` | Idle | no, the bit stays clear, so the shot leaves |

`Attacking` is `movementFlag & Agent.MovementControlFlag.AttackMask` non-zero. `Cancelling` is the same with `DefendMask`. Setting the bit means clearing
`AttackMask`, then adding `AttackDown`. The AI keeps its own wish to draw and aim, so this stage changes no behaviour weights and writes no `CanAttack`.

`StandAside` is read at most 3 times a second per soldier, because it makes native calls: no formation, no ranged weapon wielded, company on `HoldFire`,
detached, using a game object, or a target within `CloseQuarter`.

`WordDue` is `word time + this soldier's jitter <= now`. Jitter is drawn once when the component is built, from 0 to `WordJitter`, so that a volley sounds like
men and not a machine. The component reads the company's `FireMode` and word time from a main-thread array indexed by `Formation.Index`.

**Massed.** In `OnMissionTick`, at 5 Hz, for each company in Massed mode, count Holding soldiers and Drawing soldiers who started the draw less than
`DrawLimit` ago. A soldier with no view of a target stops counting after that. When Holding is at least one, the share reaches `ReadyShare`, and `WordGap` has
passed since the last word, give the word. In `Domain/MarshalFireRules.cs`, with tests. Group by nothing else in this contract.

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

### 7.3 The proof log

Land this first, and leave it in. `MarshalView` overrides `OnAgentShootMissile` (confirm the signature on `MissionBehavior`) and counts shots by company and by the shooter's
`DrawState`. The thread of that callback is not proved here, so it only adds to counters with `Interlocked` and touches no engine state. `OnMissionTick` reads the counters and writes the
line. Each company with a mode writes one summary line at most every 5 s:

`[Marshal] fire company=<n> mode=<m> drawing=<a> holding=<b> shotsWhileHolding=<c> shotsAfterWord=<d> shotsFree=<e>`

Pass: `shotsWhileHolding` stays near zero while `holding` is above zero, and `shotsAfterWord` rises within a second of a word. The manifest row reads it.

### 7.4 What this stage does not touch

No `AgentFlag.CanAttack`, no AI behaviour weights, no movement input and no standing rule. The archers keep walking when ordered and may draw on the march.

## 8. Stage M4: mark and show

### 8.1 Mark, do not move

Vanilla charges a focused enemy on a left release (GauntletOrderUIHandler.cs:333-345), through an order that calls `SetOrderWithFormation(Charge, target)`. In the capture
prefix of 5.2, when key 541 is held and the order is that call on the player's controller, set the target instead:

- For each selected company, when it is under the player's control: `formation.SetTargetFormation(target)` (Formation.cs:839). Leave its movement order alone.
- Return false, so vanilla does not run. With key 540 also held, queue the same thing as a step.

A later movement order clears the mark, because `SetMovementOrder` ends with `SetTargetFormation(null)` (Formation.cs:734). That is the intended rule, and the feature doc says so.
The engine clears a dead target on its own (2475).

**Who wins against the smart cavalry cycle.** The player. `Patch31b` postfixes `SetTargetFormation` and re-points a running cycle at the chosen formation, so this needs no new code.
A queued Move goes through `Formation.SetMovementOrder` like any player order, and it cancels the cycle (`docs/reference/harmony-patch-registry.md`, section Patch31). Write a test that the
master controller's path reaches the same patches.

### 8.2 Outlines

`MarshalOutlineService` runs in `OnMissionTick` at 4 Hz, only while `MarshalOutlines` holds and either the panel is open or game key 5 (ShowIndicators) is down. The screen tick reads that key and stores one bool, because nothing in `OnMissionTick` reads a key (section 2).

The plan is pure. Given the wanted colour of each unit, it returns who to paint and who to clear, against a dictionary of what it last painted, keyed by `Agent` reference. `Domain/` gets
the diff over integers, with tests. Only agents it painted are ever cleared.

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

Vanilla's square order builds a `RectilinearSchiltronFormation`, which packs itself and ignores width (ArrangementOrder.cs:121; FormOrder.cs:95-98). The engine already has the plain `SquareFormation`, which keeps a chosen side
width (SquareFormation.cs:63-79; FormOrder.cs:120-133) and has a working `Clone` (53-56). `Formation.Rearrange` and the two simulation methods test arrangements by exact type, and `Clone` keeps them consistent (Formation.cs:2550, 3117, 3172). **Use the plain engine class. Write no subclass.** Its members are private, and the two blemishes a subclass could fix (the middle slot, the corner soldiers' facing) do not pay for the code.

Prefix on `ArrangementOrder.GetArrangement(Formation formation)` (struct method, 114): when `MarshalHollowSquare` and `IsLive` hold, the order is `Square`, and the formation is on the player team and not under AI control, or is a simulation copy (`Team == null`) with the feature live, return `new SquareFormation(formation)`.

Two engine lines would break it, so each gets one small prefix, gated on `Arrangement.GetType() == typeof(SquareFormation)`, so that vanilla squares and every other arrangement run vanilla's code:

- `Formation.ReapplyFormOrder` (private, 2914-2922) copies `Arrangement.FlankWidth` into the custom width. A square's flank width is its perimeter (SquareFormation.cs:77), so the square would grow each time a unit joins or leaves. Keep the side: set `CustomFlankWidth = Arrangement.Width`, call `SetFormOrder(formOrder, false)`, and return false. It can run on either thread, so it reads only the formation it is given.
- `Formation.CalculateDesiredWidth` (private, 2924-2927) returns a line's flank width, which then reads as a side. Convert it, from the engine's own relation `flank = 4 * (side - diameter) - interval`: `side = (flank + Interval) / 4 + UnitDiameter`, clamped to `MinimumWidth` and `MaximumWidth`. Pure helper, with a round-trip test against the engine formula.

Read `Formation.SetArrangementOrder` (742-770) and record in the feature doc what a switch from line to square does with this. Test with trolls, because `Patch92` changes `UnitDiameter`.

### 9.2 Hold the line

Key 545 toggles a company. A shielded foot soldier in a shield wall, circle or square, under an order that is not a charge, stops attacking and stays in his slot while enemies press. He strikes back for `StrikeBackSeconds` after a real wound.

It uses the same component as M3, so `MarshalFireComponent` becomes `MarshalWordComponent`, and both rules run in one `OnAIInputSet`. One owner writes `AgentFlag.CanAttack` (`GetAgentFlags`, `SetAgentFlags`). Volley never writes it, so there is no second writer.

- **Qualifies:** arrangement, order, a carried shield (`HasShieldCached`, which does not mean wielded), no mount, no ranged weapon wielded. Read at most 3 times a second.
- **While qualified:** `CanAttack` off. `Agent.SetFiringOrder(HoldYourFire)` per agent, and restore from the formation on leaving. `Formation.AddUnit` and `RemoveUnit` overwrite it (2319, 2382), and so does a change to the company's firing order (801-804), so the 3 Hz read writes it again whenever it differs. Never remove `CanAttack` during a reload.
- **The slot clamp.** `CanAttack` off does not stop feet. If `inputVector`, which is in the agent's local frame, points away from his slot by more than `SlotAwayDot`, and he is more than `SlotSlack` from the slot, replace it with a local vector toward `Formation.GetOrderPositionOfUnit(agent)`, of length at most 1. Convert with `Agent.Frame.rotation`. This is an assumption to prove in game: log the distance to the slot.
- **Strike back:** `AgentComponent.OnHit` records the time of a blow that is not a missile and not stopped by the shield. That callback can run on either thread, so write one `double` field, and read it on the main thread.
- **Turning it off** walks the units and restores `CanAttack` and the firing order. Never leave a soldier stuck: also restore on `OnFormationSet`, on the mode's end, and when the mission's order changes to a charge.

| Constant | Value |
|---|---|
| `StrikeBackSeconds` | 4 s |
| `SlotAwayDot` | 0.3 |
| `SlotSlack` | 0.25 m |

### 9.3 Spacing repair

Vanilla's four spacing limits (`MinimumDistance`, `MaximumDistance`, `MinimumInterval`, `MaximumInterval`, Formation.cs:587-595) test `HasAnyMountedUnit`, so one rider gives an infantry company cavalry limits. The live values (`UnitDiameter`, `Interval`, `Distance`, Formation.cs:220, 520, 544) test `CalculateHasSignificantNumberOfMounted`. Make the limits agree with the live values. Every other part of those getters is already right, including dismount and arrangement spacing.

The smallest change that holds is one transpiler per getter replacing the one call to `get_HasAnyMountedUnit` with `get_CalculateHasSignificantNumberOfMounted`. Read `docs/reviews/lessons/harmony-il.md` and pin the call count in a binding test. Gate with `MarshalSpacingRepair` at patch time (a process-lifetime read is enough, and say so in the setting's hint). It applies to every team, because the fix is a base-game consistency fix. Test with `Patch92`, which postfixes `UnitDiameter`, and read the registry section first. The getters run on workers, so no state and no setting read in the path.

### 9.4 Throwers on an advance

Vanilla sends a thrower company to melee range on an Advance order (MovementOrder.cs:1170-1178), because `IsRangedFormation` reads a non-consumable ranged test. Build this only after 9.1 to 9.3 are proven, and only if the night leaves room. For an order with a position, vanilla's `GetPositionAux` runs about once per `Formation.Tick` on the AI thread, and workers reach it only for a unit with no stored slot. So a rule there costs per company and per tick. TAOM's rule: on the main thread's slow pass, compute a stand point for a player company whose units are mostly throwers and hold ammunition, at the median of its missile range in front of the target, and publish it in an immutable table. A prefix on `MovementOrder.GetPositionAux` for `Advance` reads that one point. It must not run a native call on the AI thread beyond what vanilla does at that depth (`GetNavMeshMT`, MovementOrder.cs:589). `MovementOrder`'s type initializer reads `Mission.Current`, so this patch joins `Patch_MissionTime_SetMovementOrder` timing. Log a counter of points that had no navmesh.

## 10. Stage M6: the panel

Hotkeys come first, and every order above works with no panel entry. This stage adds the entries.

`VisualOrderFactory.GetOrders` walks providers from last to first, and the first available one supplies every order (VisualOrderFactory.cs:40-49). So a TAOM provider subclasses `DefaultVisualOrderProvider` (`TaleWorlds.MountAndBlade.View.VisualOrders`, client-only, so it lives in `Client/Features/MarshalsWord/`), is registered through the composition the way `HostReviewVisualOrderProvider` is, and returns false from `IsAvailable` while the host review mission is active. It takes the base sets and adds one set: fire mode, give the word and hold the line, with vanilla icon ids. Read `DefaultVisualOrderProvider` and `VisualOrder` (the overridable members are at lines 19-38) before you add a slot, because a set past the shortcut count needs a decision. Another mod's provider registered later wins whole, and the keys still work.

The engine tests these strings and types by value, so never rename or drop a base set: the string id `order_movement_charge` (GauntletOrderUIHandler.cs:582), the set ids `order_type_movement` and `order_type_facing` (178-179), the icon id `order_type_facing` (MissionOrderVM.cs:97), the icon ids `order_movement_advance` and charge (MissionOrderTroopControllerVM.cs:815, 839), and the `ReturnVisualOrder` type tests (GauntletOrderUIHandler.cs:461-496, MissionOrderVM.cs:538). `BannerlordConfig.OrderType` picks the bar or the radial movie and `OrderLayoutType` picks the sets, and any pairing is possible, so test both layouts in both movies. The panel's `OrderSets` list is empty until the first open (MissionOrderVM.cs:715), which is one more reason the keys never go through it.

Clickable buttons, cards and a marked-target icon are not in this stage. A later stage can take the panel through `[OverrideView]` on `MissionGauntletSingleplayerOrderUIHandler` (it has the attribute itself, line 17) with a `MissionOrderVM` subclass and TAOM-named prefabs. TAOM already sets `UIConfig.DoNotUseGeneratedPrefabs = true` (`Dependencies/SubModule.cs:42`), so TAOM's prefab XML loads with no further work. Do not change that flag. `[OverrideView]` does not reach `OrderTroopPlacer` in campaign or custom battle, because those mission lists construct it directly, so never plan on a placer subclass.

## 11. Definition of done for each stage

- Pure tests go first and stay green. A gate test keeps `Main/Features/MarshalsWord/Domain/` free of `using TaleWorlds`.
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` is green, including composition and binding gates.
- `docs/features/marshals-word.md` from `TEMPLATE.md`, a feature-map row, a `Patch141_MarshalsWord` registry section, in-game check rows, and localized strings through the registration steps of `.claude/skills/localize/SKILL.md`, with translation left to the backlog.
- Every in-game check has a log line that proves pass or fail.

## Deviations

Record each change to this contract here with its reason.

1. **Before any build, from the Seat's S1 review.** Section 2: the mode change callback only sets a flag, nothing caches the kind or the
   foreign-camera walk, and the player team is tested on each tick until the session is live. Section 3: a pressed key tests the static first,
   because the view's own test allocates. Section 5.5: the end of the mission drops the flag pool with no native call. Section 7.2: a soldier
   with no formation stands aside. Section 7.3: the shot callback only adds to counters, because its thread is not proved. Section 8.2: the
   screen tick reads game key 5, because nothing in `OnMissionTick` reads a key.
