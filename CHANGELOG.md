# CHANGELOG — TAOM (Tales From the Age of Men)

## 2026-03-06

### Faction & Culture Strings

Added missing faction/culture strings for all 16 cultures, fixing "ERROR: Text with id str_faction_ruler doesn't exist!" for custom cultures like Erebor.

- Created `taom_module_strings.xml` — 192 faction strings (12 types × 16 cultures) covering ruler titles, noble titles, faction adjectives, formal/informal names
- Created `module_strings.xslt` — removes vanilla faction strings for 6 remapped cultures (empire→Dunland, vlandia→Rohan, battania→Khand, khuzait→Rhûn, aserai→Harad, sturgia→Dale)
- Updated `SubModule.xml` — registered both new GameText files

### Wanderer/Companion System — Complete Implementation

Implemented a full companion/wanderer system for all 14 kingdoms. Wanderers spawn in taverns, can be recruited, and have unique backstories, skills, and equipment.

**Batch 1 — LOTRAOM Conversion (6 kingdoms, 69 wanderers)**
- Extracted and converted wanderer data from LOTRAOM source files
- Gondor (13), Mordor (15), Gundabad (10), Isengard (10), Erebor (12), Rohan (9)
- Created `taom_wanderers.xml` — NPCCharacter templates with `occupation="Wanderer"`
- Created `taom_wanderer_skill_sets.xml` — 69 SkillSet definitions
- Created `taom_wanderer_equipment.xml` — 6 kingdom-specific companion equipment rosters
- Created `taom_wanderer_strings.xml` — 530 backstory dialogue strings
- Created `tools/extract_wanderers.py` — extraction/conversion script

**Batch 2 — Generated Wanderers (8 kingdoms, 80 wanderers)**
- Generated wanderers for kingdoms without LOTRAOM data
- Rivendell (10), Mirkwood (10), Lothlorien (10), Dol Guldur (10), Dunland (10), Harad (10), Rhun (10), Umbar (10)
- 10 archetype roles per kingdom: Engineer, Warrior, Scout, Healer, Trader, Rogue, Tactician, Smith, Cavalryman, Archer
- Added 80 NPCs, 80 skill sets, 8 equipment rosters, 640 backstory strings
- Created `tools/generate_batch2_wanderers.py` — generation script

**Culture Wiring**
- Updated `taom_spcultures.xml` — renamed `notable_templates` to `notable_and_wanderer_templates` for all 10 custom cultures, added wanderer template references
- Updated `spcultures.xslt` — replaced vanilla wanderer passthrough with LOTR wanderer references for Rohan (vlandia), Dunland (empire), Harad (aserai), Rhun (khuzait)
- Registered 4 new XML files (wanderers, skill sets, equipment, strings) in `SubModule.xml`

### Phase 1 Completion — Remaining Kingdoms

**Isengard**
- Added 4 militia troops (spearman, archer/crossbow, veteran variants) with uruk_hai race
- Added 46 NPCs (`npcs_isengard.xml`) — townsman, villager, guard, merchant, tavern staff, etc.
- Added 10 equipment rosters (`taom_equipment_sets_isengard.xml`) — 5 battle + 5 civilian
- Added 12 party templates in `taom_partyTemplates.xml`
- Wired all Isengard-specific refs in `taom_spcultures.xml` (replaced Sturgia placeholders)
- Added 6 education character templates + 98 education equipment templates

**Mordor, Rohan, Dunland, Harad, Rhun**
- Added 46 NPCs each (`npcs_{kingdom}.xml`)
- Added 10 equipment rosters each (`taom_equipment_sets_{kingdom}.xml`)
- Added militia troops for Rohan, Dunland, Harad, Rhun (4 per kingdom)
- Added 12 party templates each for Harad, Rhun, Isengard
- Wired culture-specific refs in `taom_spcultures.xml` and `spcultures.xslt`
- Created `tools/generate_xslt.py` — XSLT generation script

### Bug Fixes

- Fixed XSLT AVT conflict — escaped 469 `{=id}text` localization strings in literal element attributes as `{{=id}}text` to prevent XPath evaluation errors during XSLT compilation
- Fixed duplicate item `dunland_caerdh_pauldron__elite_a` in LOTRLOME_Armory `shoulder_armors.xml`
- Fixed duplicate monster `uruk_settlement` in LOTRLOME_Armory `monsters.xml`

---

## 2026-03-05

### Phase 1 — Kingdom Infrastructure (First Batch)

**NPC Characters**
- Created NPC files for Erebor, Rivendell, Mirkwood, Dol Guldur, Gundabad (`npcs_{kingdom}.xml`)
- Each kingdom has ~46 NPCs: townsman, villager, guard, merchant, tavern staff, etc.

**Equipment Rosters**
- Created per-kingdom equipment sets for Erebor, Rivendell, Mirkwood, Dol Guldur, Gundabad
- 5 battle + 5 civilian templates per kingdom using kingdom-specific armor and weapons

**Party Templates**
- Created `taom_partyTemplates.xml` with initial party template definitions

**Education Templates**
- Created `taom_education_character_templates.xml` and `taom_education_equipment_templates.xml`

**Troop Updates**
- Added militia troops for Erebor, Rivendell, Mirkwood, Dol Guldur, Gundabad
- Updated existing troop files with correct body properties and militia references

**Culture Wiring**
- Updated `taom_spcultures.xml` with kingdom-specific NPC, troop, equipment, and party template references

### Other

- Added Warsails naval mod integration guide (`docs/warsails-custom-map-guide.md`)
- Settlement data backup

---

## 2026-02-14

### Settlement Names

- Created `tools/Apply-SettlementNames.ps1` — script to apply LOTR settlement names from mapping file
- Applied LOTR names to `settlements.xml`

### Battle Scene Diagnostics

- Added `MBMapScene_GetBattleSceneIndexMap_Patch` — diagnostic patch for index map retrieval
- Added `MapScene_Load_DiagnosticPatch` — diagnostic patch for battle scene loading

---

## 2026-02-11

### Battle Scenes

- Implemented battle scene system (`sp_battle_scenes.xml`)
- Added `Campaign_InitializeScenes_Patch` — Harmony patch to load custom battle scenes
- Added guards and error handling for map loading

### Settlements & Locations

- Updated settlement data and clan/kingdom starting positions
- Updated `spclans.xslt` and `spkingdoms.xslt` with settlement references
- Fixed typo in `settlements.xml`

---

## 2026-02-10

### Settlement Tooling

- Created `tools/Settlement-Breakdown.ps1` — script to categorize and summarize settlements
- Created `tools/Generate-SceneEntitiesDoc.ps1` — script to generate scene entity documentation from scene file
- Updated `docs/scene-entities.md` with generated documentation
- Created `settlements.xslt` — XSLT stylesheet to transform and filter Settlement elements
- Updated settlement data

---

## 2026-02-09

### Settlements

- Added Far Harad region support with new castle and village entries
- Updated gate positions for Far Harad settlements
- Updated scene entity counts and corrected entity names in documentation

### Documentation

- Added `docs/ai-includes/agent-teams.md` — guide for using agent teams for parallel work
- Updated `CLAUDE.md` with agent teams section

---

## 2026-02-07

### Settlements

- Created initial `settlements.xml` with 658 settlements generated from scene.xscene
- Created `tools/Generate-Settlements.ps1` — settlement generation script from scene data
- Created `docs/scene-entities.md` — scene entity reference documentation for towns, castles, villages

---

## 2026-01-30

### Bug Fixes

- Updated Gondor male names for accuracy and consistency

---

## 2026-01-29

### Race System — HeroRace Feature

Implemented custom race handling for non-human characters (dwarves, orcs, uruk-hai).

**Core Infrastructure**
- Created `RaceManager` — domain service for race position configuration
- Created `ReflectionService` — infrastructure service for accessing internal TaleWorlds types
- Created `PathService` / `ModulePathAdapter` — module path resolution
- Created `FaceGenAdapter` / `IFaceGenAdapter` — adapter for sealed FaceGen types
- Created `FileLogger` — file-based logging

**HeroRace Feature**
- `CharacterSpawnerService` — handles character spawning with correct race
- `CharacterTableauService` — handles character portrait rendering with race
- `RacePositionConfigurationService` — manages per-race eye height and position config
- `EyeHeightAdjustmentHook` — adjusts eye height based on race
- `RacePersistenceService` / `RacePersistenceBehavior` — saves/loads race data with campaigns
- `HeroRosterAdapter` — adapter for hero roster access

**Harmony Patches**
- `CharacterSpawner_InitWithCharacter_Patch` — prefix patch for character spawning
- `CharacterTableau_RefreshCharacterTableau_Patch` — patch for portrait rendering
- `CharacterTableau_SetRace_Patch` — patch for race assignment
- `FaceGen_GetBaseMonsterFromRace_Patch` — patch for monster/race resolution
- `ActionSetCode_GenerateActionSetNameWithSuffix_Patch` — action set generation patch

**Tests**
- Added unit tests for `RaceManager`, `ReflectionService`, `FileLogger`
- Added tests for `RacePersistenceBehavior`, `RacePersistenceService`

**Race Data**
- Created `Races/action_sets.xml` — custom action sets for non-human races
- Created `Races/monsters.xml` — monster definitions for custom races
- Created `Races/skins.xml` — skin definitions for race visual data
- Created `TAOM_bodyproperties.xml` — body property templates for all kingdoms

**Voice System**
- Added voice definitions for Dwarf, Uruk-hai, and Uruk races
- Added ~430+ sound files (WAV/MP3) for battle cries, pain, death, commands
- Created `module_sounds.xml` — sound module registration

**Troop Race Attributes**
- Added `race="dwarf"` to Erebor/Iron Hills troops
- Added `race="orc"`, `race="uruk_hai"` to Mordor, Gundabad, Isengard, Dol Guldur troops

---

## 2026-01-28

### Lords, Clans & Heroes

- Added clans, heroes, and lords for Gondor, Rohan, Rhun, and other kingdoms (`characters/clans.xml`, `characters/heroes.xml`, `characters/lords.xml`)
- Added female Isengard and Umbar lords for child generation
- Added spouses for existing lords in Empire and Vlandia factions
- Fixed faction names in `spclans.xslt` to include diacritics (e.g., Rhûn)
- Fixed clan cultures from Gondor/Mordor to Empire where needed
- Updated banner keys and kingdom color attributes
- Updated starting positions for cultures and fixed Dol Guldur owner
- Created `scripts/replace_equipment_templates.py` — replaces custom LOTRAOM equipment templates with vanilla equivalents

### Troop Trees

- Added initial troop XML files for all 14 kingdoms
- Refactored troop files: removed redundant race attributes, fixed encoding issues
- Moved troop files from root `ModuleData/` to `ModuleData/troops/` subdirectory
- Fixed invisible characters in XML declarations
- Registered all troop XML nodes in `SubModule.xml`

### Race Infrastructure

- Created `Races/action_sets.xml`, `Races/monsters.xml`, `Races/skins.xml`
- Created `tools/Generate-ActionSets.ps1` — action set generation script
- Created `project.mbproj` — module project file

---

## 2026-01-27

### Kingdoms & Cultures

- Created `taom_spcultures.xml` — custom culture definitions for 10 new kingdoms (Gondor, Mordor, Gundabad, Isengard, Erebor, Rivendell, Mirkwood, Dol Guldur, Lothlorien, Umbar)
- Created `taom_spkingdoms.xml` — custom kingdom definitions
- Added initial clan and hero data
- Created `scripts/lowercase-pngs.ps1` — utility to rename PNG files to lowercase

---

## 2026-01-25

### Lords Migration

- Enhanced lords data with skill templates and face tags
- Consolidated lords XSLT (`lords.xslt` replacing `splords.xslt`)
- Created `scripts/add-face-tags.ps1` and `scripts/add-skill-templates.ps1`

---

## 2026-01-24

### Project Foundation

- Initial commit: minimal Bannerlord 1.3 mod skeleton
- Set up project structure: `Main/`, `TAOM.Tests/`, `docs/`, `scripts/`
- Created `CLAUDE.md` — project rules and AI instructions
- Created `README.md`
- Created `build.ps1` — build script
- Set up MSTest + NSubstitute test project

### XSLT Transformations

- Created `spkingdoms.xslt` — renames 8 vanilla kingdoms to LOTR equivalents
- Created `spcultures.xslt` — renames 6 vanilla cultures to LOTR equivalents with custom name lists
- Created `spclans.xslt` — renames 73 vanilla clans to LOTR equivalents
- Created `lords.xslt` — transforms 380 lords (names, skills, traits, BodyProperties)
- Created `heroes.xslt` — transforms 415 hero biographies

### Characters

- Created `characters/lords.xml` — 504 new LOTR lords not in vanilla
- Created `characters/heroes.xml` — new LOTR heroes not in vanilla
- Created `characters/clans.xml` — ~101 new LOTR clans not in vanilla
- Created lord extraction and XSLT generation scripts

### Documentation

- Created Architecture Decision Records (ADRs 001-009)
- Created AI include docs: architecture, patterns, TDD, research workflow, code quality, security
- Created migration documentation: tracking, XML schema changes, v1.3 API changes, ROT-Core analysis
- Created testing guide
