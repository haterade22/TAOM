# Dorwinion and South Rhovanion

## Overview

Dorwinion and South Rhovanion are two human kingdoms of the Northmen of Rhovanion, on the TAOM_Map
main map. Dorwinion is the Northmen vintners of the red-wine slopes;
South Rhovanion is the Northmen horse-lords of the plains. Each has its own culture (`dorwinion`,
`southrhovanion`), troop tree, kingdom, clans, lords, character creation, careers and faction card.
Dorwinion is cut from Dale (culture `sturgia`, troops `dale_*`); South Rhovanion is cut from Rohan
(culture `vlandia`). Both are **Neutral**, as
[Cardolan and Rhudaur](cardolan-rhudaur.md) and [Anduin Vale and Angáladh](anduin-vale-angaladh.md)
are: they stand outside the War of the Ring, with no start war and no row in
`diplomacy/diplomacy.json`. Both are middling in size, as [Ered Luin](ered-luin.md) is.

**Parked (2026-10-29):** the player cannot choose either realm at character creation until its
troops have armour ([parked realms](faction-map.md#parked-realms)). The character creation data is
built anyway, so unparking is a flag flip.

## Why This Exists

- **Mike's request (2026-10-29, #1062):** Mike placed the settlements in the TAOM_Map main map and
  asked for two kingdoms on them, built the way the earlier realms were. He chose both human,
  Dorwinion on the Dale scaffold and South Rhovanion on the Rohan scaffold, both Neutral, and
  parked at character creation.
- **Before:** the settlements around the Sea of Rhûn had no kingdom, clans or lords.

## Architecture

### Design Challenge

[Anduin Vale and Angáladh](anduin-vale-angaladh.md) already cut two realms from Dale, which has no
`<Culture>` block of its own. This change adds a second scaffold, Rohan, which has none either: it is
vanilla `vlandia` rewritten by `spcultures.xslt`, and its data uses the two names `rohan` and `vlandia`. Rohan also lacks the
child, education equipment and education character templates, so South Rhovanion borrows Gondor's
with the Rohan face. South Rhovanion also needed troops that Dale's tree never had: mounted
bowmen. And its cavalry must carry a harness on every horse slot.

### Solution Approach

The same recipe and generators, extended rather than copied:

| Step | Tool | What changed for the two realms |
|------|------|---------------------------------|
| Towns, castles, villages | `tools/add_map_fortifications.py`, `tools/add_map_villages.py` | A `dorwinion` and a `southrhovanion` `STYLE`; 13 fortifications and 48 villages (Dorwinion 7 and 23, South Rhovanion 6 and 25), 61 rows in all. Village names and types are `VILLAGES` in the village tool |
| Troops and party templates | `tools/generate_dorwinion_troops.py`, `tools/generate_southrhovanion_troops.py` on `tools/realm_troops.py` | Two new generators; `realm_troops.py` gained the `horsearcher` role |
| Bows | `tools/ranged_ladders.json` | The `dale` line claims Dorwinion's troop file; the `rohan` line claims South Rhovanion's |
| Culture, notables, wanderers, villager and caravan templates, education templates, rosters | `tools/promote_borrowed_cultures.py --only dorwinion` / `--only southrhovanion` | `_dale_successor` became `_northman_successor(scaffold)` over the `NORTHMAN_SCAFFOLDS` table (below) |
| Kingdom, clans, lords, heroes | `tools/generate_new_faction_kingdoms.py --only dorwinion` / `--only southrhovanion` | Two new entries; Dorwinion on lord template kind `dale`, South Rhovanion on a new Rohan pair |
| Narrative menus, careers, starting gear | `insert_new_faction_cc_menus.py`, `insert_new_faction_careers.py`, `generate_char_creation_equipment.py`, `generate_career_kits.py` | Authored text on Dale's and Rohan's mechanics; careers cloned from each scaffold's own |
| Tables keyed on culture | hand edits to each table and its compiled C# twin | Dale's row for Dorwinion and Rohan's for South Rhovanion wherever the rule is a scaffold's ("Tables keyed on culture") |

**What the promoter gained.** `_dale_successor` became `_northman_successor(scaffold)` with a
`NORTHMAN_SCAFFOLDS` table of two entries, `dale` (culture `sturgia`) and `rohan` (culture
`vlandia`). Each entry holds the culture id, the notable key prefix, the XSLT name and description
keys (`TAOM_sturgia_*` for Dale, `aom_vlandia_*` for Rohan), the basic and elite troop suffixes, the
face and the feats. Rohan has no TAOM child, education or stage-2 templates, so Gondor's are
borrowed with the Rohan face. South Rhovanion keeps Rohan's feats plains speed, loyalty and morale
and the Cavalry Dependent infantry-speed penalty, and drops Rohan's two cavalry bonuses (the
mounted recruit and upgrade cost, and the mounted wage). Its vassal reward is still Rohan's
`Item.eomer_sword` (Known gaps). The culture names are "Men of Dorwinion" and "Northmen of
Rhovanion".

**The `horsearcher` role.** `tools/realm_troops.py` gained a `horsearcher` role: a mounted bowman
that takes the ranged Bow cell and the cavalry Riding skill. South Rhovanion's Plains Horse-scout
line uses it. From L26 its melee is vanilla blades that Rohan's own troops carry, because the
`wm_rohan_*` blades fall below the band.

**Neutral.** `execution/alignment.json` says neutral, every start relationship is 0, and the
diplomacy file lists no pair. An unlisted pair is Neutral; `Natural` is an alliance tier, so it is
the wrong word here.

**Live map edits, in the order they happened.** Mike asked for the settlement rows first, so the
distance cache could be rebuilt once for them.

1. 12:28: the 61 rows were written with interim Dale and Rohan culture and owner, so Mike could
   rebuild the distance cache: Dorwinion's under Dale (`Culture.sturgia`, `clan_sturgia_1`) and
   South Rhovanion's under Rohan (`Culture.vlandia`, `clan_vlandia_1`). Every build defines those.
2. 13:42: the branch build was deployed. 13:43: the rows were retagged final (backup
   `settlements.xml.bak_retag_do_srh_20261029_134327`).
3. 13:48: the branch was redeployed after merging trunk `0799af376` (Feral Orcs).
4. 14:19:59: another session deployed a trunk build (`cd06da904`) over it. The deploy then lacked
   both cultures while the map named them, so a new campaign would throw at notable spawn. A
   14:20 session stopped there; the cause is UNVERIFIED.
5. 14:31:53: the map editor rebuilt the distance cache. It already names the 61 rows, so saves made
   before them are now on the #1041 path (they stop loading; decompile-read, not reproduced).
6. 14:55:49: the rows were put back on the interim values (backup
   `settlements.xml.bak_untag_do_srh_20261029_145549`).
7. 17:22:56: the branch, now on trunk `1ef7d1af6`, was deployed. 17:23:15: the rows were retagged
   final with `tools/oneoff/retag_do_srh_live.py` (backup
   `settlements.xml.bak_retag_do_srh_20261029_172315`), and the change was pushed as `4b207f951`.

Since 17:23 the live map names `Culture.dorwinion`, `Culture.southrhovanion` and their eight clans,
and only a build that contains `4b207f951` defines them. A checkout based before that commit fails
its ModuleData commit hook and must merge trunk before its next `./build.ps1`; a build from it
throws on a new campaign. The live backups of this change are in
`E:\taom-live-backups\2026-10-29-dorwinion-south-rhovanion\`.
This differs from Anduin Vale and Angáladh, whose rows were held back from the live file
([merging that branch](anduin-vale-angaladh.md#merging-this-branch)). The structural fix for the
shared live map is #1055. The branch contains trunk's Feral Orcs commit (`0799af376`), so
`feral_orcs` is defined for the 23 `hideout_feral_*` rows in the same live file.

### Component Diagram

```
realm_troops.py (+ horsearcher) <-- generate_{dorwinion,southrhovanion}_troops.py --> troops_<c>.xml + party templates
ranged_ladders.json ("dale" claims Dorwinion, "rohan" claims South Rhovanion) --> scaffold ladder bows
promote_borrowed_cultures.py --only dorwinion|southrhovanion (_northman_successor) --> <Culture> + npcs + rosters + wanderers + strings
add_map_fortifications.py / add_map_villages.py --> LIVE TAOM_Map settlements.xml (+ 12 loc files)
generate_new_faction_kingdoms.py --only dorwinion|southrhovanion --> Kingdom, clan_<c>_*, lord_DO*_* / lord_SRH*_*
CC menus, careers, CC gear --> character creation (parked at the faction card)
C#: VolunteerRecruitmentService.Dorwinion.cs / .SouthRhovanion.cs, the culture rows of the compiled tables
```

## Configuration

### Ids (fixed: saves reference them)

| Concept | Dorwinion | South Rhovanion |
|---------|-----------|-----------------|
| Kingdom and culture id | `dorwinion` | `southrhovanion` |
| Settlement and lord prefix | `DO` (`town_DO1`, `lord_DO1_1`) | `SRH` (`town_SRH1`, `lord_SRH1_1`) |
| Clans | `clan_dorwinion_1` to `_4` (tiers 5, 4, 4, 3) | `clan_southrhovanion_1` to `_4` (tiers 5, 4, 4, 3) |
| Lords | 16 (`lord_DO*`) | 16 (`lord_SRH*`) |
| Name keys of notables and townsfolk | `aom_dor_*` | `aom_srh_*` |
| Lord templates | `spc_dorwinion_lord_1`, `_2` | `spc_southrhovanion_lord_1`, `_2` |

### Realms

**Dorwinion**, King Ostric. Four clans of four lords make 16.

| Clan (tier) | Holds |
|-------------|-------|
| House of Vineholm (5) | Vineholm `town_DO2` (capital, prosperity 3,200), Vinewatch `castle_DO1` |
| Wardens of the Redwater (4) | Redwater `town_DO1` (3,100), Northgate Keep `castle_DO2` |
| House of Seagate (4) | Seagate `town_DO3` (3,000), Sunwall `castle_DO4` |
| Wardens of the Eastmarch (3) | Eastmarch Hold `castle_DO3` |

"Northgate" was taken, hence Northgate Keep. 23 villages (names in `tools/add_map_villages.py`),
hearth 350, with Dale's village types (Dale has no vineyard type).

**South Rhovanion**, King Vidumar. Four clans of four lords make 16.

| Clan (tier) | Holds |
|-------------|-------|
| House of Vidugard (5) | Vidugard `town_SRH2` (capital, 3,200), Horsemead `castle_SRH2` |
| Riders of Southwood (4) | Southwood `town_SRH1` (3,000), Westmark `castle_SRH4` |
| Wainwatch Riders (4) | Wainwatch `castle_SRH1` |
| Wardens of the Northmark (3) | Northmark `castle_SRH3` |

25 villages, hearth 350, with Rohan's village types, including `vlandian_horse_ranch`. Castles start
at prosperity 800 in both realms.

Dorwinion's lords use Dale's template pair `lord_S10_1` and `lord_S10_2`, the `taom_dale_*` skill
sets and `fighter_sturgia` faces, and each lady carries `<hair_tag name="sturgia" />`. South
Rhovanion's lords use a new Rohan pair, `lord_4_21_1` (knight) and `lord_4_16_1` (lady), the
`taom_rohan_knight` and `taom_rohan_lady` skill sets and TAOM's `fighter_rohan` faces; its ladies
carry `<hair_tag name="vlandia" />`.

**Banners.** Dorwinion flies gold (palette 2024) on wine red (2015) with misc icons 30011 (the
cluster, used by the realm and the House of Vineholm), 30015, 30008 and 30014. South Rhovanion flies
tan (2008) on dark earth (2013) with the misc horse icons 30006 (a sun between two horses, for the
realm), 30001, 30005 and 30004. No other clan flies either set (a plain token count). The realm
border colours are `#722721` for Dorwinion and `#4B3F0F` for South Rhovanion, because the banner
colours fall under the border palette's minimum lightness of 25. The MCM colours are
`RealmColourDorwinion` (Order 28) and `RealmColourSouthRhovanion` (29); Your Realm moved to 30.

### Settlements

| Realm | Fortifications | Villages | Scenes |
|-------|----------------|----------|--------|
| Dorwinion | 3 towns (`town_DO1` to `town_DO3`), 4 castles (`castle_DO1` to `castle_DO4`) | 23 | Dale's |
| South Rhovanion | 2 towns (`town_SRH1`, `town_SRH2`), 4 castles (`castle_SRH1` to `castle_SRH4`) | 25 | Rohan's (`vlandia_town_d`, `vlandia_town_e`, `vlandia_castle_a` to `_d`) |

Mike built the towns in the map editor from the vanilla empire templates
(`template_empire_town_*`, copied into the scene on 2026-10-29 and deleted from it since).
`town_SRH1` still has four children named `template_empire_town_*` (`_main` and three wall levels);
the names are cosmetic, an editor rename (Known gaps).

### Troop trees

Armour is the scaffold's own (Dale's for Dorwinion, Rohan's for South Rhovanion), as a placeholder.

- **Dorwinion, 21 troops (Dale kit):**
  - **Spear line:** Vineyard Levy (L6) to Vineyard Spearman, River Spearman, Redwater Guard,
    Veteran Redwater Guard and Warden of the Vines (L31).
  - **Bowmen:** the Vintner bowmen to the Master of the Vine-bow (L31).
  - **Elite:** Noble (elite basic, L11) to the River Rider and River Cavalier (horse plus harness),
    and on foot the Vineholm Retainer, Sergeant of Vineholm and Guard of Vineholm (L36 capstone).
- **South Rhovanion, 22 troops (Rohan kit):**
  - **Spear line:** Plains Levy (L6) to Plainsman Spearman, Ford Spearman, Ford Guard and Veteran
    Ford Guard (L26).
  - **Bowmen:** Plains Bowman to Southwood Marksman (L26).
  - **Elite:** Northman Youth (elite basic, L11) to the Rhovanion Rider, Lancer, Horse-thane,
    Rider-captain and Horse-lord of Rhovanion (L36 capstone, mounted).
  - **Horse-archers:** Plains Horse-scout to Veteran Horse-archer (L26), on the `horsearcher` role.
  - Militia at L11, as Rohan's.

Troop and career names are the builders' choices (Known gaps).

### Tables keyed on culture

| Table | Dorwinion | South Rhovanion |
|-------|-----------|-----------------|
| `execution/alignment.json` | neutral | neutral |
| `startup_resources_config.xml` gold / influence / playerGold | 90,000 / 400 / 2,000 | 90,000 / 400 / 2,000 |
| `configs/army_targeting.json` | north, east | central |
| `combat_mechanics` charge multiplier | 1.2 | 1.5 |
| `configs/battle_balance_config.json` survival bonus | 0.1 | 0.2 |
| `banner_bearers` | `close_shields_banner_t1` | `banner_of_the_horseman_t1` |
| `hall_of_fire`, `lord_banners` | `sturgia` | `vlandia` |
| `voice_accents` | `imperial` | `imperial` |
| Inn Rounds | wine, "red wine of Dorwinion" | beer, "Rhovanion mead" |
| `culture_doctrine` | Dale's row | Rohan's row |
| `culture_marketplace` | `armour_from` `sturgia` | `armour_from` `vlandia` |
| `special_resources_config.xml` | `lake_fish` | `marks` |
| `race_abilities` | Dale's Bard's Aim | Rohan's Forth Eorlingas |
| `crown_guard` | `dorwinion_vineholm_guard` | `southrhovanion_horse_lord` |
| `lotr_issues` Armourer's Commission | `sk_dale_chest_chivalry_a03` | `cts_rohan_armor2` |
| `realm_borders/palette.json` and MCM | `#722721`, `RealmColourDorwinion` (Order 28) | `#4B3F0F`, `RealmColourSouthRhovanion` (29); Your Realm moved to 30 |
| Faction card | `kingdom_of_dorwinion`: parked, neutral, difficulty 5 | `kingdom_of_south_rhovanion`: parked, neutral, difficulty 4 |

Compiled lists: `TaomKingdomIds`, `KnownCultureIds`, the compiled twins of the JSON rows, the liege
tags `TaomDorwinionCultureTag` and `TaomSouthRhovanionCultureTag`,
`VolunteerRecruitmentService.Dorwinion.cs` and `.SouthRhovanion.cs` (levy 6, elite 2), and the
`CareerSystemIoC` rows. `TaomStartOptionsProvider` also lists both realms.

## Careers and character creation

### Careers

| Career | Realm | Archetype | Cloned from |
|--------|-------|-----------|-------------|
| Spear-warden of Vinewatch | Dorwinion | Infantry | `dale_guardsman` |
| Bowman of Redwater | Dorwinion | Ranged | `dale_marksman` |
| River-Rider of Seagate | Dorwinion | Cavalry | `dale_outrider` |
| Rider of Vidugard | South Rhovanion | Cavalry | `eotheod_windrider` |
| Marksman of Westmark | South Rhovanion | Ranged | `marksman_of_aldburg` |
| Warden of Wainwatch | South Rhovanion | Infantry | `watchman_of_stangard` |

Each career has a `CareerSystemIoC` archetype row.

### Narrative menus

Each realm has six entries in each of the four culture-keyed menus: the scaffold's mechanics with
authored text, 96 `taom_cc_*` rows in all.

### Starting gear

110 CC rosters in all and 12 career kits. The live Armory carries the starter twins in
`LOTRLOME_items/dale/`, `LOTRLOME_items/rohan/` and `LOTRLOME_items/rhun/` `starter_kit.xml`. The
Rohan ladder bow's starter twin sits in the `rhun` file because its donor in `rohan/ranged_ladder.xml`
carries `culture="Culture.khuzait"`, and `generate_starter_kit.py` routes twins by culture.

## Key Files

| File | Purpose |
|------|---------|
| `tools/generate_dorwinion_troops.py`, `tools/generate_southrhovanion_troops.py` | each realm's tree and party templates; `--check` |
| `tools/realm_troops.py` | shared troop machinery, with the `horsearcher` role |
| `tools/promote_borrowed_cultures.py` | the culture blocks (`_northman_successor`, `NORTHMAN_SCAFFOLDS`) |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Dorwinion.cs`, `.SouthRhovanion.cs` | volunteer pools |
| `Main/_Module/ModuleData/troops/troops_dorwinion.xml`, `troops_southrhovanion.xml` | generated trees |
| `Main/_Module/ModuleData/characters/npcs_dorwinion.xml`, `npcs_southrhovanion.xml` | notables and townsfolk |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_dorwinion.xml`, `taom_equipment_sets_southrhovanion.xml` | lord template rosters |
| `tools/insert_new_faction_cc_menus.py`, `tools/insert_new_faction_careers.py` | narrative menus and the six careers |
| `Main/_Module/ModuleData/factionmap/factions.json` | the cards `kingdom_of_dorwinion` and `kingdom_of_south_rhovanion` |
| live `TAOM_Map/ModuleData/settlements.xml` | the 61 settlements (unversioned) |

## Dependencies

- Dale's and Rohan's armour, weapons and troops in the live Armory (`LOTRLOME_items/dale/`,
  `rohan/`), plus the starter twins named above.
- The `dale` and `rohan` lines of `tools/ranged_ladders.json`.
- Gondor's child, education equipment and education character templates, for South Rhovanion.
- The vanilla empire town templates, which Mike used to build the towns in the editor.
- Trunk's Feral Orcs commit (`0799af376`), which defines `feral_orcs` for the 23 hideout rows in the
  same live file.

## Tests

- `tools/tests/test_realm_troops.py` (Dorwinion in `DaleRealmTests`, South Rhovanion and the
  horse-archer role), `tools/tests/test_promote_block_src.py`, and the generator rows of
  `tools/tests/test_generator_regions_in_sync.py`.
- `FactionMapDataTests.DaleCutNeutralRealm_IsParkedNeutralAndCarriesItsFeats` (Dorwinion's card, with
  the other two Dale-cut cards) and
  `FactionMapDataTests.RohanCutNeutralRealm_IsParkedNeutralAndCarriesItsFeats` (South Rhovanion: parked,
  neutral, four bonuses).
- `ShippedClanSideCoverageTests` (`NeutralRealmIds` lists both ids).
- Menu, career, equipment and career-kit tests in `tools/tests/` extended for the new cultures.
- Pins that moved with the data, among them `SettingsFingerprintTests`, `ServerSettingsSchemaTests`,
  `LotrIssueConfigProviderTests`, `ArmourAcquisitionShippedDataTests`, `InnRoundsDataTests`,
  `ShippedRaceAbilitiesConfigTests`, `ShippedCultureDoctrinesConfigTests`,
  `TroopUpgradeSkillMonotonicityTests`, `VolunteerRecruitmentServiceTests`, `ClonedCultureFlavorTests`
  and `CareerChoicesIntegrationTests`.
- Map: `python tools/add_map_fortifications.py --check` and `python tools/add_map_villages.py --check`.

## Save Compatibility

A new campaign is required, as for [Ered Luin](ered-luin.md#save-compatibility). A save made before
the realms loads `settlements.xml` again but never the new kingdoms, clans or heroes. The distance
cache was rebuilt at 14:31:53 and already names the 61 rows, so a save made before them is expected
to stop loading (v1.5.6 decompile, not reproduced; #1041).

## Merging this branch

Done 2026-10-29 (`4b207f951`). The sequence is kept as the record and as the model for the next
realm whose live rows go in on interim owners. The order matters: the build that defines the
cultures must be deployed before the retag, and the retag script refuses otherwise.

1. Integrate trunk (it was `1ef7d1af6` by then) into the branch.
2. Run the full suites.
3. Deploy this worktree with `./build.ps1`.
4. Run `python tools/oneoff/retag_do_srh_live.py --apply`. It reads the owners from the map tool
   tables and refuses unless every row is on the interim values and the deployed ModuleData
   defines both cultures and all eight owner clans.
5. Run the gates: `python tools/validate_moduledata.py` (0 errors), both map `--check`s (0; they now
   also compare culture and owner), `python tools/check_external_loc_coverage.py` (0) and
   `python tools/generate_starter_kit.py --verify` (0).
6. Run `/verify`.
7. Commit and push.
8. Tell the Feral Orcs session (`E:\repos\TAOM`, 14 overlapping uncommitted files) to merge trunk
   before its next `./build.ps1`. Any build without this commit throws on a new campaign once the
   rows are final.
9. Move this change's live backups out of the module folders. Done: the 31 files
   (`settlements.xml.bak_mapforts_20261029_122852`, `.bak_mapvillages_20261029_122857`, the three
   `.bak_retag_do_srh_*` and `.bak_untag_do_srh_*`, the 12 loc `.bak_mapforts_` and
   `.bak_mapvillages_` pairs, BR and IT `.bak_nametags_20261029_122917`) are in
   `E:\taom-live-backups\2026-10-29-dorwinion-south-rhovanion\`, at their module-relative paths.

Owed after the merge: the in-game checks, the dedicated-server re-stage (the scene changed) and
the #763 translations.

## How to Change the Realms

The steps in [arthedain.md](arthedain.md) "How to Change the Realm" apply, with each realm's own
generator and `--only dorwinion` or `--only southrhovanion`. `generate_new_faction_kingdoms.py` and
`promote_borrowed_cultures.py` only report what would change until you add `--apply`. To unpark a
realm, set `playable` to `true` on its card ([faction-map.md](faction-map.md#parked-realms)).

## How to verify in game

Restart Bannerlord fully, then start a new campaign. The deployed build must contain `4b207f951`,
so the live map and the deployed TAOM agree.

1. Open the encyclopedia. Confirm both kingdoms ("Dorwinion", "South Rhovanion"), their 4 clans each
   and 16 lords each.
2. Find Vineholm, Redwater and Seagate, and Vidugard and Southwood, on the map. Confirm all 13 fortifications and 48 villages show their realm's banner.
3. Visit a town of each realm. Confirm notables, wanderers and guards are clothed.
4. Meet a lord's party in the field. Confirm the troops are clothed. Confirm Dorwinion's River Riders
   and South Rhovanion's riders and horse-archers are mounted, and that each horse wears a harness.
5. Check the ladies in the encyclopedia. Confirm none is bald.
6. In conversation, confirm a Dorwinion lord and a South Rhovanion lord call their liege by the
   realm's liege title (`TaomDorwinionCultureTag`, `TaomSouthRhovanionCultureTag`).
7. Open the faction map. Confirm both cards are parked: border and lore card shown, no banner, no
   confirm button, and neither is on the faction screen.
8. When unparked: start a second character in each realm and go through the narrative stages (four
   menus of six options), then the career screen (three careers each; take each and confirm its kit).
9. Run `taom.audit_settlement_entrances` (cheat mode on). Confirm no `*_DO*` or `*_SRH*` id is
   listed. Correct any listed id as [settlements.md](../modding/settlements.md) "Add" step 7
   describes (in the map editor, never in `settlements.xml` alone), then rebuild.
10. The distance cache was already rebuilt (14:31:53) and names the 61 settlements. Only rebuild it
    again if the scene changes.

## Known gaps

- **Armour is a placeholder:** Dale's for Dorwinion, Rohan's for South Rhovanion.
- **Open decisions for Mike:**
  - South Rhovanion's melee from L26 is vanilla blades, because the `wm_rohan_*` blades fall below
    the band.
  - South Rhovanion's vassal reward is still Rohan's `Item.eomer_sword`.
  - Troop and career names are the builders' choices.
  - Both realms share their scaffold's race ability (Bard's Aim, Forth Eorlingas), which are Rohan
    and Dale names.
- `town_SRH1` still has four children named `template_empire_town_*` (an editor rename).
- The faction-map regions of both cards were not checked against the fiefs (#1057 found Angáladh's
  misplaced).
- Career portraits: South Rhovanion's careers use Rohan's portraits, which exist; only Dorwinion's
  three (Dale's) are blank (#1058).
- Translations are owed to the paid run (#763): about 1,100 ids are seeded as English per language.
- The live map carries rows trunk's fort `--check` rejects until the merge ("Live map edits, in the
  order they happened").
- **Cross-session deploy hazard:** a deploy from another checkout can undo the build that defines the
  cultures. This is the fourth recurrence of the #1055 class, and the first by a deploy from another
  checkout, which no validator sees (at 14:19:59 trunk `cd06da904` was deployed over the branch build
  while the map named the final cultures). Since the 17:23 retag, every checkout must contain
  `4b207f951` before it deploys; the structural fix is #1055.
- Save compatibility: new campaign only; the distance cache already names these rows, so older
  saves stop loading (#1041).

## Changelog

- 2026-10-29: created on this branch (#1062), on Mike's map placement of the same day. The promoter
  gained the Rohan scaffold (`_dale_successor` became `_northman_successor` over `NORTHMAN_SCAFFOLDS`),
  `realm_troops.py` gained the `horsearcher` role, and the `dale` and `rohan` ranged-ladder lines
  claim the new troop files. The 61 live map rows went in first with interim Dale and Rohan owners;
  a live retag was undone after another session's deploy, so the final retag waited for the merge
  sitting.
- 2026-10-29: merged as `4b207f951`, deployed at 17:22:56, live rows retagged at 17:23:15. The map
  tools' `--check` now compares culture, owner and village binding, and the retag script refuses
  unless the deployed build defines its cultures and clans (review:
  [rca-dorwinion-south-rhovanion-2026-10-29.md](../reviews/rca-dorwinion-south-rhovanion-2026-10-29.md)).

## GitHub Issue

- **Issue:** #1062.
- **Status:** merged (`4b207f951`) and closed with `triage-needs-ingame`; the in-game checks
  ("How to verify in game"), the dedicated-server re-stage and the #763 translations are owed.
