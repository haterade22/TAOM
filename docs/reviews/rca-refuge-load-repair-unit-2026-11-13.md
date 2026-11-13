# RCA: refuge save-load repair unit (plan 049, deep review, 2026-11-13)

## Top line

Plan 049 moved `RefugeService`'s save-load repair into `Domain/RefugeBookRepair.cs`, with the clock
passed as two lazy delegates bound to the service's `DaysSince` and `NowTime` seams. The tier 1 review
(Lens 5 Data flow, Lens 2 Engine compatibility) found no code defect: the repair moved line for line,
the clock reads stay lazy in the IL, and the save format is unchanged. It found one LOW defect in prose:
a comment in the new test file stated a wider claim about `CampaignTime` than the engine supports. It is
fixed in the review's commit. Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | `RefugeBookRepairTests.cs:203-205` said `CampaignTime.Never` "is the one nonzero CampaignTime a test can build without a campaign". `operator -` (CampaignTime.cs:347-350) on `Zero` and `Never` builds another. What holds: `Never` (long.MaxValue ticks) reads no tick static, and the `Milliseconds` to `Years` factories multiply by tick statics (CampaignTime.cs:39-53) that `CampaignTime.Initialize` (CampaignTime.cs:178-198) sets, so with no campaign they give `Zero` for every finite argument. A first narrowing to "the only public factory" was still too wide: `Hours` and `Days` (CampaignTime.cs:302-315) given NaN or infinity compute NaN, which the unchecked cast turns into long.MinValue ticks (convergence round 1). | Other: comment against engine source | The text came word for word from the plan (`plans/049-refuge-load-repair-unit.md:603`), which had paraphrased the older comment at `RefugeServiceTests.cs:1268-1270`. The executor verified the code the plan supplied but not the comment, and the comment's "the one" is a universal claim that needs every constructor and operator checked, not only the factories. | Comment corrected. Lesson added to `lessons/testing-qa.md`. |

## Why each agent missed it

- **Agent 5, Data flow:** did not miss it in a sense that matters. Its scope is values moving through the
  code; a test comment that states an engine fact is Lens 2's domain.
- **Agent 2, Engine compatibility:** found it, rated it LOW and optional. Confirmed here against
  CampaignTime.cs:39-53, :72, :162, :178-198, :272-350.
- **The executor:** the plan's comment text is a draft under the run's evidence rule, and the comment was
  not re-derived.

## Root-cause pattern

One finding, so no shared theme. The mechanism has a precedent in this repo: plan-supplied prose carried
into a change without a check. The older loose wording at `RefugeServiceTests.cs:1268-1270` is the
source the plan paraphrased; it is the same claim, in an unchanged file.

## Left for later

- FOLLOW-UP, no issue: an /improve agent files none, and the orchestrator decides at wrap-up.
  `RefugeServiceTests.cs:1268-1270` carries the same loose wording as F1. A reword should use F1's
  corrected text (Never reads no tick static; Hours and Days give Zero for every finite argument with no
  campaign), not "the only public factory".
- FOLLOW-UP, no issue: same reason. `RefugeBookRepair.cs:50-53` and `:72-73` say a NaN target never
  reaches `BuildProgress >= 1`; `RefugeData.cs:61-62` already returns 1 for NaN. The text moved verbatim,
  and the plan's line-for-line move check forbids a reword in the move commit.
- FOLLOW-UP, no issue: same reason. `RefugeService.cs:617-618` and `RefugeCampaignBehavior.cs:63-64` name
  "the LoadFrom belt"; the load-time disband cancel is in `OnGameLoaded` (`RefugeService.cs:711`).
- FOLLOW-UP, no issue: the plan gives this to the orchestrator. `docs/features/refuge.md:112-117` omits
  the future-start reset, and its Key files table has no row for `Domain/RefugeBookRepair.cs`.

## Feedback memories to codify

None. The lesson entry is enough; the standing evidence rule already says plan text is a draft.
