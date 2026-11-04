# Codex re-review: Marshal's Word stages M1 and M2, after two fix rounds, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree, the engine cache
`C:\Users\mikew\.taom-src\v1.5.6\` and the review folder `E:\repos\taom-seat-scratch\marshal\review`: do not
search the rest of the disk. The work is uncommitted on top of `1d5d0a561`, so read it with `git status -uall`
and `git diff`, and read the new files whole.

You reviewed this work earlier, and your report is `E:\repos\taom-seat-scratch\marshal\review\codex.md`. Since
then two fix rounds ran. Their decisions are `decisions.md` (D1 to D15) and `decisions-w2.md` (E1 to E22) in that
folder, and `w2-improvement.diff` there is the diff of the second round alone. Each file's "Not applied" list was
decided, so it is not a finding. The contract is `docs/features/marshals-word/build-spec.md` sections 1 to 6,
section 11 and its Deviations rows. Sections 7 to 10 are later stages, and the field trial steps of section 11 come
in a later commit, so their absence is not a finding. Mark anything you cannot prove from code as UNVERIFIED.
The test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red for a reason outside this change.

Fixes get less attention than new code, and one fix in the first round introduced a defect, so read these as
new code.

## Questions to answer with evidence

- **Each fix.** For each of D1 to D15 and E1 to E22 that changes behaviour, does the code now do what the decision
  says, on every path? Name any decision that is only half applied.
- **What a fix took away.** E1 made the last pass's legs the march's membership. D2 drops an emptied company. E2 made
  one `RemoveCompany`. E5 moved the setting and count tests out of `MarshalArrayLayout` into the driver. E7 to E10
  gave one clear path through `Stop` and `End`. For each, find every reader of what was removed and say whether it
  still holds. Can a march now start, or fail to start, where the contract says otherwise? Can a pace table outlive
  its march on any path?
- **The end of a march.** Walk `ReadMarch` and `MarchEnd` for Charge, Stop, Retreat, an emptied company, a company
  handed to the AI, a new direct order, a team change, a fault and a session end. Which end does each take, and does
  each release the slowed company in the same frame?
- **The spread verdict.** Walk `SpreadProvesPace`, `SpreadAllowance` and `_estimate`. With `MarshalTuning.SpreadBaseSeconds`
  2.0 and `SpreadShareOfLongest` 0.25, can a build with the pace postfix removed still read PASS? Can a working build
  read FAIL? Give the numbers.
- **The empty queue and the flags.** E11 returns early on an empty queue, E12 reads a flag's point once, and E13 stops the
  redraw. Can a flag be stale, missing, or left behind after the queue empties or a step ends?
- **The selection test.** E6 uses `IsFormationSelectable`. Which formations does it refuse that the old test accepted, and
  the reverse? Check it against `OrderController.cs` lines 105, 123, 157-160 and 174-180.
- **Threads and statics.** `MarshalPaceTable` and `IMarshalHookSink` are new. Which thread publishes and which reads? Can a
  static leak into the next mission now? Does any write to an agent or formation run outside the mission tick?
- **Verdicts.** The rows of `tools/ingame_checks/0.toml` for this feature: can a build with the feature dead still pass a row?
  Can a working build fail one?

## How to report

For each finding: the severity, the file and line, the engine or TAOM line that proves it, and what a player or a run would
see. If you ran a probe, say what you ran and its output. Do not re-report a finding already fixed unless the fix is wrong.
End with a list of the areas you checked and found sound, and a list of what stays UNVERIFIED.
