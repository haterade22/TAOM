# RCA: Staged Siege (#1054) Phase 2, deep review (2026-10-29)

**Summary.** Phase 2 adds two things to the staged siege: Creature Siege Role yields its trolls to the stage plan in the
stages where TAOM steers a side, and the lord's hall gets point E (its last fight area), whose capture wins the hall. It
was reviewed uncommitted in the worktree `E:\repos\taom-staged-siege` on top of Phase 1 (`77306cf6b`). Risk tier 1
(mission-tick behaviour, a new mission result path, engine calls). Three lenses ran in one wave (data flow, engine
compatibility, XML), then data flow and engine compatibility re-reviewed the fixes. Codex was not dispatched.

- **Severity:** 0 CRITICAL, 0 HIGH, 3 MEDIUM, 11 LOW in the first review; the fix re-review added 7 LOW.
- **Decisions by Mike:** point E arms from the saved siege state (row 1); the player's orders win for his own troll
  formations, as for siege engines (row 2).
- **Outcome:** every finding fixed with tests except row 14 (kept by decision). In-game checks owed.

Feature doc: [staged-siege.md](../features/staged-siege.md). Phase 1 RCA: [rca-staged-siege-2026-10-29.md](rca-staged-siege-2026-10-29.md).

## Findings

Severity is the finder's. Each MEDIUM was re-checked against the installed v1.5.6 DLLs (taom-src cache).

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | Point E armed only from an in-memory walls-end record that the first hall mission used up, while vanilla keeps the settlement `InTheLordsHall` in the save and through a retreat (`CampaignSiegeStateHandler.cs:30`, `:45-48`; reset only by `FinalizeSiegeEvent`, `Settlement.cs:1404-1406`). A retreat and re-attack, a reload or a restart gave a vanilla hall. Lens: data flow. | In-memory hand-off beside saved state | The spec paired the hall with the previous mission through memory and never asked which state the engine saves for the same fact. | Mike chose: arm from the saved state (`InTheLordsHall`, the settlement's walls scene recorded, the switch on); the hand-off plumbing was deleted. Lesson in `lessons/state-lifecycle-save.md`. |
| 2 | MEDIUM | The new staged release also freed troll formations the player commands (it ran before the player-command rules D7 and D8, and the stage tactic steers only AI-controlled formations), so a player general's Charge no longer struck the gate in stage A and nothing steered those trolls. Lenses: data flow, engine. | Takeover rule without the player carve-out | The spec wrote the rule for the stage plan's own formations and did not carry over the "player's orders win" principle decided the same day for siege engines (Phase 1 row 27). | Release only AI-controlled formations; tests for the player's Charge (strikes the gate) and other orders. Lesson in `lessons/misc.md`. |
| 3 | MEDIUM | `Patch125` was already taken on trunk by `Patch125_HideoutClanGuard` after this branch forked; a merge would fail `PatchCategoryNumberTests` and conflict in the registry. Lens: engine. | Number reserved at spec time, taken by a parallel branch | The spec checked trunk and every worktree when it was written; a parallel session landed 125 later. | Renumbered to `Patch126_StagedSiege` after a grep of trunk, every worktree, every branch head and the registry. Lesson in `lessons/build-tooling-workflow.md`. |
| 4 | LOW | The hall adapter read `PlayerEncounter` (which dereferences `Campaign.Current` unguarded, `PlayerEncounter.cs:124`) before the `NoCampaign` guard. Lenses: data flow, engine. | Guard order | The walls adapter had the guard; the hall adapter was written fresh. | `Campaign.Current` guard on every campaign read. |
| 5 | LOW | A TAOM hall win showed no vanilla win cue (`BattleEndLogic` shows its own only for a depleted enemy). Lenses: data flow, engine. | Missing feedback | | `AddQuickInformation` plus vanilla's "press Tab" line, built as `BattleEndLogic.cs:110-111` does. |
| 6 | LOW | No hall result log line. Lens: data flow. | Diagnostics | | `[StagedSiege] hall result ready: ...`. |
| 7 | LOW | `StagedSiegeSlot.HasHallArmed` had no production reader. Lens: data flow. | Dead member | | Deleted. |
| 8 | LOW | No test pinned that the next mission's `ClearArmed` keeps `LastEnded`. Lens: data flow. | Unpinned invariant | | `ClearArmed_KeepsLastEnded`. |
| 9 | LOW | The Phase 1 lesson told a tactic to ask `Team.IsPlayerGeneral` before stopping machines, the guard Phase 1 row 27 disproved. Lens: engine. | Wrong lesson text | The lesson was written before row 27's fix. | Corrected to the per-side test. |
| 10 | LOW | A comment claimed `BattleEndLogic` ends a depleted hall "this tick". Lens: engine. | Comment | | Reworded to its once-a-second check. |
| 11 | LOW | The doc said the owner change was not traced. Lens: engine. | Doc | | The vanilla chain from `OnMissionResultReady` to `ChangeOwnerOfSettlementAction.ApplyBySiege` is in the doc; the in-game check stays owed. |
| 12 | LOW | The MCM hint did not mention point E. Lens: XML. | Doc | | One clause added; the 12 seeded rows reset to the new English. |
| 13 | LOW | `LastEnded` from an older walls fight was not cleared when a new walls siege armed. Found by the orchestrator in the builder's notes. | Stale state | | Cleared on arming; test `TryArm_AllPass_ClearsAStaleWallsEnd`. Moot for arming after row 1, kept for the inert log. |
| 14 | LOW | "The lord's hall is taken." is the only objective line with a period. Lens: XML. | Wording | | NOT APPLIED by decision: the period suits the chat-log line that shares the key. |
| 15 | LOW | Fix re-review: the hall read the scene catalog before checking the switch, so a hall fight with the switch off loaded `staged_siege.json`. Lens: data flow. | Toggle does not gate I/O | Repeat of rule 5d (toggles gate I/O) in `.claude/rules/csharp-architecture.md`. | Catalog read only after every other guard passes; RED-then-GREEN test `TryArm_SwitchOff_NeverLoadsTheCatalog`. |
| 16 | LOW | Fix re-review: three citations named the sally-out opener (`PlayerEncounter.StartSiegeAmbushMission`) instead of the assault (`PlayerSiege.StartSiegeMission`, `PlayerSiege.cs:171-172`). Lenses: data flow, engine. | Citation | The builder found one caller and cited it. | Assault cited first, the sally-out site second. |
| 17 | LOW | Fix re-review: the corrected lesson's second half said `Team.IsPlayerGeneral` suffices for a team's own formations; it reads true on every team but the player's (`Team.cs:213`, `Mission.cs:739-745`). Lens: engine. | Wrong lesson text (second pass) | Row 9's correction was written without reading the flag's writers. | Now says to ask `Formation.IsAIControlled`. |
| 18 | LOW | Fix re-review: the registry said a game run confirms the settings write; none has yet. Lens: engine. | Claim ahead of evidence | | "Verified by IL; owed in game." |
| 19 | LOW | Fix re-review: a Creature Siege Role changelog line still described the pre-row-2 rule. Lenses: data flow, engine. | Stale doc | | Corrected. |
| 20 | LOW | Fix re-review: the doc did not say that surviving defenders keep fighting after a capture and that leaving is refused within 5 m of one. Lens: data flow. | Doc | | Added to the hall section and the checklist. |
| 21 | INFO | Merge: the registry and the 12 language files conflict with trunk at their tails. Lenses: engine, XML. | Merge note | | Keep both sides at merge time; rerun the localization tests and `validate_xml_schemas.py`. |

## Root-cause patterns

**Ask what the engine saves before carrying state yourself (row 1).** The design kept an in-memory record of a fact the
campaign already saves, and the two drifted apart on the first retreat. A feature that follows campaign state reads the
saved state at the moment it acts.

**A new takeover rule inherits the player carve-out (rows 2, and Phase 1 rows 1 and 27).** Every time the staged siege
took control of something on the player's side (siege engines, then trolls), the first version forgot that the player may
command it. The rule is now one principle in the feature doc: TAOM steers only what the AI controls.

**Lessons need the same evidence as code (rows 9, 17).** A lesson written from a fix in progress was wrong twice. Both
corrections came from the engine lens reading the flag's writers.

## Why each lens caught or missed what it did

- **Data flow** found rows 1, 2, 4 to 8, 15, 16, 19, 20 by tracing the record and the rules into the engine's saved state
  and the role service.
- **Engine compatibility** found rows 2, 3, 9 to 11, 16 to 19, verified 37 engine items in the first pass and 23 in the
  re-review, all compatible.
- **XML** ran its gates (0 failures in scope) and found rows 12 and 14, plus the merge note.
- **Standards, efficiency, completeness, design:** skipped by tier.

## Follow-ups

- FOLLOW-UP #1054: in-game checks for point E (arms after a retreat, a reload and a restart; the flag and HUD; the win cue; the settlement changes owner; leaving near a defender is refused).
- FOLLOW-UP #763: paid translation of the staged siege keys (all English seeds).
