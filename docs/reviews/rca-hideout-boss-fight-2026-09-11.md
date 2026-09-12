# RCA: hideout boss fight, boss + N (#564), deep-review 2026-09-11

**Summary.** Six review agents on the #564 change set (a new pure `IHideoutBossFightService`, two
Harmony prefixes under one `Patch86_HideoutBossFight` category, the boss-phase cap on
`TaomBanditDensityModel`, a new MCM integer, the eight boss party templates cut from 67-97 bodies to
one `1/1` boss plus 3-4 soldiers, and the two Python tools that would have re-inflated them).
Standards, compatibility (19 engine members verified against the installed v1.4.8 DLLs, both
Harmony targets with their parameter names), efficiency, completeness and data flow (12 flows, 0
gaps) all passed. The tooling agent found one LOW: a docstring count the diff had not touched.
One further LOW from the compatibility agent was disputed with evidence and is recorded below so
the reasoning survives. Filtered runs green before and after the fix; the full suite could not be
rebuilt at review time because another session's in-flight `FiefGranting` edits do not compile,
so the last full run (8505 passed, 1 pre-existing Armory-data failure, 2 skipped) predates the
one-word MCM label change, and `Main` was rebuilt clean with every change in it afterwards.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | `tools/rebalance_template_power.py:217` (`troop_power` docstring) and `tools/tests/test_rebalance_template_power.py:455` (`HeroTroopHandling` docstring) still said "None of the 50 templates carries a hero", after the change moved the tool's scope from 50 templates to 42. Prose only; no behaviour. | Docs and tooling: a count that lives in more than one place | The edit was done hunk by hunk from a plan that listed the lines to change (the module docstring at `:39`, `solve_flat`, `scale_mins`, the parity comment). The plan's list came from a grep for `boss`, and the two stale sentences contain no `boss`: they quote the total. So the grep that scoped the edit could not see them, and the tool's own tests passed because they assert behaviour, not prose. Same shape as REVIEW-LOG 91 M2 (a fabricated count inside a section about verifying counts): the number was right when written and nothing re-derived it. | When a change moves a number that a file quotes (a count, a scope, a threshold), grep the file for the OLD NUMBER as a literal, not for the concept the number describes. Recorded in `docs/reviews/lessons/build-tooling-workflow.md`. |

**Disputed, no change.** The compatibility agent flagged (LOW) that in a hideout holding only
heroes and bosses, `PlanAssault` returns `FirstPhaseTroopCount = 1` with an empty index list, and
said vanilla's `HideoutMissionController.InitializeMission` assert (`NumberOfTroopsNotSupplied <=
_firstPhaseEnemyTroopCount`) would fire and log. Re-read against the installed decompile: with
three heroes the supplier holds 3 and phase 1 asks for 1, so `3 <= 1` is false and the assert does
not fire; the supplier fills the one phase-1 slot from its non-priority heroes, which is vanilla's
own shape for a priority list shorter than the count (`DefaultTroopSupplierProbabilityModel`
scores non-priority heroes 1.x, above non-priority regulars). The test the agent asked for already
exists (`PlanAssault_OnlyHeroesAndBosses_ClampsSoFirstPhaseIsOne`), and the
`HideoutAssaultSplit.FirstPhaseIndices` doc comment states the divergence and why. Recorded here
rather than fixed, because the fix would have been to a claim that does not hold.

## Why each agent missed finding 1

- **Standards, compatibility, efficiency, data flow**: prose in a Python docstring is outside every
  one of their rule sets by design.
- **Completeness** swept the docs for the OLD BEHAVIOUR (`BossFightCurve`, "first-fight +
  boss-fight", "67-97", "16 bandit", "50 templates") and read `tools/README.md`, not the tools'
  own docstrings; its list of files to check was the list of files the change edited.
- **Tooling** caught it because its brief said "every number quoted in the two tools' docstrings
  must match what the code does now", which made it read the whole file rather than the diff.
  That is the generalisation: a diff-scoped read cannot find a sentence the diff did not touch.

## What the change itself got right, for the record

The user's request was to cut the boss party templates. The research phase found that the template
sizes the boss PARTY parked in the hideout and that the engine sizes the FIGHT from the whole
hideout on two different routes (`MapEventHelper.GetPriorityListForHideoutMission` for the assault,
`HideoutAmbushMissionController.SpawnRemainingTroopsForBossFight` for the sneak-in, whose
`Clamp(pop / 2, 4, 20)` is a floor). Cutting the template alone would have changed nothing the
player could see. That is worth a lesson of its own, recorded in
`docs/reviews/lessons/campaign-mechanics.md`: a party template describes a party, and a mission
built from that party has its own sizing code, which has to be read before a data change is
promised to fix a mission-side symptom.

## Feedback memories to codify

None beyond the two lessons above. Finding 1 is a repeat of a known class (REVIEW-LOG 90 and 91:
unverified counts in prose), and the lesson names the mechanical check (grep the old literal) that
closes it.
