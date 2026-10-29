# Cardolan and Rhudaur

## Overview

Cardolan and Rhudaur are two playable kingdoms: with [Arthedain](arthedain.md) they are the three
realms Eärendur's sons made of Arnor (LOTR Appendix A I iii), and in TAOM all three lines survive.
Each has its own culture (`cardolan`, `rhudaur`), troop tree, kingdom, clans, lords, character
creation and faction card. Both are **Neutral**: they stand apart from the War of the Ring, and the
enemies at their borders are Dunland, Gundabad, the Misty Mountain orcs and the goblins.

**Parked (2026-10-28):** the player cannot choose either realm at character creation until its troops have armour
([parked realms](faction-map.md#parked-realms)).

## Why This Exists

- **Mike's request (2026-10-23, #978):** one kingdom per son of Eärendur, so Arthedain is no longer
  the only Arnor realm. Mike placed the settlements on the TAOM_Map main map and approved the
  names, the troop trees and the clan sizes.
- **Before:** the faction screen carried two non-playable placeholder cards. Rhudaur's said its
  hill-men served the Witch-king; Mike's design has Angmar gone and the Dúnedain line ruling.

## Architecture

### Design Challenge

The Arthedain recipe ([arthedain.md](arthedain.md) "Solution Approach") touches about thirty
surfaces in three modules. Doing it twice more by copying Arthedain's tools would have tripled the
troop generator and left three realms whose generators reorder each other's regions.

### Solution Approach

The same recipe and the same generators, extended rather than copied:

| Step | Tool | What changed for these two realms |
|------|------|-----------------------------------|
| Troops and the 9 soldier party templates | `tools/generate_cardolan_troops.py`, `tools/generate_rhudaur_troops.py` | The machinery moved out of `generate_arthedain_troops.py` into `tools/realm_troops.py` (Arthedain's output is byte-identical); each realm file holds only its design |
| Bow ladder | `tools/ranged_ladders.json` lines `cardolan`, `rhudaur`; `tools/generate_ranged_ladder_items.py` | Live `LOTRLOME_items/arnor/ranged_ladder.xml` now has 14 items |
| Culture, notables, wanderers, villager and caravan templates, education templates, rosters | `tools/promote_borrowed_cultures.py --only cardolan` / `--only rhudaur` | Both built by `_arnor_successor()` from Arthedain's settings; three promoter defects fixed (Changelog) |
| Towns, castles, villages | `tools/add_map_fortifications.py`, `tools/add_map_villages.py` | 9 fortifications, 29 villages; Cardolan and Rhudaur take Arthedain's `STYLE` |
| Kingdom, clans, lords, heroes | `tools/generate_new_faction_kingdoms.py --only cardolan --apply` / `--only rhudaur --apply` | New `face_template` and `female_hair_tag` options |
| Narrative menus | `tools/insert_new_faction_cc_menus.py` | Authored text per realm on Gondor's mechanics |
| Careers | `tools/insert_new_faction_careers.py` | Three each, cloned from Gondor's |
| Starting gear | `tools/generate_char_creation_equipment.py --append cardolan rhudaur`, `generate_starter_kit.py`, `wire_starter_kit_rosters.py` | 55 rosters each |

**Armour is a placeholder** (Mike, 2026-10-23): Cardolan wears Gondor's armour (Anfalas, Pinnath
Gelin and Dol Amroth pieces) and carries the three Cardolan spears the Armory ships; Rhudaur wears
the Arnor kit, with Dunland's axes and shields for its hill-men.

**Weak on purpose**, as Arthedain: ruling clans at tier 4, the rest at 3 and 2, towns at 2,400 to
2,800 prosperity, castles at 800, villages at hearth 300, and no row in
`tools/settlement_economy_floor.json`.

**Neutral, not Free** (Mike, 2026-10-23): `execution/alignment.json` lists both as `neutral`, every
start relationship is 0, and `diplomacy/diplomacy.json` has no row for either, because an unlisted
pair is Neutral. In this code `Natural` is an alliance tier, not "no stance".

### Component Diagram

```
realm_troops.py <-- generate_{arthedain,cardolan,rhudaur}_troops.py --> troops_<c>.xml + 9 party templates
promote_borrowed_cultures.py --only <c> --> <Culture id="<c>"> + npcs + rosters + wanderers + strings
add_map_fortifications.py / add_map_villages.py --> LIVE TAOM_Map settlements.xml (+ 12 loc files)
generate_new_faction_kingdoms.py --only <c> --> Kingdom.<c>, clan_<c>_N, lord_CA*/lord_RH*
CC menus, careers, CC gear --> character creation
C#: VolunteerRecruitmentService.Cardolan.cs / .Rhudaur.cs, the culture rows of the compiled tables
```

## Configuration

### Ids (fixed: saves reference them)

| Concept | Cardolan | Rhudaur |
|---------|----------|---------|
| Kingdom id, culture id | `cardolan` | `rhudaur` |
| Settlement and lord prefix | `CA` (`town_CA2`, `lord_CA1_1`) | `RH` (`town_RH1`, `lord_RH1_1`) |
| Clans | `clan_cardolan_1` to `_4` | `clan_rhudaur_1` to `_3` |
| Troops | `cardolan_*` | `rhudaur_*` |
| Name keys of notables and townsfolk | `aom_card_*` | `aom_rhud_*` |

### Realms

| Realm | Clan (tier) | Holds |
|-------|-------------|-------|
| Cardolan, King Aranthir | House of Tyrn Gorthad (4) | Caras Gorthad `town_CA2` (capital), Tyrn Gorthad `castle_CA1` |
| | House of Tharbad (3) | Tharbad `town_CA1` |
| | House of Minas Eithel (2) | Minas Eithel `town_CA3` |
| | Wardens of Sarn Ford (2) | Sarn Ford `castle_CA2` |
| Rhudaur, King Belegorn | House of Cameth Brin (4) | Cameth Brin `town_RH1` (capital), Egladil `castle_RH1` |
| | House of Ettendale (3) | Ettendale Hold `castle_RH2` |
| | Wardens of the Coldfells (2) | Coldfells `castle_RH3` |

Four lords per clan (two married couples), 16 for Cardolan and 12 for Rhudaur. Each lord draws his
own face from the `fighter_gondor` template at campaign start (Lord Face Variety rule A), where a
copied face key is only nudged (`HeroFaceVarietyDataTests`). The template's hair tags are men's
styles that match no woman's hair, and with no match the native roll falls back to the template
keys' hair indices, where a woman's index 0 is bald. So each lady carries her own
`<hair_tag name="empire" />` (10 of the 22 adult female hairs, none bald), written by the
generator's `female_hair_tag` and pinned by `FemaleLordHairDataTests`. Banners: Rhudaur flies the Arnor icons
(group 110) in silver on palette 99. Cardolan (#1047) flies green (palette 2027, `0xFF28745C`, also its realm
colour) with white emblems; the realm and the House of Tyrn Gorthad fly 31005 (an interlace ring around a
lozenge, with seven stars), the House of Tharbad flies its icon in gold (238). The generator's `sigil` and
`clan_sigils` fill the `{fg}` slot of the realm's banner template. The clans' `color`/`color2`, which
Patch23 uses to dress troops and map parties, are shades of the same green with white, and the culture
and faction-map colours match (`0xFF28745C`).

### Troop trees

- **Cardolan** (20 troops with the tavern copy): Levy (L6) to Spearman, Cardolan Spearman, Barrow
  Guard, Veteran Barrow Guard and Warden of Tyrn Gorthad (L31). Dunadan of Cardolan (elite basic,
  L11) splits into the knights (Rider of Cardolan to Knight-Captain, then King's Guard of Caras
  Gorthad, the mounted L36 capstone) and the Minhiriath Bowmen (to L26).
- **Rhudaur** (21 troops with the tavern copy): Hillman Levy (L6) to Hill Spearman, Rhudaur Axeman,
  Veteran Axeman and Fell Warden (L26); the Hill Spearman also leads to the Hill Rider (L21), the one
  cavalry troop. Dunadan of the Angle (elite basic, L11) splits into the Angle Rangers (to L31) and
  the Guard of Cameth Brin (to Lord of the Angle, the L36 capstone).
- Both: the four militia troops at the TAOM militia rule, and bows from the realm's own ranged-ladder
  line (`cardolan` ranked with Harad and Dale, `rhudaur` with Arthedain).

### Tables keyed on culture

Every table that names `arthedain` was decided for both. Four keep no row for either realm:
`settlement_guards_config.xml` (it names only Fornost's guards, so both use the default guards),
`troop_resource_costs.xml` (keyed by troop; no Arnor troop costs a special resource),
`war_chronicle.json` (the generic text), and the lord's ladder crown in
`armour_acquisition_config.xml` (Arthedain's king's crown).

| Table | Cardolan | Rhudaur |
|-------|----------|---------|
| `configs/battle_balance_config.json` survival bonus | 0.3 | 0.2 (hill-men a step under) |
| `combat_mechanics/combat_mechanics_config.json` charge multiplier | 1.3 (the horse realm) | 1.2, as Dale |
| `crown_guard/crown_guard_cultures.json` | `cardolan_warden` (the capstone rides) | `rhudaur_lord_of_the_angle` |
| `banner_bearers`, `lord_banners`, `race_abilities`, `hall_of_fire`, `voice_accents`, Postern Gate | Arthedain's rows | Arthedain's rows |
| `culture_marketplace_config.xml` | Gondor's whole pool (it wears Gondor's armour) | Arthedain's three `<Stock>` rows, the crown blacklisted |
| `special_resources_config.xml` | Castar, with Gondor and Arthedain | Castar |
| `startup_resources_config.xml` gold | 90,000 | 80,000 |
| `lotr_issues` | own Armourer's Commission; on Gondor's Deep Seam row | the same |
| `configs/army_targeting.json` | theater `north` | theater `north` |
| `realm_borders/palette.json` and MCM | `#28745C` (the banner green), `RealmColourCardolan` | `#E0735A`, `RealmColourRhudaur` |
| Careers | Bowman of Minhiriath, Warden of Tyrn Gorthad, Knight of Cardolan | Ranger of the Angle, Captain of Cameth Brin, Rider of the Fells |
| Faction card | playable, Neutral, difficulty 5 | playable, Neutral, difficulty 6 |

## Key Files

| File | Purpose |
|------|---------|
| `tools/realm_troops.py` | shared troop machinery for the three Arnor realms and, since 2026-10-27, the dwarf realm of [Ered Luin](ered-luin.md) and the Dale-cut realms of [Anduin Vale and Angáladh](anduin-vale-angaladh.md) |
| `tools/generate_cardolan_troops.py`, `tools/generate_rhudaur_troops.py` | each realm's tree and party templates; `--check` |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.Cardolan.cs`, `.Rhudaur.cs` | volunteer pools (Levy 6, Dunadan 2) |
| `Main/Features/LiegeTitles/TaomCultureTags.cs` | `TaomCardolanCultureTag`, `TaomRhudaurCultureTag` for the King and Queen titles |
| `Main/_Module/ModuleData/troops/troops_cardolan.xml`, `troops_rhudaur.xml` | generated trees |
| `Main/_Module/ModuleData/characters/npcs_cardolan.xml`, `npcs_rhudaur.xml` | notables and townsfolk |
| live `TAOM_Map/ModuleData/settlements.xml` | the 38 settlements, the last rows of the file |

## Dependencies

- Gondor's armour, spears, shields and horses; the Arnor kit; Dunland's axes and shields; the
  `cardolan` and `rhudaur` ranged-ladder items in the live Armory.

## Tests

- `tools/tests/test_realm_troops.py`: both designs (self-check, skill monotonicity, militia rule,
  mercenary, ladder cells, party templates, labels and cultures).
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`: both pools.
- Count and list pins moved with the data: `TroopUpgradeSkillMonotonicityTests`
  (`ExpectedMilitiaCultures`), `ShippedRaceAbilitiesConfigTests` (20 cultures),
  `SettingsFingerprintTests` (423 in `TaomSettings`), `ShippedClanSideCoverageTests` (`NeutralRealmIds`),
  `LotrIssueConfigProviderTests` (77 issues), `GlobalStringsOverridesTests` (the picker names).
- `TAOM.Tests/Core/FemaleLordHairDataTests.cs`: every human lady on a face template carries her own
  hair tags.
- `ClonedCultureFlavorTests` scans both realms for Gondor's places and Arthedain's seats.
- Existing gates that now cover them: `LiegeTitleOverrideTests`, `RealmPaletteTests`,
  `ArmourAcquisitionShippedDataTests`, `ShippedHallOfFireConfigTests`, `HeroFaceVarietyDataTests`,
  `WandererBackstoryCoverageTests`, `EveryLanguage_DeclaresARowForEveryEnglishKey`.
- `tools/tests/test_generator_regions_in_sync.py` runs every Arnor troop generator's and kingdom's
  `--check`, and the promoter's for the three realms when the live map is installed.
- `tools/tests/test_generate_new_faction_kingdoms_writes.py` and
  `tools/tests/test_promote_register_region.py`: the dry-run default, parse before write, and the
  refusal of a doubled registration region.

## Save Compatibility

A new campaign is required to play either realm. A save made before them loads `settlements.xml`
again but never the new kingdoms, clans or heroes (`SandBoxManager.InitializeSandboxXMLs`). The 38
new rows sit at the end of the file under `town_CA2`, so the first of them throws inside
`Settlement.Deserialize` (its alleys), `MBObjectManager.LoadXML` swallows it, and the unfinished rows
are dropped; every older settlement has already loaded. The mechanism is Arthedain's
([arthedain.md](arthedain.md) "Save Compatibility"), and the in-game check on an old save is owed.
The cache was rebuilt for these rows on 2026-10-23, so a save made before them is now expected to stop
loading (v1.5.6 decompile, not reproduced; #1041).

Names behave differently from ids. A town's name is not saved: `Settlement.Deserialize` reads it
from `settlements.xml` on every load, so a renamed town shows its new name in every save. A clan's
name is saved (`Clan.Name` is a `[SaveableProperty]`), and `clans.xml` loads only for a new campaign.
English shows the saved text as it is, while the other 12 languages look its `{=key}` up in their
language files. So a campaign started before the 2026-10-27 swap (Changelog) keeps the old house
names in English and shows the new ones in the other languages.

## How to Change a Realm

The steps in [arthedain.md](arthedain.md) "How to Change the Realm" apply, with each realm's own
generator and `--only <culture>`. Both `generate_new_faction_kingdoms.py` and
`promote_borrowed_cultures.py` only report what would change until you add `--apply`.

## Known gaps

- The armour is a placeholder until each realm has its own.
- The cloned wanderers keep the surnames of Gondor's and Arthedain's wanderers.
- Arthedain's narrative menus and culture text still name "fallen Cardolan" and the hill-men of
  Rhudaur "under Angmar's banners", its kingdom text calls it "the last of the three realms", two of
  its career descriptions (`taom_careers.xml`) and five wanderer backstories still fight Angmar,
  and the Remnants
  of Angmar faction card still stands (a decision for Mike; changing them resets their translations).
- All new English text waits for the paid translation run (#763). The 12 translated
  `loc_arnor.xml` files in the live Armory have no row yet for the 9 new ladder bows, so
  `sync_ranged_ladder_translations.py` stops on `ladder_rhudaur_bow_t2` until that run adds them.
- No new faction-screen art was made: both cards show the existing `faction_kingdom_of_cardolan.png`
  and `faction_kingdom_of_rhudaur.png` in `Main/_Module/GUI/SpriteData/FactionMap/`.

## Changelog

- 2026-10-23 (#978): created. Built with the Arthedain recipe. Along the way:
  - `tools/realm_troops.py` replaced the copy-per-realm troop generator.
  - `promote_borrowed_cultures.py` stopped copying the multi-line liege-title strings line by line
    (which wrote a malformed `taom_module_strings.xml`) and replaces its `SubModule.xml`
    registration region in place. The parse-before-write guard (`unparsable()`, #979) and the
    one marker-region writer (`tools/_marker_region.py`, #975/#976) landed upstream at the same
    time, and the merge kept those in every writer.
- 2026-10-23 (Mike): Arthedain's ranger group "Bowman of the Angle" is "Dúnedain Rangers" (the
  Angle is Rhudaur's land; 12 rows reset, #763), and every other playable culture got placeholder
  career kits too (`starting-equipment-tuning.md`).
- 2026-10-23 (#978 follow-up, Mike): career starting kits for all three Arnor realms (18 rosters,
  each realm's lowest troop gear by `generate_career_kits.py`; Rhudaur's sidearm is the Dunland
  axe, and all three ride Gondor's saddle horse and starter harness). King Arvegil II now holds
  Fornost against the orcs of Gundabad, at peace with his kin in Cardolan and Rhudaur, and
  Arthedain's "Sword of the Last Bridge" career group is "Sword of the North Downs"; both reset
  their 12 translations (#763). Arvegil's entry is saved with the hero, so an English campaign
  started before this keeps the old line; new campaigns get the new one.
- 2026-10-23 (#978 deep review):
  - The 14 ladies carry `empire` hair tags; on the bare template about half would start bald.
  - Rhudaur's kingdom banner is its royal house's icon (11002), as Arthedain's and Cardolan's are.
  - Cardolan's "Sword of the Last Bridge" career group is "Sword of Sarn Ford": the Last Bridge is in
    Rhudaur.
  - The armour class table was regenerated: Cardolan's level 26 troops wear
    `sk_gd_ano_bracer_noble_heavy_a`, which moves that piece from elite to heavy.
  - Tooling: the promoter rounds the live start point to 3 decimals (an editor save had broken its
    `--check`), joins `test_generator_regions_in_sync.py`, and refuses a duplicated registration
    region; the kingdom generator parses every output before writing any and writes only with
    `--apply`; the troop self-check catches a two-handed axe carried with a shield.
  - Arthedain's start point moved to Fornost Erain's live position (464.975, 1162.654), and its
    faction card no longer calls it the only Arnor realm still standing.
- 2026-10-27 (Mike): Tharbad and Minas Eithel swapped names. `town_CA1` stands where Tharbad is on
  Tolkien's map, at the Greyflood crossing of the North-South Road, so it is now Tharbad, and
  `town_CA3` in Minhiriath is Minas Eithel. Their clans swapped names with them, so each house still
  names its seat: `clan_cardolan_2` is House of Tharbad and `clan_cardolan_3` House of Minas Eithel.
  The villages keep their names (Mike's call). Ids, owners and positions did not change.
  - Live `TAOM_Map`: `settlements.xml` and the 12 `loc_settlements.xml` (backups
    `*.bak_ca_swap_20261027`). Repo: `clans.xml`, `taom_clan_name_strings.xml` and its 12 language
    files, and the tables of `add_map_fortifications.py` and `generate_new_faction_kingdoms.py`.
  - `add_map_fortifications.py --check` now compares each fortification's live name with its table
    row, so a reinstall that restores the old names fails the check.
  - Campaigns started before the swap keep the old house names in English ("Save Compatibility").

## GitHub Issue

- **Issue:** #978.
- **Status:** built and verified in data; in-game check owed (#978 checklist).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/anduin-vale-angaladh.md](./anduin-vale-angaladh.md)
- [docs/features/arthedain.md](./arthedain.md)
- [docs/features/ered-luin.md](./ered-luin.md)
- [docs/features/kingdom-creation.md](./kingdom-creation.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/recipe-add-a-kingdom.md](../modding/recipe-add-a-kingdom.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
