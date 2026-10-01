# RCA: deep review of the #700 Load Game preview fix (2026-10-01)

**Change:** the Patch55 prefix on `BasicCharacterTableau.RefreshCharacterTableau` calls
`ActionIndexCacheRepair.TryEnsureRepaired("basic-tableau-refresh")` first, so the main-menu Load Game
preview no longer shows the rider in bind pose (#700). The incident itself is the 2026-10-01 addendum
in [rca-prone-character-tableau-2026-07-31.md](rca-prone-character-tableau-2026-07-31.md).

**Review:** `/deep-review`, six lenses in two waves (Standards, Engine compatibility, Completeness, Data
flow; then Efficiency, Design). No lens found a defect in the code change. Every confirmed finding in
the diff was in the documentation written for it, plus one missing test; four confirmed findings sit
in unchanged code and are follow-ups.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | The RCA addendum and a new lesson said the reader was unknown; the 2026-08-01 lesson already named `BasicCharacterTableau` as a reader with no backstop | docs accuracy | The new lesson was written from the incident without reading the category file first | Folded into the existing entry as a **Recurred** bullet; the existing rule "read the category FILE before touching a subsystem" covers it |
| 2 | MED | The docs restated the 2026-08-01 claim that `ActionIndexCache` has an explicit cctor and is not `beforefieldinit`; reflection on the installed v1.5.3 DLL says `BeforeFieldInit` | engine claim | The original analysis read the flag from ILSpy's C# view, which prints a `static Type()` block for a `beforefieldinit` type too; this change copied the claim forward | Correction bullet on the cctor lesson in `lessons/animation-skeleton.md`; RCA addendum records it |
| 3 | LOW | "Poisoned on every launch": 28 of 29 retained v1.5.3 sessions that reached game init, one healthy (2026-09-27 18:08) | evidence wording | Six recent logs were read and generalised to all | Reworded with the count; `evidence-over-claims.md` C already covers it |
| 4 | LOW | `hero-race.md` row: "all 215", "prefixes" for a postfix, "earlier" attempt no longer earliest, "can never trigger the initialisation" | docs accuracy | The row was patched with a clause instead of re-read whole | Row rewritten |
| 5 | LOW | The addendum sat between the 2026-08-01 addendum and "Open thread (CLOSED, see addendum above)", breaking that pointer | docs structure | Inserted by position, not chronology | Moved before `## Related` |
| 6 | LOW | No test pinned the new call or its order before the guard's early return | test coverage | Entry points need no tests under ADR-008, so none was written | `HeroRaceWiringTests.Patch55Prefix_RunsActionIndexCacheRepairAsFirstStatement`, seen RED with the call moved below the return |

## Root-cause pattern

Findings 1 to 5 share one cause: documentation written for the change and not re-read against its
source. A claim was copied forward from an older RCA (2), a count was generalised from a few logs (3),
a table row was patched by clause instead of re-read whole (4), a lesson was written without reading
the category file it joined (1), and an addendum was placed by position rather than chronology (5).
The code change was one reviewed line; the prose around it carried the risk. Each fix was to read the
source the sentence depends on (the DLL, every log, the whole row, the category file, the document's
order) before writing it, which `evidence-over-claims.md` C1 already requires.

## Follow-ups (unchanged code, not applied here)

- `ActionIndexCacheRepair.KnownNameOverrides` misses the v1.5.3 renames `act_wreckage_death_01/02`
  (from `act_cutscene_main_hero_battle_death_01/02`), so both stay `-1`; their reader is
  `SandBox.View.Map.Visuals.BattleWreckageVisual` (map battle-site corpses, effect UNVERIFIED in game).
  No gate diffs the installed cctor against the map after an engine bump.
- `ActionIndexCacheRepair.cs` comments repeat the "explicit cctor" premise (finding 2).
- `SubModule.cs:1553-1554` calls `CharacterSpawnerService` the backstop; it never reads the statics.
  Single-owner file.
- `RepairFields` looks each action up twice (`GetActionCodeWithName`, then `Create`); one is redundant.
- The root cause: what initialises `ActionIndexCache` before action types load on most v1.5.3 launches.

## Why each lens's scope fit

The code change was one delegation into an existing, reviewed helper, so the defect lenses (1, 2, 5)
found nothing in it and spent their depth on the callee and the docs, which is where findings 1 to 5
came from. Finding 6 came from Completeness and Design. No lens missed something another caught in
the diff.

## Feedback memories to codify

None. Findings 1 and 3 are covered by existing rules (read the lessons category file first; evidence
over claims); finding 2's rule is now in the cctor lesson.
