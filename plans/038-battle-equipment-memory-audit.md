# Plan 038: Rank which equipment assets dominate a battle's memory, offline from the release packs

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat 0912e1b7..HEAD -- tools/audit_map_scene_memory.py tools/tests/test_audit_map_scene_memory.py tools/tests/test_ci_runner_compat.py tools/_gamedir.py tools/README.md`
> must print nothing, and `git ls-files tools/audit_battle_equipment_memory.py tools/tests/test_audit_battle_equipment_memory.py`
> must print nothing (both files are new). If an in-scope file changed since this plan was written,
> compare the "Current state" excerpts with the live code; a mismatch is a STOP condition.
>
> **Decided 2026-10-03 (FOR-MIKE 16s, with the Codex review):** under `--loose-assets` the engine's rule applies, not
> the packed-first order that Steps 2 and 3 below specify: a module that ships a loose `Assets` tree reads only that
> tree (its cooked packs are not read, and the run reports each one as COOKED_TREE_NOT_READ), and a module with none
> keeps its packs. The Step 2 test `test_loose_tree_is_indexed_after_the_pack_trees_when_asked` is replaced by
> `test_loose_tree_replaces_the_pack_trees_of_a_module_that_has_one`. The Step 9.4 STOP condition (an undecoded loose
> header) was reached: 115 of the live Armory's 2,595 loose textures carry metadata version 2, which the shared decoder
> rejects, and the default population reaches 82 of them. The tool reports each as TEXTURE_SIZE_UNKNOWN, counts it at
> 0 bytes and says in the report that every total is then a lower bound; decoding version 2 is a research follow-up
> outside this plan. Equipment placement follows `Equipment.IsItemFitsToSlot` now (the plan's equipment model assigned
> every item to the slot the XML names). The tool, its tests and the README row carry all three; the sections below are the first
> build's record.

## Status

- **Priority**: P2
- **Effort**: L
- **Risk**: LOW (a new read-only offline tool plus one opt-in parameter on an existing read-only tool;
  nothing runs inside the game, no C# and no ModuleData change)
- **Depends on**: none
- **Category**: perf (memory tooling)
- **Planned at**: commit `0912e1b7`, 2026-10-02
- **Baseline at that commit**: dotnet `Failed! - Failed: 1, Passed: 12345, Skipped: 2, Total: 12348`
  (net472, measured by the orchestrator at `dffdf879`; the commits since touch only documentation and
  run records), failing `EveryLanguage_DeclaresARowForEveryEnglishKey` (English keys without rows in the
  other languages; the paid translator run waits on the maintainer). Python suite
  (`python -B -m unittest discover -s tools/tests -t .`): `Ran 2962 tests`, `FAILED (failures=3, skipped=8)`,
  failing `test_applying_every_spec_is_a_no_op`, `test_the_committed_career_file_is_what_the_rule_derives`
  and `test_default_is_on_the_e_drive` (the last depends on the worktree path). The map tool's own
  tests are pytest-only and outside that run: `python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider`
  gives `33 passed` (writer's run at the planned-at commit). Trunk CI under reference assemblies also
  fails `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`
  (`FileNotFoundException` for `TaleWorlds.MountAndBlade.View`); not reproducible locally, not yours.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Each battle takes about 2 to 3 GB of native memory and gives it back afterwards
(`docs/features/battle-load-diagnostics.md:420-427`: "across six full mission cycles the
`MissionInitialize` baseline held ... each battle costing ~2-3 GB and returning it"), and the main
menu floor attributes about 970 MB to `LOTRLOME_Armory` alone
(`docs/investigations/native-commit-audit-2026-08.md:685-707`, rung L2). No tool says which items,
meshes or textures carry that weight, so every memory lever in TAOM's own content (the Armory's
armour, weapons, hair and beards) is chosen by guess. The campaign map already has such a tool
(`tools/audit_map_scene_memory.py`); this plan adds its battle counterpart: for every troop, and for a
chosen pair of armies, it resolves the equipment the engine preloads to metameshes, materials,
textures and collision bodies in the release packs and sums the bytes, counting a shared asset once.
When it lands, a content pass can start from a ranked table of the textures and meshes a given
battle (say Gondor against Mordor) preloads, with the items and troops that pull each one in,
instead of from intuition.

## Current state

### Files and their roles

- `tools/audit_map_scene_memory.py` (1,386 lines): the map scene audit. It owns the tpac
  table-of-contents walker, the per-module asset index, the texture, material and metamesh metadata
  decoders and the texture size formula. The new tool imports these; it must not copy them.
  Its outputs must not change (a byte-identical check in Step 3 proves it).
- `tools/tests/test_audit_map_scene_memory.py` (617 lines): its tests. They `import pytest`
  (line 20) and use `tmp_path`, so CI's `unittest discover` never runs them; they run only under
  pytest. Do not add tests to it.
- `tools/tests/test_ci_runner_compat.py`: a ratchet that fails when a NEW tool test module imports
  pytest (`PYTEST_BASELINE`, lines 14 to 20). Your new test module must be plain
  `unittest.TestCase` with `tempfile` and `contextlib.redirect_stdout`, no pytest import.
- `tools/_gamedir.py`: `game_dir(default)` (line 36) returns `$BANNERLORD_GAME_DIR` when set, else
  the default; reuse it for the game install default.
- `tools/README.md`: the tools catalogue; the map tool's row is line 62.
- New: `tools/audit_battle_equipment_memory.py` and
  `tools/tests/test_audit_battle_equipment_memory.py`.

### The map tool pieces you will reuse (excerpts at `0912e1b7`)

`tools/audit_map_scene_memory.py:145`:

```python
PACK_TREES = ("AssetPackages", "EmAssetPackages")
```

`tools/audit_map_scene_memory.py:287-313` (the only function this plan changes in the map tool):

```python
    def add_module(self, module: str, module_dir) -> None:
        """Index `AssetPackages/*.tpac`, then `EmAssetPackages/**/*.tpac`, of one module.
        ...
        """
        module_dir = Path(module_dir)
        found = False
        for sub in PACK_TREES:
            tree = module_dir / sub
            if not tree.is_dir():
                continue
            found = True
            packs = sorted(p for p in tree.rglob("*") if p.is_file() and p.suffix.lower() == ".tpac")
            for pack in packs:
                label = pack.relative_to(module_dir).as_posix()
                self.packs.append((module, label, pack.stat().st_size))
                try:
                    for item in iter_tpac_items(pack, module):
                        item.pack = label
                        self.add_item(item)
                except (OSError, ValueError, struct.error) as exc:
                    self.errors.append((module, label, f"{type(exc).__name__}: {exc}"))
        if not found:
            self.errors.append((module, str(module_dir), "no AssetPackages or EmAssetPackages directory"))
```

Other names you import (Step 3 adds only the `trees` parameter to `AssetIndex.add_module`): `AssetIndex` (class at :272; `items` keyed `(kind, lowercase name)`,
`by_guid`, `counts` keyed `(module, kind)`, `packs`, `errors`, `get(kind, name)` at :331, first module
added wins a name), `SEG_MESH_A` (:129, `5f98413d...`, edit data), `SEG_MESH_B` (:130, `97f81dbb...`,
render buffers), `ZERO_GUID` (:132), `scan_guids(meta, by_guid, kind)` (:433), `parse_material_meta`
(:411), `parse_metamesh_records(meta, metamesh_name)` (:452), `plausible_counts(rec, by_guid)` (:477),
`texture_chain_bytes` (:377), `_texture_row(item, index, referenced_by)` (:766; returns a dict with
`format`, `width`, `height`, `mips`, `resident_bytes`, `size_source` and a `flags` list such as
`UNCOMPRESSED`, `NO_MIPS_ABOVE_512`, `PIXELS_NOT_IN_PACKS`, `FULL_CHAIN_ONLY_IN_EMASSETPACKAGES`,
`FORMULA_MISMATCH`), `fmt_bytes` (:1121), `write_tsv(path, rows, columns)` (:1141),
`refuse_inside_game(out_path, game_root)` (:1340). `build_manifest(game_modules, scene_dir,
modules=DEFAULT_MODULES, extra_flora_kinds=())` is at :811 and calls `index.add_module(module,
game_modules / module)` at :816-818; `main` is at :1348-1382.

`_texture_row`'s size rule (:795-807): the pixel segment's decompressed size when the pack carries
it, else the header formula (flag `PIXELS_NOT_IN_PACKS`), else the stub. The docstring (:39-44) records
that the formula matched the pixel segment to the byte on 3,582 of 3,583 checkable textures.

### What the engine preloads for a battle (v1.5.3, quoted from `pwsh tools/taom-src.ps1 path <Type>`)

`SandBox.View.Missions.MissionPreloadView.OnPreMissionTick` (lines 18-39) adds every troop of every
party in `MapEvent.PlayerMapEvent.InvolvedParties` (once per head) and calls
`_helperInstance.PreloadCharacters(list)`. Lines 40-44 then call
`_helperInstance.PreloadItems(missionBehavior.GetSiegeMissiles())` when the mission has a
`SiegeDeploymentMissionController`; the audit does not model those siege missiles (Step 10,
APPROXIMATIONS).

`TaleWorlds.MountAndBlade.View.PreloadHelper` (lines 20-31, 112-162):

```csharp
public void PreloadCharacters(List<BasicCharacterObject> characters)
{
    Utilities.EnableGlobalEditDataCacher();
    foreach (BasicCharacterObject character in characters)
    {
        foreach (Equipment battleEquipment in character.BattleEquipments)
            AddEquipment(battleEquipment);
        if (Mission.Current != null && Mission.Current.DoesMissionRequireCivilianEquipment)
            foreach (Equipment civilianEquipment in character.CivilianEquipments)
                AddEquipment(civilianEquipment);
        ... GetExtraEquipmentElementsForCharacter ...
    }
    Utilities.DisableGlobalEditDataCacher();
    PreloadMeshesAndPhysics();
}

private void AddItemObject(ItemObject item)
{
    ...
    RegisterMetaMeshUsageIfValid(item.MultiMeshName, isUsingTableau, isUsingTeamColor);
    RegisterMetaMeshUsageIfValid(item.HolsterMeshName, isUsingTableau, isUsingTeamColor);
    if (item.WeaponComponent != null)
    {
        if (item.IsCraftedWeapon)
        {
            RegisterMetaMeshUsageIfValid(item.GetHolsterWithWeaponMeshIfExists(), ...);
            RegisterMetaMeshUsageIfValid(item.GetHolsterMeshIfExists(), ...);
            RegisterMetaMeshUsageIfValid(item.GetFlyingMeshIfExists(), ...);
        }
        else
        {
            RegisterMetaMeshUsageIfValid(item.HolsterWithWeaponMeshName, ...);
            RegisterMetaMeshUsageIfValid(item.HolsterMeshName, ...);
            RegisterMetaMeshUsageIfValid(item.FlyingMeshName, ...);
        }
    }
    else if (item.HasHorseComponent)
    {
        foreach (KeyValuePair<string, bool> additionalMeshesName in item.HorseComponent.AdditionalMeshesNameList)
            RegisterMetaMeshUsageIfValid(additionalMeshesName.Key, ...);
    }
    else if (item.HasArmorComponent && !string.IsNullOrEmpty(item.ArmorComponent.ReinsMesh))
    {
        RegisterMetaMeshUsageIfValid(item.ArmorComponent.ReinsMesh, ...);
        RegisterMetaMeshUsageIfValid(item.ArmorComponent.ReinsRopeMesh, ...);   // ReinsMesh + "_rope"
    }
    if (!_loadedItems.Contains(item))
    {
        RegisterMetaMeshUsageIfValid(item.GetMultiMesh(isFemale: false, useSlimVersion: false, needBatchedVersion: true), ...);
        if (item.HasArmorComponent)
        {
            RegisterMetaMeshUsageIfValid(item.GetMultiMesh(isFemale: false, useSlimVersion: true, needBatchedVersion: true), ...);
            RegisterMetaMeshUsageIfValid(item.GetMultiMesh(isFemale: true, useSlimVersion: false, needBatchedVersion: true), ...);
            RegisterMetaMeshUsageIfValid(item.GetMultiMesh(isFemale: true, useSlimVersion: true, needBatchedVersion: true), ...);
        }
        _loadedItems.Add(item);
    }
    RegisterPhysicsBodyUsageIfValid(_uniqueDynamicPhysicsShapeName, item.CollisionBodyName);
    RegisterPhysicsBodyUsageIfValid(_uniqueDynamicPhysicsShapeName, item.BodyName);
    RegisterPhysicsBodyUsageIfValid(_uniqueDynamicPhysicsShapeName, item.HolsterBodyName);
}
```

So the preload set of an armour item is all four gender and slim variants regardless of the troop's
own gender: the audit takes their union. The helper keeps every name it registers in `HashSet`s
(`_uniqueMetaMeshNames`, `_uniqueDynamicPhysicsShapeName`, `_loadedItems`, lines 12-18), so a name
registered twice (the crafted blade's `body_name` given as both body and holster body, or one
metamesh reached by several gender lists) is loaded once: the audit keeps each resolved asset once
per item.

`TaleWorlds.MountAndBlade.View.ItemCollectionElementViewExtensions.GetMultiMesh` (lines 31-47):
`flag = item.ArmorComponent.MultiMeshHasGenderVariations` (armour only, else false), then
`GetMultiMeshCopyWithGenderData(flag && isFemale, useSlimVersion, needBatchedVersion)`, falling back to
`item.GetMultiMeshCopy()` when that returns null or a metamesh with `MeshCount == 0` (line 42). Offline,
a name absent from the packs plays the part of both cases; the fallback is the item's own `mesh`,
which every gender and slim candidate list below already ends in.

`TaleWorlds.MountAndBlade.View.ItemObjectViewExtensions.GetMultiMeshCopyWithGenderData` (lines 42-72):

```csharp
MetaMesh craftedMultiMesh = itemObject.GetCraftedMultiMesh(needBatchedVersion);
if (craftedMultiMesh != null) return craftedMultiMesh;
if (string.IsNullOrEmpty(itemObject.MultiMeshName)) return null;
val = MetaMesh.GetCopy(isFemale ? (itemObject.MultiMeshName + "_female") : (itemObject.MultiMeshName + "_male"), false, true);
if (val != null) return val;
string multiMeshName = itemObject.MultiMeshName;
multiMeshName = ((!isFemale) ? (multiMeshName + (useSlimVersion ? "_slim" : ""))
                             : (multiMeshName + (useSlimVersion ? "_converted_slim" : "_converted")));
val = MetaMesh.GetCopy(multiMeshName, false, true);
if (val != null) return val;
val = MetaMesh.GetCopy(itemObject.MultiMeshName, true, true);
if (val != null) return val;
return null;
```

`TaleWorlds.Core.ArmorComponent`: `MultiMeshHasGenderVariations = true;` then overridden by
`has_gender_variations` when present (lines 159-162); `ReinsMesh` from `reins_mesh` (line 195,
default `""`); `ReinsRopeMesh => ReinsMesh + "_rope"` (line 113).

`TaleWorlds.Core.ItemObject.Deserialize` reads `mesh` (MultiMeshName, :503-506), `holster_mesh` (:508),
`holster_mesh_with_weapon` (:509), `flying_mesh` (:510), `body_name` (:525), `holster_body_name` (:528),
`shield_body_name` (CollisionBodyName, :529). For a crafted item `InitCraftedItemObject` (:355-368) sets
`MultiMeshName = ""`, `BodyName = bladeData?.BodyName`, `HolsterBodyName = bladeData?.HolsterBodyName ??
bladeData?.BodyName`, where `bladeData = craftedData.UsedPieces[0].CraftingPiece.BladeData` (the Blade
piece).

`TaleWorlds.MountAndBlade.View.CraftedDataView`: the weapon mesh is built from
`MetaMesh.GetCopy(val2.CraftingPiece.MeshName, true, false)` for each used piece (line 148); the
holster mesh from `MetaMesh.GetCopy(bladeData.HolsterMeshName, false, false)` of the Blade piece
(lines 224-230). `TaleWorlds.Core.CraftingPiece` reads `mesh` (:159) and a `<BladeData>` child (:234);
`TaleWorlds.Core.BladeData.Deserialize` reads `body_name`, `holster_mesh`, `holster_body_name`
(:50-52).

`TaleWorlds.Core.HorseComponent` (lines 157-206): `<Materials><Material name=...>` (one picked per
horse) and `<AdditionalMeshes><Mesh name=...>`.

`TaleWorlds.Core.BasicCharacterObject.Deserialize` (lines 365-418), the equipment rules the audit
must follow:

- the container is `<Equipments>` or `<equipments>` (line 365); under it, every direct `<equipment>`
  child (lowercase only, line 370) is collected as a slot override;
- `<EquipmentRoster>` adds one inline set (`MBEquipmentRoster.InitEquipment`: `equipmentType`
  attribute, else legacy `civilian="true"`, else Battle). A lowercase `<equipmentRoster>` passes the
  name test at line 377, but `MBEquipmentRoster.Init` (lines 44-54) builds a set only for a node named
  `EquipmentRoster`, so it adds no set (no release or vanilla file uses the lowercase form);
- `<EquipmentSet>` or `<equipmentSet>` takes `id` and adds every set of that standalone roster whose
  type equals this node's type (`equipmentType`, else legacy `civilian`, else Battle;
  `MBEquipmentRoster.AddEquipmentRoster`, lines 110-118). The types are `Battle`, `Civilian` and
  `Stealth` (`Equipment.EquipmentType`); a `Stealth` set is neither battle nor civilian, so the audit
  never includes it;
- after that, each override `<equipment>` is applied to every set (`AddOverriddenEquipments`,
  lines 121-136), replacing the item in the slot it names;
- `BattleEquipments => AllEquipments.WhereQ(e => e.IsBattle)` (line 106).

`TaleWorlds.Core.MBEquipmentRoster.Deserialize` (lines 55-85): a standalone `<EquipmentRoster id>`
holds `<EquipmentSet>` children, one set each. `TaleWorlds.Core.Equipment.DeserializeNode`
(lines 204-224) reads `id` (the part after the first `.` when present) and `slot` from every
non-comment child, any element name; `GetEquipmentIndexFromOldEquipmentIndexName` (lines 225-236) maps
`Item0`..`Item3` to `Weapon0`..`Weapon3` and `Item4` to `ExtraWeaponSlot`, and keeps every other slot
name (`Head`, `Body`, `Leg`, `Gloves`, `Cape`, `Horse`, `HorseHarness`) as written. An override
`slot="Item0"` and a set's `slot="Weapon0"` therefore name the same slot.

Edit data: `PreloadCharacters` runs inside `Utilities.EnableGlobalEditDataCacher()` /
`DisableGlobalEditDataCacher()`, both native. The v1.5.3 shipping client reads a mesh's edit data
(segment `5f98413d`) only on demand, through "ensure edit data" (`0x68D30`) and an async request
manager (`0x181CD0`); some callers are character paths (face and body generation `0x56F360` and
`0x574C30`, and `0x583350` "Scale %f %f %f, deform %s") (run evidence
`plans/_audit/2026-10-02-perf/evidence/native/native-mesh-editdata.txt`, "Callers"). **Whether worn
armour, hair and beards load their edit data in a battle is UNVERIFIED.** The audit therefore
reports render buffers as the floor and edit data as a flagged upper bound; it does not settle the
question.

### What the packs hold (writer's probes against the testing channel, 2026-10-02)

The testing release (`<releases>\testing\Modules`, where `<releases>` is the release channels folder
`docs/reference/release-process.md:114` names as `<releases>\<channel>\Modules\`) ships `LOTRLOME_Armory`, `TAOM`, `TAOM.Dependencies` and `TAOM_Map`.
Its Armory has 10 packs in `AssetPackages/` (4,549 metameshes, 2,595 textures, 950 materials, 393
physics shapes). Native and SandBox packs come from the game install; `SandBoxCore` has no pack tree
(its item meshes live in Native's packs). Indexing the Armory and TAOM release packs plus Native and
SandBox (1,203 packs) took under a second on a warm cache.

Sampled resolutions (the oracle for Step 9; re-derived by the writer with the map tool's index, and
again in plan review by applying Step 6's candidate table to the release XML and the same four pack
modules: with each name kept once the five rows print exactly as below, while without
de-duplication the chainmail lists `sk_gd_los_inf_chainmail_a` three times and the axe its blade body
twice):

| Item | Kind | Expected metameshes in the packs | Expected bodies |
|---|---|---|---|
| `sk_gd_los_inf_chainmail_a` | Item, BodyArmor, `mesh="sk_gd_los_inf_chainmail_a"` | `sk_gd_los_inf_chainmail_a;sk_gd_los_inf_chainmail_a_slim` (no `_male`, `_female`, `_converted*` variant exists) | none |
| `sk_gd_ano_boots_a` | Item, LegArmor | `sk_gd_ano_boots_a` | none |
| `sk_gd_ano_inf_helmet_med_a` | Item, HeadArmor | `sk_gd_ano_inf_helmet_med_a` | none |
| `sm_gd_shield_a1` | Item, Shield, `body_name="bo_cap_sm_gd_shield_a"`, `shield_body_name="bo_sm_gd_shield_a"` | `sm_gd_shield_a1` | `bo_cap_sm_gd_shield_a;bo_sm_gd_shield_a` |
| `wm_gondor_lossarnach_1h_axe_light` | CraftedItem, `crafting_template="OneHandedAxe"`, pieces `wm_lossarnach_1h_axe_black_ash_blade` (Blade, `body_name="bo_wm_lossarnach_1h_axe__blade"`) and `wm_lossarnach_1h_axe_black_ash_handle` | `wm_lossarnach_1h_axe_black_ash_blade;wm_lossarnach_1h_axe_black_ash_handle` | `bo_wm_lossarnach_1h_axe__blade` |

Measured segment sizes for two of them: `sk_gd_los_inf_chainmail_a` render 1,925,634, edit
2,201,032, plus a fourth segment type `3e6141af` of 174,030 bytes (it rides cloth-simulated armour;
type unknown); `dwarf_hair_a` (a dwarf hair mesh from the Armory skins) render 6,570,012 and edit
78,919,980 bytes. Its material `m_dwarf_beards_b` resolves to 1024 and 512 textures.

Metamesh record names: across the Armory's 4,549 metameshes the record name tails are `.lodN.N`
(29,460), `.lodN` (11,542), `.N` (6,062), none (1,712), and part names such as `.skirt.lodN`. A record
with no `.lod<N>` part, or `.lod0`, is LOD 0. On cloth armour the LOD 0 records do not decode
(`sk_gd_los_inf_chainmail_a.0` reads positions 0, faces 0 and a material "guid" made of string bytes);
`plausible_counts` rejects them, so LOD 0 faces are UNKNOWN there, while the `.lod1` and later records
decode. The material guid scan (`scan_guids(meta, by_guid, "material")`) still finds the materials.

Loose trees: the live Armory in the game install has no `AssetPackages/` and 5,454 loose
`Assets/**/*.tpac`. Their metameshes carry segment `5f98413d` (53,096 segments) and the table segment;
only 13 carry `97f81dbb`. Their textures carry two small segment types (`e25b477c`, 114 bytes, and
`a781b98a`, 38 bytes) and no pixel segment, so a loose texture's bytes come from its header formula
(`PIXELS_NOT_IN_PACKS`) and a loose mesh's render-buffer bytes are unknown.

### ModuleData shapes (release XML, read by the writer)

- Game types: an `XmlNode` may hold an `<IncludedGameTypes>` child listing `<GameType value=...>`.
  Native registers `mpitems` (Items), `native_equipment_sets` (EquipmentRosters) and
  `mp_crafting_pieces` (CraftingPieces) for `MultiplayerGame` only (`Native/SubModule.xml:80-90,
  131-135`), and CustomBattle registers `custombattlecharacters` for `CustomGame` and `EditorGame` only.
  A campaign battle loads none of them. Writer's probe over the four registration ids this tool reads:
  Native keeps 1 and drops 3, CustomBattle keeps 0 and drops 1, SandBoxCore, SandBox, the Armory and
  TAOM drop none. The tool therefore skips an `XmlNode` whose `IncludedGameTypes` is present and lists
  no `Campaign`.
- Registrations: `SubModule.xml` `Module/Xmls/XmlNode/XmlName@id` and `@path`, for example TAOM's
  `<XmlName id="NPCCharacters" path="troops/troops_gondor"/>` and the Armory's
  `<XmlName id="Items" path="LOTRLOME_items/gondor"/>` (a folder) and
  `<XmlName id="CraftingPieces" path="LOTRLOME_crafting_pieces"/>` (a file). A path names
  `ModuleData/<path>.xml` when that file exists, else every `*.xml` directly in
  `ModuleData/<path>/` (the release folder also holds `*.xml.bak-*` files, which must be ignored). A
  path may also have `ModuleData/<path>.xslt` (TAOM's `lords`, `spcultures`, `heroes`...), which the
  engine applies at load and this tool does not.
- A registered file can be missing: the testing release registers
  `characters/custom_battle_lords` but ships no such file.
- Troop (`troops/troops_gondor.xml:6-37`):

```xml
  <NPCCharacter id="gondor_loss_lumberman" default_group="Infantry" level="6" ...
      occupation="Soldier" is_basic_troop="true" culture="Culture.gondor">
    ...
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_gondor_lossarnach_1h_axe_light" />
        <equipment slot="Body" id="Item.sk_gd_los_inf_chainmail_a" />
        <equipment slot="Leg" id="Item.sk_gd_ano_boots_a" />
      </EquipmentRoster>
      <EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
    </Equipments>
  </NPCCharacter>
```

- Standalone roster (`equipmentsets/taom_equipment_sets_dolguldur.xml:6-17`): `<EquipmentRoster
  id="dolguldur_bat_template_medium_a" culture="Culture.dolguldur"><EquipmentSet><Equipment slot="Item0"
  id="Item.wm_dol_goldur_1h_sword_a01" />...`.
- Crafted item (`LOTRLOME_items/LOTRAOM_weapons.xml`): `<CraftedItem id="wm_gondor_lossarnach_1h_axe_light"
  crafting_template="OneHandedAxe" ...><Pieces><Piece id="wm_lossarnach_1h_axe_black_ash_blade"
  Type="Blade" scale_factor="100" />...`; the piece (`LOTRLOME_crafting_pieces.xml`) is
  `<CraftingPiece id=... piece_type="Blade" mesh="..."><BladeData ... body_name="..." holster_mesh="...">`.
- Crafting template item types (Native `crafting_templates.xml`): OneHandedSword, Dagger, OneHandedAxe,
  Mace -> OneHandedWeapon; TwoHandedSword, TwoHandedAxe, TwoHandedMace -> TwoHandedWeapon;
  TwoHandedPolearm, Pike -> Polearm; ThrowingKnife, ThrowingAxe, Javelin -> Thrown. The Armory uses
  Javelin, Mace, OneHandedAxe, OneHandedSword, Pike, TwoHandedAxe, TwoHandedMace, TwoHandedPolearm and
  TwoHandedSword.
- Skins (`ModuleData/skins.xml`, not registered through `XmlNode`; Native defines `human`, the Armory
  defines `dwarf`, `uruk`, `nazghul`, `orc`, `uruk_hai`, `berserker`, `cave_troll`, `hill_troll`,
  `pale_uruk`, `dg_uruk`, `goblin`, `elf`, `saruman`, `sauron`): `<skins><race id="dwarf"><skin
  gender="0" mesh_maturity_type="adult" body_meta_mesh=... body_meta_mesh_shoulders=...
  body_meta_mesh_upperbody=... legs_mesh=... hands_mesh=... face_meta_mesh=... underwear_bottom_mesh=...
  underwear_top_mesh=...>` with `<hair_meshes><hair_mesh name=... cover_type1=... cover_type4=...>` and
  `<beard_meshes><beard_mesh name=...>`. Some `id` attributes sit on the next line, so parse with
  ElementTree, never a one-line regex. Blank names are common: the release Armory's `skins.xml` holds
  52 `name=""`, 122 `underwear_top_mesh=""` and 22 `underwear_bottom_mesh=""`, and a `<beard_mesh>`
  with no `name` at all (line 1297); an empty or missing name names nothing and is dropped, never
  reported.
- Troop races in the TAOM troop and character files: elf 391, orc 273, goblin 264, dg_uruk 228,
  pale_uruk 204, dwarf 178, uruk_hai 156, uruk 147, berserker 7, human 4 (explicit), cave_troll 2,
  hill_troll 2, saruman 1; no troop file sets `is_female="true"`.
- Character kinds: `troops/troops_gondor.xml` holds 184 Soldier, 3 Mercenary, 1 Bandit;
  `characters/lords.xml` 1,182 `is_hero="true"`; `taom_wanderers.xml` and
  `taom_education_character_templates.xml` are all `is_template="true"`.

### Existing parsers considered

`tools/validate_all_troop_refs.py` (regex `Item\.([a-zA-Z][a-zA-Z0-9_]+)` over troop files, armour
prefixes only) and `tools/taom_schema.py` (`_NPC_BLOCK_RE`, `_INLINE_ROSTER_RE`, regexes for line
numbers) answer "does this id exist", not "which sets does the engine build". This audit needs the
engine's set rules above (inline rosters, `EquipmentSet` references by type, `<equipment>` overrides),
so it parses with `xml.etree.ElementTree` as the engine parses with `XmlDocument`. The skin body
attributes are a constant of eight names (`body_meta_mesh`, `body_meta_mesh_shoulders`,
`body_meta_mesh_upperbody`, `face_meta_mesh`, `hands_mesh`, `legs_mesh`, `underwear_bottom_mesh`,
`underwear_top_mesh`), the same eight that `tools/validate_mesh_refs.py:156-172` lists in
`VISUAL_MESH_ATTRS`; do not import that module.

### Conventions that bind this change

- No C# changes, so ADR-002 (thin entry points under 150 lines), ADR-007 (services take adapters, never
  sealed TaleWorlds types) and ADR-008 (services testable without the game) do not apply; if you find
  yourself touching `Main/` or `TAOM.Tests/`, STOP.
- No path-scoped rule in `.claude/rules/` matches `tools/*.py`; the standing rules apply: TDD,
  evidence, no em or en dash in prose, Python run as `python -B`, never `python3`.
- Tool test modules are `unittest.TestCase`, no pytest import (`tools/tests/test_ci_runner_compat.py`).
  Model the module after `tools/tests/test_audit_voice_clip_lengths.py:28-42` (imports, `REPO`, the
  `sys.path.insert(0, str(REPO / "tools"))` then the tool import).
- Read-only on every input; refuse to write inside the game install or the release folder (the map
  tool's `refuse_inside_game`).
- Logging (maintainer decision D6, binding): `taom_debug.log` and `IModLogger` belong to the game; this
  tool never runs there, so it adds no `taom_debug` line. D6's rules apply to the tool's own run log
  instead: a one-line configuration header, one summary block with totals, maxima and counts, one
  reason line per fallback kind (never one per item: aggregate with a count and the first occurrence in
  full, every occurrence in `unresolved.tsv`), each line format pinned literally by a test and listed
  with fields and an example in the module docstring.

### Blast radius

No C# type changes, so `graphify_taom.py affected` does not apply. `git grep -l "audit_map_scene_memory"`
at the planned-at commit names `tools/audit_map_scene_memory.py`, its test module,
`tools/tests/test_ci_runner_compat.py` (the pytest ratchet list), `tools/README.md`, two archive or
investigation docs (`docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`,
`docs/investigations/native-commit-audit-2026-08.md`) and 11 run-record files under
`plans/_audit/2026-10-02-perf/`. Only two files import the map tool: its test module and the run
probe `plans/_audit/2026-10-02-perf/evidence/native/editdata_share.py:12`. The new `trees` parameter
defaults to today's value, so neither caller changes.

## Commands you will need

Prefix every dotnet command with `TEMP="<tmp>" TMP="<tmp>"` from your dispatch rules.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline totals, unchanged (this plan adds no C# test) |
| One test class | the same, plus `--filter "FullyQualifiedName~<ClassName>"` | not needed here; a filter matching nothing proves nothing |
| Python tools | `timeout 900 python -B -m unittest discover -s tools/tests -t .` | the baseline's three failures by name, no new name; `Ran` grows by your new tests |
| New module only | `timeout 900 python -B -m unittest tools.tests.test_audit_battle_equipment_memory -v` | every test OK |
| Map tool tests | `timeout 900 python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider` | `33 passed` |
| Data | `python tools/validate_moduledata.py` | not needed (no ModuleData change); do not run tools that write |
| Docs | `timeout 900 python -B tools/lint_docs.py --fail-on-drift` | exit 0 (writer's base run: exit 0, size warnings only) |

Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify or create):
- `tools/audit_map_scene_memory.py`: a `trees` parameter on `AssetIndex.add_module` and
  `build_manifest`, and a `--loose-assets` flag on `main`; default behaviour byte-identical.
- `tools/audit_battle_equipment_memory.py` (new).
- `tools/tests/test_audit_battle_equipment_memory.py` (new, unittest).
- `tools/README.md`: one new row after line 62, and `--loose-assets` added to the map tool row's
  options cell.

**Out of scope** (do NOT touch, even though they look related):
- `tools/tests/test_audit_map_scene_memory.py` (pytest-only; CI never runs it) and
  `tools/tests/test_ci_runner_compat.py` (never add to `PYTEST_BASELINE`).
- Any asset, any ModuleData, any live module, any release folder: read-only inputs.
- Any C#, `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`,
  `.claude/settings*.json`, `docs/adrs/*.md`.
- `CHANGELOG.md`, `plans/README.md`, `docs/features/*`, `docs/reference/feature-map.md` (the
  orchestrator writes the feature doc).
- Settling the edit-data question natively (Ghidra): out of scope; the report keeps it UNVERIFIED.
- The gates themselves: never turn a gate green by editing it, skipping a test or loosening an
  assertion.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.32` at the planned-at commit; a
  hook refuses any other). Two commits:
  1. `feat(tools): <version> - map memory audit can read loose Assets trees`
  2. `feat(tools): <version> - rank the equipment assets a battle preloads`
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. No AI attribution trailer. Add `Not-tested: in-game memory; the audit attributes pack bytes, it
  does not measure process memory` to commit 2. Write each message to a file under your scratch folder
  and run `git commit -F "<file>"`. Stage explicit paths only. Never `--no-verify`.

## Steps

### Step 1: record the base

1. `timeout 900 python -B -m unittest discover -s tools/tests -t . > "<scratch>/py-base.txt" 2>&1`, then
   read the `Ran N tests` line and the `FAILED (...)` line and the names after `FAIL:` / `ERROR:`.
2. `timeout 900 python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider`.
3. `TEMP="<tmp>" TMP="<tmp>" timeout 900 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.
4. Capture the map tool's outputs before any edit, on the testing channel:
   `timeout 900 python -B tools/audit_map_scene_memory.py --game-dir "<releases>/testing" --modules TAOM_Map --out-dir "<scratch>/map-before" > "<scratch>/map-before.txt" 2>&1`
   (`<releases>` is the folder holding the `testing` channel, named in
   `docs/reference/release-process.md:114`; quote it with forward slashes).
   Expect exit 0 and nine files in `<scratch>/map-before` (`flora.tsv`, `map-scene-memory.md`,
   `materials.tsv`, `meshes.tsv`, `physics.tsv`, `prefabs.tsv`, `textures.tsv`, `totals.tsv`,
   `unresolved.tsv`).
5. `git status --porcelain > "<scratch>/status-base.txt"` and `git rev-parse HEAD`: the worktree may
   already hold untracked files that are not yours (for example run records under `plans/`); this
   file is the list later steps compare against.

**Verify**: Python `Ran 2962 tests` with `FAILED (failures=3, skipped=8)` and exactly the three
baseline names; pytest `33 passed`; dotnet `Failed: 1, Passed: 12345, Skipped: 2, Total: 12348` with
`EveryLanguage_DeclaresARowForEveryEnglishKey`. A different set is not yours to fix: report it and
use your measured set as the base.

### Step 2: RED, the shared index's `trees` option

Create `tools/tests/test_audit_battle_equipment_memory.py` (unittest only). Copy into it, as private
helpers, the encoders from `tools/tests/test_audit_map_scene_memory.py:61-131` (`TYPE_GUID`, `sized`,
`strs`, `guid_for`, `encode_texture_meta`, `encode_material_meta`, `encode_metamesh_meta`,
`write_tpac`); you cannot import that module because it imports pytest. Those helpers need
`import hashlib` and `import struct`, and line 61 reads `a.KIND_BY_TYPE_GUID` because that module
imports the map tool as `a`: write it `amsm.KIND_BY_TYPE_GUID` in the copy. Then add
`class SharedIndexTreesTests(unittest.TestCase)` with:

- `test_default_trees_ignore_a_loose_assets_tree`: a module folder with `AssetPackages/pack0.tpac`
  holding metamesh `a` and `Assets/x/b_geo.tpac` holding metamesh `b`; `AssetIndex().add_module(...)`
  with no `trees` argument resolves `a`, does not resolve `b`.
- `test_loose_tree_is_indexed_after_the_pack_trees_when_asked`: the same tree with
  `trees=amsm.PACK_TREES + ("Assets",)` resolves both, and when both trees hold a metamesh named `c`
  the `AssetPackages` copy wins (`item.pack` starts with `AssetPackages/`) and the loose one is listed
  in `also_in`.
- `test_missing_trees_error_names_the_trees_searched`: an empty module folder with the default trees
  records the exact text `no AssetPackages or EmAssetPackages directory`; with the loose tree added it
  records `no AssetPackages or EmAssetPackages or Assets directory`.
- `test_map_cli_accepts_loose_assets`: build a minimal fixture game folder (a `scene.xscene` of
  `<scene><entities/></scene>` under `<tmp>/game/Modules/TAOM_Map/SceneObj/Main_map`, and one pack
  under `<tmp>/game/Modules/TAOM_Map/Assets/` only), call `amsm.main(["--game-dir", "<tmp>/game",
  "--modules", "TAOM_Map", "--out-dir", "<tmp>/out", "--loose-assets"])` under
  `contextlib.redirect_stdout`, and assert it returns 0 and the captured stdout contains
  `packs indexed: 1`.

Import the map tool as `import audit_map_scene_memory as amsm`.

**Verify**: `timeout 900 python -B -m unittest tools.tests.test_audit_battle_equipment_memory -v` shows
the first test passing and the other three failing or erroring (`TypeError` for the unexpected
`trees` keyword, or `SystemExit: 2` from argparse for `--loose-assets`). Quote the failure lines in your
report.

### Step 3: GREEN, implement `trees` and `--loose-assets` in the map tool

In `tools/audit_map_scene_memory.py`:

- `def add_module(self, module: str, module_dir, trees=PACK_TREES) -> None:` loop over `trees` instead
  of `PACK_TREES`; the not-found message becomes
  `f"no {' or '.join(trees)} directory"` (identical text for the default). Add one docstring sentence:
  a loose `Assets` tree is read only when asked; its metameshes carry edit data (`5f98413d`) and almost
  never render buffers (`97f81dbb`), and its textures carry no pixel segment, so their bytes come from
  the header formula.
- `def build_manifest(game_modules, scene_dir, modules=DEFAULT_MODULES, extra_flora_kinds=(), trees=PACK_TREES)`
  passes `trees` to `index.add_module`.
- `main`: `ap.add_argument("--loose-assets", action="store_true", help=...)` stating the same caveat;
  pass `trees=PACK_TREES + ("Assets",)` when set, else nothing new.
- Do not change any output text, column, flag or default.

Then prove the default outputs are unchanged:
`timeout 900 python -B tools/audit_map_scene_memory.py --game-dir "<releases>/testing" --modules TAOM_Map --out-dir "<scratch>/map-after" > "<scratch>/map-after.txt" 2>&1`
followed by `diff -r "<scratch>/map-before" "<scratch>/map-after"` and
`diff <(grep -v "^report:\|^tsv:" "<scratch>/map-before.txt") <(grep -v "^report:\|^tsv:" "<scratch>/map-after.txt")`
(the `report:` and `tsv:` lines name the output folder, which differs).

**Verify**: the four `SharedIndexTreesTests` pass; both diffs print nothing; the pytest map tests give
`33 passed`. Commit 1 (`tools/audit_map_scene_memory.py`, `tools/tests/test_audit_battle_equipment_memory.py`).

### Step 4: RED, definitions (registrations, items, pieces, rosters, characters, skins)

Add `class DefinitionsTests(unittest.TestCase)` against a synthetic module tree built in a
`tempfile.TemporaryDirectory` (a helper `make_modules(root)` writing `SubModule.xml` and `ModuleData`
files for two modules, `Native` then `TAOM`). The tool API these tests pin (write the tests against
exactly these names):

- `read_registrations(module_dir) -> list[tuple[str, str]]`: `(xml_id, path)` from
  `Module/Xmls/XmlNode/XmlName`, skipping an `XmlNode` whose `<IncludedGameTypes>` is present and holds
  no `<GameType value="Campaign"/>` (see "ModuleData shapes"); a node with no `IncludedGameTypes` is
  kept.
- `registered_files(module_dir, path) -> list[Path]`: `ModuleData/<path>.xml` if it is a file, else the
  sorted `*.xml` directly in `ModuleData/<path>/` (a `x.xml.bak-1` beside them is not returned), else
  `[]`.
- `load_definitions(modules: list[tuple[str, Path]]) -> Definitions` with dicts `items`,
  `pieces`, `rosters`, `characters`, `races` keyed by id (race: `(race_id, gender)`), plus
  `unresolved: list[tuple[str, str, str]]` of `(reason, subject, referenced_by)`. `rosters[id]` is a
  list of `(equipment_type, slots)` pairs, one per `<EquipmentSet>` child.
- `battle_sets(character, defs, include_civilian=False) -> list[dict[str, str]]`: one dict per set,
  slot name to item id. Slot names are normalised with the engine's map (`Item0`..`Item3` to
  `Weapon0`..`Weapon3`, `Item4` to `ExtraWeaponSlot`, every other name unchanged), so an override
  replaces the same slot whichever spelling either side used. Item ids take the part after the first
  `.`. Set types are `Battle`, `Civilian` or `Stealth`; `Stealth` sets are never returned.
  An `<EquipmentSet id>` naming no roster contributes no set; `load_definitions` records it as
  `ROSTER_UNRESOLVED` (subject the roster id, referenced_by the character id) once, after every module
  is loaded, so this function adds no row and calling it twice changes nothing.

Tests (one behaviour each):

- `test_registration_path_can_be_a_file_or_a_folder`.
- `test_registration_for_another_game_type_is_skipped`: three `XmlNode`s, one with
  `<IncludedGameTypes><GameType value="MultiplayerGame"/></IncludedGameTypes>`, one listing
  `Campaign` and `CustomGame`, one with no `IncludedGameTypes`; `read_registrations` returns the last
  two only.
- `test_registered_but_missing_file_is_reported`: reason `REGISTERED_FILE_MISSING`, subject
  `<module>/<path>`.
- `test_xslt_beside_a_registration_is_reported_not_applied`: reason `XSLT_NOT_APPLIED`.
- `test_later_module_definition_wins_and_the_duplicate_is_reported`: reason `DUPLICATE_DEFINITION`,
  the `TAOM` definition is the one kept.
- `test_item_fields`: an `<Item>` with `mesh`, `holster_mesh`, `holster_mesh_with_weapon`,
  `flying_mesh`, `body_name`, `holster_body_name`, `shield_body_name`, `Type`, and an
  `<ItemComponent><Armor has_gender_variations="false" reins_mesh="r"/>`; `has_gender_variations`
  defaults to True when absent.
- `test_crafted_item_type_comes_from_its_template`: `crafting_template="OneHandedAxe"` gives type
  `OneHandedWeapon`; an unknown template gives `UNKNOWN` and reason `CRAFTING_TEMPLATE_UNKNOWN`.
- `test_crafting_piece_reads_mesh_and_blade_data`.
- `test_horse_reads_additional_meshes_and_materials`.
- `test_character_battle_sets_follow_the_engine_rules`: one character with an inline battle roster,
  an inline `civilian="true"` roster, an `<EquipmentSet id="std"/>` (Battle) whose standalone roster
  holds one battle and one civilian set, an `<EquipmentSet id="std" equipmentType="Civilian"/>`, and a
  direct `<equipment slot="Head" id="Item.h"/>` override: `battle_sets(character, defs)` returns two
  sets (the inline battle set and the standalone roster's battle set; nothing civilian) and every one
  of them has `Head` = `h`; `battle_sets(character, defs, include_civilian=True)` returns four (adding
  the inline civilian set and the standalone roster's civilian set, both also overridden). A standalone
  set with `equipmentType="Stealth"` in the same roster is in neither result.
- `test_override_replaces_the_same_slot_in_either_spelling`: a set with `slot="Weapon0" id="Item.a"`
  and an override `<equipment slot="Item0" id="Item.b"/>` give one `Weapon0` = `b` and no `Item0` key.
- `test_lowercase_equipment_roster_adds_no_set`: an inline `<equipmentRoster>` contributes no set
  (`MBEquipmentRoster.Init`), while `<equipments>` as the container is read like `<Equipments>`.
- `test_item_id_takes_the_part_after_the_first_dot`: `id="Item.x"` and `id="x"` both give `x`.
- `test_unresolved_roster_is_reported`: reason `ROSTER_UNRESOLVED`, the character still has its
  other sets.
- `test_skins_parse_multi_line_race_ids_and_the_adult_skin`: a `skins.xml` whose `<race` and
  `id="r"` sit on separate lines; `races[("r", "0")]` holds the eight body attributes and every
  `hair_mesh`/`beard_mesh` `name` and `cover_type1`..`cover_type4`.
- `test_blank_skin_names_are_dropped`: `underwear_top_mesh=""`, a `<hair_mesh name="">` and a
  `<beard_mesh>` with no `name` add no name to the race and no `unresolved` row.

**Verify**: the module's `DefinitionsTests` fail with `ModuleNotFoundError` or `AttributeError` for
`audit_battle_equipment_memory`, and `SharedIndexTreesTests` still pass. Quote the failure.

### Step 5: GREEN, implement the definitions loader

Create `tools/audit_battle_equipment_memory.py`. Module docstring first (WHY, WHAT IT READS, ENGINE
RULES with the source of each rule as quoted in "Current state", OUTPUTS, RUN LOG, APPROXIMATIONS;
fill OUTPUTS and RUN LOG in Step 8 and APPROXIMATIONS in Step 10). Use `from __future__ import annotations`,
`sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))`, `from _gamedir import game_dir`,
`import audit_map_scene_memory as amsm`. Implement the Step 4 API:

- `CRAFTING_TEMPLATE_TYPES` = the twelve Native templates listed in "Current state".
- Parse every registered file with `ET.fromstring(open(path, "rb").read())`; a parse error is reason
  `XML_PARSE_ERROR` (subject the file, consequence: its definitions are absent) and the run goes on.
- Registration ids read: `Items` (`Item` and `CraftedItem` children), `CraftingPieces`
  (`CraftingPiece`), `EquipmentRosters` (`EquipmentRoster`), `NPCCharacters` (`NPCCharacter`). Skins:
  `ModuleData/skins.xml` of each module when present; a later module's race and gender replaces an
  earlier one; empty or missing names are dropped.
- Characters keep: `id`, `module`, `file`, `culture` (the part after `Culture.`), `race` (default
  `human`), `is_female`, `is_hero`, `is_template`, and the raw `<Equipments>` (or `<equipments>`)
  element; resolve sets in `battle_sets(character, defs, include_civilian=False)` following "Current
  state" exactly, using the winning standalone roster definitions.

**Verify**: `DefinitionsTests` and `SharedIndexTreesTests` all pass.

### Step 6: RED then GREEN, item to asset names (the engine's preload rule)

Add `class ItemAssetNamesTests(unittest.TestCase)` pinning
`item_asset_names(item, defs) -> ItemNames` where `ItemNames` has
`mesh_candidates: list[tuple[tuple[str, ...], bool]]` (each tuple of names is tried in order and the
first one present in the index wins; the bool is `required`, explained below), `materials: list[str]`,
`bodies: list[str]` and `misses: list[tuple[str, str, str]]` (`(reason, subject, referenced_by)`). The
rule, from `PreloadHelper.AddItemObject` and
`GetMultiMeshCopyWithGenderData`, with `m` the item's `mesh`:

| Item | Mesh candidate lists | Bodies |
|---|---|---|
| Armour (`Armor` component), `gv` = `has_gender_variations` (default true) | `[m]`; `[holster_mesh]`; `[m_male, m]`; `[m_male, m_slim, m]`; female no slim: `gv ? [m_female, m_converted, m] : [m_male, m]`; female slim: `gv ? [m_female, m_converted_slim, m] : [m_male, m_slim, m]`; when `reins_mesh` is non-empty `[reins]` and `[reins_rope]` | `shield_body_name`, `body_name`, `holster_body_name` |
| Weapon `Item` (not crafted) | `[m]`; `[holster_mesh]`; `[holster_mesh_with_weapon]`; `[flying_mesh]`; `[m_male, m]` | the same three attributes |
| `CraftedItem` | one `[piece.mesh]` per piece; `[blade.holster_mesh]` of the `Type="Blade"` piece | blade `body_name`, and blade `holster_body_name` or else `body_name` |
| Horse (`Horse` component) | `[m]`; `[holster_mesh]`; one `[name]` per `AdditionalMeshes/Mesh`; `[m_male, m]` | the same three attributes; `materials` = every `Materials/Material@name` |
| Any other `Item` | `[m]`; `[holster_mesh]`; `[m_male, m]` | the same three attributes |

Empty names are dropped; `m_male` means `m + "_male"` and so on. Tests: one per row, plus
`test_crafted_piece_missing_is_reported` (reason `PIECE_UNRESOLVED`), plus
`test_no_gender_variations_reuses_the_male_lists`. Run them RED (missing function), then implement
`item_asset_names` and see them pass.

`item_asset_names` may name the same thing twice: a crafted blade with no `holster_body_name` gives
its `body_name` as both bodies (`ItemObject.cs:366-367`), and the armour gender lists of an item with
no `_male`, `_female` or `_converted` variant all end in `m`. That is the engine's own input; the
engine then keeps names in `HashSet`s. So `resolve_names` de-duplicates.

Then add `resolve_names(names, index) -> (metamesh items, physics items, misses)`: each candidate
list yields the first name `index.get("metamesh", name)` finds. Each returned list holds every index
item at most once (compare by `(kind, name.lower())`), in first-found order, and `misses` holds each
`(reason, subject, referenced_by)` once. Two kinds of list, told apart by the `required` flag
`item_asset_names` sets:

- `required=True`, a list holding a name the XML sets (`[m]`, `[holster_mesh]`,
  `[holster_mesh_with_weapon]`, `[flying_mesh]`, `[reins]`, a piece mesh, a blade holster mesh, an
  additional mesh): when nothing is found it is a miss, reason `MESH_UNRESOLVED`, subject the name,
  referenced_by the item id;
- `required=False`, a list the engine derives by naming (the four gender and slim lists, `[m_male, m]`,
  `[reins_rope]`): never a miss of its own, because it ends in `m` (already reported by `[m]` when
  missing) or is optional (`_rope`).

Bodies resolve through `index.get("physics", name)`, else reason `BODY_UNRESOLVED`. Tests for each
outcome, RED then GREEN, including `test_resolve_names_returns_each_asset_once`: a crafted item whose
Blade piece has `body_name="bo_x"` and no `holster_body_name` returns one physics item `bo_x`, and an
armour item `m` with `has_gender_variations="false"` whose fixture pack holds only `m` and `m_slim`
returns exactly those two metamesh items.

**Verify**: `ItemAssetNamesTests` pass, earlier classes still pass.

### Step 7: RED then GREEN, sizes, union, scopes and flags

Add `class SizingAndUnionTests(unittest.TestCase)` over a fixture pack written with the copied
encoders (`write_tpac`). Pin:

- `mesh_sizes(item, index) -> dict` with `render_bytes = item.seg_bytes(amsm.SEG_MESH_B)`,
  `edit_bytes = item.seg_bytes(amsm.SEG_MESH_A)`, `other_bytes = item.seg_bytes() - render - edit`,
  `lod0_faces` = the sum of faces over records with no `.lod<N>` part or `.lod0`, or `None` when any
  LOD 0 record fails `amsm.plausible_counts`, and `materials` from
  `amsm.scan_guids(item.meta, index.by_guid, "material")`.
- Material to textures with `amsm.parse_material_meta`; on a decode error fall back to
  `amsm.scan_guids(meta, index.by_guid, "texture")` and record reason `MATERIAL_UNDECODED`; an
  unindexed texture guid is `TEXTURE_UNRESOLVED`; a horse material name not in the index is
  `MATERIAL_UNRESOLVED`.
- Texture bytes: `amsm._texture_row(tex, index, "")["resident_bytes"]` and its flags (the formula is
  the map tool's; test one pixel-segment texture and one stub-only texture against
  `amsm.texture_chain_bytes`).
- Physics bytes: `item.seg_bytes()`.
- `AssetSet`: a dict keyed `(kind, lowercase name)` (kind `metamesh`, `material`, `texture` or
  `physics`) whose value is that asset's sizes: `render_bytes`, `edit_bytes`, `other_bytes` for a
  metamesh, `texture_bytes` for a texture, `physics_bytes` for a body, 0 for a material. An asset's
  `floor_bytes = render + texture + physics` and `upper_bytes = floor + edit + other`; a set's totals
  are the sums over its keys, so a key reached twice counts once. `union(*sets) -> AssetSet` merges
  keys. `item_assets(item_id, defs, index) -> AssetSet` is one item's metameshes, their materials,
  their textures and its bodies (plus horse materials and their textures). Tests:
  `test_shared_texture_counts_once` (two items whose meshes use materials that share one texture give a
  union whose texture bytes include it once, while each item's own total includes it) and
  `test_asset_floor_and_upper_bytes`.
- A metamesh without a `97f81dbb` segment (a loose tree) contributes 0 render bytes and is flagged
  `RENDER_BUFFERS_NOT_IN_TREE`, with reason line `RENDER_BUFFERS_NOT_IN_TREE`.
- Scopes, each returning an `AssetSet`:
  - `race_assets(character, defs, index)`: the adult skin of `(race, "1" if is_female else "0")`,
    every non-empty body attribute, hair and beard `name` and `cover_typeN`, resolved as metameshes
    with their materials and textures (misses are `RACE_MESH_UNRESOLVED`; a race with no skin is
    `RACE_UNRESOLVED`).
  - `troop_assets(character, defs, index, include_civilian=False)`: the union of `item_assets` over
    every item of `battle_sets(character, defs, include_civilian)` plus `race_assets`. An item id no
    definition holds is `ITEM_UNRESOLVED` (subject the item id, referenced_by the troop id).
  - `culture_assets(culture, troops, defs, index, include_civilian=False)`: the union over the given
    population troops whose `culture` matches.
  - `side_assets(troops, defs, index, include_civilian=False)`: the union over the side's troops.
  - `battle_assets(sides)`: the union of the side sets; `shared_floor_bytes(side, other_sides)` = the
    floor bytes of the side's keys that any other side also holds.

  Tests: `test_troop_assets_include_race_meshes`, `test_unknown_race_is_reported`,
  `test_culture_union_counts_a_shared_item_once`, `test_shared_floor_bytes_of_two_sides`.
- Population (no sides given): characters whose winning definition comes from module `TAOM`, not
  `is_template="true"`, not `is_hero="true"` unless `--include-heroes`, with at least one battle set.
- Flags: `LOD0_OVER_FACE_BUDGET` (`lod0_faces > --face-budget`, default 20000), `LOD0_FACES_UNKNOWN`,
  `TEXTURE_4K_ON_SMALL_ITEM` (a texture with `max(width, height) >= --big-texture`, default 4096,
  reached from an item whose type is in `SMALL_ITEM_TYPES` = HeadArmor, HandArmor, LegArmor, Cape,
  Arrows, Bolts, Thrown, OneHandedWeapon, Shield), and the map tool's texture flags carried through.
- `test_every_miss_is_kept`: a troop whose set names one missing item, one item with a missing mesh,
  and one with a missing body ends with three `unresolved` rows (`ITEM_UNRESOLVED`,
  `MESH_UNRESOLVED`, `BODY_UNRESOLVED`) and its totals still include its resolved assets.

Each behaviour: write the test, run it RED, implement, run it GREEN.

**Verify**: all classes so far pass.

### Step 8: RED then GREEN, run log, outputs and CLI

Add `class RunLogTests(unittest.TestCase)` and `class CliTests(unittest.TestCase)`. The run log goes
to stdout and, line by line with a flush, to `<tsv-dir>/run.log`; the report repeats it in a "Run log"
section. Formats (pin each one literally against a fixture run; integers unformatted, bytes as plain
integers):

```text
[EquipMemAudit] config releaseModules=<path> gameModules=<path> xmlModules=<a,b,...> packModules=<a,b,...> trees=<AssetPackages+EmAssetPackages[+Assets]> sides=<n> civilian=<on|off> heroes=<on|off> faceBudget=<n> bigTexture=<n>
[EquipMemAudit] module name=<m> root=<release|game|missing> registrations=<n> files=<n> items=<n> craftingPieces=<n> rosters=<n> characters=<n> races=<n>
[EquipMemAudit] index module=<m> packs=<n> metameshes=<n> materials=<n> textures=<n> physics=<n> errors=<n>
[EquipMemAudit] fallback reason=<CODE> count=<n> first=<subject> consequence=<text>
[EquipMemAudit] side name=<label> troops=<n> items=<n> floorBytes=<n> upperBytes=<n> sharedFloorBytes=<n>
[EquipMemAudit] battle sides=<n> troops=<n> floorBytes=<n> upperBytes=<n> sumOfSidesFloorBytes=<n>
[EquipMemAudit] summary troops=<n> items=<n> metameshes=<n> textures=<n> physics=<n> renderBytes=<n> editBytes=<n> otherMeshBytes=<n> textureBytes=<n> physicsBytes=<n> floorBytes=<n> upperBytes=<n> maxTroop=<id>:<floorBytes> maxAsset=<kind>:<name>:<floorBytes> unresolved=<n> elapsedS=<x.x>
```

One `module` line per xml module, one `index` line per pack module, one `fallback` line per reason
code that occurred (in sorted code order), `side` and `battle` lines only when sides are given, one
`summary` line last. `consequence` text comes from one dict `REASONS = {code: consequence}` covering
`MODULE_MISSING`, `REGISTERED_FILE_MISSING`, `XML_PARSE_ERROR`, `XSLT_NOT_APPLIED`,
`DUPLICATE_DEFINITION`, `CRAFTING_TEMPLATE_UNKNOWN`, `ROSTER_UNRESOLVED`, `ITEM_UNRESOLVED`,
`PIECE_UNRESOLVED`, `MESH_UNRESOLVED`, `BODY_UNRESOLVED`, `MATERIAL_UNRESOLVED`, `MATERIAL_UNDECODED`,
`TEXTURE_UNRESOLVED`, `RACE_UNRESOLVED`, `RACE_MESH_UNRESOLVED`, `RENDER_BUFFERS_NOT_IN_TREE`,
`LOD0_FACES_UNKNOWN`, `PACK_ERROR`; a test asserts every code the tool can emit has an entry.

Outputs (default folder: a `battle-equipment` folder beside the map tool's `DEFAULT_OUT`, that is the
same parent with `battle-equipment` in place of `map-manifest`; `--out-dir`, `--report`, `--tsv-dir`,
`--top` as in the map tool):

- `battle-equipment-memory.md`: the method disclaimer (attribution, not measurement; floor and upper
  bound; edit data UNVERIFIED in the words of "Current state"; the preload rule's source), the run log,
  the sides and battle table when given, per-culture totals, the top N assets by floor bytes (kind,
  name, module, floor bytes, edit bytes, flags, item and troop counts with up to five sample ids each),
  the top N meshes by edit bytes, the top N items and troops by floor bytes, a flag summary, the
  unresolved summary by reason, and an Approximations section (Step 10).
- TSVs written with `amsm.write_tsv`: `assets.tsv` (kind, name, module, pack, floor_bytes,
  edit_bytes, other_bytes, format, width, height, mips, lod0_faces, flags, items, troops, cultures,
  sample_items, sample_troops), `items.tsv` (one row per item the selected troops reach; columns
  item_id, module, kind, type, meshes, bodies, floor_bytes, upper_bytes, troops, unresolved, in that
  order; `meshes` and `bodies` are the distinct resolved names (each name once, as `resolve_names`
  returns them) in the index's own spelling, sorted case-insensitively, joined with `;`),
  `troops.tsv` (troop_id, module, culture, race, is_hero,
  battle_sets, items, floor_bytes, upper_bytes, race_floor_bytes, unresolved), `cultures.tsv`
  (culture, troops, items, metameshes, textures, physics, render_bytes, edit_bytes, texture_bytes,
  physics_bytes, floor_bytes, upper_bytes), `sides.tsv` (only with sides: side, troops, floor_bytes,
  upper_bytes, shared_floor_bytes) and `unresolved.tsv` (reason, subject, referenced_by), plus
  `run.log`. `amsm.write_tsv` joins a list value with `|` (`audit_map_scene_memory.py:1146`), so pass
  every multi-value cell (`meshes`, `bodies`, `flags`, the sample columns) as a string already joined
  with `;`.

CLI (`main(argv=None) -> int`):

```text
--release-modules DIR   default DEFAULT_RELEASE_MODULES = <releases>\testing\Modules (a module constant,
                        spelled out like the map tool's DEFAULT_GAME)
--game-dir DIR          default game_dir(amsm.DEFAULT_GAME)
--xml-modules LIST      default Native,SandBoxCore,SandBox,LOTRLOME_Armory,TAOM (load order, later wins)
--pack-modules LIST     default LOTRLOME_Armory,TAOM,Native,SandBox (resolution priority, first wins)
--loose-assets          also index each pack module's Assets tree, after its pack trees
--troops FILE           repeatable; one side per file (troop ids, one per line, '#' comments)
--culture ID            repeatable; one side per culture (population troops of that culture)
--include-civilian      add civilian sets (missions that require civilian equipment)
--include-heroes        add is_hero characters to the population
--face-budget N         default 20000
--big-texture N         default 4096
--out-dir, --report, --tsv-dir, --top
```

CustomBattle is not in the `--xml-modules` default: its only character registration is for
`CustomGame` and `EditorGame` (see "ModuleData shapes"), so it would load nothing for a campaign
battle.

A module name resolves to `<release-modules>/<name>` when that folder exists, else
`<game-dir>/Modules/<name>`, else it is `MODULE_MISSING`. `--troops` and `--culture` are mutually
exclusive. `main` returns 2 (after printing one line naming the cause) when `--release-modules` or the
game's `Modules` folder is missing, when a `--troops` file names an id no module defines, or when a
side selects zero troops. Before writing anything, `main` calls `amsm.refuse_inside_game(path, root)`
for each output path (`--out-dir`, `--report`, `--tsv-dir`) against both the game install root and
`--release-modules`. That function (`audit_map_scene_memory.py:1340-1345`) raises
`SystemExit(f"refusing to write inside the game install: {out_path}")`, a process exit status of 1,
not 2, and its text says "game install" for the release folder too; reuse it as it is, without
catching it. Otherwise return 0, unresolved references included (it is an audit).

Tests: each run-log format against a fixture run (literal string comparison of each line with
`elapsedS` replaced), `test_outputs_are_written`, `test_unknown_troop_id_exits_2`,
`test_empty_side_exits_2`, `test_refuses_to_write_inside_the_game_or_release` (two cases, an
`--out-dir` under the fixture game folder and one under the fixture `--release-modules` folder: each
`main(...)` call raises `SystemExit` under `assertRaises`, its `.code` is a string starting
`refusing to write inside the game install: `, and the folder holds no new file),
`test_two_sides_share_assets_once` (battle floor < sum of side floors when they share an asset, and
`sharedFloorBytes` equals the shared asset's bytes), `test_main_runs_end_to_end`.

**Verify**: `timeout 900 python -B -m unittest tools.tests.test_audit_battle_equipment_memory -v`, all
OK; then the full Python suite: the baseline's three failures by name, nothing new, `Ran` = 2962 plus
your new tests.

### Step 9: live run on the testing channel, and the sampled-items check

1. `start=$(date +%s); timeout 900 python -B tools/audit_battle_equipment_memory.py --out-dir "<scratch>/eq-testing" > "<scratch>/eq-testing.txt" 2>&1; echo "rc=$? secs=$(( $(date +%s) - start ))"`.
   Expect `rc=0` and well under 900 seconds. Read the whole run log: the `config` line names the
   testing channel, every `module` line has `root=release` for `LOTRLOME_Armory` and `TAOM` and
   `root=game` for the vanilla modules, the Armory `index` line reports 10 packs, 4,549 metameshes,
   2,595 textures, 950 materials and 393 physics (the writer's counts; re-derive if the channel was
   re-published since, and say so).
2. The sampled-items oracle (`items.tsv` columns 1, 5 and 6 are `item_id`, `meshes` and `bodies`):
   for each row of the "Sampled resolutions" table, `grep -P "^<item_id>\t" "<scratch>/eq-testing/items.tsv" | cut -f5,6`
   must print exactly the expected metameshes and bodies. Example:
   `grep -P "^sk_gd_los_inf_chainmail_a\t" ... | cut -f5,6` prints
   `sk_gd_los_inf_chainmail_a;sk_gd_los_inf_chainmail_a_slim` and an empty bodies field, and the axe
   prints `bo_wm_lossarnach_1h_axe__blade` once. Then run the distinct-cell `awk` check from "Done
   criteria" on the same file: it prints nothing.
3. One side pair: write `<scratch>/gondor.txt` with `gondor_loss_lumberman` and `gondor_loss_woodsman`
   and run again with `--troops "<scratch>/gondor.txt" --out-dir "<scratch>/eq-side"`; the
   `side` line reports `troops=2` and `items.tsv` holds the woodsman's two shields
   (`sm_gd_shield_a1`, `sm_gd_shield_a2`) and neither `battania_sword_1_t2` nor `burlap_waistcoat`
   (two items of the civilian roster `battania_troop_civilian_template_t2`, SandBoxCore
   `sandboxcore_equipment_sets.xml:5471-5480`, which both troops reference with
   `equipmentType="Civilian"`); then run
   `--culture gondor --culture mordor --out-dir "<scratch>/eq-battle"` and confirm the `battle`
   line's `floorBytes` is at most `sumOfSidesFloorBytes`.
4. Loose trees: run once against the game install's live Armory (`<game>` is the install root that
   `game_dir(amsm.DEFAULT_GAME)` returns) with
   `--release-modules "<game>/Modules" --loose-assets --out-dir "<scratch>/eq-live"`; confirm the
   `RENDER_BUFFERS_NOT_IN_TREE` fallback line appears with a count above zero, and read the
   `t_gd_ano_chainmail_a1_d` row of `assets.tsv`. In the testing release it is 1024x1024 DXT1, 11
   mips, 699,064 bytes from its pixel segment. The loose row must show a decoded format and size; if it
   is the same size and format, it must read 699,064 (the formula); if the live texture was resized,
   record both rows and continue. A row with `HEADER_UNDECODED` is a STOP.

**Verify**: all four sub-checks hold. Record the run times, the `summary` lines and the top five rows
of the report's top-assets table in your report (they are data for the orchestrator's feature doc).

### Step 10: documentation in the repo

- Finish the module docstring: OUTPUTS (every file and column), RUN LOG (every line format from
  Step 8 with each field explained and one example line, with `<releases>` and `<game>` placeholders
  instead of local paths), and APPROXIMATIONS, at least: XSLT transforms are not applied
  (`XSLT_NOT_APPLIED`); same-id definitions are last-wins, while the engine merges by XSD rules;
  equipment TAOM's C# changes at runtime (career kits, creature gear, enlistment) and
  `Mission.GetExtraEquipmentElementsForCharacter` are not modelled; hair and beard rows are an upper
  bound (the face key picks one per agent; every `cover_typeN` is counted); horse materials are an
  upper bound (one is picked per horse); the siege missiles `MissionPreloadView` preloads in a siege
  (`PreloadItems(GetSiegeMissiles())`) are not modelled; `Stealth` equipment sets are never counted,
  since a battle preloads only battle sets (and civilian sets with `--include-civilian`); registrations
  whose `IncludedGameTypes` omit `Campaign` are skipped, so the audit models campaign battles only;
  batched copies (`needBatchedVersion`) and crafted weapon
  meshes the engine assembles at runtime allocate buffers not in any pack; edit data residency is
  UNVERIFIED; a metamesh's bytes are for all its LODs, because the segment order in the table of
  contents does not follow the record order (map tool docstring, :83-84, "The segment order in the table of contents does not always follow the record order"), so per-LOD bytes are not
  claimed; the default race of a character with no `race` attribute is read as `human`;
  `body_mesh_suffix` in `skins.xml` is not applied (the managed preload never reads it).
- `tools/README.md`: add a row after line 62 for `audit_battle_equipment_memory.py` in the same style
  (what it reads, the engine rule it follows, floor versus upper bound, outputs, tests, the edit-data
  caveat), with the options cell listing every CLI flag; add `--loose-assets` to the map tool row's
  options cell (line 62, last cell). Row 62 spells its default output folder as a local `E:\` path;
  do not copy that into the new row: name the default folders with placeholders (`<releases>\testing\Modules`,
  "a `battle-equipment` folder beside the map tool's default output").

**Verify**: `timeout 900 python -B tools/lint_docs.py --fail-on-drift` exits 0;
`grep -nP "\xE2\x80[\x93\x94]" tools/audit_battle_equipment_memory.py tools/tests/test_audit_battle_equipment_memory.py`
prints nothing (this byte form is the one this machine's grep accepts; `\x{2014}` errors with "character
value too large"); `git diff -U0 <base> -- tools/README.md tools/audit_map_scene_memory.py | grep -nP "^\+.*\xE2\x80[\x93\x94]"`
prints nothing (`<base>` is the commit your task starts from, so the check covers commit 1's docstring
and help text as well as the uncommitted README row);
`git diff -U0 <base> -- tools/README.md | grep -P '^\+\| .audit_battle_equipment_memory\.py' | grep -cP '[A-Z]:\x5c'`
prints `0` (the edited row 62 keeps its existing `E:\` path, so the check reads only the new row);
`grep -nP '[A-Z]:\x5c' tools/audit_battle_equipment_memory.py` shows exactly one line, the
`DEFAULT_RELEASE_MODULES` constant (the output folder is derived from `amsm.DEFAULT_OUT` and the game
default is imported from the map tool), and no docstring example line. Write these greps in single
quotes as shown: `\x5c` is the backslash, and a double-quoted `"E:\\\\"` form matched nothing on this
machine in plan review.

### Step 11: full verification and commit 2

1. `timeout 900 python -B -m unittest discover -s tools/tests -t .`: baseline failures only.
2. `timeout 900 python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider`: `33 passed`.
3. `TEMP="<tmp>" TMP="<tmp>" timeout 900 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
   the baseline totals and failure (no C# changed; this confirms it).
4. `git status --porcelain`, compared with `<scratch>/status-base.txt`, lists nothing beyond what
   Step 1 recorded except `tools/audit_battle_equipment_memory.py`,
   `tools/tests/test_audit_battle_equipment_memory.py` and `tools/README.md`.
5. Commit 2 with those three paths and the body from "Git workflow".

**Verify**: `git log -2 --format=%s` prints the two subjects; `git status --porcelain` lists nothing
beyond what Step 1 recorded.

## Test plan

- New module `tools/tests/test_audit_battle_equipment_memory.py`, `unittest.TestCase` only, fixtures in
  `tempfile.TemporaryDirectory`, synthetic packs written with encoders copied from
  `tools/tests/test_audit_map_scene_memory.py:61-131`; model the module layout after
  `tools/tests/test_audit_voice_clip_lengths.py`.
- Classes: `SharedIndexTreesTests` (default unchanged, loose tree opt-in and priority, error text,
  map CLI flag); `DefinitionsTests` (registrations, game-type skips, missing files, XSLT, duplicates,
  item fields, crafted type, pieces, horses, the engine's battle-set rules, slot spellings, the
  lowercase roster, id prefix, unresolved rosters, skins, blank skin names);
  `ItemAssetNamesTests` (one per item row of the rule table, gender variations off, piece misses,
  first-found resolution, each asset returned once); `SizingAndUnionTests` (segments, LOD 0 faces known and unknown, materials
  and the fallback, texture formula and pixel segment, physics, shared assets once, loose render
  buffers, every miss kept, flags); `RunLogTests` (each line format literally, one fallback line per
  code with count and first, every code has a consequence); `CliTests` (outputs, exit 2 cases,
  refusal, two sides sharing assets, end to end).
- The live checks of Step 9 are the engine-rule check on real data; they are not unit tests because
  they need the release folder.
- Not testable here: in-game memory. The `Not-tested:` trailer of commit 2 names it.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `diff -r "<scratch>/map-before" "<scratch>/map-after"` prints nothing, and the stdout diff of
      Step 3 prints nothing
- [ ] `python -B -m pytest tools/tests/test_audit_map_scene_memory.py -q -p no:cacheprovider` gives `33 passed`
- [ ] `python -B -m unittest tools.tests.test_audit_battle_equipment_memory -v` ends `OK`
- [ ] `python -B -m unittest discover -s tools/tests -t .` fails only the baseline's three names, and
      `Ran` equals 2962 plus the new module's test count
- [ ] `git grep -n "import pytest" tools/tests/test_audit_battle_equipment_memory.py` prints nothing
- [ ] The Step 9 live run exits 0 within 900 seconds and all five sampled rows match exactly
- [ ] `python -B tools/lint_docs.py --fail-on-drift` exits 0
- [ ] The dotnet suite matches the baseline totals and failing name
- [ ] After commit 2, `git status --porcelain` lists nothing beyond what Step 1 recorded in
      `<scratch>/status-base.txt`, and `git diff --stat <base>..HEAD` (`<base>` being the commit your
      task starts from) lists only the four in-scope files
- [ ] No `meshes` or `bodies` cell of the Step 9 `items.tsv` repeats a name:
      `awk -F'\t' 'NR>1{for(c=5;c<=6;c++){split("",s);n=split($c,a,";");for(i=1;i<=n;i++)if(a[i]!=""&&s[tolower(a[i])]++)print $1}}' "<scratch>/eq-testing/items.tsv"`
      prints nothing (checked in plan review against a fixture: it names exactly the rows with a
      repeated mesh or body)
- [ ] Every docstring line, README sentence and test oracle this plan supplied was re-checked against
      the code and the packs it describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts (drift check).
- The item-to-mesh resolution cannot be made to match the engine rules for the sampled set: any of
  the five sampled rows in Step 9 differs from the table and the difference is not explained by a
  re-published channel. Report the row, what the tool resolved, and which engine rule (quoted above)
  it follows; do not tune the rule to the data.
- A TaleWorlds behaviour you need differs from "Current state" (for example `taom-src` shows a
  different `GetMultiMeshCopyWithGenderData` or `PreloadHelper.AddItemObject`); report the mismatch.
- Keeping the map tool's default outputs byte-identical is impossible with the `trees` change.
- The live run needs more than 900 seconds, or reading the release folder needs a write anywhere
  outside your scratch folder.
- A loose texture's header does not decode, or its formula bytes differ from the release pack's pixel
  segment for the same texture (Step 9.4).
- The work seems to need a C# change, a ModuleData change, an edit to the pytest module, or an
  addition to `PYTEST_BASELINE`.
- A step's verification fails twice after a reasonable fix.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (draft "tools: rank which equipment assets dominate a battle's
  memory", labels tooling and memory, in `plans/_audit/2026-10-02-perf/issue-drafts.md`, section 038).
- `/localize`: none (no player-facing text).
- `docs/features/battle-equipment-memory-audit.md` from `TEMPLATE.md` and its row in
  `docs/reference/feature-map.md`; its log section copies the RUN LOG section of the module docstring
  (every line, fields and an example), per decision D6; record the executor's Step 9 numbers there as
  the first measurement, dated, with the channel's manifest version.
- Run `/deep-review` on the branch before merge (Python only, so the tooling lenses).

## After merge: the maintainer's actions

- None required. To use it: `python tools/audit_battle_equipment_memory.py` (testing channel, all
  troops) or `--culture gondor --culture mordor` for one battle; the report lands in the default
  output folder.

## Maintenance notes

- The engine rule lives in two decompiled methods, `PreloadHelper.AddItemObject` and
  `ItemObjectViewExtensions.GetMultiMeshCopyWithGenderData`. After an engine bump, re-read both with
  `taom-src` and update `item_asset_names` and its tests together.
- The edit-data question decides whether the floor or the upper bound is the battle's real cost.
  Settling it (who calls `0x583350`, whether worn armour or hair reaches `0x68D30`) is a native
  research follow-up, deliberately out of scope; when it is settled, the report's wording changes,
  not its numbers.
- The review should probe: the battle-set rules against `BasicCharacterObject.Deserialize` (civilian
  `EquipmentSet` references, the `<equipment>` override applied to every set); that each fallback
  reason is emitted once with a count, never per item; that the map tool's default output really is
  unchanged; that no output path can land inside the game or release folders; that a shared texture
  is counted once in a union and in full in each item's own total.
- Deferred: weighting a battle by head count (the preload loads each item once whatever the head
  count, so the union is the right unit for preload memory; per-agent copies such as batched meshes are
  a separate, runtime question); modelling XSLT-authored characters (`lords.xslt`); a
  `--repo-moduledata` option to audit unreleased troop changes against released packs.
