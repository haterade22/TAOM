# Codex re-review: Marshal's Word stages M1 and M2, after a fourth fix round, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree, the engine cache
`C:\Users\mikew\.taom-src\v1.5.6\` and the review folder `E:\repos\taom-seat-scratch\marshal\review`: do not
search the rest of the disk. The work is uncommitted on top of `1d5d0a561`.

You reviewed this work three times, and your last report is `E:\repos\taom-seat-scratch\marshal\review\rereview3\codex.md`.
Since then a fourth fix round ran. Its decisions are `decisions-w4.md` (H1 to H5) in the review folder, and
`w4-improvement.diff` there is the diff of that round alone. **Review that diff, and only what it touches.** Its "Not applied"
list was decided. H3 names two limits, and G5 and G11 are older named limits, so a case inside one of them is not a finding
unless the reason given for it is false. The contract is `docs/features/marshals-word/build-spec.md` section 6.5, the M1 and M2
rows of section 11 and Deviations rows 9 to 11. Mark anything you cannot prove from code as UNVERIFIED. The test
`BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red for a reason outside this change.

Fixes to these verdict rows have left a defect behind in each of the last three rounds, so read these as new code.

## Questions to answer with evidence

- **H1, the hand-over.** `MarshalQueueDriver.MarkHandOver` sets `Behind` on the company's next step when a step with a payload ends by
  arrival and the next step has not started. Does `behind=1` now hold only for a step that waited for a Move's arrival? Walk a step shared by
  two companies, where one arrives and the other does not, and where the shared step ends for one company by a direct order. Walk a next step that a
  direct order clears. Can a dead queue, or a queue that ignores order, still write `reason=arrival end=Arrival company=N behind=1`? Leave out
  the always-true `Arrived` mutant, which was decided earlier.
- **H4, the membership.** `_membersChanged` compares each pass with the one before it, ahead of `_estimate.Clear()`. Can a march whose companies
  did not change read `company-left`? Read `MarshalOrderAdapter.ReadMarch`, including what it omits, and say whether a company can drop out of
  the legs for one pass in a healthy march and so cost a real verdict. Can a return path through `Invalid` or `Emptied` leave the flag wrong?
- **H2, the constants.** With margin 1.4 and floor 15, is any statement in the summaries of `MarshalTuning`, `MarshalChecks.SpreadProvesPace`, the row's comment in
  `tools/ingame_checks/0.toml` or `docs/features/marshals-word.md` false against the rule or the engine lines they cite? Quote the line. Does a
  case outside the H3 limits still read a false PASS or a false FAIL? Give the numbers.
- **H3 and H5, the text.** Does each new sentence say what the code does? Is the rejoin sentence now true?
- **What the fix took away.** `LostMember`, `_firstCount` and the capture assignment of `Behind` are gone. Does any reader remain, in code, tests, fixtures
  or docs? Do the fixtures named in `tools/tests/test_ingame_verdict.py` still match lines the C# can write?

## How to report

For each finding: the severity, the file and line, the engine or TAOM line that proves it, and what a player or a run would
see. If you ran a probe, say what you ran and its output. Do not re-report a finding already fixed unless the fix is wrong.
End with a list of the areas you checked and found sound, and a list of what stays UNVERIFIED.
