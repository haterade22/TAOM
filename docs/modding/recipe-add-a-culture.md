# Recipe: add a culture

A culture is the hub every other piece of content hangs off: troops, notables, wanderers, party
templates, equipment, starting gold, careers and the character-creation flow all resolve through it.
Adding a culture is the largest authoring job in TAOM, and most of it is wiring rather than writing,
because a culture that exists but is not wired looks fine in every validator and ships fielding
Calradians. This chapter is the ordered path plus the list of things that fail quietly; attribute
meanings live in [cultures.md](cultures.md) and are never restated here.

## Two paths, pick before you write anything

| Path | What you do | When to use it |
|---|---|---|
| **Custom id** | Add a `<Culture id="mine">` block to `Main/_Module/ModuleData/taom_spcultures.xml` | The culture has its own troops, notables and identity. Every TAOM culture that owns a kingdom took this path |
| **XSLT-wrapped vanilla id** | Leave the culture as a vanilla id and rewrite its attributes in `Main/_Module/ModuleData/spcultures.xslt` | The faction reuses a vanilla culture slot. Dale rides on `sturgia`, Rohan on `vlandia`, Khand on `battania` |

The split matters because the two paths inherit differently: a vanilla id keeps vanilla's child and
teenager equipment templates for free, a custom id gets **none** of them and the game NREs during
`InitialChildGeneration` on new-game if you do not author them
([new-factions-misty-mountains-lindon.md](../features/new-factions-misty-mountains-lindon.md), #267).

TAOM currently ships 24 `<Culture>` blocks, 16 of them flagged `is_main_culture="true"`, plus 6
vanilla ids rewritten in `spcultures.xslt`.
<!-- measured: python -c "import xml.etree.ElementTree as ET;c=list(ET.parse('Main/_Module/ModuleData/taom_spcultures.xml').getroot().iter('Culture'));print(len(c),sum(1 for x in c if x.get('is_main_culture')=='true'))" 2026-09-05 -->
<!-- measured: rg -o "Culture\[@id='[a-z]+'\]" Main/_Module/ModuleData/spcultures.xslt | sort -u | wc -l 2026-09-05 -->

## The checklist, fatal rows first

Source: the 14-row table in [culture-playability-wiring.md](../features/culture-playability-wiring.md),
reordered so the rows that crash come before the rows that only disappoint.

**Fatal. Skip one of these and the game crashes or a stage goes blank.**

| # | Surface | File | What breaks |
|---|---|---|---|
| 1 | The `<Culture>` object itself | `taom_spcultures.xml` ([cultures.md](cultures.md)) | Skipped with a warning, everything downstream orphans |
| 2 | Registered for character creation | `charactercreation/cultures.json` ([configs-factions-and-world.md](configs-factions-and-world.md)) | Culture cannot be picked |
| 3 | Narrative options in all four culture-scoped menus | `charactercreation/{parents,youth,education,adulthood}_menu.json` | Blank character-creation stage |
| 8 | Child, teen, lord and education equipment templates | `equipmentsets/taom_{child_equipment,lord_template_equipment,education_equipment}_templates.xml` ([equipment-rosters.md](equipment-rosters.md)) | NRE on new game |
| 9 | Stage-2 education tutor templates, 6 of them | `taom_education_character_templates.xml` | Age-8 CTD (#354) |
| 11 | Owns at least one settlement | `TAOM_Map/ModuleData/settlements.xml` ([settlements.md](settlements.md)) | Daily-tick CTD (#374) |
| 13 | Party templates: all twelve authored, all eight attributes bound, both caravan child lists bound | `taom_partyTemplates.xml` plus `taom_spcultures.xml` ([party-templates.md](party-templates.md)) | A null or empty binding is an NRE in `SpawnPatrolParty` / `SpawnCaravan`; an unbound one is silently Calradian |
| 14 | `as_<race>_facegen` action set, only if the culture introduces a race | `LOTRLOME_Armory/ModuleData/action_sets.xml` | T-pose or contorted mesh |

Row 11 and row 14 name files in the game install. This file lives in the game install, not the repo;
a module reinstall reverts hand edits, so land a repo-side validator gate with any fix. For row 11
that gate is `LANDLESS_CULTURE` in `tools/validate_moduledata.py`
([lord-spawn-guard.md](../features/lord-spawn-guard.md)).

**Silent. The culture loads, plays, and is wrong.**

| # | Surface | File | What you see |
|---|---|---|---|
| 4 | Default body | `charactercreation/cc_body_properties.xml` ([body-properties.md](body-properties.md)) | Falls back to a random vanilla body |
| 5 | Character-creation starting equipment, male and female per `title_type` | `equipmentsets/taom_char_creation_equipment.xml` | Player exits creation in vanilla default gear |
| 6 | Starting denars | `startup_resources/startup_resources_config.xml` ([configs-balance.md](configs-balance.md)) | Player starts with 0 gold |
| 7 | At least one eligible career | `career_system/taom_careers.xml` plus `charactercreation/career_menu.json` | No career offered |
| 10 | Enlistment rosters, 4 ranks | `equipmentsets/taom_enlistment_equipment.xml` | Another culture's gear is issued |
| 12 | Volunteer recruitment pool | `Main/Features/TroopProgression/RecruitmentPools/` | Empty recruit slots in every settlement |

Row 12 is the one row that is C# rather than data. There are 16 partial files under
`RecruitmentPools/` covering 22 distinct `CultureMap` keys.
<!-- measured: ls Main/Features/TroopProgression/RecruitmentPools/*.cs | wc -l 2026-09-05 -->
<!-- measured: rg -o 'CultureMap\["[a-z]+"\] =' Main/Features/TroopProgression/RecruitmentPools/ | sort -u | wc -l 2026-09-05 -->

## Order of work

The dependency rule from [kingdom-creation.md](../features/kingdom-creation.md) is short: strings
before the XML that references them, cultures before kingdoms, clans before lords. The 13-step
filing order that follows from it:

1. Pick the ids: culture id, kingdom id (same value), 2-character lord prefix, capital settlement id.
2. Module strings ([strings-and-localization.md](strings-and-localization.md)).
3. `taom_spcultures.xml`.
4. `taom_spkingdoms.xml` ([kingdoms.md](kingdoms.md)). The file on disk is lowercase; the
   capitalised `TAOM_spkingdoms.xml` in `kingdom-creation.md` is a documentation error.
5. `characters/clans.xml`, tier 6 first ([clans.md](clans.md)).
6. `characters/lords.xml` ([lords-and-heroes.md](lords-and-heroes.md)).
7. `characters/heroes.xml`, one `<Hero>` per lord. Skipping it is the startup NRE below.
8. `characters/npcs_{id}.xml`, then register it ([npcs-notables-and-townsfolk.md](npcs-notables-and-townsfolk.md)).
9. `taom_education_character_templates.xml`, 6 entries.
10. `taom_wanderers.xml` and `taom_wanderer_skill_sets.xml`, 10 each ([wanderers-and-named-companions.md](wanderers-and-named-companions.md), [skill-sets.md](skill-sets.md)).
11. `equipmentsets/taom_equipment_sets_{id}.xml`, only if the culture has its own armour.
12. Settlement ownership and culture in the live `TAOM_Map` settlements file.
13. `charactercreation/cultures.json`.

### Naming patterns

From `kingdom-creation.md` "Naming Conventions", with ids covered end to end in
[id-cheatsheet.md](id-cheatsheet.md). Getting one wrong orphans content without an error.

| Concept | Pattern | Example |
|---|---|---|
| Kingdom id and culture id | identical, lowercase, no diacritics | `lindon` |
| Lord prefix | 2 characters, uppercase, unique across kingdoms | `SH`, `AB` |
| Lord id | `lord_{PREFIX}{CLAN_N}_{MEMBER_N}` | `lord_SH1_1` |
| Clan id | `clan_{culture_id}_{N}` | `clan_shaghana_1` |
| Notable | `spc_notable_{culture_id}_{slot}` | `spc_notable_lindon_0` |
| Headman | `spc_{culture_id}_headman_{N}` | `spc_lindon_headman_1` |
| Wanderer | `spc_wanderer_{culture_id}_{N}` | `spc_wanderer_lindon_3` |
| Kingdom strings | `taom_{id}_*` | `taom_lindon_name` |
| Culture and lord strings | `aom_{id}_*` | `aom_lindon_name` |

## Worked example: Lindon

Lindon is the most recent culture TAOM shipped, and it was built the cheap way: promoted out of
Rivendell, which it borrowed until 2026-08-10. Three scripts did it, and the order is load-bearing.
Each command below is the usage line from that script's own `Run:` docstring.

```bash
python tools/promote_borrowed_cultures.py                        # dry run, reports what it would do
python tools/promote_borrowed_cultures.py --only lindon --apply  # writes the culture DATA
python tools/retag_promoted_cultures.py --apply                  # moves kingdom, clans, lords, settlements
python tools/fix_promoted_culture_flavor.py --apply              # strips the source culture's names
```

Step 1 writes the `<Culture>` block, troops, NPCs, equipment sets, wanderers, party templates and the
child/lord/education/tutor templates. Step 2 moves the existing kingdom, clans, lords and settlements
onto it. Between the two the culture exists and owns no land, which is the `LANDLESS_CULTURE`
daily-tick CTD, so run them in one sitting. Step 1's closing message names what it deliberately left
undone: register the new files in `SubModule.xml`, retag, and wire character creation.

The head of the resulting block, verbatim. The Lindon `<Culture>` element runs 336 lines in total.
<!-- measured: python -c "l=open('Main/_Module/ModuleData/taom_spcultures.xml',encoding='utf-8-sig').read().split('\n');s=[i for i,x in enumerate(l) if 'id=\"lindon\"' in x][0]-1;e=[i for i,x in enumerate(l) if '</Culture>' in x and i>s][0];print(e-s+1)" 2026-09-05 -->

<!-- example file="Main/_Module/ModuleData/taom_spcultures.xml" id="lindon" -->
```xml
  <Culture
    id="lindon"
    name="{=aom_lindon_name}Falathrim Elves"
    is_main_culture="true"
    color="0xFF50A090"
    color2="0xFF7FC4B4"
    elite_basic_troop="NPCCharacter.lindon_imladris_infantry"
    basic_troop="NPCCharacter.lindon_imladris_recruit"
    melee_militia_troop="NPCCharacter.lindon_militia_spearman"
    ranged_militia_troop="NPCCharacter.lindon_militia_archer"
    melee_elite_militia_troop="NPCCharacter.lindon_militia_veteran_spearman"
    ranged_elite_militia_troop="NPCCharacter.lindon_militia_veteran_archer"
    can_have_settlement="true"
    villager_party_template="PartyTemplate.villager_lindon_template"
    default_party_template="PartyTemplate.kingdom_hero_party_lindon_template"
    settlement_patrol_template_level_1="PartyTemplate.patrol_party_lindon_template_level_1"
    settlement_patrol_template_level_2="PartyTemplate.patrol_party_lindon_template_level_2"
    settlement_patrol_template_level_3="PartyTemplate.patrol_party_lindon_template_level_3"
    militia_party_template="PartyTemplate.militia_lindon_template"
    rebels_party_template="PartyTemplate.rebels_lindon_template"
    vassal_reward_party_template="PartyTemplate.vassal_reward_troops_lindon"
```

The three attributes a reader changes first:

1. **`basic_troop` and `elite_basic_troop`** decide what the culture's settlements recruit and what
   its garrisons are built from. Point them at your own `troops_{culture}.xml` ids
   ([troops.md](troops.md)), never at a vanilla Calradian id.
2. **The eight party-template attributes** (`default_party_template`, `villager_party_template`,
   `militia_party_template`, `rebels_party_template`, `vassal_reward_party_template` and the three
   `settlement_patrol_template_level_*`) decide what lords, villagers, militias, rebels and patrols
   are made of. All eight are read by the engine. Leaving one unbound is silent.
3. **`name` and `text`** are the culture's display name and encyclopedia blurb, both `{=key}default`
   inline strings.

Caravans are not attributes. They come only from two child elements, and the engine reads the `id`
attribute off whichever child it finds (`CultureObject.cs:485-497`), so any other attribute name
appends a null and the first caravan spawn is an NRE:

<!-- example file="Main/_Module/ModuleData/taom_spcultures.xml" id="lindon" -->
```xml
    <caravan_party_templates>
      <caravan_party_template id="PartyTemplate.caravan_template_lindon" />
    </caravan_party_templates>
    <elite_caravan_party_templates>
      <caravan_party_template id="PartyTemplate.elite_caravan_template_lindon" />
    </elite_caravan_party_templates>
```

Lindon carries 14 party templates in `taom_partyTemplates.xml`: the 12 standard ones plus two legacy
`kingdom_hero_party_rivendell_lindon_*` rows left over from the borrowed era.
<!-- measured: rg -o 'MBPartyTemplate id="[^"]*lindon[^"]*"' Main/_Module/ModuleData/taom_partyTemplates.xml | sort 2026-09-05 -->

The three new files are registered in `Main/_Module/SubModule.xml` inside a
`<!-- TAOM-NEWCULTURE-REG:BEGIN -->` marker region, one `<XmlNode>` each for
`troops/troops_lindon`, `characters/npcs_lindon` and `equipmentsets/taom_equipment_sets_lindon`.
Shape and placement rules: [submodule-and-registration.md](submodule-and-registration.md).

The character-creation entry, which is what makes the culture pickable:

<!-- excerpt file="Main/_Module/ModuleData/charactercreation/cultures.json" -->
```json
    {
        "culture_id": "lindon",
        "races": ["elf", "human"],
        "starting_settlement": "town_LN1",
        "default_age": 20.0,
        "default_weight": 0.0232,
        "default_build": 0.5347,
        "focus_to_add": 0,
        "skill_level_to_add": 0
    }
```

And the recruitment pool, which is C# and has no data equivalent:

<!-- excerpt file="Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.PromotedCultures.cs" -->
```csharp
        CultureMap["lindon"] = new List<VolunteerChance>
        {
            new VolunteerChance("lindon_imladris_recruit", 5),
            new VolunteerChance("lindon_imladris_infantry", 3),
            new VolunteerChance("lindon_imladris_bowman", 2),
            new VolunteerChance("lindon_noble", 1),                 // noble cavalry line entry
            new VolunteerChance("lindon_knight_golden_flower", 1)   // Golden-Flower foot elite line entry
        };
```

What Lindon ended up with, measured off disk: 74 `<NPCCharacter>` in `characters/npcs_lindon.xml`,
exactly 26 of them the notable and headman slots (10 Merchant, 3 Preacher, 2 Artisan, 6 GangLeader,
2 RuralNotable, 3 Headman); 10 wanderers; 6 stage-2 education templates; 6 child and 10 lord
templates; 98 education-equipment and 13 enlistment rosters; 55 character-creation rosters.

## Recipes

### Add a culture from nothing

1. Decide the path (custom id or XSLT-wrapped vanilla id) and the ids from the naming table above.
2. Write the module strings first.
3. Author the `<Culture>` block in `taom_spcultures.xml`, copying the closest existing culture's
   block rather than a vanilla one. Bind all eight party-template attributes and both caravan lists.
4. Author `troops/troops_{id}.xml` and register it. To clone a generator, copy
   `tools/generate_dale_troops.py` per [new-culture-authoring.md](../ai-includes/new-culture-authoring.md).
5. Author `characters/npcs_{id}.xml` (26 notable and headman slots plus townsfolk), and register it.
6. Add the twelve party templates to `taom_partyTemplates.xml` ([party-templates.md](party-templates.md),
   [party-template-sizing.md](../reference/party-template-sizing.md)).
7. Add 10 wanderers and 10 wanderer skill sets.
8. Author the child, lord and education equipment templates. A custom culture inherits none of them.
9. Add the 6 stage-2 education tutor templates.
10. Add the culture to `charactercreation/cultures.json`, `cc_body_properties.xml`, the four
    narrative menus, `startup_resources_config.xml`, `taom_careers.xml` and
    `taom_char_creation_equipment.xml`.
11. Add the volunteer pool partial under `RecruitmentPools/` and a test beside it.
12. Append the culture to the hardcoded `cultures` list in `tools/validate_all_troop_refs.py` or its
    troop file is never swept.
13. Give the culture a settlement. Until it owns one, it is a daily-tick CTD.
14. Remove the culture from every `documentedExceptions` list in `TAOM.Tests/` that names it.

Check: `python tools/validate_moduledata.py` then `python tools/validate_all_troop_refs.py` then
`dotnet test TAOM.Tests --filter CulturePartyTemplate`
Takes effect: new campaign only (a new `<XmlNode>` is null in-engine until the process restarts, and
lords, clans and settlements seed at world generation)
Code: Code changes required in `Main/Features/TroopProgression/RecruitmentPools/` for the volunteer
pool, and in `Main/Features/CulturalFeats/TaomCulturalFeats.cs` for any new feat

### Make an existing culture playable

Rows 2 to 10 of the checklist, for a culture whose `<Culture>` block already exists: the culture
ships, the faction is on the map, and character creation is blank.

1. Confirm `basic_troop` and `elite_basic_troop` resolve to TAOM ids.
2. Add the `cultures.json` entry with `races`, `starting_settlement` and body defaults.
3. Give it narrative options in all four culture-scoped menus.
   `python tools/insert_new_faction_cc_menus.py` clones another culture's with a text remap.
4. Read the `title_type` values out of its own `youth_menu.json`, then
   `python tools/generate_char_creation_equipment.py --append <culture> --apply`. Add a
   `no_mount: True` row to that script's `CULTURES` table if the troop tree has no `slot="Horse"`.
5. Add the `startup_resources_config.xml` row, or the player starts on 0 denars.
6. Give it careers: `python tools/insert_new_faction_careers.py --apply` clones an existing culture's.
7. Add enlistment rosters, the child, teen, lord and education templates, and the recruitment pool.
   `python tools/generate_lord_template_equipment.py` fills the mandatory 8-roster matrix.
8. Remove the culture from every `documentedExceptions` list in `TAOM.Tests/` that names it; a fixed
   culture left in a suppression list is a permanent blind spot.

Check: `python tools/audit_equipment_roster_coverage.py` then `python tools/validate_moduledata.py`
Takes effect: full game restart (the character-creation rows bind on a new campaign only)
Code: Code changes required in `Main/Features/TroopProgression/RecruitmentPools/` for step 7

### Promote a borrowed culture

For a kingdom that runs on another culture's id. `bluecraig` ran on `Culture.goblin` and `lindon` on
`Culture.rivendell`, so picking Blue Craig dropped the player in Goblin-town: the starting settlement
is a property of the culture, not of the faction-map region clicked.

1. `python tools/promote_borrowed_cultures.py --only <id> --apply` writes the culture data.
2. `python tools/retag_promoted_cultures.py --apply` moves the kingdom, clans, lords, heroes and the
   live settlements onto the new culture.
3. `python tools/fix_promoted_culture_flavor.py --apply` strips the source culture's names, places
   and epithets out of the clone.
4. Register the three new files in `SubModule.xml` and wire character creation.

Four traps these scripts exist to encode, all from
[culture-playability-wiring.md](../features/culture-playability-wiring.md):

- **A culture's id-space is not namespaced by its own name.** `troops_rivendell.xml` carries
  `imladris_*`, `noldorin_*`, `rider_*` and `battlemaster_*` ids next to `rivendell_*` ones. A blanket
  rename leaves them untouched and the clone then redefines ids that already exist, which the engine
  silently shadows.
- **Asset ids must survive the rename, data ids must not.** `Item.*`, `BodyProperty.*`, `SkillSet.*`,
  `portrait_sprite`, `particle_effect` and `sound_effect` all name real files. Renaming one invents a
  reference to nothing: 2470 validator errors in a single run here. Feat ids are the same class,
  because `CulturalFeatsService` matches against what `TaomCulturalFeats.cs` registers. This is why
  Lindon's block still reads `default_character_creation_body_property="BodyProperty.fighter_rivendell"`
  and still lists six `taom_rivendell_*` feats. Both are correct.
- **Scope every retag by element name.** A pattern that matches "any element whose id matches" also
  matches the root element, and then rewrites every culture in the file. That reported 102 retags on
  `lords.xml` instead of the 50 that belonged to the new cultures.
- **`<Culture id="x">` means two different things.** It defines a culture in `taom_spcultures.xml`.
  In `cc_body_properties.xml`, `startup_resources_config.xml` and `taom_careers.xml` it is keyed by
  culture and names an existing one. A duplicate-id check that does not separate the two reports
  every correctly wired culture as a duplicate of itself.

Check: `python tools/validate_moduledata.py --code LANDLESS_CULTURE` then `python tools/check_external_xslt.py`
Takes effect: full game restart
Code: No code changes needed for steps 1 to 4; the volunteer pool for the promoted culture is a
separate C# edit under `Main/Features/TroopProgression/RecruitmentPools/`

## Gotchas: what fails silently and what crashes

- **A missing `<Hero>` for a lord is a startup NRE.** Every `<NPCCharacter>` with `is_hero="true"` in
  `lords.xml` needs a matching `<Hero>` in `heroes.xml`, minimum
  `<Hero id="lord_X_1" faction="Faction.clan_{id}_{N}" />`. The log line is
  `Null object reference found with ID: lord_X`, and it surfaces inside
  `LordNeedsHorsesIssueBehavior.ConditionsHold` (`docs/features/kingdom-creation.md` "Known Crashes").
- **A typo'd item id does not crash and does not warn.** `GetObject<ItemObject>` returns null,
  `IsItemFitsToSlot(index, null)` returns true by design, the slot gets an empty `EquipmentElement`,
  and the troop ships naked. Only `tools/validate_all_troop_refs.py`, `tools/audit_item_refs.py` or
  `validate_moduledata.py --code BROKEN_ITEM_REF` catch it (`Equipment.cs:204-223`, `Equipment.cs:445-450`).
- **`covers_legs` and `covers_hands` do not gate the item mesh.** They clear skin-visibility bits, so
  omitting them leaves the bare body drawn through the boot or glove. An armour piece that is
  genuinely invisible has a bad `mesh=` string or an unloaded item file instead
  (`ArmorComponent.cs:196-215`).
- **A green validator proves nothing about the running game.** A new XML file or `<XmlNode>`
  registration is null in-engine until process launch, so restart Bannerlord fully before judging a
  fix (`docs/features/culture-playability-wiring.md`).
- **A binding fix reaches an existing save, slowly.** Culture objects re-deserialize on every load,
  so parties spawned after the load use the new template, but parties already on the map keep the
  roster they were built with and vanilla only tops a patrol up below full strength. A tester
  reporting "still vanilla troops" from an old save is seeing this, not a regression.
- **`tools/validate_all_troop_refs.py` sweeps a hardcoded list, not the folder.** 16
  `troops_*.xml` files exist and the list names 10, so `dunland`, `goblin`, `harad`, `mirkwood`,
  `rivendell` and `rohan` go unswept. A new culture must be appended there by hand.
  <!-- measured: ls Main/_Module/ModuleData/troops/troops_*.xml | wc -l 2026-09-05 -->
- **Only 3 schemas exist under `tools/schemas/`** (`taom_npccharacter.json`, `taom_spcultures.json`,
  `taom_equipmentsets.json`). Numeric ranges are not checked anywhere, and there is no schema for
  `taom_partyTemplates.xml` or `troop_weights.xml`. See
  [moduledata-validation.md](../features/moduledata-validation.md) for the real coverage matrix.
  <!-- measured: ls tools/schemas/*.json | wc -l 2026-09-05 -->
- **`docs/cultures.md` is stale in three places.** It puts `taom_equipment_sets_{culture}.xml` at the
  `ModuleData` root, but all 20 files sit under `Main/_Module/ModuleData/equipmentsets/`. Its "Gaps"
  table lists Umbar, Dale and Lothlorien as having no equipment set file; all three exist (Khand
  still has none, and no `khand` culture either). And it says 12 wanderers per culture: across
  `taom_wanderers.xml` there are 210 entries over 20 cultures, 17 at exactly 10, Erebor 12, Gondor
  13, Mordor 15. Author 10.
  <!-- measured: ls Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_*.xml | wc -l 2026-09-05 -->
  <!-- measured: python -c "import xml.etree.ElementTree as ET,collections;c=collections.Counter((e.get('culture') or '').replace('Culture.','') for e in ET.parse('Main/_Module/ModuleData/taom_wanderers.xml').getroot().iter('NPCCharacter'));print(sum(c.values()),len(c),sum(1 for v in c.values() if v==10),{k:v for k,v in c.items() if v!=10})" 2026-09-05 -->
- **A non-human culture needs more than `race=`.** The race must be registered in the live
  `LOTRLOME_Armory/ModuleData/skins.xml` and needs an `as_<race>_facegen` and
  `as_<race>_female_facegen` pair in `LOTRLOME_Armory/ModuleData/action_sets.xml`, each fully
  populated. A slim facegen that declares only the 14 parent action types renders the parent menu
  correctly and then T-poses the child at every later stage
  ([character-creation.md](../features/character-creation.md), [hero-race.md](../features/hero-race.md)).

## What TAOM has not written down

Three questions this chapter cannot answer from the repo, stated plainly rather than guessed.

- **There is no minimum viable kingdom.** No doc states how many clans, lords, heroes or settlements
  a culture needs before it stops crashing; what is written down is the failure list, not the floor
  (`kingdom-creation.md` "Known Crashes" plus the checklist above). The one hard number is one
  settlement, from `LANDLESS_CULTURE`.
- **There is no required-versus-optional split for the `<Culture>` attributes.** Three docs give
  three different counts and none marks which are mandatory. Take the required set from
  [cultures.md](cultures.md), which is sourced from the deserializer, and treat the eight
  party-template attributes plus the six troop attributes as always-bind.
- **Banner keys are not authored by hand and the number grammar is undecoded.** `faction_banner_key`
  is a long serialized string; keys under 100 characters are placeholders that render as plain
  blocks, so copy a working key from the clan the new one derives from. Pools:
  [banner-icon-generation.md](../reference/banner-icon-generation.md) and
  [banners-and-heraldry.md](banners-and-heraldry.md).

## Numbers in this chapter

All measured 2026-09-05 from the repo at `bannerlord-1.4.5` and the installed modules.

| Number | Command |
|---|---|
| 24 `<Culture>` blocks, 16 with `is_main_culture="true"` | `python -c "import xml.etree.ElementTree as ET;c=list(ET.parse('Main/_Module/ModuleData/taom_spcultures.xml').getroot().iter('Culture'));print(len(c),sum(1 for x in c if x.get('is_main_culture')=='true'))"` |
| 6 vanilla culture ids rewritten in the XSLT | `rg -o "Culture\[@id='[a-z]+'\]" Main/_Module/ModuleData/spcultures.xslt \| sort -u \| wc -l` |
| 210 wanderers over 20 cultures, 17 at exactly 10 (Erebor 12, Gondor 13, Mordor 15) | `python -c "import xml.etree.ElementTree as ET,collections;c=collections.Counter((e.get('culture') or '').replace('Culture.','') for e in ET.parse('Main/_Module/ModuleData/taom_wanderers.xml').getroot().iter('NPCCharacter'));print(sum(c.values()),len(c),sum(1 for v in c.values() if v==10),{k:v for k,v in c.items() if v!=10})"` |
| 20 `taom_equipment_sets_*.xml` files, all under `equipmentsets/` | `ls Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_*.xml \| wc -l` |
| 16 `troops_*.xml` files | `ls Main/_Module/ModuleData/troops/troops_*.xml \| wc -l` |
| 10 cultures in the `validate_all_troop_refs.py` hardcoded list | `sed -n '80,98p' tools/validate_all_troop_refs.py` |
| 16 recruitment-pool partials, 22 distinct `CultureMap` keys | `ls Main/Features/TroopProgression/RecruitmentPools/*.cs \| wc -l` and `rg -o 'CultureMap\["[a-z]+"\] =' Main/Features/TroopProgression/RecruitmentPools/ \| sort -u \| wc -l` |
| 383 `MBPartyTemplate` entries, 14 of them Lindon's (12 standard, 2 legacy) | `rg -c 'MBPartyTemplate id=' Main/_Module/ModuleData/taom_partyTemplates.xml` and `rg -o 'MBPartyTemplate id="[^"]*lindon[^"]*"' Main/_Module/ModuleData/taom_partyTemplates.xml \| sort` |
| 22 entries in `charactercreation/cultures.json`, using 10 distinct race values | `python -c "import json;d=json.load(open('Main/_Module/ModuleData/charactercreation/cultures.json',encoding='utf-8-sig'));r=set();[r.update(x.get('races',[])) for x in d];print(len(d),len(r))"` |
| Lindon `<Culture>` element: 336 lines | `python -c "l=open('Main/_Module/ModuleData/taom_spcultures.xml',encoding='utf-8-sig').read().split('\n');s=[i for i,x in enumerate(l) if 'id=\"lindon\"' in x][0]-1;e=[i for i,x in enumerate(l) if '</Culture>' in x and i>s][0];print(e-s+1)"` |
| 74 NPCs in `npcs_lindon.xml`, 26 of them notables and headmen | `python -c "import xml.etree.ElementTree as ET,collections;r=ET.parse('Main/_Module/ModuleData/characters/npcs_lindon.xml').getroot();c=collections.Counter(e.get('occupation') for e in r.iter('NPCCharacter'));print(sum(c.values()),sum(v for k,v in c.items() if k in ('Merchant','Preacher','Artisan','GangLeader','RuralNotable','Headman')))"` |
| Lindon templates: 6 education, 6 child, 10 lord, 98 education-equipment, 13 enlistment, 55 character-creation rosters | `python -c "import xml.etree.ElementTree as ET;p='Main/_Module/ModuleData/equipmentsets/';f=lambda q,t:sum(1 for x in ET.parse(q).getroot().iter(t) if x.get('id') and 'lindon' in x.get('id'));print(f(p+'taom_child_equipment_templates.xml','EquipmentRoster'),f(p+'taom_lord_template_equipment.xml','EquipmentRoster'),f(p+'taom_education_equipment_templates.xml','EquipmentRoster'),f(p+'taom_enlistment_equipment.xml','EquipmentRoster'),f(p+'taom_char_creation_equipment.xml','EquipmentRoster'))"` |
| 988 settlements in the live map file, 5 of them Lindon's | `python -c "import xml.etree.ElementTree as ET,collections,os;r=ET.parse(os.environ['S']).getroot();c=collections.Counter((e.get('culture') or '').replace('Culture.','') for e in r.iter('Settlement'));print(sum(c.values()),c['lindon'])"` with `S` set to the live `TAOM_Map/ModuleData/settlements.xml` |
| 14 `<race>` entries in the live `skins.xml`, 26 facegen action sets (13 pairs; `sauron` has none) | `python -c "import xml.etree.ElementTree as ET,os;print(len([e.get('id') for e in ET.parse(os.environ['K']).getroot().iter('race')]))"` and `rg -o '<action_set id="[^"]*facegen[^"]*"' <live action_sets.xml> \| wc -l` |
| 3 schemas under `tools/schemas/` | `ls tools/schemas/*.json \| wc -l` |

## Read next

- [culture-playability-wiring.md](../features/culture-playability-wiring.md): the 14-row contract and
  the party-template crash surfaces.
- [kingdom-creation.md](../features/kingdom-creation.md): the 13-file filing order, naming table and
  known crashes.
- [new-culture-authoring.md](../ai-includes/new-culture-authoring.md): armour and troop generator
  workflow, bindable-attribute table.
- [new-factions-misty-mountains-lindon.md](../features/new-factions-misty-mountains-lindon.md): the
  generator pipeline and the child-generation template requirement.
- [character-creation.md](../features/character-creation.md): CC wiring and the `as_<race>_facegen`
  recipe. [cultural-feats.md](../features/cultural-feats.md): per-culture notable counts.
- [dale.md](../features/dale.md) and [gondor-armor-revamp.md](../features/gondor-armor-revamp.md):
  two worked troop and armour authoring loops.
- [moduledata-validation.md](../features/moduledata-validation.md) and
  [testing-guide.md](../ai-includes/testing-guide.md): what the validators and shipped-data tests
  cover. [tools README](../../tools/README.md): every script named above.
