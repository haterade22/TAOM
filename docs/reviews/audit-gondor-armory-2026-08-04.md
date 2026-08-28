# Gondor armory audit — mesh↔item completeness, all armor slots (2026-08-04)

> **Trigger:** players reported that "not all of the Gondor helmets are in the game", specifically
> that they **cannot buy them in shops**. The suspected cause was an XML gap — helmet meshes
> shipped in `LOTRLOME_Armory`'s asset packages with no matching `<Item>` entry.
>
> **Result: the XML is complete.** Zero Gondor meshes are missing an item, zero Gondor items
> reference a missing mesh, across every armor slot. The player-visible symptom is a
> *reachability* problem in TAOM's own `CultureMarketplace`, not a data gap.

## Method

`tools/validate_mesh_refs.py` gained a reverse-audit mode for this
(`--unreferenced` / `--prefix`), so the check is repeatable rather than ad hoc. The forward
tiers ask *"does every referenced mesh exist?"*; the reverse pass asks *"does every packaged
mesh have an item?"* — the question a "the art shipped but it's not in the game" report
actually poses. Both sides come from the tool's existing `build_present_set()` /
`extract_refs()`, so the reverse pass costs nothing once Tier B has run.

```
python tools/validate_mesh_refs.py --no-rgl-log --unreferenced --prefix sk_gd_
python tools/validate_mesh_refs.py --no-rgl-log --unreferenced --tpac-modules LOTRLOME_Armory
```

Scope: `…\LOTRLOME_Armory\AssetPackages\pack0-9.tpac` (10 packs, 13 GB, **0 parse failures**)
against `…\LOTRLOME_Armory\ModuleData\`. Tier A did not run — no rgl_log; the offline tiers
cannot observe the running engine.

## Gondor: per-slot results

| Slot | Items | Distinct meshes | Meshes packaged | `sk_gd_*` items | Defined but unworn |
|---|---:|---:|---:|---:|---:|
| HeadArmor | 121 | 121 | **121** | 111 | 4 |
| BodyArmor | 128 | 125 | **125** | 100 | 0 |
| Cape | 71 | 71 | **71** | 63 | 10 |
| HandArmor | 31 | 31 | **31** | 24 | 1 |
| LegArmor | 30 | 27 | **27** | 20 | 1 |
| **Total** | **381** | **375** | **375** | **318** | **16** |

Reverse audit, `--prefix sk_gd_`:

```
packaged meshes considered: 418
referenced by an item     : 318
_slim variants suppressed : 100
UNREFERENCED (no item)    : 0
```

The 100 suppressed entries are `_slim` female body variants. The engine resolves those by
appending `_slim` to the mesh name, so they are never named in item XML — every one of the 100
has a base mesh that *is* referenced. Reporting them would have been 100 false positives.

**Every Gondor helmet mesh in the packages has an item, and every Gondor item's mesh is in the
packages.** Helmets specifically: 109 helmet meshes, 109 helmet mesh references, exact 1:1.

Supporting data checks, all clean:

- **0 duplicate item ids** across all 3297 armory items — the known cross-folder shadowing trap
  (`docs/reviews/lessons/data-content-cultures.md:84`) is not firing here.
- All 381 items carry `culture="Culture.gondor"`; **0 carry `is_merchandise="false"`**.
- No item carries `value=`, which matches vanilla (809 of 1486 Native items also omit it) — not
  a differentiator.
- `SubModule.xml` registers `LOTRLOME_items/gondor` as a **directory**, and `MBObjectManager`
  globs `*.xml` inside it, so all six files load including `starter_armors.xml` (never named
  individually anywhere).
- 302 distinct `Item.sk_gd_*` references across `Main/_Module/ModuleData/**` — all resolve.

### The 16 defined-but-unworn items

These load fine and are purchasable in principle; no troop or equipment set wears them.

| Slot | Ids |
|---|---|
| Cape (10) | `sk_gd_ano_pauld_fount_elite_a`, `sk_gd_ano_pauld_fount_heavy_a`, `sk_gd_ano_pauld_noble_elite_b`, `sk_gd_ano_pauld_noble_heavy_b`, `sk_gd_ano_pauld_noble_med_b`, `sk_gd_ano_pauld_noble_med_c`, `sk_gd_lin_pauld_cape_noble_heavy_a`, `sk_gd_osg_pauld_inf_elite_a`, `sk_gd_osg_pauld_inf_heavy_a`, `sk_gd_osg_pauld_inf_med_a` |
| HeadArmor (4) | `sk_gd_dol_inf_helmet_elite_c`, `sk_gd_dol_inf_helmet_elite_d`, `sk_gd_dol_ward_helmet_elite_a`, `sk_gd_dol_ward_helmet_elite_b` |
| HandArmor (1) | `sk_gd_sere_bracer_lord_a` |
| LegArmor (1) | `sk_gd_sere_grvs_lord_a` |

**Pauldrons are the real gap, not helmets** — 10 of the 16. The 4 idle helmets are all
Dol-Amroth: the `gondor_da_*` line has 11 troops and no "Warden", so
`sk_gd_dol_ward_helmet_elite_a/_b` have nobody to wear them, and the foot-knight ladder tops out
at `inf_helmet_elite_b`. The two Serelond `_lord_a` pieces are lord-tier and reserved by design.

## Why players can't buy them: `CultureMarketplace`

Not a data problem. Verified in TAOM source:

| Fact | Source |
|---|---|
| 6 items injected per town per day; roster capped at 200 distinct entries | `Main/Features/CultureMarketplace/Domain/MarketplaceTuning.cs:28-29` |
| Items whose culture ≠ the town's are **deleted whole-stack** (6/tick; uncapped on the one-shot new-game sweep) | `Main/Features/CultureMarketplace/CultureMarketplaceMaintenanceService.cs:76-81` |
| The culture key is the **owner clan's** culture, not `Settlement.Culture` | `Main/Adapters/TownRosterAdapter.cs:19-22` |
| Config names 4 warg routing entries and **zero** Gondor entries — no blacklist, no boost, no `min_stock` | `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml` (43 lines) |

A Gondor town draws 6 items/day, weighted-random, from a flat pool holding the entire Gondor
catalogue — 381 armor pieces plus weapons, horses and shields. Any one specific helmet is
roughly a 1% per-day chance in a given town. Nothing is broken; the per-item distribution is
just very thin. Two secondary reducers: a town already at ≥200 distinct entries receives no
injection at all, and a Gondor town captured by a non-Gondor clan has its Gondor armor stripped
by the filter.

This is the failure shape the feature's own RCA already named —
[`rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md`](rca-culturemarketplace-aspirational-scaffolding-2026-05-20.md):
*"user-promise gap between probabilistic + additive design and deterministic + visible
expectations."*

**Levers, none pulled here** (this audit is report-only):

- `<Routing>` with `min_stock="1"` for signature helms (Fountain Guard, Citadel Guard, Swan
  Knight). `min_stock` bypasses the 200-entry cap and is deterministic — which is what that RCA
  says an "always available" promise requires.
- A `<Culture id="gondor"><Boost>` block to bias the weighted draw.
- Raising `ItemsPerTownPerDay`, which is hardcoded and affects every culture.

**One-boot confirmation for a reporter:** the TAOM log line
`[CultureMarketplace]   gondor: N items — sample: …` (`CultureItemPoolService.cs:112-123`). A
large `N` with empty shops points at the removal pass or the owner-culture key; a large
`unresolved` count points at a `PrefixMap` classification gap instead — the shape of C3 in that
same RCA, where five crafted weapons had no culture attribute and no prefix row and were
therefore injected into no pool at all.

## Armory-wide: 549 unreferenced meshes (Gondor contributes none)

Same pass, `--tpac-modules LOTRLOME_Armory`, no prefix filter:

```
packaged meshes considered: 4,527
referenced by an item     : 3,867
_slim variants suppressed : 111
UNREFERENCED (no item)    : 549
```

| Family | Count | Reading |
|---|---:|---|
| `clo_*` | 276 | Cloth-simulation companion meshes. **201 have an item-referenced base mesh** once `clo_` is stripped, i.e. they look like the same kind of naming-convention variant as `_slim`. The remaining **75** are unexplained. |
| `sm_*` | 69 | Crafting pieces. Includes `sm_ar_art_poleaxe_blade_a/_b`, `_handle_a`, `_pommel_a` — the Gondor poleaxe that was authored then **deliberately removed** after it hard-crashed new-campaign load (`ItemObject.Deserialize` NRE), pending an `AssetPackages` recompile. |
| everything else | 204 | The genuine candidates for "art shipped, no item entry" — concentrated in `sk_dwarf_*` (30), `sk_gb_*` (20), `sk_uruk_*` (17), `dunland_*` (13). |

No Gondor **armor** mesh appears anywhere in the 549. The only Gondor-named entries are cloth
variants (`clo_sk_gd_*`), two `lrd_*` marine weapons, `wm_gondor_spear_b_shaft` and
`wm_horn_gondor_base` — none of them armor.

**Do not treat the 549 as a bug list.** The `clo_` convention is unconfirmed: if the engine
resolves cloth meshes by name convention the way it does `_slim`, then ~201 of them are benign
by construction and the tool should suppress them too. That needs a decompile before anyone
acts on it — see follow-ups.

## Follow-ups

1. **Confirm the `clo_` convention** (decompile the cloth-mesh resolution path). If it mirrors
   `_slim`, add the same suppression to `reverse_audit()` and re-baseline the 549.
2. **Home the 16 idle Gondor pieces** — chiefly the 10 pauldrons — via the variant-roster
   pattern already used for Lamedon/Lossarnach/Anfalas
   ([`gondor-armor-revamp.md`](../features/gondor-armor-revamp.md)). `/author-armor` owns this.
3. **Decide on the marketplace levers above** if "buyable" is the actual product requirement.
4. **Re-run with Tier A** (`--rgl-log <newest>` after a live battle) before calling the asset
   side authoritatively clean — the offline tiers cannot observe the running engine.
5. **Neither validator answers "defined but never used."** `validate_all_troop_refs.py` and
   `validate_moduledata.py` both check refs→definitions only; the idle-item column in this doc
   had to be computed ad hoc. An `IDLE_ITEM_DEF` (INFO) pass in `tools/taom_schema.py` would
   close it.

## Scope note

The Gondor item XML lives in the **game install** (`…\Modules\LOTRLOME_Armory\ModuleData\`) and
is in **no git repository**. Nothing in this audit modified it. Backups there must not use a
`.xml` extension — the loader globs `*.xml` and a `foo_backup.xml` becomes a silent duplicate-id
shadow.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/armoury-mesh-cleanup.md](../features/armoury-mesh-cleanup.md)
- [docs/features/mesh-ref-validation.md](../features/mesh-ref-validation.md)

<!-- backlinks-end -->
