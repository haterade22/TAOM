# RCA: Seat of Seeing, stages S6 and S7 (deep review, 2026-11-04 and 2026-11-05)

**Top line:** the tier 1 `/deep-review` of mission time (S6) and the view that rises with the order panel (S7) found one HIGH: when another writer raised fast forward at level 2 or 4, the Seat called that level 10 but left its fixed step on, which the engine's arithmetic makes a clock of 20x or 40x at 60 FPS. The second wave, which that HIGH required, found no HIGH. It found one MED that the first round of fixes had introduced. Run `20261105-003400` proved the fix with the new step `adopt_flag` (`ratio_four=10.0 fixed_step_four=0`), and run `20261105-003513` passed all 25 Seat rows in one battle.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH (Codex 1; lens 5 and lens 2 at MED) | `SeatTimeLadder.Frame` adopted another writer's flag as level 10 and wrote nothing, so the fixed step (from 2 or 4) or the slow request (from 0.5) stayed on | One clock with several multipliers | The contract's section 10 said to write nothing when the flag is adopted, and `SeatSessionTimeTests` pinned that without reading the fixed step. The `fast` step reached 10 only through the key route | Lesson below. `adopt_flag` plays the other writer in the game, and source pins hold the two `FordsMissionSpeed` writes the fix rests on |
| 2 | MED (Codex 2) | `FastRatio` capped the expected ratio after it averaged the frames, so uneven frames gave a wrong expectation and a level near 1x could pass | A trial rule simpler than the thing it measures | The contract's formula, `min(level, 0.1/mean frame time)`, was written that way | The step adds each frame's capped step, reads the fixed-step and fast-forward facts at each level, and fails `level-indistinguishable` when a clock at 1x would also match |
| 3 | MED (lens 5 F2) | `ClockOwnedElsewhere` gated `Press` and the cleanliness read only, so `OnFrame` and `Reconcile` took Long Defeat's flag for the Seat's own level. Slow then never reached 0.5, and the message was false | A guard at one of several readers | Row 105 stated the rule, and the code applied it at the press. No test set `OwnedElsewhere` and `FastForward` together | The rule now covers every read, with tests that set both |
| 4 | MED (second wave, Completeness 1 and Standards 1) | The first fix round's resume ran before the setting test, so Stay with the Seat off ended the pause and left the slow request on. The docs still said the keys do nothing with the Seat off | A fix that widens a path past a later rule | My decision said only that a pause "can always be resumed", and the builder chose to skip the setting and the permission too. It reported the choice, and no test varied the setting with a pause held | Stay resumes, then cancels with the Seat off, with tests for the setting and the permission. The second wave read the fix for this reason |
| 5 | MED (second wave, Completeness 2 and 3) | Nothing pinned the two engine writes the HIGH's fix rests on, and no trial step could reach the adoption path, so the fix had no proof in the game | A fake that restates the rule under test | The session tests run on a hand copy of `FordsMissionSpeed.Set`, and the stand-in's `PressTime` does not run `SeatTimeControl` | Source pins, a `Frame(0f)` assert in `FordsGameSpeedTests`, and `adopt_flag` |
| 6 | MED (second wave, Efficiency 1) | `SeatScreenAdapter` built a new delegate for `MissionThreadGuard.NoteCall` each frame at 2x and 4x | Per-frame allocation | Efficiency runs only in a second wave. `CreatureSiegeRoleMissionBehavior.cs:49-50` already shows the cached form | One cached delegate, with the wiring pin changed to read it |
| 7 | LOW (lens 5, lens 2, lens 7, Codex 3 to 5, Tooling) | Wrong or loose text: the hint promised a cancel at the toggle and not at the next press, the level 10 and pause messages, the take's promise, teardown debt wording, the score screen's F row, the pause's managed writers, hideout `Time` | Docs that state more than the code does | Each was written from the contract before the engine lines or the game could correct it | Each sentence corrected, with Deviations rows 120 to 126 |
| 8 | LOW (Tooling, lens 7) | `enter.fail` lacked the thread guard's line, stale counts in help and docs, redundant test, no test of the six key rows against the English, no human row for the Options rows, the collision list missed 533 to 535 | A list kept by hand | The guard line is the same class as row 2 of the S4 and S5 RCA. The other items are small gaps in lists that nothing computes | Applied. Counts are removed, not raised |
| 9 | LOW (second wave, Design, Standards, Completeness) | Two reads of the flag, two meanings of `Adopt`, two derivations of dropping fast time, a dead write, a debt that kept the per-frame rule running, a type in the wrong file, false comments, weak test names and asserts | Duplication after fixes | The first fix round added to the code without folding it | Folded into `ReadFlag` and `DropFastTime`, and the rest applied. Section banners are left: a pattern in 27 files needs its own change |

**Not applied:** the setting turned off in the Seat at level 10 leaves the level at 4x on the next leave, because the setting is read at a press (a named limit). A failure line from `Finish` for a speculative case (FOLLOW-UP: no issue: `simplicity-criterion.md` says a guard for a speculative case needs the maintainer's word, and nothing has produced this case).

## Root-cause pattern

Rows 1, 3 and 5 share one cause. The contract and the tests treated the fast-forward flag as a whole clock, when the engine multiplies a fixed step, a time request and fast forward (`MissionState.cs:155-173`, `:196`), and when the flag has other writers. A test on the Seat's own key route cannot see any of this. The proof had to play the other writer, which `adopt_flag` now does.

## Why each agent missed these

- **The builder:** it worked from the contract's sentence for row 1 and from Row 105's press rule for row 3. It reported its wider choice for row 4.
- **The checker:** it read the code against the same contract, so it found the code met that sentence.
- **Lens 5 and lens 2:** both found row 1, and rated it MED. Codex rated it HIGH on the arithmetic. I took the higher rating, and that started the second wave.
- **First wave and row 4:** row 4 did not exist until the first round of fixes.

## Feedback memories to codify

None. The two lessons below carry the rules.
