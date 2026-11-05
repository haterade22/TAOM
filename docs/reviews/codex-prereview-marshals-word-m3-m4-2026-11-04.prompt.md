# Codex pre-review: Marshal's Word stages M3 slice and M4, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `ca1cba35f`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

Marshal's Word adds commander orders to a battle. M1 (orders in turn) and M2 (keep the array) are committed. This change adds the field trial's steps for them,
M3 without its Massed mode (a company holds its fire and looses on the player's word) and M4 (mark a target, outline it).

The contract is `docs/features/marshals-word/build-spec.md` sections 2 to 4, 7, 8, 11 and its Deviations rows 12 to 15, and `docs/features/seat-of-seeing/build-spec.md` section 12 for the trial's shape.
Massed fire, M5 and M6 are later, so their absence is not a finding. Engine source: `C:\Users\mikew\.taom-src\v1.5.6-202611041234\` holds the installed build where a type is cached, and
`pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a type that is not cached yet. A moved line number alone is not a finding. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, so do not report these again: `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red until BUTR publishes changeset 125270.

Game evidence so far, on the build before the last fix: `volley_holds`, `volley_looses`, `arrive_together`, `direct_order_clears` and `outline_clears` passed. `queue_two_moves`, `mark_keeps_order` and `array_keeps_offsets` failed; the last fix changed those steps, and they have not run since. Only 5 of 40 soldiers held in `volley_holds`.

## Questions to answer with evidence

- **Holding fire.** Walk `MarshalFireComponent`, `MarshalFireDriver`, `MarshalFireTable`, `MarshalFireRules`, `MarshalVolleyJudge` and `MarshalFireAdapter`. What does a held company's soldier do: can one stay unable to shoot after the mode ends, after a formation change, after the feature is switched off, after the mission? Which calls hold and release a shot, and which thread makes them? What does a mounted archer, a thrower, a soldier with no ammunition, a player's own agent or a soldier in a siege machine do? Which part of the company can escape the hold? Is an agent keyed on a slot that can be recycled?
- **The word.** What does a loose do on an empty company, on one with no target, and when pressed twice? Does a direct order or a new order clear the mode as the contract says?
- **Mark.** Walk `MarshalMarkRules` and `MarshalMarkWatch`. What happens when a marked formation dies or changes team, or a soldier moves to another formation? Can the mark leave a company with an order it did not have? Is the mark's order the one the contract names?
- **Outlines.** Walk `MarshalOutlineService`, `OutlineDiff` and `MarshalOutlineAdapter`. Is every painted contour cleared on every exit: setting off, fault, finish, the panel closing, a rider's death or dismount, a soldier's death? Which thread writes a contour? What does a pass cost in a large battle? Does a contour overwrite one that another feature set?
- **Session.** `MarshalSession`: walk the fault path and the owed clear. Does anything run in a screen tick that writes to the world? Does teardown lose a debt? Which teardown paths exist, and does each reach the drivers?
- **Keys and settings.** Do the new keys collide in a battle? Does each setting reach its reader? Is each one classified for co-op correctly? Is the feature off in every mode the contract excludes?
- **Field trial.** For each step in `CommandTrialScript.Marshal.cs`, `CommandTrialHost.Marshal.cs`, `MarshalTrialRules` and `tools/run_command_trial.py`: can it pass with the feature broken? Can its reopen of the panel or its release of held keys hide a defect a player would see? Does it write to an agent outside the mission tick?
- **Text against code.** Does `docs/features/marshals-word.md`, the registry or a `0.toml` row claim anything the code does not do? Can a `[[check]]` row pass with its feature broken?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
