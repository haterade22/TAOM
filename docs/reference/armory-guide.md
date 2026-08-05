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
| **Gondor prefixes** | **17 regional tokens**, all `sk_gd_<region>_` in `gondor/`: `ano` Anórien (49 items), `dol` Dol Amroth (33), `pin` Pinnath Gelin (32), `los` Lossarnach (26), `lam` Lamedon (24), `sere` Serelond (22), `lin` Linhir (22), `vale` Blackroot Vale (20), `osg` Osgiliath (17), `anf` Anfalas (13), `cair` Cair Andros (11), `mns` Minas Tirith (11), `lon` Lond-Galen (10), `har` Harondor (9), `bel` Belfalas (7), `ith` Minas Ithil (6), `leb` Lebennin (6). Counts verified 2026-08-04 — this row listed only 5 of the 17 until then. Ten further head-armor ids are named/unique rather than regional (`imrahil_helmet`, `gondor_king_crown`, `ithilien_hood*`, `angbor_helmet`, `forlong_helmet`, `hirluin_helmet`, `golasgil_helment` *(misspelling is load-bearing — the mesh matches)*) |
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

