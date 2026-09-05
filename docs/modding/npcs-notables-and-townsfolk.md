# Notables, headmen and townsfolk

## What this file is

`characters/npcs_<culture>.xml` holds every named-but-not-lord character a culture needs: the 26 notable templates the game clones into town merchants, preachers, artisans, gang leaders, rural notables and village headmen, the shop and tavern NPCs who stand in the settlement scenes, and the arena practice dummy that decides what tournament fighters wear. Every entry is an `<NPCCharacter>`, the same element a troop uses, so [Troops](troops.md) covers the shared attributes and this chapter covers only what a notable or a townsperson does differently. Writing an entry here is half the job: a notable stays invisible to the game until its culture also lists it in `<notable_templates>`.

## Where it lives and how it is registered

- **Path:** `Main/_Module/ModuleData/characters/npcs_<culture>.xml`. There are 22 of them, holding 1,409 `<NPCCharacter>` entries between them <!-- measured: python ElementTree walk of Main/_Module/ModuleData/characters/npcs_*.xml 2026-09-05 -->. Worked example below: [`npcs_erebor.xml`](../../Main/_Module/ModuleData/characters/npcs_erebor.xml).
- **Root element:** `<NPCCharacters>`. **Per-entry element:** `<NPCCharacter>`. **Engine class:** `TaleWorlds.CampaignSystem.CharacterObject`, deserialized at `CharacterObject.cs:536-601` on top of `BasicCharacterObject.cs:315-527`.
- **First registration, the file itself.** One `<XmlName>` row per file in `Main/_Module/SubModule.xml`, 22 of them, all under the same `id` <!-- measured: rg -c 'characters/npcs_' Main/_Module/SubModule.xml 2026-09-05 -->:

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
      <XmlName id="NPCCharacters" path="characters/npcs_erebor"/>
```

  Troop files, `characters/lords.xml` and the wanderer file all use that same `NPCCharacters` id, so every id in this folder shares one global namespace with every troop id. A duplicate is a real collision, not a per-file matter.

- **Second registration, the notable pool.** A `<template name="NPCCharacter.<id>"/>` row inside the culture's `<notable_templates>` block. For 16 cultures that block is in `Main/_Module/ModuleData/taom_spcultures.xml`; for the six cultures that reskin a vanilla culture id it is in `Main/_Module/ModuleData/spcultures.xslt` instead <!-- measured: python ElementTree count of Culture/notable_templates in taom_spcultures.xml plus regex scan of spcultures.xslt 2026-09-05 -->.

The six reskins matter because the file name and the culture id do not agree, and the culture id is what the engine uses:

| `npcs_*.xml` file | `culture=` written on every entry | Where its `<notable_templates>` lives |
|---|---|---|
| `npcs_dale.xml` | `Culture.sturgia` | `spcultures.xslt` |
| `npcs_dunland.xml` | `Culture.empire` | `spcultures.xslt` |
| `npcs_harad.xml` | `Culture.aserai` | `spcultures.xslt` |
| `npcs_khand.xml` | `Culture.battania` | `spcultures.xslt` |
| `npcs_rhun.xml` | `Culture.khuzait` | `spcultures.xslt` |
| `npcs_rohan.xml` | `Culture.vlandia` | `spcultures.xslt` |
| the other 16 | matches the file name | `taom_spcultures.xml` |

<!-- measured: python ElementTree count of the culture attribute per npcs_*.xml 2026-09-05 -->

## Attributes

These are the attributes `CharacterObject.Deserialize` reads on top of everything the shared base class reads. The base-class set (`id`, `name`, `race`, `culture`, `is_hero`, `is_female`, `skill_template`, `default_group`, `age` and the rest) is the same for a notable as for a troop and is tabled in [Troops](troops.md).

<!-- engine-table type="TaleWorlds.CampaignSystem.CharacterObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" method="Deserialize" inert="" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `occupation` | enum | in practice yes | `NotAssigned` | The role switch for this whole chapter. `Merchant`, `Preacher`, `Artisan`, `GangLeader`, `RuralNotable` and `Headman` make an entry eligible to become a settlement notable; `Tavernkeeper`, `Armorer`, `GoodsTrader`, `Musician` and friends mark a service NPC. Parsed with `Enum.Parse`, so a misspelling throws and the whole file stops loading. | `CharacterObject.cs:539` |
| `is_template` | bool | for a notable, yes | `false` | Marks the entry as a blueprint the game copies rather than a character that exists in the world. Templates are skipped by ageing, marriage and caravan spawning. Every notable in TAOM carries it. | `CharacterObject.cs:544` |
| `is_hidden_encyclopedia` | bool | no | `false` | Keeps the entry out of the in-game encyclopedia. Reasonable on a template or a scene prop that the player should never browse. | `CharacterObject.cs:546` |
| `voice` | ref, **not** prefixed | no | none, `GetPersona()` returns `PersonaSoftspoken` | Which set of dialogue lines the character speaks. Takes a bare trait id (`curt`, `ironic`, `earnest`, `softspoken`), with no `Trait.` in front of it, because this one attribute skips the dotted-reference reader. | `CharacterObject.cs:572` |
| `is_basic_troop` | bool | no | `false` | Troop-tree entry point. Meaningless on a notable or a townsperson; see [Troops](troops.md). | `CharacterObject.cs:577` |
| `upgrade_requires` | ref, prefixed | no | null | Troop upgrade gate. Meaningless here. | `CharacterObject.cs:586` |
| `level` | int | no | `1` | Power number. A notable never fights, so leaving it off is normal; TAOM leans on `skill_template` instead. | `CharacterObject.cs:587` |
| `civilianTemplate` | ref, prefixed | no | none | Deprecated. Present, and the engine fires an assert before honouring it. Use an `<EquipmentRoster civilian="true">` instead. | `CharacterObject.cs:589` |
| `battleTemplate` | ref, prefixed | no | none | Deprecated, same assert. Use a plain `<EquipmentRoster>`. | `CharacterObject.cs:594` |

### The `occupation` values this file uses

`occupation` accepts 33 names. Six of them make a notable, the rest of the ones you will meet here mark a service NPC or a scene prop.

<!-- engine-ref type="TaleWorlds.CampaignSystem.Occupation" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" lines="539-542" -->

| Group | Values | Count across the 22 files |
|---|---|---|
| Notable, town | `Merchant`, `Artisan`, `GangLeader`, `Preacher` | 220 / 44 / 149 / 66 |
| Notable, village | `RuralNotable`, `Headman` | 51 / 66 |
| Shops and tavern | `GoodsTrader`, `Armorer`, `Weaponsmith`, `Blacksmith`, `HorseTrader`, `Tavernkeeper`, `TavernWench`, `TavernGameHost`, `Musician`, `RansomBroker`, `ShopWorker` | 17 to 18 each |
| Scene and quest | `Townsfolk`, `Villager`, `Guard`, `PrisonGuard`, `ArenaMaster`, `CaravanGuard`, `Soldier`, `Lord` | 366 / 40 / not tabled / 18 / 18 / 66 / 71 / 32 |

<!-- measured: python ElementTree Counter of the occupation attribute over npcs_*.xml 2026-09-05 -->

Only the six notable values are drawn from `<notable_templates>`. Everything else is reached by a named attribute on the culture, described next.

### The culture side: 39 NPC-role attributes

Every service NPC in a settlement scene is fetched by name from one attribute on the `<Culture>` element, not by scanning the file. The complete `<Culture>` attribute table is in [Cultures](cultures.md); this is the slice that points at this file.

<!-- engine-ref type="TaleWorlds.CampaignSystem.CultureObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CultureObject.cs" lines="292-338" -->

| Attribute | What stands where | Read at |
|---|---|---|
| `tournament_master` | the arena master who offers tournaments and practice fights | `CultureObject.cs:292` |
| `villager`, `caravan_master`, `caravan_guard` | village crowd filler, caravan leader, hireable caravan guard | `CultureObject.cs:293-295` |
| `prison_guard`, `guard` | dungeon NPC, town and keep guard | `CultureObject.cs:296-297` |
| `blacksmith`, `weaponsmith`, `armorer`, `merchant`, `horseMerchant` | the five shop NPCs. `horseMerchant` is camelCase; get the case wrong and it silently reads null | `CultureObject.cs:298-299`, `:324-327` |
| `townswoman`, `townsman` plus `_infant` / `_child` / `_teenager` for each | the eight street-crowd characters | `CultureObject.cs:300-307` |
| `village_woman`, `villager_male_child`, `villager_male_teenager`, `villager_female_child`, `villager_female_teenager` | the five village-scene characters | `CultureObject.cs:308-312` |
| `ransom_broker`, `tavernkeeper`, `taverngamehost`, `musician`, `tavern_wench`, `female_dancer` | the tavern staff. `taverngamehost` has no underscores | `CultureObject.cs:313`, `:320-323`, `:330` |
| `gangleader_bodyguard`, `merchant_notary`, `artisan_notary`, `preacher_notary`, `rural_notable_notary` | the helper who stands beside a notable and takes his quests | `CultureObject.cs:314-318` |
| `shop_worker`, `barber`, `beggar`, `female_beggar` | workshop labourer, face-change NPC, two street beggars | `CultureObject.cs:319`, `:326`, `:328-329` |

All 39 are set on all 16 cultures in `taom_spcultures.xml`, and all 39 are overridden on all six XSLT-reskinned vanilla ids, so nothing falls through to a Calradian default by omission <!-- measured: python ElementTree scan of the 39 role attributes per Culture plus a regex scan of the six xsl:template blocks 2026-09-05 -->. What they are set *to* is a different question: see the gotchas.

## Child elements

<!-- engine-table type="TaleWorlds.CampaignSystem.CharacterObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" method="Deserialize" inert="" -->

| Element | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Traits>` | container | no | no traits | `<Traits><Trait id="Valor" value="-1"/></Traits>`. Bare trait ids, no prefix. The element name is case-sensitive: `<traits>` in lower case is skipped in silence. An unknown trait id is dropped without a warning, and `value="0"` removes the entry rather than storing a zero. | `CharacterObject.cs:551` |
| `<upgrade_targets>` | container | no | empty | Troop-tree wiring, no meaning on a notable. Covered in [Troops](troops.md). | `CharacterObject.cs:557` |
| `<upgrade_target>` | child of the above | no | none | Same. | `CharacterObject.cs:563` |
| `id` | ref, prefixed | on `<upgrade_target>` | none | The `NPCCharacter.<id>` reference an `<upgrade_target>` carries. Not the `id` on `<NPCCharacter>` itself, which the object base reads. | `CharacterObject.cs:565` |

Three more children are read by the shared base class and behave exactly as they do on a troop, so they are tabled once, in the chapters that own them: `<Equipments>` and its `<EquipmentRoster>` blocks ([Equipment rosters](equipment-rosters.md), `BasicCharacterObject.cs:360-413`), `<face>` with its `face_key_template` ([Body properties](body-properties.md), `BasicCharacterObject.cs:415`) and `<skills>` ([Skill sets](skill-sets.md), `BasicCharacterObject.cs:353`). Two details from that set bite here specifically and are listed under the gotchas: `civilian="true"` on a roster, and what happens when a character has only civilian rosters.

### `<notable_templates>` on the culture

<!-- engine-ref type="TaleWorlds.CampaignSystem.CultureObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CultureObject.cs" lines="419-426" -->

| Thing | Shape | Behaviour |
|---|---|---|
| `<notable_templates>` | container on `<Culture>` | Optional; absent means an empty pool. Across modules the entries merge as a union keyed on `@name`. |
| `<template name="NPCCharacter.<id>"/>` | one row per template | The attribute is `name`, not `id`, and its value is the dotted reference form. The loop does not check the child element's name, so any child with a `name` attribute is accepted, and a child *without* one contributes a null entry that later crashes the culture. |

The same block also carries the culture's wanderer templates: of 795 `<template>` rows across every `<notable_templates>` block, 596 are notables and headmen and 199 are wanderers <!-- measured: python scan of every notable_templates block in taom_spcultures.xml and spcultures.xslt, split on the id prefix 2026-09-05 -->. Wanderers are otherwise a separate subject, in [Wanderers and named companions](wanderers-and-named-companions.md).

## Worked example

A Dwarven merchant notable, copied whole out of the shipped file:

<!-- example file="Main/_Module/ModuleData/characters/npcs_erebor.xml" id="spc_notable_erebor_0" -->
```xml
	<NPCCharacter id="spc_notable_erebor_0" race="dwarf" default_group="Infantry" is_template="true" is_hero="false" voice="ironic" culture="Culture.erebor" name="{=aom_er_notable_0}Cautious dwarven merchant" skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills" occupation="Merchant">
		<face><face_key_template value="BodyProperty.fighter_erebor" /></face>
		<skills></skills>
		<Traits><Trait id="Valor" value="-1" /><Trait id="Calculating" value="1" /></Traits>
		<Equipments>
			<EquipmentRoster civilian="true">
				<equipment slot="Body" id="Item.sk_dwarf_erebor_chest_leather_med_a" />
				<equipment slot="Leg" id="Item.sk_dwarf_erebor_boots_light_a" />
			</EquipmentRoster>
			<EquipmentRoster>
				<equipment slot="Body" id="Item.sk_dwarf_erebor_chest_leather_med_a" />
				<equipment slot="Leg" id="Item.sk_dwarf_erebor_boots_light_a" />
			</EquipmentRoster>
		</Equipments>
	</NPCCharacter>
```

That entry does nothing on its own. The second half of it is one row inside the Erebor culture's `<notable_templates>` block, where the wanderer templates are listed first and the notables follow:

<!-- excerpt file="Main/_Module/ModuleData/taom_spcultures.xml" -->
```xml
      <template name="NPCCharacter.spc_wanderer_erebor_11" />
      <template name="NPCCharacter.spc_notable_erebor_0" />
      <template name="NPCCharacter.spc_notable_erebor_0b" />
```

And the arena practice dummy, which lives in the same file and is reached a third way again, by an id the engine builds from the culture id:

<!-- example file="Main/_Module/ModuleData/characters/npcs_erebor.xml" id="gear_practice_dummy_erebor" -->
```xml
	<NPCCharacter id="gear_practice_dummy_erebor" race="dwarf" default_group="Infantry" name="{=aom_er_practice_dummy}Practice Dummy" occupation="Townsfolk" culture="Culture.erebor" skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills">
		<face><face_key_template value="BodyProperty.fighter_erebor" /></face>
		<skills></skills>
		<Equipments>
			<EquipmentRoster>
				<equipment slot="Body" id="Item.sk_dwarf_erebor_chest_chain_a" />
				<equipment slot="Leg" id="Item.sk_dwarf_erebor_boots_light_a" />
			</EquipmentRoster>
		</Equipments>
	</NPCCharacter>
```

What a reader changes first, in this order:

1. **`occupation`.** It decides which of the six notable pools the entry joins, and it is the one attribute here that kills the file load when misspelled.
2. **The two `<EquipmentRoster>` blocks.** The first, with `civilian="true"`, is what the notable wears standing in town. The second, without it, is what he wears in the arena crowd and anywhere the engine asks for battle gear. They are twins on purpose; see the gotcha about naked spectators.
3. **`name`.** It is the template's label in the files and in the encyclopedia entry for the template. It is not what a player reads on the notable in a town, because that name is generated.

## Recipes: Add / Modify / Delete

### Add: a new notable slot

1. **Pick the id from the culture's existing pattern.** Merchants are `spc_notable_<culture>_0` through `_4b` (0, 0b, 1, 1b, 2, 2b, 3, 3b, 4, 4b), preachers `_5` `_6` `_7`, artisans `_8` `_9`, gang leaders `_gl1` `_10` `_11` `_gl4` `_12` `_13`, rural notables `_21` `_22`, headmen `spc_<culture>_headman_1` through `_3`. Extra slots continue the numbering: the third rural notable is `_23`, extra gang leaders run `_gl5` upward ([xml-data rule](../../.claude/rules/xml-data.md) "Culture NPC Naming Convention").
2. **Copy the nearest sibling of the same occupation** in `Main/_Module/ModuleData/characters/npcs_<culture>.xml` and change the id, the `name` key and the traits. Copying keeps `race`, `face_key_template` and `skill_template` correct without your having to look them up.
3. **Keep `is_template="true"` and `is_hero="false"`.** Without the first, the entry is a character in the world rather than a blueprint, and the pool never sees it.
4. **Give it both rosters,** a `civilian="true"` one and a plain twin, even if they hold the same two items. A civilian-only notable renders naked in the arena crowd.
5. **Register it.** Add `<template name="NPCCharacter.<your id>" />` inside that culture's `<notable_templates>` block: `Main/_Module/ModuleData/taom_spcultures.xml` for the 16 cultures that own a `<Culture>` entry there, `Main/_Module/ModuleData/spcultures.xslt` for dale, dunland, harad, khand, rhun and rohan.
6. **Check the pool size against the target.** Adding templates only helps if the settlement's notable target is above the current pool. Targets per culture and occupation are in [cultural-feats](../features/cultural-feats.md) "Per-Settlement Notable-Count Feats"; the vanilla base is 5 in a town and 3 in a village.

Check: `python tools/validate_moduledata.py --code DUPLICATE_NPC_ID --code BROKEN_ITEM_REF --code BROKEN_BODY_PROPERTY_REF`
Takes effect: new campaign only. An existing save keeps the notables it already created; the game draws from the pool again only when it needs a new notable.
Code: No code changes needed

### Add: a tournament armour set for a culture

1. **Work out the culture's string id first,** not the file name. `TaomTournamentModel.GetParticipantArmor` asks `TournamentService.ResolveDummyId` for `gear_practice_dummy_<culture StringId>` (`Main/Features/Arena/TournamentService.cs:64-71`), and vanilla does the same thing at `DefaultTournamentModel.cs:90`. For a reskinned culture the string id is the vanilla one, so Rohan's dummy has to be named `gear_practice_dummy_vlandia`, not `_rohan`.
2. **Add one `<NPCCharacter id="gear_practice_dummy_<string id>" ...>`** to `Main/_Module/ModuleData/characters/npcs_<culture>.xml`, copying the shape of the Erebor block above.
3. **Give it a plain, non-civilian `<EquipmentRoster>`.** Only `RandomBattleEquipment` is read, so a civilian-only roster produces nothing and the model falls back to vanilla.
4. **Use items that fit the skeleton.** A dwarf-race dummy in man-sized armour is the same mesh mismatch as anywhere else; item ids come from the Armory ([armory-guide](../reference/armory-guide.md)).
5. **Confirm the item ids resolve.** A typo here is silent: the slot ends up empty and the fighters go out in their underwear.

Check: `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`
Takes effect: full game restart
Code: No code changes needed

### Modify: replace a townsfolk outfit

1. **Find who actually stands there.** The scene NPC is whatever the culture attribute names, so start from the `<Culture>` element and read the id out of `tavernkeeper`, `townsman`, `armorer` and so on. Four cultures point some or all of these at another culture's characters, so the file you need may not be the one named after the culture.
2. **Edit the `civilian="true"` roster** in that character's entry. That is the outfit worn walking a town or village.
3. **Edit the plain roster too, or add one.** It is the one the arena crowd and any battle context uses. If the character has only a civilian roster, do not hand-write the twin: run the tool, which mirrors every civilian roster into a battle twin and skips characters that already have one.
4. **Re-run the item-reference check** before launching, because a wrong item id here produces an empty slot rather than an error.

Check: `python tools/add_townsfolk_battle_rosters.py` for the dry run, then `python tools/add_townsfolk_battle_rosters.py --apply`, then `python tools/validate_moduledata.py --code BROKEN_ITEM_REF`
Takes effect: full game restart
Code: No code changes needed

### Delete: retire a notable template

1. **Remove the `<template name="NPCCharacter.<id>" />` row first,** from the culture's `<notable_templates>` block. Delete the `<NPCCharacter>` while the row still points at it and the reference resolves to a placeholder that the occupation filter then dereferences, which takes down every notable creation for that culture.
2. **Check the pool that is left.** If the id you removed was the only template of its occupation for that culture, notable creation for that occupation crashes rather than skipping. Count what remains before you delete.
3. **Only then remove the `<NPCCharacter>` entry** from `npcs_<culture>.xml`.
4. **Prefer retiring over deleting** where an old save matters: `is_obsolete="true"` on the entry, and the template row removed, keeps the id resolvable.
5. **Grep for the id across the repo** before removing it. A notable id can also appear in a culture role attribute, since `merchant_notary` and its three siblings point at `spc_notable_<culture>_0` and `_8`.

Check: `python tools/validate_moduledata.py --code BROKEN_TROOP_REF --code DUPLICATE_NPC_ID`
Takes effect: new campaign only
Code: No code changes needed

## Gotchas: what fails silently and what crashes

- **Defining a notable without registering it does nothing at all.** The engine builds the spawn pool from `culture.NotableTemplates` and never enumerates `npcs_*.xml`, so an unregistered template is simply not in the draw and the settlement fills the slot from an existing archetype instead (`DefaultHeroCreationModel.cs:252`). Both edits, every time ([xml-data rule](../../.claude/rules/xml-data.md), RCA [rca-cultural-feats-3pack-2026-05-31](../reviews/rca-cultural-feats-3pack-2026-05-31.md)).
- **No template for an occupation is a hard crash, not a skipped notable.** `GetRandomTemplateByOccupation` returns null when the filtered list is empty (`DefaultHeroCreationModel.cs:259-262`) and `HeroCreator.CreateNotable` passes that null straight into `GetBirthAndDeathDay` and `CreateHero` with no check (`HeroCreator.cs:174-178`). On a castle this once turned into an infinite new-game loading screen rather than a crash to desktop ([castle-recruitment](../features/castle-recruitment.md)).
- **One malformed `<template>` row poisons the whole culture.** The reader takes any child element and asks it for a `name` attribute (`CultureObject.cs:421-424`); a child without one adds a null to the list, and the occupation filter dereferences every entry (`DefaultHeroCreationModel.cs:252`), so every notable creation for that culture throws.
- **The template's `name=` never reaches the player.** `CreateNotable` sets `GenerateFirstAndFullName` (`HeroCreator.cs:179`) and the initializer overwrites the name from the culture's `<male_names>` and `<female_names>` lists (`HeroCreator.cs:311-314`, `DefaultHeroCreationModel.cs:355-359`). Two TAOM docs describe an over-drawn pool as producing "duplicate names"; what actually repeats is the face template, the traits and the equipment.
- **A misspelled `occupation` takes down the whole file.** `Enum.Parse` at `CharacterObject.cs:542` throws, and nothing in `npcs_<culture>.xml` loads. The same is true of `race`, which indexes a dictionary rather than falling back to human. The validator does not catch either: `tools/schemas/taom_npccharacter.json` enumerates `default_group` and nothing else.
- **A civilian-only character renders naked in the arena.** Arena spectators are the settlement culture's townsfolk and notables, spawned with battle equipment by `MissionAudienceHandler`, while the town walk uses civilian equipment ([arena](../features/arena.md) "The arena crowd is not TAOM's"). A character with only `<EquipmentRoster civilian="true">` has an empty `FirstBattleEquipment`. `tools/add_townsfolk_battle_rosters.py` mirrors the civilian roster into a battle twin and is idempotent.
- **`civilian="true"` still works and still fires an assert.** The engine reads it, tells you to write `equipmentType="Civilian"` instead, then honours it (`BasicCharacterObject.cs:395-401`). Both spellings parse; the shipped files use the old one.
- **The `gear_practice_dummy` attribute on a `<Culture>` wires nothing.** No deserializer in the v1.4.8 dump reads an attribute of that name. The only place the string appears is the id the tournament model concatenates at `DefaultTournamentModel.cs:90`. The lookup is by id, so the attribute is decoration.
- **Six authored practice dummies can never be selected.** `gear_practice_dummy_dale`, `_dunland`, `_harad`, `_khand`, `_rhun` and `_rohan` exist, but those cultures' string ids are `sturgia`, `empire`, `aserai`, `battania`, `khuzait` and `vlandia`, so the composed id lands on vanilla's Calradian dummy instead. Three more cultures (abanissa, shaghana, umbar) have no dummy at all and fall through to the vanilla model <!-- measured: rg -o 'id="gear_practice_dummy_[a-z]*"' over npcs_*.xml joined against the culture attribute per file 2026-09-05 -->.
- **Four cultures ship notables but no service NPCs.** Of the 22 files, 18 carry a full service roster and abanissa, lothlorien, shaghana and umbar carry only the 26 templates plus two lord templates. Their culture role attributes borrow: abanissa and shaghana point at TAOM's `*_harad` characters, lothlorien at `*_rivendell`, and Umbar points all 35 of its townsfolk and shop roles at vanilla Calradian ids such as `townsman_aserai` and `tavernkeeper_aserai`, defined in `SandBoxCore/ModuleData/spnpccharacters.xml`. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. An Umbar street crowd is therefore Calradian by data, not by accident of art <!-- measured: python ElementTree read of the 39 role attributes per Culture, matched against the ids defined in npcs_*.xml 2026-09-05 -->.
- **One shop merchant is out of pattern.** 17 of the 18 full service rosters give the general-goods shopkeeper `occupation="GoodsTrader"`; `merchant_rhun` uses `Merchant`, a notable occupation, and is the only notable-occupation entry in the folder without `is_template="true"` <!-- measured: python ElementTree Counter of occupation and is_template over npcs_*.xml 2026-09-05 -->. It is not registered in any `<notable_templates>` block, so it cannot be drawn as a notable.
- **`Frequency` weights the draw and nobody uses it.** `GetRandomTemplateByOccupation` weights each template by `GetTraitLevel(Frequency) * 10`, defaulting to 100 when the trait is absent or zero (`DefaultHeroCreationModel.cs:256-257`, `:266-267`). The trait is real, hidden, range 0 to 20 (`DefaultTraits.cs:129`, `:164`). It appears zero times in `Main/_Module/ModuleData/characters/` and zero times in vanilla's `spnpccharacters.xml` <!-- measured: rg -o 'Trait id="Frequency"' over Main/_Module/ModuleData/characters and SandBoxCore/ModuleData/spnpccharacters.xml 2026-09-05 -->. It is the one lever for "this archetype should be rare", and it is unused.
- **The pool is sampled with replacement.** A target equal to the pool size does not give one of each: expect roughly 63 percent distinct archetypes ([cultural-feats](../features/cultural-feats.md) "Known characteristic").
- **File hygiene is uniform in this folder and worth keeping.** All 22 files are UTF-8 with no BOM and CRLF endings <!-- measured: python byte-level check of BOM and CRLF over npcs_*.xml 2026-09-05 -->. A tool that rewrites one to LF, or adds a BOM, produces a diff nobody can read; the repo convention is in [xml-data rule](../../.claude/rules/xml-data.md).

### Not answered anywhere in TAOM

- **Whether the base roster is really 26 for every culture.** It is not any more. Measured: every culture ships 10 merchants, 3 preachers, 2 artisans and 3 headmen, but 7 cultures now ship 3 rural notables rather than 2 (bluecraig, dolguldur, goblin, gundabad, isengard, mistymountainorcs, mordor) and gang leaders run 6 everywhere except Isengard at 14 and Dol Guldur at 15 <!-- measured: python ElementTree per-file Counter of occupation where is_template is true 2026-09-05 -->. [cultural-feats](../features/cultural-feats.md) names four cultures for the third rural notable; three more have since been added and no doc records why.
- **Whether a running save ever adopts a newly added template.** Nothing in `docs/` tests it. The engine draws from the pool only when it creates a notable, so an existing settlement's notables plainly do not change, but the timing of a top-up after a notable dies is untested here.
- **What sets tournament difficulty and picks the opponents.** [tournament-armor-assignment](../features/tournament-armor-assignment.md) covers the prize pool and the practice dummy, and `Main/Features/Arena/Models/TaomTournamentModel.cs` overrides five methods, none of which chooses fighters. The named lever is on the culture, not in this file: `<tournament_team_templates_one_participant>`, `_two_` and `_four_`, selected by team size at `TournamentFightMissionController.cs:171-175`. All 112 rows across TAOM's 16 cultures point at vanilla's `tournament_template_empire_*` and `tournament_template_aserai_*` characters, so no TAOM culture fields its own tournament fighters today <!-- measured: python ElementTree scan of the three tournament_team_templates lists in taom_spcultures.xml 2026-09-05 -->. Whether that is deliberate is not written down.
- **Which `<Culture>` attributes are mandatory for a playable culture.** Three TAOM docs give three different counts and none gives a required-or-optional split. The deserializer at `CultureObject.cs:264` is the only authority; see [Cultures](cultures.md).

## Numbers in this chapter

| Number | How it was produced |
|---|---|
| 22 `npcs_<culture>.xml` files, 1,409 `<NPCCharacter>` entries | Python `ElementTree` walk of `Main/_Module/ModuleData/characters/npcs_*.xml` counting `NPCCharacter` children <!-- measured: python ElementTree walk of npcs_*.xml 2026-09-05 --> |
| 22 `<XmlName id="NPCCharacters">` rows for this folder | `rg -c 'characters/npcs_' Main/_Module/SubModule.xml` <!-- measured: rg -c characters/npcs_ SubModule.xml 2026-09-05 --> |
| 596 notable templates, all 596 registered, 0 missing | Python set difference: ids with `is_template="true"` and a notable occupation, against every `<template name>` in `taom_spcultures.xml` and `spcultures.xslt` <!-- measured: python set difference of defined against registered notable templates 2026-09-05 --> |
| Occupation split: Merchant 220, GangLeader 149, Preacher 66, Headman 66, RuralNotable 51, Artisan 44 | same walk, `Counter` on the `occupation` attribute where `is_template="true"` <!-- measured: python ElementTree Counter of occupation over npcs_*.xml 2026-09-05 --> |
| Base roster 26: 10 Merchant, 3 Preacher, 2 Artisan, 6 GangLeader, 2 RuralNotable, 3 Headman | same walk, per file <!-- measured: python ElementTree per-file Counter of occupation where is_template is true 2026-09-05 --> |
| 7 cultures at 3 RuralNotable; Isengard 14 and Dol Guldur 15 GangLeader | same per-file walk <!-- measured: python ElementTree per-file Counter of occupation where is_template is true 2026-09-05 --> |
| 795 `<template>` rows in `<notable_templates>`: 596 notables, 199 wanderers | Python scan of every `notable_templates` block in both culture sources, split on the id prefix <!-- measured: python scan of notable_templates blocks split on id prefix 2026-09-05 --> |
| 16 of 24 `<Culture>` entries in `taom_spcultures.xml` carry `<notable_templates>`; the six reskins carry theirs in `spcultures.xslt` | Python `ElementTree` over `taom_spcultures.xml` plus a regex scan of the six `xsl:template` blocks <!-- measured: python ElementTree plus regex scan of spcultures.xslt 2026-09-05 --> |
| 39 NPC-role attributes, all set on all 16 cultures and overridden on all 6 XSLT blocks | Python read of the 39 attribute names per `<Culture>` and per `xsl:template` block <!-- measured: python ElementTree read of the 39 role attributes per Culture 2026-09-05 --> |
| 18 of 22 files ship a full service roster; abanissa, lothlorien, shaghana and umbar do not | same per-file walk, counting entries outside the six notable occupations <!-- measured: python ElementTree per-file count of non-notable occupations 2026-09-05 --> |
| 19 `gear_practice_dummy_*` entries; 6 unreachable by string id, 3 cultures with none | `rg -o 'id="gear_practice_dummy_[a-z]*"'` over `npcs_*.xml`, joined against the `culture` attribute written in each file <!-- measured: rg -o gear_practice_dummy joined against the culture attribute per file 2026-09-05 --> |
| 17 of 18 service rosters use `GoodsTrader`; `merchant_rhun` uses `Merchant` | same walk, `Counter` on `occupation` per file <!-- measured: python ElementTree Counter of occupation per npcs file 2026-09-05 --> |
| 112 tournament team-template rows, all vanilla `tournament_template_empire_*` or `_aserai_*` | Python `ElementTree` scan of the three `tournament_team_templates_*` lists in `taom_spcultures.xml` <!-- measured: python ElementTree scan of tournament_team_templates lists 2026-09-05 --> |
| 0 `<Trait id="Frequency">` in TAOM or in vanilla's NPC file | `rg -o 'Trait id="Frequency"'` over `Main/_Module/ModuleData/characters/` and over `SandBoxCore/ModuleData/spnpccharacters.xml` <!-- measured: rg -o Trait id Frequency over both trees 2026-09-05 --> |
| 22 of 22 files: no BOM, CRLF endings | Python byte-level check of the first three bytes and of the CRLF against LF counts <!-- measured: python byte-level BOM and CRLF check over npcs_*.xml 2026-09-05 --> |

## Read next

- [xml-data rule](../../.claude/rules/xml-data.md), the two-layer registration rule and the 26-slot naming convention.
- [cultures](../cultures.md), the culture-authoring checklist this file sits inside.
- [cultural-feats](../features/cultural-feats.md), the per-settlement notable-count targets and the sampling-with-replacement note.
- [castle-recruitment](../features/castle-recruitment.md), what a missing template does on a castle and the guard that now catches it.
- [arena](../features/arena.md), who the arena crowd really is.
- [tournament-armor-assignment](../features/tournament-armor-assignment.md), the practice-dummy coverage table and the prize-pool filter.
- [moduledata-validation](../features/moduledata-validation.md), what the validator walks and what it does not.
- [rca-cultural-feats-3pack-2026-05-31](../reviews/rca-cultural-feats-3pack-2026-05-31.md) and [rca-castle-recruitment-guard-2026-07-07](../reviews/rca-castle-recruitment-guard-2026-07-07.md).
- [tools README](../../tools/README.md), for `add_townsfolk_battle_rosters.py` and the validator family.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/body-properties.md](./body-properties.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-kingdom.md](./recipe-add-a-kingdom.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/skill-sets.md](./skill-sets.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)

<!-- backlinks-end -->
