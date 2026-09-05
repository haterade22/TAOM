# Troops

## What this file is

A troop file is a list of `<NPCCharacter>` entries, one per rung of a culture's recruitment tree, and it is the only place a soldier's level, skills, formation, race and kit are written down. TAOM ships 16 of them under `Main/_Module/ModuleData/troops/`, holding 857 troops between them. <!-- measured: python ElementTree count of NPCCharacter over troops/troops_*.xml 2026-09-05 -->
The same element type also defines lords, notables, wanderers and education templates in other folders, so everything in this chapter applies to them too, and every id in all of those files shares one global namespace.

## Where it lives and how it is registered

| | |
|---|---|
| Path | [`Main/_Module/ModuleData/troops/troops_<culture>.xml`](../../Main/_Module/ModuleData/troops/) |
| Root element | `<NPCCharacters>` |
| Per-entry element | `<NPCCharacter>` |
| Engine class | `TaleWorlds.CampaignSystem.CharacterObject`, which extends `TaleWorlds.Core.BasicCharacterObject`, which extends `TaleWorlds.ObjectSystem.MBObjectBase` |
| Registration | one `<XmlNode>` block per file in [`Main/_Module/SubModule.xml`](../../Main/_Module/SubModule.xml), carrying `<XmlName id="NPCCharacters" .../>` |

Every troop file gets its own block. The Gondor one starts at `Main/_Module/SubModule.xml` line 179:

<!-- excerpt file="Main/_Module/SubModule.xml" -->
```xml
    <XmlNode>
      <XmlName id="NPCCharacters" path="troops/troops_gondor"/>
      <IncludedGameTypes>
        <GameType value ="Campaign"/>
        <GameType value ="CampaignStoryMode"/>
        <GameType value = "CustomGame"/>
```

The `path` carries no `.xml` extension and no leading `ModuleData/`. There are 44 `<XmlName id="NPCCharacters">` rows in TAOM's SubModule.xml, 16 of which are the troop files; the rest are lords, notables, wanderers and templates. <!-- measured: grep -c 'id="NPCCharacters"' Main/_Module/SubModule.xml and grep -n "troops/" Main/_Module/SubModule.xml 2026-09-05 -->

Two engine calls matter for the rest of this chapter. `Campaign.cs:1537` binds the element name to the campaign class (`RegisterType<CharacterObject>("NPCCharacter", "NPCCharacters", 16u)`), and `SandBoxManager.cs:362` calls `LoadXML("NPCCharacters")` when the campaign object is built. So the file is parsed when a campaign starts or a save loads, not while the game sits at the main menu. The `<XmlNode>` list itself is read once at process launch, which is why a brand new file needs a full game restart even though an edit to an existing file does not. For how your repo edits reach the running game at all, see [editing-safely](editing-safely.md) and [submodule-and-registration](submodule-and-registration.md).

## Attributes

Three classes read attributes off one `<NPCCharacter>` element, in this order: `MBObjectBase.Deserialize`, then `BasicCharacterObject.Deserialize`, then `CharacterObject.Deserialize`. They are split into three tables because they live in three files.

<!-- engine-table type="TaleWorlds.ObjectSystem.MBObjectBase" file="Core/TaleWorlds.ObjectSystem/TaleWorlds.ObjectSystem/MBObjectBase.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `id` | string | yes | none; the read is `node.Attributes["id"].Value` with no null check, so a missing id throws and the whole file fails to load | The permanent codename. Saves store it, and party templates, upgrade edges, culture bindings and the recruitment pools all point at it. Renaming one is a save break. | `MBObjectBase.cs:61` |

<!-- engine-table type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `name` | string | in practice yes | no name is set at all | Display text in the party screen, encyclopedia and troop tree. Free to change, it is not an identity. TAOM writes it as `{=key}English fallback`. | `BasicCharacterObject.cs:318` |
| `race` | string | no | `Race` is zeroed at `:323` before the read, so absent means race index 0 | Picks the skeleton, body meshes and head morphs, and through the `Monster` entry whose id equals the race name it also picks hit points (`MaxHitPoints()` at `:252-255`). Absent is the human convention in TAOM. | `BasicCharacterObject.cs:324` |
| `occupation` | string | no here | `IsSoldier` stays false | Read a second time by the base class as a case-insensitive substring test for "soldier". `IsSoldier` is what settlement scenes use to place a character as a guard. The strict enum parse is the campaign one, below. | `BasicCharacterObject.cs:329-333` |
| `is_hero` | bool | no | false | Marks a named individual rather than a stack of identical soldiers. A hero short-circuits `GetBattleTier()` to 7 at `:278-281`. Line troops leave it off. | `BasicCharacterObject.cs:334` |
| `face_mesh_cache` | bool | no | false | Not determined from the engine: it is stored to a public property at `:80` and nothing else in the shipping decompile reads it, so any effect is native side or editor only. No TAOM troop uses it. | `BasicCharacterObject.cs:335` |
| `is_obsolete` | bool | no | false | Retires a character without deleting it. The only consumer is `Hero.cs:1537`. This is the supported way to take a troop out of circulation while old saves still load. | `BasicCharacterObject.cs:336` |
| `skill_template` | ref, written `SkillSet.<id>` | no | a fresh empty skill set is created and named after this character's own id | Points at a shared skill block. If it resolves, the inline `<skills>` child is ignored outright. Zero TAOM troops use it, and the reason is under "Gotchas". | `BasicCharacterObject.cs:337` |
| `is_female` | bool | no | false, reset explicitly at `:477` | Female body, face range and animation set. | `BasicCharacterObject.cs:479` |
| `culture` | ref, written `Culture.<id>` | no, but a null culture breaks recruitment | null | Which people this troop belongs to. Drives recruitment, banner and colour, and the culture-level fallback pool. The dotted form is mandatory: a bare `gondor` throws `MBInvalidReferenceException`. | `BasicCharacterObject.cs:484` |
| `age` | int | no | `max(20, BodyPropertyMax.Age)` | Apparent age. Parsed with `Convert.ToInt32`, so `age="22.5"` throws. Fractional ages belong in `<BodyProperties age="22.55">`. | `BasicCharacterObject.cs:485` |
| `level` | int | no | 1 | The one number the whole ladder hangs off. Tier, wage, recruitment price and simulated battle power all derive from it. It also feeds `SkillFactor = min(level, 32) / 32` at `:74`, so a troop below level 32 never delivers its full listed skills. | `BasicCharacterObject.cs:487` |
| `default_group` | enum `FormationClass`, case-insensitive | no | 0, which is Infantry | The battle line, and the sole source of `IsRanged` and `IsMounted` (`:494-496`). Equipment does not affect either. A value that is not a formation name does not throw: `FetchDefaultFormationGroup` returns -1 at `:540` and the troop gets an invalid formation class. | `BasicCharacterObject.cs:489` |
| `formation_position_preference` | enum `Back` / `Middle` / `Front`, case-sensitive | no | `Middle` | Where in its own formation the troop stands. Parsed with a plain `Enum.Parse`, so a lowercase `front` throws and kills the file load. No TAOM troop sets it. | `BasicCharacterObject.cs:497` |
| `default_equipment_set` | string, a bare name | no | nothing happens | Overwrites equipment set 0 from a code-registered default equipment. Applied at `:521-525` with no null guard, so putting it on a character with no `<Equipments>` block is a hard crash. It is an engine hook, not TAOM content. | `BasicCharacterObject.cs:521` |

<!-- engine-table type="TaleWorlds.CampaignSystem.CharacterObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" method="Deserialize" -->

| Attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `occupation` | enum `Occupation` | no | `NotAssigned` | The campaign role. `Soldier` is a fieldable line troop, `Mercenary` a tavern hire, `Bandit` a hideout body. Parsed with `Enum.Parse`, so a typo throws and the file fails to load. `Mercenary`, `Gangster` and `CaravanGuard` are priced as mercenaries. | `CharacterObject.cs:539-542` |
| `is_template` | bool | no | false | Marks a blueprint the game copies to generate real characters (wanderers, education templates) rather than a spawnable troop. | `CharacterObject.cs:544` |
| `is_hidden_encyclopedia` | bool | no | false | Hides the entry from the in-game encyclopedia. | `CharacterObject.cs:546` |
| `voice` | ref by BARE id, no prefix | no | no persona; `GetPersona()` falls back to softspoken | The speaking personality. This one bypasses the dotted reference reader, so it takes `voice="earnest"`, never `Trait.earnest`. | `CharacterObject.cs:572-575` |
| `is_basic_troop` | bool | no | false, forced by an explicit else at `:583-585` | Marks the entry point of a recruitment tree, the rung a settlement actually offers. 86 of TAOM's 857 troops carry it. <!-- measured: python ElementTree scan of troops/troops_*.xml 2026-09-05 --> | `CharacterObject.cs:577` |
| `upgrade_requires` | ref, written `ItemCategory.<id>` | no | the upgrade needs no item | Forces a spare item of that category in the party inventory before the upgrade is allowed, and consumes it. This is how "you need a horse to promote a footman to cavalry" works. It is read off the TARGET, not the source. Zero TAOM troops use it. | `CharacterObject.cs:586` |
| `level` | int | no | 1 | Read a second time here with identical meaning and the identical default, so the duplication is harmless. | `CharacterObject.cs:587-588` |
| `civilianTemplate` | ref to another `CharacterObject`, deprecated | no | nothing | Legacy way of borrowing another character's civilian outfit. Present fires an engine assert telling you to stop. Use a civilian `<EquipmentSet>` instead. | `CharacterObject.cs:589-593` |
| `battleTemplate` | ref to another `CharacterObject`, deprecated | no | nothing | The same, for battle kit, with the same assert. | `CharacterObject.cs:594-598` |

**Tier is not an attribute.** `CharacterObject.Tier` (`CharacterObject.cs:361`) asks the campaign's character-stats model, and the vanilla model is `clamp(ceil((level - 5) / 5), 0, MaxCharacterTier)` at `DefaultCharacterStatsModel.cs:18-25`. Vanilla caps that at 6 (`DefaultCharacterStatsModel.cs:11`); TAOM raises it to 10 in [`Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs`](../../Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs) line 23, registered at `Main/SubModule.cs:855`. The formula itself is untouched, so the ladder is:

<!-- engine-ref type="TaleWorlds.CampaignSystem.GameComponents.DefaultCharacterStatsModel" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem.GameComponents/DefaultCharacterStatsModel.cs" lines="18-25" -->

| Tier | Levels | Tier | Levels |
|---|---|---|---|
| T0 | 1 to 5 | T6 | 31 to 35 |
| T1 | 6 to 10 | T7 | 36 to 40 |
| T2 | 11 to 15 | T8 | 41 to 45 |
| T3 | 16 to 20 | T9 | 46 to 50 |
| T4 | 21 to 25 | T10 | 51 and up |
| T5 | 26 to 30 | | |

Two consequences worth holding on to. A hero reports Tier 0 from this model, because `GetTier` returns 0 for `IsHero` before it does any arithmetic; the base class has a separate `GetBattleTier()` at `BasicCharacterObject.cs:278-285` that returns 7 for a hero and caps at 7 for everyone else, so the two numbers are not the same thing. And level 30 and level 31 are different tiers, which is why TAOM authors troops on the `6 + 5n` grid: the levels actually in use across the 16 files are 1, 6, 7, 11, 16, 21, 26, 31, 36, 41, 46 and 51, and the only off-grid one is `morannon_recruit` at level 7. <!-- measured: python ElementTree scan of troops/troops_*.xml for distinct level values 2026-09-05 -->

## Child elements

<!-- engine-table type="TaleWorlds.Core.BasicCharacterObject" file="Core/TaleWorlds.Core/TaleWorlds.Core/BasicCharacterObject.cs" method="Deserialize" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Skills>` `<skills>` | container of skill rows | no | no skills, everything reads 0 | Either capitalisation is accepted. Read only when `skill_template` did not resolve. | `BasicCharacterObject.cs:353-357` |
| `<Equipments>` `<equipments>` | container | no | the troop falls back to the empty roster and spawns in its underwear | Holds three different kinds of child in any order. The deserializer makes one pass collecting bare `<equipment>` rows, a second pass processing rosters and set references, then applies the overrides last. | `BasicCharacterObject.cs:360-413` |
| `<EquipmentRoster>` `<equipmentRoster>` | inline outfit | no | | One complete kit. Repeatable, and every extra one is another variant. Routed through `MBEquipmentRoster.Init` at `MBEquipmentRoster.cs:44-54`, which asserts unless the node is spelled with the capital R. This is how TAOM troops are authored: 2,516 inline rosters across the 16 files. <!-- measured: python ElementTree scan of troops/troops_*.xml 2026-09-05 --> | `BasicCharacterObject.cs:370-377` |
| `<EquipmentSet>` `<equipmentSet>` `EquipmentSet@id` | reference to a shared roster | `id` is required | | Pulls in a roster defined in an equipment-set file. The `id` is a BARE roster id, not the dotted form, and it resolves through a plain lookup that creates no placeholder, so the roster file must load BEFORE this troop file. A miss is a hard `NullReferenceException` at `MBEquipmentRoster.cs:112`. | `BasicCharacterObject.cs:382-408` |
| `<equipment>` | one slot | no | | Directly under `<Equipments>` it is an override stamped into EVERY set, after cloning them so shared rosters are not corrupted. Inside an `<EquipmentRoster>` it is one slot of that outfit. An `<Equipments>` holding only bare override rows and no roster crashes at `:412`. | `BasicCharacterObject.cs:363-369, 410-413` |
| `equipmentType` | enum `Battle` / `Civilian` / `Stealth`, case-sensitive | no | `Battle` | Decides when the outfit is worn. Parsed with `Enum.TryParse` without `ignoreCase`, so a lowercase `civilian` does not throw, it asserts and silently stays Battle. | `BasicCharacterObject.cs:388-393` |
| `civilian` | bool | no | | The older spelling of the same thing. On an `<EquipmentSet>` reference it fires a deprecation assert at `:397`. On an inline `<EquipmentRoster>` it takes a different code path, `MBEquipmentRoster.cs:88-106`, which accepts it with no assert at all. That is why TAOM's 129 inline civilian rosters are written `civilian="true"` while its 560 civilian set references are written `equipmentType="Civilian"`. <!-- measured: python ElementTree scan of troops/troops_*.xml 2026-09-05 --> | `BasicCharacterObject.cs:395-398` |
| `<face>` | container | no | an empty body-property range is registered | Lowercase only, there is no capitalised alias. | `BasicCharacterObject.cs:415` |
| `<face_key_template>` `face_key_template@value` | ref, written `BodyProperty.<id>` | no | | The shared face definition. It overwrites the whole body-property range, so if you supply this AND inline `<BodyProperties>`, the template wins and the inline values are discarded. Unlike `<EquipmentSet>` this reference is forward-safe. All 857 TAOM troops carry one. <!-- measured: python ElementTree scan of troops/troops_*.xml 2026-09-05 --> | `BasicCharacterObject.cs:455-458` |
| `<BodyProperties>` | age, weight, build, key | no | | The minimum end of the face range. `key` is exactly 128 hex characters; any other length makes the parse fail. | `BasicCharacterObject.cs:440-444` |
| `<BodyPropertiesMax>` | the same attributes | no | | The maximum end, so a hundred identical troops get a hundred slightly different faces. If this block fails to parse the engine overwrites the min with the empty max at `:449-453`, which destroys both. | `BasicCharacterObject.cs:447-453` |
| `<hair_tags>` `<beard_tags>` `<tattoo_tags>` | lists of rows carrying `name` | no | the face template's own tags are kept | Restrict which hair, beard and tattoo meshes the randomiser may pick. If any list is non-empty the shared body property is cloned first at `:506`, so you cannot corrupt a template other troops share. | `BasicCharacterObject.cs:419-437, 499-519` |
| `<Resistances>` `<resistances>` | element with three float attributes | no | | Either capitalisation. | `BasicCharacterObject.cs:462` |
| `knockback` `knockdown` `dismount` | float, a percentage | no | 25, 50, 50 | How hard the character is to shove, to knock flat and to pull off a mount. Each is multiplied by 0.01 and clamped to 0 to 1, so 100 means immune. Only the multiplayer stat model was found reading these three off the character, so treat the campaign effect as unproven. No TAOM troop sets them. | `BasicCharacterObject.cs:464-469` |

<!-- engine-table type="TaleWorlds.CampaignSystem.CharacterObject" file="Campaign/TaleWorlds.CampaignSystem/TaleWorlds.CampaignSystem/CharacterObject.cs" method="Deserialize" -->

| Element or attribute | Type | Required | Default when absent | What it does | Read at (file:line) |
|---|---|---|---|---|---|
| `<Traits>` | container of trait rows | no | no traits | The one child element with a case-sensitive name: `<traits>` lowercase is silently ignored. Appends key by key, it never clears first. `is_mariner` is derived here rather than authored: `<Trait id="NavalSoldier" value="1"/>` is what marks a naval troop (`CharacterObject.cs:599`). | `CharacterObject.cs:551` |
| `<upgrade_targets>` | container | no | an empty array, a dead-end troop | Replaces rather than appends: the list is rebuilt on every deserialize. Order matters, index 0 is the first upgrade choice the player is shown, and the XP and gold costs are addressed by position. | `CharacterObject.cs:557-571` |
| `<upgrade_target>` `upgrade_target@id` | ref, written `NPCCharacter.<troopid>` | `id` required | | One outgoing upgrade edge. Forward and cross-file references are fine, the object manager creates a placeholder and back-fills it. A bare undotted id throws. Child nodes not named exactly `upgrade_target` are skipped. | `CharacterObject.cs:563-566` |

Both `<skill>` rows and `<Trait>` rows are read by the same two-attribute loop, `PropertyOwner<T>.Deserialize`, so they behave identically:

<!-- engine-ref type="TaleWorlds.Core.PropertyOwner" file="Core/TaleWorlds.Core/TaleWorlds.Core/PropertyOwner.cs" lines="70-88" -->

| Attribute on a row | Type | Required | What it does | Read at (file:line) |
|---|---|---|---|---|
| `id` | bare skill or trait id, no prefix | yes, dereferenced with no null check, so a missing one throws | Which skill or trait the row sets. An UNKNOWN id is silently dropped, not an error, so a typo costs nothing and does nothing. | `PropertyOwner.cs:77` |
| `value` | int | yes, dereferenced with no null check | The number. `value="0"` REMOVES the entry rather than storing a zero. | `PropertyOwner.cs:78` |

The row's own element name is ignored, only those two attributes are read, and XML comments inside the block are skipped safely.

Every `<equipment>` row, wherever it sits, carries two required attributes and both are dereferenced with no null check at `Equipment.cs:209-212`. `id` is written `Item.<item_id>`; the value is split on the first dot and the part after it is kept, so a bare `id="gondor_sword_t3"` also works. It resolves through a plain lookup and a miss does NOT error. `slot` is one of these twelve names, mapped by a literal switch; anything the switch does not name is handed to `Enum.Parse` as written and throws if it is not a real slot:

<!-- engine-ref type="TaleWorlds.Core.Equipment" file="Core/TaleWorlds.Core/TaleWorlds.Core/Equipment.cs" lines="225-236" -->

| Written in the XML | Engine slot | Index | Written in the XML | Engine slot | Index |
|---|---|---|---|---|---|
| `Item0` | Weapon0 | 0 | `Head` | Head | 5 |
| `Item1` | Weapon1 | 1 | `Body` | Body | 6 |
| `Item2` | Weapon2 | 2 | `Leg` | Leg | 7 |
| `Item3` | Weapon3 | 3 | `Gloves` | Gloves | 8 |
| `Item4` | ExtraWeaponSlot | 4 | `Cape` | Cape | 9 |
| | | | `Horse` | Horse | 10 |
| | | | `HorseHarness` | HorseHarness | 11 |

The raw enum spellings `Weapon0` to `Weapon3` and `ExtraWeaponSlot` work too, because anything the switch leaves alone is parsed verbatim. TAOM's troop files use the `Item0` to `Item3` form throughout and never use `Item4`.

## Worked example

`gondor_ithilien_ranger` is the shape to copy: a basic-troop entry point, one face template, eight inline battle rosters that are slot-identical, an empty `<upgrade_targets />`, and one shared civilian set pulled in by reference.

<!-- example file="Main/_Module/ModuleData/troops/troops_gondor.xml" id="gondor_ithilien_ranger" -->
```xml
  <NPCCharacter
      id="gondor_ithilien_ranger"
      default_group="Ranged"
      level="51"
      name="{=aom_gondor_ithilien_ranger_name}[Gondor] Ithilien Ranger"
      occupation="Soldier"
      culture="Culture.gondor"
      is_basic_troop="true">
    <face>
      <face_key_template value="BodyProperty.fighter_gondor" />
    </face>
    <skills>
      <skill id="Athletics" value="185" />
      <skill id="Riding" value="45" />
      <skill id="OneHanded" value="270" />
      <skill id="TwoHanded" value="175" />
      <skill id="Polearm" value="175" />
      <skill id="Bow" value="320" />
      <skill id="Crossbow" value="65" />
      <skill id="Throwing" value="70" />
    </skills>
    <upgrade_targets />
    <Equipments>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow_c" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v1_a" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v1_a" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood" />
        <equipment slot="Body" id="Item.ithilien_jerkin_long" />
        <equipment slot="Cape" id="Item.ithilien_cloak" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow_b" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v2_a" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v2_a" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood_var" />
        <equipment slot="Body" id="Item.ithilien_jerkin_long" />
        <equipment slot="Cape" id="Item.ithilien_cloak_var" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots_heavy" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow_c" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v3_a" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v3_a" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood_masked" />
        <equipment slot="Body" id="Item.ithilien_jerkin_long_var" />
        <equipment slot="Cape" id="Item.ithilien_cloak" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow_b" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v4_a" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v4_a" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood_masked_var" />
        <equipment slot="Body" id="Item.ithilien_jerkin_long_var" />
        <equipment slot="Cape" id="Item.ithilien_cloak_var" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots_heavy" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v1_b" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v1_b" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood" />
        <equipment slot="Body" id="Item.ithilien_jerkin_short" />
        <equipment slot="Cape" id="Item.ithilien_cloak_var" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow_c" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v1_c" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v1_c" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood_var" />
        <equipment slot="Body" id="Item.ithilien_jerkin_short" />
        <equipment slot="Cape" id="Item.ithilien_cloak" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots_heavy" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow_b" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v1_d" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v1_d" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood_masked" />
        <equipment slot="Body" id="Item.ithilien_jerkin_short_var" />
        <equipment slot="Cape" id="Item.ithilien_cloak_var" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots" />
      </EquipmentRoster>
      <EquipmentRoster>
        <equipment slot="Item0" id="Item.wm_ithilien_bow" />
        <equipment slot="Item1" id="Item.wm_elven_arrow_v2_b" />
        <equipment slot="Item2" id="Item.wm_elven_arrow_v2_b" />
        <equipment slot="Item3" id="Item.wm_gondor_sword_a10" />
        <equipment slot="Head" id="Item.ithilien_hood_masked_var" />
        <equipment slot="Body" id="Item.ithilien_jerkin_short_var" />
        <equipment slot="Cape" id="Item.ithilien_cloak" />
        <equipment slot="Gloves" id="Item.ithilien_bracers" />
        <equipment slot="Leg" id="Item.ithilien_boots_heavy" />
      </EquipmentRoster>
      <EquipmentSet id="battania_troop_civilian_template_t2" equipmentType="Civilian" />
    </Equipments>
  </NPCCharacter>
```

The three things a reader changes first:

1. **`level="51"`.** This is the whole ladder position. 51 is T10 under TAOM's raised cap, which sets the wage (30 gold a day before multipliers), the recruitment price and the simulated battle power. Change this and you have changed the troop's tier, not just a number on a screen.
2. **The eight `<skill>` rows.** They are not free-hand numbers. Every one of them is `GROUP_BASELINES['Ranged'][51]` plus `CULTURAL_MODS['gondor']` out of [`tools/rebalance_troops.py`](../../tools/rebalance_troops.py), and the file matches the formula exactly, row for row. <!-- measured: python import of tools/rebalance_troops.py compared against the on-disk skills block 2026-09-05 -->
3. **The eight `<EquipmentRoster>` blocks.** Read them as a column, not as eight outfits. Slot `Item0` holds a bow in all eight, `Item1` and `Item2` hold the same arrow in all eight, `Item3` holds the same sword in all eight, and Head, Body, Cape, Gloves and Leg are filled in all eight. That is a requirement, not a coincidence, and the "Gotchas" section says why.

## Recipes: Add / Modify / Delete

### Add

Adding a rung means one XML entry plus five downstream places that will not tell you they are missing.

1. **Pick the level, not the tier.** Stay on the `6 + 5n` grid (6, 11, 16, 21, 26, 31, 36, 41, 46, 51) so the troop lands cleanly in a tier and so `rebalance_troops.py` has a baseline row for it. An off-grid level is skipped by the formula entirely.
2. **Write the entry** in `Main/_Module/ModuleData/troops/troops_<culture>.xml`. Copy the nearest existing rung of the same `default_group` and edit it. Keep the id convention `{culture_prefix}_{origin}_{role}` from [`.claude/rules/troops.md`](../../.claude/rules/troops.md) lines 55 to 62. Set `culture="Culture.<id>"`, `occupation="Soldier"`, a `race=` only for non-human lines (leave it off for humans), and one `<face_key_template value="BodyProperty.<id>"/>`.
3. **Give it the eight-skill block.** Every TAOM troop carries the same eight rows: Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow and Throwing. Take the numbers from `GROUP_BASELINES[<group>][<level>]` plus `CULTURAL_MODS[<culture>]` in `tools/rebalance_troops.py` rather than inventing them.
4. **Attach the parent edge.** Add `<upgrade_target id="NPCCharacter.<new_id>"/>` to the rung below it. A rung with no incoming edge and no pool entry is fielded by AI lords and unobtainable by the player.
5. **Write the rosters slot for slot.** If one battle set fills a slot, every battle set fills that slot with something of the same kind. Do not add a ninth roster that drops the arrows or the cape.
6. **Add it to the party templates** in [`Main/_Module/ModuleData/taom_partyTemplates.xml`](../../Main/_Module/ModuleData/taom_partyTemplates.xml), to every template of that culture it belongs in, or lord armies will never field it.
7. **If it is tier 6 or above**, add a `<TroopWeight id="..." weight="..."/>` row to [`Main/_Module/ModuleData/TroopWeights/troop_weights.xml`](../../Main/_Module/ModuleData/TroopWeights/troop_weights.xml), per [`docs/features/troop-tree-revamp.md`](../features/troop-tree-revamp.md) lines 158 to 165. If it is an elite that should cost a special resource, add a `<Troop .../>` row to [`Main/_Module/ModuleData/special_resources/troop_resource_costs.xml`](../../Main/_Module/ModuleData/special_resources/troop_resource_costs.xml).
8. **Make it recruitable.** Either put its id in a recruitment pool, or make sure a pool root upgrades into it. Pools live in `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`, except Gondor, which can be edited as data in [`Main/_Module/ModuleData/recruitment_pools/gondor.json`](../../Main/_Module/ModuleData/recruitment_pools/gondor.json). A clan or settlement pool REPLACES the culture pool rather than merging with it, so re-list the ordinary recruits when you add to one.
9. **Name it** with the `{=key}English fallback` form. All 857 troop names carry a key, none of them is registered in a strings file, and none is translated, so the inline fallback is the English text. See [strings-and-localization](strings-and-localization.md).

Check: `python tools/validate_moduledata.py` then `python tools/validate_all_troop_refs.py` then `dotnet test TAOM.Tests --filter AllNonMilitiaNonBossTroops_AreReachable`
Takes effect: full game restart
Code: Code changes required in `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`, unless the culture is Gondor or the new rung is reached purely by upgrading an existing pool root

### Modify

**Restatting a troop.** Changing `level=` is a restat, not a relabel: it moves the tier, the wage, the recruitment price and the auto-resolve power at once, and it can break the ladder above and below the troop.

1. Edit `level=` and the eight `<skill>` values together. A level change with the old skills leaves the troop off the curve.
2. Re-check the edges into and out of it. No skill on an upgrade target may sit below its source, and the target's tier must be strictly higher than the source's, or the upgrade costs 0 XP and the party screen crashes on hover.
3. Re-check the armour ladder as well. A target must not total less armour than its source across Head, Body, Cape, Gloves and Leg, averaged over its battle sets.
4. If the troop drops below tier 2 it stops being recruitable from prisoners: `DefaultPrisonerRecruitmentCalculationModel.cs:79` refuses anything with `Tier < 2`.
5. Tier shifts are save-safe. Moving a troop from T6 to T5 works across an existing save as long as you re-pick its skills, armour and equipment to match, per [`.claude/rules/troops.md`](../../.claude/rules/troops.md) line 153.

Check: `python tools/rebalance_troops.py --fix-monotonicity --dry-run` then `python tools/analyze_troop_balance.py --stdout` then `python tools/validate_moduledata.py`
Takes effect: next save load
Code: No code changes needed

**Adding or changing an equipment roster.** Each extra `<EquipmentRoster>` is one more visual variant of the same soldier, and the engine mixes them per slot.

1. Copy an existing roster of the same troop and change only the items you mean to change.
2. Fill every slot the other rosters fill. If the others carry a cape, this one carries a cape.
3. Keep the weapon slot indices fixed: a bow always at the same index, its arrows always at the same index, the sidearm always at the same index, in every set.
4. Keep `Horse` and `HorseHarness` paired, in every set, for any troop grouped Cavalry or HorseArcher.
5. Verify every item id exists before you save. A typo is not an error, it is a silently empty slot.

Takes the stricter answer on timing because this is the recipe where new item XML files usually arrive with the edit, and a new item file is only registered at process launch.

Check: `python tools/validate_all_troop_refs.py` then `python tools/audit_polearm_shield_parity.py` then `python tools/fix_upgrade_armour_regressions.py`
Takes effect: full game restart
Code: No code changes needed

### Delete

Do not delete a troop. An id is stored in saves and referenced from party templates, upgrade edges, culture bindings, weight and cost tables and the recruitment pools, so removing the entry breaks every one of them at once.

1. **Set `is_obsolete="true"`** on the entry and leave the entry, the id, the skills and the rosters exactly where they are.
2. **Remove the incoming edges.** Delete every `<upgrade_target id="NPCCharacter.<id>"/>` that points at it, so nothing upgrades into it any more.
3. **Purge the downstream tables.** Every `PartyTemplateStack` row in `taom_partyTemplates.xml` naming it, its `<TroopWeight>` row, and its `<Troop>` row in `troop_resource_costs.xml`.
4. **Purge the pools.** Every mention of the id in `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs` and in `recruitment_pools/gondor.json`, plus any settlement guard, emissary or notable reference in `Main/_Module/ModuleData/characters/npcs_<culture>.xml`.
5. **Re-run the reachability test.** Removing an edge can orphan everything downstream of the retired rung.

`tools/cleanup_deleted_troops_212.py` is the worked pattern for steps 3 and 4 when the retirement is large enough to be worth scripting.

Check: `python tools/validate_moduledata.py` then `dotnet test TAOM.Tests --filter AllNonMilitiaNonBossTroops_AreReachable`
Takes effect: next save load
Code: Code changes required in `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs` whenever the retired id appears in a pool

## Gotchas: what fails silently and what crashes

- **A troop does not wear one of its equipment sets. The engine builds the kit slot by slot from independently chosen sets.** `Equipment.GetRandomEquipmentElements` loops the 12 slots and, whenever the seed is a real number, re-rolls the set index at the top of every iteration. A campaign battle always supplies a real seed. So eight rosters are not eight outfits, they are a menu the engine orders one item from per slot. `Equipment.cs:549-593` and [`.claude/rules/troops.md`](../../.claude/rules/troops.md) lines 24 to 53.
- **You cannot check that in game by looking at the troop.** The encyclopedia, party screen, troop tree and tournament all use whole-set selection and render set 1, so a broken troop looks correct in every UI surface. Check it in the data, or in a battle. [`.claude/rules/troops.md`](../../.claude/rules/troops.md) lines 51 to 53.
- **A misspelled or not-yet-loaded item id is not an error, it is a naked slot.** `GetObject<ItemObject>` returns null, `IsItemFitsToSlot(slot, null)` returns true by design, and the slot is filled with an empty element. This is the underwear bug, and only an external tool catches it. `Equipment.cs:209-213` and `Equipment.cs:445-451`.
- **An item in the wrong slot behaves the same way.** The type check fires a failed assert and leaves the slot empty rather than throwing. `Equipment.cs:214-221`.
- **`race=` is the one attribute that hard-crashes on a typo.** `FaceGen.GetRaceOrDefault` is a raw dictionary index despite the name, so an unknown race string throws `KeyNotFoundException` rather than falling back to human. `BasicCharacterObject.cs:324-328`.
- **`occupation` and `formation_position_preference` also throw on a typo**, both through `Enum.Parse`. `default_group` does not: it fails soft to -1 and quietly breaks `IsMounted` and `IsRanged` everywhere downstream. `CharacterObject.cs:542`, `BasicCharacterObject.cs:498` and `BasicCharacterObject.cs:534-541`.
- **`IsMounted` and `IsRanged` come from `default_group` alone, never from equipment.** A Cavalry-grouped troop that rolls a mountless roster walks while the AI treats it as cavalry, and an archer left at `default_group="Infantry"` is waged, priced and commanded as infantry. `BasicCharacterObject.cs:494-496`, with the failure shape written up in [`docs/features/black-numenorean.md`](../features/black-numenorean.md) lines 313 to 324.
- **`skill_template` beats an inline `<skills>` block outright**, and the engine says nothing. Until 2026-08-31 that described 44 TAOM militia troops pointing at vanilla Calradian skill sets: `rivendell_militia_spearman` was authored at 850 total and delivered 215. No TAOM troop file uses the attribute today. `BasicCharacterObject.cs:353-357` and [`docs/features/troop-skill-balance.md`](../features/troop-skill-balance.md) lines 145 to 158.
- **An upgrade edge whose target does not reach a higher tier costs 0 XP**, because the vanilla cost loop runs from `source.Tier + 1` to `target.Tier` and exits immediately. The party screen then evaluates `Xp % cost` unguarded, which is a crash on hover. Gate: `UPGRADE_TIER_COLLAPSE` in [`.claude/rules/moduledata-validation.md`](../../.claude/rules/moduledata-validation.md).
- **`upgrade_requires` is checked on the target, not the source.** To make "promoting to X needs a horse", put it on X. `CharacterObject.cs:586` and `DefaultPartyTroopUpgradeModel.cs:109`.
- **A `_merc` copy must never carry `<upgrade_targets>`**, and its `occupation` must be `Mercenary`, not `Soldier`. The tavern NPC spawns either way but only talks if the offer is a mercenary, so a `Soldier` offer puts a silent, unhireable NPC in the tavern. [`docs/features/tavern-mercenaries.md`](../features/tavern-mercenaries.md) lines 134 to 146 and 188 to 199.
- **Six of the 16 troop files are never swept by the item-reference gate.** `tools/validate_all_troop_refs.py` hardcodes ten cultures at lines 80 to 97 (gondor, mordor, isengard, dolguldur, gundabad, erebor, rhun_new, dale, lindon, umbar) and the file's own comment names the six it skips: dunland, goblin, harad, mirkwood, rivendell and rohan. A new culture must be appended to that list or its troop file is never checked for broken item refs. Any doc claiming the tool covers "all 7 culture troop XMLs" is wrong on both numbers.
- **Level is not the only input to auto-resolve.** `CharacterObject.GetPower()` at `:603-606` passes both `Tier` and `IsMounted` into `GetPowerImp`, which is `(2 + tier) * (8 + tier) * 0.02` times 1.5 for a hero, 1.2 for a mounted troop and 1 otherwise (`:856-859`). Giving a rung a horse changes its simulated battle strength, and so does its formation group.
- **Names do not imply tier.** Vanilla `lowland_yew_bow` out-stats `lowland_longbow` despite reading as the lesser weapon. Grep the numbers in `SandBoxCore/ModuleData/items/weapons.xml` before assigning tier-ordered picks. This file lives in the game install, not the repo; a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. [`.claude/rules/troops.md`](../../.claude/rules/troops.md) lines 186 to 197.
- **Items come from two trees, not one.** TAOM's own armour and weapons live under `LOTRLOME_Armory/ModuleData/LOTRLOME_items/<culture>/`, but TAOM troops also name vanilla `SandBoxCore` items directly, and TAOM ships no items XSLT, so vanilla item stats currently have no override mechanism in this repo. See [items-armor](items-armor.md) and [items-weapons-and-crafting](items-weapons-and-crafting.md).

### Questions this chapter cannot answer

- **What happens when `race=` and the `<face_key_template>` disagree**, for example `race="dwarf"` with `BodyProperty.fighter_gondor`. No TAOM doc settles it, and the face path bottoms out in native code. Start from [`Main/_Module/ModuleData/TAOM_bodyproperties.xml`](../../Main/_Module/ModuleData/TAOM_bodyproperties.xml), which carries no race attribute on `<BodyProperty>`, and [`docs/features/hero-race.md`](../features/hero-race.md).
- **Which integer a given race name resolves to.** The table is built at runtime from the merged `skins.xml` list, so the index is a merge-order position and inserting a race could renumber the ones after it. The authoring comment above the `sauron` block in `LOTRLOME_Armory/ModuleData/skins.xml` records exactly that. It is not verifiable from managed code, so treat "index 0 is human" as strong inference, per [`docs/features/black-numenorean.md`](../features/black-numenorean.md) lines 162 to 176.
- **Whether the `<Resistances>` numbers do anything in a campaign.** Only the multiplayer stat model was found reading them off the character. Nothing in TAOM sets them, so nothing has ever tested it.
- **What `default_equipment_set` may legally name.** The pool it draws from is filled in code and was not traced. No TAOM content uses it.

## Numbers in this chapter

Every count below was produced on 2026-09-05 by the command beside it, run from the repo root.

| Number | Command |
|---|---|
| 16 troop files, plus 3 non-`.xml` backup sidecars | `ls Main/_Module/ModuleData/troops/` |
| 857 `<NPCCharacter>` entries in total. Per file: gondor 189, rhun_new 117, erebor 66, mordor 62, rohan 57, isengard 52, dolguldur 50, dunland 46, dale 35, harad 33, gundabad 30, lindon 30, rivendell 30, goblin 25, mirkwood 19, umbar 16 | a python regex count of `<NPCCharacter` per file over `Main/_Module/ModuleData/troops/troops_*.xml` |
| 44 `<XmlName id="NPCCharacters">` rows in SubModule.xml, 16 of them troop files | `grep -c 'id="NPCCharacters"' Main/_Module/SubModule.xml` and `grep -n "troops/" Main/_Module/SubModule.xml` |
| `race=` absent on 520 troops; the ten values in use are elf 79, dwarf 66, orc 50, uruk_hai 37, goblin 32, pale_uruk 30, dg_uruk 21, uruk 19, berserker 2, cave_troll 1 | a python ElementTree scan of the 16 files, counting the `race` attribute |
| `occupation`: Soldier 827, Mercenary 22, Bandit 8 | the same scan, counting the `occupation` attribute |
| `default_group`: Infantry 486, Ranged 205, Cavalry 143, HorseArcher 23 | the same scan, counting the `default_group` attribute |
| `formation_position_preference` set on 0 troops; `skill_template` on 0; `upgrade_requires` on 0; `<Resistances>` on 0; `is_hero="true"` on 0; `is_obsolete="true"` on 0 | the same scan |
| `is_basic_troop="true"` on 86 troops | the same scan |
| All 857 troops carry an inline `<skills>` block of exactly 8 rows, and the 8 ids are always Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing | the same scan, as a histogram of skill-row counts per troop |
| All 857 troops carry a `<face_key_template>`, and all 857 names carry a `{=key}` prefix | the same scan |
| 684 upgrade edges, 234 empty `<upgrade_targets />`, 60 troops with no `<upgrade_targets>` element at all | the same scan |
| Tier spread: T0 5, T1 35, T2 90, T3 125, T4 140, T5 149, T6 123, T7 90, T8 55, T9 29, T10 16 | the same scan, applying `clamp(ceil((level - 5) / 5), 0, 10)` to each `level=` |
| Levels in use: 1, 6, 7, 11, 16, 21, 26, 31, 36, 41, 46, 51, with `morannon_recruit` the only troop at 7 | the same scan |
| 2,516 inline `<EquipmentRoster>` blocks, of which 129 carry `civilian="true"`; 560 `<EquipmentSet ... equipmentType="Civilian"/>` references | the same scan, grouping roster and set elements by their type attribute |
| 18,973 `<equipment>` rows naming 2,557 distinct item ids; 11 of the 12 slots are used and `Item4` is used 0 times | the same scan, over every `<equipment>` element |
| 105 `<TroopWeight>` rows; 77 `<Troop>` rows in `troop_resource_costs.xml`; 383 `<MBPartyTemplate>` holding 3,295 `<PartyTemplateStack>` rows | ElementTree child counts on the three files |
| `validate_all_troop_refs.py` sweeps 10 cultures and skips 6 | `sed -n '76,100p' tools/validate_all_troop_refs.py` |
| The worked example's eight skill values equal `GROUP_BASELINES['Ranged'][51]` plus `CULTURAL_MODS['gondor']`, row for row | a python import of `tools/rebalance_troops.py` compared against the on-disk block |

Three numbers are quoted from source files rather than counted. The wage table (T0 1, T1 2, T2 3, T3 5, T4 8, T5 12, T6 15, T7 18, T8 20, T9 25, T10 30, above that 57) and the recruit-cost brackets come from `Main/Features/TroopProgression/TroopCostService.cs` lines 9 to 60; `MaxCharacterTier => 10` from `Main/Features/TroopProgression/Models/TaomCharacterStatsModel.cs` line 23; and the 96 `INCONSISTENT_ARMOUR_SLOT` warnings across 10 cultures from [`docs/features/moduledata-validation.md`](../features/moduledata-validation.md) lines 320 to 326.

## Read next

- [`.claude/rules/troops.md`](../../.claude/rules/troops.md), the per-slot mixing rule and the cross-file checklist.
- [`docs/features/troop-skill-balance.md`](../features/troop-skill-balance.md), the skill formula, the monotonicity clamp and the armour ladder.
- [`docs/features/troop-tree-revamp.md`](../features/troop-tree-revamp.md), what a whole-tree rewrite touches.
- [`docs/features/volunteer-recruitment.md`](../features/volunteer-recruitment.md), the pool priority order and the reachability invariant.
- [`docs/features/gondor-ithilien-ranger.md`](../features/gondor-ithilien-ranger.md), the worked example's own feature doc.
- [`docs/features/tavern-mercenaries.md`](../features/tavern-mercenaries.md), the `_merc` leaf-copy rule.
- [`docs/features/black-numenorean.md`](../features/black-numenorean.md), a hand-authored line and its traps.
- [`docs/features/moduledata-validation.md`](../features/moduledata-validation.md), what each gate proves and what it does not.
- [equipment-rosters](equipment-rosters.md), [party-templates](party-templates.md), [cultures](cultures.md), [balance-levers](balance-levers.md) and [validation-and-testing](validation-and-testing.md).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/modding/balance-levers.md](./balance-levers.md)
- [docs/modding/body-properties.md](./body-properties.md)
- [docs/modding/configs-factions-and-world.md](./configs-factions-and-world.md)
- [docs/modding/cultures.md](./cultures.md)
- [docs/modding/editing-safely.md](./editing-safely.md)
- [docs/modding/equipment-rosters.md](./equipment-rosters.md)
- [docs/modding/file-catalogue.md](./file-catalogue.md)
- [docs/modding/items-armor.md](./items-armor.md)
- [docs/modding/items-mounts-and-harness.md](./items-mounts-and-harness.md)
- [docs/modding/load-order-and-dependencies.md](./load-order-and-dependencies.md)
- [docs/modding/lords-and-heroes.md](./lords-and-heroes.md)
- [docs/modding/modules-overview.md](./modules-overview.md)
- [docs/modding/npcs-notables-and-townsfolk.md](./npcs-notables-and-townsfolk.md)
- [docs/modding/party-templates.md](./party-templates.md)
- [docs/modding/README.md](./README.md)
- [docs/modding/recipe-add-a-culture.md](./recipe-add-a-culture.md)
- [docs/modding/recipe-add-a-race-or-creature.md](./recipe-add-a-race-or-creature.md)
- [docs/modding/recipe-new-mod-from-zero.md](./recipe-new-mod-from-zero.md)
- [docs/modding/recipe-retire-content.md](./recipe-retire-content.md)
- [docs/modding/skill-sets.md](./skill-sets.md)
- [docs/modding/strings-and-localization.md](./strings-and-localization.md)
- [docs/modding/troubleshooting.md](./troubleshooting.md)
- [docs/modding/wanderers-and-named-companions.md](./wanderers-and-named-companions.md)

<!-- backlinks-end -->
