# Codex pre-review: Marshal's Word, the second fix round on M5, 2026-11-05

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `bb75f3599`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

Marshal's Word M1 to M6 are committed. This change is a refactor and fix round on hold the line (section 9.2): the slot counts move into a Domain type (`SlotCounts`, `SlotTrendCount`), the hold state moves onto `HoldRun` and then into a new `MarshalHoldLineDriver` taken out of `MarshalFireDriver`, the blow reads move from `MarshalFireTable` into a static `MarshalBlowReader` adapter, the trend baseline and the steering now read one position (`Agent.Position`), the window logic moves to `MarshalHoldWindow`, the slot line gains `steered= closer= farther=`, and the lost-control and team-change routes write the last slot line. Behaviour should be unchanged except where Deviations rows 36 to 43 say so.

The contract is `docs/features/marshals-word/build-spec.md` section 9.2 and Deviations rows 20 to 43. The last review record is `docs/reviews/rca-marshals-word-m3-massed-m5-m6-2026-11-04.md`. Engine source: `C:\Users\mikew\.taom-src\v1.5.6-202611041234\`, and `pwsh tools/taom-src.ps1 path <FullTypeName>` for a type not cached. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, do not report again: `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red until a new changeset publishes. The callback's input frame and thread are UNVERIFIED (row 35). Section 9.4 is not built (row 25). A pushed soldier is not walked back (a named limit).

## Questions to answer with evidence

- **Parity of the moves.** Does the new hold-line driver, `HoldRun` and `MarshalBlowReader` keep every behaviour the old code had: each exit that restores `CanAttack`, each count, the order of calls within a tick, and the teardown on both paths? Name any path the old code took that the new code drops.
- **The counts.** Can `SlotTrendCount` count a soldier twice, lose an open interval at take or forget, or count a struck or out-of-line soldier? Does `SlotCounts` `operator +` sum the census correctly?
- **The one position read.** With both reads on `Agent.Position`, is the direction the clamp compares still in the frame the input vector uses? Quote the lines.
- **Threads.** Does any moved code now run from a different thread or tick than before, or touch a mission collection from the agent tick?
- **The window and the trial.** Can `hold_line_proof`, `hold_line_slot` or `hold_line_strike` pass with the feature broken after these changes? Can a judge row in `tools/ingame_checks/0.toml` pass a real failure line?
- **Text against code.** Does the doc, a comment or a `0.toml` row claim what the code does not do?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
