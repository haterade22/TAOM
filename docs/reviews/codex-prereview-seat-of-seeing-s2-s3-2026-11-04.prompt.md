# Codex pre-review: Seat of Seeing stages S2 and S3, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `8b041f03e`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

The Seat of Seeing is a commander's view for a battle. S1 (committed) takes the hero's camera up and flies it.
S2 adds the pointer, the order panel staying open, and hiding the interface. S3 keeps the view on a soldier or a
company. Each stage also adds steps to an unattended field trial that plays one battle by itself.

The contract is `docs/features/seat-of-seeing/build-spec.md` sections 2 to 7, section 12 and Deviations rows 36 to 56.
Sections 8 to 11 are later stages, so their absence is not a finding. Engine source: the game updated at about 06:34 today from
changeset 125053 to 125270 with the same version string. `C:\Users\mikew\.taom-src\v1.5.6-202611041234\` holds the new build where a type is
cached, and `pwsh E:/repos/TAOM/tools/taom-src.ps1 path <FullTypeName>` decompiles a type that is not cached yet. `C:\Users\mikew\.taom-src\v1.5.6\` holds
the older build, and the contract's line numbers come from it. A moved line number alone is not a finding. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, so do not report these again: the test `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red because of that
game update, and another session owns the bump. `E:/repos/taom-seat-scratch/harness-review/codex-fixes.md` lists harness decisions that the fix round will apply
(`reenter_keeps_height`, the build check's timing, the autostart's catch, the direction of `fly`).

Files: `git status -uall --porcelain`, chiefly `Main/Features/SeatOfSeeing/**`, `Client/Features/SeatOfSeeing/**`, `Client/Adapters/*Seat*`,
`TAOM.Tests/Features/SeatOfSeeing/**`, `tools/run_command_trial.py`, `tools/ingame_checks/0.toml`, `docs/features/seat-of-seeing.md`,
the registry's `Patch140` section and `reflection-sites.md`.

## Questions to answer with evidence

- **Cursor.** Walk `SeatCursorRule`, `ISeatScreenAdapter.SetCursor` and the layer. A shown cursor must end on every exit: leave, fault, finish, a rage, the
  hero's fall, the mission's end, the Escape menu, a mode change. Name any exit that leaves the layer on the screen, or removes a layer twice. What does a new layer per show
  cost? Does the look (`DontChangeCursorPos`) run in the order the engine needs?
- **Keep the panel open.** Walk `SeatPanelRule` and the session's panel work. Which close routes does it reopen after, on which frame? Can it fight a close the
  engine needs: battle end, the Escape menu, a conversation, deployment, a hero with no command, `CheckCanBeOpened` false? Can it loop?
- **Focus.** The prefix on the formation target selection handler takes `____centerOfScreen` by reference. What does it do when the Seat is off, when the
  usable area is below 1, and with a cursor outside the screen? Does the focus check line prove what it says?
- **Hide the interface.** `DisableAllUI` has one writer. Walk it through every exit. Is the value restored to what it was, or to a constant? What happens when
  something else had the flag on, and in the next mission?
- **Screen patches.** Walk each new `Patch140` class: its target, signature, kind and answer when the Seat is off. Which vanilla work does each stop, and is anything left
  stuck after the Seat ends? What does each cost a player who never uses the feature? Could the JIT inline a target, and what then? Check the skip prefixes against co-op and a dedicated server.
- **Keep.** Walk `SeatKeepRule`, `SeatOrbit`, `SoldierAtPointer`, `ReadKept` and the session's stored press. Which thread casts the rays? Can a recycled agent slot or an emptied
  company be followed? What does a target's death, a mount with no rider, or a rider's death do? Can the pose reach NaN, a zero distance, or a height under the ground? Which input lets go, and
  which must not? Does anything survive the leave?
- **Keys and settings.** Do the defaults collide with a vanilla key in a battle? Which key tests run with the Seat off? Does each setting reach its reader? Does a hot-path one use a lazy accessor?
- **Field trial.** For each new step in `CommandTrialScript.Seat.cs`, `SeatTrialRules` and `tools/run_command_trial.py`: can it pass with the feature broken? Name what each misses. Does the real-time clock, the
  `live=` status, `CommandTrialSettings` or the reflection read of `_centerOfScreen` change anything for an unarmed session? Can a step write to an agent outside the mission tick?
- **Text against code.** Does `docs/features/seat-of-seeing.md`, the registry or a `0.toml` row claim anything the code does not do? Can a `[[check]]` row pass with its feature broken?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
