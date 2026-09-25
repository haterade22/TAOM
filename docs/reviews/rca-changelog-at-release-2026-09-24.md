# RCA: plan 020, CHANGELOG generated at release (2026-09-24)

## Top-line

Six deep-review lenses and a Codex adversarial review covered `bec0389d..ef5b7ff4`, which
retires the hand-written `CHANGELOG.md` for one generated at `/release` from commit bodies. Nothing
was HIGH. Fourteen findings were confirmed (six MEDIUM, eight LOW) and three were false positives.
Twelve are fixed in the review commit; the executor wrappers (orchestrator machinery) and the
missing issue go to the orchestrator. The report is
`docs/reviews/deep-review-020-changelog-at-release-2026-09-24.md`. Two convergence passes on the
fixes confirmed D1 to D3 (D3 is folded into C1) and E1 to E4, all fixed.

The generator's main job was right: all 22 tests passed, and the real-history smoke reproduced
57/53/4 in the planned group order. Every defect was at the boundaries of that job: what a commit
body can contain, what `HEAD` is at the moment of tagging, and which other files still carried the
duty being retired.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| C1 | MED | Degraded banner names four python-only gates; five exist | Hand-kept list | The diff recounted by subtracting the retired gate from a list already one short since `8dabf4a6` (2026-09-14). No test ties the banner to the hooks | `test_hooks.sh` 5b2 derives the list from the hooks and checks the banner's non-comment lines (convergence pass: the first version matched a source comment); banner prints the hook file names and carries no count |
| C2 | MED | Lens 4's `git diff HEAD -- CHANGELOG.md` cannot see a committed hand edit | Check scoped to the wrong base | Written for `/verify`'s pre-commit moment and copied to a lens that reviews committed ranges | Lens diffs against the review base |
| C3 | MED | A heading-shaped line in a commit body becomes file structure; `## vX.Y.Z (` there blocks that later release | Unescaped free text in a parsed file | The plan prescribed verbatim bodies and a whole-file regex; fixtures were plain prose and every test ran one release | `_contain` escapes ATX-heading lines; a two-release test |
| C4 | MED | `/release` generates from `HEAD`, then tags `HEAD` later; a commit landing between is in no section | Moving ref across a multi-step procedure | The generator and the skill were each correct alone; nobody traced the range from generation to the next release's start | Generator prints the SHA it ended at; Phase 6 checks it; Phase 7 checks that the release commit's parent is that SHA (D1), then tags by SHA |
| C5 | MED | Eleven live instructions still order a CHANGELOG edit; one rule claims a deleted hook enforces it | Retirement sweep too narrow | The sweep searched a directory list for fixed phrases; `docs/features/` was not on the list and reworded instructions pass a phrase grep. Repeat of the 2026-07-01 lesson "A structural refactor's leftover-reference sweep must cover living docs" | Lines fixed; lesson below |
| C6 | LOW | `release-process.md` order contradicts the skill; "every commit"; stale `v2.0.12` link | Skill and contract doc drift | The step text was replaced in place; the order around it and a link into the moved file were not re-read | Fixed; lesson below |
| C7 | LOW | A `###` hand entry above the releases passes the refusal | Check narrower than its docstring | Only `## ` was tested, the level of a dated section, though old entries used `###` | Refusal covers any level-2+ heading; test |
| C8 | LOW | `--version "v2.0.31\n"` accepted | `re.match` with `$` | `$` matches before a trailing newline | `fullmatch`; test |
| C9 | LOW | Five dashes on touched lines | House style | Plan kept them as "untouched parts" of touched lines | Fixed |
| C10 | LOW | Label pattern duplicated with no parity test | Second source of truth | A comment was taken as the link | Parity test reads the hook's pattern |
| C11 | LOW | BOM, UTF-8 stdout, no-tag exit and ordering unpinned though the plan said tests pin them | Claimed coverage | Nobody mutated the code to see a test fail | Characterisation tests |
| C12 | LOW | `/release` description omits the new phase | Stale summary | Description not re-read after adding a phase | Fixed |
| C13 | MED | Executor wrappers still order hand entries | Retirement sweep too narrow | `plans/` was out of the executor's scope | Orchestrator action |
| C14 | LOW | No GitHub issue | Process | Assigned to the orchestrator | Orchestrator action |
| D1 | MED | The C4 fix checked `HEAD` in Phase 6 and tagged by SHA in Phase 7, but nothing tied the tagged commit to the range end: a commit landing between the Phase 6 check and `git commit` was in the tag and in no section. The docstring, the `tools/README.md` row and Phase 4 said `/release` tags the commit the range ends at | Fix checked at one moment, not across the gap | The C4 fix traced generation to the check and the tag to the next release, but not the check to the commit | Phase 7 and `release-process.md` step 8 require `git rev-parse <release commit sha>^` to print the Phase 4 SHA and stop otherwise; the three texts say the range end is the release commit's parent |
| D2 | LOW | Phase 7's confirmation ran `git describe` on `HEAD`, which prints `vX.Y.Z-1-g<sha>` in exactly the case tagging by SHA exists for | Moving ref left in a confirmation step | The C4 fix changed the tag command and not the command that confirms it | It describes `<release commit sha>` |
| E1 | LOW | The D1 docstring said a commit landing mid-release "falls into the next section"; one landing before the Phase 6 check is regenerated into this section, and one landing after it stops Phase 7 | Claim wider than the mechanism | The sentence stated the goal, not what happens in each window | Docstring says no commit that lands mid-release is left out of every section |
| E2 | LOW | The D1 correction did not reach the C4 lesson's Prevent line, this table's C4 row or the report's C4 resolution, and D1 and D2 had no rows here | Correction not propagated | D3 was folded into C1 in every record; D1 and D2 were fixed in the skill and recorded only in the report | Prevent line, C4 row and the report's C4 row carry the parent check; rows D1, D2 and E1 to E4 added |
| E3 | LOW | The convergence verification said no file the pass edits is read by `scan.sh`'s timing path; its `scan_skills` and `scan_hooks` read `release/SKILL.md` and `session-start.sh` | Unchecked claim in a report | Written to explain a timeout flake without reading `scan.sh` | Sentence corrected |
| E4 | LOW | `test_hooks.sh` 5b2 selected only a line-start, double-quoted call and had no minimum: an `if` or `&&` call escaped it, a run selecting nothing passed, and ` jq ` anywhere after the name counted as the jq flag | Selector narrower than what it guards | The D3 rewrite was tested against the banner side (RED on the old banner), not against call shapes the selector could miss | Any non-comment call is selected, only a third argument `jq` skips it, and the check fails when it selects nothing; RED on a hook copy with one call in `if` form |

## Root-cause pattern

C5, C6, C12 and C13 are one pattern: retiring a duty (hand-editing `CHANGELOG.md`) touches every
place the duty is stated, and the plan enumerated those places by where it expected them rather
than by searching for the duty itself. C1 is the same shape in miniature: a list edited by
subtraction instead of being re-derived. C2 and C4 share a second pattern: a check or a range
anchored to `HEAD`, which is right at one moment and wrong at the next.

## Why each agent missed these

- **Executor (plan 020):** followed the plan's prescribed sweep and texts exactly, which is what
  the plan asked; the plan's grep was the gap.
- **Standards:** found C1, C2, C6, C9 and C10. Missed C3 and C4: its checks read each file, not
  a scenario across two invocations.
- **Efficiency:** in scope only for cost; nothing to miss.
- **Completeness:** found C2, C5, C6, C7, C11 and C14. Its instruction sweep went wider than the
  plan's but still found only some of the recipes Codex and Data flow listed.
- **Data flow:** found C4 (the only lens to trace `HEAD` from Phase 4 to Phase 7), C13 and most
  of C5. Missed C3: it traced the label regex and the insertion but not body content into the
  next run.
- **Design:** found C1, C2, C6 and C7 as proposals. It rejected "escaping `#` lines in commit
  bodies" under the simplicity criterion because none of 916 bodies had one. That test looked at
  history, while the defect is about bodies not written yet.
- **Tooling:** found C1, C2, C7, C8 and C11 with mutants and a fixture repo. Missed C3 for the
  same one-release reason.
- **Codex:** found C3, C5 and C6; missed C1, C2, C4, C7, C8 and C13.

## Feedback memories to codify

Three lessons are appended to `docs/reviews/lessons/build-tooling-workflow.md`:

1. A generator that pastes free text into a file it later parses must escape that text's
   delimiters (C3).
2. Retiring a duty: search for the duty, not for its phrases in the expected folders; a skill and
   its contract doc move together (C5, C6, C12, C13; repeat of 2026-07-01).
3. A "was X changed" check diffs against the base of what is under review, and a multi-step
   release pins the commit it read by SHA (C2, C4).

C1 is recorded as a recurrence of the existing lesson on hand-kept counts in
`docs/reviews/lessons/misc.md` (plan 025).
