# Armoury mesh cleanup (2026-08-28): deleted art, and re-dressing what wore it

## Overview

On 2026-08-28 four commits in the `lotraom-assets` repo (`0ecd5df1` Asset Delete,
`7946f65c` Gondor Cleanup, `f6029ca9` Clean up 2, `312f5ab9` Cleanup 3) deleted **755
asset files** (431 `.tpac`, 294 `.png`, 30 `.fbx`) from `v1.4/LOTRLOME_Armory/`, and the
deletion was synced into the live install between 11:33 and 11:53 that morning.

Nothing removed the corresponding mesh ids from the item XML. This doc records what was
lost, how it was measured, what each affected item was re-pointed at, and what is still
outstanding.

Two tools came out of it:

| Tool | Direction | Purpose |
|---|---|---|
| [`tools/audit_deleted_mesh_impact.py`](../../tools/audit_deleted_mesh_impact.py) | read-only | Diff the cooked packs against the authoring tree, then join gone mesh to item to troop |
| [`tools/apply_dead_mesh_item_swaps.py`](../../tools/apply_dead_mesh_item_swaps.py) | writes, preview by default | Apply the decided re-pointing, remove the dead Erebor colour variants |

## Why this exists: the stale-pack trap

`tools/validate_mesh_refs.py` resolves every mesh reference against the **cooked** packs,
`LOTRLOME_Armory/AssetPackages/pack0-9.tpac`. Those are rebuilt only when someone
re-cooks them. Delete art from `Assets/` or `AssetSources/` and the packs keep shipping
the old meshes, so:

- the validator reports `PASS`,
- the game keeps rendering the items,
- and nothing breaks until the next re-cook.

That is exactly what happened. The packs were cooked **2026-08-25 08:29**; `Assets/`
was updated on the 28th. A first `validate_mesh_refs.py` run during triage returned
`PASS: no mesh-reference issues found` while 179 meshes were already gone from the
authoring tree.

**The rule this produces: a clean mesh-reference run proves nothing about art deleted
after the last pack cook.** Compare the two trees, or wait for the re-cook to tell you
the hard way.

## The two mesh sets, and why their counts differ

Two questions that look like one:

| Set | Source of truth | Answers |
|---|---|---|
| **imported but not cooked** | `Assets/**/*.tpac` minus `AssetPackages/*.tpac` | Art that exists for the editor and not for the running game. Renders naked NOW. |
| **referenced but not cooked** | item XML refs minus `AssetPackages/*.tpac` | The subset of the above that an item actually names. |

The first is wider. On 2026-08-28 it held 11 meshes; `validate_mesh_refs.py` reported 10,
the difference being `gondor_horse_plate_armor`, which is imported into `Assets/` and
referenced by no item, so a tool that walks item XML cannot see it. **A count difference
between the two is the expected shape, not a discrepancy to reconcile.**

## What was measured

Computed with `validate_mesh_refs.py`'s own `.tpac` TOC scanner over both trees:

| Quantity | Count |
|---|---|
| Metameshes in packs (cooked, pre-cleanup) | 4,660 |
| Metameshes in `Assets/` (authoring, post-cleanup) | 4,492 |
| **Meshes deleted** | **179** |
| Meshes imported but not yet cooked | 11 |
| Collision bodies deleted | 1 (referenced by nothing) |
| Deleted meshes still referenced by item XML | 149, across 26 files |
| Distinct items affected | 153 |
| Distinct characters affected | 164 |

**Corroborated against the four commits.** The audit scans the pre-image of every `.tpac`
those commits deleted: 431 parsed, 175 mesh names recovered, **all 175 agreeing with the
set difference and none contradicting it**. The set difference stays the source of truth,
since textures and `.fbx` carry no metamesh names and can only ever be a subset.

This needs `git cat-file --filters`, **not** `git show`. The asset repo tracks `*.tpac`
through LFS, so `git show` returns a 128-byte pointer whose TOC scan finds zero meshes,
producing a silent zero indistinguishable from a genuinely empty result.

## What was decided, and applied

484 reference swaps across 8 repo files, 57 item definitions removed from both Armory
copies, 3 mesh re-points. Four groups, three different mechanisms.

### 1. Gondor lords take their own region's armour (25 refs)

Angbor is Lord of Lamedon, Forlong of Lossarnach, Golasgil of Anfalas, Hirluin of Pinnath
Gelin, Imrahil Prince of Dol Amroth. Each takes the highest tier its own region ships.

| Lord | Region | Body | Head |
|---|---|---|---|
| Angbor | Lamedon | `sk_gd_lam_inf_chest_lord_a` | `sk_gd_lam_nob_helmet_lord_a` |
| Forlong | Lossarnach | `sk_gd_los_nob_chest_lord_a` | `sk_gd_los_noble_helmet_elite_a` |
| Golasgil | Anfalas | `sk_gd_anf_inf_chest_heavy_a` | `sk_gd_anf_inf_helmet_heavy_a` |
| Hirluin | Pinnath Gelin | `sk_gd_pin_nob_chest_elite_b` | `sk_gd_pin_noble_cav_helmet_elite_a` |
| Imrahil | Dol Amroth | `sk_gd_dol_chest_elite_a` | `sk_gd_dol_cav_helmet_elite_a` |

**Regional slot coverage is uneven and this constrains the result.** Lamedon and Anfalas
ship no gloves, greaves or cape; Lossarnach and Pinnath Gelin ship no greaves. Those slots
fall back to the generic Gondor lord-tier Serelond pieces (`sk_gd_sere_bracer_lord_a`,
`sk_gd_sere_grvs_lord_a`), with an Anorien noble elite cape where the region has none.
**Dol Amroth is the only Gondor region that ships all five slots**, so Imrahil is the only
lord who stays fully regional. Hirluin and Imrahil are mounted, so both take cavalry helms.

**This costs the lords real armour and there is no way around it.** The bespoke pieces were
85 body / 50 head / 35 gloves; nothing any region ships exceeds 70 / 41 / 27. The art
backing those numbers was deleted.

### 2. Easterling becomes Loke-Rim (12 refs)

Tier-matched piece by piece so no notable or troop silently gains or loses protection:
88 to 89, 52 to 51, 41 to 41, 40 to 40, 26 to 26, 25 to 25, 20 to 20. One deliberate
exception: `easterlingwarriors04_cape` was 30 and Loke-Rim's heaviest shoulder is 24, so
it drops. `easterling_shield` becomes `sm_rh_loke_shield_med_a`.

The blast radius here was the largest: `easterling_boots` alone dressed 69 characters
through `npc_companion_equipment_template_rhun`, so most of the Rhûn civilian population
was affected.

### 3. Erebor loses blue, green and red (5 refs, 57 definitions)

All 57 `_blue` / `_green` / `_red` Erebor items had a dead mesh, so every one was deleted
from the live Armory **and** the `lotraom-assets` copy. Only 5 were equipped, all on
`named_companion_yotthani`, and each had an exact surviving base counterpart (the same id
minus the colour suffix), so he now wears the plain versions. Team colour is handled by
`Flags/UseTeamColor` on the base item, which is why the per-colour meshes were redundant.

### 4. Career starter boots are re-meshed, not swapped (3 items)

`starter_{cavalry,infantry,ranged}_khuzait_leg_a` are tuned to 5 / 7 / 9 armour for the
career start, and the lowest surviving Loke leg item is 15. A reference swap would have
near-tripled starting leg armour, so instead their `mesh=` points at `sk_rh_loke_boots_a`.
That fixes the invisible boot and leaves the tuning alone.

`lossarnach_coat` (villagers, notables, headman, ransom broker) becomes
`gondor_noble_coat_a`, 24 to 20, already worn by the rest of that file's cast.

## Outstanding

| Item | Consumers | Status |
|---|---|---|
| `ar_ardunian_elite_armour` | Umbar enlistment / wanderer / troop rosters | Dead mesh, still equipped. No replacement chosen. |
| `lotr_troll_armor` / `_bracers` / `_helmet` | `troops/troops_mordor.xml` | Dead mesh, still equipped. No replacement chosen. |
| 89 orphaned dead items | nothing | Definitions survive with dead meshes. Safe to delete whenever. |
| 11 imported-not-cooked meshes | goat bardings, Khamûl bardings, `gondor_horse_plate_armor` | Needs a pack re-cook, not a data fix. |

**Not verified in-game.** Bannerlord globs item XML once at process launch with no
hot-reload, so this needs a build, a full restart and a new campaign before it counts as
done (`.claude/rules/moduledata-validation.md`).

## Key files

- `tools/audit_deleted_mesh_impact.py` plus `tools/tests/test_audit_deleted_mesh_impact.py` (40 tests)
- `tools/apply_dead_mesh_item_swaps.py` plus `tools/tests/test_apply_dead_mesh_item_swaps.py` (25 tests)
- Report output: `tools/reports/mesh-cleanup/` (`REPORT.md`, `impact.tsv`, `impact.json`)
- Consumers touched: `characters/npcs_{gondor,rhun}.xml`,
  `equipmentsets/taom_{char_creation,equipment_sets_gondor,lord_template,wanderer}_equipment.xml`,
  `named_companions/named_companions.xml`, `troops/troops_rhun_new.xml`
- Armory touched: `LOTRLOME_items/erebor/*.xml`, `LOTRLOME_items/rhun/starter_armors.xml`,
  in both the live install and `E:\repos\lotraom-assets\v1.4\LOTRLOME_Armory\`

## Verification performed

- `python tools/validate_moduledata.py` → PASS
- `python -m unittest discover -s tools/tests -p "test_*.py"` → 691 tests OK
- Every written file re-parsed with `ElementTree`, 20 of 20 well-formed
- Audit re-run: the decision set fell from 45 to exactly the 4 deliberately left
- Zero remaining definitions or references to any Erebor colour variant, in either copy

## Related

- [mesh-ref-validation.md](./mesh-ref-validation.md): the forward validator this complements
- [../reference/armory-guide.md](../reference/armory-guide.md): canonical folders and authoring rules
- [../reviews/audit-gondor-armory-2026-08-04.md](../reviews/audit-gondor-armory-2026-08-04.md): the reverse-audit precedent

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/mesh-ref-validation.md](./mesh-ref-validation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/armory-guide.md](../reference/armory-guide.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
