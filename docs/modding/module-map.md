# The campaign map module (TAOM_Map)

## What this module is

`TAOM_Map` supplies Middle-earth itself: one baked campaign-map scene, the 988 settlements that sit
on it, and the art the two of them need. It ships no C# at all, so everything inside it is authored
either in the Bannerlord Modding Kit or typed into XML. It is also the one TAOM module that is not in
the git repo, which changes how you edit it, how you back it up, and what happens when the module is
reinstalled.

## Where it lives and how it reaches the game

The module folder is `TAOM_Map/`, under the game install's `Modules/` directory. This file lives in
the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator
gate with any fix.

There is **no build step and no deploy step**. The `TAOM` module is built out of `Main/_Module/` by
the csproj's `CopyModule` target, and `TAOM.Dependencies` out of `Dependencies/_Module/`. `TAOM_Map`
has no source tree anywhere: an edit to the live folder *is* the deployment. That cuts both ways.
Your change is live the next time the game boots, and nothing in git records that you made it.

**The repo copy is a dead shadow.** `Main/_Module/ModuleData/settlements.xml` exists, holds 863
`<Settlement>` rows against the live file's 988, and was last written on 2026-05-26. It is not
registered: `grep -n 'Settlements' Main/_Module/SubModule.xml` returns nothing at all.
<!-- measured: grep -c '<Settlement ' Main/_Module/ModuleData/settlements.xml ; grep -n 'Settlements' Main/_Module/SubModule.xml 2026-09-05 -->
Editing it changes nothing in game. Three older tools (`tools/Generate-Settlements.ps1`,
`tools/Apply-SettlementNames.ps1`, `tools/Settlement-Breakdown.ps1`) still target that stale copy, so
their output is a repo snapshot, not a live change. The full history of the split is in
[taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md).

Three authoring surfaces, three different tools:

| Surface | Tool | Where the result lands |
|---|---|---|
| Scene, terrain, navmesh, assets | The Modding Kit (`bin/Win64_Shipping_wEditor`, launched from the launcher's Modding Kit entry) | `TAOM_Map/SceneObj/Main_map/`, `Assets/`, `AssetPackages/` |
| Settlement data | A text editor, or one of the Python tools in [tools/README.md](../../tools/README.md) | `TAOM_Map/ModuleData/settlements.xml` |
| Settlement distance cache | The in-game MCM button (Options, Mod Options, TAOM, Map Tools) | `TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin` |

The Kit is a **separate engine build** with 108 DLLs against the shipping client's 85, and it carries
editor-only types the client does not have
([bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md) section 1). If
you cannot find an editor concept in the shipping decompile, that is why.

## Folder anatomy

Sizes and counts measured on 2026-09-05 against the live install.
<!-- measured: ls -1 TAOM_Map; ls -l SceneObj/Main_map; find Assets -type f | wc -l; grep -o '<game_entity' Prefabs/*.xml | wc -l 2026-09-05 -->

| Folder or file | What it holds | Needed to boot | Measured |
|---|---|---|---|
| `TAOM_Map/SubModule.xml` | The manifest: ids, version, dependencies, an empty `<SubModules/>`, 8 `<XmlNode>` rows | yes | 81 lines |
| `TAOM_Map/ModuleData/` | All XML data | yes | 23 entries |
| `TAOM_Map/ModuleData/settlements.xml` | The 988 settlements. UTF-8 **with BOM**, CRLF | yes | 1,153,217 bytes, 15,472 lines |
| `TAOM_Map/ModuleData/settlements.xslt` | Deletes vanilla Calradia from the merge buffer | yes | 15 lines |
| `TAOM_Map/ModuleData/DistanceCaches/` | `settlements_distance_cache_Default.bin`, `settlements_snapshot.json`, `last_rebuild_report.json` | no (but see below) | 3 files, 10.4 MB |
| `TAOM_Map/ModuleData/Languages/` | Root `language_data.xml` plus 12 language folders, each with `loc_settlements.xml` | no | 13 entries, 25 XML |
| `TAOM_Map/ModuleData/` (16 stubs) | `action_sets`, `action_types`, `collision_infos`, `combat_parameters`, `face_animations`, `item_holsters`, `items`, `native_parameters`, `partyTemplates`, `physics_materials`, `skins`, `spclans`, `spcultures`, `spkingdoms`, `spnpccharacters`, `spworkshops`, all still the Kit's template | yes | 197 to 326 bytes each |
| `TAOM_Map/ModuleData/project.mbproj` | Native resource registration, a loader separate from `<XmlNode>` | yes | 11 lines, 9 rows |
| `TAOM_Map/SceneObj/` | `Main_map/`, `Backups/`, 4 kitbash palettes, 2 `wip_*`, `temp_mission_scene`, 37 settlement and battle scenes | yes | 46 entries |
| `TAOM_Map/SceneObj/Main_map/` | The campaign map itself | yes | 7 files plus `ShaderCache/` |
| `TAOM_Map/SceneEditData/` | The Kit's editable sculpt data, one folder per scene | no | 46 folders, `Main_map/terrain_ed.bin` 348,743,509 bytes |
| `TAOM_Map/Assets/` | Compiled per-asset `.tpac` that both the Kit and the game read | yes | 7 folders, 1,896 files |
| `TAOM_Map/AssetSources/` | The raw FBX and PNG behind `Assets/` | no | 7 folders, 1,574 files |
| `TAOM_Map/AssetPackages/` | `pack0.tpac` to `pack4.tpac`, the player-facing cooked form | no | 5 files, 13.5 GB |
| `TAOM_Map/Atmospheres/` | 8 module-level atmosphere presets | no | 8 files |
| `TAOM_Map/NavMeshPrefabs/` | `Gondor_Mesh.bin`, a stampable navmesh patch | no | 1 file |
| `TAOM_Map/Prefabs/` | 141 prefab libraries. This is the folder the entity cap bites | yes | 93,830 `<game_entity>` |
| `TAOM_Map/Prefabs_Unused/` | The parked half of the 2026-07-24 split, plus `_INVENTORY.md` | no | 69 XML, 91,023 `<game_entity>` |
| `TAOM_Map/RuntimeDataCache/` | GUID-named cooked-asset cache the Kit writes | no | 3,069 files |
| `TAOM_Map/Shaders/D3D11/` | The module's precompiled shader cache | no | 3 files |
| `TAOM_Map/bin/Win64_Shipping_Client/` and `.../Win64_Shipping_wEditor/` | **Both empty.** The module has no assembly | no | 0 files each |

There is **no `GUI/` folder**. The campaign-map UI (the faction map, banner overlays) belongs to the
`TAOM` module, not to this one: see [faction-map.md](../features/faction-map.md).

Three of the four asset folders are here. `AssetSources/` is raw art nobody outside the team gets,
`Assets/` is the working tree both the Kit and the game read, `AssetPackages/` is the release form
for players, and `EmAssetPackages/` (the editor-distribution form, for other modders) is absent.
Loose `Assets/**` wins over a cooked pack where both exist, so on a dev install the five packs are
inert. All four are tabled in
[bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md) section 6.1.
`SceneObj/Backups/Main_map/` is the Kit's automatic one-step backup, the save immediately before the
current one: read it first when a map change goes wrong, but do not roll back to it blind.

## Worked example

### The manifest, all 81 lines

<!-- example file="TAOM_Map/SubModule.xml" id="TAOM_Map" -->
```xml
<Module>
	<Name value="TAOM_Map"/>
	<Id value="TAOM_Map"/>
	<Version value="v2.0.23" />
	<DefaultModule value="false" />
	<ModuleCategory value="Singleplayer"/>
	<ModuleType value="Community" />
	<DependedModules>
		<DependedModule Id="Native" />
		<DependedModule Id="SandBoxCore" />
		<DependedModule Id="Sandbox" />
		<DependedModule Id="CustomBattle" />
	</DependedModules>
	<DependedModuleMetadatas>
		<DependedModuleMetadata id="Native" order="LoadBeforeThis" version="v1.4.5.*" />
		<DependedModuleMetadata id="SandBoxCore" order="LoadBeforeThis" />
		<DependedModuleMetadata id="Sandbox" order="LoadBeforeThis" />
		<DependedModuleMetadata id="CustomBattle" order="LoadBeforeThis" />
		<DependedModuleMetadata id="TAOM" order="LoadBeforeThis" />
	</DependedModuleMetadatas>
	<SubModules/>
	<Xmls>
		<XmlNode>
			<XmlName id="Items" path="items"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="SPCultures" path="spcultures"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="NPCCharacters" path="spnpccharacters"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="partyTemplates" path="partyTemplates"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="Kingdoms" path="spkingdoms"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="Factions" path="spclans"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="WorkshopTypes" path="spworkshops"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
		<XmlNode>
			<XmlName id="Settlements" path="settlements"/>
			<IncludedGameTypes>
				<GameType value="Campaign"/>
				<GameType value="CampaignStoryMode"/>
			</IncludedGameTypes>
		</XmlNode>
	</Xmls>
</Module>
```

The three things to change when you copy this for a map of your own:

1. **`<Name>` and `<Id>` (lines 2 and 3).** The `Id` must equal the folder name under `Modules/`;
   every path lookup keys on it.
2. **`<DependedModuleMetadata id="TAOM" .../>` (line 19).** This row names TAOM, and it has no
   counterpart in `<DependedModules>`. For a standalone map, delete it. Copying the file "verbatim"
   and changing only two ids leaves a dependency hint pointing at a module the reader does not have.
3. **`<XmlName id="Settlements" path="settlements"/>` (line 73).** The only registration of
   `settlements.xml` anywhere in the install. The other seven `<XmlNode>` rows point at the Kit's
   empty stubs and contribute nothing; they are kept only because the files exist.

`<SubModules/>` is self-closing, which is the correct shape for a pure data and scene module, and it
is why both `bin/` folders are empty.

### How a settlement id binds to the map scene

This is the single binding the whole module rests on: the `id` of a `<Settlement>` is looked up as
the `name` of a `<game_entity>` in the map scene.

<!-- example file="TAOM_Map/ModuleData/settlements.xml" id="town_EW1" -->
```xml
  <Settlement id="town_EW1" name="{=Settlements.Settlement.name.town_EW1}Minas Tirith" owner="Faction.clan_empire_west_1" posX="902.954" posY="681.31" culture="Culture.gondor" gate_posX="904.0729" gate_posY="679.5563" text="{==Settlements.Settlement.text.town_EW1}The Tower of Guard, Gondor's capital and greatest fortress-city, built in seven concentric levels on the Hill of Guard at the eastern end of the White Mountains with Mount Mindolluin behind it. The White City houses the Citadel with the Tower of Ecthelion, the Court of the Fountain with the White Tree, and serves as seat of the Stewards of Gondor (and later King Elessar). Surrounded by the fertile Pelennor Fields enclosed by the great defensive wall called the Rammas Echor.">
    <Components>
      <Town id="town_comp_EW1" is_castle="false" background_crop_position="0.0" background_mesh="menu_empire_3" wait_mesh="wait_empire_town" gate_rotation="0.808" prosperity="5100">
        <Buildings>
```

Lines 2386 to 2397 are the twelve `<Building id level>` rows, 2398 to 2400 close `<Buildings>`,
`<Town>` and `<Components>`, and 2412 to 2416 are the `<CommonAreas>` block. All of that is covered
in [settlements.md](settlements.md). The part that matters for this chapter is `<Locations>`, because
every `scene_name` in it has to resolve to a `SceneObj/<name>/` folder in some active module:

<!-- excerpt file="TAOM_Map/ModuleData/settlements.xml" -->
```xml
    <Locations complex_template="LocationComplexTemplate.town_complex">
      <Location id="center" scene_name="taom_gondor_town_minas_tirith_forceatmo" scene_name_1="taom_gondor_town_minas_tirith_forceatmo" scene_name_2="taom_gondor_town_minas_tirith_forceatmo" scene_name_3="taom_gondor_town_minas_tirith_forceatmo" />
      <Location id="arena" scene_name="arena_empire_a" />
      <Location id="tavern" scene_name="empire_house_c_tavern_a" />
      <Location id="lordshall" scene_name_1="empire_castle_keep_a_l1_interior" scene_name_2="empire_castle_keep_a_l2_interior" scene_name_3="empire_castle_keep_a_l3_interior" />
      <Location id="prison" scene_name="empire_dungeon_stealth" />
      <Location id="house_1" scene_name="empire_house_d_interior_house" />
      <Location id="house_2" scene_name="empire_house_d_interior_house" />
      <Location id="house_3" scene_name="empire_house_d_interior_house" />
      <Location id="alley" />
    </Locations>
```

`center` points at one of this module's own scenes; the interiors reuse vanilla `empire_*` ones.

And the matching entity, the first 14 lines of it, in the map scene:

<!-- excerpt file="TAOM_Map/SceneObj/Main_map/scene.xscene" -->
```xml
		<game_entity name="town_EW1" old_prefab_name="" mobility="1">
			<tags>
				<tag name="town"/>
			</tags>
			<transform position="902.954, 681.310, 48.360" rotation_euler="0.086, -0.039, -1.255" scale="1.600, 1.600, 1.600"/>
			<scripts>
				<script name="Town Entity Manager">
					<variables>
						<variable name="Override Factor Color" value="true"/>
						<variable name="Factor Color" value="1.000, 0.961, 0.957, 1.000"/>
					</variables>
				</script>
			</scripts>
			<children>
```

The two agree on the number: `posX="902.954" posY="681.31"` in the XML against
`position="902.954, 681.310, ..."` in the scene. That is not enforced by anything, it is a convention
the map author keeps by hand, and getting it wrong puts the icon somewhere the party never walks to.

**The binding, in engine terms.** `SettlementVisual.OnStartup` does
`StrategicEntity = MapScene.GetCampaignEntityWithName(base.MapEntity.Id)`
(`SandBox__SandBox.View.cs:20553`). When that returns null it calls `AddNewEntityToMapScene`, which is
`GameEntity.Instantiate(_scene, entityId, ...)`, an attempt to instantiate a *prefab* of that name
(`MapScene.cs:144-155`), and then looks the entity up again (`SandBox__SandBox.View.cs:20561`). With
neither an entity nor a prefab, `StrategicEntity` stays null and the next line dereferences it
(`SandBox__SandBox.View.cs:20567`). So **adding settlement data for an id nobody placed in the scene
crashes map load**, which is why `tools/add_bluecraig_castles.py` adds only ids already present in
`scene.xscene`.

### settlements.xslt, all 15 lines

<!-- excerpt file="TAOM_Map/ModuleData/settlements.xslt" -->
```xml
<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform">
	<!-- Identity transformation - copies everything by default -->
	<xsl:output omit-xml-declaration="yes"/>

	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>

	<!-- Remove all vanilla Settlement elements -->
	<xsl:template match="Settlement"/>

</xsl:stylesheet>
```

An identity template plus one empty template. The empty one deletes every `<Settlement>` it matches.
`MBObjectManager.CreateMergedXmlFile` applies `xsltList[i]` to the **accumulated** document and only
then merges file `i` into it (`MBObjectManager.cs:966-980`), so this stylesheet runs against
whatever SandBox already contributed and wipes vanilla's 494 Calradian settlements before TAOM's 988
are added. `HandleXsltList` (`MBObjectManager.cs:949-963`) finds the file by convention: for a
registered `path="settlements"` it looks for `settlements.xsl`, then `settlements.xslt`. You never
name it in `SubModule.xml`.

Two consequences worth writing on the wall:

- **The loop starts at `i = 1`.** The first module in the merge never has its stylesheet applied. If
  your map module loaded first, `settlements.xslt` would not run and vanilla Calradia would ship
  alongside Middle-earth.
- **Deleting every vanilla settlement is what creates the landless-culture crash class.** A culture
  that ends up owning no settlement makes vanilla `SpawnLordParty` throw on the daily clan tick, with
  no TAOM frame in the stack. `Patch65_LandlessCultureSpawnGuard` guards it at runtime and the
  `LANDLESS_CULTURE` rule in `validate_moduledata.py` gates it in data:
  [lord-spawn-guard.md](../features/lord-spawn-guard.md).

### The map scene folder

<!-- excerpt file="TAOM_Map/SceneObj/Main_map/" -->
```
ShaderCache/
atmosphere.xml          4,941
flora.bin          39,765,047
flowmap.dds         1,048,704
navmesh.bin           867,839
references.txt         14,706
scene.xscene       11,899,947
terrain.bin        56,114,607
```
<!-- measured: ls -la SceneObj/Main_map/ 2026-09-05 -->

`scene.xscene` holds 22,796 `<game_entity>` elements against vanilla SandBox's 37,918.
<!-- measured: grep -o '<game_entity' SceneObj/Main_map/scene.xscene | wc -l 2026-09-05 -->
The terrain node is `node_dimension_x="16" node_dimension_y="16" node_size="100.000"`, a 1600 by 1600
world-unit rectangle; SandBox runs the same 16 by 16 grid at `node_size="53.000"`, so 848 by 848.
Everything the camera sees outside that rectangle is vista, and TAOM drives its vista from a single
flat texture (`vista_tileset=""`, `vista_diffuse_name="16K_Vista_02"`) rather than from vanilla's
`.gts` tileset. The attribute-to-Kit-field map is in
[main-map-vista.md](../reference/main-map-vista.md).

## The distance cache

`settlements_distance_cache_Default.bin` is the precomputed settlement-to-settlement travel cache. It
is not required to boot, and that is the problem: when the file is missing, the engine asserts
"Navigation type with id ... file is not found ... will generate cache (this will take some time)" and
then generates it in-process, on a 988-settlement map, at campaign load.

The path convention is engine-side: `<module folder>/ModuleData/DistanceCaches/settlements_distance_cache_<NavigationType>.bin`
(`NavigationCache.cs:485-487`). The first two values in the file are the scene XML CRC and the navmesh
CRC (`NavigationCache.cs:497-499`), which is why editing the scene invalidates the cache.

Rebuild it from inside the game, not from the editor button: load or start a campaign so
`Campaign.Current` exists, then Options, Mod Options, TAOM, Map Tools / Distance Cache Rebuild,
Rebuild Now. The write is atomic and leaves the previous cache as `.prev`
([editor-cache-rebuild.md](../features/editor-cache-rebuild.md)). TAOM's last full run, from
`last_rebuild_report.json`: 431.76 seconds, 988 settlements, 221 fortifications, phase 1 143.83 s over
487,578 pairs, phase 2 287.93 s over 421 neighbour pairs, smoke test passed.
<!-- measured: cat ModuleData/DistanceCaches/last_rebuild_report.json 2026-09-05 -->

`SettlementPositionScript.OnInit` decides how many caches to read:
`GetMapIsNavalDLC() || (!GetMapIsSandBox() && ModuleHelper.IsModuleActive("NavalDLC"))`
(`SandBox__SandBox.View.cs:17322`). Both flags are case-insensitive compares of the winning map
module's id against `"NavalDLC"` and `"Sandbox"` (`:17546-17549`), and this id is `TAOM_Map`, so both
are false. With NavalDLC active the third term is true: the game then also reads navigation types
2 and 3. `TAOM_Map` ships only the `Default` cache, and the lookup takes the last active module that
has a file (`SandBox__SandBox.View.cs:17358-17364`), so those two resolve to NavalDLC's caches, keyed
by settlement ids that do not exist on this map. The runtime effect of that is **unverified**.

## The prefab-entity cap

At editor startup every `<game_entity>` in every `Prefabs/` folder across **all loaded modules** is
enqueued into a native queue with a hard capacity of 131,072. Crossing it asserts before the Kit
finishes loading. TAOM hit 132,378 on 2026-07-24 after importing four packs, and the remediation was
the `Prefabs` / `Prefabs_Unused` split you see today
([editor-rglconcurrentqueue-assert-2026-07.md](../investigations/editor-rglconcurrentqueue-assert-2026-07.md)).

`python tools/check_prefab_budget.py` counts `TAOM_Map/Prefabs` **only**, and the queue is global, so
the tool reports a comfortable pass while the real total sits at the ceiling. Measured today:

| Module | `<game_entity>` in `Prefabs/` |
|---|---|
| `TAOM_Map` | 93,830 |
| `Native` | 23,828 |
| `NavalDLC` | 9,867 |
| `SandBox` | 2,847 |
| `LOTRLOME_Armory` | 48 |
| `StoryMode` | 39 |
| `TAOM` | 9 |
| **Total** | **130,468 of 131,072, 604 spare** |

<!-- measured: for m in */; do grep -ho '<game_entity' "$m"Prefabs/*.xml 2>/dev/null | wc -l; done 2026-09-05 -->

The tool printed `TOTAL: 93830 entities in 141 files (cap 131072, warn 120000)` and `OK` on the same
day. Sum every enabled module before believing it. `Prefabs_Unused/` holds another 91,023 entities
that are parked, not deleted, so moving a file back into `Prefabs/` can cost you the entire margin.

## Recipes

### Create the map module from zero

1. Make `Modules/<YourMap>/` in the game install and write `SubModule.xml` into it. Copy the 81 lines
   above and change the three things listed under the worked example.
2. Let the Kit's New Module step scaffold `ModuleData/`. It writes 16 engine stubs (`items.xml`,
   `spclans.xml`, `skins.xml`, `action_sets.xml` and the rest of the list in the anatomy table) plus
   `project.mbproj`. TAOM_Map has never edited any of the 16: each still holds
   `<replace_this_with_actual_nodes/>` or an empty root such as `<Items/>`. Leave them alone.
3. In the Kit, create a scene named exactly `Main_map`. The name is hardcoded: `MapScene.Load` calls
   `_scene.Read("Main_map", module.Id, ...)` (`MapScene.cs:206`).
4. Set the terrain node grid and sculpt the heightmap. TAOM runs 16 by 16 nodes at 100 units. The Kit
   keeps the editable sculpt in `SceneEditData/Main_map/terrain_ed.bin` and bakes
   `SceneObj/Main_map/terrain.bin` on save.
5. Set the vista before you show anyone the map, then paint the navmesh (TAOM has tile numbers for
   water only, see the gap note below).
6. Place one named `<game_entity>` per settlement, tagged by kind, and add exactly one
   `settlements_scripts` entity carrying `SettlementPositionScript`.
7. Write `settlements.xml` and `settlements.xslt`, register `Settlements` in `SubModule.xml`, and put
   your module after SandBox in the launcher's load order.
8. Import the two `world_map` grid textures losslessly (see the gotcha below), then rebuild the
   distance cache from inside a loaded campaign.

Check: `python tools/validate_moduledata.py` and `python tools/check_external_xslt.py`
Takes effect: full game restart
Code: No code changes needed

### Add a settlement to the map

1. **Place the entity first.** In the Kit, add a `<game_entity>` to `Main_map` whose `name` is the
   settlement id you intend to use, tag it by kind (`town`, `castle`, `village`, `wm_hideout`), and
   for a fortification give it a `Town Entity Manager` script. Save and close the Kit.
2. Read the entity's `<transform position="X, Y, Z">` out of `scene.xscene` and use X and Y as
   `posX`/`posY` in the XML. Do not invent coordinates.
3. Add the `<Settlement>` element to `TAOM_Map/ModuleData/settlements.xml`, with `<Components>`, then
   `<Locations>`, then `<CommonAreas>` in that order. Attribute by attribute:
   [settlements.md](settlements.md).
4. For a town or castle, add `gate_posX`/`gate_posY` as a pair. They are read only inside the
   `if (gate_posX != null)` branch, and the `gate_posY` read inside it is unguarded
   (`Settlement.cs:951-953`), so supplying one without the other throws.
5. Register the display name: `name="{=Settlements.Settlement.name.<id>}Default Text"` in
   `settlements.xml`, and a `<string id text/>` row in each of the 12 `loc_settlements.xml` files.
   See [strings-and-localization.md](strings-and-localization.md).
6. Rebuild the distance cache, then start a **new** campaign.

Check: `python tools/validate_moduledata.py` then `python tools/audit_scene_names.py`
Takes effect: new campaign only
Code: No code changes needed

### Modify a settlement (rename, re-own, move the entrance)

1. **Never change the `id`.** Ids are referenced by save files, by `bound="Settlement.<parent>"` on
   villages, and by party-template and recruitment XML. Everything else on the tag is editable.
2. **Rename:** edit the default text after `{=key}` in `settlements.xml` and the matching `text=` in
   all 12 `loc_settlements.xml`. `python tools/Apply-MapVillageNames.py` does the 13 files in one
   pass; edit its `NAMES` dict first.
3. **Re-own:** change `owner="Faction.<clan>"`, which exists only on towns and castles. Villages have
   no `owner` and inherit through `bound=`. `python tools/Assign-SettlementOwners.py` is the
   rules-driven version, dry-run by default.
4. **Move the entrance:** never hand-pick a coordinate. Run `taom.audit_settlement_entrances` in a
   loaded campaign and paste the engine's own replacement into the attribute it names
   (`gate_posX`/`gate_posY` for towns and castles, `posX`/`posY` for everything else).
5. If you edit by script, keep the bytes faithful: the file is UTF-8 **with BOM** and CRLF, and a
   plain `utf-8` text read plus a text-mode write strips the BOM and rewrites every line ending. The
   sanctioned idioms are in [tools/README.md](../../tools/README.md) under "XML I/O convention".

Check: `python tools/validate_moduledata.py`
Takes effect: next save load for `posX`/`posY`/`gate_pos*` and for display names; new campaign only
for `owner`, `prosperity`, `hearth` and `<Building level>`
Code: No code changes needed

### Delete a settlement

1. Removing the `<Settlement>` element from `settlements.xml` is the whole edit. The scene entity can
   stay: an entity with no settlement data is inert, not a fief, which is exactly the state the four
   Blue Craig castles sat in until `tools/add_bluecraig_castles.py` filled them in. The reverse (data
   with no entity) is the crash.
2. **Sweep the references first.** Grep the file for `bound="Settlement.<id>"`; every village bound to
   a fortification you delete must be deleted or re-bound. Then grep the repo's ModuleData for the id.
3. Deleting *vanilla* settlements is a different job and is already done for you, once, by
   `settlements.xslt`'s `<xsl:template match="Settlement"/>`. That is the pattern for removing content
   a module you depend on ships: an empty template in your own stylesheet, not an edit to theirs.
   TAOM's rules for writing one are in [xslt.md](../../.claude/rules/xslt.md).
4. Check that no culture is left owning zero settlements. That is the landless-culture CTD, and it
   surfaces on the daily clan tick with no TAOM frame in the stack.
5. Start a new campaign. Settlement ids are referenced by save files, which is why renaming one
   breaks a load, so removing one out from under an existing save is not something TAOM has tested.

Check: `python tools/validate_moduledata.py` (its `LANDLESS_CULTURE` rule is the one that matters here)
Takes effect: new campaign only
Code: No code changes needed

### Deploy a map change, and ship it

1. There is nothing to deploy. The folder you edited is the folder the game loads.
2. Prove the game is using your map. `MapScene.Load` calls `GetMainMapModule()`, which loops over
   every active module and keeps the **last** one that owns `SceneObj/Main_map/scene.xscene`, with no
   break (`MapScene.cs:270-281`). Three modules own one on this install: `SandBox`, `NavalDLC` and
   `TAOM_Map`. Boot the game and read `rgl_log` for TAOM's diagnostic lines
   (`TAOM: >>> Selected map module: 'X'`). If it names SandBox, move your module later in the
   launcher order ([battle-scenes.md](../features/battle-scenes.md)).
3. Because the module is not in git, land a repo-side gate alongside every live edit. The pattern is
   `apply_starting_fief_spread.py`: its default run is a drift check that exits 1, which is how a
   reinstall reverting the fix gets noticed.
4. Rebuild the distance cache **before** sweeping backups, because
   `pwsh tools/sweep_module_backups.ps1 -Apply` quarantines
   `settlements_distance_cache_Default.bin.prev` along with every other sidecar and closes your
   rollback window.
5. Package: `python tools/package_release.py --source "<game>/Modules" --dest <out> --dry-run` first,
   then without `--dry-run`. It copies an allow-list into a fresh folder and never deletes from the
   dev install. For this module it drops `RuntimeDataCache`, `AssetSources`, `Prefabs_Unused` and
   `*.xml.bak`, and keeps `ModuleData`, `SceneObj`, `SceneEditData`, `Assets`, `AssetPackages`,
   `Atmospheres`, `NavMeshPrefabs`, `Prefabs`, `Shaders` and `bin`.

Check: `python tools/package_release.py --source "<game>/Modules" --dest <out> --dry-run`
Takes effect: full game restart
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **The Kit holds the scene in memory.** Hand-editing `scene.xscene` while the Kit has `Main_map`
  open loses your edit on the Kit's next save. Change it in the UI, or close the Kit first
  ([main-map-vista.md](../reference/main-map-vista.md)).
- **A settlement with no scene entity crashes map load**, not settlement entry. The dereference is at
  `SandBox__SandBox.View.cs:20567`, immediately after the two failed lookups.
- **A `scene_name` with no `SceneObj/<name>/` folder crashes when that scene loads**, which reads as
  "battles near a specific place crash" rather than "bad XML". Windows resolves case-insensitively,
  so an exact-case audit false-flags `HART_ISENGARD` against `HART_isengard`
  ([scene-reference-audit.md](../reference/scene-reference-audit.md)).
- **An entrance on a navmesh island wedges AI with no crash and no log.**
  `PathFaceRecord.IsValid()` returns true for such a face, so an off-mesh check finds nothing; only
  comparing `FaceIslandIndex` against the rest of the map does
  ([taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)).
- **A mis-imported `worldmap_battle_scene_grid` crashes campaign load, not battle start.** Native
  `AccessViolationException` in `get_battle_scene_index_map`: boots to the menu fine, dies loading a
  campaign. Both conditions must hold, resource path `world_map/worldmap_battle_scene_grid` and a
  lossless import (Do Not Compress plus Dont Degrade). A roughly 4.19 MB uncompressed `.rdc` is
  correct, a roughly 700 KB compressed one is the tell
  ([worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md)).
- **Most of `settlements.xml` seeds new campaigns only.** `owner` is inside
  `if (Campaign.Current.CampaignGameLoadingType != SavedCampaign)` (`Settlement.cs:1036-1041`), and
  `prosperity`, `hearth` and `<Building level>` are gated the same way in the component
  deserializers. Coordinates are the exception: `_position` (`Settlement.cs:68`), `GatePosition`
  (`:271`) and `PortPosition` (`:273`) carry no `[SaveableProperty]` and are assigned only in
  `Settlement.Deserialize`, so an entrance fix reaches an existing save.
- **`project.mbproj`'s rows here are inert.** `XmlResource.GetMbprojxmls` reads
  `SelectSingleNode("base").SelectNodes("file")` (`XmlResource.cs:117`), and TAOM_Map's nine rows are
  written as `<Module id= name= type=>`, not `<file>`. Vanilla `Native` and `LOTRLOME_Armory` both use
  `<file>`. Nothing breaks today because all nine targets are Kit stubs, but do not reason about the
  file as if it were loading them. Its line 6 also gives `action_types.xml` the id `soln_action_sets`,
  a duplicate of line 5's.
- **Two `ModuleData` files here are real data that nothing loads.** `settlement_tracks.xml` (7,390
  bytes of `<MusicTracks>`) and `settlement_track_instruments.xml` (3,346 bytes of
  `<MusicInstruments>`) appear in neither `SubModule.xml` nor `project.mbproj`. SandBox registers its
  own pair at `SandBox/SubModule.xml:185` and `:192`, and that is what plays.
- **Load order is set in the launcher, not by `<DependedModules>`.** The engine takes the order from
  the launcher-supplied id array in `ModuleHelper.InitializeModules` (`ModuleHelper.cs:84-99`) and
  `GetModules` returns `_loadedModules.Values` (`ModuleHelper.cs:178-189`). The topological
  `GetSortedModules` (`ModuleHelper.cs:271`) has exactly two callers, `CustomBattleServer.cs:208` and
  `LobbyClient.cs:474`, both multiplayer. Fix a merge-order problem in the launcher list. The
  `version="v1.4.5.*"` pin on line 15 is likewise stale against the installed v1.4.8.
- **Stale names are a live category here.** All 8 `Atmospheres/` files still carry the old `lotraom_`
  and `lotrtaom_` prefixes while every `SceneObj` folder is now `taom_*`, and that exact class has
  bitten once (`lotraom_e_osgiliath` against on-disk `lotrtaom_e_osgiliath`). Nine `text=` values in
  `settlements.xml` also use `{==` where the other 3,269 keys use `{=`; what that does to the resolved
  key is not determined here, see [strings-and-localization.md](strings-and-localization.md).
  <!-- measured: grep -o '{==' ModuleData/settlements.xml | wc -l 2026-09-05 -->
- **Two TAOM docs are stale about this folder.** `docs/scene-entities.md` was extracted from a
  different asset tree and its 72 towns do not match the live scene's 78 `town` tags, so regenerate it
  with `tools/Generate-SceneEntitiesDoc.ps1` before citing it; and
  [main-map-vista.md](../reference/main-map-vista.md) says this module ships no `AssetPackages/`,
  which stopped being true on 2026-09-04.

## What TAOM has never written down

Say "we do not know" rather than guessing at any of these. Each one names where the answer would
come from.

- **How to paint a campaign-map navmesh for a land map.** The only tile numbers TAOM records are
  water-framed (shore 7, shallow ocean 18, deep ocean 19, under bridges 25, rivers 11, unnavigable
  10, from [warsails-custom-map-guide.md](../warsails-custom-map-guide.md)). Nothing covers
  face-group painting for land, which matters because `GetFaceTerrainType` returns
  `(TerrainType)FaceGroupIndex`, the fallback signal for battle-scene selection
  ([worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md)). The baked answer is
  `SceneObj/Main_map/navmesh.bin`, and nobody has read it back out.
- **How the 1600 by 1600 terrain was generated and imported.** `AssetSources/Support/` holds the
  candidate PNGs and [minas-tirith-plan.md](../scenes/minas-tirith-plan.md) section 1.3 names a
  heightmap workflow for a *battle* scene, but no doc records which file is the live height source,
  at what resolution, or how it reached the Terrain node.
- **The required scene tags and child entities per settlement kind.** Measured on the live scene:
  `town` 78, `castle` 193, `village` 609, `wm_hideout` 160, against 78 towns, 143 castles, 607
  villages and 159 hideouts in the XML, so some entities carry more than one kind tag.
  <!-- measured: grep -o '<tag name="castle"' SceneObj/Main_map/scene.xscene | wc -l 2026-09-05 -->
  Which tags are mandatory, which decorative, and whether the double-tagging is intended is recorded
  nowhere. An authoritative table would be the most useful thing anyone could add to this chapter,
  and it comes from reading the live `scene.xscene` against `SettlementVisual.OnStartup`.
- **Which of the two `settlements_scripts` entities the engine binds.** This scene has two, each
  carrying `SettlementPositionScript`; vanilla SandBox's `Main_map` has one. Whether the duplicate is
  harmless or double-registers the distance-cache system is undocumented. Flag it, do not copy it.
  <!-- measured: grep -o 'SettlementPositionScript' SceneObj/Main_map/scene.xscene | wc -l 2026-09-05 -->
- **What `gate_rotation` and the settlement-level `type` attribute do.** Both are in TAOM's data
  (`gate_rotation` on 221 `<Town>`, 159 `<Hideout>` and 131 `<Village>` nodes; `type="Hideout"` on
  159 settlements) and both are declared by `<game>/XmlSchemas/Settlements.xsd`, but neither is read
  by `Settlement.Deserialize` or the component deserializers, and a grep of the shipping-client and
  editor v1.4.8 decompiles finds no reader. Treat them as native or legacy and copy vanilla's values.
  <!-- measured: grep -o '<Town [^>]*gate_rotation=' ModuleData/settlements.xml | wc -l (and the Village/Hideout siblings) 2026-09-05 -->
- **How the 8 `Atmospheres/` presets bind to the 46 scenes.** Every scene's own `atmosphere.xml` is
  named `scene_atmosphere`, the preset filenames end `_forceatmo`, and the lookup is undocumented.
- **Which module wins when two ship the same `SceneObj/<name>/`.** The last-active-module rule is
  documented for `Main_map` only. `tools/audit_scene_names.py` cross-references every
  `Modules/*/SceneObj/`, so cross-module resolution evidently works, but the precedence rule for an
  ordinary settlement scene is unstated.
- **What the Kit's New Module wizard actually asks for.** Every TAOM guide starts from an
  already-scaffolded module. The nearest evidence is the stub set this module still carries.

## Numbers in this chapter

All measured 2026-09-05 against the live install and, where noted, the repo.

| Number | Command |
|---|---|
| 988 settlements | `grep -c '<Settlement id=' TAOM_Map/ModuleData/settlements.xml` |
| 78 towns, 143 castles, 607 villages, 159 hideouts | `grep -c 'is_castle="false"'`, `grep -c 'is_castle="true"'`, `grep -c '<Village '`, `grep -c '<Hideout '` on the same file |
| 221 settlements with `gate_posX` | `grep -c 'gate_posX=' TAOM_Map/ModuleData/settlements.xml` |
| `gate_rotation` on 221 `<Town>`, 159 `<Hideout>`, 131 `<Village>`; `type="Hideout"` on 159 | `grep -o '<Town [^>]*gate_rotation=' <file> \| wc -l` and its siblings |
| Terrain node 16 by 16 at `node_size="100.000"` here, `53.000` in SandBox | `grep -o '<terrain [^>]*>' <scene> \| head -1` |
| 1,153,217 bytes, 15,472 lines, BOM present, CRLF endings | `wc -c`, `wc -l`, and `head -c 3 settlements.xml \| od -c` for the BOM |
| 863 settlements in the repo shadow | `grep -c '<Settlement ' Main/_Module/ModuleData/settlements.xml` |
| 0 registrations of `Settlements` in the repo module | `grep -n 'Settlements' Main/_Module/SubModule.xml` (exit 1, no output) |
| 494 vanilla settlements deleted by the XSLT | `python -c "import re;print(len(re.findall(r'<Settlement[\s>]', open('SandBox/ModuleData/settlements.xml',encoding='utf-8-sig').read())))"` |
| 81 lines in `SubModule.xml`, 15 in `settlements.xslt`, 11 in `project.mbproj` | `wc -l` on each |
| 23 entries in `ModuleData/`, 46 in `SceneObj/`, 8 in `Atmospheres/` | `ls -1 <dir> \| wc -l` |
| 22,796 `<game_entity>` in `Main_map` (37,918 in SandBox's), 2 `SettlementPositionScript` (1 in SandBox's), 937 `Town Entity Manager` | `grep -o '<pattern>' <scene> \| wc -l` for each |
| Scene tag counts, `town` 78, `castle` 193, `village` 609, `wm_hideout` 160 | `grep -o '<tag name="town"' <scene> \| wc -l` and the three siblings |
| `Main_map` file sizes; 348,743,509 bytes for `SceneEditData/Main_map/terrain_ed.bin`; 5 packs and 13.5 GB in `AssetPackages/` | `ls -la` on each |
| 1,896 files in `Assets/`, 1,574 in `AssetSources/`, 3,069 in `RuntimeDataCache/` | `find <dir> -type f \| wc -l` |
| 93,830 entities in `Prefabs/` (141 files), 91,023 in `Prefabs_Unused/` (69 files) | `grep -ho '<game_entity' <dir>/*.xml \| wc -l` |
| 130,468 of 131,072 across all modules, 604 spare | `for m in */; do grep -ho '<game_entity' "$m"Prefabs/*.xml 2>/dev/null \| wc -l; done` |
| 25 XML in `ModuleData/Languages/` across 12 language folders | `find ModuleData/Languages -name '*.xml' \| wc -l` |
| Cache rebuild 431.76 s, 988 settlements, 221 fortifications | `cat ModuleData/DistanceCaches/last_rebuild_report.json` |
| 9 `{==` against 3,269 `{=` | `grep -o '{==' <file> \| wc -l` |

## Read next

- [settlements.md](settlements.md), the attribute-by-attribute reference for `settlements.xml`;
  [modules-overview.md](modules-overview.md), [module-taom.md](module-taom.md),
  [load-order-and-dependencies.md](load-order-and-dependencies.md) and
  [submodule-and-registration.md](submodule-and-registration.md).
- [taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md), the region-prefix table
  and the rename, re-own and entrance-fix procedures.
- [main-map-vista.md](../reference/main-map-vista.md), the only doc that maps a `scene.xscene`
  attribute to a named Modding Kit field, and
  [worldmap-battle-scene-grid.md](../reference/worldmap-battle-scene-grid.md), the second texture a
  map needs and its exact import settings.
- [editor-cache-rebuild.md](../features/editor-cache-rebuild.md), the distance cache end to end;
  [scene-reference-audit.md](../reference/scene-reference-audit.md), for "my settlement crashes when
  I enter it"; and
  [editor-rglconcurrentqueue-assert-2026-07.md](../investigations/editor-rglconcurrentqueue-assert-2026-07.md),
  the prefab-cap incident.
- [map-maker-quickstart.md](../scene-scripts/map-maker-quickstart.md) for the in-game scene-placement
  tooling, [settlement-building-levels.md](../features/settlement-building-levels.md) for the worked
  pattern of a bulk edit to this file, and
  [bannerlord-engine-and-toolchain.md](../reference/bannerlord-engine-and-toolchain.md) for the Kit
  build and the four asset folders.
