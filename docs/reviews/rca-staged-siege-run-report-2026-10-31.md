# RCA: Staged Siege (#1054) run report, deep review (2026-10-31)

**Summary.** The run report makes a staged siege battle and an armed lord's hall fight write, when they end, a header,
one line per stage, one `CHECK <id> PASS|FAIL|NOT SEEN: <evidence>` line per check and a summary, so that an in-game test
is judged from the log alone (Mike, 2026-10-31). A read-only campaign behavior, `StagedSiegeCampaignLog`, logs the map
event end and the owner change for the staged settlement. The change was reviewed uncommitted in the worktree
`E:\repos\taom-staged-siege`, branch `taom-staged-siege-report`, from trunk `47bf2fdc9`. Risk tier 1: it runs in the
mission tick and adds a campaign behavior through module composition. Two lenses ran: data flow and engine
compatibility. No XML and no script changed. Codex was not dispatched, because Mike gave no word for a paid review of
this work.

- **Severity:** 0 CRITICAL, 0 HIGH, 2 MEDIUM (row 0 found by the orchestrator before the review), 11 LOW (rows 11 and 12
  found by the re-review of the fixes).
- **Outcome:** all fixed. The fix round wrote its tests first and saw 31 of them fail before the fixes. The data flow
  lens re-reviewed the fixes, because rows 2 to 4 change runtime code at the mission boundary: all seven of its fixes
  closed with no new runtime defect, and it found rows 11 and 12, both fixed (row 11's two tests seen failing first).
- **Not covered:** nothing has run in game. The report lines, the HUD lines and the campaign lines are first seen on the
  next in-game run.

Feature doc: [staged-siege.md](../features/staged-siege.md), section "Run report". Earlier RCAs: [Phase 1](rca-staged-siege-2026-10-29.md),
[Phase 2](rca-staged-siege-phase2-2026-10-29.md), [Phase 3a](rca-staged-siege-phase3a-2026-10-29.md).

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 0 | MEDIUM | The campaign log cleared its record at the map event end, but the owner change arrives after that event: `MapEvent.FinalizeEventAux` raises `OnMapEventEnded` (`MapEvent.cs:1737`) before `Component.OnBeforeMapEventFinalize()` (`:1738`), which raises `SiegeCompleted` (`SiegeAssaultEventComponent.cs:248`), so `KingdomManager.SiegeCompleted` calls `ApplyBySiege` (`KingdomManager.cs:260`) and the owner change is raised at `ChangeOwnerOfSettlementAction.cs:94`. The owner line, the evidence for in-game check 12, would never have been written. Found by the orchestrator's spot check of the first build. | Event order assumed from event names | The spec said "clear the record after the map event end line" and nobody opened the method that raises both events. The spec author inferred the order from the names. | Fixed: the behavior remembers the settlement and `CampaignTime.Now.ToMilliseconds` at the map event end and logs an owner change of that settlement at the same campaign time. Lesson below. |
| 1 | MEDIUM | The HUD marker letters were recorded in every armed mission and read by nothing: the walls report stored them, the hall report dropped them, and no line printed them. Lens: data flow. | A fact with no consumer | The spec listed the marker as a fact but gave it no check and no line. | Fixed: both reports print them in the `hud` evidence; they are never a criterion, because a point can be off screen. |
| 2 | LOW | `StagedSiegeSlot.ClearArmed` left the HUD record set, so a finished `Mission` stayed referenced from a static until the next staged battle armed. Lens: data flow. | A static that outlives its mission | The spec cleared the record only on arm, never at teardown. | Fixed: `ClearArmed` (each mission's `OnCreated`) clears it, after the previous report was written. |
| 3 | LOW | Report facts were fed after calls that can throw: the hall's header after `SpawnFlag` and `ColourFlag`, the walls' set-up after `SetUpFlags`. A flag fault would have printed an empty header and "no set-up", and a `ColourFlag` fault "0 flags spawned". Lens: data flow. | Order of side effects | Each feed was written at the end of its block. | Fixed: each fact is fed when it is known, and a flag count never read prints NOT SEEN. |
| 4 | LOW | `CHECK faults` saw only service faults: `StagedSiegeMissionEdge.Fault` only logged, so `faults PASS` could stand beside a boundary fault line, and that line said "staged rules off" although the service stays live. Lens: data flow. | A verdict narrower than its claim | The spec defined the fault fact at the service level only. | Fixed: every edge catch passes the service and hall it holds; the report keeps service and boundary faults apart; the edge line reads `boundary fault in <site>: ...; this call skipped, vanilla continues`. |
| 5 | LOW | `CHECK hud` passed when no stage was reached (a quit during deployment), because "every reached stage was shown" is true of an empty set. Lens: data flow. | A PASS with nothing evaluated | The spec rule did not say what an empty set means. | Fixed: NOT SEEN "no stage reached". Lesson below. |
| 6 | LOW | `CHECK victory_cue` failed "never ran" when vanilla's `BattleEndLogic` ended the battle in the same check as a staged `AttackerWon` (`Mission.CheckMissionEnded` takes the first logic that returns true, and `BattleEndLogic` is earlier). Lens: data flow. | A FAIL without proof that the code misbehaved | The build widened FAIL to "never ran" without asking which paths skip the cue. | Fixed: NOT SEEN with that reason; FAIL stays for a cue that threw. `hall_cue` keeps FAIL for "never ran", because the hall cue runs inside the capture itself. |
| 7 | LOW | `CHECK refill` failed a stage and side that the reserve budget held on purpose: `ReserveBudget` rounds the cumulative allowance down, so a small reserve can get nothing for a whole stage. Lens: data flow. | A FAIL for designed behaviour | The spec rule did not account for the budget's rounding. | Fixed: a stage and side with a budget hold is named "held by budget" and is not a miss. |
| 8 | LOW | The campaign log's engine reads were not pinned in `StagedSiegeBindingTests`, unlike every other engine-facing class of the feature. A renamed member fails when the handler is JIT-compiled, before its `try` runs. Lens: engine compatibility. | A missing binding pin | The spec did not name the binding test, and the build did not apply the feature's own convention to the new class. | Fixed: four `[BindingVerification]` tests pin both event types, the `MapEvent` reads, `TroopCasualties`, `CampaignTime.Now` and `ToMilliseconds`, and `Hero.Clan`. |
| 9 | LOW | The doc called `TroopCasualties` "casualties": it counts every troop wounded, killed or routed on a side over the whole map event (`MapEventSide.cs:613-644`), so after a hall fight it sums both fights. It also did not say that the new owner is the kingdom leader (`KingdomManager.cs:247`). Lens: engine compatibility. | Doc precision | | Fixed in the doc. |
| 10 | LOW | Nothing pinned that each campaign start builds a new `StagedSiegeCampaignLog`, whose constructor is the reset of a static record (`.claude/rules/csharp-architecture.md`, "Singleton Services Holding Per-Campaign State", asks for that pin). Lens: engine compatibility. | A missing lifecycle pin | The wiring test checked only the behavior type. | Fixed: a test asserts that two `Create` calls return different instances. |
| 11 | LOW | Row 4's new edge line said "this call skipped" for every catch, but the `EarlyStart` catch drops the service and the hall, so the staged rules are off for the whole mission and no report follows: that line is the only evidence. Lens: data flow (re-review). | A shared message for unlike effects | The fix brief wrote one wording from the common case. | Fixed: `Fault` takes the effect text; `EarlyStart` passes `staged rules off for this mission`; an edge test and a source pin in `StagedSiegeWiringTests`. |
| 12 | LOW | The doc lagged rows 2 and 3: the `flags` and `hall_flag` NOT SEEN cells, the hall `faults` row and the HUD record's clear points. Lens: data flow (re-review). | Doc lag | | Fixed in the doc, which also names the `EarlyStart` line. |

## Root-cause pattern

**A self-check's verdict must be earned (rows 1, 4, 5, 6, 7).** The run report is a new kind of code in TAOM: the mod
grades its own run. Each of these rows is a verdict the report would have printed without the evidence for it. A PASS
needs at least one evaluated input; an empty set is NOT SEEN (row 5). A FAIL needs proof that the code under test
misbehaved; a path where vanilla acted first (row 6) or a hold the design makes on purpose (row 7) is not a failure. A
check must cover every source of the thing it names (row 4). And a fact that feeds no line and no check is dead weight
(row 1). Mike asked the same day for this kind of logging in every feature, so more run reports will follow, and the rule
goes into the lessons now.

**Event order is a decompile question (row 0).** Today's Fords of War RCA added the lesson that an engine method's
effect must be read from the line that produces it. Row 0 is the same miss in another form: the order in which one
engine method raises two events.

## Process

The first build saw no RED run for the report tests: the tests were written first, but the first run came after the
domain classes existed. The fix round wrote stubs, ran the tests and saw 31 fail before each fix.

## Why each lens caught or missed what it did

- **Data flow** traced 18 flows (every verdict against every end path, the settlement and HUD record lifecycles, the
  write-once report, engine floats and threads) and found rows 1 to 7. It confirmed row 0's fix against the decompile. Its re-review of the fixes traced 9 flows and found
  rows 11 and 12.
- **Engine compatibility** verified 25 engine usages against the installed v1.5.6 DLLs, including the six-argument
  owner-change event, the synchronous siege-won chain and the per-campaign factory, and found rows 8 to 10.
- **Row 0** was fixed before the lenses ran, so neither had it to find; both re-verified the chain.
- **Codex** was not dispatched.

## Lessons

- `lessons/testing-qa.md`: a self-check's verdict must be earned (rows 1, 4 to 7).
- `lessons/adapters-taleworlds-api.md`: read the raise order of two engine events before a listener depends on it (row 0).
