# Cultures

## What this file is

A culture is the object every other piece of TAOM content points back at: a troop names its culture, a settlement names its culture, a clan and a kingdom name theirs, and the engine then asks that one object for the recruits, the militia, the caravans, the town shopkeepers, the names it gives generated heroes and the banner it draws. TAOM writes 24 of them in `Main/_Module/ModuleData/taom_spcultures.xml` and rewrites 6 more vanilla ones through `Main/_Module/ModuleData/spcultures.xslt`. <!-- measured: python ElementTree child count on taom_spcultures.xml and rg -n 'xsl:template match="Culture\[@id' on spcultures.xslt 2026-09-05 --> It is the largest single XML element in the mod: 92 attributes and 18 child lists are read off one `<Culture>` element by three deserializers stacked on top of each other. <!-- measured: python regex scan of MBObjectBase.cs, BasicCultureObject.cs and CultureObject.cs Deserialize bodies 2026-09-05 -->

## Where it lives and how it is registered

Two sources feed one object type, and they behave differently. Read [editing-safely](editing-safely.md) first if you have not, because both traps below are merge traps.

| Source | Path | What it does |
|---|---|---|
| TAOM's own cultures | [`Main/_Module/ModuleData/taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml) | Declares 24 new `<Culture>` elements. Nothing is inherited: an attribute you leave out is null. |
| TAOM's rewrites of the vanilla six | [`Main/_Module/ModuleData/spcultures.xslt`](../../Main/_Module/ModuleData/spcultures.xslt) | Transforms the vanilla document in place. Everything is inherited: an attribute the block never names keeps its Calradian value. |

The vanilla document the stylesheet transforms is `SandBoxCore/ModuleData/spcultures.xml`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. TAOM never edits it. There is no `spcultures.xml` in TAOM at all, only the stylesheet.

Both are registered under the same `XmlName` id in [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml):

```xml
<XmlName id="SPCultures" path="spcultures"/>
<XmlName id="SPCultures" path="taom_spcultures"/>
```

Line 78 is the stylesheet row (TAOM ships no `spcultures.xml`, so `path="spcultures"` resolves to `spcultures.xslt` alone) and line 119 is the data row. How the loader turns a `path` into a file, and why the two rows are not interchangeable, is in [submodule-and-registration](submodule-and-registration.md).

- **Root element:** `<SPCultures>`.
- **Per-entry element:** `<Culture>`.
- **Engine class:** `TaleWorlds.CampaignSystem.CultureObject`, which extends `TaleWorlds.Core.BasicCultureObject`, which extends `TaleWorlds.ObjectSystem.MBObjectBase`. The type is registered as element `Culture` under XmlName `SPCultures` by `Campaign.cs:1542`, and loaded by `Campaign.cs:1462`.
- **When it loads:** `Campaign.cs:1410` calls `InitializeBasicObjectXmls()`, which calls `LoadXML("SPCultures")`, and that call sits outside the `if (_gameLoadingType != GameLoadingType.SavedCampaign)` guard at `Campaign.cs:1396-1411`. Cultures are therefore rebuilt from XML when a save loads, not only on a new campaign. Anything already written into the save (a hero's drawn name, a notable already spawned, a party already on the map) keeps what it got.
- **Load order:** items, equipment rosters and party templates are loaded first, at `Campaign.cs:1471-1473`, and cultures after them at `:1462`. That ordering is what makes the silent item drop in "Gotchas" possible.

## Attributes

Three classes read attributes off one `<Culture>` element, in this order: `MBObjectBase.Deserialize`, then `BasicCultureObject.Deserialize`, then `CultureObject.Deserialize`. They are three tables because they live in three files. Only the first two rows are hard-required at parse time; everything else has a default, and an absent object reference is simply null (`MBObjectManager.cs:1499-1502`).

**Every object reference must be written in the dotted `Type.id` form.** `ReadObjectReferenceFromXml` splits the value on `.` and throws `MBInvalidReferenceException` when there is no dot (`MBObjectManager.cs:1505-1508`). So `basic_troop="NPCCharacter.erebor_reg_miner"`, never `basic_troop="erebor_reg_miner"`. The prefixes are in [id-cheatsheet](id-cheatsheet.md).

<!-- engine-table type="TaleWorlds.ObjectSystem.MBObjectBase" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none; the read is `node.Attributes["id"].Value` with no null check, so a missing id throws and takes the rest of the file with it | The permanent codename, for example `erebor`. Troops, settlements, clans, kingdoms, party templates and the character-creation JSON all point at this string. Renaming one orphans every reference at once. | `MBObjectBase.cs:61` |

<!-- engine-table type="TaleWorlds.Core.BasicCultureObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCultureObject.cs" method="Deserialize" inert="cloth_alternative_color1,cloth_alternative_color2,banner_background_color1,banner_foreground_color1,banner_background_color2,banner_foreground_color2" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `name` | string, localisable | yes in practice | none; the read is unguarded like `id` and crashes the load the same way | The display name, written `{=aom_erebor_name}Dwarves`. The `{=key}` part is the translation key and the text after it is the English fallback. See [strings-and-localization](strings-and-localization.md). | `BasicCultureObject.cs:46` |
| `color`, `color2` | hex string parsed base 16 into uint | no | `uint.MaxValue`, which is white | Primary and secondary faction colours, written `color="0xFF23432D"`. Party icons, map banners and troop tint use them. | `BasicCultureObject.cs:47-48` |
| `is_main_culture` | bool | no | false | Read but does not make a culture playable. It gates the encyclopedia filters and the average-wage pass only. What makes a culture pickable is `AddCharacterCreationCulture`, covered under "Gotchas". | `BasicCultureObject.cs:55` |
| `can_have_settlement` | bool | no | false | Whether factions of this culture may own towns, castles and villages, and whether their parties path to settlements at all. Every TAOM culture sets it true. | `BasicCultureObject.cs:61` |
| `is_bandit` | bool | no | false | Marks an outlaw culture. Drives hideout spawning and troop-upgrade legality. 8 of TAOM's 24 set it. | `BasicCultureObject.cs:59` |
| `encounter_background_mesh` | string, a mesh name | no | null | The 2D art behind the encounter and army menus for this faction. A mesh name, not a texture path. TAOM reuses vanilla names such as `encounter_sturgia`. | `BasicCultureObject.cs:56` |
| `faction_banner_key` | string, the packed banner code | no | an empty `Banner`, not null, so omitting it gives a blank banner rather than a crash | The default heraldry. The number-group grammar is not decoded anywhere in TAOM; copy a working key and edit it against [`docs/reference/banner-icon-generation.md`](../reference/banner-icon-generation.md) and [banners-and-heraldry](banners-and-heraldry.md). | `BasicCultureObject.cs:57` |
| `cloth_alternative_color1`, `cloth_alternative_color2` | hex string | no | white | Read but has no effect: no consumer for `ClothAlternativeColor` exists in the v1.4.8 decompile, and no TAOM culture sets either. | `BasicCultureObject.cs:49-50` |
| `banner_background_color1`, `banner_foreground_color1`, `banner_background_color2`, `banner_foreground_color2` | hex string | no | white | Read but has no effect on a singleplayer campaign as far as the decompile shows. No TAOM culture sets any of the four. | `BasicCultureObject.cs:51-54` |

<!-- engine-table type="TaleWorlds.CampaignSystem.CultureObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CultureObject.cs" method="Deserialize" inert="militia_bonus,prosperity_bonus,naval_factor,fishing_party_template,settlement_patrol_template_coastal,shipwright,shipyard_worker,militia_veteran_archer,gear_dummy,bandit_raider,text" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `basic_troop`, `elite_basic_troop` | ref `NPCCharacter.<id>` | not by the parser, but nothing works without them | null | The roots of the common and the noble troop trees. This is the single most load-bearing pair on a culture: recruitment, lord retinues and the displayed tree all start here. See [troops](troops.md). | `CultureObject.cs:286`, `:281` |
| `melee_militia_troop`, `ranged_militia_troop`, `melee_elite_militia_troop`, `ranged_elite_militia_troop` | ref `NPCCharacter.<id>` | no | null | The four militia bodies a settlement fields. The elite pair is used when the town is rich or loyal. | `CultureObject.cs:282-285` |
| `default_party_template` | ref `PartyTemplate.<id>` | no | null | The troop mix a lord of this culture starts a warband with, and the fallback whenever a clan has no template of its own. | `CultureObject.cs:270` |
| `villager_party_template` | ref `PartyTemplate.<id>` | no | null | The peasant caravans that run village to town. | `CultureObject.cs:271` |
| `militia_party_template` | ref `PartyTemplate.<id>` | no | null | The mix used when a settlement's militia is spawned as a field party. | `CultureObject.cs:273` |
| `rebels_party_template` | ref `PartyTemplate.<id>` | no | null | The army a rebel clan fields after a settlement rebellion. | `CultureObject.cs:274` |
| `vassal_reward_party_template` | ref `PartyTemplate.<id>` | no | null | The troops a king hands a new vassal. Iterated stack by stack with no null guard, so a kingdom-owning culture that leaves this out crashes when the player joins a kingdom. | `CultureObject.cs:276` |
| `settlement_patrol_template_level_1`, `settlement_patrol_template_level_2`, `settlement_patrol_template_level_3` | ref `PartyTemplate.<id>` | no | null | Town and castle patrols, chosen by Guard House level. Level 1 is also the fallback for an unrecognised tier. | `CultureObject.cs:277-279` |
| `bandit_boss_party_template` | ref `PartyTemplate.<id>` | no | null | The party guarding a hideout. Bandit cultures only. | `CultureObject.cs:275` |
| `fishing_party_template`, `settlement_patrol_template_coastal` | ref `PartyTemplate.<id>` | no | null | Read but has no effect: no consumer for either property exists in the v1.4.8 decompile. No TAOM culture sets them. | `CultureObject.cs:272`, `:280` |
| `default_battle_equipment_roster`, `default_civilian_equipment_roster` | ref `EquipmentRoster.<id>` | no | null | Fallback battle and town kits for a character of this culture that has none of its own. See [equipment-rosters](equipment-rosters.md). | `CultureObject.cs:287-288` |
| `default_stealth_equipment_roster`, `duel_preset_equipment_roster`, `marriage_bride_equipment_roster` | ref `EquipmentRoster.<id>` | no | null | Sneak-into-town gear, the arena duel loadout, and the wedding dress. All three are null-checked before use, so they are safe to omit. TAOM points all three at vanilla rosters on most cultures. | `CultureObject.cs:289-291` |
| `bandit_bandit`, `bandit_chief`, `bandit_boss` | ref `NPCCharacter.<id>` | no | null | The rank and file of a hideout, the chief you duel at the end of a raid, and the named boss that must be present in a boss party. Bandit cultures only. | `CultureObject.cs:335`, `:337-338` |
| `bandit_raider` | ref `NPCCharacter.<id>` | no | null | Read but has no effect: `BanditRaider` has no consumer in the decompile. All 8 TAOM bandit cultures set it anyway. | `CultureObject.cs:336` |
| `tournament_master`, `villager`, `caravan_master`, `caravan_guard` | ref `NPCCharacter.<id>` | no | null | The arena master, the generic peasant used to bulk out crowds, the caravan leader, and the hireable caravan guard. `caravan_guard` also supplies 30 percent of the tavern mercenary offer, so leaving it null is visible in town. | `CultureObject.cs:292-295` |
| `prison_guard`, `guard`, `blacksmith`, `weaponsmith`, `armorer`, `horseMerchant`, `barber`, `merchant`, `shop_worker` | ref `NPCCharacter.<id>` | no | null | The shopkeeper and guard NPCs placed in a town scene. `horseMerchant` is camelCase in the XML and nowhere else: spell it `horse_merchant` and the stable is empty with no error. | `CultureObject.cs:296-299`, `:319`, `:324-327` |
| `tavernkeeper`, `taverngamehost`, `musician`, `tavern_wench`, `ransom_broker`, `gangleader_bodyguard` | ref `NPCCharacter.<id>` | no | null | The tavern cast plus the thug beside a gang leader. `taverngamehost` has no underscores. | `CultureObject.cs:313-314`, `:320-323` |
| `beggar`, `female_beggar`, `female_dancer` | ref `NPCCharacter.<id>` | no | null | Street and tavern dressing. | `CultureObject.cs:328-330` |
| `townsman`, `townsman_infant`, `townsman_child`, `townsman_teenager`, `townswoman`, `townswoman_infant`, `townswoman_child`, `townswoman_teenager` | ref `NPCCharacter.<id>` | no | null | Town crowd by sex and age band. The two adults also form the crowd in the coronation scene. | `CultureObject.cs:300-307` |
| `village_woman`, `villager_male_child`, `villager_male_teenager`, `villager_female_child`, `villager_female_teenager` | ref `NPCCharacter.<id>` | no | null | Village crowd. Note the asymmetry with the town set: there is no `village_man` attribute, the adult male villager is `villager`. | `CultureObject.cs:308-312` |
| `merchant_notary`, `artisan_notary`, `preacher_notary`, `rural_notable_notary` | ref `NPCCharacter.<id>` | no | null | The clerk who stands beside a notable and takes quests for him. TAOM points each at the first notable slot of the matching occupation. See [npcs-notables-and-townsfolk](npcs-notables-and-townsfolk.md). | `CultureObject.cs:315-318` |
| `shipwright`, `shipyard_worker`, `militia_veteran_archer`, `gear_dummy` | ref `NPCCharacter.<id>` | no | null | Read but has no effect: none of the four properties has a consumer in the decompile. No TAOM culture sets any of them. | `CultureObject.cs:331-334` |
| `default_character_creation_body_property` | ref `BodyProperty.<id>` | no | null, and explicitly null-guarded | The face and body preset a new player of this culture starts from. See [body-properties](body-properties.md). | `CultureObject.cs:339` |
| `start_point_position_x`, `start_point_position_y` | float | no | 0 | Where on the world map a new player of this culture is placed. | `CultureObject.cs:341-342` |
| `board_game_type` | enum: `Seega`, `Puluc`, `Konane`, `MuTorere`, `Tablut`, `BaghChal` | no | the field default, which is `Seega` | Which tavern board game the game host plays. The assignment sits inside an `if` around a `TryParse`, so a misspelt value is not an error, it silently leaves Seega. | `CultureObject.cs:344-347` |
| `militia_bonus`, `prosperity_bonus`, `naval_factor` | int, int, float | no | 0 | Read but has no effect: `MilitiaBonus`, `ProsperityBonus` and `NavalFactor` are parsed into public properties that nothing in the decompile reads. Erebor sets `prosperity_bonus="1"` and it does nothing. | `CultureObject.cs:267-269` |
| `text` | string, localisable | no | an empty `TextObject`, not null | Intended as the encyclopedia blurb, and read but has no effect: `EncyclopediaText` has no reader in the v1.4.8 decompile. TAOM writes a full paragraph on most cultures anyway. | `CultureObject.cs:340` |

## Child elements

Every one of the 18 lists is optional, and an absent list is an empty list rather than a null. Each is a container holding repeated entries, and the entry attribute is **not** the same name in every list: three of them take a bare id with no dotted prefix, two of them key on `@name`, and the rest on a dotted `@id`. Get that wrong and the entry is skipped or the load throws.

<!-- engine-table type="TaleWorlds.CampaignSystem.CultureObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CultureObject.cs" method="Deserialize" -->

| Element | Entry shape | What it does | Read at (file:line) |
|---|---|---|---|
| `<default_policies>` | `<policy id="policy_senate"/>`, **bare id** | The kingdom policies a faction of this culture starts with. An unknown id resolves to null and the null is added to the list with no check. | `CultureObject.cs:369-375` |
| `<male_names>`, `<female_names>` | `<name name="{=key}Berling"/>`, keyed on `@name` | The pools the name generator draws from for generated heroes of this culture. All 24 TAOM cultures ship both. | `CultureObject.cs:377-390` |
| `<clan_names>` | `<name name="..."/>`, keyed on `@name` | The pool for procedurally generated clan names. | `CultureObject.cs:391-397` |
| `<cultural_feats>` | `<feat id="taom_erebor_loyalty"/>`, **bare id**, element must be spelled `feat` | The culture bonuses. The element name is load-bearing: the entry is built by `CreateObjectFromXmlNode`, which dispatches on the element name. A feat id nothing registers in C# yields an empty object with no effect. | `CultureObject.cs:398-409` |
| `<possible_clan_banner_icon_ids>` | `<icon id="100"/>`, **bare integer** | The sigils the game picks from when it auto-generates a banner for a new companion clan or a rebel clan. The parse is `int.TryParse` with the result discarded, so a non-numeric id silently becomes icon 0. | `CultureObject.cs:411-418` |
| `<notable_templates>` | `<template name="NPCCharacter.<id>"/>`, keyed on `@name` | The characters cloned to create a settlement's notables. A culture with no template for an occupation simply lacks that notable type in its towns. | `CultureObject.cs:419-426` |
| `<lord_templates>` | `<template name="NPCCharacter.<id>"/>` | Cloned to create new lord heroes, for example when a companion is promoted. | `CultureObject.cs:427-434` |
| `<rebellion_hero_templates>` | `<template name="NPCCharacter.<id>"/>` | Cloned to create the leader of a settlement rebellion. | `CultureObject.cs:435-442` |
| `<tournament_team_templates_one_participant>`, `<tournament_team_templates_two_participant>`, `<tournament_team_templates_four_participant>` | `<template name="NPCCharacter.<id>"/>` | The fighter loadouts used to fill tournament teams. The one-participant list is also the default branch, so it covers every team size other than 2 and 4. | `CultureObject.cs:443-461` |
| `<basic_mercenary_troops>` | `<template name="NPCCharacter.<id>"/>` | The tavern hire pool. Empty means the culture's towns offer no mercenary. See [`docs/features/tavern-mercenaries.md`](../features/tavern-mercenaries.md) for why these must be leaf `_merc` copies. | `CultureObject.cs:471-477` |
| `<vassal_reward_items>` | `<item id="Item.<id>"/>`, dotted `@id` | The gear a king hands a new vassal. Any entry whose item failed to load is dropped from the list with no log. | `CultureObject.cs:464-470`, `:522` |
| `<banner_bearer_replacement_weapons>` | `<item id="Item.<id>"/>` | The one-handers a standard bearer's two-hander is swapped for, so he can hold the banner. Same silent drop as above. An empty list leaves the bearer holding a banner and nothing else. | `CultureObject.cs:478-484`, `:525` |
| `<caravan_party_templates>`, `<elite_caravan_party_templates>` | `<caravan_party_template id="PartyTemplate.<id>"/>`, dotted `@id`, and the inner element name is the same in both lists | The normal and the veteran caravan compositions. There is no `caravan_party_template` **attribute**: caravans come only from these children. | `CultureObject.cs:485-498` |
| `<available_ship_hulls>` | `<ship_hull id="ShipHull.<id>"/>` | Which hulls the culture can field. This is the last branch of the if chain, so a child element named anything the engine does not recognise falls off the end and is dropped with no warning. | `CultureObject.cs:501-508` |
| `id`, `name` | the attribute read off each entry inside a list | Which of the two a list uses is fixed per list and is in the "Entry shape" column above. The dotted forms go through the same `Type.id` split as the attributes, so a bare value throws. | `CultureObject.cs:373`, `:381`, `:423` |

**All 18 lists union across modules rather than replacing.** Every container carries `AlwaysPreferMerge` in `SPCultures.xsd`, so `MergeElements` recurses into the existing container and appends entries it has not already seen, keyed by that list's unique attribute (`MBObjectManager.cs:851-872`). To replace a list instead of adding to it, put `_replaceWhileMerging="true"` on the container: `MergeElementAttributes` returns true for that flag and `MergeElements` then wipes the existing children first (`MBObjectManager.cs:804-808`, `:828-832`). The flag is injected into every complex type at runtime (`MBObjectManager.cs:1092`), so it is legal anywhere without touching the schema.

**Attributes replace, lists union.** That single sentence is the whole shape of the caravan bug this chapter keeps coming back to.

## Worked example

Erebor is the fullest culture in the file: it sets every attribute TAOM uses and carries one of each of the 18 child lists. The opening tag, verbatim.

<!-- example file="Main/_Module/ModuleData/taom_spcultures.xml" id="erebor" -->

```xml
  <Culture
    id="erebor"
    name="{=aom_erebor_name}Dwarves"
    is_main_culture="true"
    color="0xFF23432D"
    color2="0xFFB5913E"
    elite_basic_troop="NPCCharacter.erebor_noble"
    basic_troop="NPCCharacter.erebor_reg_miner"
    melee_militia_troop="NPCCharacter.erebor_militia_spearman"
    ranged_militia_troop="NPCCharacter.erebor_militia_archer"
    melee_elite_militia_troop="NPCCharacter.erebor_militia_veteran_spearman"
    ranged_elite_militia_troop="NPCCharacter.erebor_militia_veteran_archer"
    can_have_settlement="true"
    villager_party_template="PartyTemplate.villager_erebor_template"
    default_party_template="PartyTemplate.kingdom_hero_party_erebor_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_erebor_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_erebor_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_erebor_template_level_3"
    militia_party_template="PartyTemplate.militia_erebor_template"
    rebels_party_template="PartyTemplate.rebels_erebor_template"
    vassal_reward_party_template="PartyTemplate.vassal_reward_troops_erebor"
    prosperity_bonus="1"
    encounter_background_mesh="encounter_sturgia"
    faction_banner_key="11.12.12.4345.4345.764.764.1.0.0.462.13.13.512.512.764.764.1.0.0"
    text="{=aom_erebor_desc}The Longbeards of Erebor are a proud and industrious race, dwelling in the halls of the Lonely Mountain. Masters of craftsmanship and mining, they are renowned for their skill in forging weapons, armor, and treasures of unrivaled beauty. Led by their King under the Mountain, the dwarves of Erebor are a resilient people, fiercely protective of their homeland and its riches."
    tournament_master="NPCCharacter.tournament_master_erebor"
    villager="NPCCharacter.villager_erebor"
    caravan_master="NPCCharacter.caravan_master_erebor"
    caravan_guard="NPCCharacter.caravan_guard_erebor"
    veteran_caravan_guard="NPCCharacter.veteran_caravan_guard_erebor"
    prison_guard="NPCCharacter.prison_guard_erebor"
    guard="NPCCharacter.guard_erebor"
    blacksmith="NPCCharacter.blacksmith_erebor"
    weaponsmith="NPCCharacter.weaponsmith_erebor"
    townswoman="NPCCharacter.townswoman_erebor"
    townswoman_infant="NPCCharacter.townswoman_infant_erebor"
    townswoman_child="NPCCharacter.townswoman_child_erebor"
    townswoman_teenager="NPCCharacter.townswoman_teenager_erebor"
    townsman="NPCCharacter.townsman_erebor"
    townsman_infant="NPCCharacter.townsman_infant_erebor"
    townsman_child="NPCCharacter.townsman_child_erebor"
    village_woman="NPCCharacter.village_woman_erebor"
    villager_male_child="NPCCharacter.villager_child_erebor"
    villager_male_teenager="NPCCharacter.villager_teenager_erebor"
    villager_female_child="NPCCharacter.village_woman_child_erebor"
    villager_female_teenager="NPCCharacter.village_woman_teenager_erebor"
    townsman_teenager="NPCCharacter.townsman_teenager_erebor"
    ransom_broker="NPCCharacter.ransom_broker_erebor"
    gangleader_bodyguard="NPCCharacter.gangleader_bodyguard_erebor"
    merchant_notary="NPCCharacter.spc_notable_erebor_0"
    artisan_notary="NPCCharacter.spc_notable_erebor_8"
    preacher_notary="NPCCharacter.spc_notable_erebor_5"
    rural_notable_notary="NPCCharacter.spc_notable_erebor_21"
    shop_worker="NPCCharacter.shop_worker_erebor"
    tavernkeeper="NPCCharacter.tavernkeeper_erebor"
    taverngamehost="NPCCharacter.taverngamehost_erebor"
    musician="NPCCharacter.musician_erebor"
    tavern_wench="NPCCharacter.tavern_wench_erebor"
    armorer="NPCCharacter.armorer_erebor"
    horseMerchant="NPCCharacter.horseMerchant_erebor"
    barber="NPCCharacter.barber_erebor"
    merchant="NPCCharacter.merchant_erebor"
    beggar="NPCCharacter.beggar_erebor"
    female_beggar="NPCCharacter.female_beggar_erebor"
    female_dancer="NPCCharacter.female_dancer_erebor"
    default_battle_equipment_roster="EquipmentRoster.erebor_bat_template_medium_a"
    default_civilian_equipment_roster="EquipmentRoster.erebor_civ_template_default_a"
    default_stealth_equipment_roster="EquipmentRoster.default_stealth_equipment_roster"
    duel_preset_equipment_roster="EquipmentRoster.stu_duel_preset_template"
    marriage_bride_equipment_roster="EquipmentRoster.marriage_female_emp_cutscene_template"
    board_game_type="Konane"
    default_character_creation_body_property="BodyProperty.fighter_erebor"
    start_point_position_x="949.3796"
    start_point_position_y="1191.117">
```

Then the first six of its child lists, verbatim.

<!-- excerpt file="Main/_Module/ModuleData/taom_spcultures.xml" -->

```xml
    <caravan_party_templates>
      <caravan_party_template id="PartyTemplate.caravan_template_erebor" />
    </caravan_party_templates>
    <elite_caravan_party_templates>
      <caravan_party_template id="PartyTemplate.elite_caravan_template_erebor" />
    </elite_caravan_party_templates>
    <available_ship_hulls></available_ship_hulls>
    <vassal_reward_items>
      <item id="Item.sm_dwarf_erebor_1h_axe_a" />
    </vassal_reward_items>
    <banner_bearer_replacement_weapons>
      <item id="Item.sm_dwarf_erebor_1h_axe_a" />
      <item id="Item.sm_dwarf_erebor_1h_axe_b" />
      <item id="Item.sm_dwarf_erebor_1h_axe_c" />
      <item id="Item.sm_dwarf_erebor_1h_axe_d" />
    </banner_bearer_replacement_weapons>
    <default_policies>
      <policy id="policy_senate" />
    </default_policies>
```

The three attributes a reader changes first:

1. **`basic_troop` and `elite_basic_troop`.** These decide who the culture actually fields. Point them at a troop id that exists in [`Main/_Module/ModuleData/troops/`](../../Main/_Module/ModuleData/troops/) and keep the dotted `NPCCharacter.` prefix. A culture with no troop file of its own borrows another culture's roots and that is a normal, supported thing to do: `lothlorien` uses Rivendell's `imladris_recruit` and `imladris_infantry`, `shaghana` and `abanissa` use Harad's `harad_levy` and `harad_noble`, `bluecraig` and `mistymountainorcs` use `goblin_snaga` and `goblin_fighter`. <!-- measured: python regex extraction of basic_troop and elite_basic_troop from every Culture block in taom_spcultures.xml 2026-09-05 -->
2. **The eight party-template bindings** (`default_party_template`, `villager_party_template`, `militia_party_template`, `rebels_party_template`, `vassal_reward_party_template` and the three `settlement_patrol_template_level_*`). These decide which stacks the engine spawns for the culture. Read [party-templates](party-templates.md) before retuning what is inside a template, because the stack `max_value` is a spawn ceiling and not a party size.
3. **`faction_banner_key`.** The long numeric code is the culture's default heraldry. Nothing in TAOM decodes the number-group grammar, so copy a working key from a culture whose banner you like and change it with the in-game banner editor rather than by hand. Placeholder keys ship as a visible in-game defect, documented under "Placeholder Banner Keys" in [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md).

### The same culture written as an XSLT block

Dale is not a `<Culture>` element anywhere. It is vanilla's `sturgia`, renamed and repointed by `spcultures.xslt`. The block opens by copying the whole vanilla element:

<!-- excerpt file="Main/_Module/ModuleData/spcultures.xslt" -->

```xml
	<xsl:template match="Culture[@id='sturgia']">
		<xsl:copy>
			<!-- Copy all vanilla attributes first, then override the ones we change -->
			<xsl:apply-templates select="@*"/>
```

and then names, one at a time, the attributes it wants to change:

<!-- excerpt file="Main/_Module/ModuleData/spcultures.xslt" -->

```xml
			<xsl:attribute name="basic_troop">NPCCharacter.dale_recruit</xsl:attribute>
			<xsl:attribute name="elite_basic_troop">NPCCharacter.dale_squire</xsl:attribute>
			<xsl:attribute name="melee_militia_troop">NPCCharacter.dale_militia_spearman</xsl:attribute>
			<xsl:attribute name="ranged_militia_troop">NPCCharacter.dale_militia_archer</xsl:attribute>
			<xsl:attribute name="melee_elite_militia_troop">NPCCharacter.dale_militia_veteran_spearman</xsl:attribute>
			<xsl:attribute name="ranged_elite_militia_troop">NPCCharacter.dale_militia_veteran_archer</xsl:attribute>
			<xsl:attribute name="default_party_template">PartyTemplate.kingdom_hero_party_dale_template</xsl:attribute>
			<xsl:attribute name="villager_party_template">PartyTemplate.villager_dale_template</xsl:attribute>
			<xsl:attribute name="militia_party_template">PartyTemplate.militia_dale_template</xsl:attribute>
			<xsl:attribute name="rebels_party_template">PartyTemplate.rebels_dale_template</xsl:attribute>
			<xsl:attribute name="vassal_reward_party_template">PartyTemplate.vassal_reward_troops_dale</xsl:attribute>
			<xsl:attribute name="settlement_patrol_template_level_1">PartyTemplate.patrol_party_dale_template_level_1</xsl:attribute>
			<xsl:attribute name="settlement_patrol_template_level_2">PartyTemplate.patrol_party_dale_template_level_2</xsl:attribute>
			<xsl:attribute name="settlement_patrol_template_level_3">PartyTemplate.patrol_party_dale_template_level_3</xsl:attribute>
			<xsl:attribute name="default_battle_equipment_roster">EquipmentRoster.dale_bat_template_medium_a</xsl:attribute>
			<xsl:attribute name="default_civilian_equipment_roster">EquipmentRoster.dale_civ_template_default_a</xsl:attribute>
```

and finishes by emitting its child lists and passing through the vanilla children it did not name:

<!-- excerpt file="Main/_Module/ModuleData/spcultures.xslt" -->

```xml
			<!-- Caravans. See the Dunland block above for why these are children, not attributes. -->
			<caravan_party_templates>
				<caravan_party_template id="PartyTemplate.caravan_template_dale" />
			</caravan_party_templates>
			<elite_caravan_party_templates>
				<caravan_party_template id="PartyTemplate.elite_caravan_template_dale" />
			</elite_caravan_party_templates>

			<!-- Pass through vanilla child elements we don't override -->
			<xsl:apply-templates select="*[not(self::caravan_party_templates or self::elite_caravan_party_templates or self::notable_templates or self::vassal_reward_items)]"/>
		</xsl:copy>
	</xsl:template>
```

Line 1194 is why this file is dangerous. `<xsl:apply-templates select="@*"/>` copies every vanilla attribute in, and then each `<xsl:attribute>` overwrites one. **An attribute the block never names is not unchanged, it is inherited, and what it inherits is Calradia.** Nothing in the file looks wrong, because the wrong value is not in the file. That is how Dale, Rohan, Khand and nine cultures' town patrols shipped fielding vanilla troops. The full write-up is [`.claude/rules/xslt.md`](../../.claude/rules/xslt.md) lines 19 to 31.

Line 1310 is the second half of the same trap. The filter names four elements to suppress, so vanilla's `caravan_party_templates` is dropped and only Dale's survives. Emit the child and forget the filter and both survive, because lists union, and the culture then rolls Calradian caravans about half the time. The six blocks in the stylesheet exclude 10, 9, 10, 9, 4 and 4 names respectively, so they are not interchangeable and each has to be edited where it stands. <!-- measured: rg -n 'not\(self::' Main/_Module/ModuleData/spcultures.xslt piped through a python count of self:: names per line 2026-09-05 -->

## Recipes: Add / Modify / Delete

### Add

Adding a culture is a whole-kingdom job, not a file edit. The ordered sequence, the id naming patterns and the 13 files it touches are in [recipe-add-a-culture](recipe-add-a-culture.md) and [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md). What belongs here is the culture element itself and the floor below which it will not run.

1. **Write the `<Culture>` element** into [`Main/_Module/ModuleData/taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml), copying the nearest existing culture block whole and editing it. Do not start from an empty element: a new culture in this file inherits nothing, so every attribute you fail to carry over is a null.
2. **Set `id` and `name` first**, then `can_have_settlement="true"` and `is_main_culture="true"` for a settled playable culture, or `is_bandit="true"` for an outlaw one.
3. **Bind the six troop attributes and the eight party-template attributes.** Reuse another TAOM culture's ids if yours has no roster yet. Never leave one pointing at a vanilla Calradian id, and never leave one out: two of them crash rather than field the wrong troops, covered under "Gotchas".
4. **Bind both caravan child lists.** They are children, not attributes.
5. **Point the four `*_notary` attributes and the whole townsfolk run** at NPCs that exist. Either author `characters/npcs_<culture>.xml` per [npcs-notables-and-townsfolk](npcs-notables-and-townsfolk.md), or reuse another culture's ids the way `umbar` reuses vanilla `guard_aserai`.
6. **Give it at least one `<banner_bearer_replacement_weapons>` entry.** All 24 shipped cultures declare it, because an empty list leaves a standard bearer holding a banner and nothing else. <!-- measured: python ElementTree per-culture child-element histogram over taom_spcultures.xml 2026-09-05 -->
7. **Register it for character creation** by adding a row to `Main/_Module/ModuleData/charactercreation/cultures.json`. `is_main_culture` does not do this.
8. **Give it a settlement.** A culture that owns no settlement makes vanilla's unguarded `Settlement.All.First(culture)` throw on the daily clan tick as soon as a lord of that culture exists. The validator code is `LANDLESS_CULTURE`.
9. **Add its cultural feat ids to C#** if you want feats. A `<feat id="...">` that no code registers produces an empty `FeatObject` with no effect.

Check: `python tools/validate_moduledata.py` then `python tools/check_external_xslt.py`
Takes effect: full game restart, and a new campaign before the world is built around it
Code: Code changes required in `Main/Features/CulturalFeats/TaomCulturalFeats.cs` for new feat ids and in `Main/Features/TroopProgression/RecruitmentPools/` for the volunteer pool

### Modify

#### Repointing a binding on a TAOM culture

1. Open [`Main/_Module/ModuleData/taom_spcultures.xml`](../../Main/_Module/ModuleData/taom_spcultures.xml) and find the `<Culture` line whose next line is `id="<yours>"`. The id sits on its own line, so grep for the id, not for the opening tag.
2. Change the attribute value in place. Keep the dotted prefix.
3. If the target is a party template, confirm it exists in [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml) first. A dangling `PartyTemplate.` reference is a warning in the validator, not an error, and it becomes a null at runtime.
4. If the target is a troop or an NPC, confirm it exists in the culture's file under `Main/_Module/ModuleData/troops/` or `Main/_Module/ModuleData/characters/`.

Check: `python tools/validate_moduledata.py`
Takes effect: full game restart
Code: No code changes needed

#### Repointing a binding on one of the six XSLT-wrapped cultures

The six are `empire` (Dunlendings), `aserai` (Haradrim), `vlandia` (Rohirrim), `khuzait` (Easterlings), `sturgia` (Barding) and `battania` (Variag). <!-- measured: rg -n 'xsl:template match="Culture\[@id|xsl:attribute name="name"' Main/_Module/ModuleData/spcultures.xslt 2026-09-05 -->

1. **Enumerate before you edit.** Open `CultureObject.Deserialize` and classify every attribute it reads as BIND (your culture must supply it), PASSTHROUGH (vanilla's value is correct for you) or N/A (the attribute has no effect). The attribute tables above are that enumeration. Doing this by eye over the block you are editing is what failed four times.
2. **Add or change the `<xsl:attribute name="...">` line** inside the right `Culture[@id='...']` template.
3. **For a child list, make two edits, not one.** Emit your `<caravan_party_templates>` (or whichever list) inside the block, **and** add that element name to the `not(self::...)` filter on the block's closing `<xsl:apply-templates select="*[not(...)]"/>` line. Only the first edit and both versions survive, because lists union.
4. **To add to a vanilla list rather than replace it**, write a nested template instead: `<xsl:template match="Culture[@id='sturgia']/cultural_feats">` copies the vanilla children through `<xsl:apply-templates select="@*|node()"/>` and appends yours. Four of the six blocks take feats this way; `empire` and `vlandia` instead emit a full `<cultural_feats>` inline and exclude it in the filter, which replaces vanilla's. <!-- measured: rg -n '<cultural_feats>' and rg -n 'Culture\[@id=.*\]/cultural_feats' on Main/_Module/ModuleData/spcultures.xslt 2026-09-05 -->
5. **Prove it mechanically.** Transform the stylesheet over the installed `SandBoxCore/ModuleData/spcultures.xml` and flag every emitted attribute whose value still carries a vanilla culture id. `CulturePartyTemplateTests` does exactly this against a sentinel document, so extend its attribute list when you bind something new.

Check: `python tools/check_external_xslt.py` then `dotnet test TAOM.Tests --filter CulturePartyTemplate` and `dotnet test TAOM.Tests --filter CultureLordTemplate`
Takes effect: full game restart
Code: No code changes needed, unless the test's attribute list has to grow, which is `TAOM.Tests/Core/CulturePartyTemplateTests.cs`

### Delete

**Do not delete a culture.** Every troop, settlement, clan, kingdom, hero, party template and equipment roster that names it keeps naming it, and the references do not fail loudly: a settlement whose culture id no longer resolves is a `UNKNOWN_CULTURE` error in the validator and a null at runtime, and a lord whose culture owns no settlement is a daily-tick crash inside vanilla code with no TAOM frame on the stack.

Retire it instead.

1. **Retag its content first, then the culture, never the reverse.** Repoint every settlement, clan, lord and troop that names the culture at the culture that inherits its role. Run the validator between the two steps.
2. **Leave the `<Culture>` element in place** so old saves still resolve it.
3. **Take it out of `charactercreation/cultures.json`** so a player can no longer pick it. That is what makes it retired from the player's side.
4. **Confirm no lord is left behind.** `LANDLESS_CULTURE` fires on exactly this: a culture with lords and no settlement.
5. For the XSLT six, the equivalent of a delete is an unconditional empty template, the way `TAOM_Map/ModuleData/settlements.xslt` strips every vanilla settlement with `<xsl:template match="Settlement"/>`. That file lives in the game install, not the repo. Do not do this to a `Culture` element: vanilla's character-creation stage sorts its list with five `Single(...)` calls, so removing one of the six is a hard crash on the culture stage.

Check: `python tools/validate_moduledata.py --code UNKNOWN_CULTURE` then `python tools/validate_moduledata.py --code LANDLESS_CULTURE`
Takes effect: full game restart
Code: No code changes needed

### The gates, and what each one actually covers

| Gate | What it catches on a culture edit |
|---|---|
| `python tools/validate_moduledata.py` | `UNKNOWN_CULTURE` (a `Culture.` reference pointing at an id that does not exist), `LANDLESS_CULTURE` (a culture with content and no settlement), `BROKEN_PARTY_TEMPLATE_REF` (a `PartyTemplate.` reference with no definition, reported as a warning), `MISSING_EDUCATION_TEMPLATES` (a main culture with no stage-2 education tutor templates, which is the age-8 crash), `DUPLICATE_CULTURE_ID`. Run a single one with `--code <CODE>`. |
| `python tools/check_external_xslt.py` | Well-formedness of every TAOM stylesheet, including the ones in the live modules that CI cannot reach. It does not check what a transform emits. |
| `dotnet test TAOM.Tests --filter CulturePartyTemplate` | Runs `spcultures.xslt` over a synthetic vanilla document whose every party-template binding is a unique sentinel, then fails on attribute-absent, sentinel-survived or bound-to-a-non-TAOM-id. This is the only gate that can see an exclusion filter or an attribute that is simply not there. |
| `dotnet test TAOM.Tests --filter CultureLordTemplate` | The `<lord_templates>` side of the same contract. |
| `python tools/audit_cc_bonuses.py --report` | Read-only. The skill, attribute and focus bonuses each culture's character-creation options grant, which is where a new culture ends up over- or under-powered against the rest. |

`/xslt-check` is a Claude Code skill, not a shell command. It validates a stylesheet against vanilla passthrough, and it only reaches stylesheets under `Main/_Module/ModuleData/`.

None of these checks a number for being sensible. A troop bound to the wrong tier, a patrol template of 400 men, a colour that reads as black on the map: all of that is green.

## Gotchas: what fails silently and what crashes

- **An XML comment inside a child list kills every culture after it.** Every one of the 18 child loops iterates `node.ChildNodes` and dereferences `childNode.Attributes[...]` with no node-type check, and `XmlNode.Attributes` is null on a comment node. The resulting exception is swallowed by an empty catch in the loader, so the remaining cultures in the file just never load and nothing is printed. Comments between `<Culture>` blocks are fine; comments inside `<male_names>`, `<lord_templates>`, `<vassal_reward_items>` and the rest are not. `CultureObject.cs:369-508` and `MBObjectManager.cs:790-796`.
- **`is_main_culture="true"` does not make a culture playable.** Vanilla's `InitializeCharacterCreationCultures` is a literal six-string comparison against the vanilla ids, and there is no `CanChooseCulture` property anywhere. TAOM adds its own by calling `AddCharacterCreationCulture` from `CharacterCreationContentService.RegisterCustomCultures`, driven by `charactercreation/cultures.json`. [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md) lines 26 to 56.
- **Never remove one of the vanilla six, and never give a new culture an id containing `vlan`, `stur`, `empi`, `aser` or `khuz`.** The culture stage sorts with five `Single(i => i.CultureID.Contains(...))` calls, and `Single` throws on a missing match and on a duplicate one alike. Same doc, same lines.
- **The XSD is not enforced, so a misspelt attribute is silently ignored.** `MBObjectManager.LoadXmlWithValidation` sets `ValidationType.None` (`MBObjectManager.cs:1104`). `horse_merchant` instead of `horseMerchant` gives you an empty stable and no message.
- **But the XSD is still load-bearing for merging.** The merge indexes a dictionary built out of `SPCultures.xsd` by element XPath, and that lookup is outside the loader's try/catch. An element path that exists in your XML and not in the schema throws during the merge instead of being ignored. Stick to the element names in the tables above. `MBObjectManager.cs:837-856` and `:789-796`.
- **Two bindings crash rather than field the wrong troops.** `SpawnPatrolParty` dereferences `partyTemplate.ShipHulls` with no null guard and `SpawnCaravan` calls `GetRandomElementWithPredicate` on the caravan list with no empty guard. So never remove a patrol or caravan binding without adding its replacement in the same edit. [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md) lines 216 to 270.
- **A typo'd item id in `<vassal_reward_items>` or `<banner_bearer_replacement_weapons>` disappears without a word.** Both lists run `RemoveAll(x => !x.IsReady)` after parsing, and because items load before cultures, an entry that is still not ready is genuinely broken and is dropped. `CultureObject.cs:522` and `:525`.
- **An unknown `<policy id>` becomes a null entry in the list.** `GetObject<PolicyObject>` returns null for an id it does not know and the null is added with no check. `CultureObject.cs:373-374`.
- **A `<feat id>` nothing registers in C# is an empty feat with no effect.** Vanilla hard-codes 18 feat ids in `DefaultCulturalFeats.cs:88-105`. TAOM registers 130 more in `Main/Features/CulturalFeats/TaomCulturalFeats.cs`, and all 102 feat ids used in `taom_spcultures.xml` are among them. You cannot author a feat from XML alone. <!-- measured: python set comparison of Register("...") ids in TaomCulturalFeats.cs against the feat ids in taom_spcultures.xml 2026-09-05 -->
- **Five attribute names in the shipped file are read by nothing.** `veteran_caravan_guard` (16 cultures), `gear_practice_dummy`, `weapon_practice_stage_1`, `weapon_practice_stage_2` and `weapon_practice_stage_3` (2 cultures each) are declared in `SPCultures.xsd` but never read by `CultureObject.Deserialize`. The stylesheet emits 36 more of the same kind across its six blocks, including `armed_trader`. They are harmless and they are not doing anything. <!-- measured: python comparison of every attribute used in taom_spcultures.xml against the names read by the three Deserialize bodies, plus rg -no on the xsl:attribute names in spcultures.xslt 2026-09-05 -->
- **`caravan_party_template` and `elite_caravan_party_template` as attributes are pure dead markup.** They look bindable, they parse, and the deserializer never reads them. Caravans come only from the plural child elements. No shipped TAOM culture uses the attribute form. <!-- measured: rg -c on the attribute forms in taom_spcultures.xml, which returns no matches 2026-09-05 -->
- **There is no way to author culture traits.** `CultureObject.Traits` is declared but never assigned in `Deserialize`, and no XML element feeds it.
- **`docs/features/kingdom-creation.md`'s name-list example is wrong.** Its required-child-elements block writes `<name id="{=key}Firstname"/>`, but the deserializer reads `childNode2.Attributes["name"].Value` (`CultureObject.cs:381`), so an `id=` entry is a null dereference inside the swallowed try/catch. The shipped file writes `<name name="..."/>`, which is correct. Copy the shipped file, not that block.

### What TAOM has not answered

Two things a culture author will want and will not find written down anywhere in this repo.

- **How to author a `faction_banner_key` from scratch.** Every culture and every kingdom needs one, the grammar of the number groups is not decoded in any TAOM doc, and placeholder keys are a known in-game defect. The places to look are the engine's `Banner.Deserialize` and `TryGetBannerDataFromCode` in `Core/TaleWorlds.Core/TaleWorlds.Core/Banner.cs`, the icon and colour id pools in [`docs/reference/banner-icon-generation.md`](../reference/banner-icon-generation.md), and a working key such as Erebor's, which is 20 number groups long.
- **What a non-human culture needs beyond `race=` on its troops.** The race registry is `skins.xml` in the live `LOTRLOME_Armory` module, which has no managed deserializer in the decompile at all, so its attribute meanings cannot be read out of the engine. Start from [`docs/features/hero-race.md`](../features/hero-race.md) and row 14 of the checklist in [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md), which marks a missing `as_<race>_facegen` action set as a fatal T-pose.

## Numbers in this chapter

Every count below was produced on 2026-09-05 by the command beside it, run from the repo root.

| Number | Command |
|---|---|
| 24 `<Culture>` entries in `taom_spcultures.xml`, in 5,595 lines; the `erebor` block is 331 of them | a python ElementTree child count on the root, and `wc -l` |
| 16 with `is_main_culture="true"`, 8 with `is_bandit="true"`, 24 with `can_have_settlement="true"` | `rg -c 'is_main_culture="true"' Main/_Module/ModuleData/taom_spcultures.xml` and the same for the other two |
| The 24 ids: erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor, shaghana, abanissa, dunland_raiders, rhun_raiders, harad_raiders, gundabad_raiders, umbar_corsairs, gondor_soldiers, erebor_warriors, mirkwood_stalkers, goblin, mistymountainorcs, lindon, bluecraig | `rg -n '^\s+id="' Main/_Module/ModuleData/taom_spcultures.xml` |
| 92 attributes read off one `<Culture>`: 1 in `MBObjectBase`, 14 in `BasicCultureObject`, 77 in `CultureObject` | a python regex scan of the three `Deserialize` bodies for `Attributes["x"]` and `ReadObjectReferenceFromXml<T>("x", ...)` |
| 18 child list elements, matched by `item5.Name == "x"` in one if/else chain | the same scan, for `Name == "x"` inside `CultureObject.cs:264-530` |
| 11 attributes read into a property nothing else in the decompile reads | the same scan, cross-referenced against the unresolved list in the engine research for `CultureObject` |
| 84 distinct attributes used across the 24 shipped cultures, 5 of which nothing reads, in 24 occurrences | a python ElementTree attribute histogram over `taom_spcultures.xml`, differenced against the read set |
| 13 read attributes that no shipped TAOM culture sets | the same comparison, the other direction |
| 6 XSLT-wrapped cultures; their passthrough filters exclude 10, 9, 10, 9, 4 and 4 element names | `rg -n 'not\(self::' Main/_Module/ModuleData/spcultures.xslt`, counting `self::` names per line |
| 36 `<xsl:attribute>` emissions in the stylesheet naming attributes the deserializer never reads, across 6 names | `rg -no 'xsl:attribute name="(armed_trader\|veteran_caravan_guard\|gear_practice_dummy\|weapon_practice_stage_[123])"' Main/_Module/ModuleData/spcultures.xslt` |
| 2 blocks emit `<cultural_feats>` inline; 4 append through a nested `Culture[@id]/cultural_feats` template | `rg -n '<cultural_feats>'` and `rg -n 'cultural_feats">'` on the stylesheet |
| 130 feat ids registered in `TaomCulturalFeats.cs`, 102 used in `taom_spcultures.xml`, 0 of them unregistered; 18 vanilla ids in `DefaultCulturalFeats.cs` | a python set comparison of `Register("...")` against the XML feat ids, and `sed -n '88,105p'` on the decompiled file |
| 22 rows in `charactercreation/cultures.json`, 6 of them the vanilla ids the service skips | a python `json.load` on the file, and `sed -n '41,44p' Main/Features/CharacterCreation/CharacterCreationContentService.cs` |
| Child-list coverage: 24 of 24 cultures ship `<male_names>`, `<female_names>` and `<banner_bearer_replacement_weapons>`; the other 15 lists are on the 16 non-bandit cultures only | a python ElementTree per-culture child-element histogram |
| 16 recruitment-pool partial classes under `Main/Features/TroopProgression/RecruitmentPools/` | `ls Main/Features/TroopProgression/RecruitmentPools/` |

## Read next

- [`docs/features/culture-playability-wiring.md`](../features/culture-playability-wiring.md), the 14-row playability checklist and the party-template contract.
- [`docs/ai-includes/new-culture-authoring.md`](../ai-includes/new-culture-authoring.md), the bind-versus-passthrough table and the 12 standard party templates.
- [`.claude/rules/xslt.md`](../../.claude/rules/xslt.md), the passthrough rules and the two-edit rule for child elements.
- [`docs/features/kingdom-creation.md`](../features/kingdom-creation.md), the 13-file ordered sequence and the id naming conventions.
- [`docs/features/cultural-feats.md`](../features/cultural-feats.md), what a feat can actually do and where its C# lives.
- [`docs/features/tavern-mercenaries.md`](../features/tavern-mercenaries.md), the `<basic_mercenary_troops>` contract.
- [`docs/features/banner-bearers.md`](../features/banner-bearers.md), why `<banner_bearer_replacement_weapons>` cannot be empty.
- [`docs/features/bandit-management.md`](../features/bandit-management.md), the bandit-culture attribute set.
- [`docs/reference/party-template-sizing.md`](../reference/party-template-sizing.md), what the numbers inside a template mean.
- [`docs/features/moduledata-validation.md`](../features/moduledata-validation.md), what the validator covers and what it does not.
- [`docs/reviews/lessons/xslt-moduledata.md`](../reviews/lessons/xslt-moduledata.md), the four shipped instances of the passthrough bug.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/banners-and-heraldry.md](./banners-and-heraldry.md)
- [docs/modding/body-properties.md](./body-properties.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/kingdoms.md](./kingdoms.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/npcs-notables-and-townsfolk.md](./npcs-notables-and-townsfolk.md)
- [docs/modding/party-templates.md](./party-templates.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/settlements.md](./settlements.md)
- [docs/modding/troops.md](./troops.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
