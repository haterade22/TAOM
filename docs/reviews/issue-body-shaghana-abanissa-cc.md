## Problem

`shaghana` and `abanissa` are CC-selectable cultures (registered in `Main/_Module/ModuleData/charactercreation/cultures.json`) but have **zero entries** across all 5 narrative menu JSONs:

- `parents_menu.json`: 0 entries for either culture
- `childhood_menu.json`: 0 entries
- `education_menu.json`: 0 entries
- `youth_menu.json`: 0 entries
- `adulthood_menu.json`: 0 entries

When a player picks shaghana or abanissa at the culture-selection step, vanilla CC then renders an empty narrative page (Family / Childhood / Education / Youth / Adulthood). Vanilla CC throws/crashes on `SelectedOptions[CurrentMenu]` when the player tries to advance past an empty selection page.

**Net effect:** these two cultures appear playable at the culture-selection step but the player cannot complete character creation. The startup-resources config (gold + influence + playerGold) added in #110 is functionally dead for these cultures because finalize is unreachable.

## Found by

Codex Phase 3 self-review (2026-05-06) of fixes from #110. Trace-step:
- `cultures.json:143` registers shaghana with starting settlement `town_A6`
- `cultures.json:153` registers abanissa with starting settlement `town_A14`
- `grep -cE '"culture_id":\s*"shaghana"' Main/_Module/ModuleData/charactercreation/*_menu.json` → 0/0/0/0/0
- `grep -cE '"culture_id":\s*"abanissa"' …` → 0/0/0/0/0

Issue #110 added these two cultures to `startup_resources_config.xml` based on the assumption that they were full peer kingdoms (which they are — see `taom_spkingdoms.xml`, 9 + 8 NPC lords). The gold/influence config for the NPC lords + clans is correct and useful at new-game start. The `playerGold` rows are correct in shape but unreachable in practice until narrative coverage lands.

## Design options (pick one)

### Option A: Author full narrative menu coverage (recommended for "kingdoms are playable")

For each of `parents_menu.json` / `childhood_menu.json` / `education_menu.json` / `youth_menu.json` / `adulthood_menu.json`, add `culture_id="shaghana"` and `culture_id="abanissa"` option entries following the same shape as existing custom cultures (gondor, mordor, erebor, rivendell, etc.). Reference docs/features/character-creation.md for the schema and existing examples.

For youth options specifically, also need matching equipment rosters in `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml` for every `(culture, title_type, gender)` combination — see #110 for the convention.

Effort: hours. Substantial content authoring.

### Option B: Hide shaghana/abanissa from CC selection (smallest scope)

Remove the entries from `cultures.json` so the culture step doesn't show them. Keeps them as full NPC kingdoms (lords/clans/settlements still work) but not selectable as a player culture. Equivalent to "they exist as factions in the world but not as starting cultures for the player."

Effort: 30 seconds.

### Option C: Add a safe fallback for empty culture-filtered menus

Modify `NarrativeMenuBuilder` (or the registration handler) to detect cultures with zero options and either fall back to a vanilla aserai option set (lore-adjacent) or inject a placeholder "wandered the desert" option that doesn't crash. Lower effort than A but more invasive than B.

Effort: small. Mid-confidence (need to verify vanilla doesn't crash on a single-option-list).

## Recommendation

Whoever owns the shaghana/abanissa kingdoms should pick. If the goal is "fully playable" → Option A. If "NPC kingdoms only, not for player" → Option B. If "playable but stub-grade content" → Option C.

## Cross-references

- Closes the gap left by #110 (player startup gold for these cultures unreachable)
- Codex Phase 3 review file: `docs/reviews/codex-adversarial-player-startup-fixes-2026-05-06.md`
- RCA: `docs/reviews/rca-player-startup-2026-05-06.md`
