# Codex pre-review: Seat of Seeing stages S6 and S7, 2026-11-04

Role: reviewer (`.ai/roles/reviewer.md`). Review only; make no edits, commits or AI calls. Read
`AGENTS.md` and `.ai/review-reference.md` first. Work only from this worktree and the engine cache: do
not search the rest of the disk. The work is uncommitted on top of `6b02b31e2`, so read it with
`git status -uall` and `git diff`, and read the new files whole.

The Seat of Seeing is a commander's view for a battle. S1 to S5 are committed. S6 adds mission time: keys step through slow, fast and fast forward, and one key pauses. S7 lifts the third person view while the order panel is open, under a setting that is off by default.

The contract is `docs/features/seat-of-seeing/build-spec.md` sections 4, 10, 11, 12 and Deviations rows 101 to 115. Engine source: `C:\Users\mikew\.taom-src\v1.5.6-202611041234\` holds the installed build where a type is cached, and
`pwsh tools/taom-src.ps1 path <FullTypeName>` decompiles a type that is not cached yet. A moved line number alone is not a finding. Mark anything you cannot prove from code as UNVERIFIED.

Known and decided, so do not report these again: `BannerlordRefAsmVersion_PinnedGameVersion_IsTheSameGameBuild` is red until BUTR publishes changeset 125270. `SeatRiseWithPanel` stays false by default.

Game evidence: run `E:/Temp/command-trial-runs/20261104-220808` passed `enter`, `pause_flies`, `slow`, `fast`, `speed_cleared`, `rise_with_panel` and `sink_on_close` in one custom battle.

## Questions to answer with evidence

- **Mission time left behind.** Walk the pause, the fixed step, the fast forward flag and the time request through every exit: leave, fault, mission end, a retreat, a take, the Escape menu, and a switch to a menu or a conversation. Can any stay on after the mission or the session ends? The fast forward setter writes a process-global byte through native code.
- **Other writers.** Walk `Mission.IsFastForward`, `AddTimeSpeedRequest` and `MissionState.Paused` against the other writers: the score view, `TakeControlOfAgent`, the Fords and Long Defeat speed code, the order panel's slow time. Does the per-frame rule ever fight them or lie about the level?
- **Illegal engine calls.** Can the session add a request twice, or remove an absent id? Can a key act several times in one press under fast forward? What runs under a pause that assumes time moves?
- **Gates.** What do the keys do in a network session (`SeatMissionClassifier.InNetworkSession`), a co-op battle, a mission type with no time permission, a hideout, a conversation, a faulted session, and on a dedicated server? Is the check cheap in a mission with no key press?
- **The lift.** Walk the offset through every path: panel open and close, first person, a fault, the Seat, a mode change, a mission with no permission, a panel open during the first lift. Can a leftover non-zero offset survive? The target lock tests for exact zero.
- **Settings and keys.** Does each new setting and key reach its reader? Is each classified for co-op correctly? Do keys 533 to 535 collide with another TAOM or vanilla key in that category?
- **Field trial.** For each new step in `CommandTrialScript.Seat.cs`, `SeatTrialRules` and `tools/run_command_trial.py`: can it pass with the feature broken? Does it write outside the mission tick where a rule says it must not? Can a judge pass a real failure line?
- **Text against code.** Does `docs/features/seat-of-seeing.md`, a key or setting text or a `0.toml` row claim anything the code does not do?

Report each finding with severity (CRITICAL, HIGH, MEDIUM, LOW), file and line, impact, and the proving code
or a reproduction. Say PASS for a question you checked and found sound.
