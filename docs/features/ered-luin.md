# Ered Luin

## Overview

The Kingdom of Ered Luin is a playable dwarf realm in the Blue Mountains, the range on the western
edge of the TAOM_Map main map, between Lindon and the Arnor realms. It has its own culture
(`eredluin`, race `dwarf`), troop tree, kingdom, four clans, sixteen lords, character creation,
careers and faction card. Durin's Folk hold Thorin's Halls, and in TAOM the Broadbeams and the
Firebeards hold Belegost and Nogrod again. It stands on the Free side, allied by nature to Erebor
and Lindon, with the goblins of Blue Craig on its northern border.

**Parked (2026-10-28):** the player cannot choose Ered Luin at character creation until its troops have armour
([parked realms](faction-map.md#parked-realms)).

## Why This Exists

- **Mike's request (2026-10-27):** Mike placed three towns, five castles and twenty-three villages
  in the Blue Mountains on the main map and asked for a new kingdom of Ered Luin for the dwarves on
  them, built the way the earlier kingdoms were. He gave full authority over the design while he was
  away, so every choice below is open to his review.
- **Before:** Erebor was TAOM's only dwarf realm. The far west held Lindon, Blue Craig and the three
  Arnor realms, and no dwarves, although Tolkien puts Thorin's Halls, Belegost and Nogrod there.

## Architecture

### Design Challenge

A kingdom touches about thirty surfaces in three modules (the repo, the live unversioned TAOM_Map
and the live unversioned Armory), and the recipe ([cardolan-rhudaur.md](cardolan-rhudaur.md),
[arthedain.md](arthedain.md)) was written for human realms cut from Gondor. A dwarf realm needs
what those tools did not do: a `race` on every troop, the dwarf face template, no mounted troops
(`MOUNTED_DWARF`), dwarf lord templates, dwarf hair, and dwarf bows and crossbows.

### Solution Approach

The same recipe and the same generators, extended rather than copied:

| Step | Tool | What changed for Ered Luin |
|------|------|----------------------------|
| Towns, castles, villages | `tools/add_map_fortifications.py`, `tools/add_map_villages.py` | An `eredluin` `STYLE` (Erebor's sturgia scenes and meshes, building levels a step under Erebor's) and `MESHES` row; 8 fortifications and 23 villages appended at the end of the live file |
| Troops and the 9 soldier party templates | `tools/generate_eredluin_troops.py` on `tools/realm_troops.py` | `Realm` gained `race`, `body` and `ladder_line`, and a `crossbow` role; the self-check refuses a mounted dwarf. The three Arnor outputs stay byte-identical |
| Bows and crossbows | `tools/ranged_ladders.json` | The `erebor` line claims `troops_eredluin.xml` too (as `rivendell` claims Lindon's), so Ered Luin carries Erebor's ladder items and no new Armory item was needed |
| Melee band | `tools/melee_ladders.json` | `eredluin` offset 0.04, a step under Erebor's 0.05 |
| Culture, notables, wanderers, villager and caravan templates, education templates, rosters | `tools/promote_borrowed_cultures.py --only eredluin` | A target cut from Erebor (`src_short="er"`: Erebor's notable keys are `aom_er_*`) |
| Kingdom, clans, lords, heroes | `tools/generate_new_faction_kingdoms.py --only eredluin` | A `dwarf` lord template pair (Erebor's Náin and Dísa) |
| Narrative menus, careers, starting gear | `insert_new_faction_cc_menus.py`, `insert_new_faction_careers.py`, `generate_char_creation_equipment.py`, `generate_starter_kit.py`, `wire_starter_kit_rosters.py`, `generate_career_kits.py` | Authored dwarf text on Erebor's mechanics; Erebor's Ironguard cloned twice (one-handed and two-handed) and its Crossbow Master once, no cavalry career; Erebor's gear with no mount ("Careers and character creation") |
| Tables keyed on culture | hand edits to each table and its compiled C# twin | Erebor's row wherever the rule is a dwarf one ("Tables keyed on culture") |

**A middling realm.** The ruling House of Borin is tier 5, the Broadbeams and the Firebeards tier 4
and the Wardens of Dolmed tier 3, so the realm starts with about 9 lord parties
(`DefaultClanTierModel.GetPartyLimitForTier`: tier 5 gives 3, tiers 3 and 4 give 2). That sits
between the weak Arnor realms (about 8) and Blue Craig (12). Towns start at 3,000 to 3,200
prosperity against Erebor's 3,500, castles at 800, villages at Erebor's hearth 350, and the culture
is not in `tools/settlement_economy_floor.json`.

**On foot only.** The dwarf skeleton's rider bone is misaligned, so no Ered Luin troop, lord or
career kit has a horse, and Ered Luin has no war rams (Erebor's Ironpass line keeps those).

**Free, with Erebor and Lindon.** Ered Luin is Hostile to the realms Arthedain is Hostile to, so
Full War (day 44) brings it into the War of the Ring against Blue Craig and the other goblin and orc
realms. No war on day one.

### Component Diagram

```
realm_troops.py <-- generate_eredluin_troops.py --> troops_eredluin.xml + 9 party templates
ranged_ladders.json ("erebor" line claims eredluin) --> Erebor's ladder bows and crossbows
promote_borrowed_cultures.py --only eredluin --> <Culture id="eredluin"> + npcs + rosters + wanderers + strings
add_map_fortifications.py / add_map_villages.py --> LIVE TAOM_Map settlements.xml (+ 12 loc files)
generate_new_faction_kingdoms.py --only eredluin --> Kingdom.eredluin, clan_eredluin_1..4, lord_EL*_*
CC menus, careers, CC gear --> character creation
C#: VolunteerRecruitmentService.EredLuin.cs, the culture rows of the compiled tables
```

## Configuration

### Ids (fixed: saves reference them)

| Concept | Value |
|---------|-------|
| Kingdom id, culture id | `eredluin` |
| Settlement and lord prefix | `EL` (`town_EL3`, `lord_EL1_1`) |
| Clans | `clan_eredluin_1` to `_4` |
| Troops | `eredluin_*` |
| Name keys of notables and townsfolk | `aom_elu_*` (Erebor's `aom_er_*` renamed, so no key is shared) |

### Realm

| Clan (tier) | Lords | Holds |
|-------------|-------|-------|
| House of Borin (5), King Farin II | `lord_EL1_1` to `_4` | Thorin's Halls `town_EL3` (capital), Dwarrowgate `castle_EL5` |
| Broadbeams (4) | `lord_EL2_1` to `_4` | Belegost `town_EL1`, Ironcrag `castle_EL1` |
| Firebeards (4) | `lord_EL3_1` to `_4` | Nogrod `town_EL2`, Stonewatch `castle_EL4` |
| Wardens of Dolmed (3) | `lord_EL4_1` to `_4` | Dolmed Hold `castle_EL2`, Hammerdeep `castle_EL3` |

Four lords per clan, two married couples. Each lord draws a face from Erebor's `fighter_erebor`
template at campaign start. That template's hair tags are culture names (`empire`, `sturgia`), but
the dwarf skin in the Armory's `skins.xml` tags its hairs by style (`Ponytail`, `TiedAcrossBack`),
so nothing matches. `fighter_erebor`'s keys carry hair indices 1 and 10, and a tag list that matches
no hair draws between them (native `FUN_18057c9f0`, v1.5.6), so no dwarf lady can start bald (index
0 of the dwarf woman's skin is the Bald mesh). Each lady carries `<hair_tag name="TiedAcrossBack" />`
as a style choice: it picks among the 8 tied styles (`dwarf_hair_c`, `_e` to `_k`) instead of `_a`
to `_j`. The generator requires a tag on every face-template realm. Banners: dwarf
icons 24000 to 24003 (`banner_icons.xml`, ones no clan flew) in silver on the slate blue of palette
200.

Names come from the Old Norse dwarf-lists Tolkien drew on, avoiding every name Erebor's lords and
bandits use and every canonical dwarf of the age. Two are also in Erebor's culture pool: Hanar, and
Farin, kept with Borin for the royal house. The king is Farin II, of the line
of Borin: Borin's descendants (Fundin, Balin, Dwalin, Glóin, Gimli) were Thorin's folk in the Blue
Mountains.

### Settlements

| Id | Name | Kind | Scene | Prosperity |
|----|------|------|-------|-----------|
| `town_EL3` | Thorin's Halls (capital) | town | `sturgia_town_g` | 3,200 |
| `town_EL1` | Belegost | town | `sturgia_town_c` | 3,000 |
| `town_EL2` | Nogrod | town | `sturgia_town_b` | 3,000 |
| `castle_EL5` | Dwarrowgate | castle | `sturgia_castle_c` | 800 |
| `castle_EL1` | Ironcrag | castle | `sturgia_castle_003` | 800 |
| `castle_EL4` | Stonewatch | castle | `sturgia_castle_c` | 800 |
| `castle_EL2` | Dolmed Hold | castle | `sturgia_castle_003` | 800 |
| `castle_EL3` | Hammerdeep | castle | `sturgia_castle_siege_001` | 800 |

Belegost and Nogrod keep their Tolkien names. Belegost (`town_EL1`) lies north of the Gulf of Lune
and Nogrod (`town_EL2`) south, the order Tolkien gives them; Thorin's Halls (`town_EL3`) faces east
toward the road to the Shire. The castles and the 23 villages carry Westron names, the names the
dwarves use before Men: mines (iron, silver, clay, salt), upland farms and charcoal burners. The
village table is `VILLAGES` in `tools/add_map_villages.py`. `taom_dwarf_town_01_forceatmo` in
`TAOM_Map/SceneObj` is bound to no settlement. A settlement needs only a `scene_name` that matches a
`SceneObj/<name>/` folder in some loaded module; no module registers scenes in `project.mbproj`.
Using it for Thorin's Halls is an open decision (Known gaps).

### Troop tree

27 troops with the tavern copy, all `race="dwarf"`, all on foot:

- **Axe line:** Delver (basic, L6) to Axeman, Hearth-guard, Hall Warden, Veteran Hall Warden and
  Warden of Thorin's Halls (L31). Axe and shield.
- **Mattock line:** Axeman to Mattock Hewer (L16), Mattock Warrior and Veteran Mattock Warrior
  (L26). Two-handed axes and the Iron Hills mattocks, no shield; each carries a throwing axe
  (`southern_throwing_axe_1_t4`, vanilla's Tribesman Throwing Axe, the one Erebor's troops throw) in
  Item1 of both rosters. No other Ered Luin line throws.
- **Bowmen:** Delver to Bowman (L11), Veteran Bowman and Blue Mountain Bowman (L21).
- **Noble** (elite basic, L11) splits into the **Broadbeams** (Broadbeam Guard, Broadbeam
  Shield-guard, Gatewarden of Belegost, Ironbound Warden, then the King's Guard of Thorin's Halls,
  the L36 capstone; spear and tower shield) and the **Firebeards** (Firebeard Bowman at L16, then
  the crossbows: Firebeard Arbalester, Firebeard Marksman and Master Arbalester of Nogrod at L31).
- The four militia troops at the TAOM militia rule, and `eredluin_noble_merc`, the tavern's Hired
  Noble (a leaf copy of the Noble).

Weapons were placed by sustained DPS against the `eredluin` melee band (the Delver takes the two
lightest axes, 38 and 43 DPS, because the tier-1 band ends at 44). No troop carries a named hero
weapon: the first tree gave the Mattock Warrior Dáin's own hammer (`sm_dwarf_dain_hammer_a`, in
`<NamedWeapons>`). It now carries Erebor's heavier two-handed axe (`sm_dwarf_erebor_axe_2h_b`, 72.7
DPS) in both rosters. It is not named, it sits inside the tier-4 band, and it beats both of the
Mattock Hewer's weapons (62.9 and 54.6 DPS), so the upgrade is stronger on either roster roll.

Armour is a placeholder from the dwarf kit in the Armory: the Iron Hills pieces (`sk_dwarf_iron_*`)
and Erebor's (`sk_dwarf_erebor_*`), placed by the id tier keyword (`ARMOUR_MESH_TIER_LADDER`). The
upper ranks wear the Iron Hills elite helmets, chests and pauldrons no other troop wears, so Ered
Luin's best do not look like Erebor's.

**Borrowed armour never moves.** The armour class table (`armour_classes.xml`) and the armour restat
band each piece by its lowest battle wearer (`rebalance_armor.level_to_band`). Erebor dresses above
its id tiers (it wears most of its "heavy" pieces from level 31), so the first tree moved 50 Erebor
and Iron Hills pieces into a lower class (deep review XML-1): 26 from elite to heavy under its level
21 and 26 troops (still gated, sold from one armoury level lower), 14 to medium under its level 16
troops (out of the armoury gate, so sold and looted freely), and 10 to light under its level 11
troops. The tree was re-dressed so no Ered Luin troop wears a piece below the band another kingdom
wears it at, and `armour_classes.xml` now matches the base commit row for row.
`BorrowedArmourAnchorTests` in `tools/tests/test_realm_troops.py` fails the next edit that breaks it.
The cost is visible in the lower ranks: the Noble wears light kit at level 11, the level 26 troops
wear the Iron Hills heavy helmets and capes Erebor's level 26 troops wear, and the Gatewarden of
Belegost wears an Iron Hills heavy chest rather than Erebor plate, which would also have made its
promotion from the Shield-guard lower its armour.

Ered Luin's engine-tier median armour is 262 at tier 5 and 306 at tiers 6 and 7, against Erebor's
236, 264 and 277 (the comparison columns of the validator's `CROSS_CULTURE_ARMOUR_INVERSION`
messages, 2026-10-27): the Iron Hills heavy and elite chests carry leg armour that Erebor's plate
lacks (Known gaps).

### Wanderers

Twelve wanderers (`spc_wanderer_eredluin_0` to `_11` in `taom_wanderers.xml`), cloned from Erebor's
twelve by the promoter. Each has eight backstory rows of its own in `taom_wanderer_strings.xml`
(`prebackstory`, `backstory_a` to `_d`, `response_1`, `response_2` and `generic_backstory`), 96
rows at the end of the file. The stories are set in the Blue Mountains (Belegost, the forges of
Nogrod, the five castles, the Dwarf-road to Bree, the goblins of Blue Craig) and name no Erebor
place or person. The names keep Erebor's Khuzdul surnames (`{FIRSTNAME} Bit Barukkhaz` and the
rest; Known gaps).

### Diplomacy

| Where | Setting |
|-------|---------|
| `taom_spkingdoms.xml` | no start war: `value="0"` toward Blue Craig, the goblins, Gundabad and the Misty Mountain orcs; `value="1"` toward Erebor and Lindon |
| `execution/alignment.json` | `free` |
| `diplomacy/diplomacy.json` | Hostile with the nine realms Arthedain is Hostile with: Blue Craig, Dol Guldur, Dunland (`empire`), Mordor (`empire_s`), Goblin-town, Gundabad, Isengard, Rhûn (`khuzait`) and the Misty Mountain orcs; Natural with `erebor` and `lindon` |
| `diplomacy/war_of_the_ring.json` | not listed; its wars start on day 44 through the Hostile rows |
| `configs/army_targeting.json` | theater `north`; no `FactionPriorityTargets` or `FactionAggressionMultipliers` row names it (Blue Craig is passive) |

### Tables keyed on culture

Every table and compiled list that names `erebor` or the Arnor realms was decided for Ered Luin, and
a dwarf rule takes Erebor's row. These keep no row, as none has one for Arthedain or Cardolan:
`FactionUI/faction_characters.json`, `FactionUI/faction_map_pos.json`, `elite_emissary_config.xml`,
`TroopWeights/troop_weights.xml`, the kingdom messages of `siege/siege_defense_config.json`,
`tournament_rewards.json` (Erebor has no row either), `TaomCultureLinkStyles`,
`diplomacy/war_of_the_ring.json` and `tools/settlement_economy_floor.json`.

| Table | Ered Luin | Why |
|-------|-----------|-----|
| `execution/alignment.json` | `free` | the Free side, with Erebor and Lindon ("Diplomacy") |
| `diplomacy/diplomacy.json`, `configs/army_targeting.json` | 9 Hostile rows and 2 Natural; theater `north` | "Diplomacy" |
| `factionmap/factions.json` faction card | `kingdom_of_ered_duin`: playable, `game_faction` `eredluin`, side `free`, difficulty 5, "Kingdom of Ered Luin" | The non-playable Ered Duin placeholder, converted in place. Its key stays because the CC prefab (`CharacterCreationCultureStage.xml`), `factionmap/regions.json` and the card's image `faction_kingdom_of_ered_duin.png` use it. New text: six bonuses (six of Erebor's eight cultural feats, which the `eredluin` culture block carries; the promoter drops garrison wages and tariff income, the two that come from Erebor's wealth), three perks, three special units (King's Guard of Thorin's Halls, Master Arbalester of Nogrod, Warden of Thorin's Halls), three strengths and three weaknesses; the four traits are unchanged |
| `FactionUI/faction_kingdoms.json` | `kingdom_of_ered_duin` to `eredluin` | the card's Leader and Lords lists show Ered Luin's king and lords |
| `special_resources_config.xml` | `<Kingdom id="eredluin" />` and `<Culture id="eredluin" />` on Erebor's Gems | the dwarf mining resource; its shared tier texts still name Erebor (Known gaps) |
| `troop_resource_costs.xml` | no row, a comment | Erebor's only costed tree troops are the Ironpass war rams, which Ered Luin does not field; Erebor's other Gems rows are emissary offers at level 41 and up, past Ered Luin's level 36 King's Guard |
| `startup_resources_config.xml` | gold 75,000 per lord, influence 400, playerGold 3,000 | The file's runway rule: 150 days (a step under Erebor's 270) on the 4.86 denars a head measured from `kingdom_hero_party_eredluin_template`, 72,900 rounded. Influence and playerGold are a step under Erebor's 600 and 3,500; playerGold is open (Known gaps) |
| `realm_borders/palette.json` and MCM | `#7B68EE`, `RealmColourEredLuin` (Realm Colours, Order 25; Your Realm moved to 26) | a slate blue clear of Erebor's `#2A4DA8`; `RealmPaletteTests` holds every pair of colours at least `minimumDeltaE` (15) apart |
| `configs/battle_balance_config.json` survival bonus | 0.3 | Erebor's |
| `combat_mechanics/combat_mechanics_config.json` charge multiplier | 1.0 | Erebor's; no Ered Luin troop rides |
| `banner_bearers/banner_bearers_config.json` | `banner_of_oaken_shields_t1` | Erebor's |
| `crown_guard/crown_guard_cultures.json` | `eredluin_kings_guard` | one troop per culture: the King's Guard of Thorin's Halls, the L36 capstone |
| `settlement_guards/settlement_guards_config.xml` | a Thorin's Halls (`town_EL3`) row: `eredluin_kings_guard` (weight 2), `eredluin_halls_warden` (3), `eredluin_veteran_hall_warden` (3); a `<Spear>` row, `western_spear_3_t3` | the capital's own guards, as Fornost has; the spear is Erebor's |
| `hall_of_fire/hall_of_fire.json` | `sturgia` | Erebor's |
| `voice_accents/voice_accents.json` | `imperial` | Erebor's |
| `lord_banners/lord_banner_cultures.json` | `["sturgia"]` | Erebor's |
| `race_abilities/race_abilities.json` | no row | the `dwarf` race row gives every dwarf Stand Fast, every Ered Luin troop is `race="dwarf"`, and Erebor has no culture row either |
| Postern Gate (`PosternGateDefinition.cs`) | the Hidden Door, with `erebor` | the dwarf variant |
| Inn Rounds | `inn_rounds.json` `beer`; `str_taom_round_drink.eredluin` "blue-mountain stout" | |
| Liege titles | King and Queen of Ered Luin (`str_liege_title.eredluin`, `str_liege_title_female.eredluin`, tag `TaomEredLuinCultureTag`) | |
| `culture_marketplace/culture_marketplace_config.xml` | `armour_from="erebor"`, `<Stock from="erebor" kind="armour" />` and `kind="weapons"` | Its troops wear Erebor's armour and carry its weapons, and `armour_from` gives it Erebor's lord kit and lord's material. Erebor's war rams and their barding are neither armour nor weapons, so they stay out |
| `lotr_issues/taom_lotr_issues.xml` | its own Armourer's Commission (`lotr_armourer_commission_eredluin`, reward `sk_dwarf_iron_chest_heavy_b`); `eredluin` on Erebor's Deep Seam row (`lotr_deep_seam_erebor`) | the reward is the chest its level 26 troops (Veteran Hall Warden, Firebeard Marksman) wear; the Deep Seam pays Erebor's lord's material, which `armour_from` makes Ered Luin's |
| `culture_doctrine/culture_doctrines.json` | Erebor's row whole: Erebor's tactics, `neverRout`, bravery 15, `horseStand` dismount false | without a row Ered Luin takes the default doctrine and routs; the `horseStand` opt-in now covers five cultures (Known gaps) |
| C# lists | `TaomStartOptionsProvider.TaomKingdomIds`, `CustomBattleCommandersProvider.KnownCultureIds`, the compiled twins of the JSON rows above (`BannerBearerConfig`, `BattleBalanceConfig`, `CombatMechanicsConfig`, `CrownGuardMap`, `HallOfFireMap`, `LordBannerMap`, `VoiceAccentMap`), `RealmBordersSettingsProvider` and `CoopSettingsRelevance` (as the other realm colours), `VolunteerRecruitmentService.EredLuin.cs` | the compiled twins mirror the JSON rows; the Advanced Start Options picker's name is `str_campaign_starting_options_item_name.eredluin` in `global_strings.xml` |

## Careers and character creation

### Careers

`tools/insert_new_faction_careers.py` clones Erebor's foot careers: Ironguard twice, once for the
one-handed and shield career and once for the two-handed one, and the Crossbow Master once. Erebor's Ram Rider is not cloned, because its
passives are `MountChargeDamage` and `MountHealth`. Each career is eligible for `eredluin` alone, its
archetype is a row in `CareerSystemIoC.BuildCareerArchetypeMap`, and its `career_menu.json` entry
adds no skill or attribute, as no career's entry does.

| Career (id) | Archetype | Cloned from | Ranks | Ability |
|-------------|-----------|-------------|-------|---------|
| Warden of Thorin's Halls (`warden_of_thorins_halls`) | Infantry | Ironguard | Deep Warden, Warden of Thorin's Halls, Shieldwall of the Blue Mountains | Bastion of the Halls |
| Mattock Hewer of Belegost (`hewer_of_belegost`) | TwoHanded | Ironguard | Mattock-bearer, Hewer of Belegost, Stonebreaker of the Blue Mountains | Mattock Storm |
| Firebeard Arbalester (`nogrod_arbalester`) | Ranged | Crossbow Master | Bolt-Smith of Nogrod, Firebeard Quarreler, Forge-Lord of the Firebeards | Firebeard Volley |

**The two foot careers share their numbers.** Their ability templates, root choices and six
choice groups match value for value; they differ in names, text and the ability's name. A starting
kit is chosen by culture and archetype, so the Warden starts with the infantry kit and the Hewer with
the two-handed kit (below). The Hewer began as the Broadbeam Shield-guard, a second one-handed and
shield career with the same kit, and was replaced by the two-handed career on 2026-10-28 (#1045);
the troop `eredluin_broadbeam_shieldguard` keeps its name. Bespoke Hewer mechanics are a balance
follow-up (Known gaps).

**Ids and art.** `firebeard_arbalester` and `hewer` are parts of the troop ids
`eredluin_firebeard_arbalester` and `eredluin_hewer`, so the careers are `nogrod_arbalester` and
`hewer_of_belegost` while both display names echo their troop names. Art
and FX ids are the source careers': both foot careers name `career_ironguard_portrait`, the
Arbalester `career_crossbow_master_portrait`, which `TAOMSpriteData.xml` does not register (Known
gaps). Each career's wording is its own `CAREER_TEXT_REMAP` table in the tool, and the tool's
contamination gate refuses Erebor's place and people words.

### Narrative menus

`tools/insert_new_faction_cc_menus.py` gives Ered Luin six entries in each of the four culture-keyed
menus (parents, youth, education, adulthood): Erebor's mechanics entry for entry (skills,
attribute, focus, occupation and title types) with authored text (`AUTHORED["eredluin"]`),
registered as 48 `taom_cc_*` rows in `taom_cc_strings.xml`. The text is set in Thorin's Halls under
King Farin of the House of Borin, among the Broadbeams of Belegost and the Firebeards of Nogrod, the
iron mines of Mount Dolmed, the Dwarf-road through the Shire to Bree, the trade with Lindon and the
goblins of Blue Craig; it names no Erebor place or person. Childhood has no culture-keyed entries.

`charactercreation/cultures.json` has an `eredluin` row (races `["dwarf"]`) equal to Erebor's
except the starting settlement, `town_EL3` (Thorin's Halls); `cc_body_properties.xml` gives it
Erebor's default dwarf body.

### Starting gear

| Rosters | Made by | What they hand out |
|---------|---------|--------------------|
| 55 CC rosters (`taom_char_creation_equipment.xml`) | `generate_char_creation_equipment.py --append eredluin`, table `dict(CULTURES["erebor"], no_mount=True)` | Erebor's dwarf gear and no Horse or HorseHarness slot, so `Patch20_NarrativeHorseGuard` skips the horse actor in the narrative stages ([no-mount cultures](no-mount-cultures.md)). The 7 mothers wear `sk_dwarf_dress_normal_a` and the 7 fathers `sk_dwarf_tunic_noble_a` (the new `mother_body` and `father_body` keys, set on Erebor's table to what Erebor's shipped parents wear). The childhood and education rosters wear the dwarf leathers `sk_dwarf_erebor_chest_leather_light_a` and `_b`, where Erebor's shipped ones also carry Harad tunics |
| Starter kits | `wire_starter_kit_rosters.py --apply` | 142 ids in 16 rosters point at `starter_` twins, the same 11 twins Erebor's rosters use, so `generate_starter_kit.py` had nothing to write and nothing new went into the Armory |
| Career kits (`taom_career_starting_equipment.xml`) | `generate_career_kits.py` (`eredluin` rows in `SIDEARM` and `RANGED`; the `twohanded` layout needs none) on six hand-added rosters | `player_career_eredluin_{infantry,ranged,twohanded}_{m,f}`, no cavalry pair. Ranged: `ladder_erebor_xbow_t4`, `sm_dwarf_iron_bolt_a` and the axe `sm_dwarf_erebor_1h_axe_c`; the crossbow is the one Ered Luin's first crossbowman, the L21 Firebeard Arbalester, carries. Infantry: that axe, `sm_dwarf_erebor_shield_leather_a` and `sm_dwarf_erebor_spear_a`. Two-handed (the `twohanded` layout: a two-handed weapon and a throwing weapon, no one-handed weapon, shield or spear, so its roster has no `Item2`): `sm_dwarf_erebor_axe_2h_a` and `southern_throwing_axe_1_t4`, both carried by the lowest Mattock Hewer (L16; the mattock line throws from the Hewer up). All three wear `sk_dwarf_iron_chest_light_a` and `sk_dwarf_iron_boots_light_a` |

## Key Files

| File | Purpose |
|------|---------|
| `tools/generate_eredluin_troops.py` | the troop tree and soldier party templates; `--check` |
| `tools/realm_troops.py` | shared troop machinery for the Arnor realms, Ered Luin and [Anduin Vale and Angáladh](anduin-vale-angaladh.md) |
| `Main/Features/TroopProgression/RecruitmentPools/VolunteerRecruitmentService.EredLuin.cs` | volunteer pool (Delver 6, Noble 2) |
| `Main/_Module/ModuleData/troops/troops_eredluin.xml` | generated troop tree |
| `Main/_Module/ModuleData/characters/npcs_eredluin.xml` | notables and townsfolk |
| `Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_eredluin.xml` | the lord template rosters (`rosters_kept`; Erebor's ram rosters are not cloned) |
| `tools/insert_new_faction_cc_menus.py`, `tools/insert_new_faction_careers.py` | the narrative menus (`AUTHORED["eredluin"]`) and the three careers (`TARGETS["eredluin"]`, `CAREER_TEXT_REMAP`) |
| `Main/_Module/ModuleData/career_system/taom_careers.xml`, `taom_ability_templates.xml`, `taom_career_choices.xml` | the three careers |
| `Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml`, `taom_career_starting_equipment.xml` | the 55 CC rosters and the six career kits |
| `Main/_Module/ModuleData/factionmap/factions.json` | the faction card, key `kingdom_of_ered_duin` |
| `Main/_Module/ModuleData/taom_wanderer_strings.xml` | the 96 wanderer backstory rows, the last block of the file |
| live `TAOM_Map/ModuleData/settlements.xml` | the realm's 31 settlements (unversioned), the last rows of the file |

## Dependencies

- Erebor's dwarf kit in the live Armory (`LOTRLOME_items/erebor/`, `LOTRLOME_items/iron_hills/` and
  the dwarf weapons and shields in `LOTRAOM_weapons.xml` and `LOTRAOM_shields.xml`).
- Erebor's ladder bows and crossbows (`ladder_erebor_bow_t*`, `ladder_erebor_xbow_t*`).
- The dwarf race, skeleton and skin in the Armory.

## Tests

- `tools/tests/test_realm_troops.py`, and three `eredluin` rows in
  `tools/tests/test_generator_regions_in_sync.py`: the troop generator's, the kingdom generator's and
  the promoter's `--check`.
- `tools/tests/test_insert_new_faction_cc_menus.py` (new): Erebor's mechanics entry for entry, no
  Erebor place or person and no em or en dash in the text, every entry registered, the dry run
  writing nothing while `--check` fails, and `--apply` idempotent.
- `tools/tests/test_insert_new_faction_careers.py` (new): a source cloned twice gives two distinct
  careers that keep its effects; three Ered Luin careers and no rider, eligible for `eredluin` alone,
  no passive that needs a mount, and a `career_menu.json` entry and a foot archetype in the IoC map
  for each.
- `tools/tests/test_generate_char_creation_equipment.py` (new): Ered Luin's table hands out no mount
  and only dwarf gear and dresses the parents as Erebor's; a culture without parent bodies keeps the
  old shape; the 55 shipped rosters are all there, none mounted.
- `tools/tests/test_generate_career_kits.py`: Ered Luin's axe sidearm and crossbow, an infantry and
  a ranged roster for each sex, no cavalry pair and no mount.
- `CareerKitCoverageTests` now asks each culture for a kit per archetype among the careers it offers
  (read from `taom_careers.xml` through `CareerSystemIoC.GetCareerArchetypeMap`), not for all six, so
  Ered Luin needs no cavalry kit and a culture that gains a career gains its kit demand the same day.
- `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`: five Ered Luin tests
  (the Delver on rolls 0 and 5, the Noble on 6 and 7, the pool exists).
- `ClonedCultureFlavorTests` scans Ered Luin's player-facing text for 19 of Erebor's identity words
  (Erebor, Lonely Mountain, Dale, Smaug, Iron Hills, Dáin and the rest).
- Count and list pins moved with the data: `ArmourAcquisitionShippedDataTests` (22 commissions),
  `LotrIssueConfigProviderTests` (78 issues), `InnRoundsDataTests` (26 main cultures),
  `SettingsFingerprintTests` (468 reflected in `TaomSettings`), `ServerSettingsSchemaTests` (498),
  `ShippedCultureDoctrinesConfigTests` (the never-rout list, five `horseStand` cultures),
  `DiplomacyShippedConfigTests` (Arthedain's new-free-realm test runs for `eredluin` too),
  `FactionMapDataTests` (`kingdom_of_ered_duin`), `GlobalStringsOverridesTests`,
  `TroopUpgradeSkillMonotonicityTests` (`ExpectedMilitiaCultures`); in `tools/tests`,
  `test_generate_name_tags.py` (78 kingdom names), `test_militia_elite_bonus.py` (38 veteran militia),
  `test_rebalance_template_power.py` (50 templates) and `test_upgrade_skill_monotonicity.py` (76
  militia-bound troops).
- Existing gates that now cover it: `WandererBackstoryCoverageTests`, `RealmPaletteTests`,
  `ShippedClanSideCoverageTests`, `WarTheaterConfigInvariantsTests`, `CareerArchetypeServiceTests`,
  `HeroFaceVarietyDataTests`, `SupplyCaravanTemplateTests`, `NameGrammarTagTests`, the validator's
  `MOUNTED_DWARF`, `LANDLESS_CULTURE` and `MISSING_EDUCATION_TEMPLATES`.
- Map: `python tools/add_map_fortifications.py --check` and `python tools/add_map_villages.py --check`.

## Save Compatibility

A new campaign is required to play Ered Luin. A save made before it loads `settlements.xml` again
but never the new kingdom, clans or heroes (`SandBoxManager.InitializeSandboxXMLs`). The 31 new
rows sit at the end of the file, starting with the town `town_EL3`, so the first of them throws inside
`Settlement.Deserialize` (its alleys), `MBObjectManager.LoadXML` swallows it, and the unfinished rows
are dropped; every older settlement has already loaded. The mechanism is Arthedain's
([arthedain.md](arthedain.md) "Save Compatibility"). Once the distance cache is rebuilt for these
rows, a save made before them is expected to stop loading (v1.5.6 decompile, not reproduced; #1041).

## How to Change the Realm

The steps in [arthedain.md](arthedain.md) "How to Change the Realm" apply, with Ered Luin's own
generator and `--only eredluin`. `generate_new_faction_kingdoms.py` and
`promote_borrowed_cultures.py` only report what would change until you add `--apply`.

## How to verify in game

Restart Bannerlord fully, then start a new campaign.

1. Open the encyclopedia. Confirm the kingdom, listed as "Ered Luin", its four clans and 16 lords.
2. Find Thorin's Halls, Belegost and Nogrod west of the Shire, around the Gulf of Lune. Confirm all
   8 fortifications and 23 villages show Ered Luin's banner.
3. Visit an Ered Luin town. Confirm notables, wanderers and guards are dwarves and clothed.
4. Meet an Ered Luin lord's party in the field. Confirm the troops are clothed, bowmen and
   arbalesters hold their weapons, and no dwarf rides.
5. Check the eight ladies in the encyclopedia. Confirm none is bald.
6. Visit Thorin's Halls. Confirm the guards are the King's Guard and the Hall Wardens, and an Ered
   Luin lord calls his liege the King of Ered Luin in conversation.
7. Start a second character and open the Kingdom of Ered Luin on the faction map. Confirm the card
   is playable, Free and difficulty 5, shows the dwarf-hall image, its six bonuses and three special
   units, and its Leader and Lords lists show King Farin II and Ered Luin's lords.
8. Pick it and go through the narrative stages. Confirm each of the four menus offers six options
   set in the Blue Mountains, no horse appears, the mother wears the dwarf dress and the father the
   noble tunic, and the campaign starts at Thorin's Halls.
9. On the career screen, confirm three careers (Warden of Thorin's Halls, Mattock Hewer of Belegost,
   Firebeard Arbalester) and no rider. Take the Firebeard Arbalester and confirm the crossbow, bolts
   and axe are equipped on the dwarf; on another start take the Warden and confirm the axe,
   shield and spear, and take the Mattock Hewer and confirm the dwarf starts a battle holding the two-handed axe in both
   hands, with a stack of throwing axes in the second slot and no shield, spear or one-handed weapon; on
   the career menu, check how the preview poses the two weapons; with no career, confirm the dwarf starter kit.
10. Run `taom.audit_settlement_entrances` (cheat mode on). Confirm no `*_EL*` id is listed; older
    ids such as `castle_village_MM1_2` may still be. Correct any listed id as
    [settlements.md](../modding/settlements.md) "Add" step 7 describes (in the map editor, never in
    `settlements.xml` alone), then rebuild.
11. Rebuild the distance cache (Map Tools, Rebuild Now) for the 31 new settlements.

## Known gaps

- **TAOM and TAOM_Map ship Ered Luin together.** Since 2026-10-27 the live `TAOM_Map`
  `settlements.xml` carries the 31 settlements (`Culture.eredluin`, the 8 fortifications owned by
  `clan_eredluin_1` to `_4`), while the deployed TAOM v2.0.36 and every release channel lack the
  culture and the clans. Until this branch is merged and deployed, build no TAOM_Map editor package
  from the live install and rebuild no distance cache. `settlements.xml.bak_mapforts_20261027_204235`
  is the last state before Ered Luin, and each language's `loc_settlements.xml` has a backup of the
  same name; the `Main_map` scene, saved with the 31 entities, has no backup in `SceneObj`. The
  channel check is in [release-process.md](../reference/release-process.md).
- The armour is a placeholder from the Iron Hills and Erebor kit until Ered Luin has its own. Its
  engine-tier medians (262 at tier 5, 306 at tiers 6 and 7) sit above Erebor's (236, 264 and 277)
  although the lore makes Ered Luin the poorer realm, and `CROSS_CULTURE_ARMOUR_INVERSION` checks only
  the under-dressed direction. A decision for Mike.
- **Open decisions for Mike:**
  - playerGold 3,000, between Erebor's 3,500 and Cardolan's and Rhudaur's 2,000; 2,000 is the
    alternative.
  - The doctrine row came whole from Erebor, `horseStand` (dismount false) included, so five
    cultures now opt in to it although Ered Luin fields no mounted troop.
  - The Warden and the Hewer share every number; the Hewer has no two-handed mechanics of its own,
    only its own wording and starting kit.
  - The shared Gems tiers in `special_resources_config.xml` say "Erebor's forges burn bright" and
    "The wealth of Erebor flows", and an Ered Luin player sees them.
  - Erebor's 16 adult CC rosters carry `sumpter_horse` and `light_harness` (pre-existing, recorded
    in [no-mount cultures](no-mount-cultures.md)); Ered Luin's carry no mount.
  - Bind `taom_dwarf_town_01_forceatmo` to Thorin's Halls (`town_EL3`, now `sturgia_town_g`)? It
    carries town spawn tags (48 `sp_` tags, 60 `npc_common`), but whether it works as a town (gates,
    alleys, siege entities) needs his editor check, and a bound scene must ship in every release
    channel's `TAOM_Map/SceneObj`.
  - 77 elite-to-lord upgrade links in `armour_classes.xml`, all pre-existing, lead to a lord piece
    that is no better on any armour value and worse on at least one. By kit: Rhûn 32, Rivendell 25,
    Dol Guldur 16 (15 of them the Khamul line in the Armory's rhun folder, 1 an Uruk chest),
    Gondor 2, Gundabad 1 and Rohan 1. 49 of them lose 1 to 5 points in total and 20 lose more than
    20. This branch's armour-class fix exempts lord targets and left them alone: keep them, or
    re-link them.
- The twelve wanderers carry the same Khuzdul surnames as Erebor's twelve, as Cardolan's keep
  Gondor's.
- No art was made for Ered Luin: its faction card shows the dwarf-hall image the Ered Duin
  placeholder carried (`GUI/SpriteData/FactionMap/faction_kingdom_of_ered_duin.png`, which
  `FactionImageWidget` loads by the card's `image`).
- The three careers show no ability icon and the Firebeard Arbalester no portrait: `CareerScreenVM`
  builds the icon name from the ability template id, and `TAOMSpriteData.xml` registers no sprite for
  the cloned templates or for `career_crossbow_master_portrait`. Pre-existing for all 30 cloned
  careers; a follow-up.
- Dead data, the same shape in every generated realm: nothing binds
  `kingdom_hero_party_mercenary_eredluin_template` or `kingdom_hero_party_outlaw_eredluin_template`,
  and no lord uses the `_e` lord rosters (`eredluin_bat_template_medium_e`,
  `eredluin_civ_template_default_e`). A cleanup for all realms together.
- The six age-8 education tutors (`child_education_templates_stage_2_page_0_branch_0_eredluin` to
  `_5`) have no `race` and render as humans in human clothes, cloned from Erebor's identical gap. The
  fix is an engine-side patch that passes the race through the education screen, not data.
- Translations owed to the paid run (#763, row added 2026-10-28): the 536 ids seeded with English into each of the 12
  languages, the 2 faction-card keys whose English changed (`taom_faction_kingdom_of_ered_duin_name`
  and `_desc`, reset to English and evicted from the 12 translation caches), and the 31 Ered Luin
  settlement names in each language of the live TAOM_Map (each `taom_map` count in
  `tools/external-loc-coverage-baseline.json` rose by 31).

## Changelog

- 2026-10-27: created overnight on branch `feat/ered-luin-kingdom`, on Mike's map placement of
  2026-10-27. Along the way `tools/realm_troops.py` gained the dwarf fields and the crossbow role,
  and the live map was checked against the saved scene row by row after Mike's editor save.
  - Careers, narrative menus and starting gear. `insert_new_faction_cc_menus.py` became a dry run by
    default with `--apply` and `--check`; `insert_new_faction_careers.py` takes a list per source
    (`targets_for()`), per-career wording (`CAREER_TEXT_REMAP`) and `--check`;
    `generate_char_creation_equipment.py` gained `mother_body` and `father_body` and parses before
    it writes; `CareerKitCoverageTests` asks for a kit per offered archetype.
  - Every culture-keyed table and compiled list ("Tables keyed on culture"); the Ered Duin
    placeholder card became Ered Luin's.
  - Integration: the culture and kingdom descriptions speak of kin "in the east" instead of naming
    Erebor; 217 name-string rows regenerated; 536 ids seeded into each language; the
    `supply_caravan_template_eredluin` crew template; the BR and IT grammar tags, including the Ered
    Luin rows of the live TAOM_Map `loc_settlements.xml` (backup
    `loc_settlements.xml.bak_nametags_20261027_214259` beside each).

## GitHub Issue

- **Issue:** #1044.
- **Status:** built in data; in-game check owed ("How to verify in game").

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/anduin-vale-angaladh.md](./anduin-vale-angaladh.md)
- [docs/features/armour-acquisition.md](./armour-acquisition.md)
- [docs/features/army-targeting.md](./army-targeting.md)
- [docs/features/cardolan-rhudaur.md](./cardolan-rhudaur.md)
- [docs/features/career-system.md](./career-system.md)
- [docs/features/coop-dedicated-server.md](./coop-dedicated-server.md)
- [docs/features/kingdom-creation.md](./kingdom-creation.md)
- [docs/features/no-mount-cultures.md](./no-mount-cultures.md)
- [docs/features/starting-equipment-tuning.md](./starting-equipment-tuning.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/modding/settlements.md](../modding/settlements.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)
- [docs/reference/release-process.md](../reference/release-process.md)
- [docs/reference/taom-map-settlement-naming.md](../reference/taom-map-settlement-naming.md)
- [docs/reviews/rca-ered-luin-2026-10-27.md](../reviews/rca-ered-luin-2026-10-27.md)
- [docs/reviews/rca-ered-luin-two-handed-career-2026-10-28.md](../reviews/rca-ered-luin-two-handed-career-2026-10-28.md)

<!-- backlinks-end -->
