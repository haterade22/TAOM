# New Factions — Misty Mountain Orcs, Goblins & Lindon

## Overview

Adds **four new kingdoms** and two new cultures to TAOM: the **Misty Mountain Orcs** and
**Goblins** (two new orc cultures), **Goblins of Blue Craig** (a second goblin-culture kingdom),
and **Lindon** (a new High-Elven kingdom that reuses the existing `rivendell` culture). All four
are full, **CC-playable** AI map-factions with settlements, clans, lords, troop trees, recruitment
pools, **cultural feats**, faction-map cards, and **forever-alliance diplomacy** ties. The orc
cultures' troops are infantry + archer lines wearing **orc armor** with mixed orc weapons.

Authored 2026-06-01 against scene entities the map author (user) placed in the live
`TAOM_Map` worldmap scene.

## Why This Exists

The Misty Mountains and the western Elven realm of Lindon were lore-only entries on the CC
faction map (`goblins_of_goblin_town`, `kingdom_of_moria`, `high_kingdom_of_lindon`) with no
backing game factions. This feature promotes them to real, conquerable kingdoms so the
central Misty Mountains and the Grey Havens are populated and playable in the campaign.

## Factions

| Kingdom id | Culture | Region code (settlements / lords) | Capital | Fiefs | Side |
|---|---|---|---|---|---|
| `mistymountainorcs` | `mistymountainorcs` (race `orc`) | `MM` / `MM` | `town_MM1` (Hrakdûr) | 3 towns, 7 castles, 23 villages | evil |
| `goblin` | `goblin` (race `goblin`) | `GT` / `GB` | `town_GT1` (Goblin Town) | 1 town, 6 villages | evil |
| `bluecraig` | `goblin` (race `goblin`) | `GBC` / `BC` | `town_GBC1` (Blue Craig)† — far west, by Lindon | 1 town + 4 villages (+ user-placed castles) | evil |
| `lindon` | `rivendell` (race `elf`) | `LN` / `LN` | `town_LN1` (Mithlond) | 1 town, 4 villages* | free |

\* The 4 Lindon villages (`village_LN1_1..4`) have been **placed in the map editor** (positions in
`settlements.xml` are the live editor placements, ≈232–319 X).

† `town_GBC1` (Blue Craig) has been **placed in the map editor** (≈250.97, 1270.66 — far NW by the
Grey Havens). **Blue Craig is the SECOND, separate goblin kingdom** — a western realm distinct from
the Misty Mountains goblins of Goblin Town (its clans are `clan_bluecraig_*` in `Kingdom.bluecraig`,
lords `lord_BC*` — no overlap with `clan_goblin_*`). Its castles/villages are still to be added by the
map author (drop them into `tools/taom_new_factions_layout.json` + re-run the settlements generator).

### Village economy (orc/goblin)

Goblin + Misty Mountain Orc villages follow a per-fief rule (`tools/assign_orc_village_types.py`):
**mostly animal food (swine_farm / cattle_farm) + ~1 iron/silver mine per fief**; fiefs with **4+
villages** add a **lumberjack** for variety. (1 village → animal; 2-3 → (n-1) animal + 1 mine; 4+ →
(n-2) animal + 1 mine + lumberjack; mine alternates iron/silver across fiefs.) VillageType stringIds
are code-registered (DefaultVillageTypes) — the rule uses only verified-valid ids (`cattle_farm`, NOT
`cattle_range`), guarded by a `VALID_VILLAGE_TYPES` check. Lindon villages keep an elven economy
(lumberjack / fisherman / wheat / vineyard). The settlements generator now applies the same rule, so
re-running it reproduces the live file exactly (positions + types) without clobbering editor work.

### Clan → fief ownership

- **mistymountainorcs** (5 clans): clan_1 = town_MM1 + castle_MM1 (ruler), clan_2 = town_MM2 +
  castle_MM2, clan_3 = town_MM3 + castle_MM3, clan_4 = castle_MM4 + MM5, clan_5 = castle_MM6 + MM7.
- **goblin** (5 clans): clan_1 = town_GT1 (ruler); clans 2-5 = landless vassal warbands.
- **bluecraig** (5 clans): clan_1 = town_GBC1 + 3 villages (ruler); clan_2 = castle_GBC1 (Krathol) + 2
  villages; clan_3 = castle_GBC2 (Gorgrim) + 2 villages; clan_4 = castle_GBC3 (Skarnak) + 4 villages;
  clan_5 = castle_GBC4 (Bolgkrag) + 4 villages. The 4 castles + 12 castle-villages were added by
  `tools/add_bluecraig_castles.py` (positions taken FROM the author's `scene.xscene` placements — every
  settlement MUST have a worldmap-scene entity or `SettlementVisual.OnStartup` NREs at map load).
- **lindon** (2 clans): clan_1 = town_LN1 (ruler), clan_2 = landless vassal.

### Lords / population

Lord counts (region code → `lord_<CODE><clan>_<n>`): the three orc/goblin kingdoms — **goblin (GB,
Goblin Town)**, **mistymountainorcs (MM)**, and **bluecraig (BC, Blue Craig)** — each get **~40 lords**
(5 clans × 8; numerous, to fight off many enemies). **lindon (LN)** gets 10 (2 clans). Every clan is
male-dominant with **≥2 females**, and each female is paired as a **spouse** to a clan male
(within-clan, the Gundabad precedent) at childbearing age — so the clans reproduce and the factions
endure. ~130 new lords/heroes total; all spouse refs reciprocal + opposite-gender + resolved.

### Diplomacy — forever alliances (`diplomacy/diplomacy.json`)

TAOM's diplomacy uses `AllianceTier`: **Permanent** (force-started at launch, war forbidden),
**Natural** (allied, breakable), **Hostile** (no alliance, war allowed), **Neutral** (default).

- **Goblins + Misty Mountain Orcs + Blue Craig** — `Permanent` with each other AND with the
  Sauron/shadow bloc (`empire_s`/Mordor, `isengard`, `gundabad`, `dolguldur`, `aserai`/Harad,
  `khuzait`/Rhun); `Natural` with `umbar`/`shaghana`/`abanissa`/`empire`(Dunland); `Hostile` to the
  Free Peoples (`empire_w`, `vlandia`, `erebor`, `rivendell`, `lothlorien`, `mirkwood`, `sturgia`).
- **Lindon** — `Permanent` with `rivendell`; **Neutral** to everyone else (incl. the orcs), per the
  isolationist Grey-Havens design. (Lindon is deliberately excluded from the orc Hostile list.)

**Execution alignment (`execution/alignment.json`).** A SEPARATE kingdom-keyed config that
`AlignmentService` reads (free/evil/neutral side) — it drives `TaomExecutionRelationModel`
execution-relation penalties and the `DiplomacyService.IsWarAllowed` same-alignment war-block
backstop. The 4 new kingdoms MUST have a row here or `GetKingdomSide` falls back to `Neutral`
(mis-scores both). Set: `goblin`/`mistymountainorcs`/`bluecraig` = `evil`, `lindon` = `free`.
(Missed in the initial authoring; caught by the completeness-audit workflow — see RCA W1.
General rule: adding a faction means updating EVERY kingdom-enumerating config, not just diplomacy.)

Villages inherit their owner from the bound town/castle (no `owner` attribute on villages).

## Architecture

The two orc cultures are **clones of the `gundabad` orc culture** (the closest existing
template) with ids/culture/race/loc-keys renamed, then reshaped by
[`tools/mordor_armor_remap.py`](../../tools/mordor_armor_remap.py):
- **Armor → orc-only pool** `sk_md_orc_*` / `sk_gn_orc_*` (by slot+tier). **No `sk_uruk_mordor_*`** —
  those are Black-Uruk-sized and too big for regular orcs/goblins.
- **Weapons → mixed** Gundabad + Mordor + Dol Guldur (1h sword/axe/mace cycled; spear/shield/javelin/bows
  stay Gundabad/orc — role-preserving).
- **Infantry + archer lines only — no cavalry.** All `Cavalry`/`HorseArcher` troops (warg_tamer,
  tracker, scout, dread_rider, despoiler — 5 per culture) are stripped, with their `upgrade_target`
  refs and party-template stacks removed. 23 troops each (16 Infantry, 6 Ranged + militia).

Faces reuse `BodyProperty.fighter_gundabad`. Every remap target is verified present in Mordor/Dol
Guldur data, so there are zero broken refs. Display names read as `[Goblin] Goblin Warrior` /
`[Misty Mountains] Orc …`.

**Clone DISPLAY-text remap (important — see RCA P0/C2/W2).** The id-rename is lowercase and
case-sensitive, so it leaves player-facing strings (culture names, descriptions, `<clan_names>`,
notable names, harvested loc strings) saying the SOURCE faction's words. Both `clone_transform`
(generate) and `transform` (insert) now remap the source race/faction WORDS — `[Gundabad]`→tag,
`Pale Uruk`/`Pale Orc`/`pale orc`/plain ` orc `→ this culture's race word (culture-aware: a no-op
for the orc culture, `orc`→`goblin` for the goblin culture), capital `Gundabad`→race word, plus
bespoke per-culture phrase subs for the lore descriptions (a word-swap can't fix "Mount Gundabad").
`generate_new_factions.py` ends with a **post-generation assertion that fails the build** if any
`Gundabad`/`Pale Uruk`/`Pale Orc`/`pale orc` survives in a generated player-facing field — so a
future clone-source change can't silently reintroduce the class. (Preserved lowercase technical ids
like `Item.wm_gundabad_*` / `BodyProperty.fighter_gundabad` are intentional and not touched.)

### Cultural feats

Both orc cultures have authored feats (`Main/Features/CulturalFeats/TaomCulturalFeats.cs` +
`CulturalFeatsService.cs` + `<cultural_feats>` in `taom_spcultures.xml`):

| Culture | Feats |
|---|---|
| **goblin** | +40% party size (Goblin Swarm), +25% volunteer respawn (Endless Spawn), +10% snow speed (Tunnel-Runners), **+20% food consumption** (Ravenous Swarm, penalty) |
| **mistymountainorcs** | −40% army influence cost (Orc Horde), +30% party size (Mountain Host), +10% snow speed (Mountain-Bred), **+15% food consumption** (Hungry Host, penalty) |

Big party-size bonuses offset weaker troops; the food-consumption penalty is the cost of the
horde. Snow-speed reflects their snow-navmesh mountain home. Lindon inherits Rivendell's 6
feats automatically (shared culture). Feat string-ids in XML exactly match the C# `Register()`
ids (verified); all 8 are dispatched in `CulturalFeatsService`.

### Playable faction cards

All 3 are `playable=true` in `factions.json` (`goblins_of_goblin_town`→goblin,
`kingdom_of_moria`→mistymountainorcs, `high_kingdom_of_lindon`→rivendell) with bonuses/perks
that match the authored feats. Lindon's card mirrors Imladris (same Rivendell culture/feats).
Strings registered via `tools/harvest_factionmap_strings.py`. Added to `cultures.json` (CC
culture list) and `CareerCultureCoverageTests` documentedExceptions (careers are a follow-up).

Lindon reuses the `rivendell` culture wholesale (troops, npcs, equipment, party templates,
cultural feats) — only the kingdom, clans, lords, heroes, and settlements are new. Its lords
use the rivendell equipment rosters (`rivendell_bat_template_medium_a..e`) and the generic elf
skill templates (`taom_elf_king/warrior/lady_skills`).

### Generators (re-runnable, idempotent)

| Script | Produces |
|---|---|
| `tools/taom_new_factions_layout.json` | Authoritative settlement/clan/kingdom layout (built from scene positions) |
| `tools/generate_new_factions.py` | `troops_{c}.xml`, `npcs_{c}.xml`, `taom_equipment_sets_{c}.xml` (orc cultures) |
| `tools/insert_new_factions.py` | Inserts wanderers, wanderer-equipment, party templates, culture blocks, SubModule regs, culture loc-strings (marker-wrapped, idempotent) |
| `tools/generate_new_faction_kingdoms.py` | 4 kingdoms + clans + ~130 lords + heroes (templates read by id, parametrized) |
| `tools/generate_new_faction_settlements.py` | `<Settlement>` blocks; `--apply` writes the live `TAOM_Map/settlements.xml` (+ timestamped backup) |
| `tools/assign_orc_village_types.py` | Per-fief orc/goblin village economy (animal food + 1 mine; `VALID_VILLAGE_TYPES` guard); `--apply` edits the live settlements.xml in place |
| `tools/make_new_factions_playable.py` | The 4 CC faction-map cards in `factions.json` (`--apply` dry-run guard); then `tools/harvest_factionmap_strings.py` propagates the card strings into `taom_module_strings.xml` |
| `tools/insert_new_faction_cc_menus.py` | Clones gundabad's CC narrative menu entries → goblin/mistymountainorcs (parents/youth/adulthood/education); childhood is culture-independent. Fixes blank CC stages. |
| `tools/add_bluecraig_castles.py` | Adds the 4 Blue Craig castles (`castle_GBC1..4`) + 11 castle-villages to the live settlements.xml, spread across clans 2-5 (`--apply` + backup; idempotent; positions FROM scene.xscene). Only adds ids already placed in the scene. |

Re-run order: `generate_new_factions.py` → `insert_new_factions.py` → `generate_new_faction_kingdoms.py`
→ `generate_new_faction_settlements.py --apply`. All inserts are idempotent
(`<!-- TAOM-NEWFACTIONS:… -->` markers stripped + re-inserted).

### Child / teenager / lord / education equipment templates (REQUIRED — child-generation crash, issue #267)

A custom culture whose clans get lords triggers `InitialChildGeneration` → vanilla `HeroCreator.CreateChild` →
`EquipmentSelectionModel.GetEquipmentForInitialChildrenGeneration`, which searches for a **culture-matching**
equipment roster flagged `IsChildEquipmentTemplate` (young child) or `IsTeenagerEquipmentTemplate` (teen),
both also `IsLordTemplate`; **if none exists it returns null and the game NREs on new-game.** Custom cultures
get NONE of these for free (XSLT/vanilla cultures inherit vanilla's). So `insert_new_factions.py` also clones
gundabad's rosters from three files (idempotent, armor/weapon remap via `transform()`; education keeps vanilla
childhood clothing):

| File | Flags | Per culture |
|---|---|---|
| `taom_child_equipment_templates.xml` | `IsChildEquipmentTemplate` + `IsLordTemplate` (+`IsFemaleTemplate`) | 6 (noble/townsman/villager × m/f) |
| `taom_lord_template_equipment.xml` | `IsLordTemplate`, and `IsTeenagerEquipmentTemplate` on the teen rosters | 10 (lord/ruler battle/civ/teen × m/f) |
| `taom_education_equipment_templates.xml` | none (id-suffix `_<culture>`, childhood-education events) | 98 |

bluecraig (Culture.goblin) and Lindon (Culture.rivendell) inherit their culture's templates. Guarded by
`ConfigIdValidationTests.ChildGenerationCultures_HaveChildTeenAndLordEquipmentTemplates` +
`NewOrcCultures_HaveChildEducationEquipmentRosters`. RCA: [docs/reviews/rca-new-factions-2026-06-02.md](../reviews/rca-new-factions-2026-06-02.md) Phase 4.

## Key Files

| Layer | File | New / Modified |
|---|---|---|
| Cultures | `taom_spcultures.xml` | +2 `<Culture>` blocks (no `<cultural_feats>`) |
| Troops | `troops/troops_goblin.xml`, `troops/troops_mistymountainorcs.xml` | new (28 troops each, cloned) |
| NPCs | `characters/npcs_goblin.xml`, `…_mistymountainorcs.xml` | new (69 NPCs each) |
| Equipment | `equipmentsets/taom_equipment_sets_{goblin,mistymountainorcs}.xml` | new (10 rosters each) |
| Wanderers | `taom_wanderers.xml`, `equipmentsets/taom_wanderer_equipment.xml` | +10 wanderers + roster per culture |
| Party templates | `taom_partyTemplates.xml` | +12 kingdom templates per orc culture |
| Kingdoms | `taom_spkingdoms.xml` | +3 `<Kingdom>` blocks + sibling relationships |
| Clans | `characters/clans.xml` | +9 `<Faction>` rows |
| Lords | `characters/lords.xml` | +42 `<NPCCharacter>` lords |
| Heroes | `characters/heroes.xml` | +42 `<Hero>` rows |
| Settlements | `TAOM_Map/ModuleData/settlements.xml` (LIVE, external) | +45 `<Settlement>` (968 total) |
| Registration | `SubModule.xml` | +6 `<XmlNode>` (troops/npcs/equipment ×2 orc cultures) |
| Recruitment | `Features/TroopProgression/VolunteerRecruitmentService.cs` | +`InitializeGoblinCulture/MistyMountainOrcsCulture/RivendellCulture` (fixes the previously-missing `CultureMap["rivendell"]`, which also benefits the existing Rivendell kingdom) |
| Faction map | `factionmap/factions.json` | `game_faction` set on `goblins_of_goblin_town`→goblin, `kingdom_of_moria`→mistymountainorcs (lindon already →rivendell) |
| Localization | `taom_module_strings.xml` | `str_faction_*` / `str_culture_*` per orc culture |
| Tests | `VolunteerRecruitmentServiceTests.cs` (+6), `ConfigIdValidationTests.cs` (sets 16→18 cultures, 18→21 kingdoms) | modified |

## Tests

- `VolunteerRecruitmentServiceTests` — low/high-roll for goblin, mistymountainorcs, rivendell pools (6 new).
- `ConfigIdValidationTests` — culture set 16→18, kingdom set 18→21, count assertions updated.
- Full suite: **2902 pass, 0 fail.** `tools/validate_moduledata.py`: PASS. Cross-ref chain
  (kingdom→owner→hero, clan→owner/kingdom/home, settlement→owner-clan, lord→hero, village→bound):
  all consistent.

## Known Limitations / Follow-ups (placeholders by design)

1. **Cultural feats — DONE** (4 each; see above). Lindon inherits rivendell's feats.
2. **CC-playable — DONE** (all 3 `playable=true` with full faction cards).
3. **Lindon villages** — `village_LN1_1..4` need scene entities placed in the map editor.
4. **Battle-scene grid** — new settlement positions in the central Misty Mountains / Grey Havens may
   need worldmap battle-scene-grid coverage so field battles near them load correct terrain
   (see `docs/reference/scene-reference-audit.md`). This is map-editor work.
5. **Troop trees** are Gundabad-tree clones (infantry + archer only) wearing **orc armor**
   (`sk_md_orc_*`/`sk_gn_orc_*`) + mixed Gundabad/Mordor/Dol-Guldur weapons (placeholder).
   Authoring dedicated Goblin / Misty-Mountain trees + bespoke armor is optional polish via
   `docs/ai-includes/new-culture-authoring.md`.
6. **Careers — DONE** (2026-08-10). Each orc culture has three careers cloned from Gundabad's by
   `tools/insert_new_faction_careers.py`; both cultures are un-parked from
   `CareerCultureCoverageTests` documentedExceptions. Art and FX ids are deliberately reused from
   the source careers rather than minted, so the clones are exactly as well-resourced as their
   originals — only 21 of the 50 shipped careers have a portrait registered in `TAOMSpriteData.xml`.

8. **CC starting equipment + starting denars — DONE** (2026-08-10). Both were missing outright and
   both failed silently: the player finished character creation naked and on zero denars. Neither
   was listed here because neither was known. See
   [culture-playability-wiring.md](culture-playability-wiring.md) for the full checklist that now
   separates *selectable* from *playable*, and `PlayerStartCoverageTests` for the gate.
7. **Localization** — culture/kingdom display names use inline-default `{=…}` keys (English-only);
   faction-map card strings ARE registered (via `harvest_factionmap_strings.py`). Run
   `tools/translate_with_claude.py` to translate.

## Note on the `goblin` id

The user typed the kingdom id as `golbin`; it was implemented as `goblin` (the kingdom is named
"Goblins"; `golbin` read as a transposition typo). IDs are save-immutable — confirm before
starting a campaign if `golbin` was intended.

## Changelog

- 2026-06-23 — Misty Mountain Orcs grown from 5 to 15 clans (10-strong 6♂/4♀ warbands, +110 lords) via `generate_mistymountain_clans.py`; ownerless-clan audit clean.
- 2026-06-02 — Codex + completeness-audit fixes: remapped Gundabad clone-leftover display text, reworked faction-map cards off the stripped cavalry, added the 4 kingdoms to `execution/alignment.json`.
- 2026-06-02 — Deep-review fixes: `cattle_range`→`cattle_farm` VillageType, `TAOM_Map`→TAOM module dependency declared, Blue Craig given placeholder villages/economy.
- 2026-06-02 — Reviewed editor-saved settlements and assigned per-fief orc/goblin village economies (animal food + ~1 mine; generator reproduces the live file).
- 2026-06-02 — Blue Craig confirmed as a separate western goblin kingdom by Lindon and expanded to ~40 lords.
- 2026-06-02 — Goblin + Misty Mountain Orc lord rosters expanded to ~40 each with within-clan breeding pairs.
- 2026-06-02 — Added the Goblins of Blue Craig kingdom and forever-alliance diplomacy (`diplomacy/diplomacy.json`).
- 2026-06-02 — Added cultural feats, CC-playability, orc-only armor + no-cavalry troops, and Third-Age lord/settlement names for Misty Mountains + Lindon.
- 2026-06-01 — Initial feature: three new kingdoms and two new cultures (Misty Mountain Orcs, Goblins, Lindon) as full AI map-factions.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/culture-playability-wiring.md](./culture-playability-wiring.md)

<!-- backlinks-end -->
