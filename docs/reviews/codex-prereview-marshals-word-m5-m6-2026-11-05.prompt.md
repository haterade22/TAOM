# Codex pre-review: Marshal's Word, Massed, M5 and M6, 2026-11-05

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `6b02b31e2`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

Marshal's Word is a set of company orders for a battle. M1 to M4 and the first slice of M3 are committed. This change adds Massed fire (a company's archers hold until a word, and all loose together), hold the line (section 9.2: a shield company on a Stop or a Move keeps its place and does not attack unless struck), the hollow square's side kept, the spacing repair for one rider (section 9.3) and the order panel's set (section 10, M6).

The contract is `docs/features/marshals-word/build-spec.md` sections 7, 9, 10, 11 and Deviations rows 20 to 30. Engine source: `C:\Users\mikew\.taom-src\v1.5.6-202611041234\` holds the installed build where a type is cached, and
`pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a type that is not cached yet. A moved line number alone is not a finding. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, so do not report these again: `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red until BUTR publishes changeset 125270. Section 9.4 is not built (row 25). The check `hold-slot` failed once and is being measured (row 28): report a defect in the measure or the clamp, not the open status.

Game evidence, under `E:/Temp/command-trial-runs/`: `20261105-002122` (`hold_line_proof`), `20261105-002348` (`volley_massed`), `20261105-002536` (`square_side_kept`), `20261105-002627` (`hold_line_slot`), `20261105-002843` (`hold_line_strike`), `20261105-003126` (`hold_line_flag`). These ran before the fixes of rows 27 to 30.

## Questions to answer with evidence

- **Agent flags and firing order left behind.** Walk `CanAttack` and the firing order through every exit: toggled off, a charge, a retreat, a soldier who leaves the company, a soldier who stops being AI controlled, a detachment, a soldier's death, a fault, the mission's end and removal of the behaviour. Can a soldier stay disarmed, or get a flag back that the engine took away?
- **The clamp.** `OnAIInputSet` replaces `inputVector`. Is the frame right? Read the native call that hands it over if you can. Can the clamp walk a soldier off his ground, stop him fleeing or reaching a game object, or send a non-finite value to native code?
- **Threads and ticks.** What runs off the main mission thread? Does any agent write leave `OnMissionTick`? What does the callback pay per soldier per frame in a large battle?
- **Massed fire.** Can a company be stuck holding after the word, a split, a dead shooter, an empty quiver or a change of order? Can the word leave the split-off soldiers in the wrong company? Does it use `SetOrderWithFormationAndNumber` legally for a player's company?
- **The square and the spacing.** Do the Harmony patches on the arrangement and the spacing fire only where they should? What do they do for an AI team, for another mod's arrangement and in a siege? Is the cached rider ratio read safely?
- **The panel.** Does `MarshalVisualOrderProvider` return the right list in each Order Type and Order Layout, in a hideout, in Long Defeat and in Host Review? Does a click act on the right companies when the selection is cleared first?
- **Gates.** What does each thing do in a network session, a co-op battle, on a dedicated server, with the setting off and with `MarshalsWord` off? Is each setting and key classified for co-op correctly? Can a persisted MCM value surprise a player?
- **Field trial.** For each new step, can it pass with the feature broken? Does it write outside the mission tick where a rule says it must not? Can a judge in `tools/ingame_checks/0.toml` or `tools/run_command_trial.py` pass a real failure line?
- **Text against code.** Does `docs/features/marshals-word.md`, a key or setting text or a `0.toml` row claim anything the code does not do?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
