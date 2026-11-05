# Marshal's Word

## Overview

Marshal's Word gives the player richer orders for companies (formations) in a battle. This file covers the four stages built so far, of
the six in [marshals-word/build-spec.md](marshals-word/build-spec.md). **M1, Orders in turn:** hold the queue key (Give the order in turn,
default Left Shift) while giving an order with the order panel open, and the company carries it out after its current orders. Flags on the
ground show the road ahead. **M2, Keep the array:** several selected companies given one click or drag move as one body, keep their places
relative to each other, and walk at paces that bring them in together. **M3, Loose on my word (built without Massed):** key 543 (Change the
Archers' Fire, default Comma) switches the selected companies between firing at will and holding a drawn bow, and key 544 (Give the Word,
default M) looses every archer who was already holding, a fraction of a second apart. The mode that fires by itself when enough archers are
ready is held back until the game has shown that a bow stays drawn. **M4, Mark and show:** hold Mark, Do Not Move (default Left Ctrl) while
you order a charge on an enemy company, and the company takes that company as its target and keeps its own movement order (a mark steers
archers and other ranged soldiers; soldiers fighting hand to hand choose their own foe); gold outlines
show the selected companies and red outlines the company one of them marks or charges. Stages M5 (hollow square, hold the line, spacing
repair) and M6 (the panel entries) are not built, and key 545 is not registered.

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
reads the three held keys only with the order panel open, and the two pressed keys and the show-indicators key on every live frame; it forwards
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

- **The archers' hold** (`MarshalFireComponent`, `MarshalFireDriver`, `MarshalFireTable`, `MarshalFireAdapter`): native calls
  `AgentComponent.OnAIInputSet` once a tick for an AI-controlled soldier whose callback flag is on (Agent.cs:1660-1666), with the flags the AI
  has just decided. While a company's mode is on-word, the driver (once a second of mission time) adds the component to each of its soldiers
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
   +-- MarshalFireDriver --> MarshalFireRules / MarshalVolleyJudge (Domain) ; IMarshalFireAdapter ; MarshalFireTable <-- MarshalFireComponent (native)
   +-- MarshalMarkWatch ; MarshalOutlineService<Agent> --> OutlineDiff (Domain) ; IMarshalOutlineAdapter<Agent>
```

## Configuration

MCM group "Marshal's Word", all defaults on, read once a frame through a lazy accessor (`MarshalSettingsProvider`). The four settings are
excluded from the co-op fingerprint in `CoopSettingsRelevance`.

| Setting | Default | Effect |
|---|---|---|
| `MarshalEnabled` | on | Read when the session decides whether to go live. Off, none of this acts from the next battle on; a battle already running keeps the feature (build-spec Deviations, row 3) |
| `MarshalKeepArray` | on | A click or drag moves several companies as one body. Off, vanilla lays them out |
| `MarshalArriveTogether` | on | Companies ordered together walk at paces that bring them in together. Needs Keep the Array |
| `MarshalOutlines` | on | Gold and red outlines while the panel is open or the show-indicators key is held. Off, every outline is cleared at the next pass |

Keys (Options > Keybindings > Action, category `TaomMarshalHotKeyCategory`, ids from 540): **540 Give the order in turn**, default Left Shift,
held. **541 Mark, do not move**, default Left Ctrl, held. **542 Keep the widths**, default Left Alt, held: a drag turns the group without
stretching it. **543 Change the archers' fire**, default Comma, pressed. **544 Give the word**, default M, pressed. The three held keys are
read only while the order panel is open; the two pressed keys act on the selected companies whenever the player commands the team
(`IsPlayerGeneral || IsPlayerSergeant`, the question the panel asks before it opens), and their defaults are in none of the eight
categories the mission's scene layer reads; they are vanilla's keys too, so they still act with the panel closed (zoom, banners), and Left Alt has one more
vanilla reader, `GauntletInformationView.cs:61`, which extends the on-screen message timer. A held key is the contract's accepted exception to the rule that a default key stays clear of every key the scene layer reads:
Left Shift is also game key 24 (Zoom), so on the hero's camera holding it zooms; Left Alt is game key 5 (ShowIndicators), so it shows the
banners and, with Outlines on, the outlines. Left Ctrl has three vanilla readers (a ground drag lays out horizontally, which also turns Keep
the Array off for that drag; the number keys select no company; a click on a card adds it to the selection), so a player who selects with
Ctrl and still holds it on the release marks instead of charging. Id 545 is reserved for stage M5.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/MarshalsWord/Domain/` | Pure rules: `MarshalOrderQueue` and `QueueStep`, `MarshalArraySolver`, `MarshalMarch`, `MarshalChecks`, `MarshalTuning`, `MarshalFireRules` (the draw state machine and the stand-aside rule), `MarshalVolleyJudge`, `MarshalMarkRules`, `OutlineDiff`, `ShotWatch`, and the trial's `MarshalTrialRules` and `MarshalTrialRows` (BCL only, gated) |
| `Main/Features/MarshalsWord/MarshalFireDriver.cs`, `MarshalFireComponent.cs`, `MarshalFireTable.cs`, `MarshalMarkWatch.cs`, `MarshalOutlineService.cs`, `IMarshalOutlines.cs` | The archers' hold, its shot counters, the mark proof and the outlines |
| `Main/Adapters/IMarshalFireAdapter.cs`, `MarshalFireAdapter.cs`, `IMarshalOutlineAdapter.cs`, `MarshalOutlineAdapter.cs` | The engine side of the hold and of the paint |
| `Client/Features/MarshalsWord/Trial/` | The Marshal half of the field trial: `CommandTrialScript.Marshal.cs`, `CommandTrialHost.Marshal.cs`, `ICommandTrialHost.Marshal.cs` |
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
`MarshalOrderPayloadTests` (the allow list row by row, the widths key a drag records, the flag height), `MarshalFireRulesTests`, `MarshalVolleyJudgeTests`,
`OutlineDiffTests`, `MarshalFireDriverTests`, `MarshalMarkAndOutlineTests`, `ShotWatchTests`, `MarshalMarkCaptureAndShotTableTests` (the mark payload, the queue driver's marks, the capture prefix with the mark key,
the shot table, and the source pins with the engine line behind each), `MarshalTrialRulesTests`, `CommandTrialMarshalStepsTests` (each trial step passes on a working stand-in and fails with its own facts when the one thing it reads is broken; the limits of that claim are under "The trial's limits" below), `MarshalPatchTests` (bindings,
parameter names, drive each prefix, the pace table), `MarshalAdapterPinTests` (the engine-facing adapter statements a test cannot run, each
with its engine line), `MarshalsWordWiringTests` (the Domain gate, the module, the view's forwards), `MarshalsWordChecksRenderTests` (the
[Check] lines the code writes against the fixtures and the manifest), `TaomMarshalHotKeyCategoryTests`, `MarshalSettingsProviderTests`, `MarshalWordingTests` (the English of every later-stage text against its literal, its row and the seed row of each language, the 14 seconds against `MarshalTuning.HoldLimit`, and each default named in a hint against the key or the setting it names).
`tools/tests/test_ingame_verdict.py` holds `MarshalsWordManifestTests` and twenty-eight fixtures; `tools/tests/test_run_command_trial.py` ties the wrapper's `MARSHAL_STEPS` to the row list and the judge to Marshal's failure lines.

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

   A run with no `--steps` arms every step of the Seat and of Marshal's Word in one battle, which the Seat's steps fit and Marshal's do not.
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

## Co-op: the four questions

Marshal's Word is off whenever a network session is active (the engine's flag or a live co-op session, `SeatMissionClassifier.InNetworkSession`), so the answers are short. The archers' component, the shot table and the outline
paint are created only by a live session, so they follow the same answers. **Presence** (`IsCoopActive`): the patches are applied
for every player, and each returns at its first test unless the session is live, which it never is in a session. **Session and role:** no
session means the local machine owns the whole simulation; a session stops the feature before it begins. **Dedicated server:** no screen, so no
view and no session; the three patches are applied but see `IsLive` false. **Whose party:** the player's own team in a single-player mission.
`MarshalView` is a `Local` row in `CoopMissionRoles`. The four settings are listed as non-simulation in `CoopSettingsRelevance`.

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
- **Massed is not built.** Key 543 cycles free and on-word only, and nothing computes a massed word. The enum has no third value yet.
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

## Performance

A frame with no live session reads no key. A live frame reads the three held keys only with the panel open and the two pressed keys and the show-indicators key always, copies three settings and, with the panel open,
rebuilds the flags at most four times a second while any step is queued (an empty queue draws once and stops). The capture prefixes cost a
field read each. The array prefix runs at 30 Hz while a drag is drawn with the mouse held (OrderTroopPlacer.cs:300, 799), with an
`OutcomeCounter` increment and no string; the queue driver returns at once while no company holds a step. The pace postfix is one volatile read when no march runs. With no company on a mode, the
archers' driver returns before any walk and no component exists; with one, a census walk costs one pass over its soldiers once a second, and
each callback is one native-to-managed trip per flagged soldier, with two counters and no line. The shot callback returns at its first test
with no session. The outline service returns at once while nothing should show and nothing is painted, and otherwise runs four times a second
over the selected companies' soldiers. The mark watch runs once a second over the companies that hold a mark and skips a company on Charge, Advance or
ChargeToTarget before it walks the company, so a battle of charges pays nothing for it; a marked company costs a walk of its soldiers, up to 20 native
reads and one log line a second.

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

## GitHub Issue

- **Issue:** none. The maintainer waived the issue for this work by his word, and the commit body records the waiver. The draft manifest
  `tools/ingame_checks/0.toml` is shared with the Seat of Seeing and waits for one issue number if the maintainer opens one.
- **Status:** Open.
