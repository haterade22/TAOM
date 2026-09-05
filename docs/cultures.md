# Culture Creation Checklist for TAOM

## Current State

_Recounted from the files 2026-08-12. The previous version of this block said "10 custom cultures",
listed only the first ten, and mapped battania to Dunland (it is Khand)._

- **24 cultures** defined in `taom_spcultures.xml`. Sixteen are settled: erebor, rivendell, mirkwood,
  lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor, shaghana, abanissa, goblin,
  mistymountainorcs, lindon, bluecraig. Eight are hideout-only bandit cultures: dunland_raiders,
  rhun_raiders, harad_raiders, gundabad_raiders, umbar_corsairs, gondor_soldiers, erebor_warriors,
  mirkwood_stalkers.
- **6 retagged vanilla cultures** in `spcultures.xslt`: empire (Dunland), aserai (Harad),
  vlandia (Rohan), khuzait (Rhun), sturgia (Dale/Barding), battania (Khand/Variag).
- **16 total** NPC files, 14 troop files, 12 equipment files

### Which cultures share another culture's troops and party templates

This mapping is load-bearing and lives only in the data, so it is recorded here. It is not a defect;
these cultures deliberately have no roster of their own.

Two of the rows arrived a different way. Blue Craig and the Misty Mountain Orcs were promoted out of
a borrowed culture in August 2026, and the promotion cloned a whole troop tree for each. Neither
clone ever diverged in a way a player could see, so all three goblin kingdoms shared one roster in
everything but the encyclopedia, which listed 69 near-identical troops. The clones were retired on
2026-08-29 and each culture kept a single bespoke capstone. The lesson generalises: a promoted
culture needs a roster of its OWN or a share, and a clone is neither.

| Culture | Shares | Why |
|---|---|---|
| lothlorien | rivendell | Both elven, one roster |
| battania (Khand/Variag) | rhun | No `khand_*` troop or template exists |
| umbar | harad | Has its own `umbar_elite` noble line, but no basic or militia line |
| shaghana | harad | Haradrim sub-culture, lord parties are entirely `harad_*` |
| abanissa | harad | Same |
| bluecraig | goblin | Its own tree was a clone of goblin's that never diverged. Keeps one bespoke T7 capstone, `bluecraig_bolgs_ironfang` ("Skarnak's Ironfang") |
| mistymountainorcs | goblin | Same clone, differing only in a race tag and skill numbers. Keeps `mistymountainorcs_bolgs_ironfang` |

**Deferred, tracked here so it is not lost:** umbar, shaghana and abanissa still carry roughly 40
vanilla `*_aserai` **NPC role** bindings between them (tavernkeeper, blacksmith, guard, ransom broker,
plus the child and teenager variants). Most repoint cheaply to an existing `_harad` id, but the
child/teenager ids have no `_harad` counterpart and need authoring. Left out of the 2026-08-12
party-template pass deliberately. See [kingdom-creation.md](features/kingdom-creation.md)
"What Can Be Inherited".

---

## Complete File Checklist for a New Culture `{culture}`

### 1. Culture Definition (REQUIRED)
**File:** `Main/_Module/ModuleData/taom_spcultures.xml`
- Add `<Culture id="{culture}" ...>` block (~350 lines per culture)
- **Attributes needed (~80+):**
  - Identity: `id`, `name`, `text`, `is_main_culture`, `can_have_settlement`
  - Visual: `color`, `color2`, `faction_banner_key`, `encounter_background_mesh`, `board_game_type`
  - Troop refs: `basic_troop`, `elite_basic_troop`, `melee_militia_troop`, `ranged_militia_troop`, `melee_elite_militia_troop`, `ranged_elite_militia_troop`
  - Party template refs (all eight are engine-read and all eight must be bound): `default_party_template`, `villager_party_template`, `militia_party_template`, `rebels_party_template`, `vassal_reward_party_template`, `settlement_patrol_template_level_1/2/3`. **`elite_caravan_party_template` used to be listed here and is not an attribute at all:** the deserializer takes caravans only from the child elements below. Contract + the two crash surfaces: [culture-playability-wiring.md](features/culture-playability-wiring.md)
  - Equipment roster refs: `default_battle_equipment_roster`, `default_civilian_equipment_roster`, `default_stealth_equipment_roster`, `duel_preset_equipment_roster`, `marriage_bride_equipment_roster`
  - Notary refs: `merchant_notary`, `artisan_notary`, `preacher_notary`, `rural_notable_notary`
  - Town/village NPC refs: `villager`, `caravan_master`, `caravan_guard`, `veteran_caravan_guard`, `prison_guard`, `guard`, `blacksmith`, `weaponsmith`, `townswoman`, `townsman`, `village_woman` + age variants (infant/child/teenager for each), `ransom_broker`, `gangleader_bodyguard`, `shop_worker`, `tavernkeeper`, `taverngamehost`, `musician`, `tavern_wench`, `armorer`, `horseMerchant`, `barber`, `merchant`, `beggar`, `female_beggar`, `female_dancer`
  - Character creation: `default_character_creation_body_property`, `start_point_position_x/y`
- **Child elements:**
  - `<caravan_party_templates>` and `<elite_caravan_party_templates>`
  - `<vassal_reward_items>` — culture-specific reward weapons/armor
  - `<banner_bearer_replacement_weapons>`
  - `<default_policies>` — starting kingdom policies
  - `<male_names>` — 50+ names with `{=aom_{culture}_male_name_N}` keys
  - `<female_names>` — 50+ names with localization keys
  - `<clan_names>` — 15+ clan names
  - `<cultural_feats>` — culture bonuses/penalties
  - `<possible_clan_banner_icon_ids>` — available banner icons
  - `<notable_templates>` — references to all 26 notables + wanderers
  - `<lord_templates>` — special faction leader templates
  - `<rebellion_hero_templates>`
  - `<tournament_team_templates_one/two/four_participant>` — tournament configs
  - `<basic_mercenary_troops>` — the town tavern's hire pool. **Must list `*_merc` troops of your own
    culture, never vanilla ids.** See "Tavern mercenaries" below.

### Tavern mercenaries (`<basic_mercenary_troops>`)

`RecruitmentCampaignBehavior.UpdateCurrentMercenaryTroopAndCount` rerolls each town's offer daily: 70%
of rolls draw a random entry from this list and then **randomly walk its `UpgradeTargets`** (each tier
deeper weighted 1/1.5×); the other 30% use the culture's `caravan_guard`. Whatever it lands on is sold
in the backstreet menu and spawned as the walking tavern NPC.

Three rules, pinned by `TAOM.Tests/Features/TroopProgression/TavernMercenaryDataTests.cs`:

1. **Reference `<source>_merc` copies, not line troops.** Copy the culture's rarest recruitment-pool
   troops (lowest `VolunteerChance` weight in `Main/Features/TroopProgression/RecruitmentPools/`) into
   dedicated entries with `occupation="Mercenary"` and a `{=aom_merc_<source>_name}[Culture] Hired …`
   name. Copies keep the source's skills and equipment; the originals stay `Soldier`, so notable
   recruitment and AI party wages are untouched (occupation drives ×2 recruit cost / ×1.5 wage through
   `TroopCostService`).
2. **`occupation="Mercenary"` is mandatory** — the tavern NPC's hire dialogue and AI lord/caravan
   hiring both gate on Mercenary/CaravanGuard/Gangster.
3. **No `<upgrade_targets>` on a `_merc` copy** — the engine's upgrade walk would otherwise drift the
   offer back onto a normal Soldier line troop.

Generator: `python tools/oneoff/generate_tavern_mercenaries.py` (idempotent; add the culture's picks to
its `PICKS` table).

### 2. NPC Character File (REQUIRED)
**File:** Create `Main/_Module/ModuleData/characters/npcs_{culture}.xml` (~900-1,300 lines)

**26 Notable NPCs (exact distribution):**

| ID Pattern | Occupation | Count |
|-----------|------------|-------|
| `spc_notable_{culture}_0` through `_4b` | Merchant | 10 |
| `spc_notable_{culture}_5`, `_6`, `_7` | Preacher | 3 |
| `spc_notable_{culture}_8`, `_9` | Artisan | 2 |
| `spc_notable_{culture}_gl1`, `_10`, `_11`, `_gl4`, `_12`, `_13` | GangLeader | 6 |
| `spc_notable_{culture}_21`, `_22` | RuralNotable | 2 |
| `spc_{culture}_headman_1`, `_2`, `_3` | Headman | 3 |

**Service/Townsfolk NPCs (all needed):**
- Tournament master, villagers, caravan master/guard/veteran guard
- Prison guard, guard, blacksmith, weaponsmith
- Townswoman/townsman + infant/child/teenager variants
- Village woman + child/teenager variants
- Ransom broker, gangleader bodyguard, shop worker
- Tavernkeeper, tavern game host, musician, tavern wench
- Armorer, horse merchant, barber, merchant
- Beggar, female beggar, female dancer

**12 Wanderer NPCs:**
- `spc_wanderer_{culture}_0` through `_11`

### 3. Troop Tree (REQUIRED)
**File:** Create `Main/_Module/ModuleData/troops/troops_{culture}.xml`
- Full troop progression tree (recruit -> elite tiers, T0-T10)
- Militia variants (melee + ranged)
- Each troop needs: skills, equipment roster refs, upgrade paths, culture assignment

### 4. Equipment Sets (REQUIRED)
**File:** Create `Main/_Module/ModuleData/taom_equipment_sets_{culture}.xml`
- Battle equipment rosters (tiers a-e)
- Civilian equipment rosters
- Stealth/duel/marriage bride rosters
- Troop-specific equipment rosters (referenced by troops file)

### 5. Party Templates (REQUIRED)
**File:** Add entries to `Main/_Module/ModuleData/taom_partyTemplates.xml`
- `villager_{culture}_template`
- `caravan_template_{culture}` + elite variant
- `kingdom_hero_party_{culture}_template`
- `militia_{culture}_template`
- `patrol_party_{culture}_template_level_1/2/3`
- `rebels_{culture}_template`
- `vassal_reward_{culture}_template`

### 6. Kingdom Definition (REQUIRED if independent faction)
**File:** Add to `Main/_Module/ModuleData/taom_spkingdoms.xml`
- `<Kingdom id="..." culture="Culture.{culture}" ...>`
- Includes: name, ruler, banner, relationships, initial settlements, policies

### 7. Clans (REQUIRED if has noble houses)
**File:** Add entries to `Main/_Module/ModuleData/characters/clans.xml`
- `<Faction id="clan_{culture}_N" culture="Culture.{culture}" super_faction="Kingdom.{kingdom}" ...>`
- Each clan needs: name, banner, tier, owner hero ref

### 8. Lords/Heroes (REQUIRED if has kingdom)
**Files:**
- `Main/_Module/ModuleData/characters/lords.xml` — lord NPCCharacter definitions with skills, equipment, body properties
- `Main/_Module/ModuleData/characters/heroes.xml` — hero biographical data
- After adding: run `python tools/rebalance_lords.py --apply` to balance skills

### 9. Settlements (REQUIRED if has territory)
**File:** `Main/_Module/ModuleData/settlements.xml` or `Main/_Module/ModuleData/custom_settlements.xml`
- Each settlement needs `culture="Culture.{culture}"` attribute
- Settlements also need scene/map entities (separate map work)

### 10. SubModule.xml Registration (REQUIRED)
**File:** `Main/_Module/SubModule.xml`
- Add `<XmlNode>` entries for each new file:
  - NPCCharacters file
  - Troops file
  - Equipment file
  - (Party templates, kingdoms, clans are likely already in shared files)

### 11. Localization Strings (REQUIRED)
**File:** `Main/_Module/ModuleData/taom_module_strings.xml`
- Culture name/description strings with `{=aom_{culture}_*}` keys
- All name lists use inline `{=key}` format in `taom_spcultures.xml`

### 12. C# Code (USUALLY NOT NEEDED)
- No changes needed unless adding culture-specific gameplay mechanics
- Existing `Main/Adapters/CultureObjectAdapter.cs` and `Main/Features/FactionMap/CultureResolverService.cs` handle culture resolution generically
- FactionMap Harmony patches handle culture selection UI automatically

---

## Gaps / Missing Files in Current Cultures

| Culture | Missing |
|---------|---------|
| Umbar | No `taom_equipment_sets_umbar.xml` |
| Dale | No `taom_equipment_sets_dale.xml` |
| Khand | No `taom_equipment_sets_khand.xml`, no `troops_khand.xml` |
| Lothlorien | No `taom_equipment_sets_lothlorien.xml` |

---

## Summary: Minimum Files to Create/Edit for a New Culture

| # | File | Action |
|---|------|--------|
| 1 | `taom_spcultures.xml` | Add ~350-line `<Culture>` block |
| 2 | `characters/npcs_{culture}.xml` | **Create** (~900-1300 lines, 26 notables + townsfolk + wanderers) |
| 3 | `troops/troops_{culture}.xml` | **Create** (full troop tree) |
| 4 | `taom_equipment_sets_{culture}.xml` | **Create** (battle/civilian/troop equipment) |
| 5 | `taom_partyTemplates.xml` | Add ~8 party template entries |
| 6 | `taom_spkingdoms.xml` | Add kingdom entry |
| 7 | `characters/clans.xml` | Add clan entries |
| 8 | `characters/lords.xml` | Add lord NPCCharacters |
| 9 | `characters/heroes.xml` | Add hero biographical entries |
| 10 | `settlements.xml` / `custom_settlements.xml` | Assign settlements to culture |
| 11 | `SubModule.xml` | Register new XML files |
| 12 | `taom_module_strings.xml` | Add localization strings |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/new-culture-authoring.md](ai-includes/new-culture-authoring.md)
- [docs/modding/id-cheatsheet.md](modding/id-cheatsheet.md)
- [docs/modding/npcs-notables-and-townsfolk.md](modding/npcs-notables-and-townsfolk.md)

<!-- backlinks-end -->
