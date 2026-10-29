# Anduin Vale and Angáladh

## Overview

Anduin Vale and Angáladh are two human kingdoms of the Vales of Anduin and western Mirkwood, on the
TAOM_Map main map between the Misty Mountains and the forest. Anduin Vale is the Beornings and
Woodmen of the Vales of Anduin; Angáladh is the Woodmen of western Mirkwood. Each has its own
culture (`anduinvale`, `angaladh`), troop tree, kingdom, clans, lords, character creation, careers
and faction card. Both are cut from Dale (culture `sturgia`, troops `dale_*`). Both are
**Neutral**, as [Cardolan and Rhudaur](cardolan-rhudaur.md) are: they stand outside the War of the
Ring, with no start war and no row in `diplomacy/diplomacy.json`.

**Parked (2026-10-28):** the player cannot choose either realm at character creation until its
troops have armour ([parked realms](faction-map.md#parked-realms)). The character creation data is
built anyway, so unparking is a flag flip.

## Why This Exists

- **Mike's request (2026-10-28, #1053):** Mike placed the settlements in the TAOM_Map main map and
  asked for two kingdoms on them, built the way the earlier realms were ([Ered Luin](ered-luin.md),
  [Cardolan and Rhudaur](cardolan-rhudaur.md)). He chose both human, both cut from Dale, both
  Neutral and weak in numbers, as Cardolan.
- **Before:** the Anduin and western Mirkwood settlements had no kingdom, clans or lords.

## Architecture

### Design Challenge

The recipe ([ered-luin.md](ered-luin.md), [cardolan-rhudaur.md](cardolan-rhudaur.md)) cuts a realm
from a culture that has a `<Culture>` block of its own. Dale has none: it is vanilla `sturgia`
rewritten by `spcultures.xslt` ([dale.md](dale.md)), and its data uses two names. `dale` names its
troops, notables, wanderers, rosters and party templates; `sturgia` names the culture id, the module
strings, and the lord-template and enlistment rosters. The promoter had to render a culture from
the XSLT and take each shared file from the right name. Dale also lacks three things a new game
needs: the child, education equipment and education character templates. Without them a new game
throws or the age-8 stage crashes.

### Solution Approach

The same recipe and generators, extended rather than copied:

| Step | Tool | What changed for the two realms |
|------|------|---------------------------------|
| Towns, castles, villages | `tools/add_map_fortifications.py`, `tools/add_map_villages.py` | An `anduinvale` and an `angaladh` `STYLE` (Gundabad's menus, meshes and building scenes with Dale's gate rotations, Dale's sturgia town and castle scenes); 11 fortifications and 30 villages, town first, written at the end of the live file in the merge sitting ("Merging this branch") (Anduin Vale 16 villages, Angáladh 14); village names and types are `VILLAGES` in the village tool |
| Troops and party templates | `tools/generate_anduinvale_troops.py`, `tools/generate_angaladh_troops.py` on `tools/realm_troops.py` | Two new generators; the self-check now also counts `great_axe` as two-handed |
| Bows | `tools/ranged_ladders.json` | The `dale` line's `files` list both troop files; archers appear only at L6, 16, 21, 26 and 31, where the line has cells |
| Culture, notables, wanderers, villager and caravan templates, education templates, rosters | `tools/promote_borrowed_cultures.py --only anduinvale` / `--only angaladh` | `_dale_successor()`, `render_xslt_culture()`, `block_src`, `id_overrides` and `lord_templates` (below) |
| Kingdom, clans, lords, heroes | `tools/generate_new_faction_kingdoms.py --only anduinvale` / `--only angaladh` | Both entries in `REGENERATABLE`, lord template kind `dale` |
| Narrative menus, careers, starting gear | `insert_new_faction_cc_menus.py`, `insert_new_faction_careers.py`, `generate_char_creation_equipment.py`, `generate_career_kits.py` | Authored text on Dale's mechanics; Dale's Guardsman cloned for the foot careers, its Marksman and Outrider for the bow and horse careers |
| Tables keyed on culture | hand edits to each table and its compiled C# twin | Dale's row wherever the rule is a Dale one ("Tables keyed on culture") |

**What the promoter gained.** `_dale_successor()` builds the target from Dale's settings.
`render_xslt_culture()` renders Dale's `<Culture>` block through lxml. `block_src` names, per shared
file, which source culture supplies the block: `sturgia` for the three files that use the culture
id, and `gondor` for the child, education equipment and education character templates. The token
`Culture.sturgia` is renamed through `id_overrides` (`"Culture.sturgia": "Culture.<c>"`, a
whole-token rename). `lord_templates` and `add_lord_templates()` give each realm its own two lord
templates, written on the `lord_face` template: a hero keeps its template's culture, and Dale lists
vanilla Sturgian leaders (`CultureLordTemplateTests`). `capital_position` falls back to the capital's
scene entity when the capital has no live row, which is the case until the map rows are applied.
Dale's blocks are rendered from the installed vanilla `spcultures.xml`, so an engine bump that
changes vanilla `sturgia` shows as `--check` drift on both targets; `--apply` fixes it. The tutors take `fighter_sturgia`, an `id_overrides`
row. Both realms carry Dale's two feats, `taom_dale_plain_speed` and `taom_dale_loyalty`.

**A weak pair.** Ruling clans are tier 4 and the others 3 and 2. Towns start at 2,800 prosperity
(the capitals) and 2,400, castles at 800, villages at a hearth of 300, and neither culture has a row
in `tools/settlement_economy_floor.json`.

**Neutral.** `execution/alignment.json` says neutral, every start relationship is 0, and the
diplomacy file lists no pair. An unlisted pair is Neutral; `Natural` is an alliance tier, so it is
the wrong word here.

**Prefixes.** `AND` begins with Arthedain's `AN`, but every prefix reader takes all the leading
capitals (`Generate-Settlements.ps1` `'^([A-Z]+)'`, `Settlement-Breakdown.ps1`,
`generate_new_faction_kingdoms.py`), so no tool reads `AND` as `AN`.

### Component Diagram

```
realm_troops.py <-- generate_{anduinvale,angaladh}_troops.py --> troops_<c>.xml + party templates
ranged_ladders.json ("dale" line claims both files) --> Dale's ladder bows
promote_borrowed_cultures.py --only anduinvale|angaladh --> <Culture> + npcs + rosters + wanderers + strings
add_map_fortifications.py / add_map_villages.py --> LIVE TAOM_Map settlements.xml (+ 12 loc files)
generate_new_faction_kingdoms.py --only anduinvale|angaladh --> Kingdom, clan_<c>_*, lord_AV*_* / lord_AND*_*
CC menus, careers, CC gear --> character creation (parked at the faction card)
C#: VolunteerRecruitmentService.AnduinVale.cs / .Angaladh.cs, the culture rows of the compiled tables
```

## Configuration

### Ids (fixed: saves reference them)

| Concept | Anduin Vale | Angáladh |
|---------|-------------|----------|
| Kingdom and culture id | `anduinvale` | `angaladh` |
| Settlement and lord prefix | `AV` (`town_AV1`, `lord_AV1_1`) | `AND` (`town_AND1`, `lord_AND1_1`) |
| Clans | `clan_anduinvale_1` to `_4` | `clan_angaladh_1` to `_3` |
| Troops | `anduinvale_*` | `angaladh_*` |
| Name keys of notables and townsfolk | `aom_av_*` | `aom_ang_*` |
| Lord templates | `spc_anduinvale_lord_1`, `_2` | `spc_angaladh_lord_1`, `_2` |

### Realms

**Anduin Vale**, Chieftain Grimbeorn (son of Beorn). The ruler's title is "Chieftain" for both
genders. Four clans of four lords make 16.

| Clan (tier) | Holds |
|-------------|-------|
| House of Beorn (4) | Beorn's Hall `town_AV1` (capital), Eastbank `castle_AV4` |
| Folk of the Old Ford (3) | Old Ford `town_AV2`, High Pass Watch `castle_AV2` |
| Wardens of the Carrock (2) | Carrock `castle_AV1` |
| Riders of the Gladden (2) | Gladden Hold `castle_AV3` |

**Angáladh**, King Ingomar, King and Queen as titles. Three clans of four lords make 12.

| Clan (tier) | Holds |
|-------------|-------|
| House of Angáladh (4) | Woodmen-town `town_AND1` (capital), Ironwood Hall `castle_AND4` |
| Woodwards of the Forest Road (3) | Forest Gate `castle_AND3`, Thornhold `castle_AND1` |
| Wardens of Emyn Duir (2) | Emyn Duir Watch `castle_AND2` |

Lords use Dale's template pair `lord_S10_1` and `lord_S10_2` and Dale's skill sets. Faces are drawn
from `fighter_sturgia`, and each lady carries `<hair_tag name="sturgia" />`. Angáladh's lords ride
Dale's `charger` although its troops are on foot (Known gaps).

Banners: Anduin Vale flies the Anduin Vale icons (20006 bear's head for the realm and the House of
Beorn, 20004 bee, 20003 paw, 20002 horse trident) in honey gold (2024) on Beorning brown (palette
2007, `0xFF654D37`). Angáladh flies icons no other clan flies (Mike, 2026-10-28): Silvan 17000 for
the realm and the House of Angáladh, 17001 for the Woodwards of the Forest Road and the plain sun
20001 for the Wardens of Emyn Duir, in off-white (palette 2022, `#F2F4F7`, the kingdom's `color2`, which the engine paints a clan's sigil in) on forest green (palette 2021, `0xFF4F6F5A`).
Mirkwood's Nos Galengrim flies 17006, 17007 and 17008.

### Settlements

| Id | Name | Kind | Scene | Prosperity |
|----|------|------|-------|-----------|
| `town_AV1` | Beorn's Hall (capital) | town | `sturgia_town_e` | 2,800 |
| `town_AV2` | Old Ford | town | `sturgia_town_a` | 2,400 |
| `town_AND1` | Woodmen-town (capital) | town | `sturgia_town_d` | 2,800 |
| `castle_AV4` | Eastbank | castle | `sturgia_castle_c` | 800 |
| `castle_AV2` | High Pass Watch | castle | `sturgia_castle_003` | 800 |
| `castle_AV1` | Carrock | castle | `taom_dale_castle_001_forceatmo` | 800 |
| `castle_AV3` | Gladden Hold | castle | `sturgia_castle_c` | 800 |
| `castle_AND4` | Ironwood Hall | castle | `sturgia_castle_003` | 800 |
| `castle_AND3` | Forest Gate | castle | `taom_dale_castle_001_forceatmo` | 800 |
| `castle_AND1` | Thornhold | castle | `sturgia_castle_c` | 800 |
| `castle_AND2` | Emyn Duir Watch | castle | `sturgia_castle_003` | 800 |

The castle scenes rotate Dale's three. Angáladh's castles keep the empire map icons Mike placed. The 41
rows (11 fortifications and 30 villages, town first) go at the end of the live
`TAOM_Map/ModuleData/settlements.xml`, which has 1,141 rows today because they are held back
("Merging this branch"). Written once, the file had 1,182 rows. That built state is kept beside the
file and each of the 12 `loc_settlements.xml` as `*.bak_av_and_built_20261028_200935`; the state
before it is `*.bak_mapforts_20261028_181850`.

**Scene fix.** The entity `castle_village_AN1_1` among Angáladh's castles was renamed
`castle_village_AND1_1` in the live `Main_map/scene.xscene` (one occurrence; backup
`E:\taom-live-backups\scene\scene.xscene.bak_and_rename_20261028_181457`).

### Troop trees

22 troops each, with the militia and the tavern copy. Armour is Dale's, as a placeholder, and never
sits on a troop below the lowest level a Dale troop wears it (`DaleRealmTests` in
`tools/tests/test_realm_troops.py`). Militia are at L6 and veterans at L16, Dale's own levels.

- **Anduin Vale:**
  - **Axe line:** Vale Levy (L6) to Vale Axeman (L11), Beorning Axeman (L16), Axe-warrior (L21),
    Veteran Beorning (L26) and Beorning Champion (L31).
  - **Great-axe branch:** Great-axe (L21) and Axe-lord (L26).
  - **Bowmen:** Woodman bowmen, L16 to L26.
  - **Elite:** Beorning Youth (elite basic, L11) to the Vale Riders (L16 and L21, on
    `sturgia_horse` or `charger` with `chain_horse_harness`), the Carrock Guard (L16), the
    Bear-guard (L26) and the Bear-warden of the Carrock (L36 capstone, `dale_poleaxe_a`).
- **Angáladh, on foot only:**
  - Woodman Levy (L6) to the spear line (to Forest Warden, L31), the axe line (to Ironwood Hewer,
    L26) and the bowmen (to Master Woodman Archer, L31).
  - Ironwood Youth (L11) to Ironwood Guard, Ironwood Sentinel and the Guard of Woodmen-town (L36
    capstone). The Youth also upgrades into the Woodman Bowman line (`generate_angaladh_troops.py:181`).

### Wanderers

Ten wanderers each, with 160 authored backstory rows in `taom_wanderer_strings.xml`. Dale's surnames
Bainling and Ravenhild were remapped (Anduin Vale: Grimling, Bearhild; Angáladh: Oakling, Rowanhild).
Fellrider names no Dale person or place and is kept.

### Culture

Culture names are "Beornings" and "Woodmen". Both cultures take Dale's feats (plain speed and
loyalty), so each faction card carries two bonuses.

### Tables keyed on culture

| Table | Anduin Vale | Angáladh |
|-------|-------------|----------|
| `execution/alignment.json` | neutral | neutral |
| `startup_resources_config.xml` gold / influence / playerGold | 85,000 / 400 / 2,000 | 90,000 / 400 / 2,000 |
| `configs/army_targeting.json` | north, east | north, east |
| `combat_mechanics` charge multiplier | 1.2 | 1.2 (inert: no rider) |
| `configs/battle_balance_config.json` survival bonus | 0.1 | 0.1 |
| `banner_bearers` | `close_shields_banner_t1` | same |
| `hall_of_fire`, `lord_banners` | `sturgia` | `sturgia` |
| `voice_accents` | `imperial` | `imperial` (a custom id with no row is silent) |
| Inn Rounds | `beer`, "Beorn's honey mead" | `beer`, "forest ale" |
| `culture_doctrine` | Dale's row | Dale's row |
| `culture_marketplace` | `armour_from` `sturgia`, armour and weapon `<Stock>` | same |
| `special_resources_config.xml` | Marks | Marks |
| `race_abilities` | on Dale's `sturgia` key (Bard's Aim) | same |
| `settlement_guards` | `<Spear>` row | `<Spear>` row |
| `crown_guard` | `anduinvale_bear_warden` | `angaladh_woodmen_guard` |
| `realm_borders/palette.json` and MCM | `#654D37`, `RealmColourAnduinVale` (Order 26) | `#587A4A`, `RealmColourAngaladh` (27); Your Realm moved to 28 |
| Liege titles | Chieftain of Anduin Vale | King and Queen of Angáladh |
| `lotr_issues` | Armourer's Commission (`sk_dale_chest_infrantry_a03`); on Dale's Deep Seam row | Armourer's Commission (`sk_dale_chest_archer_a03`); on the same row |
| Faction card | `anduin_vale`: `game_faction` `anduinvale`, neutral, parked, difficulty 5 | `kingdom_of_angaladh`: `angaladh`, neutral, parked, difficulty 6 |

The starting gold is the 150-day runway of the startup file's rule, at 5.77 and 6.04 denars a head.

Compiled lists: `TaomKingdomIds`, `KnownCultureIds`, the compiled twins of the JSON rows,
`RealmBordersSettingsProvider`, `CoopSettingsRelevance`, `TaomAnduinValeCultureTag` and
`TaomAngaladhCultureTag`, and `VolunteerRecruitmentService.AnduinVale.cs` and `.Angaladh.cs` (levy
6, elite 2).

Pins that moved with the data: `TaomSettings` reflects 471 settings (was 469) and the server schema
501 (499); LotR issues 80 (78); commissions 24 (22); inn rounds 28 (26); race-ability cultures 22
(20); `horseStand` cultures 7 (5). In `tools/tests`, the militia pin is 42 (38), upgrade monotonicity
84 (76), template power 54 (50) and kingdom names 84 (78).

## Careers and character creation

### Careers

| Career (id) | Realm | Archetype | Cloned from |
|-------------|-------|-----------|-------------|
| Axe-warden of Beorn's Hall (`axe_warden_of_beorns_hall`) | Anduin Vale | Infantry | `dale_guardsman` |
| Archer of Eastbank (`archer_of_eastbank`) | Anduin Vale | Ranged | `dale_marksman` |
| Rider of the Vales (`rider_of_the_vales`) | Anduin Vale | Cavalry | `dale_outrider` |
| Warden of the Forest Gate (`warden_of_the_forest_gate`) | Angáladh | Infantry | `dale_guardsman` |
| Hewer of Ironwood Hall (`hewer_of_ironwood_hall`) | Angáladh | TwoHanded | `dale_guardsman` |
| Woodward of the Forest Road (`woodward_of_the_forest_road`) | Angáladh | Ranged | `dale_marksman` |

Each career has a `CareerSystemIoC` archetype row.

### Narrative menus

Each realm has six entries in each of the four culture-keyed menus: Dale's mechanics with authored
text, 96 `taom_cc_*` rows in all. `charactercreation/cultures.json` and `cc_body_properties.xml`
carry rows equal to `sturgia`'s except the start, `town_AV1` and `town_AND1`.

### Starting gear

55 CC rosters each, Angáladh's with `no_mount`. There are 12 career kits. The Angáladh hewer's
second slot is the one-handed axe `dale_war_axe_b`, not a throwing weapon, because no Dale troop
throws (`THROWN_FOR` in `generate_career_kits.py`). The Anduin Vale
cavalry kit uses Cardolan's `saddle_horse` and `starter_cavalry_gondor_horse_armor_a`. The live
Armory's `LOTRLOME_items/dale/starter_kit.xml` is not new: it has existed since 2026-09-12.
`generate_starter_kit.py` rewrote it, keeping every shipped id and adding 12 items; the
`.bak-starterkit` sidecars date from 2026-10-23, because the tool takes them once. To undo, revert
the rosters, then run the tool with `--apply --allow-shrink`. The `dale` folder is already
registered, so the file loads at the next campaign start or save load.

## Key Files

| File | Purpose |
|------|---------|
| `tools/generate_anduinvale_troops.py`, `tools/generate_angaladh_troops.py` | each realm's tree and party templates; `--check` |
| `tools/realm_troops.py` | shared troop machinery |
| `tools/promote_borrowed_cultures.py` | the culture blocks (`_dale_successor`, `render_xslt_culture`, `block_src`, `lord_templates`) |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.AnduinVale.cs`, `.Angaladh.cs` | volunteer pools |
| `Main/_Module/ModuleData/troops/troops_anduinvale.xml`, `troops_angaladh.xml` | generated trees |
| `Main/_Module/ModuleData/characters/npcs_anduinvale.xml`, `npcs_angaladh.xml` | notables and townsfolk |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_anduinvale.xml`, `taom_equipment_sets_angaladh.xml` | lord template rosters |
| `tools/insert_new_faction_cc_menus.py`, `tools/insert_new_faction_careers.py` | narrative menus and the six careers |
| `Main/_Module/ModuleData/factionmap/factions.json` | the cards `anduin_vale` and `kingdom_of_angaladh` |
| live `TAOM_Map/ModuleData/settlements.xml` | the 41 settlements (unversioned), the last rows of the file |

## Dependencies

- Dale's armour, weapons and troops in the live Armory (`LOTRLOME_items/dale/`), plus the new
  `starter_kit.xml` there.
- Dale's ladder bows, through the `dale` line of `tools/ranged_ladders.json`.
- Gondor's child, education equipment and education character templates, and Cardolan's
  `saddle_horse` and `starter_cavalry_gondor_horse_armor_a`.
- The banner icons 20002 to 20006 (Anduin Vale), 17000 and 17001 (Silvan) and 20001 (the plain sun).

## Tests

- `tools/tests/test_realm_troops.py` (`DaleRealmTests`), `tools/tests/test_promote_block_src.py`
  (new), and the generator rows of `tools/tests/test_generator_regions_in_sync.py`.
- `TAOM.Tests/Core/CultureLordTemplateTests.cs`: a realm's heroes keep their own lord template's
  culture.
- `FactionMapDataTests.DaleCutNeutralRealm_IsParkedNeutralAndCarriesItsFeats`: both cards parked,
  neutral, with two bonuses.
- Menu, career, equipment and career-kit tests in `tools/tests/` extended for the new cultures.
- Pins moved with the data ("Tables keyed on culture"), among them `SettingsFingerprintTests`,
  `ServerSettingsSchemaTests`, `LotrIssueConfigProviderTests`, `ArmourAcquisitionShippedDataTests`,
  `InnRoundsDataTests`, `ShippedRaceAbilitiesConfigTests`, `ShippedCultureDoctrinesConfigTests`,
  `TroopUpgradeSkillMonotonicityTests` and `VolunteerRecruitmentServiceTests`.
- Map: `python tools/add_map_fortifications.py --check` and `python tools/add_map_villages.py --check`.
- The final gate run is quoted in `docs/reviews/rca-anduin-vale-angaladh-2026-10-28.md`.

## Save Compatibility

A new campaign is required, as for [Ered Luin](ered-luin.md#save-compatibility). A save made before
the realms loads `settlements.xml` again but never the new kingdoms, clans or heroes. The 41 new rows
sit at the end of the file, so the unfinished rows are dropped and every older settlement has
already loaded. Once the rows are applied and the distance cache is rebuilt for them, a save made before them is
expected to stop loading (v1.5.6 decompile, not reproduced; #1041).

## How to Change the Realms

The steps in [arthedain.md](arthedain.md) "How to Change the Realm" apply, with each realm's own
generator and `--only anduinvale` or `--only angaladh`. `generate_new_faction_kingdoms.py` and
`promote_borrowed_cultures.py` only report what would change until you add `--apply`. To unpark a
realm, set `playable` to `true` on its card ([faction-map.md](faction-map.md#parked-realms)).

## Merging this branch

The live `TAOM_Map` is shared by every checkout and the deployed game. With the 41 rows in it, the
live map named cultures and clans that trunk and the deployed TAOM lack: the base checkout's
validator gave 52 errors and blocked every other checkout's ModuleData commit. So the rows were
restored out of the live file after the build (the change was insert-only), and they return in the
merge sitting. The scene rename and the Armory starter kit stay. The structural fix is #1055.

0. **Guard.** The live `settlements.xml` and all 12 `loc_settlements.xml` must still hash-match
   `*.bak_mapforts_20261028_181850`. Whether or not they do, never copy the
   `*.bak_av_and_built_*` files back over them: that would undo any live edit made since 18:18.
   They are only the reference for step 2.
1. **Apply, in one sitting, from this branch:** `python tools/add_map_fortifications.py --apply`,
   `python tools/add_map_villages.py --apply`, then
   `python tools/name_tags.py --target live-map --apply --allow-live-write`.
2. **Compare.** If step 0's guard held, `cmp` the 13 written files against
   `*.bak_av_and_built_20261028_200935`. They are expected to be identical (the review proved it).
3. **Gates.** Until step 1 has run, the branch's own `python tools/validate_moduledata.py` reports
   53 `LANDLESS_CULTURE` errors, so make the commit only after this step. Then
   `python tools/validate_moduledata.py` reports 0 errors, both map `--check` runs exit
   0, `python tools/check_external_loc_coverage.py` exits 0, and
   `python tools/generate_starter_kit.py --verify` exits 0 (the Dale starter twins live only in the
   unversioned Armory). Never run `--update-baseline` while the rows are held back.
4. **Commit, push, deploy** (`./build.ps1`) right after, so trunk, the deployed TAOM and the live
   map agree. Restart Bannerlord, start a new campaign and run the in-game checks below. Then
   rebuild the distance cache (#1041), re-stage the dedicated server (the scene changed) and do
   the TAOM_Map part of #763.
5. **Clean up.** Move every backup this branch and the merge sitting write out of the module
   folders: `*.bak_mapforts_20261028_181850`, `*.bak_mapvillages_20261028_181855`, the BR and IT
   `*.bak_nametags_20261028_191309`, `*.bak_av_and_built_20261028_200935`, and the new
   `*.bak_mapforts_<stamp>`, `*.bak_mapvillages_<stamp>` and `*.bak_nametags_<stamp>` that the merge
   sitting writes.
6. **Other worktrees.** Worktrees based before the merge fail their ModuleData commit hook until
   they merge trunk, because they lack the new cultures.

## How to verify in game

Restart Bannerlord fully, then start a new campaign. Precondition: the rows are applied ("Merging this branch"), so both map `--check` runs exit 0.

1. Open the encyclopedia. Confirm both kingdoms ("Anduin Vale", "Angáladh"), their 4 and 3 clans and
   16 and 12 lords.
2. Find Beorn's Hall and Old Ford along the Anduin, and Woodmen-town in western Mirkwood. Confirm
   all 11 fortifications and 30 villages show their realm's banner.
3. Visit a town of each realm. Confirm notables, wanderers and guards are clothed.
4. Meet a lord's party in the field. Confirm the troops are clothed. Anduin Vale's Vale Riders must
   be mounted, and Angáladh's troops on foot.
5. Check the ladies in the encyclopedia. Confirm none is bald.
6. In conversation, confirm an Anduin Vale lord calls his liege the Chieftain of Anduin Vale and an
   Angáladh lord calls his the King of Angáladh.
7. Open the faction map. Confirm both cards are parked: border and lore card shown, no banner, no
   confirm button, and neither is on the faction screen.
8. When unparked: start a second character in each realm and go through the narrative stages
   (four menus of six options, no horse for Angáladh, the campaign starting at `town_AV1` or
   `town_AND1`), then the career screen (three careers each; take each and confirm its kit).
9. Run `taom.audit_settlement_entrances` (cheat mode on). Confirm no `*_AV*` or `*_AND*` id is
   listed; older ids such as `castle_village_MM1_2` may still be. Correct any listed id as
   [settlements.md](../modding/settlements.md) "Add" step 7 describes (in the map editor, never in
   `settlements.xml` alone), then rebuild.
10. Rebuild the distance cache (Map Tools, Rebuild Now) for the 41 new settlements.

## Known gaps

- **The branch validator fails while the rows are held back.** `python tools/validate_moduledata.py`
  on this branch reports 53 `LANDLESS_CULTURE` errors, because the two cultures own no live
  settlement. Commit only inside the merge sitting, after the step 3 gates. Never re-apply the rows
  mid-build to make that error go away.
- **The map rows are held back.** The live `TAOM_Map` has 1,141 rows today; the 41 rows return in
  the merge sitting ("Merging this branch"). Once they are applied, start no new campaign and
  re-stage no dedicated server until the deploy: the live map and the deployed TAOM must agree.
  The channel check is in [release-process.md](../reference/release-process.md).
- The armour is Dale's, as a placeholder. Dale's ids carry no tier keyword, so
  `ARMOUR_MESH_TIER_LADDER` cannot check them.
- Harad's tier 6 now shows a `CROSS_CULTURE_ARMOUR_INVERSION` warning: the two realms field
  Dale-level armour at tier 4 (204 and 201), which lifts the comparison median above Harad's line.
  This is a pre-existing under-dressing of Harad and was not changed.
- **Open decisions for Mike:**
  - Both realms share Dale's race ability "Bard's Aim", which is a Dale person's name.
  - The cards carry 2 bonuses (two feats), not 3.
  - Angáladh's lords ride Dale's `charger` although its troops are on foot.
  - Starting gold sits on the 150-day runway tier; the 100-day tier gives about 60,000.
  - Banner icons for Angáladh are Silvan ornaments and a plain sun, not a tree. A design for Mike to
    review. Icons 17000 and 17001 are also the `keystone_icon` of Mirkwood's Silvan Archer and Elk
    Rider careers (`career_system/taom_careers.xml` lines 824 and 845), which is a career screen,
    not a banner. One Lindon clan flies the same background and sigil colours (2021 with 2022) with
    icon 31003.
  - The name "Angáladh" is read as "iron-tree" in the kingdom text (Sindarin `ang` plus `galadh`).
    Mike's to confirm.
- Tournaments field vanilla Sturgian teams. Lord and rebellion templates are the realms' own now,
  children's clothes are Gondor's, and the wanderers' surnames are remapped from Dale's.
- Angáladh's faction-map region is drawn east of Erebor, not over its fiefs in western Mirkwood
  (#1057).
- The six careers show a blank portrait, as Dale's three do (#1058).
- The cloned companion rosters `npc_companion_equipment_template_anduinvale` and `_angaladh` carry
  `dale_great_axe_a` beside a shield, inherited from Dale's own roster. `audit_polearm_shield_parity.py`
  warns and passes.
- Load timing: the full restart is still needed for the new TAOM.dll and the six new
  `SubModule.xml` entries, even though the Dale starter kit loads at the next campaign start.
- The 48 live Armory language files (`loc_LOTRAOM_horses`, `loc_LOTRLOME_crafting_pieces`,
  `loc_arnor` and `loc_harad`, in 12 languages) were written by
  `translate_with_claude.py --sync-ids` at 19:06, run with its module default `all`: an insert-only
  English seeding of 102 earlier Armory ids per language, with no backup (#1056). Seed with
  `--module TAOM`.
- Translations are seeded into 12 languages (free) and owed to the paid run (#763). The live BR and
  IT grammar tags (41 rows each) are written by merge step 1 (`name_tags.py --target live-map`), not
  now, and each `taom_map` count in `tools/external-loc-coverage-baseline.json` rose by 41.

## Changelog

- 2026-10-28: created on branch `feat/anduin-vale-angaladh` (#1053), on Mike's map placement of the
  same day. The promoter gained the Dale source (`render_xslt_culture`, `block_src`, `id_overrides`,
  `lord_templates`), `realm_troops.py` counts `great_axe` as two-handed, the `dale` ranged-ladder
  line lists both troop files, and the entity `castle_village_AN1_1` became `castle_village_AND1_1`
  in the live scene. After the deep review the 41 map rows were held back from the live file and
  are applied in the merge sitting, and Angáladh's banner icons changed to ones no other clan flies.

## GitHub Issue

- **Issue:** #1053.
- **Status:** built in data; map rows held back until the merge sitting; in-game check owed
  ("How to verify in game"), then the distance-cache rebuild.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/cardolan-rhudaur.md](./cardolan-rhudaur.md)
- [docs/features/ered-luin.md](./ered-luin.md)
- [docs/features/kingdom-creation.md](./kingdom-creation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)

<!-- backlinks-end -->
