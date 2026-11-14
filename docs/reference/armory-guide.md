# Equipment & Armory guide

> LOTRLOME_Armory item layout, canonical-folder-per-prefix table, Gondor prefixes, CC facegen action_sets. Extracted from CLAUDE.md 2026-07-18. Authoring workflow: `docs/features/troop-tree-revamp.md`.

The Armory ships to players at the next editor package, and "unversioned" means untracked by git, not unshipped.
Never edit, commit or push `E:\repos\lotraom-assets`; Mike syncs it himself. Name the live file you changed in your report (Mike, 2026-09-22).


| Item | Details |
|------|---------|
| **Armory dependency** | `LOTRLOME_Armory` |
| **Item definitions** | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<folder>\` |
| **Item files per folder** | `body_armors.xml`, `head_armors.xml`, `leg_armors.xml`, `shoulder_armors.xml`, `arm_armors.xml`; `harad/` also holds `weapons.xml` (KEYforce's Harad regular weapons, #805) |
| **Global items** | `LOTRLOME_items\LOTRAOM_weapons.xml`, `LOTRAOM_shields.xml`, `LOTRAOM_horses.xml` |
| **Shield rule** | `item_usage="hand_shield"` (centre-grip) MUST pair with `ForceAttachOffHandPrimaryItemBone="true"`; `item_usage="shield"` (forearm-strapped) with `ForceAttachOffHandSecondaryItemBone="true"`. Never both. `body_name` is the `bo_cap_*` capsule, `shield_body_name` the full `bo_*` body. Two entries look mistyped and must NOT be corrected — see [armory-shield-audit.md](armory-shield-audit.md) |
| **Harness rule** | Every `Type="HorseHarness"` MUST carry `<Armor family_type="N">` matching its mount's `Monster.family_type` (1 horse/warg/spider/war ram, 4 chariot, 10 elephant/mûmakil). A missing attribute deserializes to **0 = human**, and the inventory screen then refuses the harness on every mount **with no error message** (`SPInventoryVM.IsItemEquipmentPossible`, v1.4.7 `:4112`) while stripping any pre-placed one on the next transfer (`:3923`). Mount-side authority is the monsters XML only, and `family_type` on `<Horse>` is never parsed. Enforced by `python tools/validate_moduledata.py` (`MISSING_HARNESS_FAMILY_TYPE` / `HARNESS_FAMILY_MISMATCH`). Separately, **every `Horse` slot REQUIRES a `HorseHarness` slot in the same set** (`MOUNT_WITHOUT_HARNESS`). The harness is not always armour: on the war ram it is the saddle, since `sk_eb_goat_a`/`_b` are bare pelts, so an empty slot spawns the rider on bare hide. No attribute records which mounts carry their own seat, so exceptions are argued in `_HARNESSLESS_BY_DESIGN` rather than assumed. |
| **Gondor prefixes** | **17 regional tokens**, all `sk_gd_<region>_` in `gondor/`: `ano` Anórien (49 items), `dol` Dol Amroth (33), `pin` Pinnath Gelin (32), `los` Lossarnach (26), `lam` Lamedon (24), `sere` Serelond (22), `lin` Linhir (22), `vale` Blackroot Vale (20), `osg` Osgiliath (17), `anf` Anfalas (13), `cair` Cair Andros (11), `mns` Minas Tirith (11), `lon` Lond-Galen (10), `har` Harondor (9), `bel` Belfalas (7), `ith` Minas Ithil (6), `leb` Lebennin (6). Counts verified 2026-08-04; this row listed only 5 of the 17 until then. Ten further head-armor ids are named/unique rather than regional (`imrahil_helmet`, `gondor_king_crown`, `ithilien_hood*`, `angbor_helmet`, `forlong_helmet`, `hirluin_helmet`, `golasgil_helment`). **The five named-lord helmets in that list are DEAD as of 2026-08-28**: their meshes were deleted AND their item definitions went with them (corrected 2026-09-01, this row previously said the definitions survive; grepping all of `LOTRLOME_items/` for those five prefixes returns nothing), so referencing one is a hard `BROKEN_ITEM_REF` rather than a naked mesh. The lords are dressed from their regions instead. `golasgil_helment`'s misspelling was load-bearing only while its mesh matched, so treat it now as a dead id, not a name to preserve. See "Deleted on 2026-08-28" below |
| **KEYforce spec drops** | `E:\repos\lotraom-assets\tools\<culture>_armors_and_troops.md`: per-culture item lists + unit progression specs |
| **CC facegen action_sets** | LIVE at `E:\Steam\...\LOTRLOME_Armory\ModuleData\action_sets.xml`; tracked snapshot `docs/reference/lotrlome-armory-snapshot/`. Every TAOM race id needs full-surface `as_<race>_facegen` + `_female_facegen` (copy `as_dwarf_facegen`; slim entries break post-parent CC). See [character-creation.md](../../docs/features/character-creation.md) |
| **action_sets structure** | Every `<action>` MUST be nested inside an `<action_set>`: `<action_sets>` accepts only `<action_set>` children (`<game>/XmlSchemas/soln_action_sets.xsd`, byte-identical in the game and dedicated-server installs). A root-level `<action>` is structurally illegal, but the game client **loads it silently**: build 1.4.7.117484 tolerates the file, while build 117131, which the dedicated-server engine ships: throws `KeyNotFoundException` in `MBObjectManager.MergeElements` at schema path `/action_sets/action` and dies on boot. Both build numbers are as reported in the 2026-08-03 co-op field report; the installed client's `Version.xml` carries only `v1.4.7`, so they are not locally verifiable. Note the XSD is *not* the enforcement point: it never declares `base_set`, which every shipped file uses heavily, so the client clearly does not validate against it; the invariant is enforced by the loader's merge path. Guard: `python tools/audit_action_set_parity.py` exits non-zero on any root-level `<action>`. Fixer for the 2026-08-03 case (twelve self-closing `as_<race>_female_villager_in_aserai_tavern` sets orphaning 168 elements): `tools/oneoff/fix_orphaned_tavern_conversation_actions.py`, which repairs the LIVE file and the tracked snapshot together. The generator is not the suspect: `tools/generate_race_civilian_action_sets.py` bounds its output with `TAOM-CIVILIAN-COVERAGE:START/END` markers and emits no `_in_aserai_tavern` sets, so a broken tavern set was hand-authored. "It loads in game" is evidence about one engine build, so a structural defect needs the audit, not a play session. Run the audit before committing any `action_sets.xml` edit. Fuller action-set tooling: [lotrlome-armory-snapshot/README.md](lotrlome-armory-snapshot/README.md) |

### Armory folder canonical home per item-id prefix

**MANDATORY: before authoring a new item, grep ALL `LOTRLOME_items/*/` subfolders for the prefix.** The first folder that already contains items with that prefix is the canonical home. Adding items to a different folder duplicates the id (the engine merges the two entries attribute by attribute, so a mix of both definitions loads with no message; `DUPLICATE_ITEM_DEF` flags it). Even when the spec file is named after culture X, the canonical folder may be a sub-culture (e.g., dwarf items live in `iron_hills/`, not `erebor/`).

| Item prefix | Canonical folder | Notes |
|-------------|------------------|-------|
| `sk_gd_*` | `gondor/` | All Gondor regional items (Anorien through Lamedon) |
| `sk_md_orc_*`, `sk_gn_orc_*`, `sk_uruk_mordor_*`, `ar_ardunian_*` | `mordor/` | Generic orc pool shared across factions also lives here |
| `sk_uruk_hai_*`, `sk_is_orc_*`, `urukscout_*`, `clo_urukscout_*` | `isengard/` | |
| `sk_dg_uruk_*`, `sk_dg_orc_*` | `dol_guldur/` | |
| `sk_dg_khml_*` (Khamul) | `rhun/` | Cross-faction with Dol Guldur — lives in `rhun/` |
| `sk_gb_uruk_*` | `gundabad/` | |
| `sk_hr_st_*`, `sk_sg_st_*` (armour, bardings, saddles); `hr_st_*`, `sg_st_*`, `sm_hr_st_*`, `sm_sg_st_*` (weapons, shields) | `harad/` | Harwan (Harad) and Shaghana. The weapons sit in `harad/weapons.xml` (#805), the only culture folder with a `weapons.xml`; the other cultures' troop weapons are in the global files (the ladder launchers sit in each folder's `ranged_ladder.xml`, the player starter weapons in `starter_kit.xml`) |
| `sk_dwarf_erebor_*` | `erebor/` | Core dwarven set |
| `sk_dwarf_iron_*` | **`iron_hills/`** | NOT `erebor/` — caught in #211 deep-review (RCA: `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md`) |
| `sk_dwarf_dain_*` | `erebor/` | Dain's set |
| `sk_rh_loke_*`, `sk_rh_drag_*` | `rhun/` | Loke-Rim + Dragon-Wrath |

**Validation:** When adding/changing equipment, always verify item IDs exist in Armory. Characters appear in underwear when items are missing. Run `python tools/validate_moduledata.py`; its `BROKEN_ITEM_REF` covers every troop file.

## Two asset trees, and why a clean validator run can lie

The game loads a module's loose `Assets/**/*.tpac` whenever the module has an `Assets/` folder, and ignores cooked
`AssetPackages/pack*.tpac` while it exists. The engine log names `Assets` for each module
(`Loading packages $BASE/Modules/LOTRLOME_Armory/Assets...`). The Armory has held both trees since 2026-09-26.
TAOM and TAOM_Map ship both; `SandBoxCore` and `SandBox` ship no packs. The live `Assets/` folder is the single
source of truth (Mike, 2026-09-26). The dated evidence is in
[armoury-mesh-cleanup](../features/armoury-mesh-cleanup.md) "Asset-tree evidence".

| Tree | What it is | Read by |
|---|---|---|
| `AssetPackages/pack0-9.tpac` | the **cooked** packs; the game ignores them while `Assets/` exists | only `tools/audit_deleted_mesh_impact.py`, which diffs them against `Assets/`; no validator resolves refs against them |
| `Assets/**/*.tpac` | the **authoring** tree, one tpac per asset, and what the game loads | `tools/validate_mesh_refs.py`, `tools/audit_deleted_mesh_impact.py` |

Packs are rebuilt only on an explicit re-cook, so they go stale, but the game does not read them while `Assets/`
exists. **Art deleted from `Assets/` is broken in-game at once, whatever a stale pack still holds, and art imported
after the last cook renders at once.**

- **Validators read `Assets/`.** `validate_mesh_refs.tpac_paths_for_modules` scans a module's loose `Assets/**`
  whenever it has one and falls back to cooked packs only for a module with no loose tree (Native).
  `validate_moduledata.py` and `audit_armory_refs.py` call it rather than choosing a tree themselves, so a `PASS` is
  against the tree the game loads.
- **`audit_deleted_mesh_impact.py` has no cooked side to diff** when `AssetPackages` is absent. It derives "gone"
  from the reference side instead, which cannot see art deleted while nothing referenced it.
- **The dedicated server reads a third folder.** The co-op server engine loads `<module>/DsAssetPackages/*.tpac` for every loaded module (`Rgl.dll` `get_asset_path_for_module`). It stages the Armory's ModuleData but not its art, so creature skeletons come from `Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac`, copied unchanged from the tree the client loads. After an Armory art sync, `audit_armory_refs.py` runs `build_ds_skeleton_pack.py --check` and warns on drift.

## Item and piece names need a Languages row

An artist drop often ships `name="{=KEY}English"` with no `<string id=KEY>` row in the Armory's
top-level `Languages/*.xml`, so the translator cannot see the name. `python tools/register_armory_names.py --check`
fails on any such key outside `tools/armory-unregistered-names-baseline.txt` (a ratchet that only
shrinks), and without a flag it lists the rows `--apply` would append to the right `loc_<folder>.xml`.
By default that covers only keys outside the baseline (what `--check` flags); `--include-baselined`
adds the backlog. `--prefix <regex>` matches a key or an element id, so an artist's new ids work.

Run `--check` before `check_external_loc_coverage.py`. Every row `--apply` writes raises that gate's
untranslated count by one per language, so run its `--update-baseline` in the same change; translating
the rows is a separate paid run. Names minted by `generate_starter_kit.py` (the `starter_kit.xml`
item files and the TAOM-STARTER-KIT block of the pieces file) are out of scope for this tool.

## Deleted on 2026-08-28 (do not re-reference)

Four `lotraom-assets` commits removed 755 asset files. What that means for authoring:

- **No Erebor team colours.** All 57 `sk_dwarf_erebor_*_{blue,green,red}` items are gone
  from both Armory copies. Use the base id and let `Flags/UseTeamColor` do the work; that
  is what made the per-colour meshes redundant in the first place.
- **No Easterling armour set.** `easterling_*` and `easterlingwarriors0N_*` are dead art.
  Rhûn's living set is Loke-Rim (`sk_rh_loke_*`) and Dragon-Wrath (`sk_rh_drag_*`).
- **No bespoke Gondor lord kits.** `angbor_*`, `forlong_*`, `golasgil_*`, `hirluin_*`,
  `imrahil_*` and `lossarnach_coat` are dead. Dress named Gondor lords from their
  region's own prefix instead.

  Two corrections to this list, both verified 2026-09-01:

  - **`ar_ardunian_elite_armour` is NOT dead** and was wrongly swept in here. It is
    still defined in `mordor/body_armors.xml` as "[Mordor] Black Numenorean Armour",
    its mesh `sm_md_num_inf_chest_elite_a` is `referenced=Y` in the catalogue, and
    Umbar's corsair boss wears it today. Only the other `ar_ardunian_*` ids went.
  - **These lost their item DEFINITIONS, not just their meshes.** The row above used
    to say "the item definitions survive but nothing references them". They do not
    survive: grepping all of `LOTRLOME_items/` for `angbor|forlong|golasgil|hirluin|imrahil`
    returns nothing. Referencing one is a hard missing-item (`BROKEN_ITEM_REF`), not a
    naked mesh, so the two failure modes are not interchangeable.

**Gondor regional slot coverage is uneven**, which matters when dressing a lord: Dol Amroth
(`sk_gd_dol_`) is the only region shipping all five slots. Lamedon and Anfalas ship no
gloves, greaves or cape; Lossarnach and Pinnath Gelin ship no greaves. The generic
lord-tier fallbacks are `sk_gd_sere_bracer_lord_a` and `sk_gd_sere_grvs_lord_a`.

## LODs in the FBX sources

**The rule (Mike, 2026-09-25): every mesh carries LOD0 through LOD5, with no gaps; `clo_` cloth
meshes and `bo_` collision bodies need none.** A missing level is drawn as the level before it,
so a gap costs as much as no LOD at that range. What still falls short is listed in
[lod-audit.md](armory-catalogue/lod-audit.md) (`tools/audit_fbx_lods.py`, read from
`AssetSources`); `python tools/lod_fill_batch.py --all --apply` fills every gap it can, one FBX
at a time, and installs only what verifies.

- **Naming.** LOD0 is the bare object name or `<name>_lod0`, LOD N is `<name>.lodN` or
  `<name>_lodN`, and the Kit folds both into one metamesh (the catalogue holds only
  `dwarf_hair_a` for `Dwarf_Hair_A_lod0` to `_lod3`). `<mesh>.<part>` is a sub-mesh with its
  own chain, not a LOD. A name the Kit cannot fold (`.lod` with no number, `.lod2.001`, `.od2`)
  or a family of numbered parts with falling triangle counts (`.base`, `.base1` to `.base5` on
  `SK_GB_Uruk_Cape_Pauldron_Elite_A`) ships as extra sub-meshes drawn all at once.
- **Only LOD0 carries morphs.** Every race head and hair has 101 face-morph channels on LOD0
  and none below. A head with 100 (the goblin, until 2026-09-25) has every facegen slider one
  morph off if the Kit maps them by order.
- **Budgets.** The elf and dwarf basemeshes and dwarf hairs A to E run 70/30/15/7/3% of LOD0.
  KEYforce's creature gear budgets (2026-09-25): warg fur 18k/11k/5k/2k/1k then a single hidden
  plane; orc rider saddle 15k/10k/5k/2k/500/100 with the metal fittings dropped from LOD2 and
  everything but the seat from LOD4.
- **A missing level is filled by the one before it** (KEYforce's reading of the Kit: the warg
  fur's L0, L1, L4, L6 chain drew 11k at LOD1 to 3). So a mesh that should vanish at range needs
  an explicit last level, a single hidden plane, or it keeps drawing its last real LOD.
- **Filling a chain.** A gap is filled geometrically between its two neighbours; below the last
  level the standard step continues. The artist's own levels are never touched, and a level past
  5 is left alone. Seams lock on the body parts a race skin stitches, never on hair.
- **Re-import risk.** Five Armoury FBX carry a skeleton into their own tpac (the elephant, the
  chariot, the warg, `keyforce_dwarf`, the hill troll). A Kit re-import regenerates that
  package, and the elephant's and chariot's fitted hit capsules (#624) were patched straight into
  it. Back the tpac up first (the fill batch does), compare the skeleton after, and restore it
  with `tools/tpac_skeleton_swap.py` only if it really changed. Compare the DECOMPRESSED segments
  (`read_segment_data` in `tools/tpac_skeleton_dump.py`): the LZ4 bytes differ on every compile.
  On 2026-09-25 all five kept their capsule, body and ragdoll segment byte-for-byte, and the bone
  segment moved only by FBX round-off (under 1e-4), which is no reason to restore.
- **Import dialog settings persist.** The Kit's FBX import dialog keeps its last choices in the Windows registry, `HKCU\Software\TaleWorlds\RGL\fbx_import_settings` (a `QSettings("TaleWorlds", "RGL")` group whose keys are the field names), so what a file gets depends on the previous import: check every flag before each import. First-run defaults there are unit `mm`, `convert_to_z_up` on, `as_vertex_anim`, all six import flags on and `remove_shape_type_names` off. (yotthani, MithrilForge `docs/engine/modding-kit.md`@0a2f4761 lines 109-118, Kit decompile 1.5.3. **TAOM:** the key exists on the desktop with `import_physics_shapes`, `convert_to_unit` and the other fields, `reg query` 2026-10-15; its values are the last import's, not the defaults.)
- **Materials on a re-import.** The Kit keeps the material already bound to a mesh name it knows,
  so an old, hand-set binding survives. A NEW mesh name (every added LOD) binds to the material
  its FBX slot names, and when the Kit has no material by that name the level renders unbound
  (`MetaMesh( x.lod3 ) : Unable to find material ...` in the rgl log). Before adding levels, set
  each new level's slot to the Kit material its LOD0 is bound to today
  (`tools/lod_material_fixes.json`, `lod_fill_batch.py --materials`). A multi-material mesh
  compiles to one record per material (`x.0`, `x.1`), so a binding check looks for those, not the
  bare name.
- **Authoring and proof.** `tools/blender/add_mesh_lods.py` writes the levels;
  `audit_fbx_lods.py --diff <old> <new>` proves nothing else moved, bind pose included (a Blender
  export rewrites the file's axis system, so raw bind matrices always differ; compare relative
  to a reference bone).
- **Known defect: `sk_dwarf_beard_a_10`.** The FBX is fine, but `SK_Dwarf_Beards_geo.tpac`
  registers that metamesh with `_beard_a` replaced by eight NUL bytes, so the engine logs
  `Meta mesh with name sk_dwarf_beard_a_10 cannot be found!` and a dwarf who rolls beard 10 has
  none. A Kit re-import of `Race Test/Beards/SK_Dwarf_Beards.fbx` rewrites the name.

## Cloth simulation meshes

Kysaro measured these (his notes `armor/project_baf_native_cloth_sim.md`, a survey of 275 `clo_` sims in 98 TAOM Armory
FBX files on 2026-10-22, and `armor/project_bannerlord_armor_factory.md`, 2026-10-18), unverified in TAOM.

- **A native sim is a quad ring grid on the render surface.** Median quad share 1.00, typically two boundary loops (a
  waist ring and a hem make a tube), edges about 4.6 cm, sim vertices a median 0.23 cm from the render surface. His
  outer-envelope sim, which stood off the fabric, shredded; his triangle-soup sims were unlike every native one.
- **The `clo_` sim is a separate loose mesh.** The FBX holds `<asset>` with its LODs, `<asset>.skirt` (plus
  `.skirt.lodN`) as the render skirt, and `clo_<asset>_Skirt` as the sim: about 800 triangles, no LODs, skinned, and it
  needs a material (the engine reports "Unable to find material" otherwise). A rigid chest must not keep parts of a
  skirt that has become cloth.
- **The top rows are pinned with vertex alpha 0.** Alpha 0 at the waist anchor holds the sim to the chest; alpha 1 lets
  it move. In the Armory convention alpha runs from 0 at the waist (z of 0.9 and up) to 1 at z of 0.5 and below. A sim
  top that is not painted down to alpha 0 is not pinned.
- **After a sim topology change the reimport fails with "Unable to update cloth mapping data (RDC)".** His fix: unlink
  the sim (set it to None), save, reimport, relink, then re-cook.

### What the engine does with a cloth item (yotthani's findings, not re-checked by TAOM)

All of this is from yotthani's race-fit work, measured on installed game data around game 1.5.5 to 1.5.6 (the sources do
not name a build or a PC), and TAOM has checked none of it on v1.5.7. Sources: `yotthani/MithrilForge`
`src/MithrilForge.TpacFormat/Assets/ClothMapping.cs@ea7a7bfb:9-70`, `docs/engine/modding-kit.md@ea7a7bfb:421-441`,
`src/MithrilForge.Crowd/CrowdCommand.ClothRefit.cs@ea7a7bfb`, and `yotthani/bannerlord` `HoN/RaceArmorFit` (README, commits).

- **A render vertex whose colour alpha is above 0 is drawn from the simulation; alpha 0 is an ordinary skinned vertex.**
  yotthani rebuilt the cloth vertex shader (`rgl_cloth_transform`) on the CPU from the game's shader sources: such a vertex
  takes three particles and four weights from the cloth map, at `sum(b*P) + w*normalize(sum(b*N))`, with that normal as its
  shading normal (negated when index 0 is above index 1). Particles below `normal_update_start_index` keep their rest
  normal. The identification of buffer 8 (painted weight, 0 on pins; the leash is `max_distances x multiplier`) and buffer
  9 (1 / summed triangle area) is "by elimination from the shader, not from the engine's binding code" in the source.
- **The cloth map is indexed by edit-data face corner**: one entry per corner, as three u16 particle indices and four floats
  (three barycentrics and `w`). Corners with alpha 0 need an entry too, because the engine asserts the map length. Rebuilt at
  rest it reproduces the edit data to a p95 of 0.13 to 0.29 mm when `w` runs along the sim mesh's authored corner normals; with
  the engine's computed normals the awnings drifted to a p95 of 6 to 7 mm. A changed sim mesh therefore needs a new map, not only
  a new cook. A double-sided cloth mesh holds each triangle twice with reversed winding, so an offline renderer shows facets
  (`docs/engine/modding-kit.md@ea7a7bfb:431-441`, commit `d81dd7e`).
- **Particle indices are 16 bits**, so a sim mesh has at most 65,536 particles (`ClothCooker.cs@ea7a7bfb:55`); the cape
  already has 1,363 at vanilla resolution. A 1:4 subdivision reaches the ceiling quickly. TAOM's sims (about 800 triangles
  above) are far from it.
- **Not every cloth item has a separate `clo_` metamesh.** The Erebor chain mail keeps its cook in its visible mesh (commit
  `1b4122a`; `CrowdCommand.ClothSelfCarrier.cs@ea7a7bfb:12-17`). Check the mesh's cloth flags (`uses_cloth_simulation`,
  `force_enable_cloth`) before assuming a `clo_` sim. A sim metamesh that stores no cook (`clo_mirkwood_shoulder`) is cooked
  by the engine from its geometry on load; that the engine orders the particles as the kit cooker does is marked ASSUMPTION
  in the source (`ClothRefit.cs:406-409`), supported only by the cooker reproducing the stored cooks byte for byte.
- **At run time `MetaMesh.HasClothData()` recognises cloth garments reliably and `Mesh.HasCloth()` is true for every mesh**
  (RaceArmorFit README, the later of two contradicting commits; see
  [head-mesh-and-groom-authoring.md](head-mesh-and-groom-authoring.md) "Run-time edits of armour meshes").
- **The simulation pulls a fitted garment back to the human shape stored in the item's cloth asset** after a civilian/battle
  view switch (commit `b91e53cd`, seen in game 16 to 18 October 2026). Pinning every cloth simulator of the agent's skeleton
  fixed the look: `SetMaxDistanceMultiplier(0)` (pinned) or `0.3` (loose) plus `SetResetRequired()`, as vanilla pins capes
  (`Agent.CheckEquipmentForCapeClothSimulationStateChange`, `FieldFit.cs@c45d8374:257-258`). Because every `12D6` crash involved a fitted cloth item,
  the mod later stopped editing meshes flagged for cloth simulation and set its cloth-sim taming (`ClothSimMode`: none, reset,
  pin, loose) to `none` by default (commit `6502bd15`; `FieldFit.cs@c45d8374:141-143`). Its spec adds that cloth bodies bind to
  a skeleton through `owner_skeleton` and that vanilla meshes carry a 200-particle map; a refined cook needs a cloth map per LOD.
- **A carrier metamesh for a race fit** [yotthani, `ClothRefit.cs@ea7a7bfb:257-259, 364-376`]: the race's cook goes in as
  `clo_<name>_<race>` next to a carrier `<visible>_<race>_cloth` that holds only the smallest LOD and points at that cook; the
  original coat stays untouched for the other races. The engine knows only its own metamesh flags, and yotthani removed an
  unknown flag as a "load risk" (commit `1478d8d`). A simulated LOD0 is not what such a fit moves, so yotthani's coat QA
  evaluates LOD1 "as the game fits them" unless `--lod0 yes` is given (`CrowdCommand.CoatQa.cs@ea7a7bfb:64, 73`).

### Fitting human armour to another build: eight failure mechanisms

yotthani met these fitting dwarf armour to the human body and back (commit bodies in `yotthani/MithrilForge`, history up to @ea7a7bfb, game version
and PC not stated, sizes as given; "user 18.10." in the code comments marks an in-game report). TAOM fits
human armour to dwarves, orcs and uruks, and has verified none of this.

| Commit | Symptom | Cause | Fix |
|---|---|---|---|
| `6180d2d` | Rim vertices of Erebor scale and chain mail up to 10 mm inside the human wrist at a closed sleeve end | Min-gap smoothing: the cap joins the opposite rim, whose push points the other way | A vertex keeps its own push when the smoothed push would leave it under the gap |
| `e8fcca4` | Sleeve rim swings into the wrist when the hand bends | A dwarf sleeve end hangs about 20 % on the hand bone while the human skin under it follows the forearm | Take the body skin weights (`<BodySkin>`) for the rim |
| `71219c5` | Dark holes in a skirt modelled with thickness (15 pairs below the belt) | A shrinking field pushes the back side of a closed two-sided layer through the front | Keep half the original thickness for opposite-facing vertices within 15 mm |
| `eab99ee` | Streaks of light on a robe in game | The robe cook bent 11 to 16 degrees at every particle row between belt and hands (human: 2 to 5) | Gaussian z-smoothing of the field |
| `1ae2991`, `208f0eb` | Capes stand off and fold when walking; skirt back panel stands off | Cape trunk-depth scale 1.18; skirt hip-depth scale 1.14 | A `--depth-scale` option on both fields (cape heights are also mapped piecewise along ankle, knee, hip, waist, thorax, clavicle and head); the skirt hangs closer at 1.03 |
| `c4b75e1` ("Diagnosis 17.10.") | Knee-pit fold of the Gondor trousers | It comes from the human bone weights, not the leg heights: the "legz" mode leaves it, dwarf weights smooth it | None stated: the commit adds `--dwarf-weights` as a diagnosis switch |
| `eea07dc` | Nothing drawn between trousers and a lower boot | Trousers that end under a dwarf knee end too high on the longer human shin, and with `covers_body` the gap stays bare | Lower the girth meshes' leg ends, faded over a ramp (not at the midline, where a crotch flap would become a sheet), and pull the lower half of the band to the leg's own skin so the cuff goes into the boot |
| `9c9cc66` | Boots whose shaft is part of the main shell (no shaft plates) leave the leg bare: shaft cut by 7 to 12 cm (elite b: top 0.295 instead of 0.419 m) | When the shaft is part of the main shell, the shell blended from the foot map by mapped height up to 36 cm, and the foot map's height scale cut the shaft | The shell hands over from 8 to 23 cm original height above the sole |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/orientation.md](../ai-includes/orientation.md)
- [docs/ai-includes/weapon-creation-workflow.md](../ai-includes/weapon-creation-workflow.md)
- [docs/features/armoury-mesh-cleanup.md](../features/armoury-mesh-cleanup.md)
- [docs/features/chariot.md](../features/chariot.md)
- [docs/features/elephant.md](../features/elephant.md)
- [docs/features/gondor-armor-revamp.md](../features/gondor-armor-revamp.md)
- [docs/features/mesh-ref-validation.md](../features/mesh-ref-validation.md)
- [docs/features/multi-culture-armor-revamp.md](../features/multi-culture-armor-revamp.md)
- [docs/modding/editing-safely.md](../modding/editing-safely.md)
- [docs/modding/file-catalogue.md](../modding/file-catalogue.md)
- [docs/modding/id-cheatsheet.md](../modding/id-cheatsheet.md)
- [docs/modding/items-armor.md](../modding/items-armor.md)
- [docs/modding/items-mounts-and-harness.md](../modding/items-mounts-and-harness.md)
- [docs/modding/items-shields.md](../modding/items-shields.md)
- [docs/modding/items-weapons-and-crafting.md](../modding/items-weapons-and-crafting.md)
- [docs/modding/module-armory.md](../modding/module-armory.md)
- [docs/modding/npcs-notables-and-townsfolk.md](../modding/npcs-notables-and-townsfolk.md)
- [docs/modding/recipe-retire-content.md](../modding/recipe-retire-content.md)
- [docs/reference/armory-catalogue/README.md](armory-catalogue/README.md)
- [docs/reference/armory-shield-audit.md](./armory-shield-audit.md)
- [docs/reference/bannerlord-animation-system-map.md](./bannerlord-animation-system-map.md)
- [docs/reference/lotrlome-soln-id-fix.md](./lotrlome-soln-id-fix.md)
- [docs/reference/lotrlome-war-ram-changes.md](./lotrlome-war-ram-changes.md)
- [docs/reference/tpac-static-prop-authoring.md](./tpac-static-prop-authoring.md)
- [docs/reviews/lessons/xslt-moduledata.md](../reviews/lessons/xslt-moduledata.md)

<!-- backlinks-end -->
