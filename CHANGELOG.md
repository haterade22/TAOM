# CHANGELOG — TAOM (Tales From the Age of Men)

## 2026-04-02

### Fix: ShaderPrecompilation — Stuck-Shader Auto-Abort + Countdown UI (#57)

- A shader stuck at "1 remaining" could block indefinitely with no way to exit
- After 30s stuck at the same count: shows "stuck Xs (aborting in Ys)" countdown in the loading screen text
- After 120s stuck: calls `MBGameManager.EndGame()` to abort and return to the main menu automatically
- `TaomShaderGameManager.IsShaderBattleActive` flag scopes the timeout to TAOM shader battles only
- Note: TaleWorlds exposes no API for which shader is stuck — only the count is available

### Feat: Named Hero Civilian Equipment — Sauron, Witch-King, Nazgul, Khamul, Nazgul V1, Glorfindel (#61)

- Added dedicated `*_civ_equipment` roster entries for all named Mordor and Rivendell heroes so they appear in their unique armor in civilian/settlement scenes
- `sauron_civ_equipment`, `witchking_civ_equipment`, `nazgul_civ_equipment`, `khamul_civ_equipment`, `nazgul_v1_civ_equipment` added to `taom_equipment_sets_mordor.xml`
- `glorfindel_civ_equipment` added to `taom_equipment_sets_rivendell.xml`
- Updated `lords.xslt` (10 entries) and `lords.xml` (Glorfindel) to reference the new civ roster IDs instead of generic `mordor_civ_template_default_*`/`rivendell_civ_template_default_*`

### Feat: All-Culture Lords Civilian Equipment Pass — Lords Always in Battle Gear (#59)

- Systematically replaced all `*_civ_template_*` lord civilian templates across 13 cultures with exact mirrors of their `*_bat_template_medium_*` battle loadouts
- Cultures updated: Umbar, Dunland, Rohan, Lothlorien, Dale, Harad, Isengard, Dol Guldur, Gundabad, Mordor, Rhun, Mirkwood, Rivendell
- Lords now appear in full armor (weapons, helm, body, cape, gloves, greaves, horse/mount) in both battle and town/settlement scenes
- Named hero civilian outfits preserved: Theoden, Thranduil, Legolas
- Erebor and Gondor were completed in prior sessions (#56, #58)

### Fix: BannerInjection — Fire Once Per Game Start/Load Instead of Every Session Launch

- `BannerInjectionBehavior` was subscribed to `OnSessionLaunchedEvent`, which fires on every return from a battle or mission to the campaign map — causing the full kingdom/clan loop to run (and log) after every fight
- Swapped to `OnNewGameCreatedEvent` + `OnGameLoadedEvent` so injection fires exactly once: on new game creation and on save load
- No behavioral change for players — banners are campaign-level data that persist across sessions; re-injection after battles was unnecessary

### Feat: ShaderPrecompilation — Pre-compile Shaders at Main Menu (#57)

- Mid-game stutter when encountering new armor/mesh combinations (first-time shader compilation) eliminated by pre-warming the cache before campaign start
- New **"Pre-compile Shaders"** button on the main menu (order index 100) launches a hidden custom battle containing all TAOM characters from all 13 non-bandit cultures
- Bannerlord's renderer compiles all unique material shaders as it renders each character; loading screen shows "Compiling shaders... N remaining" with live countdown
- Progress text updated only when count changes — avoids per-frame string allocation in `LoadingWindowViewModel.Update()` postfix
- `Patch21_ShaderPrecompilation` / `TaomShaderGameManager` / `ShaderPrecompilationService`; all 14 v1.3.12 APIs verified via decompilation

### Feat: Gondor Equipment Pass — Lords in Battle Gear + Noble Coat/Jerkin Variety (#58)

- Gondor lords now wear full battle armor in civilian scenes — `gondor_civ_template_default_a/b/c/d/e` updated to mirror their `gondor_bat_template_medium_*` counterparts (weapons, helm, chest, cape, gloves, greaves, horse)
- Boromir (`boromir_civ_equipment`) and Faramir (`faramir_civ_equipment`) civilian outfits unchanged (intentional character-specific looks)
- 8 new civilian items added to LOTRLOME_Armory (`gondor_noble_coat_a/b`, `gondor_noble_coat_a/b_slim`, `gondor_noble_jerkin_a/b`, `gondor_noble_jerkin_a/b_slim`) — light cloth stats, `Civilian="true"` flag
- All Gondor civilian NPCs (craftsmen, tavern, services, beggars, dancers, merchants, notables, headmen) switched from `ithilien_jerkin_*` / `boromir_jerkin` to new noble coats/jerkins and `lossarnach_coat`
- Female-coded NPCs (`tavern_wench`, `female_beggar`, `female_dancer`, `townswoman_*`, `village_woman_*`) use slim variants
- Armorer and ransom broker retain chainmail second roster (appropriate for role); gang bodyguard chainmail kept
- All 26 notables spread across the full item range for visual variety

### Feat: Erebor Equipment Pass — Lords in Battle Gear + Full Dress/Tunic Variety (#56)

- Dwarf lords now wear full battle armor in civilian scenes (town/settlement) — `erebor_civ_template_default_a/b/c/d/e` updated to mirror their `erebor_bat_template_medium_*` counterparts (weapons, helm, chest, cape, bracers, greaves)
- Male-coded civilian NPCs (townsman, blacksmith, weaponsmith, barber, beggar and family variants) switched from dresses to `tunic_normal_a/b`
- Female-coded NPCs (townswoman, village_woman, female_beggar, female_dancer, tavern_wench and family variants) spread across dresses `e–i`
- Neutral NPCs (villager, teenagers, musician, tavernkeeper, merchant) given two civilian roster entries each (dress + tunic) for random variety
- Notable preachers (`_5/_6/_7`) and gang leaders (`_12/_13`) updated to dresses `e–i`
- Rural notables (`_21/_22`) and headmen (`_2/_3`) upgraded to `tunic_noble_a/b/c` to reflect their status
- All 9 dresses (a–i) and both tunics (a–b) now in use; noble tunics (a–c) introduced for notable NPCs

### Feat: MainMenuCustomizer — Hide Campaign, Rename Sandbox (#55)

- Bannerlord main screen exposed "Campaign" (vanilla story mode) alongside "Sandbox" — misleading for a total conversion mod
- `OnBeforeInitialModuleScreenSetAsRoot` override calls `Module.CurrentModule.OverrideInitialStateOption` twice: sets `isHidden: () => true` on `campaign_single_player`, renames `sandbox_single_player` to "Enter The Age Of Men"
- Original action, disabled-state delegates, and order index preserved on both overrides
- `IModuleMenuAdapter` / `ModuleMenuAdapter` wraps `Module.CurrentModule` static API; `MainMenuCustomizerService` holds no TaleWorlds references

## 2026-03-31

### Feat: TaomTournamentModel — Increased Tournament Frequency (#52)

- Vanilla bucketed each town into 1 of 3 week-slots per season, suppressing tournaments to ~1 per 1–3 seasons
- `GetTournamentStartChance`: removed week-gate, replaced linear formula with diminishing-returns step curve tuned for LOTR campaigns where lords are rarely at peace (1 lord=45%, 2=75%, 3=90%, 4+=100%)
- `GetTournamentEndChance`: extended grace period from 10 → 20 days, slowed ramp from 0.05 → 0.033/day — tournaments stay active longer
- All tuning values extracted as `internal const` for testability and future MCM exposure

### Feat: TaomTournamentModel — Culture-Specific Tournament Prize Items (#52)

- `DefaultTournamentModel.GetEliteRewardItems` returned a hardcoded list of 31 vanilla items — none exist in TAOM; elite prizes were silently empty
- `GetRegularRewardItems` filtered by gold value range, missing most LOTRLOME_Armory items
- Both methods now dynamically scan `Items.All` filtered by settlement culture + `item.Tierf` threshold (regular: 2–4, elite: 4+)
- Cultures without armory entries (lothlorien, dale, khand) fall back to `base` gracefully
- Called once per tournament win — not a hot path; no performance impact

### Feat: TaomTournamentModel — Per-Participant Culture Armor (#52)

- `DefaultTournamentModel.GetParticipantArmor` used settlement culture for ALL participants (heroes, lords, filler troops) — human lords in Erebor tournaments received dwarf chainmail on human skeletons
- Root cause (confirmed via decompilation): vanilla ignores the `participant` parameter entirely; no race/culture check exists anywhere in the tournament pipeline
- New `TaomTournamentModel : DefaultTournamentModel` overrides `GetParticipantArmor` to try participant's own culture first, then falls back to vanilla (settlement culture → empire)
- Data-driven: each culture's `gear_practice_dummy_*` already has skeleton-appropriate gear; no explicit race mapping needed
- New files: `Main/Features/Arena/Models/TaomTournamentModel.cs`, `TAOM.Tests/Features/Arena/TaomTournamentModelTests.cs`

### Fix: Arena Practice Crash — All 13 TAOM Cultures (#49)

- `ArenaPracticeFightMissionController.AddRandomWeapons` crashed with `ArgumentOutOfRangeException` for all TAOM custom culture arenas
- Root cause: all 39 `weapon_practice_stage_{1-3}_{culture}` EquipmentRosters were tagged `civilian="true"` → `BattleEquipments` returned empty list → `RandomInt(0)` crashed
- Fix: removed `civilian="true"` from all 39 rosters, added tier-appropriate weapons (Stage 1: T2, Stage 2: T3, Stage 3: T4 swords) to `Item0` slot
- Affected files: `npcs_{erebor,gondor,mordor,rivendell,mirkwood,lothlorien,isengard,gundabad,dolguldur,umbar,rohan,harad,rhun}.xml`

### Fix: Dwarf Character Creation — 3 Cascading Crashes (#50)

- **Crash 1 (NRE):** `GetYouthMenuNarrativeMenuCharacterArgs` unconditionally reads `DefaultEquipment[Horse].Item.StringId` — crashed when Erebor CC rosters had no horse
- **Crash 2 (ArgumentNullException):** `SpawnNonHumanNarrativeMenuCharacter` called `MBObjectManager.GetObject<T>(null)` — horse scene character had uninitialized IDs when horse NarrativeMenuCharacterArgs was skipped
- **Lore fix:** Removed `Horse`/`HorseHarness` slots from all 16 `player_char_creation_erebor_*` non-civilian EquipmentSets
- **Patch20_NarrativeHorseGuard:** Two new Harmony patches in `CharacterCreationCampaignBehavior_GetYouthMenuArgs_Patch.cs`
  - Prefix on `GetYouthMenuNarrativeMenuCharacterArgs`: skips horse entry when `DefaultEquipment[Horse].Item == null`
  - Finalizer on `SpawnNonHumanNarrativeMenuCharacter`: suppresses `ArgumentNullException("key")` from null horse item ID
- Pattern is data-driven — any future no-mount culture works automatically by omitting horse slots from CC equipment

### Fix: Arena Practice Clothes Crash + Culture-Specific Clothing (#51)

- `ArenaPracticeFightMissionController.AddRandomClothes` crashed (NRE) for all TAOM custom culture arenas
- Root cause: all 13 `gear_practice_dummy_{culture}` characters had only `civilian="true"` EquipmentRosters → `RandomBattleEquipment` returned null → null dereference
- Fix: removed `civilian="true"` from all 13 characters, updated item IDs to be culture-appropriate (dwarves use tunic not dress, mirkwood/lothlorien use rivendell items, dale uses sturgia, khand uses dunland armory, dunland/rhun updated from vanilla to TAOM armory items)
- Added missing `gear_practice_dummy_lothlorien` entry (was absent — fell back to empire clothes)
- Affected files: `npcs_{erebor,gondor,isengard,mordor,rivendell,dolguldur,mirkwood,gundabad,harad,dunland,rhun,dale,khand,lothlorien}.xml`

### Fix: TaomPartyHealingModel NRE in Arena Practice (#52)

- `GetSurvivalChance` crashed (NRE at line 34) when an agent died during arena practice
- Root cause: `party` parameter is null in arena practice context (no campaign party exists); line `party.Owner?.Culture ?? party.Culture` dereferences null `party`
- Fix: added `if (party == null) return vanillaSurvival;` guard before config/culture access in `TaomPartyHealingModel.cs`
- Vanilla base model handles null `party` gracefully; cultural survival bonuses simply don't apply in arena context

### Fix: Dwarf Character Creation — Remaining Stage NREs (#50 continued)

- **Root cause (full picture):** `CharacterCreationCampaignBehavior` has 6 `Get*NarrativeMenuCharacterArgs` methods; 3 of them unconditionally dereference `DefaultEquipment[Horse].Item.StringId`. Each fires on a separate CC screen click, producing a new NRE each time.
- **Adult stage** (`GetAdultMenuNarrativeMenuCharacterArgs` line 2819): added Prefix returning `"player_adulthood_character"` (age 20)
- **Age selection stage** (`GetAgeSelectionMenuNarrativeMenuCharacterArgs` line 3298): added Prefix returning `"player_age_selection_character"` (age = `StartingAge`)
- `Patch20_NarrativeHorseGuard` now has 4 patches (3 Prefixes + 1 Finalizer) covering all crash sites — decompilation confirmed no further horse-reading methods exist in the class

## 2026-03-28

### awesome-claude-skills Cherry-Pick: ADR Scaffolding & Atomic Commit Workflow

Reviewed 13,152 skills from the awesome-claude-skills marketplace. 45 of 47 filtered candidates were skipped (wrong language, wrong domain, or already covered). Two genuine gaps filled:

- **New skill:** `/new-adr [name]` — auto-numbers from existing `docs/adrs/`, reads `000-template.md` for exact format, pre-fills Context from `git log --oneline -10` + CHANGELOG, writes `docs/adrs/NNN-name.md`, reminds to fill Decision/Consequences/Examples and update README.md
- **New skill:** `/commit-split` — inspects staged + unstaged + untracked files, groups by TAOM-specific heuristics (feat/test/data/docs/chore), confirms grouping with user, then executes each atomic commit with 50/72-rule messages, optional trailers, and staged diff review per commit
- **Updated CLAUDE.md:** Skills table updated with both new skills

### oh-my-claudecode Cherry-Pick: Researcher Safety, Deslop, Deep-Review Adversarial Mode, Commit Trailers

Reviewed the oh-my-claudecode repository (19 agents, 32 skills, MCP bridge). Most components require the OMC MCP bridge and were skipped. Cherry-picked 5 zero-infrastructure patterns adapted for TAOM's C#/.NET stack.

- **Updated agent:** `taleworlds-researcher.md` — added `disallowedTools: [Write, Edit, NotebookEdit]` so the researcher can never accidentally modify code; added decompilation fallback chain (ILSpy MCP → ilspycmd CLI → grep) with 3-failure circuit breaker
- **New skill:** `/deslop [path]` — regression-safe C# AI-slop cleanup: requires green tests first, deletion-first ordering (dead code → comments → null guards → inline single-use methods → extract duplicates), TAOM-specific slop patterns table
- **Updated skill:** `/deep-review` — added Step 2b adversarial escalation: when Agent 1 finds a CRITICAL adapter-pattern violation, a 5th agent launches in adversarial mode to confirm the violation, map blast radius, and produce minimum surgical fix plan
- **Updated CLAUDE.md:** `/deep-review` added to Critical Rules table (mandatory before every C# commit); commit trailers convention added (`Constraint:`, `Rejected:`, `Not-tested:`, `Research:`, `Save-compat:`)
- **Fixed:** `deep-review/SKILL.md` frontmatter `argument-hint` YAML quoting

## 2026-03-27

### Feature: Custom Battles

- TAOM Custom Battle support: all TAOM cultures, commanders, and troops available in Custom Battle mode
- 5 Harmony patches (Patch19_CustomBattles) replacing vanilla factions/commanders/troops with TAOM content
- Dynamic faction loading from ObjectManager (cultures with settlements, non-bandit)
- Dynamic commander loading with filtering (excludes companions, children, tutorial, vanilla commanders)
- Formation-to-troop mapping using culture militia/elite troop definitions
- Team-fix MissionBehavior preventing friendly fire in custom battles and custom sieges
- Custom battle GUI prefabs (already existed) now backed by service layer
- New IObjectManagerAdapter for testable ObjectManager access
- 29 new tests covering service logic and hook behavior

### Fix: Custom Battle NRE crash on screen init

- Root cause: lord characters and cultures were only registered for Campaign game type, not CustomGame
- CustomBattleSideVM.OnCharacterSelection crashed with NullReferenceException when Characters list was empty
- Fix: registered SPCultures (XSLT + custom), lords (XSLT + TAOM) for CustomGame/EditorGame in SubModule.xml
- Added safety fallback in Characters patch — falls back to vanilla if TAOM commander list is empty
- Fixed commander filtering: added "wanderer" and "notable" to exclusion list (wanderers/notables have is_hero=true but aren't lords)
- Fixed faction selector UI: `CustomBattleFactionSelectionVM` isn't a `SelectorVM`, so the dropdown couldn't work. Created `TaomFactionSelectionVM` subclass with `ExecuteSelectNextFaction`/`ExecuteSelectPreviousFaction` commands, injected via Harmony postfix on `CustomBattleSideVM` constructor. UI now uses arrow buttons matching the character selector pattern.

### Feature: Custom Culture Feats (Expanded)

- **59 custom feats** across 11 cultures (10 custom + Rohan XSLT), up from initial 30
- Party size feats: Mordor/Gundabad +30%, Dol Guldur +25%, Isengard +20%, Gondor +10%
- Food consumption feats: Rivendell/Mirkwood/Lothlorien -15%, Dol Guldur +10%
- Settlement loyalty feats: Gondor/Erebor +1/day, Lothlorien/Rivendell/Rohan +0.5/day
- Party morale feats: Gondor/Rohan/Erebor +5, Mirkwood/Lothlorien +3
- Smithing energy cost feats: Erebor -30%, Isengard -20%
- Tariff income feat: Umbar +15%
- Raid damage feats: Mordor/Gundabad +25%, Isengard +20%
- Rohan custom C# feats (replacing vanilla Vlandia): -15% mounted cost/wage, -10% speed when >50% infantry
- Erebor production feat changed from +30% animal-only to +10% ALL production
- Isengard construction speed flipped from -15% penalty to +15% bonus (industrial might)
- 7 new GameModel overrides: TaomPartySizeModel, TaomFoodConsumptionModel, TaomSettlementLoyaltyModel, TaomPartyMoraleModel, TaomSmithingModel, TaomClanFinanceModel, TaomRaidModel
- Feats registered via Harmony postfix on `Campaign.InitializeDefaultCampaignObjects()` (Patch18_CulturalFeats)
- 16 total GameModel overrides consuming feats
- Extended TaomPartyWageModel with Rohan mounted wage reduction (scaled by mounted troop fraction)
- Extended TaomPartySpeedModel with Rohan infantry speed penalty
- XSLT updated: Dunland uses Battanian feats, Rohan uses custom C# feats
- 64 tests verifying feat registration structure and property correctness

### Enhancement: Diplomacy & Alliance System Logging

- Added diagnostic logging to diplomacy enforcement hooks (`AllianceActionHook`, `PeaceActionHook`)
- Added initialization logging to `DiplomacyBehavior` and `WarOfTheRingBehavior`
- Added null-hook warnings to all 3 diplomacy Harmony patches for debugging initialization issues
- LogInfo for blocked actions (alliance end, war declaration, peace), LogDebug for allowed actions

### Fix: Warg Combat System — BT Runtime Failures

- **Bug:** Wargs never attacked in combat — 10x `ArgumentException` in `BehaviorTrees.dll`
- **Root cause 1:** `OnBehaviorInitialize` is never called for behaviors added during `SubModule.OnMissionBehaviorInitialize` in Bannerlord 1.3.12. `BTRegister.RegisterClass("WargTree")` never ran, so every `BehaviorTreeAgentComponent` failed to build its tree.
- **Fix:** Moved initialization from `OnBehaviorInitialize` to first `OnMissionTick` call via `_initialized` flag
- **Root cause 2:** `WargBehaviorTree` constructor line 30 (`Rider.GetValue().Formation`) threw NRE when warg had no rider at tree construction time
- **Fix:** Changed to `agent.RiderAgent?.Formation` (null-safe)
- **Safety net:** Added manual `comp.OnTickAsAI(dt)` loop in case engine doesn't call `OnTickAsAI` for mount agents
- **Verified:** 10 Dol Guldur Fell Warg-Riders in combat — all trees build successfully, wargs attack

## 2026-03-26

### Feature: Warg Combat System — Autonomous Warg AI (#44)

- **New feature:** Wargs are now autonomous combat agents with their own behavior tree AI, attacking enemies independently and entering rage mode when damaged
- **Ported from:** LOTRAOM's warg combat system, adapted for Bannerlord 1.3.12 APIs
- **Rage mode:** 10% chance on >10 damage — warg takes over control for 2-3 attacks, then returns to rider
- **Architecture:** BehaviorTree framework (pre-compiled DLLs) + SpatialGrid spatial partitioning + bone-based collision detection + reflection-based Mission.RegisterBlow
- **New adapters:** IAgentAdapter/AgentAdapter, IMissionAdapterFactory (mission-scope agent wrapping)
- **New services:** IWargAttackService (damage calc), IBoneCollisionService, ISpatialGridDebugService
- **Dependencies:** Alliance.Wargs (XML data), BehaviorTrees.dll, BehaviorTreeWrapper.dll
- **1.3.12 fixes:** MBAgentVisuals (renamed), WeakGameEntity (RegisterBlow reflection), OnMainAgentChangedDelegate signature, CombatLogData constructor, AIScriptedFrameFlags qualification
- **Files:** ~50 new C# files across Adapters/, Features/AdvancedCombat/, Features/Warg/
- **Cultures affected:** Gundabad, Dol Guldur, Isengard (7 warg-mounted troops)

### Feature: Troop Weight System — Elite Unit Party Capacity

- **New feature:** Elite/supernatural units consume more party capacity, preventing armies of pure elite troops
- **Weights:** Cave trolls (4x), legendary elf commanders (3x), all elves/warg riders/elite guards (2x), standard troops (1x default)
- **Mechanism:** Harmony postfixes on `PartyBase.NumberOfAllMembers`, `NumberOfRegularMembers` + 2 UI patches for recruitment and party screens
- **Config:** `ModuleData/TroopWeights/troop_weights.xml` — data-driven weight assignments for ~80 troop types across all cultures
- **MCM toggle:** "Enable Troop Weight" in Troop Weight settings group (enabled by default)
- **Architecture:** `ITroopWeightService` + `TroopWeightXmlLoader` + 4 hook implementations + 4 Harmony patches (`Patch17_TroopWeight`)
- **Ported from:** LOTRAOM's TroopWeight feature, adapted to TAOM conventions (static Initialize pattern, IPathService, simplified caching)
- **Stability fix:** Removed TroopRoster-level patches (fired on every roster in the game, caused IndexOutOfRange spam + freeze during loading). PartyBase-level patches are sufficient.
- **Fix:** Null-safe MCM guard prevents NRE when MCM is not loaded

### Feature: Atmosphere Persistence for Forced-Atmosphere Scenes

- **New feature:** Scenes with "forceatmo" in their name bypass campaign weather, preserving scene-embedded atmosphere
- **Ported from:** LOTRAOM's `AtmospherePersistence` feature (originally from The Old Realms mod)
- **1.3 refactor:** Replaced fragile string-based patch (`ScriptingInterfaceOfIMBMission`) with type-safe `Mission.Initialize()` prefix
- **Architecture:** Static `AtmosphereOverrideService` + thin Harmony patch (`Patch16_AtmospherePersistence`), follows `WeatherBoundsGuard` pattern
- **Tests:** 7 new tests for scene name detection (null, empty, case-insensitive, position variants)

### Feature: Startup Resources — Culture-Based Gold & Influence Distribution

- **New feature:** Lords receive startup gold and clans receive startup influence at new game creation, configured per culture via XML
- **Config:** `ModuleData/startup_resources/startup_resources_config.xml` — data-driven, all 15 cultures with gold (500K–6M) and influence (50–2000)
- **Architecture:** `StartupResourcesBehavior` fires at `OnNewGameCreatedPartialFollowUpEvent` index 1, delegates to `StartupGoldService` and `StartupInfluenceService`
- **Adapters:** `IStartupHeroAdapter`, `IGoldGiftAdapter`, `IClanStartupAdapter` wrap TaleWorlds sealed types
- **Tests:** 22 new tests covering config parsing, gold distribution, influence distribution, and behavior trigger logic
- **Ported from:** LOTRAOM's `StartupFunds` and `StartingInfluence` features

### Fix: NullReferenceException on Minor Faction Hero Spawning

- **Fixed:** Game crash (`NullReferenceException` at `CharacterObject.get_StealthEquipments()`) when spawning minor faction heroes (e.g. Ghilman) on new campaign start
- **Root cause:** Bannerlord v1.3 added `default_stealth_equipment_roster` attribute to cultures; the 4 XSLT-transformed cultures (Dunland, Harad, Rohan, Rhun) were missing it while the 10 custom cultures in `taom_spcultures.xml` had it
- **Fix:** Explicitly set `default_stealth_equipment_roster` in all 4 XSLT culture templates in `spcultures.xslt`

### Everything-Claude-Code Cherry-Pick: Developer Workflow Hooks & Skills

Reviewed the everything-claude-code repository (125+ skills, 28 agents, 60 commands) and adapted the most valuable patterns for TAOM's C#/Bannerlord workflow.

- **New skill:** `/build-fix [error]` — incremental dotnet build error fixer with C#/Bannerlord-specific error patterns (CS0246, CS0115, CS0234, etc.), one error at a time, minimal diffs
- **New skill:** `/verify [quick|full]` — comprehensive build + test + git verification with structured pass/fail report
- **New hook:** `config-protection.sh` (PreToolUse Edit|Write) — blocks AI edits to CLAUDE.md, Directory.Build.props, settings.json, and ADR files without explicit user request
- **New hook:** `suggest-compact.sh` (PreToolUse *) — counts tool calls per session, suggests `/compact` at 50 calls then every 25 after
- **New hook:** `mcp-health-check.sh` (PreToolUse mcp__*) — blocks MCP tool calls to servers marked unhealthy in last 60 seconds
- **New hook:** `mcp-health-mark.sh` (PostToolUseFailure mcp__*) — marks MCP server as unhealthy after failed tool call, 60s backoff
- **Updated hook:** `check-build-before-commit.sh` — added `--no-verify` flag blocking to protect pre-commit hooks
- **Updated agents:** `taleworlds-researcher.md` and `feature-builder.md` — added iterative retrieval (3-cycle progressive refinement) guidance
- **Updated:** `CLAUDE.md` with model routing table (Opus/Sonnet/Haiku guidance)
- **Updated:** `settings.json` with 4 new hook entries (config-protection, suggest-compact, mcp-health-check, mcp-health-mark)

### Claude Code Session Hooks, Agent Audit Logging & Scope-Check Skill

Cherry-picked ideas from the Claude Code Game Studios template and adapted them to TAOM's workflow. Adds session awareness, context recovery, agent tracking, and a scope assessment tool.

- **New hook:** `session-start.sh` (SessionStart) — prints branch, last 5 commits, latest CHANGELOG features, uncommitted file counts, and TODO/FIXME count on fresh session startup. Skips on resume/compact/clear.
- **New hook:** `pre-compact.sh` (PreCompact) — dumps all modified/staged/untracked files before context compaction so the file list survives summarization.
- **New hook:** `log-agent.sh` (SubagentStart) — silently logs every subagent invocation (type, ID, timestamp) to `.claude/logs/agent-audit.log`.
- **New skill:** `/scope-check [change]` — read-only assessment that classifies a proposed change as GREEN (natural extension), YELLOW (adjacent work), or RED (scope creep) based on CHANGELOG themes, recent commits, and in-progress work.
- **Updated:** `settings.json` with SessionStart, PreCompact, SubagentStart hook entries
- **Updated:** `.gitignore` with `.claude/logs/` exclusion
- **Updated:** `CLAUDE.md` hooks and skills tables, `agent-teams.md` troubleshooting and limitations sections

## 2026-03-25

### Remove "The" Prefix from Kingdom/Faction Names (#38)

Fixed in-game messages displaying awkward text like "The Erebor have formed an alliance with the Imladris" and "Daeron of the Mirkwood". The "The" came from two sources: TAOM's own formal name strings and vanilla localization templates designed for plural names like "Vlandians".

- **Stripped "The"** from 12 `str_faction_formal_name_for_culture.*` strings (e.g., "The Clans of Dunland" → "Clans of Dunland")
- **Overrode ~30 vanilla localization templates** in `taom_module_strings.xml` using GameText last-write-wins mechanism
- **Categories overridden:** diplomacy notifications, siege/raid news, battle results, faction titles, policy decisions, alliance/war decisions, peace warning prompts, minor faction dialogue
- **Grammar fixes:** adjusted plural verbs to singular ("have formed" → "has formed") for proper noun kingdom names
- **DLL token overrides** for policy/alliance messages (reuse same `{=TOKEN}` IDs) — needs in-game verification

### Alignment-Aware Execution System

Replaced vanilla Bannerlord's one-size-fits-all lord execution penalties with LOTR-thematic alignment logic. Free Peoples executing servants of Sauron now incur zero honor or relation penalties with allies. Same-alignment executions are treated as kinslaying with 50% harsher penalties.

- **New feature:** `Main/Features/Execution/` — full execution override system (12 new files)
- **GameModel override:** `TaomExecutionRelationModel` replaces `DefaultExecutionRelationModel` — alignment-aware relation penalties
- **Harmony patches:** `KillCharacterAction.ApplyInternal` (thread-local context) + `TraitLevelingHelper.OnLordExecuted` (honor penalty skip)
- **Alignment data:** `Main/_Module/ModuleData/execution/alignment.json` — 16 kingdoms mapped to Free/Evil/Neutral
- **Cross-alignment kills:** 0 honor penalty, 0 relation penalty with executor's allies
- **Kinslaying (same-alignment kills):** 1.5x vanilla penalties (-90 same-clan, -45 friend, -15 faction)
- **Neutral kingdoms (Umbar):** treated as enemy by both sides
- **28 new tests** covering AlignmentService and ExecutionActionHook
- **Documentation:** `docs/features/alignment-aware-execution.md`
- **Modified:** `IoC.cs`, `SubModule.cs` (registration + Patch14_Execution category)

### Child Equipment Templates for Custom Cultures

Added child equipment roster templates for all 10 custom TAOM cultures to prevent NullReferenceException during offspring delivery and ensure children spawn with culture-appropriate clothing.

- **New file:** `taom_child_equipment_templates.xml` — 60 equipment rosters (6 per culture: noble/townsman/villager × male/female)
- **Cultures covered:** gondor, erebor, rivendell, mirkwood, lothlorien, isengard, gundabad, mordor, dolguldur, umbar
- **Item selection:** lightest civilian items from each culture's Armory (tunics, dresses, boots)
- **Fallback sharing:** lothlorien reuses rivendell items, umbar reuses gondor items
- **Safety net:** existing `GetCivilianEquipment_Patch` Harmony patch retained as a defensive fallback
- Registered in SubModule.xml as EquipmentRosters

## 2026-03-21

### Erebor & Iron Hills Troop Tree Restructure

Complete overhaul of the Erebor faction troop trees based on artist specifications (41 new troops):

**Erebor Regular (T2-T6, 8 troops):** Miner → Militia → Skirmisher/Company branches → Bowman/Fighter → Mattock Warrior/Warrior terminals. Leather-to-chain armor progression.

**Erebor Noble (T3-T9, 13 troops):** Noble → Ranger/Longbeard branches → Archer line (Veteran Archer T6) + Infantry line (Guard → Shield-Guard → Gate Warden → Royal Warden T9) + 2H line (Axe-Guard → Veteran Axe-Guard → Shield-Breaker T8). Plate armor progression.

**Erebor Oathsworn (T7-T9, 3 troops):** Special rare line with legionary helmets. Oathsworn → Legionary → Royal Legionary. Chariots planned for future.

**Iron Hills Regular (T2-T6, 8 troops):** Recruit → Militia → Skirmisher/Company → Bowman/Fighter → Axe Warrior/Warrior. Uses Iron Hills items (sm_dwarf_iron_sword, iron shields, iron armor).

**Ironpass Regional Noble (T2-T7, 9 troops):** Recruit → Warrior → Infantry/Arbalest branches → Axeman → Veteran Axeman → Mountain Guard (T7). Uses crossbows and tower shields with Iron Hills heavy armor.

**Integration:**
- Old 47 troops orphaned (upgrade_targets cleared) for save compatibility
- Updated all 9 Erebor party templates with new troop IDs
- Updated spcultures: basic_troop=erebor_reg_miner, elite_basic_troop=erebor_noble
- Added Erebor settlement/clan/culture mappings to VolunteerRecruitmentService (13 settlements, 7 clans, 3-tier culture fallback)
- 24 new recruitment tests added (63 total passing)
- All item IDs validated against LOTRLOME_Armory

### Khamul's Troop Tree (Dol Guldur)

Added complete Khamul human troop tree (T4-T9, 14 troops total):
- 8 new troops: Shadow Initiate → Disciple → Infantry/Archer split → Warden/Marksman → 3-way elite split
- Updated 6 existing troops with Khamul-specific equipment
- Shadow Initiate marked as `is_basic_troop` — standalone entry point
- All Khamul troops are human (no race attribute), using `fighter_dolguldur` face template
- Added Khamul troops to DG party template + recruitment service

### Dol Guldur Troop Tree Fixes

- Fixed Goblin Skirmisher Bow skill 80 → 10 (was leftover from Ranged role)
- Removed `is_basic_troop` from `dg_warg_scout` (now upgrade from Orc Recruit)

## 2026-03-20

### Fix Siege Camp IndexOutOfRangeException

- Added Harmony Prefix patch on `BesiegerCamp.GetSiegeCampPartyPosition` to guard against empty `siegeCamp1GlobalFrames`
- Settlement "Gwígar" (and potentially others) has no `siege_camp_1` scene entities, causing `IndexOutOfRangeException` when a party starts a siege
- Patch swaps camp2 frames into camp1 slot when camp1 is empty, preserving vanilla positioning logic
- Falls back to settlement gate position if both camp frame arrays are empty

### Fix Villager Party Settlement Menu NRE

- Added battle equipment rosters to all 13 custom villager NPCs across all cultures
- Villagers only had `civilian="true"` equipment, causing `FirstBattleEquipment` to return null
- `CampaignUIHelper.GetCharacterCode` crashes on `.Clone()` when rendering the settlement party overlay
- Cultures fixed: Gondor, Dale, Erebor, Dunland, Dol Guldur, Gundabad, Harad, Isengard, Mordor, Rhûn, Rivendell, Mirkwood, Khand

### Fix Clan Owner NRE Crash

- Created 17 unique Harad lord heroes (`lord_A10_1` through `lord_A26_1`) for clans `clan_aserai_10`-`clan_aserai_26`
- Created 5 unique Umbar lord heroes (`lord_U2_1` through `lord_U6_1`) for clans `clan_umbar_2`-`clan_umbar_6`
- All 22 clans previously shared placeholder owners (`lord_3_1` / `lord_U1_1`), causing orphaned clans with null Kingdom at runtime and NRE in `ChangeKingdomAction.ApplyInternal`

### Fix Orphaned Clan Owners — Missing XSLT Faction Reassignment

- Fixed 9 custom clans whose owner heroes still had vanilla faction assignments in `heroes.xslt`
- Added `faction` attribute to XSLT templates for: `lord_6_21`-`lord_6_24` (Rhûn clans 10-13), `lord_1_34` (Faramir → Garvirionath), `lord_1_48` (Khamûl → Hîondrûs), `lord_4_23` (Marhad), `lord_4_28` (Morcargas), `lord_V11_l` (Deáfringas)
- Updated `spclans.xslt` to reassign vanilla clan owners for `clan_vlandia_7` (→ `lord_4_23_1`), `clan_vlandia_10` (→ `lord_4_28_1`), `clan_vlandia_11` (→ `lord_V11_u`)
- Also moved family members (spouses/children) to correct custom clans via `heroes.xslt`
- Root cause: `CharacterRelationCampaignBehavior.OnClanChangedKingdom` NRE when `oldKingdom` is null

### Fix Gondor Equipment — Replace Armory_2-Only Items

Replaced 367 equipment item references across 10 files that pointed to items only
available in `LOTRLOME_Armory_2` (not in `LOTRLOME_Armory` which TAOM depends on).
Characters in CC, NPCs, lords, and troops were appearing in underwear because the
body/head/leg/arm/cape items didn't exist at runtime.

**Item mapping (29 items replaced):**
- Body: `gondor_noble_coat_a/b` → `ithilien_jerkin_long/_var`, `gondor_noble_jerkin_a/b` → `ithilien_jerkin_short`/`boromir_jerkin`, `gond_tab_9ld` → `cts_gondor_armor3`, `citidel_guard_armor1/2/4` → `sk_gd_mns_citadel_chest_*`/`sk_gd_ano_inf_chest_heavy_a`, `fountain_armor1` → `sk_gd_mns_fount_chest_heavy_a`, `gondor_king_armor` → `sk_gd_ano_inf_chest_heavy_b`
- Head: `citidel_guard_helmet1/3/5` → `sk_gd_mns_cita_helmet_heavy_a/b`/`sk_gd_mns_noble_helmet_heavy_a`, `fountain_guard_helmet` → `sk_gd_mns_fount_helmet_heavy_a`
- Leg: `citidel_guard_boots/_light` → `sk_gd_ano_grvs_inf_med_a/_light_a`, `fountain_guard_boots` → `sk_gd_ano_grvs_noble_med_a`, `gondor_nobke_boots` → `sk_gd_ano_boots_a`
- Arms: `citidel_guard_gloves/bracers/bracers_shield` → `sk_gd_ano_gloves_a`/`sk_gd_ano_bracer_inf_med_a`/`sk_gd_ano_bracer_noble_med_a`, `gondor_nobke_bracers` → `sk_gd_ano_bracer_noble_heavy_a`
- Cape: `citidel_guard_armor_pauldrons/_light` → `sk_gd_ano_pauld_inf_heavy_a/_med_a`, `fountain_guard_pauldrons` → `sk_gd_ano_pauld_cape_fount_elite_a`, `fountain_shoulders2` → `sk_gd_ano_pauld_noble_med_a`, `gondor_nobke_pauldrons` → `sk_gd_ano_pauld_noble_heavy_a`

**Files modified:** `taom_char_creation_equipment.xml`, `taom_equipment_sets_gondor.xml`, `npcs_gondor.xml`, `npcs_umbar.xml`, `troops_gondor.xml`, `troops_umbar.xml`, `troops_rohan.xml`, `troops_rivendell.xml`, `taom_wanderer_equipment.xml`, `lords.xml`

Also removed non-existent `spc_wanderer_rohan_9` reference from `spcultures.xslt`.

### Fix Null Object Reference Errors

- Added missing `spc_wanderer_rohan_9` wanderer (definition, skill set, backstory strings)
- Reassigned Gondor heroes (lord_EW_9/14/23/20) from non-existent clans 15-18 to existing empire_west clans 10-13
- Reassigned Mordor heroes (lord_M16_1/17_1/18_1) from non-existent clans 16-18 to existing empire_south clans 10-12
- Fixed Easterling caravan templates: `caravan_template_khuzait` → `caravan_template_rhun` (matching Rohan pattern)

### Rhûn Troop Generator

Created `tools/generate_rhun_troops.py` — Python generator replacing manually-maintained XML with
113 troops across 11 unit groups:
- **Easterling Regular** (T1-T5, 13 troops) — `sk_rh_loke_` spiky/east armor
- **Loke-Rim Noble** (T3-T7, 14 troops) — `sk_rh_loke_` half-plate → plate, role-specific helmets
- **Dragon-Wrath** (T5-T9, 14 troops) — `sk_rh_drag_` half-plate → plate
- **Wainriders** (T3-T7, 8 troops) — `sk_rh_loke_` lamellar/arch helmets
- **Black Sun Mercenaries** (T2-T8, 11 troops) — `sk_rh_drag_` lamellar (shock) / spiky (archer)
- **Darkhûn Mercenaries** (T2-T8, 11 troops) — `sk_dg_khml_` half-plate (inf) / lamellar (cav)
- **Sagarûn** (T3-T7, 10 troops) — Loke scalemail (marines) / Drag scalemail (naffatun/arbalest)
- **Balcoth** (T2-T6, 9 troops) — Easterling Regular armor
- **Far-Rhun** (T3-T7, 9 troops) — Easterling Regular armor
- **Kharaghûl** (T2-T7, 10 troops) — Easterling Regular armor
- **Militia** (T2-T3, 4 troops) — old easterling armor (preserved)

Deleted `troops_rhun_new.xml` (superseded) and removed its SubModule.xml entry.
Updated `rebalance_troops.py` to process `troops_rhun.xml` (was skipped when old/new coexisted).

### Dol Guldur Troop Tree Restructure

Restructured all three non-Khamul DG troop lines to match artist spec:

**Goblin line** — converted from linear chain to branching tree:
- Renamed "Goblin Slave" display to "Goblin Runt" (ID unchanged for save compat)
- Added 3 new troops: Goblin Harrier (T2 melee), Goblin Impaler (T4 melee), Goblin Fellbow (T5 ranged)
- Runt now splits into Harrier (melee branch) and Crawler (ranged branch)
- Skirmisher moved to melee branch (Infantry), retooled equipment from bows to melee weapons
- Hunter now upgrades directly to Archer (was Skirmisher)

**Orc line** — connected Warg branch:
- Orc Recruit now upgrades to both Orc Gnasher AND Warg Scout (was Gnasher only)
- Removed Orc Scout branch from Orc Warrior upgrade path (Warrior → Reaver only)
- Orc Scout and Orc Archer kept as orphaned troops for save compatibility

**Uruk line** — display name corrections:
- "Uruk Warrior" (T3) renamed to "Uruk Fighter" to match spec
- "Uruk Veteran Warrior" (T4) renamed to "Uruk Warrior" to match spec

Updated ALL Dol Guldur party templates:
- `kingdom_hero_party_dolguldur_template`: added Harrier, Archer, Impaler, Fellbow stacks
- `kingdom_hero_party_outlaw_dolguldur_template`: added Harrier
- `patrol_party_dolguldur_template_level_1`: added Harrier
- `patrol_party_dolguldur_template_level_3`: added Khamul Shadow Warden + Marksman
- `rebels_dolguldur_template`: added Harrier
- `vassal_reward_troops_dolguldur`: added Khamul Shadow Infantry + Archer

Added `.claude/rules/troops.md` — troop management checklist, race attributes, party template types, save compatibility rules.

## 2026-03-19

### Khamul's Troop Tree (Dol Guldur)

Added complete Khamul human troop tree (T4-T9, 14 troops total):
- 8 new troops: Shadow Initiate → Disciple → Infantry/Archer split → Warden/Marksman → 3-way elite split
- Updated 6 existing troops (Veiled Knight/Guard/Marksman, Shadow Knight/Guard/Bowman) with Khamul-specific equipment
- Shadow Initiate marked as `is_basic_troop` — standalone entry point, disconnected from generic DG feeder troops
- All Khamul troops are human (no race attribute), using `fighter_dolguldur` face template
- PLATE armor line (Guard/Knight), SPIKY armor line (Reaper/Archer)

Integration:
- Added Khamul troops to `kingdom_hero_party_dolguldur_template` party template
- Added Dol Guldur settlement/clan/culture mappings to `VolunteerRecruitmentService` (with tests)
- Removed Khamul upgrade targets from generic `dg_warden` and `dg_marksman` feeder troops

### Gondor Old Asset Cleanup

Removed 66 orphaned armor item entries from LOTRLOME_Armory gondor XMLs whose FBX source
files were deleted in lotraom-assets commit `defb2642`:
- head_armors.xml: -31 items (citadel helmets, fountain helmets, old soldier helmets)
- body_armors.xml: -14 items (citadel/fountain/king/noble armor, old tabard)
- shoulder_armors.xml: -9 items (citadel/fountain/king/noble/old pauldrons)
- arm_armors.xml: -5 items (citadel bracers/gloves, king/noble bracers)
- leg_armors.xml: -7 items (citadel/fountain/king/noble/old boots)

Fixed 4 militia troops referencing deleted body armor (gondor_noble_jerkin_a/b,
gond_tab_9ld, gondor_noble_coat_a) — replaced with sk_gd_ano_chainmail_* items.

Added 10 missing armor items (total now 93): 3 elite body, 5 shoulders, 2 elite bracers.

Replaced 13 additional old Gondor items with `sk_gd_*` equivalents across all equipment sets
(troops, lords, NPCs, wanderers, char creation, equipment sets):
- 7 helmets → `sk_gd_ano_inf_helmet_med_a` / `heavy_a` / `sk_gd_ano_noble_helmet_med_a`
- 1 body → `sk_gd_ano_chainmail_half_a`
- 2 shoulders → `sk_gd_ano_pauld_inf_med_a`
- 1 arm → `sk_gd_ano_bracer_noble_med_a`
- 1 leg → `sk_gd_ano_boots_a`
- Removed all 79 orphaned items from both lotraom-assets and Steam armory XMLs

Cleanup script: `tools/cleanup_deleted_gondor_armor.py`

### Gondor Equipment Pass — 6 Guided Groups + Scaffolding

Created 83 new armor item definitions (`sk_gd_*` prefix) in LOTRLOME_Armory for 6 guided groups:
- **Anorien Regular** — Generic infantry base armor (chainmail → heavy chest progression)
- **MT Citadel Guard** (T5-T8) — Citadel-specific chest/helmet progression
- **MT Fountain Guard** (T9) — Elite fountain helmet + cape+pauldron combo
- **Osgiliath** (T3-T7) — Branch-specific helmets (Infantry/Dome Guard vs Longbow)
- **Cair Andros** (T3-T7) — Branch-specific helmets (Pike vs Warden)
- **Minas Ithil** (T5-T9) — Noble armor progression, Moon Guard at T9

Refactored remaining 17 region equip functions to tier-based dictionary structure:
- 20 dict sets (LOSS_*, PEL_*, DA_INF_*, etc.) with empty slots ready for future armor guides
- `_apply_region_armor()` helper falls back to GENERIC_* when dict values are empty
- All region-specific weapons preserved (axes, swan knight spears, etc.)
- Generator: `tools/generate_gondor_armor.py` (--dry-run / --apply)

### New Gondor Troop Tree

Replaced the existing 77-troop Gondor tree with a comprehensive 182-troop tree spanning 23 unit groups across 18 sub-regions:

**8 Regular Lines** (village recruitment): Lossarnach, Lebennin, Lamedon, Belfalas, Pinnath Gelin, Anfalas, Harondor, Anorien
**15 Noble Lines** (notable recruitment): Lossarnach Noble, Pelargir, Calembel, Ringlo Vale, Dol Amroth, Linhir, Tolfalas, Arndir, Blackroot Vale, Serelond, Lond-Galen, Methir, Minas Ithil, Cair Andros, Osgiliath, Minas-Tirith

- 24 is_basic_troop roots for recruitment
- Skills balanced via rebalance_troops.py (Gondor cultural modifiers + weapon specializations)
- Equipment reused from existing Gondor item pool, themed by sub-region
- Generator script: `tools/generate_gondor_troops.py`
- Notable elite units: Swan Knights (T9), Fountain Guard (T9), Moon Guard (T9)

**Note**: spcultures.xml and partyTemplates.xml references not yet updated — old troop IDs still referenced.

## 2026-03-15

### Bug Fix — Character Creation Race Display (#22)

Non-human races (dwarf, elf, uruk, etc.) displayed as human models during character creation. Two root causes:

**Race filtering broke FaceGenVM** — The `FaceGen_GetRaceNames_Patch` postfix filtered `GetRaceNames()` globally, but `FaceGenVM` uses array index as global race ID. Filtering shifted all indices (dwarf→uruk, uruk→orc, nazgul→goblin).
- Disabled race filtering in `FaceGen_GetRaceNames_Patch` (now a no-op, all races shown in dropdown)
- Removed `CharacterTableau_SetRace_Patch` race index mapper prefix (no longer needed)
- Stripped `FilterRaceNames` and `MapFilteredIndexToGlobalId` from `GetRaceNamesHook` / `IOnGetRaceNames`
- Simplified `CharacterCreationIoC` — removed filter/mapper wiring

**Body property templates pointed to human** — 7 non-human cultures had `default_character_creation_body_property` set to empire (human) template instead of race-specific templates.
- Updated `taom_spcultures.xml`: erebor→`fighter_erebor`, rivendell→`fighter_rivendell`, mirkwood→`fighter_mirkwood`, lothlorien→`fighter_rivendell`, isengard→`fighter_uruk_hai`, gundabad→`fighter_gundabad`, dolguldur→`fighter_dolguldur`

**Secondary fix** — Female action set name had double underscore in `CharacterTableau_RefreshCharacterTableau_Patch` (`as_dwarf_female__warrior` → `as_dwarf_female_warrior`).

240 tests passing.

## 2026-03-12

### Bug Fix — Youth Equipment Differentiation (Phase 6)

Fixed bug discovered during in-game testing of character creation:

**Youth equipment all identical** — Youth narrative options were not setting `SelectedTitleType`, causing all options to produce the same equipment regardless of selection.
- Added `TitleType` property to `NarrativeOptionDefinition` model
- Updated `NarrativeMenuBuilder.BuildOption()` to set `SelectedTitleType` when `title_type` is present (vs `SetParentOccupation` for parent menus)
- Updated `NarrativeDataProvider.ParseOption()` to parse `title_type` from JSON
- Added `title_type` to all 91 entries in `youth_menu.json` mapping each option to a career (retainer, guard, hunter, infantry, skirmisher, bard, mercenary)

### Feature — Character Creation Equipment Rosters (Phase 5)

Created culture-specific equipment rosters for all 10 custom cultures, replacing the temporary `EquipmentCultureRemap_Patch` Harmony workaround.

- `tools/generate_char_creation_equipment.py` — Python generator producing 550 equipment rosters from per-culture item mappings
- `ModuleData/taom_char_creation_equipment.xml` — 550 rosters (55 per culture × 10 cultures)
  - 2 parent fallback (`none`), 12 parent occupation, 24 childhood/education age, 16 adult career, 1 show per culture
- Items sourced from LOTRLOME_Armory module with culture-appropriate low-tier gear
- Lothlorien uses Rivendell items; Umbar uses Rhun/Easterling items
- Registered in `SubModule.xml` as `EquipmentRosters` node
- Removed `EquipmentCultureRemap_Patch.cs` and `Patch8_CharacterCreation` from `SubModule.cs`

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
