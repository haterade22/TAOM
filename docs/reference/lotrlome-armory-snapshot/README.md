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

**Each facegen action_set must contain all 14 CC parent action types** — 7 × Bannerlord 1.2 names (`act_character_creation_<gender>_default_0..6`) AND 7 × Bannerlord 1.3 named slots (`_default_standing`, `_side_to_side_1`, `_mother_front`, `_father_sitting`, `_side_to_side_2`, `_side_to_side_3`, `_hugging`). The 2026-05-04 commit (`307df40`) added the 1.3 aliases to the 12 pre-existing facegen sets but did not author the missing `as_elf_facegen` pair; the 2026-05-22 fix closes that gap.

**Important rule** carried over to memory `feedback_lotrlome_action_set_aliases.md`: when fixing CC parent rendering, the recipe must both (a) **patch** existing facegen action_sets with 1.3 aliases AND (b) **create** missing facegen action_sets for any race in TAOM cultures that LOTRLOME's authors never anticipated as a playable race. Patching alone is not enough.

## Snapshot date

2026-05-22 — `action_sets.xml` re-snapshotted with elf CC parent entries appended.
Previous snapshot: 2026-05-04 — initial snapshot with the 1.3 action-type alias edits across the 12 pre-existing facegen sets.

If you re-snapshot later (e.g., after a LOTRLOME update we want to track), bump this date and note any changes vs. the previous snapshot.
