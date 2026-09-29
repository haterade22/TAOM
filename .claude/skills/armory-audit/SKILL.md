---
name: armory-audit
description: Use after any Armory sync, art drop, or "ARMORY ART DRIFT" at session start. Audits every mesh and body ref, joins breakage to troops, commits the report.
argument-hint: [check|regen]
---

# Armory reference audit

One command, one committed report, one verdict. The artists combine, rename, delete and add
meshes; the item XML keeps naming whatever it named before; a `body_name` the engine cannot
resolve is the #352 infinite mission load (#599 was the elf start, two days). This skill is the
thing to run after an Armory sync so that class is caught on the desk, not in a tournament.

**Full background:** [docs/features/armory-ref-audit.md](../../../docs/features/armory-ref-audit.md).

## When to invoke

- `session-start.sh` printed `ARMORY ART DRIFT` (a `*_geo.tpac` newer than the committed catalogue).
- After pulling or copying anything into `<game>/Modules/LOTRLOME_Armory` (KEYforce, Solus, a "TAOM Update" mirror commit, your own editor re-import).
- Before any battle or tournament smoke that follows art changes, and before `/release`.

**If the drop replaced textures, not just meshes:** this audit checks names, never layouts. A re-exported sheet
can keep every name and move the UV layout under it. Compare each replaced texture to the file already in
`AssetSources/` with
`magick compare -metric RMSE "(" integrated.png -resize "WxH!" ")" "(" new.png -alpha off ")" null:`; near zero
is the same layout on a different canvas, a large value means the mesh moved too. Canvas aspect on its own
proves nothing, because UVs are normalized ([module-armory.md](../../../docs/modding/module-armory.md)).

## Mode selection

- `$ARGUMENTS` = `check` or empty: audit and write the report, catalogue untouched.
- `$ARGUMENTS` = `regen`: audit, then rewrite `docs/reference/armory-catalogue/catalogue.tsv` so the next drift check starts from today.

## Step 1: run it

```bash
python tools/audit_armory_refs.py                    # check
python tools/audit_armory_refs.py --regen-catalogue  # regen
python tools/wire_hill_troll_race.py --check         # both modes: the only gate that catches an unkeyed troll clip on a melee-table code (the +0x6590B9 swing CTD)
```

About 15 s against the live install. Writes `docs/audits/armory-ref-audit.md` and prints
`Verdict: CLEAN|BROKEN`. Exit 1 on any `MISSING_BODY`, `MISSING_MESH`, dead `Item.<id>`, or a
deleted or renamed mesh the live XML still names. Generator drift is a WARNING with its `DRIFT:`
lines shown.

## Step 2: read the report, in this order

1. **Broken mesh and body refs**: each row names the asset, the item, the file:line, and every troop, lord, roster or config carrying it. A `MISSING_BODY` row hangs every mission that preloads a carrier; the player character is always first in a tournament's preload set.
2. **Catalogue drift**: a deleted or renamed mesh still named by an item is tomorrow's row.
3. **New art nothing uses**: what the artists shipped that no item wears yet (a signal for the roster authors, not a fault).
4. **Dead item ids** and **generators**.

## Step 3: repair the REF side, never the asset side

Repoint `mesh` / `body_name` / holster refs to the art that ships (byte-exact edits, both the live
Armory and the `E:\repos\lotraom-assets` mirror; the #599 shape is `E:\taom-hang-2026-09-15\repoint_refs.py`).
Do not restore a tpac beside its replacement. If the mapping is ambiguous (three old flags, four
new), pick by index, leave a comment, and hand the heraldry question to the artist. Then re-run
with `regen`, and `git diff docs/audits/armory-ref-audit.md` is the change log.

Then regenerate the armour acquisition class table, which the game reads to gate markets, loot and
the armoury: `python tools/generate_armour_classes.py --check`, and if it is stale, `--apply`.

## Step 4: commit the audit files and the class table

`docs/audits/armory-ref-audit.md`, `docs/reference/armory-catalogue/catalogue.tsv` and, when step 3
regenerated it, `Main/_Module/ModuleData/armour_acquisition/armour_classes.xml` go in the same commit as
the ref repair. The Armory XML itself is unversioned here; say in the commit body which files changed
in the live install and the mirror.

## Gotchas

- The catalogue diff's own "REFERENCED, will break" flag is the flag at the last regen, not today's; the audit re-derives it from the live XML. Trust the audit's count.
- `validate_mesh_refs.py --unreferenced` matches case-insensitively and reported the new elven bows as used when nothing used them; the audit's "new art nothing uses" is an exact match.
- `validate_moduledata.py` now carries the same body check as `MISSING_COLLISION_BODY` (ERROR) and `MISSING_VISUAL_MESH` (WARNING), so the commit hook blocks on it too (since #622: until 2026-09-18 `main()` never called the pass; the MCP tool and `/verify` still do not run it, #623). The audit is still the only place the troops are joined in.
- A body that RESOLVES can still be borrowed, and this audit cannot see that (#633): three Rhun longbows carried the elven bow's `bo_wm_elven_bow_a03` and every ref check here read CLEAN. After an art drop also run `python tools/validate_moduledata.py --code COLLISION_BODY_BORROWED`, which errors on a body that is provably another kit's twin (same-kit sharing and the Rhun family exempt). The audit does not run that pass itself yet.
- The player release under `E:\LOTRAOM_Releases\dev` ships its own copy of the Armory; a repair here reaches players only through `/release`.
