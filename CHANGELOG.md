# CHANGELOG — TAOM (Tales From the Age of Men)

## 2026-03-12

### Feature — Character Creation Narrative System (Phases 1-3)

Ported LOTRAOM character creation system to TAOM's Bannerlord 1.3.x handler-based API (`ICharacterCreationContentHandler`). Replaces vanilla Calradia narrative text with LOTR-themed lore for all 16 cultures.

**Phase 1 — Feature Scaffold + Culture Registration (8 new C# files):**
- `CharacterCreationIoC.cs` — DI registrations for feature services
- `CharacterCreationRegistrationBehavior.cs` — CampaignBehavior listening for `OnCharacterCreationInitializedEvent`
- `TaomCharacterCreationContentHandler.cs` — `ICharacterCreationContentHandler` at priority 1050 (after SandBox 800)
- `ICharacterCreationContentService.cs` / `CharacterCreationContentService.cs` — Core logic: culture registration, menu management, finalization
- `ICultureCreationDataProvider.cs` / `CultureCreationDataProvider.cs` — Loads `cultures.json` with caching
- `Models/CultureCreationData.cs` — POCO for per-culture race, settlement, body property data
- Registers 10 custom cultures via `AddCharacterCreationCulture()` (6 vanilla already registered by SandBox)
- Integration: `IoC.cs` + `SubModule.cs` updated

**Phase 2 — Parents Stage (4 new files):**
- `INarrativeDataProvider.cs` / `NarrativeDataProvider.cs` — Generic JSON loader with `ConcurrentDictionary` cache
- `NarrativeMenuBuilder.cs` — Maps JSON definitions to v1.3 `NarrativeMenuOption` objects with skill/attribute resolution
- `Models/NarrativeOptionDefinition.cs` — POCO for narrative option data
- `parents_menu.json` — 96 options (6 per culture x 16 cultures) with LOTR lore text
- Removes vanilla parent options, adds TAOM options with culture-filtered `OnCondition` delegates

**Phase 3 — Childhood + Youth Stages (2 new data files):**
- `childhood_menu.json` — 6 universal LOTR-themed options (no culture filter)
- `youth_menu.json` — 91 culture-specific options (5-6 per culture x 16 cultures)
- Refactored `NarrativeDataProvider` to support generic `LoadMenuOptions(menuName)` pattern
- `NarrativeMenuBuilder` handles universal options (empty `culture_id` = null condition = always visible)
- Education, Adulthood, Age stages keep vanilla SandBox content (non-culture-specific)

**Data files (4 JSON):**
- `ModuleData/charactercreation/cultures.json` — 10 custom culture definitions
- `ModuleData/charactercreation/parents_menu.json` — 96 parent narrative options
- `ModuleData/charactercreation/childhood_menu.json` — 6 childhood narrative options
- `ModuleData/charactercreation/youth_menu.json` — 91 youth narrative options

**Phase 4 — Finalization: Player Race Setting (1 new test file):**
- Added `IRaceManager` + `IHeroRosterAdapter` dependencies to `CharacterCreationContentService`
- `SetPlayerRace()` uses first race from `CultureCreationData.Races[]` (defaults to "human" if empty/null)
- Called from `OnCharacterCreationFinalize()` after teleport to starting settlement
- `CharacterCreationContentServiceTests.cs` — 5 tests (first race, single race, empty/null races, logging)

**Tests (25 new):**
- `CultureCreationDataProviderTests.cs` — 9 tests (JSON parsing, caching, lookup)
- `NarrativeDataProviderTests.cs` — 11 tests (multi-menu loading, caching, culture filtering)
- `CharacterCreationContentServiceTests.cs` — 5 tests (race setting logic)

**Total:** 193 narrative options across 3 stages, 213 tests passing

### Lords Skill Rebalancing (Phase 2)

- Created `tools/rebalance_lords.py` — baseline + cultural modifier balancing for all 914 lords
- Processes both `lords.xslt` (389 vanilla-transform lords) and `characters/lords.xml` (525 custom lords)
- 12 archetypes derived from vanilla `sandbox_skill_sets.xml`: ruler, warrior_knight, warrior_infantry, warrior_ranged, tactician, siege_engineer, politician, manager, spymaster, scholar, trader, dandy
- Cultural modifiers for 13 cultures: 6 vanilla (dunland, dale, harad, rohan, mirkwood, rhun) + 7 custom (dolguldur, erebor, gundabad, isengard, lothlorien, rivendell, umbar)
- Age scaling: peak at 25-50, gentle decline after 55
- Junior lords (rookie skill_template) at 60% of senior baselines
- 10 legendary lords (Nazgul/Sauron/Witch-King) at 2.5x ruler baseline
- Non-combat archetypes (politician, manager, scholar) now correctly have LOW combat / HIGH non-combat skills
- Combat archetypes (warrior_knight, warrior_infantry, warrior_ranged) have HIGH combat / LOW non-combat
- CLI: `--dry-run`, `--apply`, `--export-csv`

### Lords XSLT Completion (Phase 1)

- Completed `lords.xslt` with all vanilla attributes explicit (was 2-3, now 9-11 per template)
- Added 16 missing lords: 7 dead lords, 9 new Vlandia/Rohan lords (skipped main_hero)
- Total templates: 396 (up from 380)
- Created `tools/complete_lords_xslt.py` for regeneration with `--dry-run`, `--apply`, `--export-csv`
- Exported lord attribute inventory to `tools/lords_inventory.csv`
- No passthrough attributes remain — every attribute is now visible and editable in the XSLT

### Tooling — Claude Code Capabilities Overhaul

**Custom Skills (4 new slash commands):**
- `/research [Class]` — Decompile and analyze TaleWorlds classes via ilspycmd
- `/new-feature [Name]` — Scaffold feature modules with IoC, services, adapters, tests
- `/xslt-check [file]` — Validate XSLT against SandBoxCore vanilla XML
- `/migration-status` — Summarize v1.2 -> v1.3 migration progress

**Path-Scoped Rules (5 new rules):**
- `.claude/rules/xslt.md` — XSLT passthrough, SandBoxCore reference (scoped to `**/*.xslt`)
- `.claude/rules/adapters.md` — Adapter pattern enforcement (scoped to `Main/Adapters/**`)
- `.claude/rules/tests.md` — TDD naming, AAA pattern, coverage (scoped to `TAOM.Tests/**`)
- `.claude/rules/xml-data.md` — NPC naming, region codes (scoped to `ModuleData/**/*.xml`)
- `.claude/rules/harmony-patches.md` — Patch rules, thin entry points (scoped to `Main/**/Hooks/**`)

**Custom Agents (2 new agents):**
- `.claude/agents/taleworlds-researcher.md` — Specialized decompilation and analysis agent
- `.claude/agents/feature-builder.md` — Feature scaffolding following TAOM architecture

**Hook Enhancements:**
- Added `check-changelog-updated.sh` Stop hook — reminds to update CHANGELOG.md at session end
- Enabled agent teams via `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS` env var

**Permission & Settings Improvements:**
- Expanded permission allowlist with `dotnet test`, `dotnet build`, `git log/diff/status/branch`
- Added VS Code extensions: `vscode-dotnet-runtime`, `redhat.vscode-xml`, `github.vscode-pull-request-github`
- Enhanced VS Code settings: bracket pair colorization, test peek view, XML validation

**Build Configuration:**
- Added `Directory.Build.props` — centralizes shared MSBuild properties (TargetFramework, LangVersion, Nullable, GameFolder)
- Removed duplicated properties from `TAOM.csproj` and `TAOM.Tests.csproj`

**GitHub CI/CD:**
- Added `.github/workflows/build.yml` — validates XML, XSLT, and JSON well-formedness on every push/PR
- Build & Test job conditional on `BANNERLORD_GAME_DIR` repo variable (requires game DLLs)

**GitHub MCP Server:**
- Added GitHub MCP to `.mcp.json` — enables PR, issue, actions, and code search from Claude

**CLAUDE.md Optimization:**
- Slimmed from 198 to 136 lines — moved detailed XSLT rules, TaleWorlds Research Protocol, and verbose sections to scoped rules and skills
- Added Skills, Scoped Rules, and Custom Agents sections
- Saves ~30% context window on every conversation start

### Tooling — Claude Code Hooks

- Added pre-commit build check hook (`.claude/hooks/check-build-before-commit.sh`) — blocks `git commit` if `dotnet build` fails
- Added C# edit notification hook (`.claude/hooks/notify-csharp-edit.sh`) — logs modified C# file paths to session
- Created `.claude/settings.json` with hook configuration
- Enabled hooks globally (removed `disableAllHooks: true` from global settings)

## 2026-03-11

### Tooling — Developer Environment & AI Workflow Improvements

**VS Code project config (3 new files):**
- `.vscode/tasks.json` — Build (Ctrl+Shift+B), Build+Test, Run Tests tasks with `$msCompile` problem matcher
- `.vscode/extensions.json` — Recommends Claude Code, C# DevKit, XML, PowerShell extensions
- `.vscode/settings.json` — Hides bin/obj/.vs from explorer, enables format-on-save

**Editor formatting (1 new file):**
- `.editorconfig` — Enforces 4-space C# indent, 2-space XML/JSON indent, CRLF line endings, trim trailing whitespace

**Serena MCP per-project configuration:**
- Created `.mcp.json` for TAOM — Serena symbolic code navigation now targets TAOM's C# codebase
- Created `.mcp.json` for Achaea — Serena continues targeting LEVI-Achaea
- Removed Serena from global MCP config (was always pointing at Achaea regardless of project)

**Claude Code configuration cleanup:**
- Removed 5 stale one-off permission entries from global `settings.json`
- Removed 3 stale permission entries from project `.claude/settings.local.json`
- Added 4 new memory files: user profile, feedback (SandBoxCore reference, XSLT passthrough), external references
- Updated MEMORY.md index with new memory file links

**CLAUDE.md updates:**
- Added VS Code config, .editorconfig, and .mcp.json to Key Paths table
- Added MCP Servers section documenting Serena, sequential-thinking, and context7

### Feature — Interactive Faction Selection Map

Ported external LOTRAOM_FactionMap feature into TAOM as `Main/Features/FactionMap/`. Replaces vanilla character creation culture selection with a clickable Middle-earth map (36 regions, 18 factions, 6-pass rendering with animations).

**Architecture (46 new C# files):**
- Models: FactionData, RegionData, LandmarkDef, FactionSelectionResult, HoverStateChange (5 POCOs/DTOs)
- Services: FactionConfigProvider, FactionRegistryService, LandmarkService, CultureResolverService, FactionSelectionService, FactionHoverService (6 TDD services + interfaces)
- Adapter: ICultureObjectAdapter/CultureObjectAdapter wrapping MBObjectManager
- ViewModels: FactionSelectionVM (thin, <200 lines) + 4 sub-VMs (TraitItem, BonusItem, PerkItem, LandmarkItem)
- Widgets: PolygonWidget (6-pass renderer), BannerWidget, FactionImageWidget, MapContainerWidget, RuntimeSprite
- Hooks: 3 Harmony patch pairs (Constructor/Tick/Finalize) on CharacterCreationCultureStageView using hook interface pattern
- Infrastructure: FactionMapIoC, FactionMapPaths, FactionMapStaticBridge

**Data & Assets:**
- `factions.json` — 29 factions with culture IDs mapped to TAOM's 16 cultures (10 custom + 6 remapped vanilla)
- `regions.json` — 36 clickable map regions with bounding boxes and polygon vertices
- 111 PNG sprite assets (banners, faction images, highlights)
- FactionMap.xml brushes, CharacterCreationCultureStage.xml prefab, sprite registration XML

**Tests (45 new tests):**
- FactionConfigProviderTests (6), FactionRegistryServiceTests (9), FactionSelectionServiceTests (12), FactionHoverServiceTests (7), CultureResolverServiceTests (6), LandmarkServiceTests (5)

**Review fixes (9 issues resolved):**
- Added explicit `[HarmonyPostfix]`/`[HarmonyPrefix]` attributes to all 3 Harmony patches (were relying on method name convention only)
- Added comments explaining dynamic `TargetMethod()` pattern for View assembly types
- Extracted FactionDisplayHelper from FactionSelectionVM (263→150 lines)
- Extracted ICultureSettingService/CultureSettingService from CultureStageViewCreatedHook (205→146 lines)
- Extracted FactionDataParser from FactionConfigProvider (161→119 lines)
- Fixed LandmarkService thread safety (lazy init → constructor initialization)
- Added IModLogger to CultureObjectAdapter for exception logging
- Converted PolygonWidget to file-scoped namespace
- Updated all `game_faction` values in factions.json to TAOM culture IDs (gondor, erebor, mordor, rivendell, etc.)
- Added 7 edge-case tests (malformed JSON, color fallbacks, difficulty bounds, logging verification)

**Modified existing files:**
- IoC.cs — Added FactionMapIoC registration
- SubModule.cs — Added FactionMapPaths initialization + Patch7_FactionMap category
- TAOM.csproj — Added AllowUnsafeBlocks, System.Numerics.Vectors package

### Website — Weapon Balance Data Corrections

- Fixed Rhun avgMelee from 66 to 69 (was using simple average instead of weighted average across rhun+khuzait cultures)
- Fixed Rhun meleePercent from 97% to 101% to match corrected average
- Demoted Dol Guldur from A-tier to B-tier for Shock Troops (no longer justified with -3 pts weapons)
- Demoted Dol Guldur from A-tier to B-tier for Line Breakers (same reason)
- Removed 22 stale percentage-based weapon references from balance-overview.astro (140%, 120%, 118%, etc.)
- Updated Overview section in weapon-balancing.astro from old percentage system to points-based narrative

### Website — Balance Overview Page

- Added `/mod-info/balance-overview` page with faction power rankings across all three balance axes (troop skills, armor, weapons)
- Added Balance Overview card to mod-info index page
- Faction Power Comparison table with S-D grading for 12 non-elven cultures + 3 elven cultures (separate section)
- Iron Hills and Erebor graded individually (not combined)
- Balance Triangle visual explaining the three-axis system

### Website — Infantry Subcategories & Tier Lists

- Added 7 tier lists: Overall Infantry, Front Line, Shock Troops, Line Breakers, Skirmishers, Cavalry, Ranged
- Gaming-style S-D tier format with per-culture reasons
- Updated all tier list descriptions to reference actual troop equipment loadouts (Item0-Item4 from NPC XML)
- Troop role classification based on actual equipment: sword+shield = frontline, 2H weapon = shock/linebreaker, throwing weapon = skirmisher, bow/crossbow = ranged
- Key findings from equipment analysis:
  - Dunland: 28 of 30 infantry carry throwing weapons (S-tier skirmisher, D-tier frontline)
  - Dol Guldur: 17 ranged troops (S-tier ranged), 22 shield troops, 5 linebreakers
  - Erebor/Iron Hills: zero throwing troops, zero cavalry — pure heavy infantry
  - Rohan: 18 infantry shield troops (Westfold, Westmarches, Edoras) — B-tier frontline, not D

### Weapon Rebalancing — Points-Based System

- Replaced percentage-based weapon modifiers with points-based craftsmanship system
- Each culture gets points above/below global average melee damage (68):
  - Noldor (Rivendell): +10, Sindar (Lothlorien): +9, Erebor/Iron Hills: +5
  - Mirkwood: +4, Gondor: +3, Rhun: +2, Arnor: +2
  - Isengard: 0 (baseline), Rohan: 0 (polearms +3), Harad: 0
  - Gundabad: -1, Mordor: -2, Dunland: -2, Dol Guldur: -3
- Applied 217 blade piece modifications via `rebalance_weapons.py --apply`
- Rohan polearms get separate +3 point bonus for cavalry lance superiority
- Hero/legendary weapons exempt from modifiers (18 pieces)
- Bows excluded — to be handled separately later
- Updated `weapon-balancing.astro` with new per-culture data and craftsmanship narrative
- Updated `balance-overview.astro` weapon grades to reflect new system
- New philosophy: weapon quality reflects craftsmanship (elves = best, dwarves = great, evil = crude)

### Website — Rename Goblins to Dol Guldur Orcs

- Renamed 'Goblins' to 'Dol Guldur Orcs' across weapon-balancing, troop-balancing, armour-balancing, and balance-overview pages
- Preserved 'Goblin' in troop names (Goblin Hunter, Goblin Slave) and race descriptions

### Armor Modifier Revisions

- Gundabad protection: -2 → 0 (holds dwarven cities, access to dwarven forges)
- Dol Guldur protection: -1 → 0 (fortress-forged plate from Sauron's armories)
- Rivendell protection: +6 → +5 (on par with dwarves, not above)
- Gondor protection: 0 → +1 (Numenorean smithing tradition)
- Re-ran `rebalance_armor.py --apply` on 83 armor files (2,368 items)
- Updated `balance-overview.astro` armor grades: Gundabad D→B, Dol Guldur C→B
- Updated `armour-balancing.astro` culture detail cards with new values and lore

---

## 2026-03-10

### Website — Database Landing Page & Lord Database Fixes

- Added `/database` landing page with overview cards matching mod-info style (Troops, Lords, Armoury, Weaponry)
- Added "Overview" link to Database dropdown nav
- Fixed lord database: culture group headers now start collapsed by default
- Fixed bug where collapsed culture headers disappeared — `filterRows()` was checking display state instead of filter match
- Removed 48 generic militia troops (militia archer/spearman/veteran variants) from website troop data across 12 cultures; keeps named militia troops (gondor_militiaman, rohan_westfold_militiaman, harad_militia, easterling_militia)

### Armor Rebalancing — 2,368 Items Across 17 Cultures

Comprehensive armor stat rebalancing using a uniform baseline + cultural modifier formula, mirroring the troop skill rebalancing system.

**Approach:**
- Created `tools/rebalance_armor.py` — Python script with baseline armor values per tier (civilian/light/medium/heavy/elite/lord) and per-slot (head/body/arm/leg/shoulder), plus cultural modifiers
- Tier detection via keyword matching on item names/IDs with value-based fallback
- Numbered variants (I, II, III...) get +1 armor progression within each tier
- Material type corrected: light=Leather, medium=Chainmail, heavy+=Plate

**Baseline body armor values:** civilian=5, light=20, medium=32, heavy=42, elite=50, lord=60

**Cultural Identities:**

| Culture | Protection Mod | Weight Mult | Identity |
|---------|---------------|-------------|----------|
| Erebor | +4 | 1.05x | Master dwarven smiths |
| Iron Hills | +5 | 1.10x | Heaviest dwarven armor |
| Rivendell | +6 | 0.70x | Finest elven masterwork |
| Mirkwood | +5 | 0.65x | Lightest elven craft |
| Lothlorien | +5 | 0.70x | Golden wood craft |
| Gondor | +0 | 1.00x | Reference culture |
| Rohan | -2 | 0.90x | Lighter for mounted |
| Isengard | +2 | 1.15x | Industrial heavy |
| Mordor | -1 | 1.10x | Crude mass-produced |
| Gundabad | -2 | 1.15x | Crude but heavy |
| Harad | -3 | 0.85x | Desert light armor |
| Dunland | -2 | 0.95x | Hill-folk |

**Files modified:** 83 armor XMLs in `taommod/src/data/armory/` + `tools/rebalance_armor.py`
**Item count:** 2,368 armor items across 17 cultures, 5 armor slots

---

### Troop Progression — Level 51 Support (TroopProgression Feature)

Ported LOTRAOM's extended troop tier system to TAOM for Bannerlord 1.3. Raises the troop tier cap from vanilla's 6 (level 31+) to 10 (level 51+), enabling meaningful differentiation across all troop levels produced by the rebalance script.

**C# Implementation (10 files):**
- `TaomCharacterStatsModel` — GameModel override: `MaxCharacterTier => 10` (vanilla 6). Vanilla `GetTier()` formula `Ceiling((level-5)/5)` clamped to `[0, MaxCharacterTier]` naturally produces tiers 7-10 for levels 36-55
- `TaomPartyWageModel` — GameModel override: extended tier-based wages (T0=1 through T10=30) and level-bracket recruitment costs (L1=10 through L51=3600, L52+=4000). `MaxWagePaymentLimit` raised to 20,000 (vanilla 10,000). Includes mounted surcharge (1.3x) and mercenary/gangster/caravan guard multipliers
- `TaomVolunteerModel` — GameModel override: `MaxVolunteerTier => 6` (vanilla 4), allowing higher-tier volunteers
- `TroopCostService` / `ITroopCostService` — wage and recruitment cost calculations using primitives only (no sealed types)
- `VolunteerTierService` / `IVolunteerTierService` — volunteer tier configuration
- `TroopProgressionIoC` — DryIoc feature registration
- 37 `TroopCostServiceTests` + 2 `VolunteerTierServiceTests` = 39 new tests

**Tier-to-level mapping (with MaxCharacterTier=10):**

| Tier | Levels | Wage | Recruitment Cost |
|------|--------|------|-----------------|
| 0 | 1-5 | 1 | 10-20 |
| 1 | 6-10 | 2 | 20-50 |
| 2 | 11-15 | 3 | 50-200 |
| 3 | 16-20 | 5 | 200-400 |
| 4 | 21-25 | 8 | 400-600 |
| 5 | 26-30 | 12 | 600-1000 |
| 6 | 31-35 | 15 | 1000-1500 |
| 7 | 36-40 | 18 | 1500-2100 |
| 8 | 41-45 | 20 | 2100-2800 |
| 9 | 46-50 | 25 | 2800-3600 |
| 10 | 51-55 | 30 | 3600-4000 |

**Integration:** GameModels registered via `CampaignGameStarter.AddModel()` in `SubModule.OnGameStart` — "last model wins" semantics ensure TAOM overrides vanilla defaults.

**Not yet ported from LOTRAOM (future work):** culture feat wage modifiers (6 factions), `GetTotalWage` faction modifiers, race bonus wage hooks, settlement-specific volunteer pools.

---

### Troop Skill Rebalancing — All 13 Culture Files (545 troops)

Comprehensive skill rebalancing across all troop trees using a uniform baseline + cultural modifier formula. Previously, skills were wildly inconsistent: Rhun had placeholder 150 values, Rivendell had 300+ at level 21 (3x peers), Umbar/Dunland cavalry were 0.5x average, and 40 militia entries had zero skills.

**Approach:**
- Created `tools/rebalance_troops.py` — Python script with baseline skill tables per level/group (Infantry, Ranged, Cavalry, HorseArcher) and per-culture modifiers
- Baseline tables define center values for 11 level tiers (1-51) across 8 combat skills
- Cultural modifiers (±5-10 for standard factions, +25-50 for elven factions) give each culture distinct identity
- Weapon specialization detection swaps primary/secondary weapon skills based on troop names (crossbow, pike, sword, axe)
- Militia entries now use level 21 baselines of their culture instead of all-zero skills
- Regex-based XML replacement preserves all formatting, comments, and non-skill attributes

**Cultural Identities:**

| Culture | Strengths | Weaknesses |
|---------|-----------|------------|
| Erebor | TwoHanded +20, Athletics +10, OneHanded +10, Polearm +10, Throwing +10 | Riding -20 |
| Iron Hills | TwoHanded +20, Polearm +20, OneHanded +15, Athletics +10, Throwing +10 | Riding -5 |
| Gondor | OneHanded +10, Athletics +5, Riding +5, TwoHanded +5, Polearm +5 | Throwing -10 |
| Rohan | Riding +20, Polearm +10, Throwing +2 | Crossbow -10, Athletics -5, Bow -5 |
| Isengard | TwoHanded +15, Polearm +15, Athletics +10, OneHanded +10, Crossbow +10, Throwing +10 | Riding +5 |
| Mordor | TwoHanded +5, Throwing +5 | Athletics -5, Riding -5, Polearm -5, Bow -5, Crossbow -5 |
| Harad | Riding +15, Bow +10, OneHanded +5 | TwoHanded -10, Polearm -5 |
| Rhun | Riding +18, Polearm +15, Athletics +5 | Bow -10, Crossbow -10, Throwing -5 |
| Dunland | Athletics +20, Throwing +15, OneHanded +5, TwoHanded +5 | Riding -5 |
| Dol Guldur | OneHanded +5, TwoHanded +5 | Riding -10, Bow -5, Crossbow -5 |
| Gundabad | TwoHanded +10, Athletics +5, Polearm +5, Throwing +5 | Bow -10, Crossbow -10, Riding -5 |
| Rivendell | All combat +30-40 (elite High Elves) | — |
| Mirkwood | Bow/Crossbow/Throwing +50, Athletics +45, OneHanded +40 (elite) | — |
| Lothlorien | Bow/Crossbow/Throwing +35, Athletics +35, Polearm +30, OneHanded +30 (elite) | — |
| Umbar | Athletics +10, OneHanded +10, TwoHanded +5 | Riding -15 |

**Files modified:** 13 troop XMLs + `tools/rebalance_troops.py`
**Troop count:** 545 troops across Dol Guldur (50), Dunland (45), Erebor (47), Gondor (71), Gundabad (30), Harad (29), Isengard (38), Mirkwood (17), Mordor (28), Rhun (91), Rivendell (28), Rohan (57), Umbar (14)

---

### Website — Culture Theming & Troop Balancing Page

Updated the taommod website with culture-specific color theming across all data tables and the troop balancing page.

**Troop Balancing Page (`troop-balancing.astro`):**
- Renamed all 15 cultures to lore-accurate names (Gondorians, Rohirrim, Longbeards, Ironfists, Noldorin, Silvan, Sindar, Uruk-Hai, Mordor Orcs, Gundabad Orcs, Goblins, Haruze, Easterlings, Dunlending, Umbarean)
- Added culture-colored backgrounds to comparison table cells and culture detail cards
- Updated identity descriptions with lore text (Gondor regional specializations, Erebor/Iron Hills weapon preferences, Rohan cavalry focus, evil faction creature notes)
- Culture badges styled with per-culture colors

**Culture Color Scheme (across all pages):**
- Erebor: blue-gold `#6a9fd4` / `rgba(106, 159, 212)`
- Iron Hills: dark red/clay `#a04030` / `rgba(160, 64, 48)`
- Gundabad: cool gray `#7a8a9a` / `rgba(122, 138, 154)`
- Harad: red `#c43c3c` / `rgba(220, 20, 60)`
- Easterlings/Rhun: golden `#d4a24c` / `rgba(212, 162, 76)`
- Other cultures retain established colors

**Files modified:** `src/styles/global.css` (data-table culture row colors), `src/pages/mod-info/troop-balancing.astro` (full page overhaul)

---

## 2026-03-06

### Banner Injection Feature

Ported LOTRAOM's Banner Injection system to TAOM for Bannerlord 1.3. Re-applies custom `banner_key` values to Kingdom and Clan objects on every session launch, preventing banner reversion on save/load cycles. Leverages 1.3 public setters (no reflection needed).

**C# Implementation (18 files):**
- `BannerInjectionService` — core injection logic: loads config, compares runtime banners to XML, sets + invalidates visuals for mismatches
- `BannerExclusionService` — tracks player-modified banners via `IDataStore` persistence to avoid overwriting player edits
- `BannerConfigProvider` — parses `banner_key` from 4 sources: `taom_spkingdoms.xml`, `spkingdoms.xslt`, `characters/clans.xml`, `spclans.xslt`. Handles both inline XML attributes and `xsl:attribute` XSLT patterns
- `BannerInjectionBehavior` — thin `CampaignBehaviorBase`, fires injection on `OnSessionLaunchedEvent`
- `IKingdomBannerAdapter` / `KingdomBannerAdapter` — wraps `Kingdom.All`, `Kingdom.Banner` setter, visual invalidation
- `IClanBannerAdapter` / `ClanBannerAdapter` — wraps `Clan.All`, `Clan.Banner` setter, ruling clan detection
- `GauntletBannerEditorScreen_OnDone_Patch` — Harmony postfix detects player banner edits, marks clan as player-modified
- `BannerInjectionIoC` — DryIoc registration for all banner services
- 8 `BannerConfigProviderTests` + 5 `BannerExclusionServiceTests` + 13 `BannerInjectionServiceTests` = 26 new tests

**XSLT Changes:**
- Added vanilla `banner_key` attributes to all 73 clan templates in `spclans.xslt` (across 8 culture groups) in anticipation of future clan rework
- Each template excludes `banner_key` from pass-through to prevent duplication

### Notable NPCs — Culture-Specific Notables

Replaced vanilla Empire notable NPCs with culture-specific notables for all 10 custom cultures. Previously all settlements (including orc/elf/dwarf) spawned human Empire notables as merchants, artisans, preachers, etc.

- Created 26 notary NPCs per culture matching vanilla occupation distribution: 10 Merchant, 3 Preacher, 2 Artisan, 6 GangLeader, 2 RuralNotable, 3 Headman
- Each NPC has correct race, `is_template="true"`, varied voices, traits, and culture-appropriate equipment
- Updated `taom_spcultures.xml` — replaced `spc_notable_empire_*` references with culture-specific `spc_notable_{culture}_*` in all 10 `notable_templates` blocks + culture-level `merchant_notary`/`artisan_notary`/`preacher_notary`/`rural_notable_notary` attributes
- Created `characters/npcs_lothlorien.xml` and `characters/npcs_umbar.xml` (new files — these cultures had no NPC file)
- Registered new files in `SubModule.xml`

### XSLT Fixes

- Fixed XSLT attribute filters for aserai→Harad, vlandia→Rohan, khuzait→Rhun — replaced 60+ attribute exclusion filters with `<xsl:apply-templates select="@*"/>` passthrough pattern
- Fixed child element duplication across all 4 XSLT cultures — `vassal_reward_items`, `banner_bearer_replacement_weapons`, `default_policies`, `male_names`, `female_names`, `clan_names` now excluded from passthrough
- Fixed 23 corrupted accent characters in `taom_wanderers.xml` (double-encoded UTF-8: `Ã»`→`û`, `Ãª`→`ê`, `Ã³`→`ó`, `Ã¡`→`á`, `Ã­`→`í`)

### Faction & Culture Strings

Added comprehensive faction/culture strings for all 16 cultures, fixing "ERROR: Text with id str_faction_ruler doesn't exist!" and replacing vanilla culture names/descriptions with LOTR-themed content.

- Created `taom_module_strings.xml` — 272 strings across 17 types for 16 cultures:
  - Faction strings (12 types): ruler titles, noble titles, faction adjectives, formal/informal names
  - Culture descriptions (16): LOTR lore text for character creation
  - Culture rich names (16): e.g. "Rohirrim", "Dwarves", "Galadhrim"
  - Culture adjectives (16): e.g. "Dunlending", "Rohirric", "Dwarven"
  - Player parent names (32): LOTR-themed father/mother names for character creation
- Created `module_strings.xslt` — removes vanilla strings for 6 remapped cultures (empire→Dunland, vlandia→Rohan, battania→Khand, khuzait→Rhûn, aserai→Harad, sturgia→Dale)
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
