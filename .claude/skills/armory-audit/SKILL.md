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

## Mode selection

- `$ARGUMENTS` = `check` or empty: audit and write the report, catalogue untouched.
- `$ARGUMENTS` = `regen`: audit, then rewrite `docs/reference/armory-catalogue/catalogue.tsv` so the next drift check starts from today.

## Step 1: run it

```bash
python tools/audit_armory_refs.py                    # check
python tools/audit_armory_refs.py --regen-catalogue  # regen
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

## Step 4: commit both files

`docs/audits/armory-ref-audit.md` and `docs/reference/armory-catalogue/catalogue.tsv` go in the
same commit as the ref repair. The Armory XML itself is unversioned here; say in the CHANGELOG
which files changed in the live install and the mirror.

## Gotchas

- The catalogue diff's own "REFERENCED, will break" flag is the flag at the last regen, not today's; the audit re-derives it from the live XML. Trust the audit's count.
- `validate_mesh_refs.py --unreferenced` matches case-insensitively and reported the new elven bows as used when nothing used them; the audit's "new art nothing uses" is an exact match.
- `validate_moduledata.py` now carries the same body check as `MISSING_COLLISION_BODY` (ERROR) and `MISSING_VISUAL_MESH` (WARNING), so the commit hook, the MCP tool and `/verify` see it too. The audit is still the only place the troops are joined in.
- The player release under `E:\LOTRAOM_Releases\dev` ships its own copy of the Armory; a repair here reaches players only through `/release`.
