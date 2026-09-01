# Equipment & Armory guide

> LOTRLOME_Armory item layout, canonical-folder-per-prefix table, Gondor prefixes, CC facegen action_sets. Extracted from CLAUDE.md 2026-07-18. Authoring workflow: `/author-armor`.


| Item | Details |
|------|---------|
| **Armory dependency** | `LOTRLOME_Armory` (NOT `Armory_2` — it will be deleted) |
| **Item definitions** | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<folder>\` |
| **Item files per folder** | `body_armors.xml`, `head_armors.xml`, `leg_armors.xml`, `shoulder_armors.xml`, `arm_armors.xml` |
| **Global items** | `LOTRLOME_items\LOTRAOM_weapons.xml`, `LOTRAOM_shields.xml`, `LOTRAOM_horses.xml` |
| **Shield rule** | `item_usage="hand_shield"` (centre-grip) MUST pair with `ForceAttachOffHandPrimaryItemBone="true"`; `item_usage="shield"` (forearm-strapped) with `ForceAttachOffHandSecondaryItemBone="true"`. Never both. `body_name` is the `bo_cap_*` capsule, `shield_body_name` the full `bo_*` body. Two entries look mistyped and must NOT be corrected — see [armory-shield-audit.md](armory-shield-audit.md) |
| **Harness rule** | Every `Type="HorseHarness"` MUST carry `<Armor family_type="N">` matching its mount's `Monster.family_type` (1 horse/warg/spider, 4 chariot, 10 elephant/mûmakil). A missing attribute deserializes to **0 = human**, and the inventory screen then refuses the harness on every mount **with no error message** (`SPInventoryVM.IsItemEquipmentPossible`, v1.4.7 `:4112`) while stripping any pre-placed one on the next transfer (`:3923`). Mount-side authority is the monsters XML only — `family_type` on `<Horse>` is never parsed. Enforced by `python tools/validate_moduledata.py` (`MISSING_HARNESS_FAMILY_TYPE` / `HARNESS_FAMILY_MISMATCH`). |
| **Gondor prefixes** | **17 regional tokens**, all `sk_gd_<region>_` in `gondor/`: `ano` Anórien (49 items), `dol` Dol Amroth (33), `pin` Pinnath Gelin (32), `los` Lossarnach (26), `lam` Lamedon (24), `sere` Serelond (22), `lin` Linhir (22), `vale` Blackroot Vale (20), `osg` Osgiliath (17), `anf` Anfalas (13), `cair` Cair Andros (11), `mns` Minas Tirith (11), `lon` Lond-Galen (10), `har` Harondor (9), `bel` Belfalas (7), `ith` Minas Ithil (6), `leb` Lebennin (6). Counts verified 2026-08-04; this row listed only 5 of the 17 until then. Ten further head-armor ids are named/unique rather than regional (`imrahil_helmet`, `gondor_king_crown`, `ithilien_hood*`, `angbor_helmet`, `forlong_helmet`, `hirluin_helmet`, `golasgil_helment`). **The five named-lord helmets in that list are DEAD as of 2026-08-28**: their meshes were deleted, the item definitions survive but nothing references them, and the lords are dressed from their regions instead. `golasgil_helment`'s misspelling was load-bearing only while its mesh matched, so treat it now as a dead id, not a name to preserve. See "Deleted on 2026-08-28" below |
| **KEYforce spec drops** | `E:\repos\lotraom-assets\tools\<culture>_armors_and_troops.txt` — per-culture item lists + unit progression specs |
| **CC facegen action_sets** | LIVE at `E:\Steam\...\LOTRLOME_Armory\ModuleData\action_sets.xml`; tracked snapshot `docs/reference/lotrlome-armory-snapshot/`. Every TAOM race id needs full-surface `as_<race>_facegen` + `_female_facegen` (copy `as_dwarf_facegen`; slim entries break post-parent CC). See [character-creation.md](../../docs/features/character-creation.md) |
| **action_sets structure** | Every `<action>` MUST be nested inside an `<action_set>` — `<action_sets>` accepts only `<action_set>` children (`<game>/XmlSchemas/soln_action_sets.xsd`, byte-identical in the game and dedicated-server installs). A root-level `<action>` is structurally illegal, but the game client **loads it silently**: build 1.4.7.117484 tolerates the file, while build 117131 — which the dedicated-server engine ships — throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at schema path `/action_sets/action` and dies on boot. Both build numbers are as reported in the 2026-08-03 co-op field report; the installed client's `Version.xml` carries only `v1.4.7`, so they are not locally verifiable. Note the XSD is *not* the enforcement point — it never declares `base_set`, which every shipped file uses heavily, so the client clearly does not validate against it; the invariant is enforced by the loader's merge path. Guard: `python tools/audit_action_set_parity.py` exits non-zero on any root-level `<action>`. Fixer for the 2026-08-03 case (twelve self-closing `as_<race>_female_villager_in_aserai_tavern` sets orphaning 168 elements): `tools/oneoff/fix_orphaned_tavern_conversation_actions.py`, which repairs the LIVE file and the tracked snapshot together. Fuller action-set tooling: [lotrlome-armory-snapshot/README.md](lotrlome-armory-snapshot/README.md) |

### Armory folder canonical home per item-id prefix

**MANDATORY: before authoring a new item, grep ALL `LOTRLOME_items/*/` subfolders for the prefix.** The first folder that already contains items with that prefix is the canonical home. Adding items to a different folder creates runtime duplicate-ID warnings (engine silently shadows one entry). Even when the spec file is named after culture X, the canonical folder may be a sub-culture (e.g., dwarf items live in `iron_hills/`, not `erebor/`).

| Item prefix | Canonical folder | Notes |
|-------------|------------------|-------|
| `sk_gd_*` | `gondor/` | All Gondor regional items (Anorien through Lamedon) |
| `sk_md_orc_*`, `sk_gn_orc_*`, `sk_uruk_mordor_*`, `ar_ardunian_*` | `mordor/` | Generic orc pool shared across factions also lives here |
| `sk_uruk_hai_*`, `sk_is_orc_*`, `urukscout_*`, `clo_urukscout_*` | `isengard/` | |
| `sk_dg_uruk_*`, `sk_dg_orc_*` | `dol_guldur/` | |
| `sk_dg_khml_*` (Khamul) | `rhun/` | Cross-faction with Dol Guldur — lives in `rhun/` |
| `sk_gb_uruk_*` | `gundabad/` | |
| `sk_dwarf_erebor_*` | `erebor/` | Core dwarven set |
| `sk_dwarf_iron_*` | **`iron_hills/`** | NOT `erebor/` — caught in #211 deep-review (RCA: `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md`) |
| `sk_dwarf_dain_*` | `erebor/` | Dain's set |
| `sk_rh_loke_*`, `sk_rh_drag_*` | `rhun/` | Loke-Rim + Dragon-Wrath |

**Validation:** When adding/changing equipment, always verify item IDs exist in Armory. Characters appear in underwear when items are missing. Run `python tools/validate_all_troop_refs.py` to cross-check every `sk_*/ar_*/clo_urukscout_*/urukscout_*` reference across all 7 troop XML files in one pass.

## Two asset trees, and why a clean validator run can lie

> **Corrected 2026-09-01: the Armory has no cooked tree any more.** As of v2.0.23 there is
> no `LOTRLOME_Armory/AssetPackages/` directory at all: 0 cooked packs against 4,490 loose
> `Assets/**/*.tpac`, which are what the game now loads. `SandBoxCore` and `SandBox` have
> none either. Everything below still describes how the two trees relate wherever a cooked
> tree exists (`Native`, `TAOM`, `TAOM_Map`), but for the Armory the stale-pack trap is
> currently **unreachable**, and `Assets/` is the single source of truth.
>
> Two consequences, both now handled in the tools:
> - `validate_mesh_refs.py` falls back to `Assets/**` for any module shipping no cooked
>   packs, and warns when it does. Before that fix its default module list resolved to
>   Native alone, which reported thousands of false `MISSING_MESH`.
> - `audit_deleted_mesh_impact.py` no longer exits 2 on the absent `AssetPackages`. With no
>   cooked side to diff against it derives "gone" from the reference side instead, which is
>   a narrower question: it cannot see art deleted while nothing referenced it.

The Armory carries the same art twice, and they can disagree:

| Tree | What it is | Read by |
|---|---|---|
| `AssetPackages/pack0-9.tpac` | the **cooked** packs the running game loads | `tools/validate_mesh_refs.py` |
| `Assets/**/*.tpac` | the **authoring** tree, one tpac per asset | `tools/audit_deleted_mesh_impact.py` |

Packs are rebuilt only on an explicit re-cook. **Art deleted from `Assets/` keeps shipping
from a stale pack, so `validate_mesh_refs.py` returns `PASS` while the source tree is
already broken.** The reverse also happens: art imported after the last cook exists for the
editor and not for the game, and renders naked in-game right now. Check both directions
before trusting either. Full case: [`docs/features/armoury-mesh-cleanup.md`](../features/armoury-mesh-cleanup.md).

## Deleted on 2026-08-28 (do not re-reference)

Four `lotraom-assets` commits removed 755 asset files. What that means for authoring:

- **No Erebor team colours.** All 57 `sk_dwarf_erebor_*_{blue,green,red}` items are gone
  from both Armory copies. Use the base id and let `Flags/UseTeamColor` do the work; that
  is what made the per-colour meshes redundant in the first place.
- **No Easterling armour set.** `easterling_*` and `easterlingwarriors0N_*` are dead art.
  Rhûn's living set is Loke-Rim (`sk_rh_loke_*`) and Dragon-Wrath (`sk_rh_drag_*`).
- **No bespoke Gondor lord kits.** `angbor_*`, `forlong_*`, `golasgil_*`, `hirluin_*`,
  `imrahil_*`, `lossarnach_coat` and `ar_ardunian_elite_*` are dead. Dress named Gondor
  lords from their region's own prefix instead.

**Gondor regional slot coverage is uneven**, which matters when dressing a lord: Dol Amroth
(`sk_gd_dol_`) is the only region shipping all five slots. Lamedon and Anfalas ship no
gloves, greaves or cape; Lossarnach and Pinnath Gelin ship no greaves. The generic
lord-tier fallbacks are `sk_gd_sere_bracer_lord_a` and `sk_gd_sere_grvs_lord_a`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/armoury-mesh-cleanup.md](../features/armoury-mesh-cleanup.md)
- [docs/reference/armory-shield-audit.md](./armory-shield-audit.md)
- [docs/reviews/lessons/xslt-moduledata.md](../reviews/lessons/xslt-moduledata.md)

<!-- backlinks-end -->
