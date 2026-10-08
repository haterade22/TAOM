# Arthedain

## Overview

The Kingdom of Arthedain is a playable what-if realm: the North-kingdom of Arnor in which Arvedui's
line survived. It has its own culture (`arthedain`), a Dúnedain troop tree built on the Arnor armour
in the Armory, ten fortifications and twenty-four villages in the north-west of the TAOM_Map main
map, five clans under King Arvegil II at Fornost Erain, and full character-creation support. It is
weak by design: few lords, small parties, poor towns, and at war with Gundabad (Angmar) from day one.

## Why This Exists

- **Vanilla behavior:** Bannerlord has no northern Dúnedain realm, and TAOM's Eriador held only
  Rivendell, Lindon, the goblin holds and Dunland's settlements.
- **TAOM requirement:** Mike asked for a new kingdom of Arthedain (Arnor), using the Arnor armour
  pack already in the Armory, placed on the TAOM_Map main map, and easily overrun by Angmar, Dunland
  or Gundabad.
- **Without this feature:** the faction-select map carried a non-playable `kingdom_of_arthedain`
  placeholder pointing at Rohan's culture, and the Arnor armour had no wearers.

## Architecture

### Design Challenge

A new kingdom touches about thirty surfaces in three modules (the repo, the live unversioned
TAOM_Map and the live unversioned Armory) and several of them crash rather than fail quietly:
a culture that owns no settlement (#374), a lord without a `<Hero>` row, missing stage-2 education
templates (#354), and a settlement row whose scene entity does not exist (#269). Each surface was
written by a generator rather than by hand, so the whole kingdom can be regenerated and checked.

### Solution Approach

Data only, no new `Main/Features/` module. The pieces, in the order they must be built:

| Step | Tool | Writes |
|------|------|--------|
| Troop tree and the 9 soldier party templates | `tools/generate_arthedain_troops.py` | `troops/troops_arthedain.xml`, a `TAOM-ARTHEDAIN-PT` region of `taom_partyTemplates.xml` |
| Ranger bow ladder | `tools/ranged_ladders.json` line `arthedain`, `tools/generate_ranged_ladder_items.py` | live `LOTRLOME_items/arnor/ranged_ladder.xml` |
| Culture, townsfolk, notables, wanderers, villager and caravan templates, education templates, lord and child rosters | `tools/promote_borrowed_cultures.py --only arthedain` (Gondor as the scaffold) | `taom_spcultures.xml`, `characters/npcs_arthedain.xml`, `equipmentsets/taom_equipment_sets_arthedain.xml`, shared files |
| Towns and castles | `tools/add_map_fortifications.py` | live `TAOM_Map/ModuleData/settlements.xml` and 12 `loc_settlements.xml` |
| Villages | `tools/add_map_villages.py` | the same live files |
| Kingdom, clans, lords, heroes | `tools/generate_new_faction_kingdoms.py --only arthedain` | `taom_spkingdoms.xml`, `characters/{clans,lords,heroes}.xml` |
| Narrative menus and their strings | `tools/insert_new_faction_cc_menus.py` | the four culture-scoped menus, `taom_cc_strings.xml` |
| Careers | `tools/insert_new_faction_careers.py` | `career_system/*`, `charactercreation/career_menu.json` |
| Starting gear | `tools/generate_char_creation_equipment.py --append arthedain`, then `generate_starter_kit.py`, then `wire_starter_kit_rosters.py` | `taom_char_creation_equipment.xml`, live starter kits |

The Gondor scaffold was chosen because the Dúnedain of the North are Gondor's own people; every
Gondor place, person and order the clones carried was replaced (Ithilien, Osgiliath, Dol Amroth,
the Steward), and `ClonedCultureFlavorTests` holds the culture to that with an independent Gondor
word list.

**Weak on purpose.** The ruling clan is tier 4 and two vassals are tier 2, which caps the realm at
about 8 lord parties against 15 for five tier-6 clans (`DefaultClanTierModel.GetPartyLimitForTier`:
below tier 3 gives 1 party, tiers 3 and 4 give 2). Tier also sizes each party (+25 men per tier
for the clan leader, +15 for other members, `DefaultPartySizeLimitModel`), sets the companion limit
and the starting renown and influence; renown earned in play raises the tiers, so the cap is a
starting handicap rather than a permanent one. Towns start at 2,400 to 3,000 prosperity and
castles at 800 with wall level 1, villages at 300 hearth, and the culture is deliberately absent
from `tools/settlement_economy_floor.json`, whose floor would lift it. Starting gold is 90,000
against Gondor's 150,000. The troops themselves are good: weakness is in numbers, not quality.

### Component Diagram

```
generate_arthedain_troops.py ---> troops_arthedain.xml + 9 party templates
promote_borrowed_cultures.py ---> <Culture id="arthedain"> + npcs + rosters + strings
add_map_fortifications.py    --\
add_map_villages.py          ---> LIVE TAOM_Map settlements.xml (+ 12 loc files)
generate_new_faction_kingdoms.py -> Kingdom.arthedain, clan_arthedain_1..5, lord_AN*_*
insert_new_faction_cc_menus.py / insert_new_faction_careers.py / CC equipment ---> character creation
        |
C#: the volunteer pool (VolunteerRecruitmentService.Arthedain.cs), three career archetype rows,
and the culture rows in the compiled config defaults and id lists (recipe-add-a-kingdom.md "C# a
kingdom touches")
```

## Configuration

### Ids (fixed: saves reference them)

| Concept | Value |
|---------|-------|
| Kingdom id, culture id | `arthedain` |
| Lord and settlement region prefix | `AN` (`lord_AN{clan}_{n}`, `town_AN1`) |
| Clans | `clan_arthedain_1` to `_5` |
| Troops | `arthedain_*` |
| Narrative strings | `taom_cc_taom_{parent,youth,education,adulthood}_arthedain_N_{text,desc}` |
| Notable and townsfolk name keys | `aom_arth_*` (Gondor's `aom_gd_*` renamed, so no key is shared) |

### Realm

| Clan | Tier | Holds |
|------|------|-------|
| House of Arvedui (King Arvegil II) | 4 | Fornost Erain `town_AN2` (capital), Barad Forn `castle_AN9` |
| House of Evendim | 3 | Annúminas `town_AN1`, Emyn Uial `castle_AN10` |
| Wardens of Amon Sûl | 3 | Bree `town_AN3`, Amon Sûl `castle_AN4` |
| House of the Baranduin | 2 | Weather Hills `castle_AN5`, Brandywine Bridge `castle_AN6` |
| House of Emyn Beraid | 2 | Emyn Beraid `castle_AN8`, Forochel `castle_AN11` |

Four lords per clan (two married couples), each with a distinct face taken from a shipped Gondor
lord, and each clan flying its own Arnor icon (11000 to 11004) in silver on Arnor blue.

### Troop tree

Levy (basic, L6) to Spearman, Fornost Footman, Fornost Shieldman, Fornost Guard and Warden of
Fornost (L31). Dúnadan Youth (elite basic, L11) splits three ways: Rangers (Ranger of the North to
Ranger Captain, L31), the Citadel (Dúnadan Squire to King's Guard of Fornost, the one tier-7
capstone at L36) and a short cavalry branch (Outrider, Lancer). Four militia troops (the veterans
at level 16, as every culture's are), and `arthedain_dunadan_youth_merc` for the tavern. Every bow
and every archer's Bow skill is its `arthedain` ranged-ladder cell, computed by
`tools/ranged_ladder.py`, the module the ladder tools write with. Weapons are placed by sustained DPS against the
melee ladder, not by name: `numenorean_bastard_medium` sustains 105, above every elite blade, so no
troop carries it.

### Diplomacy

| Where | Setting |
|-------|---------|
| `taom_spkingdoms.xml` | at war with `gundabad` from day one (`value="-1" isAtWar="true"`) |
| `diplomacy/diplomacy.json` | Hostile with `gundabad`, `empire` (Dunland), `isengard`, and (Mike, 2026-10-07) every realm the seven core free realms are all Hostile to: `empire_s`, `dolguldur`, `khuzait`, `goblin`, `mistymountainorcs`, `bluecraig`, so Full War (day 44) declares them all and blocks their peace (`DiplomacyShippedConfigTests`); Natural with `rivendell`, `lindon`, `empire_w` |
| `diplomacy/war_of_the_ring.json` | phase 1: Gundabad attacks Arthedain |
| `configs/army_targeting.json` | theater `north`; Arthedain's three towns head Gundabad's and Dunland's priority lists |

### Tables keyed on culture or troop id

Swept the way the #749 lesson asks (every table keyed on the scaffold culture `gondor`), and
decided one by one:

| Table | Arthedain row | Why |
|-------|---------------|-----|
| `configs/battle_balance_config.json` `CulturalSurvivalBonuses` | 0.3, as Gondor | the same Dúnedain people; quality, not numbers |
| `combat_mechanics/combat_mechanics_config.json` `cultureMultipliers` | 1.3, as Gondor | same |
| `banner_bearers/banner_bearers_config.json` | Gondor's `standard_of_duty_t1` | no Arnor standard exists |
| The compiled defaults of those three (`BattleBalanceConfig.cs`, `CombatMechanicsConfig.cs`, `BannerBearerConfig.cs`) | the same rows | the fallback for a missing file; a test per table now holds compiled and shipped equal |
| `race_abilities/race_abilities.json` and `RaceAbilityDefaults.cs` | on Gondor's key: the Citadel Guard rally | human troops resolve their ability by culture; without a row they had none |
| `CareerSystemIoC` archetype map | Ranger of the North (Ranged), Warden of Fornost (Infantry), Knight of Arthedain (Cavalry) | an unmapped career's ability is a no-op that still shows its toast |
| `culture_marketplace/culture_marketplace_config.xml` | `armour_from="gondor"` | the Arnor pack is tagged `Culture.gondor` in the Armory, so Gondor's market pool is where it sits |
| `special_resources/troop_resource_costs.xml` | Castar costs for the King's Guard, Knight, Ranger Captain and Warden | Gondor's elites pay Castar; so do Arthedain's |
| `settlement_guards/settlement_guards_config.xml` | Fornost (`town_AN2`): King's Guard, Wardens, Fornost Guards | the capital's own garrison |
| `armour_acquisition` lord's materials and weapon picks | none of its own | one material per culture that owns armour by item culture; Arthedain owns none, so through `armour_from` its lord's ladder spends Gondor's material and offers Gondor's five weapon picks (Andúril among them) |
| `lotr_issues/taom_lotr_issues.xml` Deep Seam | on Gondor's row (`cultures="gondor,arthedain"`) | a culture that borrows its armour shares its donor's row, as Lindon shares Rivendell's |
| `lotr_issues/taom_lotr_issues.xml` Armourer's Commission | its own row, rewarding the Arnor infantry heavy chest | every playable culture has one (`Commissions_EveryCharacterCreationCultureHasARow`) |
| `TroopWeights/troop_weights.xml` | none | Gondor carries one row; unlisted troops weigh the default |
| Elite Emissary, culture doctrine, tournament rewards | none | optional offers, a feature that ships off, defaults apply |

### Other per-culture rows

`execution/alignment.json` (free), `special_resources_config.xml` (Castar, with Gondor),
`startup_resources_config.xml`, `realm_borders/palette.json` (`#A0B4D8`; Gondor's blue was too
close) and the matching MCM field `RealmColourArthedain`, `factionmap/factions.json` (playable,
difficulty 5), `FactionUI/faction_kingdoms.json`, the Advanced Start Options kingdom list, and the
known-culture set in `CustomBattleCommandersProvider` (pinned to the playable cultures by a test).

## Key Files

| File | Purpose |
|------|---------|
| `tools/generate_arthedain_troops.py` | the troop tree and soldier party templates; `--check` |
| `tools/add_map_fortifications.py` | town and castle rows, gates computed from the scene; `--check` |
| `tools/add_map_villages.py` | village rows (the Arthedain batch is in `VILLAGES`); `--check` |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Arthedain.cs` | volunteer pool (Levy 6, Dúnadan Youth 2) |
| `Main/_Module/ModuleData/troops/troops_arthedain.xml` | generated troop tree |
| `Main/_Module/ModuleData/characters/npcs_arthedain.xml` | notables and townsfolk |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_arthedain.xml` | the ten lord template rosters (`rosters_kept`; Gondor's named lords' kits are not cloned) |
| live `TAOM_Map/ModuleData/settlements.xml` | the realm's 34 settlements (unversioned), the last rows of the file |

## Dependencies

- The Arnor armour in the live Armory (`LOTRLOME_items/arnor/`, 76 `sk_ar_art_*` items).
- Gondor's spears, shields, lances, horses and civilian dress, which the Arnor pack does not cover.
- The `arthedain` ranged ladder line (five `ladder_arthedain_bow_t*` items in the live Armory).

## Tests

- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`: the Arthedain pool
  (low, boundary and high rolls, pool present).
- `TAOM.Tests/Features/CharacterCreation/ClonedCultureFlavorTests.cs`: no Gondor identity word in
  Arthedain's player-facing text.
- Added by the 2026-10-07 review, each failing on the gap it names: `CareerArchetypeServiceTests`
  (every career in the data has an archetype), `ShippedRaceAbilitiesConfigTests` (Arthedain shares
  the Citadel Guard), `ArmourAcquisitionShippedDataTests` (every playable culture has a commission
  row and is served by a Deep Seam row), the compiled-defaults tests in
  `ShippedBattleBalanceConfigTests`, `ShippedCombatMechanicsConfigTests` and
  `ShippedBannerBearerConfigTests`, and `CustomBattleCommandersShippedDataTests` (the provider's
  culture set equals the playable cultures read from the data).
- Existing data gates covering Arthedain unchanged: `CulturePartyTemplateTests`,
  `TavernMercenaryDataTests`, `RealmPaletteTests`, `TaomStartOptionsProviderTests`,
  `PlayerStartCoverageTests`, `CareerCultureCoverageTests`, `NarrativeCultureCoverageTests`.
  Count pins moved with the data: `WarTheaterConfigInvariantsTests` (86 priority targets),
  `SettingsFingerprintTests` (328 settings), `TroopUpgradeSkillMonotonicityTests` (militia cultures),
  `LotrIssueConfigProviderTests` (75 issues).
- Tools: `tools/tests/test_generate_arthedain_troops.py` (self-check, skill monotonicity, militia
  rule, ladder cells), `tools/tests/test_add_map_fortifications.py` (gate maths, block shape,
  `--check`, the save-order rule) and `tools/tests/test_add_map_villages.py`.
- Map: `python tools/add_map_fortifications.py --check` and `python tools/add_map_villages.py --check`.

## Save Compatibility

A new campaign is required to play Arthedain. A save made before it loads `settlements.xml` again
but never the new kingdom, clans or heroes (`SandBoxManager.InitializeSandboxXMLs`). The 38 new
settlement rows sit at the end of the file under a town, so in such a save the first of them throws
inside `Settlement.Deserialize` (its alleys), `MBObjectManager.LoadXML` swallows it, and the
unfinished rows are dropped as non-ready objects; every older settlement has already loaded. Not yet
reproduced on a live save: the in-game check is owed. A new row placed before an existing one would
leave that existing settlement unready, so the old save would unregister it while its parties and
heroes still pointed at it, which is why
both map tools append and `add_map_fortifications.py --check` refuses any other order.

## How to Change the Realm

1. **A troop:** edit `tools/generate_arthedain_troops.py`, run it with `--apply`, then
   `python tools/validate_moduledata.py`. Never rename a troop id; display names may change.
2. **A settlement Mike moved, added or renamed in the editor:** save the scene, then run both map
   tools' `--check`. A new entity needs a row in the matching table, then `--apply`, which appends
   it at the end of `settlements.xml` (see Save Compatibility). A row without an entity crashes a
   campaign load (#269). Two entities with one name bind the row to the first one in the scene;
   `--check` notices only when their positions differ, so search the scene for the id. Then rebuild
   the distance cache in game (Map Tools, Rebuild Now; `docs/modding/settlements.md` "Add" step 7).
3. **Lords, clans or the ruler:** edit the `arthedain` entry of `generate_new_faction_kingdoms.py`
   and run it with `--only arthedain`. Never run it without `--only`.
4. **Culture text or names:** edit the `arthedain` target of `promote_borrowed_cultures.py` and run
   it with `--only arthedain`. Any changed English that is already translated needs the reset in
   `docs/localization/TRANSLATOR_GUIDE.md` "Changing English Text That Is Already Translated".
5. Restart Bannerlord and start a new campaign: kingdoms, clans and new item files load only then.

## Known gaps

- Every new string has an English placeholder in the 12 language files; the translator run is #754.
- Arthedain shares Gondor's five cultural feats, whose code names are Gondorian. Character creation
  shows only the feats' descriptions (`CharacterCreationCultureVM`), never the names.
- The faction screen has no painted portraits or special characters for Arthedain (art owed).
- Lords, rulers, heirs and enlisted players wear the Arnor kit, but their weapons, horses and
  harnesses are still Gondor's, and the child and education rosters are Gondor's clones.
- No item carries `Culture.arthedain`, so Arthedain towns never stock Gondor-tagged weapons and gear
  (the market's daily filter strips them); Lindon has the same shape. Follow-up #755.
- Combe (`village_AN3_3`) is bound to Emyn Beraid, its nearest fortification; its Bree-land name and
  its place on the map may want a second look.

## Changelog

- 2026-10-07, Mike's calls after the review: Arthedain is Hostile to every evil realm, not only
  its three neighbours, so the War of the Ring's Full War reaches it like every other free realm.
  The cloned lord, ruler, heir and enlistment rosters wear the Arnor kit (the promoter's
  `item_map`, Gondor piece to Arnor piece by slot and tier) and King Arvegil II alone the Arnor
  crown (`leader_head` in the kingdoms generator, a direct Head override). The ranger bows are
  named "[Arnor] Ranger's Bow" (`names` on the `arthedain` ranged ladder line). Emyn Forn moved
  from Fornost to Annúminas and Combe from Bree to Emyn Beraid, each to its nearest fortification.
  The 18 careers of the six cultures promoted in August got their archetypes too.
- 2026-10-07, deep review: the three careers got their battle abilities (archetype rows), the troops
  their race ability (Gondor's Citadel Guard), and Arthedain its Armourer's Commission and a place on
  Gondor's Deep Seam row; the veteran militia dropped to level 16; 64 unused cloned Gondor rosters
  were dropped; Luinkrag's rows moved to the end of `settlements.xml` with the rest of the batch, so
  an older save keeps its existing settlements (docs: Save Compatibility; RCA
  `docs/reviews/rca-arthedain-2026-10-07.md`).
- 2026-10-07: created. Culture, troops, ranger bows, ten fortifications and twenty-four villages
  placed by Mike, kingdom, five clans and twenty lords, diplomacy, character creation (narrative
  menus, three careers, 55 starting rosters) and the playable faction card. Blue Craig gained its
  second town, Luinkrag (`town_GBC2`), in the same session.

## GitHub Issue

- **Issue:** #753; follow-ups #754 (translations, backlog), #755 (donor-culture market weapons),
  #756 (policy ids no kingdom can use).
- **Status:** built and verified in data; in-game check owed (#753 checklist).
