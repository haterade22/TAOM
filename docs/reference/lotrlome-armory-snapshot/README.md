# LOTRLOME_Armory snapshot

Reference copies of LOTRLOME_Armory's race-defining XML files. **Not registered or loaded by TAOM.** Storage only — for reference and to preserve edits we've applied to LOTRLOME's working copy in the Steam install.

## Why this exists

TAOM depends on `LOTRLOME_Armory` (the LOTR armor/items module) for dwarf/uruk/orc/elf monster definitions, skeletons, and animation sets. We've patched LOTRLOME_Armory's `action_sets.xml` directly in the Steam install (e.g., adding Bannerlord 1.3 action-type aliases for CC parent animations — see CHANGELOG entry "Fix: CC parent agents not rendering for custom-race cultures" 2026-05-04). Those edits live at:

```
E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\
```

Any future Steam-Workshop or manual update of `LOTRLOME_Armory` will overwrite our edits silently. This snapshot is the safety net — copy these files back into the Steam install if a LOTRLOME update breaks something we previously fixed.

## Files

| File | Purpose | Size |
|---|---|---|
| `action_sets.xml` | All LOTRLOME race action sets (combat / facegen / villager / etc.). **Includes the 1.3 action-type aliases** added 2026-05-04 across all 12 pre-existing facegen sets, **plus** the new `as_elf_facegen` + `as_elf_female_facegen` action_sets authored 2026-05-22 (see CC parent fix checklist below). | ~3.7 MB |
| `monsters.xml` | LOTRLOME monster definitions (dwarf, uruk, nazghul, orc, etc.) and their skeleton bindings | ~63 KB |
| `skins.xml` | Race-to-skeleton mapping, body proportions, mesh slot configurations | ~5.3 MB |

## How to restore from this snapshot

If a LOTRLOME update overwrites the working copies and breaks something we previously fixed:

```bash
cp "docs/reference/lotrlome-armory-snapshot/action_sets.xml" \
   "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml"
```

Same for `monsters.xml` and `skins.xml` if needed. Then delete the compressed shader-cache sacks (see `feedback_shader_cache_invisible_cc.md` memory) and re-launch — Bannerlord will re-cook shaders and the action-set edits will take effect.

## Loading status — DO NOT REGISTER

These files **must not** be referenced by `Main/_Module/SubModule.xml` or moved into `Main/_Module/ModuleData/`. Bannerlord auto-loads `<module>/ModuleData/{action_sets,monsters,skins}.xml` from every enabled module's root, so duplicating these into TAOM's ModuleData would cause double-load conflicts (same IDs in two modules) — exactly the dead-duplicate state we cleaned up on 2026-05-04 (commit `307df40`).

## CC parent fix — required action_sets checklist

The Character Creation parent menu renders both parents with race-specific skeleton + animations via an engine lookup of `as_<race>_facegen` (male) and `as_<race>_female_facegen` (female). If either ID is missing for any race that TAOM cultures consume, the parents render as a contorted / T-pose mesh (see screenshot in CHANGELOG 2026-05-22 entry).

After restoring from this snapshot — or after any LOTRLOME update — `action_sets.xml` MUST contain BOTH the male and female facegen entries for every race below:

| Race | Required `_facegen` entries | Source of CC parent anims |
|---|---|---|
| `berserker` | `as_berserker_facegen`, `as_berserker_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `cave_troll` | `as_cave_troll_facegen`, `as_cave_troll_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `dg_uruk` | `as_dg_uruk_facegen`, `as_dg_uruk_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `dwarf` | `as_dwarf_facegen`, `as_dwarf_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| **`elf`** | **`as_elf_facegen`, `as_elf_female_facegen`** | **TAOM-authored 2026-05-22 — base_set=`as_human_warrior` (elf monster uses human skeleton)** |
| `goblin` | `as_goblin_facegen`, `as_goblin_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `orc` | `as_orc_facegen`, `as_orc_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `pale_uruk` | `as_pale_uruk_facegen`, `as_pale_uruk_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `uruk` | `as_uruk_facegen`, `as_uruk_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |
| `uruk_hai` | `as_uruk_hai_facegen`, `as_uruk_hai_female_facegen` | LOTRLOME pre-2026-05-04 + 1.3 aliases |

The races `hill_troll`, `nazghul`, and `saruman` also have `_facegen` entries in LOTRLOME but are not consumed by any TAOM culture; they're listed here only so a future re-snapshot doesn't accidentally drop them.

**Quick sanity check after any restore** (run from a shell with grep):
```bash
grep -oE 'id="as_[a-z_]+_facegen"' "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/action_sets.xml" | sort -u
```
Output should contain `as_elf_facegen` and `as_elf_female_facegen`. If either is missing, the snapshot got partially overwritten — re-apply the elf block from the snapshot file directly.

**Each facegen action_set must contain ALL of the following directly declared** — the engine does NOT fall through `base_set` inheritance for these action types, so they must live in the facegen action_set itself, not in its base:

1. **14 CC parent action types** — 7 × Bannerlord 1.2 names (`act_character_creation_<gender>_default_0..6`) AND 7 × Bannerlord 1.3 named slots (`_default_standing`, `_side_to_side_1`, `_mother_front`, `_father_sitting`, `_side_to_side_2`, `_side_to_side_3`, `_hugging`). The 2026-05-04 commit (`307df40`) added the 1.3 aliases to the 12 pre-existing facegen sets but did not author the missing `as_elf_facegen` pair; the 2026-05-22 fix closes that gap.
2. **7 `act_character_creation_toddler_0..6` actions** — mapped to `anim_toddler_0..6`.
3. **All `act_childhood_*` actions used by the post-parent narrative stages** (~60 of them: `tough`, `decisive`, `ready`, `leader`, `athlete`, `memory`, `numbers`, `manners`, `animals`, `streets`, `peddlers[_2]`, `militia`, `schooled`, `apprentice`, `fierce`, `sharp`, `vibrant`, `fox`, `gracious`, `clever[_2]`, `book`, `arms[_2]`, `artisan`, `polearm`, `roguery`, `spotting`, `manners_2[_3]`, `closed_tutor`, `confident_tutor`, `confident2_tutor`, `demure_tutor`, `hip_tutor`, `ready_bow`, `guard_up_staff`, `spoiled`, `explorer[2]`, `play[_2]`, `genius`, `honor`, `hardened`, `grit`, `defend`, `focus`, `strenght`(sic), `leader_2`, `contemplate`, `tactician`, `appearances`, `sceptic`, `ready_throw`, `riding_2`). Cross-reference LOTRLOME's `as_dwarf_facegen` (lines ~16812-17134 of `action_sets.xml`) for the authoritative list — when creating a new race facegen, copy that block verbatim.
4. **8 `act_childhood_toddler_*` actions** — `sleep`, `vigor`, `social`, `endurance`, `intelligence`, `control`, `cunning`, `tantrum`.
5. **Inventory / banner-editor / stand / sit poses** — `act_inventory_idle[_start]`, `act_visual_test_morph_animation`, `act_command`, `act_walk_idle_unarmed`, `act_stand_1`, `act_stand_2`, `act_sit_1`.
6. **12 rider/horse story-background actions** — `act_rider_story_background_1..6` and `act_horse_story_background_1..6`.

**Warning — slim facegen entries are insufficient.** A facegen action_set that declares only the 14 CC parent action types (relying on `base_set="as_human_warrior"` or similar to inherit childhood / toddler / inventory actions) WILL render the parent menu correctly but break **every subsequent CC stage** (Early Childhood lying-down pose, Youth / Adolescence / Adulthood narrative options without any character animation). This was caught on 2026-05-22 after the first in-game test of the slim elf entries; the v2 fix replaced them with verbatim copies of LOTRLOME's `as_dwarf_facegen` / `as_dwarf_female_facegen` blocks (with only `id` and `base_set` renamed). See memory `feedback_lotrlome_action_set_aliases.md` 2026-05-22 addendum for the "declare everything, don't trust inheritance" rule.

**Important rule** carried over to memory `feedback_lotrlome_action_set_aliases.md`: when fixing CC parent rendering, the recipe must both (a) **patch** existing facegen action_sets with 1.3 aliases AND (b) **create** missing facegen action_sets for any race in TAOM cultures that LOTRLOME's authors never anticipated as a playable race. Patching alone is not enough.

## Snapshot date

2026-05-22 — `action_sets.xml` re-snapshotted with elf CC parent entries appended.
Previous snapshot: 2026-05-04 — initial snapshot with the 1.3 action-type alias edits across the 12 pre-existing facegen sets.

If you re-snapshot later (e.g., after a LOTRLOME update we want to track), bump this date and note any changes vs. the previous snapshot.
