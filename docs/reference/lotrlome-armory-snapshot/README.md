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
| `action_sets.xml` | All LOTRLOME race action sets (combat / facegen / villager / etc.). **Includes the 1.3 action-type aliases** added 2026-05-04 across all 12 facegen sets (dwarf, dwarf_female, orc, orc_female, uruk, uruk_female, uruk_hai, uruk_hai_female, berserker, nazghul, dg_uruk, etc.) | ~3.7 MB |
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

## Snapshot date

2026-05-04 — taken from a Steam install with the CC-parent-animation 1.3 alias edits applied.

If you re-snapshot later (e.g., after a LOTRLOME update we want to track), bump this date and note any changes vs. the previous snapshot.
