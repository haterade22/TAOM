# RCA: UPGRADE_SKILL_REGRESSION judges templated upgrade edges (plan 060, deep review, 2026-11-19)

## Top line

Plan 060 (#626) makes `UPGRADE_SKILL_REGRESSION` judge an upgrade edge with a `skill_template` side
on the SkillSet with the inline rows laid over it, and makes `ranged_ladder.planned_skill_edits` and
`rebalance_troops.py` resolve a template the same way and refuse to write a templated character. The
tier 2 review (Agent 5 Data flow, Tooling correctness) found one MEDIUM defect: the guard meant to
stop an unread install from passing as clean tested the merged SkillSet dict for emptiness, and the
repo's own SkillSet files always fill that dict, so the guard could not fire on the real tree. Two
LOW findings came with it (no test of either main's refusal; an unreadable SkillSet file exiting with
the ladder tool's "edits pending" code). All three are fixed in the review's commit. Nothing reached a
player: no shipped upgrade edge has a templated side (0 of 913 indexed edges, plan 060). An RCA file
is written because F1 repeats a recorded cause: a guard that cannot see the failure it targets
(`lessons/build-tooling-workflow.md`, "Installed is not effective", 2026-10-14). Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MEDIUM | `Validator._skill_sets`, `rebalance_troops.main` and `rebalance_ranged_ladders.main` refused only an empty `load_skill_sets` result. `skill_set_files` always appends `Main/_Module/ModuleData/**/*skill_sets*.xml` (two files), so the result is never empty on the real tree. An install root whose vanilla modules hold no SkillSet file read every vanilla template as empty, and a real drop on a templated edge passed clean. | Dead / no-op code (a guard that cannot fire) | The executor wrote the guard against the loader's return value without asking which of its sources can be missing. The tests that pinned it used a scratch ModuleData with no repo SkillSet file, the one case where an empty dict is possible. 16 `test_troop_skill_curve` runs of `rebalance_troops.main` read the real repo's ModuleData beside a fake install and passed only because of the repo files. | `load_skill_sets(..., require_vanilla=True)` raises when `vanilla_skill_set_files` finds nothing; the gate reports it as its one `(skill sets)` ERROR and both writers refuse. Four red-green tests, each with a repo SkillSet file present. Lesson added to `lessons/build-tooling-workflow.md`. |
| F2 | LOW | Neither writer's refusal had a test. | Test gap | The refusal was added with a fixture change that only exercises the passing side. | `RebalanceToolTests.test_an_install_with_no_vanilla_skillset_is_refused_though_the_repo_has_one`, `ClampOnlyRunTests.test_an_install_with_no_vanilla_skillset_stops_before_anything_is_written`. |
| F4 | LOW | A SkillSet file that is not UTF-8 raised `UnicodeDecodeError` out of `rebalance_ranged_ladders.main`, exit 1, which that tool uses for "edits still pending". | Missing error handling on a new read | The new read was added outside the `try` blocks its neighbours use for refusals. | The F1 handler catches `ValueError`, of which `UnicodeDecodeError` is a subclass, and returns 2; test `test_an_unreadable_skillset_file_is_a_refusal_not_pending_edits`. |

## Why each agent missed these

- **The plan and the executor:** the plan asked for "one ERROR when the SkillSets cannot be read"
  and the executor tested the result, not the inputs. The docs then repeated the claim ("a SkillSet
  read that fails or finds none is one ERROR"), which was literally true and never reachable.
- **Agent 5, Data flow:** found F1 as trace 3 (rated LOW) by following `skill_set_files` to its
  always-present repo half.
- **Tooling correctness:** found F1 (MEDIUM, with a reproducing probe against the real install), F2
  and F4.
- **Agents 1, 2, 3, 4, 6 and 7:** skipped by tier (3, 4 and 6 by the tier 2 rule) or not in scope.
- **The lead's own first test:** the first draft of the ladder-tool test ran with `--apply` and
  passed against the unfixed code, because that fixture also returns 2 for missing ladder items. It
  was caught by reading why it passed, and rewritten as a dry run that must also write no report.

## Root-cause pattern

F1 and the 16 fixture runs share one cause: a merged load in which one source is always present (the
repo's own files, read from the real checkout even under a fake install) hides the absence of the
source that can be missing (the install). The guard and the tests both looked at the merged result.

## Left for later

- FOLLOW-UP, no issue: an /improve lead files none, and the orchestrator decides at wrap-up.
  `validate_moduledata.skill_template_mismatch_issues` (`tools/validate_moduledata.py:538-543`) has the
  same `if not sets` check, so its "no SkillSet definitions" branch is equally dead on the real
  tree. Whether a root with no vanilla SkillSet still fails there through `scan.unresolved` (`:570-575`)
  depends on which templates carry inline rows; UNVERIFIED.
- FOLLOW-UP, no issue: the orchestrator decides at wrap-up. The ladder writer and
  `RANGED_LADDER_INVERSION` disagree on a templated ladder troop (Agent 5 trace 5); a decision for
  Mike, no troop carries a template today.

## Feedback memories to codify

None beyond the lesson entry. The rule fits the existing lessons file; no harness rule changes.
