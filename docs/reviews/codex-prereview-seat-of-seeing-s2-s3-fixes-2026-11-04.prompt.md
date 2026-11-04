# Codex re-review: Seat of Seeing stages S2 and S3, after two fix rounds, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree, the engine cache
`C:\Users\mikew\.taom-src\v1.5.6\` and the review folder `E:\repos\taom-seat-scratch\s2s3-review`: do not
search the rest of the disk. The work is uncommitted on top of `8b041f03e`, so read it with `git status -uall` and
`git diff`, and read the new files whole.

You reviewed S2 and S3 once, and your report is `E:\repos\taom-seat-scratch\s2s3-review\codex.md`. Since then two fix rounds ran.
Their decisions are `decisions.md` (R1 to R28) and `decisions-w2.md` (W1 to W28) in the review folder, and `w2-improvement.diff`
there is the diff of the second round alone. Each file's "Not applied" list was decided, so an item on it is not a finding unless the reason given
for it is false. The limits named in `docs/features/seat-of-seeing.md` are decided in the same way. The contract is
`docs/features/seat-of-seeing/build-spec.md` sections 2 to 7, section 12 and Deviations rows 36 to 71. Sections 8 to 11 are later stages, so
their absence is not a finding. A game update on 2026-11-04 changed the engine from changeset 125053 to 125270 with the same version string, and
`C:\Users\mikew\.taom-src\v1.5.6-202611041234\` holds the new build where a type is cached. A moved line number alone is not a finding.
Mark anything you cannot prove from code as UNVERIFIED. The test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red for a reason outside this change.

These rounds simplified working code after its only run in the game, and the fixed build has not run in the game. Fixes in earlier stages of
this feature have left defects behind, so read them as new code.

## Questions to answer with evidence

- **Each fix.** For each decision that changes behaviour, does the code now do what it says, on every path? Name any decision that is only half applied.
- **The panel work.** Walk `SeatSession.PanelWork` as it stands: the signals, the closing edge, the reopen, the cursor, the focus line and the hide restore. Can the
  cursor stay shown, or the look's hold stay on, after any exit? Can the reopen fight a close the engine needs? W13 reads the Transfer menu only on a closing edge: is that
  edge still the frame on which the menu's state means what the rule assumes?
- **The kill.** `CommandTrialHost.Kill` calls `Mission.KillAgentCheat` (`Mission.cs:6216`). Name what can make it do nothing or do something else in the steps that use it, among
  them the guard on the mission's mode, an `Immortal` agent, `DisableDying`, and the game keys it reads. Can a kill leave a company with units, so that a step reads a false failure?
- **The failed step.** W10 removed the held verdict: a failed step closes a panel left open at once. Name each thread and callback the close runs on, and what the next step sees.
- **The keys.** `SeatKeys.Tick` now returns unless the state is Seated. Which press, in which state, is now dropped, and is any of them still promised by the doc, the contract or a `0.toml` row?
- **The shared pointer.** `SeatOrderRay.PointerRay`, `RayThickness` and `RayFlags` now serve the order ray and the keep pick. Did the keep pick's ray change in a way that
  changes what it finds? Which thread casts each?
- **The trial's verdicts.** For each row of `SEAT_STEPS` and each `[[check]]` of the Seat's features in `tools/ingame_checks/0.toml`: can it pass with its feature broken, and can a
  working build fail it? W24 makes the judge read `MISMATCH` on the judged line and W23 makes it always read `git_state()`: walk both.
- **Text against code.** Does `docs/features/seat-of-seeing.md`, the registry or a `0.toml` row claim anything the code does not do now?

## How to report

For each finding: the severity, the file and line, the engine or TAOM line that proves it, and what a player or a run would see. If you ran a probe, say what you ran and its output.
Do not re-report a finding already fixed unless the fix is wrong. End with a list of the areas you checked and found sound, and a list of what stays UNVERIFIED.
