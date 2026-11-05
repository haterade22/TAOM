# Marshal's Word

## Overview

Marshal's Word gives the player richer orders for companies (formations) in a battle. This file covers the stages built so far, of
the six in [marshals-word/build-spec.md](marshals-word/build-spec.md). **M1, Orders in turn:** hold the queue key (Give the order in turn,
default Left Shift) while giving an order with the order panel open, and the company carries it out after its current orders. Flags on the
ground show the road ahead. **M2, Keep the array:** several selected companies given one click or drag move as one body, keep their places
relative to each other, and walk at paces that bring them in together. **M3, Loose on my word:** key 543 (Change the
Archers' Fire, default Comma) cycles the selected companies through firing at will, holding a drawn bow until the word, and Massed fire, and key 544 (Give the Word,
default M) looses every archer who was already holding, a fraction of a second apart. On Massed fire the word is given by itself once 70 percent of the archers who
can still shoot hold a drawn bow. The game has shown that a bow stays drawn (35 of 40 held, 36 of 37 loosed at the word), so Massed is built. **M4, Mark and show:** hold Mark, Do Not Move (default Left Ctrl) while
you order a charge on an enemy company, and the company takes that company as its target and keeps its own movement order (a mark steers
archers and other ranged soldiers; soldiers fighting hand to hand choose their own foe); gold outlines
show the selected companies and red outlines the company one of them marks or charges. **M5, hollow square, hold the line, small repairs:** a Square order on a company you command forms the plain square of
the base game, which keeps the side you drag and grows only when its soldiers no longer fit; key 545 (Hold the Line, default Period) toggles a
company: its shielded foot soldiers in a shield wall, circle or square, under an order that is not a charge, stop attacking and keep their places, a soldier
wounded by a blow strikes back for four seconds, and a charge, the toggle or the hero's fall gives every soldier his attack flag and firing order back; and a
company of foot soldiers with riders under a tenth of its soldiers keeps the foot soldiers' spacing limits. **M6, the panel:** the order panel gets one more set, Marshal's Word, with
Archers' fire, Give the word and Hold the line. The throwers on an advance (9.4) are not built: the build stopped on that point (Deviations row 25).

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
`OnRemoveBehavior`) call one safe-to-repeat `Finish()` and drop the session. In the screen tick, and only while the session is live, the view
reads the three held keys only with the order panel open, and the three pressed keys and the show-indicators key on every live frame; it forwards
the mission tick, the shot callback and the mode change.

- **Going live** (`MarshalSession`): at the first mission tick, and after each mode change until the answer is yes, it asks
  `MarshalEnabled`, then `SeatMissionPolicy` (a Battle or Hideout kind with no foreign camera) and the network fact (`GameNetwork.IsSessionActive` or a live co-op session, `SeatMissionClassifier.InNetworkSession`). While the
  answer is yes and there is no player team yet, each tick tests the team. Once live it stays live unless the fault latch ends it.
- **The fault latch:** the screen tick, the mission tick and the capture each run in one try. A throw clears `MarshalHooks`, writes one
  `fault` fail line, shows one message, and a capture then answers "not queued", so vanilla runs the order. The screen tick and the capture write
  only TAOM fields and hide the flags, so a fault there leaves the outline clear and the archers' release to the next mission tick, owed by
  one field; a fault in the mission tick pays it at once. The release turns off the callback flag of every soldier the archers' driver touched
  and resets its component (`IMarshalFireAdapter.ReleaseAll`, the one teardown of the hold that makes native calls); without it the mission would
  go on with a native-to-managed call per touched soldier for the rest of the battle. The finish makes no native call and forgets both debts. The other entries have no try of
  their own: `ArraySolved`, `ArrayFault` and `MarchRequested` run inside the array prefix's try or catch, and the team-change and
  direct-order callbacks call only list work, the outcome log and a pace publication, none of which throws.
- **Capture** (`MarshalOrderCapturePatch`, Patch141): a prefix on each of the six `OrderController` order methods. It returns true at its first
  test unless the session is live, the queue key or the mark key is down with the panel open, and the instance is the player's own controller. Otherwise
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

- **The archers' hold** (`MarshalWordComponent`, `MarshalFireDriver`, `MarshalFireTable`, `MarshalFireAdapter`): native calls
  `AgentComponent.OnAIInputSet` once a tick for an AI-controlled soldier whose callback flag is on (Agent.cs:1660-1666), with the flags the AI
  has just decided. While a company's mode is on-word or Massed, or its hold the line is on, the driver (once a second of mission time) adds the component to each of its soldiers
  and turns the flag on; the component then reads the AI's own attack bits and follows `MarshalFireRules.Step` (pure, in `Domain/`): a bow
  the AI has drawn is kept drawn by adding the attack-down bit on every call, with the defend and abandon bits cleared so the draw is not
  cancelled, and it is let go by clearing the attack bits at the word, after 14 seconds, or when the soldier has a reason to stand aside (the
  mode went to free, no ranged weapon wielded, a hold-fire order, detached, using an object, a target within 7 m). The stand-aside facts are
  native reads, so they are read at most three times a second and kept. A word loosens only a soldier who was already holding when it was
  given, after a jitter of up to 0.3 s drawn once per soldier. A company under AI control has no mode: the next pass sets it to free. A change
  to on-word also gives the company fire at will through the master controller, so an older hold-fire order does not make the new mode look
  broken. The callback runs in one try; a throw latches it off for the process and writes nothing to the flags. The soldiers the driver
  touched are kept by reference (never `Agent.Index`); one no longer in a ruled company gets its flag off and its component reset, and only
  while it still is the slot's occupant. A word is measured against a census taken at the word, not one up to a second old, and a second word
  given before the first one's release window is judged leaves that window as it was.
- **The proof log of the hold:** `MarshalView.OnAgentShootMissile` forwards to `MarshalFireTable.OnShot`, which counts a shot of a
  player-team soldier whose company has a mode, with `Interlocked` and no lock (native chooses that callback's thread): while holding, in the
  two seconds after a release, or any other. A light pass four times a second judges two checks with `MarshalVolleyJudge` (pure): the
  hold check (at least five holding through five seconds with no word, at most one shot in twenty of the smallest holding count) and the
  release check (at a word with at least five holding, at least 70 percent loose within 1.3 s). The driver also writes the `[Marshal] word`
  line at each word and a `[Marshal] fire` summary every five seconds and once more when the mission ends, for every company the table holds a
  mode for, including one wiped out just before the end. The `[Marshal] fire` line ends `idle= aside= shotsAfterOther=`: `idle` counts the
  soldiers in neither draw state who do not stand aside, `aside` the ones who do, and the shots in the two seconds after a release are counted
  by its cause, `shotsAfterWord` for a release the word caused and `shotsAfterOther` for one the hold limit or standing aside caused, so
  a hold that ran out is never read as the word's answer. The field trial's control counts every shot of a company in any mode
  (`MarshalFireTable.BeginWatch`, `ShotWatch`).
- **The mark** (`MarshalQueueDriver.CaptureMark`, `MarshalMarkWatch`): the capture prefix now arms on key 540 or key 541, each held with the
  panel open. With key 541 held, a player's charge or advance on an enemy company is turned into a mark: one request per selected company
  under the player's control, applied at the next mission tick by `Formation.SetTargetFormation` (a native write per soldier, so never in
  the screen tick), and vanilla's order does not run. The company's movement order stays. With key 540 held too, the mark waits its turn in
  the queue as an instant step that selects nothing and orders nothing. With no company under the player's control the capture declines and
  vanilla runs the charge. `MarshalMarkWatch` samples at most 20 soldiers of each company that holds a mark once a second, writes
  `[Marshal] mark company= target= onTarget= of=`, and after three seconds judges a ranged company on Stop. A melee company on a Move is
  sampled and logged but never judged (Deviations row 19). A company on Charge, Advance or ChargeToTarget is neither sampled nor logged:
  vanilla keeps a target with those orders (`OrderController.cs:812-824`, `MovementOrder.cs:684-685`), so it is a mark by accident, and
  sampling it cost a walk of the company, up to 20 native reads and one log line a second for every charging company.
- **The outlines** (`MarshalOutlineService<TKey>`, `OutlineDiff`, `MarshalOutlineAdapter`): four times a second of mission time, while they
  should show or while anything is painted, the service asks the adapter for the colour of every unit that should wear one (gold for a
  selected company, red for the company a selected company marks or charges), plans with the pure generic `OutlineDiff` against what it
  last painted, and paints or clears through the adapter, which reads a rider's mount as a wanted unit of its own (so the dictionary remembers
  every visual it painted and a mount is cleared even after its rider dismounted or died) and guards each write with the slot's current
  occupant. They show while `MarshalOutlines` is on and the panel is open or game key 5 (show indicators) is down. When they should not
  show, the wanted set is empty and that pass clears everything it painted; the finish only forgets the dictionary, because the agents go
  with the mission, and a fault clears the paint before it tears down (in the mission tick: see the fault latch above). That clear tries each
  unit on its own and throws the first failure after the others are done; a unit that failed is not tried again, because nothing is known to throw
  there and a retry loop would be code for a guess.

- **Massed fire** (`MarshalFireRules.Eligible`, `IsStaleDraw`, `MassedWordDue`; `MarshalFireDriver.MassedPass`; `IMarshalFireAdapter.Count`): five times a second of
  mission time the driver counts the soldiers already touched in each Massed company, over the cheap `Count` that adds no component and writes no flag. A
  soldier is eligible when he holds, draws or stands ready, and is not standing aside; a drawing soldier whose draw began more than 6 seconds ago is left out,
  because a soldier with no view of a target stops counting after that. When at least one soldier holds, at least 70 percent of the eligible hold and 2.5
  seconds have passed since the company's last word, the driver gives the word through the same path as a pressed one: the same release window, the same
  `[Marshal] word` line, and a `[Marshal] massed company= holding= eligible= stale= idle= aside=` line. The check `massed` is the volley that followed a Massed
  word. One ready archer among thirty reads 1 in 30 and gives no word.
- **Hold the line** (`MarshalHoldLineRules`, `MarshalHoldRules`, `MarshalHoldWindow`, `MarshalWordComponent`, `MarshalFireAdapter.Hold`, `MarshalFireDriver.HoldPass`, `MarshalFireTable`): key 545 toggles
  the selected companies under the player's control. Four things run for a held company, and every write waits for the mission tick.
  - **Who stands in the line** (`MarshalHoldLineRules.Qualifies`, pure): the arrangement is the shield wall, circle or square; the company's order is not Charge,
    ChargeToTarget or Retreat; the company is not under the AI; the soldier carries a shield, has no mount, wields no ranged weapon, is not detached, is not using a game
    object and is not the main agent. The component reads these at most three times a second (`HoldReadSeconds`), from `OnAIInputSet` and from the hold pass, and keeps the first
    clause that failed (`why=` on the slot line). A company that goes to the AI, the toggle, or a charge leaves the line, so the hero's fall cannot leave a company disarmed with
    nobody to press the key.
  - **The flag and the firing order** (`RefreshHold`, called by the hold pass): while the line stands the soldier's attack flag is cleared and his firing order is hold fire, written again whenever
    it differs; when it stops both go back, the flag by `MarshalHoldRules.FlagAction` (only a flag this component cleared) and the order from the company's own (`MarshalHoldLineRules.FiringAction`). The debt of
    each stays until its write was made. The 1 Hz sweep adds the component and turns the callback on; the hold pass (`IMarshalFireAdapter.Hold`, every third of a second, over the touched soldiers
    of the held companies, with `MissionThreadGuard.NoteCall`) applies the want and counts the line.
  - **The strike back:** `OnHit` records the time of a wound (`damage > 0`, not a missile; a parried blow arrives with no damage). For `StrikeBackSeconds` after it the want is `StrikeBack` and the
    flag is on; the blows of such a soldier are counted apart (`MarshalFireTable.StrikeBlowsOf`), so the proof of `hold-line`, which compares the held company's blows with a control's, is not spoiled by the
    line's own answer.
  - **The slot clamp:** the slot is `GetOrderPositionOfUnit` under a Move, and under Stop the place the soldier stood at when the order was read, kept as an anchor (the engine answers the soldier's own
    position under StandGround, so a Stop would otherwise show a distance of zero). Once a soldier is more than `SlotSlack` from his slot and the AI's input points away from it (a cosine under minus
    `SlotAwayDot`), `OnAIInputSet` replaces the input with a step toward the slot, in the soldier's local frame, of length at most one (`ClampInput`; an input that is not a number comes back unchanged).
    `OnFormationSet` only expires the next read. The clamp tests a zero input before it reads `Agent.Frame`, a native read. The trend of a steered soldier is the distance to his slot at his first replaced tick against the distance at the next hold read,
    and a soldier in his strike back window is left out of it. Soldiers the engine says run away (`Agent.IsRunningAway`, Agent.cs:810, set by the native retreat callback at 1641-1646) or walk to a game object such as a ladder
    (`HumanAIComponent.IsInterestedInAnyGameObject`, HumanAIComponent.cs:521-524) are out of the line, so the clamp never stops a rout or that walk. The clamp replaces only an input that points away from the slot: it does not
    walk back a soldier who was pushed or drawn off, and a soldier whose input is zero or sideways stays where he stands (a named limit below).
  - **The proof:** the view's `OnAgentHit` counts a melee blow of a player-team soldier on another team's soldier per company. Every 5 seconds the driver writes
    `[Marshal] hold company= units= flagOff= blows= control= controlBlows= seconds=` and `[Marshal] slot company= units= qualified= flagOff= anchored= far= max= mean= order= wounded= struck= why= closer= farther= steered= loose=` (and once more when the hold goes off or the mission ends, for the window then open, with no verdict), and the judged checks are
    `hold-line` (the blows of the soldiers in the line against the busiest company that is not on hold, over 10 seconds that begin 3 seconds after the press; a soldier of the held company who is not in the line deals blows that do not count against it, and a control soldier always counts. A control that dealt under ten blows takes a new baseline and the window stays open), `hold-slot` (under Stop, with the line wounded and at least five steered reads in the window: no more of the steered soldiers ended farther from their slot than nearer;
    `far=` and `max=` are facts, not tests) and `hold-strike` (at least half of the soldiers whose wound is two reads old have the flag on).
  The stage rests on beliefs only the game proves: that the flag stops an AI that already holds a target (the first run says it does), that clearing it mid swing is safe, that the AI's `inputVector` is in the
  local frame with y forward, and that replacing it moves a soldier at all. `hold-slot` and the slot line are how the last two are read.
- **The panel** (`MarshalVisualOrderProvider`, `MarshalOrderSet`, `MarshalPanelOrders`, `MarshalOrderProviderRegistrar`, `MarshalPanelRules`): a provider that subclasses the base game's and adds one set, Marshal's Word, with three entries that store
  the presses of keys 543, 544 and 545 for the companies the panel had selected. It answers only in a live Marshal's Word mission that is not a hideout; otherwise the factory falls through to the base game's provider.
  The set goes before the legacy layout's trailing Return, which takes its F8, and last in every other list, with no key of its own. Nothing here writes an agent, a formation or an order.
- **The hollow square** (`MarshalSquareArrangementPatch`, `MarshalSquareReapplyPatch`, `MarshalSquareWidthPatch`, `MarshalSquareRules`, `MarshalProofWatch`): vanilla's
  Square order builds a `RectilinearSchiltronFormation`, which packs itself and ignores width. While the session is live and Hollow Square is on, the prefix on
  `ArrangementOrder.GetArrangement` builds the engine's plain `SquareFormation` for a company of the player's team that is not under AI control. Two small prefixes
  on `Formation`, gated on the arrangement being exactly that class, keep the side when a unit joins or leaves (`ReapplyFormOrder` would copy the perimeter) and read the side
  as the desired width (`CalculateDesiredWidth`). The session's slow pass writes `[Marshal] square company= side= files= ranks= units= placed= laidOut=` when a square forms and
  at most once a second when its unit count changes; the check `square` passes when `placed == units`. A switch from line to square runs `Formation.SetArrangementOrder`
  (Formation.cs:742-770): it ends the old order, sets the new one, and when the form order is Custom it sets a custom form order of `CalculateDesiredWidth()`, so the width prefix
  is what the new square is sized from. A square entered from a column starts packed, because `ColumnFormation` is not a `LineFormation`.
- **The spacing repair** (`MarshalSpacingPatch`, `MarshalSpacingRules`): the four spacing limits (`MinimumDistance`, `MaximumDistance`, `MinimumInterval`,
  `MaximumInterval`, Formation.cs:587-595) and the three public `GetUnitPositionWithIndexAccordingToNewOrder` overloads (1683-1695) test `HasAnyMountedUnit`, so one rider
  gives a foot company cavalry limits, while the live `UnitDiameter`, `Interval` and `Distance` test `CalculateHasSignificantNumberOfMounted` (a tenth of the company). One
  transpiler over those seven methods swaps the one call. It applies when Patch141's category applies at the first mission, only when Marshal's Word and Spacing Repair are on and
  neither co-op nor a dedicated server is present. Three readers keep the old value: the iterator at Formation.cs:3174, `OrderController.cs:1363` and the spawn frames at
  `Mission.cs:3975`. The getters can be inlined, and a patch on an inlined method never runs, so the session writes `[Marshal] spacing` once for the first company with a rider
  but not a significant number of them and the check `spacing` passes when its limits read as a foot company's.

### Component Diagram

```
MarshalView (screen tick: keys, panel; mission tick; both teardowns)
        |
  MarshalSession ---- MarshalHooks (statics the patches read) <---- MarshalOrderCapturePatch / MarshalArrayPatch / MarshalPacePatch
   |        |                                                              |                 |
   |  MarshalQueueDriver --> MarshalOrderQueue (Domain)         MarshalArrayLayout --> MarshalArraySolver (Domain)
   |  MarshalMarchDriver --> MarshalMarch (Domain)
   |        |
IMarshalScreenAdapter     IMarshalOrderAdapter (master controller, formations, paces, marks)
(messages, flags)         MarshalOrderPayload (the captured call, a mark)
   |
   +-- MarshalFireDriver --> MarshalFireRules / MarshalVolleyJudge / MarshalHoldLineRules (Domain) ; IMarshalFireAdapter ; MarshalFireTable <-- MarshalWordComponent (native)
   +-- MarshalMarkWatch ; MarshalOutlineService<Agent> --> OutlineDiff (Domain) ; IMarshalOutlineAdapter<Agent>
   +-- MarshalProofWatch --> MarshalSquareRules / MarshalSpacingRules (Domain) ; IMarshalOrderAdapter.ReadSquares / ReadSpacing
   +-- MarshalSquare*Patch, MarshalSpacingPatch (Patch141, read MarshalHooks.HollowSquare and the settings at patch time)
```

## Configuration

MCM group "Marshal's Word", all defaults on, read once a frame through a lazy accessor (`MarshalSettingsProvider`). The six settings are
excluded from the co-op fingerprint in `CoopSettingsRelevance`.

| Setting | Default | Effect |
|---|---|---|
| `MarshalEnabled` | on | Read when the session decides whether to go live. Off, none of this acts from the next battle on, except Spacing Repair, which is decided when the game starts and needs this on at that start; a battle already running keeps the feature (build-spec Deviations, row 3) |
| `MarshalKeepArray` | on | A click or drag moves several companies as one body. Off, vanilla lays them out |
| `MarshalArriveTogether` | on | Companies ordered together walk at paces that bring them in together. Needs Keep the Array |
| `MarshalOutlines` | on | Gold and red outlines while the panel is open or the show-indicators key is held. Off, every outline is cleared at the next pass |
| `MarshalHollowSquare` | on | A Square order on a company you command forms the plain square of the base game. Off, vanilla's packed schiltron |
| `MarshalSpacingRepair` | on | A company of foot soldiers with riders under a tenth of its soldiers keeps the foot soldiers' spacing limits. Read once when the first mission begins, with Marshal's Word, so a change needs a restart (`RequireRestart`) and it applies only if Marshal's Word is on at that start; off under co-op and on a dedicated server whatever its value |

Keys (Options > Keybindings > Action, category `TaomMarshalHotKeyCategory`, ids from 540): **540 Give the order in turn**, default Left Shift,
held. **541 Mark, do not move**, default Left Ctrl, held. **542 Keep the widths**, default Left Alt, held: a drag turns the group without
stretching it. **543 Change the archers' fire**, default Comma, pressed (free, on-word, Massed). **544 Give the word**, default M, pressed. **545 Hold the line**, default Period, pressed. The three held keys are
read only while the order panel is open; the three pressed keys act on the selected companies whenever the player commands the team
(`IsPlayerGeneral || IsPlayerSergeant`, the question the panel asks before it opens), and their defaults are in none of the eight
categories the mission's scene layer reads; they are vanilla's keys too, so they still act with the panel closed (zoom, banners), and Left Alt has one more
vanilla reader, `GauntletInformationView.cs:61`, which extends the on-screen message timer. A held key is the contract's accepted exception to the rule that a default key stays clear of every key the scene layer reads:
Left Shift is also game key 24 (Zoom), so on the hero's camera holding it zooms; Left Alt is game key 5 (ShowIndicators), so it shows the
banners and, with Outlines on, the outlines. Left Ctrl has three vanilla readers (a ground drag lays out horizontally, which also turns Keep
the Array off for that drag; the number keys select no company; a click on a card adds it to the selection), so a player who selects with
Ctrl and still holds it on the release marks instead of charging. Period, like Comma and M, is in none of the eight categories the scene layer reads.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MarshalsWord/Domain/` | Pure rules: `MarshalOrderQueue` and `QueueStep`, `MarshalArraySolver`, `MarshalMarch`, `MarshalChecks`, `MarshalTuning`, `MarshalFireRules` (the draw state machine and the stand-aside rule), `MarshalVolleyJudge`, `MarshalMarkRules`, `OutlineDiff`, `ShotWatch`, `MarshalHoldLineRules` (who stands in the held line, the strike back, the firing order, the slot and the clamp), `MarshalPanelRules`, and the trial's `MarshalTrialRules` and `MarshalTrialRows` (BCL only, gated) |
| `Main/Features/MarshalsWord/MarshalFireDriver.cs`, `MarshalWordComponent.cs`, `MarshalFireTable.cs`, `MarshalMarkWatch.cs`, `MarshalOutlineService.cs`, `IMarshalOutlines.cs` | The archers' hold, Massed fire, hold the line, their shot and blow counters, the mark proof and the outlines |
| `Main/Features/MarshalsWord/MarshalProofWatch.cs`, `Domain/MarshalHoldRules.cs`, `MarshalSquareRules.cs`, `MarshalSpacingRules.cs` | The square's and the spacing repair's proof lines and the pure rules of hold the line, the square and the spacing |
| `Main/Features/MarshalsWord/Hooks/MarshalSquareArrangementPatch.cs`, `MarshalSquareReapplyPatch.cs`, `MarshalSquareWidthPatch.cs`, `MarshalSpacingPatch.cs` | The four M5 patch classes |
| `Main/Adapters/IMarshalFireAdapter.cs`, `MarshalFireAdapter.cs`, `IMarshalOutlineAdapter.cs`, `MarshalOutlineAdapter.cs` | The engine side of the hold and of the paint |
| `Client/Features/MarshalsWord/Trial/` | The Marshal half of the field trial: `CommandTrialScript.Marshal.cs`, `CommandTrialScript.MarshalM5.cs`, `CommandTrialScript.MarshalHold.cs`, `CommandTrialHost.Marshal.cs`, `ICommandTrialHost.Marshal.cs` |
| `Client/Features/MarshalsWord/MarshalVisualOrderProvider.cs`, `MarshalOrderSet.cs`, `MarshalPanelOrders.cs`, `MarshalOrderProviderRegistrar.cs`; `Main/Features/MarshalsWord/IMarshalOrderProviderRegistrar.cs`, `NullMarshalOrderProviderRegistrar.cs` | The panel set, its three entries and their registration (stage M6) |
| `Main/Features/MarshalsWord/MarshalsWordModule.cs` | Feature module: services, the keys, `Patch141_MarshalsWord` at the first mission |
| `Main/Features/MarshalsWord/MarshalQueueDriver.cs`, `MarshalMarchDriver.cs`, `CaptureResult.cs` | The slow passes, over `IMarshalOrderAdapter`, and what a capture answers |
| `Main/Features/MarshalsWord/MarshalHooks.cs`, `MarshalPaceTable.cs`, `IMarshalHookSink.cs` | The statics the patches read, the pace table, the sink interface |
| `Main/Features/MarshalsWord/Hooks/` | The seven patch classes, and `MarshalArrayLayout.cs`, the static helper that only the array patch calls |
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
`MarshalOrderPayloadTests` (the allow list row by row, the widths key a drag records, the flag height), `MarshalFireRulesTests`, `MarshalVolleyJudgeTests`,
`OutlineDiffTests`, `MarshalFireDriverTests`, `MarshalMarkAndOutlineTests`, `ShotWatchTests`, `MarshalMarkCaptureAndShotTableTests` (the mark payload, the queue driver's marks, the capture prefix with the mark key,
the shot table, and the source pins with the engine line behind each), `MarshalTrialRulesTests`, `CommandTrialMarshalStepsTests` (each trial step passes on a working stand-in and fails with its own facts when the one thing it reads is broken; the limits of that claim are under "The trial's limits" below), `MarshalPatchTests` (bindings,
parameter names, drive each prefix, the pace table), `MarshalAdapterPinTests` (the engine-facing adapter statements a test cannot run, each
with its engine line), `MarshalsWordWiringTests` (the Domain gate, the module, the view's forwards), `MarshalsWordChecksRenderTests` (the
[Check] lines the code writes against the fixtures and the manifest), `TaomMarshalHotKeyCategoryTests`, `MarshalSettingsProviderTests`, `MarshalMassedAndHoldDriverTests` (Massed and hold the line in the driver), `MarshalHoldRulesTests`, `MarshalSquareRulesTests`, `MarshalSpacingRulesTests` (the pure M5 rules),
`MarshalHoldLineRulesTests` (qualifying clause by clause, the strike back, the firing order, the slot and the clamp, with the not-a-number cases), `MarshalHoldLinePassTests` (the 3 Hz pass in the driver and its two judged checks), `MarshalPanelRulesTests`, `MarshalPanelPressTests` (a click names its companies), `MarshalPanelWiringTests` (the provider, its entries and the registration), `MarshalProofWatchTests`, `MarshalSquareAndSpacingPatchTests` (bindings, fall-throughs, the transpiler over synthetic and real IL), `MarshalHoldLineWiringTests` (the engine-facing statements a unit run
cannot execute, each with its engine line), `MarshalTrialRulesM5Tests`, `CommandTrialMarshalM5StepsTests`, `MarshalWordingTests` (the English of every later-stage text against its literal, its row and the seed row of each language, the 14 seconds against `MarshalTuning.HoldLimit`, and each default named in a hint against the key or the setting it names).
`tools/tests/test_ingame_verdict.py` holds `MarshalsWordManifestTests` and the `marshal_*` fixtures (`ls tools/tests/fixtures/ingame_verdict/marshal_*.txt` counts them); `tools/tests/test_run_command_trial.py` ties the wrapper's `MARSHAL_STEPS` to the row list and the judge to Marshal's failure lines.

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
6. Archers: select a company of archers and press Comma. A message says they draw and hold until the word, or for 14 seconds at most. Wait until enemies are in range:
   the bows stay drawn and no arrow leaves for as long as 14 seconds. Press M: they loose together, a fraction of a second apart. Press Comma
   again and they fire at will. The log shows `[Marshal] word`, a `[Marshal] fire` summary every five seconds, and the checks `fire`, `word`,
   `volley-hold` and `volley-release`. The hold needs at least five archers holding for five seconds with no word; fewer judges nothing. The
   summary line ends with `idle=`, `aside=` and `shotsAfterOther=`, so that the share of the company that holds can be read: `idle` is the soldiers
   of the company in neither draw state who do not stand aside, and `aside` the ones a rule stands aside (no ranged weapon wielded, a
   hold-fire order, detached, using an object, a target within 7 m, or the mode free). The `holding=` of the hold check is the smallest count of its
   window, and a window opens at the first census of five, so on a count that climbs it reads near five whatever the company does after.
   `shotsAfterWord` counts only the shots after a release the word caused: a hold that ran out at 14 seconds counts under `shotsAfterOther`.
   **The stage rests on a belief only the game proves:** that setting the attack bit each tick keeps a bow drawn with no shot, and that one tick
   of cleared bits looses it. If `volley-hold` or `volley-release` reads FAIL, stop and record it in the contract's Deviations.
7. Mark: select a company, open the panel, hold Left Ctrl and order a charge on an enemy company. The company does not move, keeps its
   order and takes the enemy as its target; the log shows `mark` and, after three seconds, `[Marshal] mark company= target= onTarget= of=` once
   a second. The `mark-effect` check judges a ranged company on Stop: at least half of the soldiers with a target aim at the marked company. A mark on
   a melee company keeps its order and is harmless, and it writes its `mark` line, but its soldiers in hand to hand choose their own foe, so the
   check does not judge it. With Left Shift also held the mark waits its turn in the queue.
8. Outlines: with the panel open the soldiers of the selected companies wear a gold outline and a marked enemy company a red one; closing the
   panel, or turning Outlines off, clears them at once. Holding the show-indicators key (Left Alt) shows them with the panel closed.
9. The field trial's Marshal steps run as subsets, each in its own battle, and a builder never launches them. A battle runs out: the enemy
   reaches the line about 75 to 90 seconds in, and a step that follows a long walk finds its archers gone (the second run put every step in one
   battle, and `volley_holds` then found no archers). The subsets, one battle each:
   - `python tools/run_command_trial.py --steps queue_two_moves,direct_order_clears,outline_clears`
   - `python tools/run_command_trial.py --steps array_keeps_offsets,arrive_together`
   - `python tools/run_command_trial.py --steps volley_holds,volley_looses`
   - `python tools/run_command_trial.py --steps mark_keeps_order`
   - `python tools/run_command_trial.py --steps volley_massed`
   - `python tools/run_command_trial.py --steps hold_line_proof`
   - `python tools/run_command_trial.py --steps hold_line_slot`
   - `python tools/run_command_trial.py --steps hold_line_strike`
   - `python tools/run_command_trial.py --steps hold_line_flag`
   - `python tools/run_command_trial.py --steps square_side_kept`
   - `python tools/run_command_trial.py --steps spacing_one_rider`

   A run with no `--steps` arms every step of the Seat and of Marshal's Word in one battle. Prove each stage by its subset; the Seat's rows ran in one battle once (`20261105-003513`), and Marshal's steps do not all fit it.
   Each step waits for a battle and a live session, writes real orders through the player's controller after the in-mission-tick marker, and
   ends in a `[SeatTrial] step=<name> result=PASS|FAIL` line with the facts it judged. A direct order shuts the order panel (the panel's own listener,
   `MissionOrderTroopControllerVM.cs:827`), so a step that holds a key after one opens the panel again first, and `array_keeps_offsets` and
   `arrive_together` wait for the companies to stand before they read or plan. An open panel asks for a quarter of the mission's speed when the
   player has slow time on order (the engine's default), and the steps count real seconds, so `queue_two_moves` and the mark step shut the
   panel before they wait on the world. `array_keeps_offsets` drags along the group's own front moved 20 m sideways and passes only when each
   order point moved by that shift (within 3 m) and the two companies stayed as far apart as they were; its facts name `planned=` and `miss=`.
   - **`arrive_together`** first stands its second company 45 m behind the first and 20 m to its right (`MarshalTrialRules.ColumnBehindMetres`
     and `ColumnSideMetres`), whatever the two stood like, and waits for both to stand. Its plan is a quarter turn of the group, and a turn
     about a point makes two companies' walks differ by at most 1.41 times the distance between them. The proof needs the far walk at least twice
     the near one and between 17 and 30 s at the slower company's speed, so two companies that stand close have no plan at all: run
     20261104-184832 left a stagger of 25 m and read `no-plan speed=3.5`, and a column 25 m apart or less has none at any speed the tests
     sweep, while the staged column has one at 3 to 4.5 m/s. The proof's limits are unchanged. The step fails on a `no-test` skip, because a walk
     that proves nothing is not a pass, and plans no walk of more than 30 seconds for its far company, because a longer one takes the group into
     the enemy before the march ends (it then fails `reason=no-plan`, with `apart=` beside `speed=`).
   - **`volley_holds`** walks the archers forward first: it picks the enemy company nearest them and orders a Move that leaves about 80 m between the two
     once both have walked (`MarshalTrialRules.ApproachWalk` counts the enemy's own walk), and waits for the archers to stand. A company that stands behind
     the infantry has no line of fire, and the first run of this step read 40 idle archers for 55 seconds and `holding=6` at the most when the enemy came.
     It then runs a control with the mode free: it counts every shot of the company for 10 mission seconds and the soldiers who loosed them
     (`free_shots=`, `free_shooters=`; the table of distinct shooters holds 128, so a larger company reads its first 128). Then it presses key 543 and waits
     for the hold verdict. Its line names `gap=` (the metres to that enemy company at the control), `held=` (the window's smallest count), `most_holding=`,
     `peak_idle=` and `peak_aside=`. The control decides what the numbers mean: a free company that shoots with 12 soldiers cannot be asked to hold with
     30, and `most_holding` against `free_shooters` is the share of the shooters that held. Massed fire rests on those numbers.
   - **`mark_keeps_order`** takes the archers on a Stop; it waits until the enemy company with the most units is within 70 m, so that the sampled soldiers
     have targets, marks it, shuts the panel and waits up to 15 seconds for the `mark-effect` verdict. It passes only on a verdict that passed, with
     the order kept; a read with fewer than three soldiers that have a target fails with `reason=too-few-targets`, and a verdict that failed fails
     the step with `effect=fail`. The step for a melee company on a Move is gone (Deviations row 19).
   - **Runs so far** (2026-11-04, folders under `E:\Temp\command-trial-runs`). The first run passed five steps and failed three (Deviations row 15).
     `20261104-162919`, every step the table then held, in one battle, passed `queue_two_moves`, `direct_order_clears`, `array_keeps_offsets`, `mark_keeps_order` and
     `outline_clears`, and failed `arrive_together`, `volley_holds` and `volley_looses` for the battle running out (row 16). `20261104-184719`
     (`queue_two_moves`, `direct_order_clears`, `outline_clears`) passed all three. `20261104-184832` passed `array_keeps_offsets` (`stagger=25.0 shift=1.5
     planned=20.0 miss=0.0`) and failed `arrive_together` with `no-plan speed=3.5`. `20261104-184948` failed `volley_holds` (`no-window units=40 most_holding=6`, 40
     idle for about 55 s, then at most 6 holding with up to 35 idle) and `volley_looses` (`holding=0`, the step before it had judged nothing). `20261104-185250`
     (`mark_keeps_order`) passed with `onTarget=20 of=20` at 69.7 m. `20261104-185456` (`mark_melee_on_move`) failed with `onTarget=1 of=20` at 29.7 m. Rows 17 to 19
     record the corrections. On the corrected build (19:40 to 19:48, one battle per subset, every run exit 0 with `checks_fail=0 swallowed=0`): `20261104-194038` passed `queue_two_moves` (`arrival=2`),
     `direct_order_clears` and `outline_clears`; `20261104-194149` passed `array_keeps_offsets` (`stagger=25.0 shift=1.4 planned=20.0 miss=0.0`) and `arrive_together` (`near=31.6 far=77.7 seconds=25.3`);
     `20261104-194356` passed `volley_holds` (`gap=80.3 free_shots=45 free_shooters=21 held=8 most_holding=35 shots=0`) and `volley_looses` (`loosed=36 of=37`); `20261104-194538` passed
     `mark_keeps_order` (`context=RangedOnStop onTarget=19 of=20`). So the hold's stop rule clears on most of a company in range, and Massed fire may be built.
   - **The second runs of the M5 slices** (22:52 to 22:57, every run exit 0 with the feature's checks passing; Deviations row 23). `20261104-225226` (`hold_line_proof`): the feature's own `hold-line` read
     `flagOff=20 blows=0 control=3 controlBlows=28 seconds=10.2` and passed (the driver's last line read `blows=0 controlBlows=40` over 112.8 s while the held company fell from 20 to 14), so the flag stops an AI soldier's
     blows and the stop rule of 9.2 clears; the step failed `control-quiet` because its own window opened after the control's fight ended. The step now rests on the driver's check. `20261104-225648` (`spacing_one_rider`) passed
     with `reads=Infantry riders=1 units=41 any=1 significant=0` and `repair=applied`, so the spacing getters were not inlined. `20261104-221200` (`square_side_kept`) passed, with its open point corrected in the step. `20261104-221022`
     (Massed) gave the word at 28 of 39 holding. `20261104-225512` (`volley_massed`) failed `not-settled first=0` after 46 s with no `[Marshal]` line before it: the log holds no position, the passing runs of the same ground settled about
     31 s after the deployment against a flat limit of 45 s, and the limit now follows the walk and the failure names the walk, the speed, the distance and the order. The cause is not proved. Nothing from the full rule of this round
     (the firing order, the slot clamp, the strike back, the panel) has run in the game.
   - **First runs of Massed fire and the M5 slices** (2026-11-04, 22:09 to 22:13, one battle per subset, every run exit 0 with `swallowed=0`; Deviations rows 21 and 22).
     `20261104-221200` (`square_side_kept`) read `type=SquareFormation chosen=8.4 side0=7.6 side1=7.6 unit=0.8 units0=40 units1=39 placed=39` and passed. The side it read is one unit
     width from the side it asked for, and the line has no side from before the drag, so that pass does not show the drag moved the side. The other three failed for causes under the step.
     `20261104-221022` (`volley_massed`) read `reason=no-word units=40 gap=83.8 free_shots=37 free_shooters=19 peak_holding=27` while the log held `[Marshal] word company=1 time=50.5 holding=28`
     and `[Marshal] massed company=1 holding=28 eligible=39` in the same second. The feature was wrong: the driver counted the word when it was given and set its numbers only when the release
     window was judged, 1.3 mission seconds or more later, so the step found a count and no word. The wait was right: it starts at the second press, runs 60 s, and the word came about 2 s
     after it. The numbers are now set with the word. `20261104-220929` (`hold_line_proof`, `not-controllable held=1 control=0`) and `20261104-221252` (`spacing_one_rider`,
     `rider-not-counted any=0 significant=0 riders=0 units=40`) had one cause: the host moved soldiers with `Formation.TransferUnits`, the AI's split, which returns at once for a company the player
     owns (`Formation.cs:2157`), so the control company stayed empty and the infantry company stayed at 40. The host now gives the player's own Transfer order (`OrderController.cs:942`), and both
     steps prove their ground first: `no-split` or `not-controllable`, and `rider-not-moved`, `no-rider-in-company` or `rider-not-counted`. These fixes are untried in the game.
   - **The runs of 2026-11-05** (00:21 to 00:33, a build stamped `6b02b31e2.dirty`, one battle per subset; Deviations rows 27 and 28). All six steps read PASS. `20261105-002122` (`hold_line_proof`): `driver=pass
     flag_off=14 held_blows=0 control_blows=26 peak_near=103 units=20`. `20261105-002348` (`volley_massed`): `massed_holding=31 eligible=39 stale=0 loosed=31 of=31`. `20261105-002536` (`square_side_kept`):
     `before=5.3 chosen=8.4 side0=7.6 side1=7.6 unit=0.8 placed=39`. `20261105-002627` (`hold_line_slot`): `far=0 max=1.4 mean=0.3 wounded=1 samples_pass=3`, with the clamp counter at 0 for the session.
     `20261105-002843` (`hold_line_strike`): `wounded=7 settled=6 flag_on=6 blows_after=1 driver_strike=1`, clamp counter 352. `20261105-003126` (`hold_line_flag`): `flag_off_before=39 flag_off_after=0 restored_in=0.3`.
     **Open:** in the `hold_line_proof` battle the check `hold-slot` failed (`qualified=18 far=7`, `max=3.1`, clamp counter 365 for the session), and the trial exited 1 for it. The cause is not shown (Deviations row 28).
     Section 9.4 stays stopped (row 25).
   - **The runs of 2026-11-05 with the clamp's measure** (01:09 to 01:17, the same build; Deviations rows 30 and 31). Every run below is in the run history table that follows. The three newest read the slot line's
     `closer=` and `farther=`: 22 steered reads ended nearer the slot against 5 farther (010922: 1 and 0, then 15 and 5; 011208: 1 and 0 in the step's line; 011444: 5 and 0), so the sign of the clamp is right. The same runs show why distance
     from the anchor does not test it: the first window of 010922 read `far=7 max=2.6 wounded=0` with one counted steered read, so seven soldiers stood far with the clamp counted once. The rule was changed to judge the trend (row 31),
     and the steps changed after these runs (rows 29 to 31) have not run in the game.

     | Folder under `E:\Temp\command-trial-runs` | Step | Result | The numbers |
     |---|---|---|---|
     | `20261105-002122` | `hold_line_proof` | PASS | `driver=pass flag_off=14 held_blows=0 control_blows=26`; the trial exited 1 for a `hold-slot` fail (`far=7`) |
     | `20261105-002348` | `volley_massed` | PASS | `massed_holding=31 eligible=39 loosed=31 of=31` |
     | `20261105-002536` | `square_side_kept` | PASS | `before=5.3 chosen=8.4 side0=7.6 side1=7.6 placed=39` |
     | `20261105-002627` | `hold_line_slot` | PASS | `far=0 max=1.4 wounded=1 samples_pass=3`, clamp counter 0 |
     | `20261105-002843` | `hold_line_strike` | PASS | `wounded=7 settled=6 flag_on=6 blows_after=1`, clamp counter 352 |
     | `20261105-003126` | `hold_line_flag` | PASS | `flag_off_before=39 flag_off_after=0 restored_in=0.3` |
     | `20261105-010922` | `hold_line_proof` | FAIL | `reason=control-quiet` (`control_blows=9` under the minimum of 10, `hold-line` skip `no-control`); `hold-slot` fail at `far=9 max=4.9 closer=15 farther=5`; first window `far=7 closer=1 farther=0 wounded=0`; clamp counter 525 |
     | `20261105-011208` | `hold_line_slot` | PASS | `far=3 max=3.1 clamps=5 closer=1 farther=0`, 3 samples about a second apart, no `hold-slot` line in the battle |
     | `20261105-011444` | `hold_line_strike` | FAIL | `reason=no-blow-after-wound` (`wounded=3 settled=1 flag_on=1 blows_after=0 driver_strike=1`; the one settled soldier was gone by the next slot line, `units=39 wounded=0`); window `closer=5 farther=0`; clamp counter 72 |

10. Massed fire: put a company of archers on Massed fire (press Comma twice) with an enemy in range. The bows are drawn and held; once most of the archers who can
    still shoot hold, the word is given by itself and the volley follows a fraction of a second apart. The log shows `[Marshal] massed company= holding= eligible= stale=` and the
    checks `word`, `volley-release` and `massed`. A third press returns the company to fire at will. The trial's `volley_massed` walks the archers to about 80 m, counts a free-fire control
    and presses Comma twice; its line names `free_shooters=`, `massed_holding=`, `eligible=`, `stale=` and `loosed=`, and it fails `reason=no-word` with the peak census when
    the share is never reached, or `reason=word-not-recorded` when a word was counted and its numbers were not.
11. Hold the line: select a company of shielded foot soldiers, give it the shield wall and Stop, press Period and let it meet the enemy. Its soldiers stop attacking and stay in their places; a soldier
    wounded by a blow answers for four seconds and is then disarmed again; press Period again, or give a charge, and every soldier fights. The log shows
    `[Marshal] hold company= units= flagOff= blows= control= controlBlows= seconds=` and `[Marshal] slot company= ... order= ... why=` every 5 seconds and the checks `hold-line`, `hold-slot` and `hold-strike`
    (Pass, Fail, or a skip or no line while the ground is not there: `no-control`, `flag-not-off`, no wound yet). **The stage rests on a belief only the game proves**, and a Fail stops it (build-spec 9.2): the flag
    stops an AI that already holds a target (the first run says it does: `blows=0` against `controlBlows=40`), and clearing it mid swing is safe (`docs/features/spider.md:202` records a native CTD on a nearby
    path, a Monster's `CanAttack` flag, which is not this one). The trial has four steps, each its own subset and each ending by lifting its hold:
    - **`hold_line_proof`** splits the largest company of foot soldiers in two with the player's own Transfer order, puts both in the shield wall under Stop, holds one and waits for the driver's judged `hold-line`
      verdict for this mission (150 real seconds at most). A control that dealt too few blows (`no-control`) is not a verdict: the driver takes a new baseline, the window stays open and the step keeps waiting through it. It does not send for the enemy: a charge ends the hold. Its line carries `driver=` and the driver's numbers, `peak_near=` (the most enemy soldiers it saw within 15 m
      of the held company), `restored=`, and `closer=` and `farther=`, the clamp's totals for the whole hold, read before the lift drops them. It fails `no-split` (with `held_units=` and `control_units=`) or `not-controllable` before anything else, `not-qualified` (with `why=`, `arrangement=` and `order=`) when the line never
      qualifies, `enemy-not-in-reach` when fewer than 10 enemy soldiers came within 15 m, `control-quiet`, `flag-not-off` and `held-company-struck` from the driver's verdict, `driver-never-judged` when it gave none, and
      `flag-not-restored` when the soldiers' flags are not back within 5 seconds of the lift. The flag is read from the soldiers (`GetAgentFlags`).
    - **`hold_line_slot`** holds one company under Stop and waits for the enemy at the line, a wound and the clamp's first replaced input (counted from the step's own start). It then takes a sample at each hold read, every third of a second, that
      has a wound and a qualified line, and ends at 45 samples (15 seconds of reads after the first steer, so the driver's five second windows have judged them), or when the driver's `hold-slot` check fails, or after 150 real seconds. It prints
      `order=`, `anchored=`, `far=`, `max=`, `mean=`, `wounded=`, `samples=`, `clamps=`, `steered=`, `closer=`, `farther=` and `slot_failed=`. It fails `order-not-stop`, `no-anchor`, `nobody-struck` (a line nobody hurt proves nothing),
      `steered-away` (the driver's slot check failed, or the totals of the whole hold ended more steered soldiers farther from their slot than nearer), `no-clamp` (a line in place proves nothing about a clamp that never acted),
      `too-few-samples`, or `too-few-steers` (the clamp acted, but fewer than five steered reads were counted). The old reason `left-the-slot` is gone: the far count is a fact and no longer a test (Deviations row 31).
    - **`hold_line_strike`** first waits for the line to be disarmed (`flag_off_before=`, read from the soldiers' own flags), then for the first wound and a settled one, and passes when the wounded soldiers' flag reads on (`flag_on=`),
      the company's strike blows rise (`blows_after=`) and the driver's `hold-strike` passed. It keeps waiting past the strike back window until the company's blows rise or 150 real seconds pass, because the first soldiers wounded can die before they land one. It fails `flag-not-off` (a build that never clears the flag), `nobody-wounded`, `strike-window-missed`, `flag-stayed-off` or `no-blow-after-wound`.
    - **`hold_line_flag`** holds one company, then orders a charge, and passes when every soldier's flag reads on within `HoldRestoreSeconds` (1.5 real seconds; it prints `restored_in=`), the order reads Charge and the toggle stays on;
      it fails `flag-not-off` before the charge when nothing was cleared, and `flag-not-restored`.
12. Hollow square: give a pure infantry company the Square order and drag a line across its front. It forms the plain square (the log shows `[Marshal] square ... placed=<units>` and the check
    `square` passes) with the side you dragged, and the side stays when a soldier falls. Test with trolls too, because Patch92 changes `UnitDiameter`. A company of foot and ranged soldiers
    mixed ignores the side (`laidOut=1`), as vanilla's schiltron does today, and the manifest's `square` row passes only on `laidOut=0`. The trial's `square_side_kept` chooses a company whose every soldier is infantry (it reads the soldiers, not the formation's class, which calls an even mix of melee and ranged a melee company), prints `laid_out=`, and reads the side the square formed with (`before=`), asks for four unit widths over it (under it near the maximum), removes one soldier and fails with
    `not-the-plain-square`, `laid-out` (the mixed layout places the soldiers, so the side proves nothing), `drag-did-nothing` (the side after the drag is within one unit's width of the side before), `side-not-reached`, `nobody-removed`, `side-not-kept` or `unplaced`.
13. Spacing repair: a company of foot soldiers with one rider, with Spacing Repair on (restart after changing it) and no co-op. The log shows one `[Marshal] spacing` line for the first company
    with a rider but not a significant number of them, and the check `spacing` passes when its four limits read as a foot company's. If the check reads Cavalry on a session with
    `repair=applied`, a getter was inlined and the patch never ran (build-spec 9.3). The trial's `spacing_one_rider` moves one rider into the largest company of foot soldiers with the player's own Transfer order and fails
    `reason=rider-not-moved`, `no-rider-in-company` or `rider-not-counted` before it reads the limits, and
    `reason=repair-not-applied why=<setting-off|repair-off|coop|dedicated-server|not-prepared|prepare-failed>` when the transpilers were not applied: they are applied when the first mission begins, from the saved settings,
    so a run on a machine whose saved Marshal's Word or Spacing Repair setting is off cannot be fixed from the trial.

14. The panel (stage M6): open the order panel in a field battle. One more set, Marshal's Word, shows Archers' fire, Give the word and Hold the line, each with its icon; a click does what its key does for the
    companies that were selected (a toggled panel closes after a click, so the click names its companies). With the legacy layout the set takes F8 and F9 still closes the panel; with the default layout the set is the tenth
    and has no key. The set's own button is drawn with the base game's toggle icon (`MarshalOrderSet.IconId` answers `order_type_toggle`, the id of the toggle set of `DefaultVisualOrderProvider.cs:58`, which keeps the string id
    `taom_order_type_marshal`): `GenericVisualOrderSet.IconId` would answer the string id (GenericVisualOrderSet.cs:20-36), `OrderSetVM.cs:180` copies it to the icon widget, and a brush layer the brush lacks draws nothing.
    A town, an arena, a hideout and a Host Review mission show the base game's panel. Nothing of M6 has run in the game; the manifest's `[[human]]` rows are its checks.

## Co-op: the four questions

Marshal's Word is off whenever a network session is active (the engine's flag or a live co-op session, `SeatMissionClassifier.InNetworkSession`), so the answers are short. The archers' component, the shot table and the outline
paint are created only by a live session, so they follow the same answers. **Presence** (`IsCoopActive`): the patches are applied
for every player. Those that read the session return at their first test unless it is live, which it never is in a session, and the two square-width prefixes test for a plain `SquareFormation`, which only the live arrangement prefix builds. **Session and role:** no
session means the local machine owns the whole simulation; a session stops the feature before it begins. **Dedicated server:** no screen, so no
view and no session; the patches other than the spacing repair are applied, and those that read the session see `IsLive` false and the two width prefixes find no plain square. **Whose party:** the player's own team in a single-player mission.
`MarshalView` is a `Local` row in `CoopMissionRoles`. The six settings are listed as non-simulation in `CoopSettingsRelevance`.

**The spacing repair answers the four questions for itself, because its patch is applied for every player and is not behind the session.** *Presence* (`IsCoopActive`): it decides the
repair: with a co-op module present `MarshalSpacingPatch.Prepare` answers false and the seven methods stay vanilla, so a host and a client never compute different widths. *Session and
role:* no session needs to be consulted, because presence is a process-lifetime fact (`docs/features/coop-interop.md`), which is what a patch-time decision may read.
*Dedicated server:* `IDedicatedServerProvider.IsDedicatedServer` keeps it off there too. *Whose party:* it applies to every team in a single-player mission, because it is a base-game
consistency fix, and so it reads no `IsLive`. The setting is therefore not simulation-relevant, and `MarshalSpacingPatch.cs` is on the presence-read gate's allowlist with that reason.

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
- **Hold the line, what it does not do.** The line is a shield wall, circle or square of shielded foot soldiers; a Line company, an archer, a rider, a thrower with a thrown weapon in hand, a soldier on a siege engine
  or a howdah, and the hero are never disarmed. A mixed company (melee and ranged) is laid out by Patch30 with no regard for the arrangement, so its slots are the mixed layout's. In Host Review `Mission.DisableDying`
  skips the hit path (Agent.cs:5525-5527), so no soldier there records a wound and none strikes back. A Stop that follows a Move inside one third of a second keeps the old anchor, because the read cannot see the change.
  The clamp acts on an AI soldier's input only; a soldier the Seat's take hands to the player is released at the next sweep. A held company that the AI takes (the hero falls, or a delegate) loses the line at once.
- **The strike back's blows are the line's answer.** They are counted apart from the blows `hold-line` judges, and `hold-line` compares only the others. A soldier of a held company who is not in the line (no shield, or a mount or a ranged weapon) deals blows
  that count for nobody, and every soldier of a company that is not held counts, so the control is never undercounted.
- **The panel highlight lags a click.** A click only stores the press, and the panel refreshes its sets' active states in the click, in `MissionOrderVM.OnOrderExecuted` (MissionOrderVM.cs:460-469), before the mission tick applies it, so a panel that stays open shows the old state of Archers' fire and Hold the line until the selection changes or the panel opens again. It is cosmetic; the press itself acts at the next mission tick.
- **The panel is not a clickable card.** The set has three entries and no marked-target icon; nobody has checked a movie's room for one more set in all four pairs of the Order Type and Order Layout options, which the manifest's human rows ask.
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
- **Massed fire counts what the census counts.** `Eligible` is the holding, drawing and idle soldiers less stale draws, so a soldier whose AI never draws (no target in view, a friend in the line
  of fire) stays idle and counts against the share, as the contract asks; the stand-aside facts are read three times a second, and `DrawLimit` (6 s) is the contract's number, not a measured one.
  A company of 40 with all 40 eligible needs 28 holding (`ReadyShare` 0.7); 21 of 30 is the case the rule tests. The trial run `20261104-194356` held 35 of 40, so the share is reachable at 80 m.
- **The engine clears the attack flag too, and the toggle gives way to it.** `ClimbingMachineDetachment.cs:241-243, 266-268` clears `CanAttack | CanDefend` on a soldier in water whom it has just
  detached, and nothing in that file puts the bits back. A soldier whose flag the toggle had cleared, and whom that code then detaches with both bits off, is not armed again by the toggle
  (`MarshalHoldRules.FlagAction`, fourth fact `engineCleared`: detached and `CanDefend` off); a flag the engine cleared before the toggle is never written, and a flag the toggle cleared for any other reason comes back
  as before. A soldier with `CanDefend` already off for another reason, taken by a non-climbing detachment while the toggle holds his flag, would read as the engine's clear and stay disarmed; none is known.
  `MissionMainAgentController.cs:659, 672` read the flag too, on the main agent, which the toggle never touches.
- **The slot proof reads the clamp's trend, and a pushed soldier is not walked back.** `ClampInput` replaces an input that points away from the slot and leaves a zero input, and one that does not point away, alone, as the contract says
  (build-spec 9.2), so a soldier knocked or drawn past two metres stays there until the AI walks him toward the slot. Under Stop the engine's order position is the soldier's own place (Formation.cs:1403-1404), so nothing else pulls him back.
  Returning a soldier on a zero input is out of scope. `far=` and `max=` are facts for that reason, and the slot check judges what the clamp did: in the three newest runs the steered soldiers ended nearer 22 times and farther 5 times,
  and a line with no wounds read `far=7 wounded=0` in a fight of 5 blows to 1, so distance from the anchor is not a test of the clamp (Deviations row 31). `loose=` counts the reads beyond the slack that no replaced tick preceded.
- **A fault in the callback pays the line off.** The callback's catch latches the component off for the process. From then on `RefreshHold` and the driver act as hold off, so the mission tick gives back the flag and the firing order the
  line took, and the table keeps saying the company is held. A soldier whose restore throws in a fault cleanup is not retried (the cleanup tries each soldier on his own and rethrows the first failure after the rest, as the outline clear does).
- **Nothing removes the view in a live mission, and the finish pays no restore.** In TAOM's `Main`, `Client` and `Dependencies` and in the 346 cached engine types, `RemoveMissionBehavior` is reached by `Mission.OnMissionStateFinalize`
  (Mission.cs:2209-2224, every behaviour, at the mission's end) and by `DeploymentMissionController` removing itself (DeploymentMissionController.cs:80); another mod's call is not searched. The finish makes no native call by design (section 2), so a
  view removed early would leave the soldiers it disarmed as they were. No guard is added for a route with no producer.
- **The climbing machine's detach and clear.** `ClimbingMachineDetachment.AddAgentAtSlotIndex` detaches the soldier (cs:49-50) and `TickClimbingMachines` clears `CanAttack | CanDefend` on him (cs:241-244, 266-269). The toggle decides at the moment
  of the write, from the soldier being detached with `CanDefend` off. A detach that ends between two reads, when `Formation.AttachUnit` clears the detachment without putting the bits back, would read as the toggle's own debt and arm the soldier.
  The only constructor of `ClimbingMachineDetachment` found is naval (`NavalDLC.Missions.Objects.MissionShip.cs:2403`), and naval battles are outside this feature, so no state is added for the transition.
- **The callback's input frame and thread are UNVERIFIED.** `native_decompile.py --engine-method IMBAgent.SetHasOnAiInputSetCallback --callers 1` (2026-11-05) prints `IMBAgent_SetHasOnAiInputSetCallback` at 0x6E17C0, size 7:
  `*(undefined1 *)(param_1 + 0x91c) = param_2;` with no callers, since only the registration table reaches it. That shows where the flag lives. It does not show the reader that calls `OnAIInputSet`, the frame of `inputVector` or the thread, so
  `MarshalWordComponent`'s comment that the callback runs serially on the main thread, and the clamp's local frame, rest on the managed side (`Agent.cs:1660-1666` iterates the components) and on the runs (`hold-clamp` 525, 5 and 72).
- **Blows are the measure.** "Attacks" are melee blows that reached a soldier of another team or his shield (`OnAgentHit` is raised for a blocked blow too); a swing in the air is not counted,
  so a flag that stopped the landing and not the swing would pass. The control is the busiest company not on hold, chosen once; a cavalry company can be it, and the trial avoids that by
  splitting the held company's own kind.
- **The plain square has never run in a shipped mission.** The check `square` is its proof; `laidOut=1` marks a mixed company whose slots Patch30 answers, so the side has no effect. The sizes
  of a square in a dense battle (the middle slot, the corner soldiers' facing) are vanilla's class as it is.
- **The spacing repair needs a restart and is off under co-op.** It reads the saved settings when the first mission begins; the three readers that keep `HasAnyMountedUnit`
  (Formation.cs:3174, `OrderController.cs:1363`, `Mission.cs:3975`) still see a rider. The limits' readers in the cached engine call through `IFormation` (LineFormation.cs:152, 162; SquareFormation.cs:36, 40), which is not inlined; the `spacing` check reads the getter from TAOM's own call (`MarshalOrderAdapter.SpacingOf`), so it proves the getter from there and cannot see a vanilla call site.
- **A bow kept drawn is a belief.** The hold repeats the attack bit each tick because native copies the AI's stored result over the flags
  just before the callback (the contract's native read of v1.5.6 at `0x69FD10`); only the game shows that a bow stays drawn and no shot leaves, and what
  the abandon bit `0x4000` does beside the attack bit. The hold check reads FAIL when it does not. A second read of `0x69FD10` in the installed
  `TaleWorlds.Native.dll` (2026-11-04, `native_decompile.py --rva 0x69FD10 --callers 1`) lands inside `FUN_18069fa50`, a function of 0x3E6 bytes that returns a
  pair of floats and writes none of `|= 0x200`, `& 0xfffffc3f` or `0x4000`; its one caller is a large update function. So at that RVA the installed DLL holds
  another function, and this read neither confirms nor refutes the contract's statements about those bits. What the hold itself can do follows from the
  component: `Leave` writes nothing, and `Hold` and `Release` write only for a soldier who is already `Holding`, so the hold cannot keep a soldier `Idle`
  or stop one from drawing. A soldier who never draws is one the AI never aims for (no target in view, a line of friends in the way) or one who stands
  aside by rule, and the `aside=` count and the free-fire control of `volley_holds` separate the two.
- **A shot the hold limit looses is not the word's.** `shotsAfterWord` once counted every shot in the two seconds after any release, so the shots of a hold
  that ran out at 14 seconds (run 20261104-184948 read `shotsAfterWord=3 shotsBeforeFirstWord=3` with no word given) looked like an answer to a word, and
  the release check could pass on them. The component now records whether the word caused the release, and the other shots count under `shotsAfterOther`.
- **`held` is a window minimum, not a share.** The hold check's `holding=` and the `held=` of `volley_holds` are the smallest census count of the
  judged window, and the window opens at the first census of five. A run in which the count climbed one soldier at a time read `held=5` of 40 archers,
  and that was the judge's floor, not the share of the company that drew (nine of 40 had begun a draw; the rest had not, with no arrow outside a hold).
  `most_holding=` on the step line and `idle=` on the summary line say more; Massed fire will need the real share.
- **Thrown weapons.** The hold keeps any ranged weapon drawn, thrown weapons included (`IsRangedWeapon`), and key 543 does not filter the company.
  A thrower company held for up to 14 seconds is unproved; a human row looks at it.
- **Shots late by a quarter second can count.** The volley checks are judged on a pass four times a second, so a shot up to 0.25 s after the
  release window can still be counted in it.
- **Standing aside near the enemy is the AI's.** An archer with a target within 7 m, or one that left the formation, uses a bow as the AI
  would. The 7 m figure and the 14 s hold limit are the contract's numbers, not measured ones.
- **A mark given by a click on a panel opened by toggle shuts the panel.** The panel's own handler closes a toggled panel after a charge on a
  focused enemy company and after an order hotkey, captured or not (`GauntletOrderUIHandler.cs:340-343` and `:470-479`), as in the base game; only a
  panel opened by hold stays open, and a captured ground click leaves it open. So a mark given by click on a toggled panel applies, the panel
  shuts, the held keys disarm, and the red outline shows only with Left Alt or when the panel is opened again. The trial gives its orders through
  the controller and not through the handler, so only the human row can confirm this.
- **A queued mark writes no `mark` line,** and one that fails writes `dropped reason=none-selected`; only an immediate mark writes `mark` pass or
  `not-applied`. The manifest reads the immediate line alone, so this cannot give a false verdict.
- **A cavalry company with a running charge cycle.** A mark calls `Formation.SetTargetFormation`, which Patch31b postfixes: for a company
  whose cycle is running it re-points the cycle at the marked company, so the player's mark wins; for any other company it does nothing.
- **A mark holds only until the next movement order, by any route** (`SetMovementOrder` ends with `SetTargetFormation(null)`,
  Formation.cs:734): `OrderController.TryCancelStopOrder` for a company on Stop (called for `LookAtEnemy`, every arrangement and the form
  orders), `BannerBearerLogic:157`, `Formation.Tick`'s substitute loop and `Formation.OnDeploymentFinished`. A fire order does not clear it.
  In a queue a mark placed before a movement step is lost when that step starts, and one placed after it stays. The engine clears a dead
  target on its own.
- **The mark's effect is unread in vanilla.** Vanilla sets a target company only with a charge or an advance; how the AI aims at one on a Stop
  or a Move is unread, so the `mark-effect` check can stop the stage. A company's mark line is written for any company that holds a mark, but a
  company on Charge, Advance or ChargeToTarget is skipped before it is read, and only a ranged company on Stop is judged.
- **A mark steers ranged soldiers; melee soldiers choose their own foe.** A melee company on a Move, marked at 29.7 m, had `onTarget=1 of=20` (run
  20261104-185456), and an archer company on Stop, marked at 69.7 m, had `onTarget=20 of=20` (run 20261104-185250). The mark on a melee company stays
  harmless and allowed: it keeps the company's order, writes its `mark` line, and aims no one. The stop rule of the contract's 8.1 ends the melee half here,
  and the key text and the Outlines hint say so.
- **The trial does not force the saved settings.** A run reads the plain provider, so a saved Outlines or Keep the Array that is off fails its step
  with facts that never name the setting. It cannot give a false pass, and forcing the values would take a settings class of its own.
- **One battle reaches the mark-effect row.** Its one pass pattern is the ranged case, which `mark_keeps_order` reaches, and a failed `mark-effect` line fails
  the wrapper's run.
- **A free company's shots are counted by a callback whose thread is native's.** `ShotWatch` adds with `Interlocked` and no lock, and it keeps the first 128
  distinct shooters, so the control of `volley_holds` reads `free_shooters=128` at most.
- **A throw in the release stops the rest of it.** `MarshalFireAdapter.ReleaseAll` turns each touched soldier off in turn and has no try per soldier, so a
  throw leaves the soldiers after it with their callback on until the mission ends; the outline clear tries each unit. Nothing is known to throw there (the
  soldier's slot is tested first), so a try per soldier would be code for a guess.
- **The trial's limits.** A step fails when the one thing it reads is broken, and four things it cannot read are named here. `outline_clears` reads
  the service's dictionary, so a build that records outlines and never paints them passes; the colour a person sees is on the human row.
  `volley_holds` proves the stop rule on the soldiers that held (at least five), not on the company; its control counts a free company's shots in one
  battle at one spot. `array_keeps_offsets` reads the order points
  and the standing places, not the lines of the preview. The mark step proves what the AI aimed at in one battle, with the enemy the step
  chose, and says nothing about a different enemy or a different weapon. The archers are found by the cached ranged flag
  (`IsRangedFormationReadOnly`), which the engine's own tick fills, so a company that has not been ticked yet reads as no archers.
- **Two outline writers.** An agent that carries a race ability glow (`RaceAbilityVisuals`) can be repainted by either writer. The race
  writer repaints every pass while it has a colour for the agent, so it wins while it has one; when it clears the agent, it clears this
  outline too. A new banner bearer's equipment rebuild replaces the mesh the outline was painted on, and this writer repaints only on a
  change.
- **Outlines only change.** The service paints a unit when its colour is new or changed and does not repaint, so an outline lost to an
  equipment rebuild stays lost until the unit leaves the wanted set and returns.
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
- **Interference read for stages M3 and M4** (build-time, from the source and from metadata scans, nothing run):
  - `Main/Features/RaceAbilities/` writes contours in `RaceAbilityVisuals.Paint` (lines 120-127) and nowhere else in the feature; this
    feature's writer is the separate `MarshalOutlineAdapter`, because `RaceAbilitiesWiringTests` pins one write site per file. The two
    can repaint one agent, accepted above.
  - Patch31b postfixes `Formation.SetTargetFormation` and `Main/Adapters/CavalryCommandAdapter.cs:53` calls it for the smart cavalry's own
    charge; the postfix returns for an AI-controlled formation, an Idle cycle and a null target. A mark on a company with a running cycle
    re-points the cycle (see above). Patch31 and Patch35 postfix `SetMovementOrder` and a mark does not call it.
  - `Main/Features/CombatMechanics/` has no `OnAIInputSet`, no movement-flag write and no `CanAttack`; the only other writers of the flags this
    component writes are none: a grep of `Main` and `Client` for `OnAIInputSet`, `SetHasOnAiInputSetCallback` and `HasOnAiInputSetCallback` finds
    only this feature's component and adapter (the same grep finds `BehaviorTreeAgentComponent`'s class line when asked for `: AgentComponent`).
  - Other `AgentComponent` overrides of `OnAIInputSet`: none in the installed Native, SandBox, SandBoxCore, StoryMode, CustomBattle,
    BirthAndDeath, Multiplayer and NavalDLC assemblies (a scan of 82 assemblies by method name, whose known hits were `Agent.OnAIInputSet` and
    `AgentComponent.OnAIInputSet`). Callers of `Agent.SetHasOnAiInputSetCallback` in the same assemblies: none (an IL scan whose known hits were the wrapper's calls
    into `IMBAgent`). The scan covers those assemblies and this repository, not other mods.
  - `Main/Features/CultureDoctrine/Hooks/Tactics/VolleyControl.cs` drives AI formations only and gives fire at will once on a player takeover
    (lines 30-41); this feature gives the company fire at will itself on a change to on-word and sets a company that went to the AI to free,
    so a hold-fire order of the AI's volley control makes the archers stand aside and nothing writes a firing order per agent.
  - `Client/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs` only reads `PlayerOrderController.SelectedFormations`
    and registers a mouse-only layer; none of the three new default keys is read there.
  - `Client/Features/SeatOfSeeing/`: the Seat's panel keep-open and cursor use `Mission.IsOrderMenuOpen`, the fact this feature reads too, and
    the Seat's fast key is game key 24, which Left Shift (key 540) also is, so holding the queue key in the Seat flies faster (accepted
    in the contract, section 3). Comma, M and Left Ctrl are not Seat keys.

- **Interference read for stage M5** (build-time, from the source and the registry, nothing run):
  - `ArrangementOrder.GetArrangement`, `Formation.ReapplyFormOrder`, `Formation.CalculateDesiredWidth`, the four limit getters: no other TAOM patch (searched
    `patch-targets.md` and `harmony-patch-registry.md` for each name; the known hit was this feature's own rows). The three public `GetUnitPositionWithIndexAccordingToNewOrder`
    overloads carry Patch92's prefix and finalizer (a thread-static scope for a team-less copy); a transpiler changes the body and Harmony wraps it, so the two do not meet, and the
    repair agrees with Patch92's own rule (a tenth of the company, vanilla's share for horses).
  - Every TAOM patch on `Formation`: `SetMovementOrder` (Patch31, Patch35), `SetTargetFormation` (Patch31b), `GetOrderPositionOfUnit` (Patch30), `UnitDiameter` and the layout scope
    (Patch92). None is on `ArrangementOrder` or `FormOrder`. Patch30 answers any laid-out formation's slot in a field battle and reads no arrangement, so a mixed company in a square keeps
    its mixed layout (named above). A hold changes no movement order, so Patch31 and Patch35 do not run for it.
  - Every writer of `AgentFlag.CanAttack` and its readers are listed under "Hold the line is a slice" and in Deviations row 20. `HumanAIComponent.cs:229` (pick-up of items: an alarmed
    soldier whose flag is off picks nothing up), `Mission.cs:4897` and `6291` (a search for a target near the main agent, and a count of agents that can attack, both skip a soldier
    whose flag is off) and `AttackInformation.cs:236` (a default for a victim's flags) are the readers the contract lists and nobody traced the native ones.
  - `Main/Features/CombatMechanics/` has no `CanAttack`, `OnAIInputSet` or arrangement patch (grepped). `Client/Features/SeatOfSeeing/`: S4 hands the hero to the AI and S5 takes a
    soldier who then stops being AI controlled. The fire adapter never touches `Mission.MainAgent`, and a soldier who is no longer AI controlled is not touched in the next sweep, so the
    adapter releases him and puts his flag back (`TurnOff` restores a flag this feature cleared, before the callback goes off).
  - `PatchShield` strips TAOM patches on a missing API and is off with a co-op module active. The three overloads are on `ExcludedTargetMethods` already; the four getters and the three new
    targets are shielded (nothing measured their cost), so a missing-API exception on one of them would strip that patch for the rest of the process.

## Performance

A frame with no live session reads no key. A live frame reads the three held keys only with the panel open and the three pressed keys and the show-indicators key always, copies three settings and, with the panel open,
rebuilds the flags at most four times a second while any step is queued (an empty queue draws once and stops). The capture prefixes cost a
field read each. The array prefix runs at 30 Hz while a drag is drawn with the mouse held (OrderTroopPlacer.cs:300, 799), with an
`OutcomeCounter` increment and no string; the queue driver returns at once while no company holds a step. The pace postfix is one volatile read when no march runs. With no company on a mode, the
archers' driver returns before any walk and no component exists; with one, a census walk costs one pass over its soldiers once a second, and
each callback is one native-to-managed trip per flagged soldier, with two counters and no line. The shot callback returns at its first test
with no session. The outline service returns at once while nothing should show and nothing is painted, and otherwise runs four times a second
over the selected companies' soldiers. The mark watch runs once a second over the companies that hold a mark and skips a company on Charge, Advance or
ChargeToTarget before it walks the company, so a battle of charges pays nothing for it; a marked company costs a walk of its soldiers, up to 20 native
reads and one log line a second. Hold the line costs nothing for a company that is not held. For a held company the hold pass runs every third of a second over its touched soldiers and each soldier reads
his qualifying facts (about ten managed reads, one wielded-weapon read and, under a Move, one `GetOrderPositionOfUnit` call that runs Patch30) and one native `GetFiringOrder` read, plus `GetAgentFlags` for the
census; the callback adds one array read per call and, only for a soldier beyond his slack whose input points away, one `Agent.Frame` read. The panel provider builds its list each time the panel opens (MissionOrderVM.cs:498-505, called at :715), not per frame.

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

- 2026-11-04: stages M3 (without Massed) and M4 built from the contract, and the field trial steps for M1 to M4.
- 2026-11-04: first game run of the trial: the steps that held a key after a direct order, and the array step that read an offset while a company
  was still walking, are corrected; a fault in the screen tick or the capture no longer writes a contour; a mount is cleared as a unit of its
  own; the archers' skip lines carry their company and the finish summary reads the table (Deviations row 15).
- 2026-11-04: review of stages M3 and M4 applied: a fault turns the archers' callbacks off in the next mission tick; a failed contour clear no
  longer stops the rest; a word counts its holders at the word, and a second word keeps the first one's release window; the mark steps judge
  their own case and a new `mark_melee_on_move` step covers the melee case; `array_keeps_offsets` needs a real translation; the judge reads
  a skipped Marshal patch class and a failed key registration; the fire text names the 14 seconds (Deviations row 16).
- 2026-11-04: the second wave of trial runs (18:44 to 18:57) and the second review applied: `arrive_together` stages its column before it plans
  (row 17); `volley_holds` walks the archers forward, runs a free-fire control and names the idle and the aside soldiers, a shot after a release the
  hold limit caused is no longer the word's (row 18); the melee half of the mark proof is gone, a company holding a vanilla target is not sampled, and the
  key text and the Outlines hint say a mark steers ranged soldiers (row 19); a fault owes the soldiers one cleanup field and its throw paths are tested.

- 2026-11-04: stage M3 gets Massed fire after the game run showed the hold (35 of 40 held, 36 of 37 loosed), and stage M5 is built in part: the hollow square, the proof slice of hold the line
  (key 545), and the spacing repair, with four trial steps (`volley_massed`, `hold_line_proof`, `square_side_kept`, `spacing_one_rider`), their checks and fixtures. `MarshalFireComponent` is
  `MarshalWordComponent`. Nothing of this has run in the game (Deviations row 20).
- 2026-11-04: stage M5's hold the line is completed (qualifying, the firing order, the slot clamp with its anchor, the strike back, the restore on a charge, a soldier who stops being AI controlled), with the checks `hold-slot` and
  `hold-strike`, three new trial steps and the proof step resting on the driver's check; the volley approach waits as long as its walk; the square step reads the side before the drag; stage M6, the panel, is built; section 9.4 stops
  (Deviations rows 23 to 26). Nothing of this has run in the game.
- 2026-11-05: the first game runs of the full hold the line, Massed fire and the square all read PASS, with one open check (`hold-slot`, Deviations rows 27 and 28). A soldier the engine's climbing code disarms is no longer armed
  again by the toggle (row 29); `hold_line_strike` requires the line to be disarmed first, `hold_line_slot` requires the clamp to act, and the `[Marshal] slot` line counts the steered soldiers who ended nearer and farther (row 30).

- 2026-11-05: review of the full M5 and M6 build applied (Deviations rows 31 to 35). The slot check judges the clamp's trend (steered soldiers that ended nearer against farther, from the distance at the first replaced tick, with
  `steered=` and `loose=`) and not the far count, and `hold_line_slot` reads 15 seconds of reads after the first steer. A fault in the callback pays the line off on the mission tick; `ReleaseAll` tries each soldier on his own; the
  strike check reads the real flag; a held company's soldier outside the line deals no blow that counts against it; `WoundedEver` latches; a quiet control takes a new baseline; soldiers who run away or walk to an object are out of
  the line. The trial's strike window closes on a blow, the square step fails `laid-out` and reads its company from the soldiers, and the judge names the failed checks beside a failing step. The panel's set keeps its id and draws with
  the toggle icon. Two hold texts and three hints say what the code does. Nothing of this has run in the game.

## GitHub Issue

- **Issue:** none. The maintainer waived the issue for this work by his word, and the commit body records the waiver. The draft manifest
  `tools/ingame_checks/0.toml` is shared with the Seat of Seeing and waits for one issue number if the maintainer opens one.
- **Status:** Open.
