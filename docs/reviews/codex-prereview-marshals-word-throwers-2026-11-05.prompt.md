# Codex pre-review: Marshal's Word, throwers on an advance (section 9.4), 2026-11-05

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `6d2c51de9`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

The contract is `docs/features/marshals-word/build-spec.md` section 9.4 and Deviations row 51 onward. On an Advance, a player company mostly of throwing soldiers stands at a gap it throws from, then is released to vanilla's Advance and goes in to melee. A main-thread driver publishes an immutable table; a Harmony postfix on the private `MovementOrder.GetPositionAux` replaces the Advance answer on the AI thread; a prefix and postfix timer on `CreateNewOrderWorldPositionMT` measure the cost. Both patches are in the deferred category `Patch_MissionTime_SetMovementOrder`. Engine source: `C:\Users\mikew\.taom-src\v1.5.6-202611041234\`, and `pwsh tools/taom-src.ps1 path <FullTypeName>` for a type not cached. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, do not report again: `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red until a new changeset publishes. Translations go to a backlog issue. No game has run this code.

## Questions to answer with evidence

- **The postfix on the AI thread.** Is every read in the postfix safe off the main thread, with no allocation, no lock and no native call beyond vanilla's? Is `__instance` a struct copy, and does anything rely on writing it? Does it yield in Deployment, FallBack and every mission the contract excludes?
- **The table and its lifetime.** Is the table published and withdrawn on every route: order change, direct order, hold, fire mode, arrangement, AI control, team change, setting off, fault, mission end? Can a stale table outlive its mission?
- **The rule for "then".** Does the run release on each named reason, and does the release hand the company back to vanilla's Advance so it goes in to melee? Can it stand forever or walk back?
- **The timer.** Is the measurement correct (buckets, max with compare-and-swap, epoch reset off the AI job)? Can its verdict pass with no data?
- **The trial.** Can `advance_throwers` pass with the feature broken? Can `advance_throwers_vanilla` fail? Does the roster swap touch any other subset?
- **Settings and co-op.** Is the new setting classified and counted, and is the feature inert in co-op and network sessions?
- **Text against code.** Does the doc, a comment or a `0.toml` row claim what the code does not do?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
