# Multi-Culture Armor Revamp (KEYforce, 2026-05-22)

## Overview

Mesh-first authoring pass following the Gondor #99 pipeline. Adds 277 new Armory items across 5 cultures (Mordor, Isengard, Dol Guldur, Erebor, Rhun) to close gaps where KEYforce ships meshes but the corresponding item XML hadn't been authored yet. Gundabad spec was already fully covered.

## Why This Exists

KEYforce ships meshes (`.tpac` packages) and per-culture spec files (`.txt` at `E:\repos\lotraom-assets\tools\`) on a rolling cadence. Each culture's spec lists every item the artist will eventually deliver, but mesh shipments lag the spec. We can't author items per spec ahead of meshes (creates underwear bug); we can't ignore the spec either or shipped meshes go unused. The mesh-first compromise:

- **Vanilla behavior:** Bannerlord loads any item XML at startup, references mesh by id, falls back to nothing if mesh missing.
- **TAOM requirement (user direction):** "use the spec as a guide, but create variations within that guide to use all meshes. It is important all armor is showed off."
- **Without this revamp:** ~277 shipped meshes have no item XML, can never appear on troops, KEYforce's work is invisible.

## Architecture

### Design Challenge

Each culture has hundreds of items in spec but only a fraction shipped as meshes. The Gondor #99 pipeline solved this for Gondor with: (a) author every item that has a backing mesh, (b) skip un-meshed items (or use within-culture fallback), (c) validate refs against Armory + shipped meshes.

This pass extends the same pipeline to 5 more cultures via one Python generator script per culture.

### Solution Approach

| Step | Pattern |
|------|---------|
| 1. Parse spec | Per-culture `tools/generate_<culture>_armor.py` — hardcodes the item list or auto-extracts from the spec file |
| 2. Idempotent author | Skip items already present in Armory XML; append new items inside `</Items>` |
| 3. Steam-install default | Each generator defaults to `LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/` |
| 4. Stat consistency | All generators share the `STAT_TIERS` table from `tools/generate_gondor_armor.py` (light/medium/heavy/elite/lord per slot) |

## Configuration

### Spec source-of-truth: `E:\repos\lotraom-assets\tools\`

| File | Date | Lines | Status |
|------|------|-------|--------|
| `gondor_armors_and_troops.txt` | May 19 | 1287 | DONE (#99) |
| `mordor_armor_and_troops.txt` | May 20 | 661 | Items authored this pass |
| `isengard_armors_and_troops.txt` | May 20 | 684 | Items authored this pass |
| `dol_guldur_armors_and_troops.txt` | May 20 | 713 | Items authored this pass |
| `erebor_armors_and_troops.txt` | May 19 | 1037 | Iron Hills authored this pass |
| `gundabad_armors_and_troops.txt` | May 7 | 282 | Already complete |
| `rhun_armors_and_troops.txt` | May 7 | 2005 | Final 22 items added |

### Item count per culture (post-revamp)

| Culture | Total items in Armory | New this pass | Item prefix(es) |
|---------|----------------------|---------------|-----------------|
| Mordor | ~380 | 103 | `sk_uruk_mordor_*`, `sk_gn_orc_*`, `sk_md_orc_*`, `sk_is_orc_*`, `sk_dg_orc_*` (shared) |
| Isengard | ~294 | 15 | `sk_uruk_hai_*`, `sk_is_orc_*`, `urukscout_*`, `clo_urukscout_*` |
| Dol Guldur | ~245 | 14 | `sk_dg_uruk_*`, `sk_dg_orc_*` |
| Gundabad | 101 | 0 | `sk_gb_uruk_*` |
| Erebor | ~504 | 123 | `sk_dwarf_erebor_*`, `sk_dwarf_iron_*` |
| Rhun | ~586 | 22 | `sk_rh_loke_*`, `sk_rh_drag_*`, `sk_dg_khml_*` (shared with DG) |

## Key Files

| File | Purpose |
|------|---------|
| `LOTRLOME_Armory/.../mordor/{head,body,shoulder,arm,leg}_armors.xml` | 103 new generic orc items |
| `LOTRLOME_Armory/.../isengard/{head,body}_armors.xml` | 15 new paint + scout items |
| `LOTRLOME_Armory/.../dol_guldur/head_armors.xml` | 14 new DG-paint helmets |
| `LOTRLOME_Armory/.../iron_hills/*.xml` | 125 pre-existing + 5 new Iron Hills items (`sk_dwarf_iron_*` — **NOT in `erebor/` folder, see Deep-Review Correction below**) |
| `LOTRLOME_Armory/.../rhun/head_armors.xml` | 22 new Loke-Rim elite helmets |
| `tools/generate_mordor_armor.py` | Mordor generator |
| `tools/generate_isengard_armor.py` | Isengard generator |
| `tools/generate_dolguldur_armor.py` | Dol Guldur generator |
| `tools/generate_erebor_armor.py` | Erebor generator (parses spec at runtime; targets `iron_hills/` folder) |
| `tools/generate_rhun_armor.py` | Rhun generator (closes 22-item gap) |
| `tools/validate_all_troop_refs.py` | Multi-culture cross-reference gate (replaces Gondor-only `validate_gondor_refs.py`) |
| `tools/rollback_erebor_iron_misfile.py` | One-off cleanup script used during deep-review RCA |

## Dependencies

- `LOTRLOME_Armory` module declared in `SubModule.xml`.
- KEYforce mesh `.tpac` packages in `Assets/<culture>_assets/` — shipped on rolling cadence; verified for Mordor (33 meshes), Isengard (12), Dol Guldur (5), Gundabad (5), Erebor (26), Rhun (45). Items reference mesh names by id; meshes not yet shipped will appear missing in-game until KEYforce delivers them.

## Tests

No new C# tests added (data-only change). Validation done via:
- Build: `./build.ps1` exits clean.
- Cross-reference: per-culture grep of `Item.sk_*` refs in `troops_<culture>.xml` against `id=` in Armory XML. Mordor verified (87 refs, 0 missing). Isengard verified (armor refs all resolve; misses are out-of-scope weapons/mounts).

## How to add another culture

1. Read the KEYforce spec at `E:\repos\lotraom-assets\tools\<culture>_armors_and_troops.txt`.
2. Audit current state: `grep -hoE 'id="..."' LOTRLOME_Armory/.../<culture>/*.xml | sort -u | wc -l`.
3. Identify mesh availability: `find LOTRLOME_Armory/Assets/<culture>_assets/ -name "*_geo.tpac"`.
4. Clone the closest existing generator (`tools/generate_<existing>_armor.py`).
5. Edit the `ArmorItem` list (or `parse_*_items()` function for spec-driven extraction).
6. `python tools/generate_<new>_armor.py --dry-run` then `--apply`.
7. Validate refs against troops_<culture>.xml.
8. Add to CHANGELOG, update this feature doc table.

## How to add items as KEYforce ships more meshes

Same pattern — append to the per-culture generator's `ArmorItem` list, re-run with `--apply`. The script is idempotent so existing items aren't duplicated.

## Performance

No runtime impact. Pure data addition. ~277 new items add ~15 KB total to per-culture item indexes — negligible vs the multi-MB Bannerlord item system.

## Deep-Review Correction (2026-05-22)

`/deep-review` Agent 5 (data flow) caught one HIGH issue post-implementation: the Erebor generator initially wrote 123 `sk_dwarf_iron_*` items to `LOTRLOME_items/erebor/`, but the canonical home for that prefix is `LOTRLOME_items/iron_hills/` (125 pre-existing items). 118 IDs ended up in both folders → would have caused runtime engine warnings/shadowing.

**Fix (in-session):**
1. `tools/rollback_erebor_iron_misfile.py --apply` removed all 123 mis-filed items from `erebor/*.xml`.
2. `generate_erebor_armor.py` `DEFAULT_ARMORY_BASE` re-targeted to `iron_hills/`.
3. Re-running the generator added the 5 unique spec items to `iron_hills/shoulder_armors.xml` (the items not previously in the folder).

**Net result:** 0 duplicates across folders, 5 new items shipped (not 123 as originally claimed). Verified via `tools/validate_all_troop_refs.py` — 7/7 cultures PASS.

**Codified lesson:** CLAUDE.md "Equipment & Armory" now includes a per-prefix canonical-folder table. Memory: `feedback_multi_folder_id_uniqueness.md`. RCA: `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md`.

## Changelog

- 2026-05-22 — KEYforce multi-culture armor revamp (#211): mesh-first authoring pass adding 277 new Armory items across 5 cultures (Mordor 103, Isengard 15, Dol Guldur 14, Erebor 123, Rhun 22; Gundabad already complete). Added 5 per-culture `generate_<culture>_armor.py` generators (idempotent, sharing the `STAT_TIERS` table) plus the new multi-culture `validate_all_troop_refs.py` gate (7/7 cultures PASS, 0 missing refs). Post-review fix re-targeted the Erebor `sk_dwarf_iron_*` items from `erebor/` to the canonical `iron_hills/` folder, avoiding 118 duplicate IDs.

## GitHub Issue

- **Issue:** [#211 — feat(armory): KEYforce multi-culture armor revamp](https://github.com/haterade22/TAOM/issues/211)
- **Status:** Closed (after this commit)
- **RCA:** [docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md](../reviews/rca-multi-culture-armor-revamp-2026-05-22.md)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)

<!-- backlinks-end -->
