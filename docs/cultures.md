# Culture Creation Checklist for TAOM

## Current State
- **10 custom cultures** defined in `taom_spcultures.xml`: erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, umbar, dolguldur, gondor, mordor
- **6 XSLT passthrough cultures** in `spcultures.xslt`: empire(dunland), aserai(harad), vlandia(rohan?), khuzait(rhun), sturgia(dale/barding), battania(dunland)
- **16 total** NPC files, 14 troop files, 12 equipment files

---

## Complete File Checklist for a New Culture `{culture}`

### 1. Culture Definition (REQUIRED)
**File:** `Main/_Module/ModuleData/taom_spcultures.xml`
- Add `<Culture id="{culture}" ...>` block (~350 lines per culture)
- **Attributes needed (~80+):**
  - Identity: `id`, `name`, `text`, `is_main_culture`, `can_have_settlement`
  - Visual: `color`, `color2`, `faction_banner_key`, `encounter_background_mesh`, `board_game_type`
  - Troop refs: `basic_troop`, `elite_basic_troop`, `melee_militia_troop`, `ranged_militia_troop`, `melee_elite_militia_troop`, `ranged_elite_militia_troop`
  - Party template refs: `villager_party_template`, `default_party_template`, `elite_caravan_party_template`, `militia_party_template`, `rebels_party_template`, `vassal_reward_party_template`, `settlement_patrol_template_level_1/2/3`
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
  - `<basic_mercenary_troops>`

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

<!-- backlinks-end -->
