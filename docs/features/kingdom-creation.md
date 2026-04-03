# Kingdom Creation Guide

## Overview

TAOM supports two kinds of kingdoms: XSLT kingdoms that wrap vanilla Bannerlord factions (e.g., Harwan wraps `Kingdom.aserai`), and custom kingdoms that are fully independent XML-defined factions (e.g., Umbar, Shaghana, Gondor, Mordor). This guide covers **custom kingdoms only** — the common path for adding new Middle-earth content. A new custom kingdom requires changes to 13 files. Skipping any one of them will cause a crash or missing content at runtime.

## Why This Exists

Vanilla Bannerlord ships with 8 kingdoms (empire, aserai, vlandia, battania, sturgia, khuzait, and two empire splits). TAOM represents ~30 distinct Middle-earth factions. Some can be mapped to vanilla kingdoms through XSLT passthrough, but many require fully independent definitions — their own culture, troop trees, clans, lords, and notables — because they have no vanilla analog with compatible attributes.

Without a complete kingdom definition:
- New games crash with NREs during `LordNeedsHorsesIssueBehavior.ConditionsHold`
- Lords appear with null `Culture` and `CharacterObject`, breaking encyclopedia entries
- Settlements spawn without valid owners, causing map pop-up crashes
- Wanderers never appear in settlements (wrong `culture` attribute on NPCCharacter)

## Architecture

Custom kingdoms are pure data — no C# code is involved. The Bannerlord engine loads the following XML and JSON files on startup via `SubModule.xml` registrations and the module data directory. The load order matters: strings must exist before the XML that references them; cultures must exist before kingdoms; clans must exist before lords.

### Data Flow

```
taom_module_strings.xml       (string IDs resolved first)
         |
taom_spcultures.xml           (Culture object registered)
         |
TAOM_spkingdoms.xml           (Kingdom object registered, references Culture)
         |
characters/clans.xml          (Faction objects registered, reference Kingdom + Culture)
         |
characters/lords.xml          (NPCCharacter objects, reference Culture + SkillSet)
         |
characters/heroes.xml         (Hero objects bound to NPCCharacter + Faction)
         |
characters/npcs_{id}.xml      (Notable NPCCharacters for settlements)
         |
taom_education_character_templates.xml  (Character creation branch templates)
         |
taom_wanderers.xml            (Wanderer NPCs)
taom_wanderer_skill_sets.xml  (Wanderer SkillSet definitions)
         |
equipmentsets/taom_equipment_sets_{id}.xml  (if new armor; optional)
         |
settlements.xml               (ownership + culture assignment)
charactercreation/cultures.json  (character creation support)
```

## Filing Order

Follow this sequence exactly. Each file depends on the previous ones being correct before moving on.

1. Decide the four core identifiers: kingdom `id`, culture `id` (same value), lord prefix (2-char uppercase), and capital settlement ID
2. Write all module strings first — every other file references them
3. `taom_spcultures.xml` — culture definition
4. `TAOM_spkingdoms.xml` — kingdom definition
5. `characters/clans.xml` — one `<Faction>` per clan, tier 6 first
6. `characters/lords.xml` — one `<NPCCharacter>` per lord
7. `characters/heroes.xml` — one `<Hero>` per lord (**do not skip; see Known Crashes below**)
8. `characters/npcs_{id}.xml` — create file; register in `SubModule.xml`
9. `taom_education_character_templates.xml` — 6 entries
10. `taom_wanderers.xml` and `taom_wanderer_skill_sets.xml` — 10 entries each
11. `equipmentsets/taom_equipment_sets_{id}.xml` — only if new culture-specific armor (register in `SubModule.xml`)
12. `settlements.xml` — update `owner` and `culture` for all assigned settlements
13. `charactercreation/cultures.json` — add entry to JSON array

## Naming Conventions

| Concept | Pattern | Example |
|---------|---------|---------|
| Kingdom ID | lowercase, no spaces, no diacritics | `shaghana` |
| Culture ID | identical to kingdom ID | `shaghana` |
| Lord prefix | 2-char uppercase, unique across all kingdoms | `SH` (Shaghana), `AB` (Abanissa), `U` (Umbar) |
| Lord ID | `lord_{PREFIX}{CLAN_N}_{MEMBER_N}` | `lord_SH1_1` |
| Clan ID | `clan_{culture_id}_{N}` | `clan_shaghana_1` |
| Notable NPC | `spc_notable_{culture_id}_{slot}` | `spc_notable_shaghana_0` |
| Headman | `spc_{culture_id}_headman_{N}` | `spc_shaghana_headman_1` |
| Wanderer | `spc_wanderer_{culture_id}_{N}` | `spc_wanderer_shaghana_3` |
| Wanderer skill set | `spc_wanderer_{culture_id}_{N}_skills` | `spc_wanderer_shaghana_3_skills` |
| Kingdom strings | `taom_{id}_*` prefix | `taom_shaghana_name` |
| Culture/lord strings | `aom_{id}_*` prefix | `aom_lord_SH1_1_name` |

## File Reference

### File 1 — `Main/_Module/ModuleData/taom_module_strings.xml`

Add all localizable strings for the kingdom before touching any other file. Required groups:

**Kingdom strings** (referenced by `TAOM_spkingdoms.xml`):
```xml
<string id="taom_{id}_name"        text="{=taom_{id}_name}Full Kingdom Name" />
<string id="taom_{id}_short_name"  text="{=taom_{id}_short_name}Short Name" />
<string id="taom_{id}_title"       text="{=taom_{id}_title}Kingdom of ..." />
<string id="taom_{id}_ruler_title" text="{=taom_{id}_ruler_title}King" />
<string id="taom_{id}_desc"        text="{=taom_{id}_desc}Encyclopedia description." />
```

**Culture strings** (referenced by `taom_spcultures.xml`):
```xml
<string id="aom_{id}_name" text="{=aom_{id}_name}Culture Display Name" />
<string id="aom_{id}_desc" text="{=aom_{id}_desc}Culture encyclopedia description." />
```

**Lord name strings** (one per lord):
```xml
<string id="aom_lord_{PREFIX}{N}_1_name" text="{=aom_lord_{PREFIX}{N}_1_name}Lord Name" />
```

**Clan name strings** (one per clan):
```xml
<string id="aom_clan_{id}_{N}_name" text="{=aom_clan_{id}_{N}_name}Clan Display Name" />
```

**Notable NPC name strings** (26 per culture, one per slot — see File 6 for slot list):
```xml
<string id="aom_{id}_notable_{slot}" text="{=aom_{id}_notable_{slot}}Display Name" />
```

**Culture name pool** (male names, female names, clan names referenced in the culture `<male_names>` etc. blocks):
```xml
<string id="aom_{id}_male_name_1"  text="{=aom_{id}_male_name_1}Firstname" />
<string id="aom_{id}_female_name_1" text="{=aom_{id}_female_name_1}Firstname" />
<string id="aom_{id}_clan_name_1"  text="{=aom_{id}_clan_name_1}House Name" />
```

### File 2 — `Main/_Module/ModuleData/taom_spcultures.xml`

Add one `<Culture>` element. This is the longest entry — approximately 50 attributes and several child elements.

Required attributes:

| Attribute | Value | Notes |
|-----------|-------|-------|
| `id` | `{id}` | Must match kingdom `culture` value |
| `name` | `{=aom_{id}_name}Display` | |
| `is_main_culture` | `true` | Required for playable cultures |
| `color` | `0xffRRGGBB` | Primary culture color (hex ARGB) |
| `color2` | `0xffRRGGBB` | Secondary color |
| `can_have_settlement` | `true` | Required |
| `basic_troop` | `NPCCharacter.{base}_recruit` | Can reuse vanilla (e.g., `aserai_recruit`) |
| `elite_basic_troop` | `NPCCharacter.{id}_elite` | Culture-specific, or inherited |
| `melee_militia_troop` | `NPCCharacter.aserai_militia_spearman` | Can reuse vanilla |
| `ranged_militia_troop` | `NPCCharacter.aserai_militia_archer` | Can reuse vanilla |
| `melee_elite_militia_troop` | `NPCCharacter.aserai_militia_veteran_spearman` | Can reuse vanilla |
| `ranged_elite_militia_troop` | `NPCCharacter.aserai_militia_veteran_archer` | Can reuse vanilla |
| `default_party_template` | `PartyTemplate.kingdom_hero_party_aserai_template` | Can inherit from base culture |
| `villager_party_template` | `PartyTemplate.villagers_aserai_template` | |
| `militia_party_template` | `PartyTemplate.militia_aserai_template` | |
| `rebels_party_template` | `PartyTemplate.rebels_aserai_template` | |
| `vassal_reward_party_template` | `PartyTemplate.vassal_reward_aserai_template` | |
| `settlement_patrol_template_level_1` | existing template | |
| `settlement_patrol_template_level_2` | existing template | |
| `settlement_patrol_template_level_3` | existing template | |
| `merchant_notary` | `NPCCharacter.spc_notable_{id}_0` | First merchant slot |
| `artisan_notary` | `NPCCharacter.spc_notable_{id}_8` | First artisan slot |
| `preacher_notary` | `NPCCharacter.spc_notable_{id}_5` | First preacher slot |
| `rural_notable_notary` | `NPCCharacter.spc_notable_{id}_21` | First rural notable slot |
| `board_game_type` | `Seega`, `Tablut`, etc. | Match the region's cultural analog |
| `start_point_position_x` | float | Campaign map character creation start X |
| `start_point_position_y` | float | Campaign map character creation start Y |
| `text` | `{=aom_{id}_desc}...` | Encyclopedia description |

Required child elements:
```xml
<caravan_party_templates>
    <CaravanPartyTemplate template="PartyTemplate.caravan_aserai_template" />
</caravan_party_templates>
<elite_caravan_party_templates>
    <EliteCaravanPartyTemplate template="PartyTemplate.caravan_aserai_template" />
</elite_caravan_party_templates>
<vassal_reward_items />
<banner_bearer_replacement_weapons />
<default_policies>
    <policy id="policy_royal_privilege" />
</default_policies>
<male_names>
    <name id="{=aom_{id}_male_name_1}Firstname" />
</male_names>
<female_names>
    <name id="{=aom_{id}_female_name_1}Firstname" />
</female_names>
<clan_names>
    <name id="{=aom_{id}_clan_name_1}House Name" />
</clan_names>
```

Crowd NPC attributes (townsfolk, guards, blacksmiths, etc.) can all reference vanilla culture equivalents. The Umbar culture, for example, sets `guard="NPCCharacter.guard_aserai"` for all crowd slots.

### File 3 — `Main/_Module/ModuleData/TAOM_spkingdoms.xml`

Add one `<Kingdom>` element. Required attributes:

| Attribute | Value |
|-----------|-------|
| `id` | `{id}` |
| `owner` | `Hero.lord_{PREFIX}1_1` (the ruling lord) |
| `initial_home_settlement` | `Settlement.town_{REGION}N` (capital) |
| `banner_key` | Banner serialization string (copy from existing, modify) |
| `primary_banner_color` | `0xffRRGGBB` |
| `secondary_banner_color` | `0xffRRGGBB` |
| `color` | `0xffRRGGBB` (map faction color) |
| `color2` | `0xffRRGGBB` |
| `culture` | `Culture.{id}` |
| `settlement_banner_mesh` | `encounter_flag_a`, `_b`, or `_c` |
| `flag_mesh` | `info_screen_flags_a` or `_b` |
| `name` | `{=taom_{id}_name}Display Name` |
| `short_name` | `{=taom_{id}_short_name}Short Name` |
| `title` | `{=taom_{id}_title}Kingdom of ...` |
| `ruler_title` | `{=taom_{id}_ruler_title}King` |
| `text` | `{=taom_{id}_desc}Description text` |

The `<relationships>` block must include **every other kingdom** — both custom kingdoms in `TAOM_spkingdoms.xml` and the vanilla kingdoms wrapped by XSLT (aserai, empire, empire_w, empire_s, vlandia, battania, sturgia, khuzait). Relationship stances:

| Stance | `value` | `isAtWar` |
|--------|---------|-----------|
| At war | `-1` | `true` |
| Neutral | `0` | `false` |
| Allied | `1` | `false` |

The `<policies>` block should include at minimum:
```xml
<policies>
    <policy id="policy_royal_privilege" />
    <policy id="policy_lord_prerogative" />
    <policy id="policy_religious_privilege" />
    <policy id="policy_castle_charters" />
</policies>
```

### File 4 — `Main/_Module/ModuleData/characters/clans.xml`

Add one `<Faction>` per clan. Guidelines for clan count: 5–10 clans per kingdom is typical. One tier-6 (ruling clan, the kingdom owner's clan), one or two tier-5 (major clans), and the rest tier 3–4.

Required attributes per clan:

| Attribute | Value |
|-----------|-------|
| `id` | `clan_{id}_{N}` |
| `name` | `{=aom_clan_{id}_{N}_name}Clan Name` |
| `tier` | 1–6 (6 = ruling clan) |
| `owner` | `Hero.lord_{PREFIX}{N}_1` |
| `culture` | `Culture.{id}` |
| `super_faction` | `Kingdom.{id}` |
| `is_noble` | `true` |
| `banner_key` | Banner serialization string |
| `initial_home_settlement` | Settlement owned by this clan at start (tier-6 gets capital; tier 3–4 get castles; tier 1–2 may have none) |

### File 5 — `Main/_Module/ModuleData/characters/lords.xml`

Add one `<NPCCharacter>` per lord (minimum: one per clan, which is the clan owner). More members per clan is fine.

Required attributes:

| Attribute | Value |
|-----------|-------|
| `id` | `lord_{PREFIX}{CLAN_N}_{MEMBER_N}` |
| `name` | `{=aom_lord_{PREFIX}{N}_{M}_name}Lord Name` |
| `age` | 30–60 for clan owners; 15–25 for children |
| `voice` | `earnest`, `curt`, `ironic`, `softspoken`, etc. |
| `is_hero` | `true` |
| `culture` | `Culture.{id}` |
| `occupation` | `Lord` |
| `default_group` | `Infantry`, `Cavalry`, `Ranged`, or `HorseArcher` |
| `face_mesh_cache` | `true` |
| `skill_template` | A `SkillSet` reference (e.g., `SkillSet.spc_swordsman_skills`) |

Mandatory child elements:
```xml
<face>
    <BodyProperties version="4" age="35" weight="0.2" build="0.74" key="[64-char hex]" />
    <hair_tags>
        <hair_tag name="{culture_hair_tag}" />
    </hair_tags>
    <beard_tags>
        <beard_tag name="{culture_beard_tag}" />
    </beard_tags>
    <tattoo_tags>
        <tattoo_tag name="Cleanface" />
    </tattoo_tags>
</face>
<skills>
    <!-- All 18 skills must be listed: -->
    <!-- OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing -->
    <!-- Riding, Athletics, Smithing -->
    <!-- Scouting, Tactics, Roguery, Charm, Leadership, Trade -->
    <!-- Steward, Medicine, Engineering -->
    <skill id="OneHanded" value="N" />
    <!-- ... -->
</skills>
<Traits>
    <!-- Combat: KnightFightingSkills, Commander values 1–10 -->
    <!-- Personality: Politician, Manager values 1–10 -->
    <!-- Virtues: Valor, Generosity, Mercy, Honor, Calculating values -2 to +2 -->
    <!-- Politics: Egalitarian, Oligarchic, Authoritarian values -1, 0, or 1 -->
</Traits>
<Equipments>
    <EquipmentSet id="{culture}_bat_template_medium_a" />
    <EquipmentSet id="{culture}_civ_template_default_a" civilian="true" />
</Equipments>
```

`BodyProperties` key: copy a 64-char hex string from an existing lord with a similar ethnicity and modify `age`, `weight`, and `build` to differentiate. The `key` encodes morph weights for facial features.

### File 6 — `characters/heroes.xml`

**This is the most commonly missed file.** Every `<NPCCharacter>` with `is_hero="true"` in `lords.xml` must have a corresponding `<Hero>` entry here. Without it the game crashes on new game creation. See the Known Crashes section below.

Minimal entry for a non-ruler lord:
```xml
<Hero id="lord_SH2_1" faction="Faction.clan_shaghana_2" />
```

Entry for the ruling lord (add `text` for encyclopedia biography):
```xml
<Hero
    id="lord_SH1_1"
    faction="Faction.clan_shaghana_1"
    text="{=lord_SH1_1_text}Biography text for the encyclopedia." />
```

For lord family members (children reference their parent):
```xml
<Hero id="lord_SH1_11" father="Hero.lord_SH1_1" faction="Faction.clan_shaghana_1" />
```

### File 7 — `characters/npcs_{culture}.xml` (NEW FILE)

Create this file. Register it in `SubModule.xml` as:
```xml
<XmlName id="NPCCharacters" path="characters/npcs_{id}"/>
```

The file must contain exactly 26 `<NPCCharacter>` entries following this distribution:

| Slot IDs | Count | Occupation | Culture attribute reference |
|----------|-------|------------|-----------------------------|
| `spc_notable_{id}_0` through `_4b` | 10 | `Merchant` | `merchant_notary` references `_0` |
| `spc_notable_{id}_5`, `_6`, `_7` | 3 | `Preacher` | `preacher_notary` references `_5` |
| `spc_notable_{id}_8`, `_9` | 2 | `Artisan` | `artisan_notary` references `_8` |
| `spc_notable_{id}_gl1`, `_10`, `_11`, `_gl4`, `_12`, `_13` | 6 | `GangLeader` | |
| `spc_notable_{id}_21`, `_22` | 2 | `RuralNotable` | `rural_notable_notary` references `_21` |
| `spc_{id}_headman_1`, `_2`, `_3` | 3 | `Headman` | |

Required attributes per NPC:
- `is_template="true"`
- `is_hero="false"`
- `culture="Culture.{id}"`
- `skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills"`

Notable NPCs (Merchant, Preacher, Artisan, GangLeader, RuralNotable) carry only civilian `<EquipmentRoster>` entries — no combat gear. Headmen use `occupation="Headman"` exactly; all others use the occupation string exactly as listed above.

### File 8 — `Main/_Module/ModuleData/taom_education_character_templates.xml`

Add 6 `<NPCCharacter>` entries named `child_education_templates_stage_2_page_0_branch_{0-5}_{id}`.

Required attributes:
- `name="{=!}stage_2_page_0_branch_{N}_{id}"`
- `age="45"`, `default_group="Infantry"`, `is_hero="false"`, `occupation="Lord"`, `is_template="true"`
- `culture="Culture.{id}"`
- `skill_template="SkillSet.infantry_heavyinfantry_level1_template_skills"`
- `<face_key_template value="BodyProperty.fighter_{base_face}" />`
- **No `race` attribute** for human cultures; non-human cultures must specify `race="{race_id}"`

Each entry needs one combat `<EquipmentRoster>` and one empty civilian roster. Vary equipment across branches: branch 0 provides body and legs only; subsequent branches add head armor or gloves to differentiate the six character creation paths.

### File 9 — `Main/_Module/ModuleData/taom_wanderers.xml`

Add 10 `<NPCCharacter>` entries per culture: `spc_wanderer_{id}_0` through `spc_wanderer_{id}_9`.

Required attributes:
- `is_template="true"`
- `is_hero="false"`
- `occupation="Wanderer"`
- `culture="Culture.{id}"` — must match the new culture ID, not the base culture; this controls which settlements this wanderer appears in

Each entry references a skill set: `skill_template="SkillSet.spc_wanderer_{id}_{N}_skills"`.

### File 10 — `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml`

Add 10 `<SkillSet>` entries: `spc_wanderer_{id}_0_skills` through `spc_wanderer_{id}_9_skills`.

Each archetype should have one primary skill at 200–230 and supporting skills at lower values. Standard 10 archetypes:

| Index | Archetype | Primary Skill |
|-------|-----------|--------------|
| 0 | Engineer | Engineering 200+ |
| 1 | Warrior | OneHanded 230+ |
| 2 | Scout/Archer | Bow 210+ |
| 3 | Healer | Medicine 220+ |
| 4 | Tactician | Tactics 220+ |
| 5 | Rogue | Roguery 210+ |
| 6 | Trader | Trade 220+ |
| 7 | Cavalry | Riding 230+ |
| 8 | Smith | Crafting 220+ |
| 9 | Leader | Leadership 220+ |

### File 11 — `equipmentsets/taom_equipment_sets_{culture}.xml` (conditional)

Create this file only if the culture uses new or unique armor items. If the culture reuses an existing culture's equipment (e.g., Shaghana reusing Harad's `haradrim_*` items), lords can reference the base culture's `EquipmentSet` IDs directly in their `<Equipments>` block and this file is not needed.

If created, register in `SubModule.xml`:
```xml
<XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_{id}"/>
```

Always validate item IDs against the `LOTRLOME_Armory` module before writing equipment entries. Characters appear in underwear when item IDs do not exist in the Armory. Use `grep -o 'id="[^"]*"' <armory-file>` to enumerate valid IDs.

### File 12 — `Main/_Module/ModuleData/settlements.xml`

For each settlement assigned to the new kingdom, update two attributes on the existing `<Settlement>` element:
- `owner="Faction.clan_{id}_{N}"` — assigns the settlement to a clan
- `culture="Culture.{id}"` — controls settlement visuals and culture bonuses

Settlement positions do not change. Only ownership and culture attributes are updated.

### File 13 — `charactercreation/cultures.json`

Add one entry to the JSON array:

```json
{
  "culture_id": "{id}",
  "races": ["human"],
  "starting_settlement": "town_{REGION}{N}",
  "default_age": 20.0,
  "default_weight": 0.5417,
  "default_build": 0.5231,
  "focus_to_add": 1,
  "skill_level_to_add": 10
}
```

Set `races` to the list of playable races for this culture:
- Human-only: `["human"]`
- Harad-adjacent with multiple types: `["human"]`
- Mordor: `["uruk", "goblin", "orc", "human"]`
- Elven: `["elf", "human"]`

`starting_settlement` is the campaign map location where character creation places the player. Use the capital town for the new kingdom.

## SubModule.xml Registration

The `npcs_{id}.xml` file requires a registration entry in `SubModule.xml`. Place it with the other `NPCCharacters` entries:

```xml
<XmlName id="NPCCharacters" path="characters/npcs_{id}"/>
```

If a custom troop file was created:
```xml
<XmlName id="NPCCharacters" path="troops/troops_{id}"/>
```

If a custom equipment sets file was created:
```xml
<XmlName id="EquipmentRosters" path="equipmentsets/taom_equipment_sets_{id}"/>
```

## What Can Be Inherited

Many resources can be shared with existing cultures. When a new kingdom is culturally adjacent to an existing one (e.g., Shaghana and Abanissa are both Harad-adjacent and inherit from aserai), there is no need to create new troop trees, party templates, or crowd NPCs.

| Resource | Inherit from | How |
|----------|-------------|-----|
| Basic troop | Any vanilla culture | `basic_troop="NPCCharacter.aserai_recruit"` in culture |
| Militia troops | Any vanilla culture | `melee_militia_troop="NPCCharacter.aserai_militia_spearman"` |
| Party templates | Any existing template | `default_party_template="PartyTemplate.kingdom_hero_party_aserai_template"` |
| Crowd NPCs | Any vanilla culture | `guard="NPCCharacter.guard_aserai"` etc. in culture attributes |
| Equipment sets | Any existing culture's sets | `<EquipmentSet id="harad_bat_template_medium_a" />` in lord `<Equipments>` |

Umbar, Shaghana, and Abanissa all inherit aserai-base resources this way.

## Known Crashes

### Missing heroes.xml Entry

**Symptom:** New game creation crashes with a NullReferenceException. The log shows `Null object reference found with ID: lord_X` at startup.

**Root cause:** Every `<NPCCharacter>` with `is_hero="true"` in `lords.xml` must have a corresponding `<Hero>` in `heroes.xml`. The `<Hero>` element registers the Hero instance in `MBObjectManager` and binds it to a clan. Without it, the Hero's `CharacterObject` is null even though the NPCCharacter loads correctly. This surfaces as a NRE in `LordNeedsHorsesIssueBehavior.ConditionsHold` when the behavior checks `issueGiver.Culture.StringId` on the null hero.

**Diagnosis:** Search the startup log for `"Null object reference found with ID"`. Each listed ID is a lord missing from `heroes.xml`. Compare your new lord IDs against the `<Hero id="...">` entries in that file.

**Fix:** Add a `<Hero>` entry for each listed ID. The minimal entry is `<Hero id="lord_X_1" faction="Faction.clan_{id}_{N}" />`.

### Wanderers Not Appearing

**Symptom:** No wanderers show up in settlements belonging to the new kingdom.

**Root cause:** The `culture` attribute on wanderer `<NPCCharacter>` entries controls which settlements they can spawn in. If wanderers were copied from a base culture (e.g., aserai) and the `culture` attribute was not updated to `Culture.{new_id}`, they will spawn only in aserai settlements.

**Fix:** Verify all 10 wanderer entries in `taom_wanderers.xml` have `culture="Culture.{id}"` where `{id}` is the new culture ID.

### Settlement Shows Wrong Culture Visuals

**Symptom:** A settlement's banners, guard uniforms, or prosperity visuals do not match the new kingdom.

**Root cause:** The `culture` attribute on the `<Settlement>` element in `settlements.xml` was not updated, or was updated to the wrong culture ID.

**Fix:** Search `settlements.xml` for each settlement ID assigned to the new kingdom and verify both `owner` and `culture` attributes are correct.

## Existing Kingdom Examples

| Kingdom | Culture ID | File type | Lord prefix | Notes |
|---------|------------|-----------|-------------|-------|
| Umbar | `umbar` | Custom XML | `U` | Full implementation; reference for all 13 files |
| Shaghana | `shaghana` | Custom XML | `SH` | Harad sub-kingdom; reuses aserai militia and crowd NPCs |
| Abanissa | `abanissa` | Custom XML | `AB` | Far Harad sub-kingdom; same inheritance pattern as Shaghana |
| Gondor | `gondor` | Custom XML | varies | Fully custom troops and equipment sets |
| Harwan | n/a | XSLT | — | Stays on `Kingdom.aserai`; clans defined in `spclans.xslt` |

When in doubt, use Umbar as the reference implementation — it is the most complete custom kingdom and all 13 files are present.

## File Format Notes

- **Encoding:** UTF-8, CRLF line endings
- **Indentation:** Tabs (not spaces) in XML; 2-space indentation in JSON
- **String key format:** `{=string_id}Display Text` — the string ID before the closing `}` must exactly match a `<string id="...">` entry in `taom_module_strings.xml`
- **Banner keys:** Long serialized strings. Copy from an existing kingdom of similar visual style and modify colors. The Bannerlord banner editor in-game can also generate a new key.
- **BodyProperties keys:** 64-char hex strings encoding facial feature morph weights. Copy from a culturally similar lord and adjust `age`, `weight`, and `build` to differentiate individuals.
- **Color format:** `0xffRRGGBB` — always prefix with `ff` for the alpha channel. Colors without the `ff` prefix render as transparent.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/ModuleData/TAOM_spkingdoms.xml` | Kingdom definitions (custom kingdoms only) |
| `Main/_Module/ModuleData/taom_spcultures.xml` | Culture definitions for all custom kingdoms |
| `Main/_Module/ModuleData/characters/clans.xml` | Clan (Faction) definitions |
| `Main/_Module/ModuleData/characters/lords.xml` | Lord NPCCharacter definitions |
| `Main/_Module/ModuleData/characters/heroes.xml` | Hero object registrations (binds lords to clans) |
| `Main/_Module/ModuleData/characters/npcs_{id}.xml` | Notable NPCs for settlements (per culture, new file) |
| `Main/_Module/ModuleData/taom_education_character_templates.xml` | Character creation branch templates |
| `Main/_Module/ModuleData/taom_wanderers.xml` | Wanderer NPCCharacter entries |
| `Main/_Module/ModuleData/taom_wanderer_skill_sets.xml` | Wanderer skill set definitions |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_{id}.xml` | Custom equipment sets (conditional) |
| `Main/_Module/ModuleData/settlements.xml` | Settlement ownership and culture assignments |
| `Main/_Module/ModuleData/taom_module_strings.xml` | All localizable strings |
| `Main/_Module/ModuleData/charactercreation/cultures.json` | Character creation culture support |
| `Main/_Module/SubModule.xml` | File registrations for new npcs, troops, and equipment files |

## Dependencies

This feature is pure data — no C# dependencies. It does depend on:

- `LOTRLOME_Armory` module — must be loaded for equipment item IDs to resolve
- Vanilla Bannerlord module data — militia troops, party templates, and crowd NPCs are borrowed from vanilla cultures
- `taom_module_strings.xml` — must be loaded before all other kingdom/culture/lord files

## Tests

There are no automated unit tests for kingdom XML data. Validation is performed by:

1. Loading the game and starting a new campaign — crashes indicate missing Hero entries or broken references
2. Checking the startup log for `"Null object reference found with ID"` entries
3. Opening the encyclopedia and verifying kingdom, clan, and lord entries all render correctly
4. Visiting a settlement owned by the new kingdom and confirming wanderers, notables, and guards appear with correct culture visuals

## GitHub Issue

- **Issue:** #63 — Harad Split: Shaghana and Abanissa kingdoms
- **Status:** Closed
