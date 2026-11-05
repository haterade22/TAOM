# Codex pre-review: Seat of Seeing stages S4 and S5, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `ca1cba35f`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

The Seat of Seeing is a commander's view for a battle. S1 to S3 are committed. S4 lets the hero fight on under the AI while the player
commands from the Seat. S5 keeps the view and the command alive after the hero falls, and lets the player take a soldier. Both also change what the
battle's input, the boundary countdown, ranged siege weapons and the order panel do while the Seat is up.

The contract is `docs/features/seat-of-seeing/build-spec.md` sections 2, 3, 8, 9, 12 and Deviations rows 72 to 87. Sections 10 and 11 are later stages,
so their absence is not a finding. Engine source: `C:\Users\mikew\.taom-src\v1.5.6-202611041234\` holds the installed build where a type is cached, and
`pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a type that is not cached yet. A moved line number alone is not a finding. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, so do not report these again: `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red until BUTR publishes changeset 125270.
No rule marks a race unplayable, so the take has no race refusal (row 75). `SeatHeroFightsOn` stays false by default (row 73). The ladder queue has no patch, a named limit (row 81).

Game evidence so far, on the build before the last fix: `fight_on`, `morale_held` and `back_to_stand` passed; `take_soldier` passed; `hero_falls` read too early, and its step now waits. That fixed step has not run.

## Questions to answer with evidence

- **Controller writes.** Walk every write of the hero's controller through S4 and S5: enter, leave, fall, take, a rage, the fault path, the mission's end, the Escape menu, retreat and battle end. Can the hero stay on the AI, or stay a player, after the Seat ends? Which thread writes, and when?
- **Morale.** Walk the two changed morale models and `SeatFightRule`. Which game modes reach them? What do they do with the Seat off, in co-op, on a dedicated server? Can a hero's morale stay changed afterwards?
- **The take.** `Patch140_TakeControlOfAgent` and `SeatTakeRule`. What may be taken: a soldier in another team, a mount, a dying agent, a captain, an AI hero, a soldier in a siege machine? Who owns the formations afterwards, and how many are handed? What happens when the taken soldier falls? Does the mission's end or a retreat behave?
- **Other patches.** `Patch140_OrderPanelCheck`, `Patch140_BoundaryCountdown`, `Patch140_RangedSiegeInput`, `SeatOrderPanelGate`: target, signature, kind, and the answer with the Seat off. Which vanilla work does each stop, and what stays stuck after the Seat ends? Can the JIT inline a target? Do the skip prefixes pass co-op and a dedicated server?
- **Bad states.** Can the battle end wrongly, or fail to end, with a hero on the AI or a dead hero and a live Seat? Does a player with no agent reach a code path that assumes one: a null `MainAgent` in TAOM or vanilla code?
- **Settings.** Does each new setting reach its reader, in the right layer? Is each one classified for co-op correctly? Is any default flipped under a saved name?
- **Field trial.** For each new step in `CommandTrialScript.Seat.cs`, `SeatTrialRules` and `tools/run_command_trial.py`: can it pass with the feature broken? Does it write to an agent outside the mission tick?
- **Text against code.** Does `docs/features/seat-of-seeing.md`, the registry or a `0.toml` row claim anything the code does not do? Can a `[[check]]` row pass with its feature broken?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
