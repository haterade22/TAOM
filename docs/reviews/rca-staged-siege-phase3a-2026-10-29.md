# RCA: Staged Siege (#1054) Phase 3a, custom battle sieges, deep review (2026-10-29)

**Summary.** Phase 3a lets a Custom Battle siege on a recorded scene run the same stages A to D, for either player side,
with no lord's hall after it: capturing D is a win for an attacking player and a loss for a defending one. It was
reviewed uncommitted in the worktree `E:\repos\taom-staged-siege` on top of Phase 2 (`92cd9bcd1`). Risk tier 1 (arming
gate and mission result path). Two lenses ran: data flow and engine compatibility; no XML changed. Codex was not
dispatched.

- **Severity:** 0 CRITICAL, 0 HIGH, 1 MEDIUM, 4 LOW (after merging the two lenses' overlapping findings).
- **Outcome:** all fixed (rows below); row 2 has no RED test, because an unmapped end cannot be built without a live
  mission. No fix re-review was run: row 1's fix repeats the hall cue's engine calls, which two Phase 2 engine reviews
  verified, and row 2 changes nothing for any existing end. In-game checks owed.

Feature doc: [staged-siege.md](../features/staged-siege.md). Earlier RCAs: [Phase 1](rca-staged-siege-2026-10-29.md),
[Phase 2](rca-staged-siege-phase2-2026-10-29.md).

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | A staged custom battle win (`AttackerWon`, `CreateSuccessful`) showed no "battle won" line and no "press Tab" hint: `BattleEndLogic` shows them only for a depleted or retreating enemy (BattleEndLogic.cs:106-111, :134-143), and the mission does not close by itself (Mission.cs:4850). Lenses: engine (MEDIUM), data flow (LOW). | Missing feedback on a TAOM-made end | Phase 2 fixed the same gap for the hall (row 5 of its RCA), but the new end path was written without asking what the player sees after each result. Repeat within the feature. | A walls-adapter announcement mirrors `AnnounceHallTaken` with vanilla's strings, fired once on the accepted `Successful` result; test that it fires once and only for that end. Lesson below. |
| 2 | LOW | `TryEnd` stopped the spawners before checking that the end maps to a result, so a future unmapped end would stall the battle. Lenses: data flow, engine. | Order of side effects | | Check first, then stop. |
| 3 | LOW | Stale docs: the overview, the stage line and the key files described only the campaign; the checklist did not name the wall level the points were recorded at. Lenses: data flow, engine. | Docs | | Corrected; the checklist names the level and tries another. |
| 4 | LOW | The MCM hint described only the campaign end. Lenses: data flow, engine (both left it to Mike because translations would go stale). | Doc | The 12 language rows are unpaid English seeds, so the wording costs nothing to correct now. | Reworded for both kinds of siege; seed rows reset to the new English. |
| 5 | LOW | The new binding pin asserted a namespace `typeof` already binds and repeated a `Campaign.Current` assert. Lens: engine. | Test noise | | Redundant asserts removed. |

## Root-cause pattern

**Every TAOM-made mission end needs its own player cue (row 1, and Phase 2 row 5).** When TAOM ends a battle through
`MissionEnded`, vanilla's end messages, cheer and auto-close do not run unless the enemy is depleted or fleeing. Each new
end path must say what the player sees and how he leaves.

## Why each lens caught or missed what it did

- **Engine compatibility** read `BattleEndLogic`'s message gates and the scoreboard (`CustomBattleScoreboardVM`), verified
  17 engine items (custom battle sally-outs are excluded by three checks; Patch126 applies in custom battle), and found
  rows 1 to 5.
- **Data flow** traced every consumer of `AttackerWon` and `HallFollows` and the custom battle path end to end, and found
  rows 1 to 4.

## Follow-ups

- FOLLOW-UP #1054: in-game custom battle runs on Ustokol Castle as attacker and defender (the end cue, the scoreboard result, the HUD without E, one other wall level).
