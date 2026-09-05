# Settlements

## What this file is

`settlements.xml` is the map: every town, castle, village and bandit hideout in TAOM, each with its position on the campaign map, its owner, its culture, its starting wealth, its buildings and the scene files the game loads when you walk in. One `<Settlement>` element is one place, and the live file holds 988 of them. <!-- measured: rg -oF '<Settlement ' on the live TAOM_Map settlements.xml, piped to wc -l 2026-09-05 --> The file the engine actually reads lives in the `TAOM_Map` module inside the game install, and the copy this repo ships is a shadow that nothing loads.

## Where it lives and how it is registered

| Path | What it is |
|---|---|
| `TAOM_Map/ModuleData/settlements.xml` | **The live file.** 1,153,217 bytes, last written 2026-09-04. Every edit that reaches a player is made here. |
| `TAOM_Map/ModuleData/settlements.xslt` | 15 lines. An identity transform plus `<xsl:template match="Settlement"/>`, which deletes every vanilla settlement so Calradia does not appear underneath TAOM's map. |
| `TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml` | 12 files, 1,227 `<string>` rows each. The translated display names and Encyclopedia text. |
| `Main/_Module/ModuleData/settlements.xml` | **A stale shadow. Editing it changes nothing in game.** 1,023,041 bytes, last written 2026-05-26, 863 settlements against the live file's 988. |

This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. <!-- measured: ls -l on both settlements.xml copies, wc -l on settlements.xslt, and rg -oF '<string id=' on each of the 12 loc_settlements.xml 2026-09-05 -->

**Registration.** `TAOM_Map/SubModule.xml` line 73 carries `<XmlName id="Settlements" path="settlements"/>`. The engine maps that `id` to the object type through `Campaign.cs:1550`, `objectManager.RegisterType<Settlement>("Settlement", "Settlements", 25u)`, with the three component types sharing one list name: `Village` at `Campaign.cs:1552`, `Hideout` at `:1553`, `Town` at `:1554`. Root element `<Settlements>`, per-entry element `<Settlement>`, engine class `TaleWorlds.CampaignSystem.Settlements.Settlement`.

**Why the repo copy does nothing, in full.** The build copies `Main/_Module/` into the game install, so `TAOM/ModuleData/settlements.xml` exists there and is byte-for-byte the repo shadow (both 1,023,041 bytes, both dated 2026-05-26). It is never loaded, because `Main/_Module/SubModule.xml` registers 100 `<XmlName>` rows and none of them is `Settlements`. A file the engine has no registration for is inert no matter which module it sits in. That is the whole answer to "does my repo edit reach the game": for this one file, no, and the deployment step is not the reason. <!-- measured: ls -l on TAOM/ModuleData/settlements.xml in the install, rg -c '<XmlName' and rg -n 'Settlements' on Main/_Module/SubModule.xml 2026-09-05 -->

The three PowerShell scripts named for this file, [`tools/Apply-SettlementNames.ps1`](../../tools/Apply-SettlementNames.ps1), [`tools/Generate-Settlements.ps1`](../../tools/Generate-Settlements.ps1) and [`tools/Settlement-Breakdown.ps1`](../../tools/Settlement-Breakdown.ps1), all target that shadow. Do not reach for them expecting an in-game change ([taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)).

**What is in the live file.** 988 settlements: 221 fortifications (78 towns and 143 castles), 607 villages, 159 hideouts and 1 `CustomSettlementComponent` (the retirement retreat). Under them sit 2,509 `<Building>` rows, 1,898 `<Location>` rows and 2,055 `<Area>` rows. Every settlement carries a `culture=`, 235 carry `text=`, and exactly the 221 fortifications carry `gate_posX`. No settlement carries `port_posX`. <!-- measured: rg -oF on each element and attribute name against the live settlements.xml, and a python regex pass over the 988 <Settlement> open tags 2026-09-05 -->

## Attributes

The identity attribute comes from the base class every one of these objects derives from.

<!-- engine-table type="TaleWorlds.ObjectSystem.MBObjectBase" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none, the read is unguarded and a missing `id` throws | The code name of the settlement, or of the `<Town>` / `<Village>` / `<Hideout>` component. Never shown to the player. Everything else that points here writes it as `Settlement.<id>`. | `MBObjectBase.cs:61` |

### `<Settlement>`

<!-- engine-table type="TaleWorlds.CampaignSystem.Settlements.Settlement" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `name` | string, localised | yes | none, unguarded read | The name on the map and in menus. Written as `{=KEY}Fallback`; the fallback is the English text, because there is no English `Languages/` folder. | `Settlement.cs:947` |
| `posX` | float | yes | none, unguarded read | Map position, X axis. Must match where the settlement's entity actually sits in the map scene. | `Settlement.cs:948` |
| `posY` | float | yes | none, unguarded read | Map position, Y axis. Together the pair also seeds the gate and port points when those are absent. | `Settlement.cs:948` |
| `gate_posX` | float | no | the gate point falls back to `posX`, `posY` | The spot a party actually stands to enter, and the anchor for sieges and raids. All 221 fortifications set it; nothing else does. | `Settlement.cs:951` |
| `gate_posY` | float | only if `gate_posX` is present | none inside that branch, so `gate_posX` alone throws | Y half of the gate point. Always author the pair. | `Settlement.cs:953` |
| `port_posX` | float | no | the port point is seeded from `posX`, `posY` but `HasPort` stays false | Naval only. Supplying it is what makes a settlement reachable by ship. No TAOM settlement uses it. | `Settlement.cs:956` |
| `port_posY` | float | only if `port_posX` is present | none inside that branch | Y half of the dock point. Author as a pair. | `Settlement.cs:958` |
| `culture` | ref, `Culture.<id>` | no in the parser, yes in practice | `Settlement.Culture` stays null and militia spawning throws | Which culture the place belongs to: scene dressing, townsfolk, vanilla militia troop types. The dotted prefix is mandatory; a bare `gondor` throws `MBInvalidReferenceException`. | `Settlement.cs:961` |
| `text` | string, localised | no | empty | The Encyclopedia flavour paragraph. No gameplay effect. 235 settlements carry one. | `Settlement.cs:962` |
| `owner` | ref, `Faction.<clanId>` | no | no starting owner | The clan that holds this fief when a **new campaign** begins. Applied only when the settlement has a `<Town>` component, and skipped entirely on a save load. All 221 rows sit on fortifications; a village never has one. | `Settlement.cs:1038` |

### `<Town>` (towns and castles both)

<!-- engine-table type="TaleWorlds.CampaignSystem.Settlements.Town" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Town.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `is_castle` | bool | no | `false`, meaning a town | The one switch between town and castle. A castle has no market, tavern, arena or workshops and uses the castle building ids. | `Town.cs:684` |
| `background_crop_position` | float | yes | none, unguarded `float.Parse` | Vertical framing offset for the settlement's painted panel. Every shipped value is `0.0`. | `Town.cs:685` |
| `background_mesh` | string | yes | none, unguarded read | The large Encyclopedia picture. The UI also appends `_t` to this name for the small thumbnail, so you must ship both `X` and `X_t`. | `Town.cs:686` |
| `wait_mesh` | string | yes | none, unguarded read | The backdrop behind the text menu while the player waits here. | `Town.cs:687` |
| `prosperity` | float | yes on a new campaign, ignored on a save | none, unguarded `float.Parse` | Starting wealth. Sets the fief's value to the AI, and picks the crowd band that decides how many people spawn in the town scene. | `Town.cs:690` |

### `<Village>`

<!-- engine-table type="TaleWorlds.CampaignSystem.Settlements.Village" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Village.cs" method="Deserialize" inert="castle_background_mesh" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `background_crop_position` | float | yes | none, unguarded `float.Parse` | Same framing offset. Every shipped value is `0.0`. | `Village.cs:285` |
| `background_mesh` | string | yes | none, unguarded read | The village painting, plus `X_t` for the thumbnails in the town-management and kingdom screens. | `Village.cs:286` |
| `castle_background_mesh` | string | yes to be **present**, but read but has no effect | none, unguarded read, so omitting it throws | Parsed into `SettlementComponent.CastleBackgroundMeshName` and then read by nothing in the v1.4.8 decompile. Write the attribute, do not expect the value to do anything. | `Village.cs:287` |
| `wait_mesh` | string | yes | none, unguarded read | Backdrop behind the village menu. | `Village.cs:288` |
| `hearth` | int | yes on a new campaign, ignored on a save | none, unguarded `int.Parse` | The village's wealth number. Sets its value to the AI and picks the villager crowd band. | `Village.cs:291` |
| `village_type` | ref, `VillageType.<id>` | no in the parser, yes in practice | null, and the daily production tick then throws | What the village produces, which also picks its scene prop and animation set: `cattle_farm`, `iron_mine`, `clay_mine`, `lumberjack` and the rest. | `Village.cs:293` |
| `bound` | ref, `Settlement.<id>` | yes on a new campaign, ignored on a save | none, the next line dereferences it | The castle or town this village belongs to. This is what gives the village an owner and puts it in that fief's village list. | `Village.cs:300` |

### `<Hideout>`

<!-- engine-table type="TaleWorlds.CampaignSystem.Settlements.Hideout" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Hideout.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `background_crop_position` | float | yes | none, unguarded `float.Parse` | Framing offset for the hideout picture. | `Hideout.cs:164` |
| `background_mesh` | string | yes | none, unguarded read | Hideout picture, plus `X_t`. | `Hideout.cs:165` |
| `wait_mesh` | string | yes | none, unguarded read | Backdrop for the hideout menu. | `Hideout.cs:166` |

A hideout starts invisible and becomes visible only when the player scouts it, and its faction is taken at runtime from whichever bandit party is inside, so `owner=` on the parent `<Settlement>` does nothing for one.

### The four scene slots, which no attribute table can name

The scene-file attributes are read through a name the engine builds in a loop, `"scene_name" + ("_" + i)` for `i` in 0 to 3, so they are documented here rather than in a table the attribute checker can read.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Settlements.Settlement" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs" lines="1003-1009" -->

| Attribute on `<Location>` | What it does |
|---|---|
| `scene_name` | Slot 0. The scene used at fortification level 0, and the fallback every other slot falls back to when it is empty (`Location.cs:277-285`). |
| `scene_name_1` | Slot 1. Fortification level 1. Villages always ask for this slot (`VillageEncounter.cs:19`). |
| `scene_name_2` | Slot 2. Fortification level 2. |
| `scene_name_3` | Slot 3. Fortification level 3, the fully walled version. |

**The trap.** The loop runs all four slots unconditionally and writes an empty string into every slot whose attribute is missing (`Settlement.cs:1003-1009`). Writing a `<Location>` element therefore blanks all four scene names the template supplied and refills only the ones you list. In the live file, 221 `<Location>` rows set slots 1 to 3 and leave slot 0 empty, and 78 set no scene name at all. Those work because fortification levels floor at 1 and because those rooms are never asked for at level 0, not because the template's names survived. <!-- measured: python regex pass over the 1,898 <Location> nodes in the live settlements.xml, counting slot presence 2026-09-05 -->

### Attributes the engine never reads

Present in shipped data, read by nothing in the v1.4.8 managed code. They are safe to copy and safe to drop.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Settlements.Settlement" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs" lines="944-1045" -->

| Attribute | Where it appears | Occurrences in the live file |
|---|---|---|
| `gate_rotation` | `<Settlement>`, `<Town>`, `<Village>`, `<Hideout>` | 511 |
| `map_icon` | `<Hideout>` and the `CustomSettlementComponent` | 160 |
| `type="Hideout"` | `<Settlement>` | 159. What makes a settlement a hideout is the `<Hideout>` component, not this. |
| `type` | `<Area>` | 2,055, in six values: `Pasture`, `Thicket` and `Bog` 607 each, `Backstreet`, `Clearing` and `Waterfront` 78 each. What identifies an area is its position in the list, not this attribute. |
| `trade_bound` | nowhere | 0. There is no such attribute in the engine. A village bound to a town trades with that town; a village bound to a castle has its market chosen at runtime. |

<!-- measured: rg -oF for each attribute name against the live settlements.xml, and rg -o '<Area type="[A-Za-z]+"' piped through sort and uniq -c 2026-09-05 -->

## Child elements

### Under `<Settlement>`

<!-- engine-table type="TaleWorlds.CampaignSystem.Settlements.Settlement" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Settlement.cs" method="Deserialize" inert="" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Components>` | container | yes in practice | the session-start call has no null check and throws | Holds exactly one component: `<Town>`, `<Village>`, `<Hideout>` or `<CustomSettlementComponent>`. The element name is what routes it. Two components do not merge; the last one wins. | `Settlement.cs:965` |
| `<CustomSettlementComponent>` | element | no | not used | The escape hatch for a component with no element name of its own. One instance ships, the retirement retreat. | `Settlement.cs:970` |
| `component_name` | string | yes on that element | an unknown or missing name makes the component null, and the next line throws | Names the registered component type, in practice `RetirementSettlementComponent`. | `Settlement.cs:972` |
| `<Locations>` | container | no, but any settlement the player can enter needs it | the settlement has no interiors | Does not define rooms. It points at a reusable template and then overrides scene files for rooms that template already has. | `Settlement.cs:982` |
| `complex_template` | ref, `LocationComplexTemplate.<id>` | yes on that element | null, and building the location complex then throws | One of `town_complex` (9 rooms), `castle_complex` (3), `village_complex` (1), `hideout_complex` (1), `retreat_complex` (1), `ambush_complex` (1). | `Settlement.cs:984` |
| `<Location>` | element | no | the template's own scene names stand | One room being re-skinned. Only children literally named `Location` are read. | `Settlement.cs:995` |
| `id` | string | yes on that element | unguarded read | Which room: `center`, `lordshall`, `prison`, `tavern`, `arena`, `alley`, `house_1` to `house_3`, `village_center`, `hideout_center`. An id the template does not define crashes the load. | `Settlement.cs:997` |
| `max_prosperity` | int | no | the template's own cap stands | The most NPC agents allowed in that room at once. Lower it when a room is visually overcrowded. | `Settlement.cs:998` |
| `<CommonAreas>` | container | no | the settlement has no gang alleys or outdoor areas | On a new campaign each child appends an alley tagged `alley_1`, `alley_2`, `alley_3` in document order. On a save load it re-initialises the existing entries in place instead. | `Settlement.cs:1013` |
| `<Area>` | element | no | not used | One area. Its list position becomes its tag, which is what the scene artist places in the town scene. Towns and villages ship three each; castles ship none. | `Settlement.cs:1020` |
| `name` | string, localised | yes on that element | unguarded read | The player-facing label of the alley or outdoor area. | `Settlement.cs:1022` |

### Under `<Town>`

<!-- engine-table type="TaleWorlds.CampaignSystem.Settlements.Town" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.Settlements/Town.cs" method="Deserialize" inert="" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Buildings>` | container | no | the fief starts with no buildings | **Replaces, never appends**: the list is cleared before the loop. Read only on a new campaign. The container name must be exactly `Buildings`. | `Town.cs:701` |
| `<Building>` | element | no | not used | One building that exists at campaign start. The child name must be exactly `Building`. | `Town.cs:707` |
| `id` | string, **bare id, no prefix** | yes | unguarded read, and an id no `BuildingType` matches throws on the next line | Which building, for example `building_settlement_fortifications` or `building_castle_granary`. Unlike almost everything else in this file it takes a bare id with no `BuildingType.` prefix. | `Town.cs:709` |
| `level` | int | no | that building type's own start level | How far it is already built, 0 to 3. On the fortifications building this number also picks which walled scene loads, because `Town.GetWallLevel()` reads it and hands it to `Location.GetSceneName(n)` (`Town.cs:657-673`). | `Town.cs:710` |

## Worked example

Four entries copied out of the live file, one of each shape. A castle first, because it is the smallest complete fief.

<!-- example file="TAOM_Map/ModuleData/settlements.xml" id="castle_EN3" -->

```xml
<Settlement id="castle_EN3" name="{=Settlements.Settlement.name.castle_EN3}Tûr Morva" owner="Faction.clan_empire_north_6" posX="543.07" posY="804.873" culture="Culture.empire" gate_posX="545.1684" gate_posY="807.2845">
    <Components>
      <Town id="castle_comp_EN3" is_castle="true" background_crop_position="0.0" background_mesh="menu_empire_1" wait_mesh="wait_empire_town" gate_rotation="0.908" prosperity="890">
        <Buildings>
          <Building id="building_castle_fortifications" level="2" />
          <Building id="building_castle_barracks" level="1" />
          <Building id="building_castle_training_fields" level="1" />
          <Building id="building_castle_guard_house" level="0" />
          <Building id="building_castle_siege_workshop" level="1" />
          <Building id="building_castle_castallans_office" level="1" />
          <Building id="building_castle_granary" level="1" />
          <Building id="building_castle_craftmans_quarters" level="0" />
          <Building id="building_castle_farmlands" level="1" />
          <Building id="building_castle_mason" level="0" />
          <Building id="building_castle_roads_and_paths" level="1" />
        </Buildings>
      </Town>
    </Components>
    <Locations complex_template="LocationComplexTemplate.castle_complex">
      <Location id="center" scene_name="empire_castle_004" scene_name_1="empire_castle_004" scene_name_2="empire_castle_004" scene_name_3="empire_castle_004" />
      <Location id="lordshall" scene_name_1="empire_castle_keep_a_l1_interior" scene_name_2="empire_castle_keep_a_l2_interior" scene_name_3="empire_castle_keep_a_l3_interior" />
      <Location id="prison" scene_name="empire_dungeon_stealth" />
    </Locations>
  </Settlement>
```

1. **`owner="Faction.clan_empire_north_6"`.** The clan that holds it on day one of a new campaign. Change this and nothing changes in a save already in progress.
2. **`prosperity="890"`.** Castle wealth. The live castles run 420 to 1,100 with a median of 810, so 890 is an ordinary one.
3. **`<Building id="building_castle_fortifications" level="2" />`.** Two things at once: the AI's wall strength, and the scene slot the game loads. Level 2 makes the game ask for `scene_name_2`.

A town differs in `is_castle="false"`, in the town building ids, and in having rooms a castle does not.

<!-- example file="TAOM_Map/ModuleData/settlements.xml" id="town_comp_EW1" -->

```xml
<Town id="town_comp_EW1" is_castle="false" background_crop_position="0.0" background_mesh="menu_empire_3" wait_mesh="wait_empire_town" gate_rotation="0.808" prosperity="5100">
        <Buildings>
          <Building id="building_settlement_fortifications" level="3" />
          <Building id="building_settlement_barracks" level="3" />
          <Building id="building_settlement_training_fields" level="3" />
          <Building id="building_settlement_guard_house" level="2" />
          <Building id="building_settlement_siege_workshop" level="3" />
          <Building id="building_settlement_tax_office" level="2" />
          <Building id="building_settlement_marketplace" level="2" />
          <Building id="building_settlement_warehouse" level="3" />
          <Building id="building_settlement_mason" level="3" />
          <Building id="building_settlement_waterworks" level="3" />
          <Building id="building_settlement_courthouse" level="3" />
          <Building id="building_settlement_roads_and_paths" level="2" />
        </Buildings>
      </Town>
```

That is the `<Town>` component of `town_EW1`, Minas Tirith, whose full entry also carries nine `<Location>` rows and three `<Area>` rows. Its 5,100 prosperity puts it in the top crowd band, which is what fills the streets with people.

A village has no `owner` of its own. It has `bound`.

<!-- example file="TAOM_Map/ModuleData/settlements.xml" id="castle_village_EN3_1" -->

```xml
<Settlement id="castle_village_EN3_1" name="{=Settlements.Settlement.name.castle_village_EN3_1}Brynbuarth" posX="545.806" posY="793.34" culture="Culture.empire" text="{=Settlements.Settlement.text.castle_village_EN3_1}Brynbuarth sits in a plateau in the Dryatic mountains. Highland cattle thrive on the grasses of the heights.">
    <Components>
      <Village id="castle_village_comp_EN3_1" village_type="VillageType.clay_mine" hearth="610" bound="Settlement.castle_EN3" background_crop_position="0.0" background_mesh="gui_bg_village_empire" wait_mesh="wait_empire_village" castle_background_mesh="gui_bg_castle_empire" />
    </Components>
    <Locations complex_template="LocationComplexTemplate.village_complex">
      <Location id="village_center" scene_name="empire_village_j" />
    </Locations>
    <CommonAreas>
      <Area type="Pasture" name="{=fOUsLdZR}Pasture" />
      <Area type="Thicket" name="{=66Mzk0NZ}Thicket" />
      <Area type="Bog" name="{=iXA5SttU}Bog" />
    </CommonAreas>
  </Settlement>
```

1. **`bound="Settlement.castle_EN3"`.** Ownership, raid target and fief membership all come from this one reference. Point it at the castle above and the village follows that castle through every conquest for the rest of the campaign.
2. **`village_type="VillageType.clay_mine"`.** Production and scene dressing together.
3. **`hearth="610"`.** Above the 600 threshold, so this village sits in the busiest villager band.

A hideout is the smallest shape in the file.

<!-- example file="TAOM_Map/ModuleData/settlements.xml" id="hideout_forest_1" -->

```xml
<Settlement id="hideout_forest_1" name="{=Settlements.Settlement.name.hideout_forest_1}Dunlending Raider's Camp" type="Hideout" posX="773.894" posY="888.486" culture="Culture.dunland_raiders">
    <Components>
      <Hideout id="hideout_forest_1" map_icon="bandit_hideout_b" background_crop_position="0.0" background_mesh="empire_twn_scene_bg" wait_mesh="wait_hideout_forest" gate_rotation="6.283" />
    </Components>
    <Locations complex_template="LocationComplexTemplate.hideout_complex">
      <Location id="hideout_center" scene_name="bandit_forest_sv" />
    </Locations>
  </Settlement>
```

1. **`culture="Culture.dunland_raiders"`.** One of the 8 cultures carrying `is_bandit="true"` in `Main/_Module/ModuleData/taom_spcultures.xml`. Those 8 are exactly the 8 cultures that appear on hideouts in the live file.
2. **`scene_name="bandit_forest_sv"`.** A folder that has to exist under some module's `SceneObj/`. This one is vanilla's, in `SandBox/SceneObj/bandit_forest_sv`.

### The stylesheet that clears Calradia

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

The identity template copies everything, and the one-line template below it matches every `Settlement` element and emits nothing. That is how vanilla's towns stay off TAOM's map. It is also the pattern for deleting anything from an upstream file you do not own: match it, emit nothing, keep the identity template above it intact ([`.claude/rules/xslt.md`](../../.claude/rules/xslt.md)).

### One localisation row

<!-- excerpt file="TAOM_Map/ModuleData/Languages/DE/loc_settlements.xml" -->

```xml
<string id="Settlements.Settlement.name.town_EW1" text="Minas Tirith" />
```

The `id` is the key from the `{=KEY}` in `name=`, without the braces. There is no English `Languages/` folder, so the fallback text inside the master file is the English name; the 12 loc files are the other 12 languages.

## Recipes: Add / Modify / Delete

### Add

Adding a settlement is the one operation in this file with a prerequisite outside the file: the map scene must already contain an entity with the same id.

1. **Find or place the scene entity.** Open `TAOM_Map/SceneObj/Main_map/scene.xscene` and search for the id. A settlement entity looks like `<game_entity name="castle_GBC1" old_prefab_name="map_icon_castle_empire" mobility="1">` with a `<tag name="castle"/>` and a `<transform position="250.684, 1200.344, 79.999" .../>`. The naming convention per region is in [`docs/scene-entities.md`](../scene-entities.md); note that its counts are stale against the live map (it lists 72 town and 132 castle entities, 204 total, while the live file now has 221 fortifications), so use it for the id shapes and re-derive counts yourself.
2. **Take `posX` and `posY` from that transform**, first two numbers, dropping the third. This is not a convention, it is how the shipped tool did it: `tools/add_bluecraig_castles.py` lists `castle_GBC1` at `250.684, 1200.344`, which is the scene transform above, character for character. Adding settlement data for an id with no scene entity crashes the map load in `SettlementVisual.OnStartup` ([`tools/add_bluecraig_castles.py`](../../tools/add_bluecraig_castles.py) lines 5 to 9).
3. **Copy a whole sibling entry** of the shape you want from the live file. Copy the `<Locations>` block from that sibling too, unchanged, unless you have a scene of your own.
4. **Change the ids** (`<Settlement id>`, the component `id`), the name key, `posX`, `posY`, `culture`, and for a fortification `owner`, `prosperity` and the `<Building>` levels. For a village change `bound`, `village_type` and `hearth`.
5. **Set `gate_posX` and `gate_posY`** on a fortification. The shipped tool sets them equal to `posX` and `posY`, which is safe; a hand-picked value is not, see the entrance gotcha below.
6. **Add a row per language.** 12 rows, one in each `TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml`, using the key from step 4. Tolkien proper nouns take the same spelling in every language.
7. **Rebuild the settlement distance cache**: in game, Options, Mod Options, TAOM, Map Tools, Rebuild Now, then reload ([editor-cache-rebuild.md](../features/editor-cache-rebuild.md)). The cache is `TAOM_Map/ModuleData/DistanceCaches/settlements_distance_cache_Default.bin`, 10,205,146 bytes, and it is keyed by settlement id.

Check: `python tools/validate_moduledata.py` then `python tools/audit_scene_names.py`
Takes effect: new campaign only
Code: No code changes needed

### Add a hideout

Shorter, because a hideout has no owner, no buildings and one room.

1. Copy an existing `<Settlement ... type="Hideout">` block in `TAOM_Map/ModuleData/settlements.xml`.
2. Change `id`, the component `id`, `posX`, `posY`, and set `culture=` to one of the 8 cultures with `is_bandit="true"`.
3. Give it a unique `name="{=Settlements.Settlement.name.<id>}Camp Name"` and add the 12 loc rows. The shipped hideouts register their keys this way, not in `taom_module_strings.xml`; step 5 of the "add a new hideout" list in [bandit-management.md](../features/bandit-management.md) does not match what the live file actually does.
4. Point `<Location id="hideout_center" scene_name="X">` at a folder that exists under some module's `SceneObj/`. Raiding a hideout whose scene is missing crashes.
5. If the culture is new, it also needs a `bandit_boss` troop with `occupation="Bandit"`, or the guard dialogue hijacks the boss conversation and the bandits never turn hostile ([bandit-management.md](../features/bandit-management.md)).
6. Rebuild the distance cache as above.

Check: `python tools/audit_scene_names.py`
Takes effect: new campaign only
Code: No code changes needed

### Modify

**1. Change who owns a fief.** Edit `owner="Faction.<clanId>"` on the fortification's `<Settlement>` tag in the live file. Never touch villages; they follow their `bound` fortification. For a planned redistribution use [`tools/Assign-SettlementOwners.py`](../../tools/Assign-SettlementOwners.py) (dry-run by default, `--apply` writes with a `.bak`) or [`tools/apply_starting_fief_spread.py`](../../tools/apply_starting_fief_spread.py), whose default run is a drift check that exits 1, which is how you notice a module reinstall reverted it.

Check: `python tools/apply_starting_fief_spread.py`
Takes effect: new campaign only
Code: No code changes needed

**2. Change a settlement's culture.** Edit `culture="Culture.<id>"`. Unlike ownership this one reaches campaigns already in progress, because `Settlement.Culture` is not an engine-saved field and is re-read from XML on every load ([culture-conversion.md](../features/culture-conversion.md)). That is why the 2026-08-04 Khand retag of 26 K-series settlements landed on existing saves. Check the culture actually has troops behind it: a retag wakes bindings that were dormant while nothing carried the culture ([lord-spawn-guard.md](../features/lord-spawn-guard.md)).

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE`
Takes effect: next save load
Code: No code changes needed

**3. Rename a settlement.** Change the fallback text after `{=KEY}` in `name=` and the matching `text=` row in all 12 `loc_settlements.xml`. **Never change the `id`.** [`tools/Apply-MapVillageNames.py`](../../tools/Apply-MapVillageNames.py) does all 13 files in one pass and preserves the file's UTF-8 and CRLF bytes. The idiom table per region prefix is in [taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md).

Check: `python tools/check_external_loc_coverage.py`
Takes effect: next save load
Code: No code changes needed

**4. Retune prosperity or hearth.** `prosperity` on `<Town>`, `hearth` on `<Village>`. Read the current spread first with [`tools/analyze_settlement_prosperity.py`](../../tools/analyze_settlement_prosperity.py) `--stdout`, then write with [`tools/rebalance_settlement_prosperity.py`](../../tools/rebalance_settlement_prosperity.py) (`--apply` writes, default is a dry run). The thresholds worth knowing: a town crosses into the busiest crowd band at 5,000 and the middle one at 2,000 (`Town.cs:738-749`); a village at 600 and 200 (`Village.cs:320-331`). Eight cultures are held to a committed floor of 4,800 town, 950 castle, 500 hearth by `tools/settlement_economy_floor.json`.

Check: `python tools/validate_moduledata.py --code SETTLEMENT_ECONOMY_FLOOR`
Takes effect: new campaign only
Code: No code changes needed

**5. Retune buildings.** Do not hand-edit 2,509 `<Building level>` values. Edit the fief's entry in `DECISIONS` in [`tools/author_settlement_buildings.py`](../../tools/author_settlement_buildings.py), regenerate, then apply per culture with [`tools/apply_settlement_buildings.py`](../../tools/apply_settlement_buildings.py) (`--culture <c>` dry run, then `--apply`). Levels are 0 to 3 and fortifications floor at 1, below which the engine asserts ([settlement-building-levels.md](../features/settlement-building-levels.md)). Raising fortifications changes which scene loads, so confirm the matching `scene_name_<n>` is set.

Check: `python tools/dump_settlement_buildings.py --culture <culture>`
Takes effect: new campaign only
Code: No code changes needed

**6. Repoint a scene.** Change `scene_name` on the `<Location>` row. The folder has to exist under some module's `SceneObj/`, case-insensitively. [`tools/remap_stale_scene_names.py`](../../tools/remap_stale_scene_names.py) does verified bulk repoints (`--dry-run` first) and every replacement is confirmed on disk before it writes.

Check: `python tools/audit_scene_names.py`
Takes effect: next save load
Code: No code changes needed

### Delete

**Do not delete a settlement. Retag it or move it.** Nothing in the engine or in TAOM is built to lose one:

1. **Save files reference settlements by id.** Ownership, garrisons, prisoners, quests and party targets all resolve through it ([taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)).
2. **81 settlement ids are hard-coded in C#**, in the per-settlement volunteer pools under `Main/Features/TroopProgression/`. A deleted or renamed id silently drops that settlement's recruitment pool.
3. **Villages die with their parent.** Every `bound="Settlement.<id>"` pointing at a deleted fortification dereferences null on a new campaign.
4. **The last settlement of a culture makes that culture landless**, which is the crash the `LANDLESS_CULTURE` gate exists to stop: vanilla's lord-spawn code calls `Settlement.All.First(culture)` unguarded on the daily clan tick ([lord-spawn-guard.md](../features/lord-spawn-guard.md)).
5. **The scene entity stays behind.** Removing the data leaves the map icon's entity in `scene.xscene` with nothing bound to it.

What to do instead: retag the culture, reassign the owner, or move `posX` and `posY`. If a settlement genuinely must go, the supported shape is the one TAOM uses against vanilla's file, an XSLT template that matches the element and emits nothing, applied to a file you do not own. TAOM has never done this to one of its own settlements, so treat it as unexplored ground and start a new campaign.

Check: `python tools/validate_moduledata.py`
Takes effect: new campaign only
Code: Code changes required in `Main/Features/TroopProgression/` if the id has a volunteer pool

## Gotchas: what fails silently and what crashes

- **The repo copy is a decoy.** `Main/_Module/ModuleData/settlements.xml` is 125 settlements and three months behind the live file, and it is not registered in `Main/_Module/SubModule.xml`. Editing it looks exactly like working (`CLAUDE.md` Traps, [taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)).
- **A wrong entrance coordinate wedges the AI with no crash and no log line.** An entrance can sit on a navmesh island the rest of the map cannot path to. `PathFaceRecord.IsValid()` returns true for such a face, so an off-mesh check finds nothing; only comparing island indices does. Never hand-pick a replacement, run `taom.audit_settlement_entrances` in a loaded campaign and take the coordinate the engine computes ([dev-console.md](../features/dev-console.md) and `Main/Features/DevConsole/Cheats/SettlementEntranceCheats.cs`).
- **A `<Location>` element blanks the template's scene names.** Every slot you do not write becomes an empty string, not the inherited value (`Settlement.cs:1003-1009`).
- **A missing scene folder crashes on entry, far from the XML.** It reads as "battles near this place crash", not as bad data ([scene-reference-audit.md](../reference/scene-reference-audit.md)).
- **Adding a `<CommonAreas>` entry to a settlement an existing save already knows reads past the end of the saved alley list.** On a save load the loop re-initialises `Alleys[num]` in place instead of adding (`Settlement.cs:1024-1031`).
- **A reference typo does not error, it creates a ghost.** The object manager auto-creates a placeholder for an unknown `Settlement.`, `Culture.` or `VillageType.` reference and fills it in later if the real node turns up (`MBObjectManager.cs:713-731`). You find out as a crash or an ownerless fief much later.
- **A reference missing its dotted prefix throws.** `culture="gondor"` raises `MBInvalidReferenceException`; `culture="Culture.gondor"` works. The two exceptions that take a bare id are `<Building id>` and workshop `<Input>` (`MBObjectManager.cs:1503-1508`, `Town.cs:709-711`, `WorkshopType.cs:139`).
- **A `<Building id>` naming a building type that does not exist crashes the load**, because the next line reads `StartLevel` off the null lookup (`Town.cs:709-712`).
- **Nine name keys in the live file are written `{==Settlements...}` with a doubled equals**, and the loc files faithfully mirror the malformed key. They still resolve, because the whole string between the braces is the key, but do not copy the pattern into a new entry. <!-- measured: rg -oF '{==' on the live settlements.xml piped to wc -l 2026-09-05 -->
- **The commit hook never fires on this file.** `.claude/hooks/check-moduledata-validation.sh` runs the validator only when the commit stages a path under `Main/_Module/ModuleData/*.xml` (lines 68 to 74), and this file is not in the repo at all. Run `python tools/validate_moduledata.py` by hand.
- **What the validator covers here is narrow.** `TAOM_Map` contributes exactly two files to a run: `settlements.xslt` (one boolean, does it strip vanilla) and `settlements.xml` (the culture attribute plus town and village economy). Its 1,012 `Culture.` references are never swept, and because those ids feed the validator's own settled-culture set, a typo there masks a `LANDLESS_CULTURE` error instead of raising one ([`tools/README.md`](../../tools/README.md) line 37, [moduledata-validation.md](../features/moduledata-validation.md)).
- **Siege camps are a scene contract, not a data one.** A settlement whose scene has no `siege_camp_1` entity logs a warning and runs on a safety net; the fix is in the map editor ([siege.md](../features/siege.md)).
- **`tools/check_prefab_budget.py` counts only `TAOM_Map/Prefabs`** against a cap the engine applies across every loaded module, so it reports healthy at 99 percent of the real budget (`CLAUDE.md` Traps).

### What TAOM has not answered

- **Which module wins when two ship the same `SceneObj/<name>`.** The last-active-module rule is documented only for the main map, through `MapScene.GetMainMapModule`. Ordinary settlement scenes are unstated. Start at [`docs/reference/worldmap-battle-scene-grid.md`](../reference/worldmap-battle-scene-grid.md) lines 69 to 71 and [`tools/audit_scene_names.py`](../../tools/audit_scene_names.py), which cross-references every module's `SceneObj/`.
- **Why `Main_map` has two `settlements_scripts` entities carrying `SettlementPositionScript`** where vanilla has one, and whether the duplicate double-registers anything. Compare `TAOM_Map/SceneObj/Main_map/scene.xscene` against `SandBox/SceneObj/Main_map/scene.xscene`, with behaviour notes at [editor-cache-rebuild.md](../features/editor-cache-rebuild.md) lines 203 to 204.
- **How to paint campaign-map navmesh for new land.** The only tile values TAOM records are framed around shores, ocean and bridges, in [`docs/warsails-custom-map-guide.md`](../warsails-custom-map-guide.md) lines 25 to 34, and the face-group to terrain mapping in [`docs/reference/worldmap-battle-scene-grid.md`](../reference/worldmap-battle-scene-grid.md) lines 60 to 63. Neither documents land painting, and the baked result is `TAOM_Map/SceneObj/Main_map/navmesh.bin`.
- **What `background_crop_position` is measured in.** It is handed straight to the UI widget. Every shipped value is `0.0`.
- **What `gate_rotation` and `map_icon` were for.** No managed reader exists in either the shipping or the editor build of v1.4.8. Do not claim they rotate or draw anything.

### Appendix: workshops

`spworkshops.xml` is the catalogue of workshop kinds, registered at `Campaign.cs:1551` and pointed at by `SandBox/SubModule.xml` line 143 and `TAOM_Map/SubModule.xml` line 66. TAOM has authored none: `TAOM_Map/ModuleData/spworkshops.xml` is four lines holding an empty `<WorkshopTypes/>`, so towns get vanilla's 12 kinds (`artisans`, `brewery`, `linen_weavery`, `olive_press`, `pottery_shop`, `silversmithy`, `smithy`, `stable`, `tannery`, `velvet_weavery`, `wine_press`, `wool_weavery`). How many shops a town has is not authored here either; the engine gives every town the same count and `spworkshops.xml` only biases which kinds get picked. Two shapes to know before authoring one: `<Input>` takes a **bare** item-category id and is read positionally, so it must be the first attribute on the node, while `<Output>` takes the prefixed `ItemCategory.<id>` form (`WorkshopType.cs:139` and `:168`). <!-- measured: wc -l and rg -o 'id="[a-z_]+"' on both spworkshops.xml copies, and rg -n on the two SubModule.xml files 2026-09-05 -->

## Numbers in this chapter

Every count was produced on 2026-09-05 by the command beside it. The live file is `TAOM_Map/ModuleData/settlements.xml` in the game install.

| Number | Command |
|---|---|
| 988 `<Settlement>` live, 863 in the repo shadow | `rg -oF '<Settlement ' <file>` piped to `wc -l`, on each copy |
| 221 `<Town>`, of which 143 `is_castle="true"` and 78 towns; 607 `<Village>`; 159 `<Hideout>`; 1 `<CustomSettlementComponent>` | `rg -oF` for each element name, and `rg -oF 'is_castle="true"'`, piped to `wc -l` |
| 2,509 `<Building>`, 1,898 `<Location>`, 2,055 `<Area>` | the same `rg -oF` count per element name |
| 988 with `culture=`, 235 with `text=`, 221 with `gate_posX`, 221 with `owner=`, 0 with `port_posX` | a python regex pass over the 988 `<Settlement>` open tags |
| Live file 1,153,217 bytes dated 2026-09-04; shadow 1,023,041 bytes dated 2026-05-26; the deployed copy at `TAOM/ModuleData/settlements.xml` matches the shadow byte for byte | `ls -l` on all three |
| 100 `<XmlName>` rows in `Main/_Module/SubModule.xml`, none of them `Settlements` | `rg -c '<XmlName' Main/_Module/SubModule.xml` and `rg -n 'Settlements'` on it |
| 15 lines in `settlements.xslt`; 1,227 `<string>` rows in each of the 12 `loc_settlements.xml` | `wc -l`, and `rg -oF '<string id='` piped to `wc -l` per language |
| Town prosperity 1,700 to 5,600, median 4,000 (n=78); castle 420 to 1,100, median 810 (n=143); village hearth 100 to 722, median 350 (n=607) | a python regex and `statistics.median` pass over the live file |
| 6 towns at or above the 5,000 crowd band, 67 between 2,000 and 5,000, 5 below; 18 villages at or above hearth 600 | the same pass, bucketed at the thresholds in `Town.cs:738-749` and `Village.cs:320-331` |
| 30 distinct cultures on settlements; 22 own fortifications; 8 appear on hideouts, matching the 8 `is_bandit="true"` cultures in `taom_spcultures.xml` | a python `collections.Counter` over `culture="Culture.X"` per settlement block, and `rg -c 'is_bandit="true"'` on `taom_spcultures.xml` |
| 511 `gate_rotation`, 160 `map_icon`, 159 `type="Hideout"`, 2,055 `<Area type=` in six values | `rg -oF` per attribute, and `rg -o '<Area type="[A-Za-z]+"'` piped through `sort` and `uniq -c` |
| 221 `<Location>` rows set slots 1 to 3 with no slot 0; 78 set no scene name at all | a python regex pass over the 1,898 `<Location>` nodes |
| 9 name keys written `{==` | `rg -oF '{==' <live file>` piped to `wc -l` |
| 81 settlement ids hard-coded in `Main/Features/TroopProgression/` | `rg -o '"(town\|castle\|village\|castle_village\|hideout)_[A-Za-z0-9_]+"' Main/Features/TroopProgression/ -g'*.cs'` piped through `sort -u` and `wc -l` |
| 6 location complex templates with 9, 1, 3, 1, 1 and 1 rooms | a python ElementTree pass over `SandBox/ModuleData/location_complex_templates.xml` |
| 12 vanilla workshop types; TAOM's own `spworkshops.xml` is 4 lines and empty | `rg -o 'id="[a-z_]+"'` piped through `sort -u`, and `wc -l`, on both copies |
| The distance cache is 10,205,146 bytes | `ls -l` on `TAOM_Map/ModuleData/DistanceCaches/` |
| `castle_GBC1`'s scene transform is `250.684, 1200.344, 79.999`, and the tool that added it used `250.684` and `1200.344` | `rg -m2 -n 'name="castle_GBC1"'` on `scene.xscene` with `sed -n` around the hit, against `tools/add_bluecraig_castles.py` line 36 |

## Read next

- [`docs/reference/taom-map-settlement-naming.md`](../reference/taom-map-settlement-naming.md), the region prefix to culture to naming-idiom table, the rename workflow and the entrance-coordinate correction list.
- [`docs/features/settlement-building-levels.md`](../features/settlement-building-levels.md), the three-script building pipeline and the tier vocabulary.
- [`docs/features/fief-granting.md`](../features/fief-granting.md), who holds what at campaign start and which kingdoms have more clans than fiefs.
- [`docs/features/lord-spawn-guard.md`](../features/lord-spawn-guard.md), what a landless culture does to the daily clan tick, and the Khand retag in full.
- [`docs/features/culture-conversion.md`](../features/culture-conversion.md), why a day-one culture and owner mismatch never converts itself.
- [`docs/features/bandit-management.md`](../features/bandit-management.md), hideout cultures, boss troops and the hideout migration.
- [`docs/reference/scene-reference-audit.md`](../reference/scene-reference-audit.md), the scene-name audit tools and the case-insensitivity rule.
- [`docs/scene-entities.md`](../scene-entities.md), the settlement entity ids per region prefix in the map scene.
- [`docs/features/editor-cache-rebuild.md`](../features/editor-cache-rebuild.md), the distance-cache rebuild, its recovery path and its backup trap.
- [`docs/features/siege.md`](../features/siege.md), the siege-camp scene entity contract.
- [`docs/features/dev-console.md`](../features/dev-console.md), `taom.audit_settlement_entrances` and what it prints.
- [`docs/reference/engine/settlement-economy-food-prosperity.md`](../reference/engine/settlement-economy-food-prosperity.md), the engine formulas behind prosperity and food.
- [`docs/warsails-custom-map-guide.md`](../warsails-custom-map-guide.md), the aspirational custom-map notes, naval-shaped and incomplete.
- [Cultures](cultures.md), [Clans](clans.md), [Kingdoms](kingdoms.md) and [Party templates](party-templates.md), the four things a settlement points at or is pointed at by.
- [Module: the map](module-map.md), what else `TAOM_Map` ships.
- [Validation and testing](validation-and-testing.md) and [Balance levers](balance-levers.md).
