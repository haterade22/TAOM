# Codex re-review: Marshal's Word stages M1 and M2, after a third fix round, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree, the engine cache
`C:\Users\mikew\.taom-src\v1.5.6\` and the review folder `E:\repos\taom-seat-scratch\marshal\review`: do not
search the rest of the disk. The work is uncommitted on top of `1d5d0a561`, so read it with `git status -uall`
and `git diff`, and read the new files whole.

You reviewed this work twice, and your last report is `E:\repos\taom-seat-scratch\marshal\review\rereview\codex.md`.
Since then a third fix round ran. Its decisions are `decisions-w3.md` (G1 to G11) in the review folder, and
`w3-improvement.diff` there is the diff of that round alone. Its "Not applied" list was decided, and G5 and G11 are
named limits, so none of those is a finding unless the reason given for it is false. The contract is
`docs/features/marshals-word/build-spec.md` sections 1 to 6, section 11 and its Deviations rows. Sections 7 to 10 are
later stages, and the field trial steps of section 11 come in a later commit, so their absence is not a finding.
Mark anything you cannot prove from code as UNVERIFIED. The test
`BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red for a reason outside this change.

Fixes to this verdict rule have twice left a defect behind (an emptied company, then the proof's baseline), so read
these as new code.

## Questions to answer with evidence

- **Each fix.** For each of G1 to G11 that changes behaviour, does the code now do what the decision says, on every
  path? Name any decision that is only half applied. Is any reason given for G5 or G11 false?
- **The spread verdict, again.** With `ArrivalSlack` 3, `MinPace` 0.3, `SpreadBaseSeconds` 2, `SpreadShareOfLongest`
  0.25, `SpreadMinLongest` 10 and `SpreadProofMargin` 1.15, walk `MarshalMarch.Update`, `Spread`,
  `MarshalMarchDriver.Conclude` and `MarshalChecks`. Do the measure and its control now mean one thing? Can a build
  with the pace postfix removed still read PASS? Can a working build read FAIL? Try unequal speeds, the slower company
  on the shorter or on the longer walk, soldiers above the cached speed, a company inside the slack on the first pass,
  and the last company still outside the slack when the march ends. Give the numbers of any case you find.
- **Who is in the march.** `LostMember` compares the latest pass's count with the first pass's count. Can a company
  leave and come back, or can the count stay equal while the members differ? What does the line then say?
- **`behind=`.** `QueueStep.Behind` is set when the step is captured. A step ahead of it can be cleared, dropped or
  finished by patience before it starts. Does `behind=1` then still mean what the row's comment says? Can a build with
  a dead queue write `reason=arrival end=Arrival company=N behind=1`?
- **What a fix took away.** G7 made `Stop` private and added `OnPlayerTeamChanged`. G10 deleted `FirstLongest` and
  `FirstShortest`. G8 moved `CaptureResult` and `MarshalCall`. For each, find every reader of what moved, and say
  whether it still holds. Can a pace table now outlive its march on any path, in particular a fault and a session end?
- **Other verdict rows.** The rows of `tools/ingame_checks/0.toml` for this feature: can a build with the feature dead
  still pass a row? Can a working build fail one? Do the fixtures named in `tools/tests/test_ingame_verdict.py` match
  lines the C# can write?

## How to report

For each finding: the severity, the file and line, the engine or TAOM line that proves it, and what a player or a run would
see. If you ran a probe, say what you ran and its output. Do not re-report a finding already fixed unless the fix is wrong.
End with a list of the areas you checked and found sound, and a list of what stays UNVERIFIED.
