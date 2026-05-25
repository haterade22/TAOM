# TAOM - Tales From the Age of Men

A Lord of the Rings total conversion mod for Mount & Blade II: Bannerlord v1.4.5.

## Overview

TAOM reimagines Bannerlord as Middle-earth during the War of the Ring. Sixteen factions wage war across a custom map with over 500 unique troops, race-specific lifespans, autonomous warg AI, alignment-driven diplomacy, a full career and class progression system, per-kingdom special resources, and dozens of other systems built to make Calradia feel like Tolkien's world. Every kingdom, clan, lord, and troop has been replaced or rewritten to fit the setting.

**By the numbers:** 31+ GameModel overrides, 28 Harmony patch categories, 50 careers across 16 cultures, 40+ documented features, 944 unit tests, and a custom AI-assisted development pipeline built on Claude Code and Codex.

## Factions & Cultures

### Free Peoples

| Faction | Identity | Notable Traits |
|---------|----------|----------------|
| **Gondor** | The last great kingdom of Men | Regular, Ranger, and Gondolin elite lines |
| **Rohan** | Horse lords of the Riddermark | Cavalry-focused with Royal Guard elite |
| **Rivendell** | Elven refuge of Elrond | Immortal warriors, 2x troop weight |
| **Mirkwood** | Woodland realm of Thranduil | Scout and Palace Guard lines |
| **Lothlorien** | Golden Wood of Galadriel | Elven guardians and archers |
| **Erebor** | Dwarven kingdom under the Mountain | 5 troop lines across 41 units |
| **Dale** | Frontier kingdom of Men | Defenders, archers, and militia |
| **Arthedain** | Northern Dunedain remnant | Human kingdom |

### Dark Powers

| Faction | Identity | Notable Traits |
|---------|----------|----------------|
| **Mordor** | Sauron's domain | Orcs, Uruks, Trolls, Black Numenoreans |
| **Isengard** | Saruman's war machine | Uruk-Hai infantry and warg riders |
| **Gundabad** | Orcish stronghold | Orc hordes with warg cavalry |
| **Dol Guldur** | The Necromancer's fortress | 14-unit Khamul shadow line |
| **Easterlings** | Eastern empire of Rhun | 113 troops across 11 unit groups |
| **Harad** | Southern desert kingdom | Spearmen, archers, camel cavalry |
| **Khand** | Eastern tribal lands | Tribal warriors |

### Neutral

| Faction | Identity | Notable Traits |
|---------|----------|----------------|
| **Umbar** | Corsair kingdom | Hostile to both alignments |

Over **100 clans** and **500+ unique troop definitions** across all factions.

## Features

### Career System

A full career and class progression system with 50 careers across 16 cultures. Players choose their career during character creation and progress through a tiered choice tree (31 choices per career) that unlocks passive bonuses and an active battlefield ability.

- **3 archetypes:** Infantry (AoE troop buff), Ranged (self ranged buff), Cavalry (self + mount buff)
- **Active abilities:** Press V in battle to activate — XML-tunable damage, duration, radius, and cooldown
- **Career screen UI:** AI-generated portraits (800x400) and ability icons (256x256) in a dedicated sprite atlas
- **Passive integration:** Career perks feed into GameModel overrides (wages, party speed, morale, upgrade costs, etc.)
- **Character creation stage:** 6th narrative menu lets players pick from culture-eligible careers with skill/attribute bonuses

### Special Resources

Per-kingdom resource system with 11 unique resources across 18 kingdoms. War Spoils, Gems, Castar, Marks, Elven Wine, Lake Fish, War Drums, Tribal Relics, Dunlending Ale, Plunder, and War Banners — each earned through battles, quests, and daily income. Resources gate elite troop upgrades and display on the map bar with rich tooltips. XML-driven with many-to-one kingdom/culture mappings.

### Cultural Feats

16 custom culture feats replacing vanilla feats, each providing unique bonuses that reflect the lore. Rohan gets cavalry and forest speed bonuses. Erebor gets smithing energy discounts. Mordor gets raid damage. Gondor gets settlement loyalty. All backed by dedicated GameModel overrides.

### War of the Ring

Scripted phased escalation into total war. Isengard and Dunland strike Rohan first, then all hostile-tier kingdom pairs are drawn into permanent conflict. Three layers of peace-blocking ensure the War of the Ring cannot end with a handshake. Configurable via JSON and MCM settings.

### Race & Age System

Each race has its own lifespan and fertility rate. Elves are effectively immortal with very low birth rates. Dwarves live 250 years. Orcs burn out at 50-60 but breed aggressively. Nazgul and Saruman cannot die of age. Heroes die and reproduce at race-appropriate rates, keeping faction demographics consistent with the lore.

### Warg Combat

Wargs fight autonomously using a behavior tree AI framework. They select and attack nearby enemies on their own. Taking heavy damage triggers rage mode (10% chance): the warg takes control from its rider for 2-3 attacks. Uses spatial grid partitioning for efficient proximity queries and bone-based collision detection for accurate hits.

### Named Companions

18 lore-faithful companions (Aragorn, Legolas, Gimli, and more) implemented as recruitable wanderers. Each has a designated spawn settlement, custom equipment, and vanilla dialog integration. Race persistence handled through the existing HeroRace system.

### Settlement Guards

Per-settlement guard customization with XML-driven guard troop pools. Settlement-to-clan-to-culture fallback chain for troop selection, spawn-point filtering, weighted random selection, and per-culture spear mappings. Harmony prefixes on private `GuardsCampaignBehavior` methods.

### Diplomacy & Alliances

Kingdom relationships defined in tiers: Permanent Alliance, Alliance, Neutral, Natural Enemy, Hostile, and Permanent War. Racial enmity constraints on alliance formation. Culture-specific execution penalties. The diplomacy system integrates with War of the Ring to enforce permanent hostilities between Free Peoples and Dark Powers while allowing internal diplomacy within each alignment.

### Tournament Armor

Per-participant culture armor in tournaments. Culture-specific prize pools with tier-based regular and elite rewards. Backed by `TaomTournamentModel`.

### Banner Color Persistence

Player clan colors persist everywhere — UI, 3D battles, conversations, inventory, party screens. 15+ Harmony patches ensure banner colors don't drift when vanilla code tries to update them during War of the Ring kingdom changes.

### Army Targeting

Besieger army AI with commitment stickiness (4x), faction priority lists, strength gate bypass per faction, distance compensation, and border proximity floor. Prevents AI armies from abandoning sieges or ignoring strategic targets.

### Troop Weight System

Elite units consume more party capacity. A cave troll costs 4 party slots. Elven warriors cost 2. Standard infantry costs 1. Configurable weights per troop in XML, toggleable via MCM.

### Alignment-Aware Execution

Executing an enemy of your alignment carries no honor penalty. Kinslaying inflicts 1.5x vanilla penalties. Umbar is hostile to everyone.

### Custom Battles

TAOM factions, commanders, and troops available in Custom Battle mode. Play any faction matchup without starting a campaign.

### Siege Defense

Timed defense events when watched factions are besieged. Config-driven watched factions with CampaignTime deadlines. Relation and influence rewards on arrival.

### Additional Systems

| System | Description |
|--------|-------------|
| **Atmosphere Persistence** | Forced-atmosphere scenes (Moria, Dead Marshes, Fangorn) resist weather override |
| **Startup Resources** | Per-culture starting gold (Rivendell 6M, Isengard 2M, Gondor 500K) |
| **Character Creation** | 10 custom cultures with race-specific bodies, equipment, skills, and LOTR backstories |
| **XSLT Transformations** | 415 biographies, ~350 lords, 73 clans, 8 kingdoms renamed at load time |
| **Offspring Race Inheritance** | Children inherit race from same-sex parent |
| **Shader Precompilation** | Pre-compile shaders from the menu to eliminate in-game stutter |
| **Main Menu Customizer** | Hides Campaign, renames Sandbox to "Enter The Age Of Men" |
| **Troop Progression** | Extended tier system (T0-T10) with configurable military power per tier |
| **No-Mount Cultures** | Suppress horse crashes for cultures without mounts during character creation |

### GameModel Overrides (31+)

All GameModel overrides extend vanilla behavior with LOTR-appropriate rules. Career passives and cultural feats are integrated directly into these models.

| Model | Purpose |
|-------|---------|
| `TaomCharacterStatsModel` | Max tier 10 (vanilla 6) |
| `TaomPartyWageModel` | Extended T0-T10 wages + culture/career bonuses |
| `TaomVolunteerModel` | Max volunteer tier 6 (vanilla 4) |
| `TaomArmyManagementModel` | Culture army influence feats |
| `TaomPartySpeedModel` | Culture forest speed + career bonus |
| `TaomSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | Culture production feats |
| `TaomCaravanModel` | Umbar caravan cost feat |
| `TaomBattleRewardModel` | Umbar renown + career bonus |
| `TaomPartyTroopUpgradeModel` | Mounted recruit cost + career bonus |
| `TaomPartySizeModel` | Party size feats (5 cultures) + career bonus |
| `TaomFoodConsumptionModel` | Food consumption feats (elves, Dol Guldur) |
| `TaomSettlementLoyaltyModel` | Settlement loyalty feats (4 cultures) |
| `TaomPartyMoraleModel` | Party morale feats (4 cultures) + career bonus |
| `TaomSmithingModel` | Smithing energy feats + career enchantment bonus |
| `TaomClanFinanceModel` | Umbar tariff income feat |
| `TaomRaidModel` | Raid damage feats (3 cultures) + career bonus |
| `TaomMilitaryPowerModel` | Configurable T7-T10 troop power (MCM + JSON) |
| `TaomCombatSimulationModel` | Configurable blunt/cut damage ratio per battle type |
| `TaomPartyHealingModel` | Per-faction death chance multiplier |
| `TaomTournamentModel` | Per-participant culture armor + prize pools |
| `TaomAgeModel` | Race-appropriate lifespans (elven immortality, dwarf aging) |
| `TaomPregnancyModel` | Race-appropriate pregnancy durations |
| `TaomHeroCreationModel` | Race-aware hero creation defaults |
| `TaomAllianceModel` | Racial enmity constraints on alliances |
| `TaomKingdomDecisionPermissionModel` | Culture/race-based decision rules |
| `TaomDiplomacyModel` | LOTR faction relationship logic |
| `TaomExecutionRelationModel` | Culture-specific execution penalties |
| `TaomInformationRestrictionModel` | Encyclopedia visibility per settings |
| `TaomTargetScoreModel` | Besieger AI commitment + faction priority lists |

### Harmony Patches (28 categories)

28 categorized Harmony patch sets (Patch0 through Patch28) covering battle scenes, race assignment, face generation, banner editing, diplomacy, weather bounds, troop weight, cultural feats, custom battles, shader precompilation, army targeting, banner color persistence, special resources, career system, and settlement guards.

## For Players

### Requirements

- Mount & Blade II: Bannerlord **v1.4.5+**
- [Bannerlord.Harmony](https://www.nexusmods.com/mountandblade2bannerlord/mods/2006)
- [Mod Configuration Menu (MCM)](https://www.nexusmods.com/mountandblade2bannerlord/mods/612)
- Alliance.Wargs module (bundled)

### Installation

1. Install the required dependencies listed above
2. Extract the TAOM module folder into your Bannerlord `Modules/` directory
3. Enable the mod in the Bannerlord launcher
4. Start a new campaign — existing saves are not supported

## Development Toolchain

TAOM is built with a multi-IDE, AI-assisted development pipeline. The mod code is written in C# targeting .NET Framework 4.7.2, with extensive use of AI tooling for code generation, review, and quality assurance.

### Visual Studio 2022 (2026)

Visual Studio 2022 is the **primary IDE** for C# development, debugging, and test execution.

- **Solution:** `TAOM.sln` at the project root contains both `Main` (mod code) and `TAOM.Tests` (unit tests)
- **Target framework:** .NET Framework 4.7.2 with C# 10 language features and nullable reference types enabled
- **Build configuration:** `Directory.Build.props` at the root sets shared properties — target framework, platform (x64), language version, and Bannerlord DLL references via the `BANNERLORD_GAME_DIR` environment variable
- **Debugging:** Attach to the Bannerlord process (`TaleWorlds.MountAndBlade.Launcher.exe`) for live debugging of Harmony patches, GameModel overrides, and CampaignBehaviors
- **Test runner:** MSTest with NSubstitute for mocking — 944 unit tests run directly from the Test Explorer
- **Build commands:**
  ```powershell
  .\build.ps1                # Build only
  .\build.ps1 -RunTests      # Build + run tests
  dotnet test TAOM.Tests     # Tests only
  ```

### Visual Studio Code

VS Code serves as a **secondary IDE**, particularly useful for XML/XSLT/JSON editing, MCP server interaction, and running Claude Code.

**Configured extensions** (`.vscode/extensions.json`):
- `anthropic.claude-code` — Claude Code integration
- `ms-dotnettools.csharp` + `ms-dotnettools.csdevkit` — C# language support
- `dotjoshjohnson.xml` + `redhat.vscode-xml` — XML editing and validation
- `ms-vscode.powershell` — PowerShell script support
- `github.vscode-pull-request-github` — PR management

**MCP servers** (`.vscode/mcp.json`):
| Server | Purpose |
|--------|---------|
| `filesystem` | File operations across TAOM, Bannerlord Modules, and LOTRAOM assets (3 mount points) |
| `git` | Rich git operations (diff, blame, log, branch management) |
| `ilspy` | Decompile TaleWorlds DLLs for research |

**Build tasks** (`.vscode/tasks.json`):
- Build, Build + Test, and Run Tests — all mapped to `build.ps1` or `dotnet test`

### Claude Code

[Claude Code](https://docs.anthropic.com/en/docs/claude-code) is Anthropic's agentic coding tool. It is deeply integrated into TAOM's development workflow as an AI development partner — not just for code generation, but as a structured engineering system with its own rules, skills, agents, hooks, and memory.

The project-level instruction file [CLAUDE.md](CLAUDE.md) is over 2,000 lines and serves as the authoritative reference for every Claude Code session. It defines critical rules, architecture patterns, file paths, GameModel/Harmony patch registries, MCP server usage, research procedures, and the mandatory completion workflow.

#### Custom Skills (14 slash commands)

Skills are project-specific slash commands that Claude Code executes as structured workflows.

| Skill | Purpose |
|-------|---------|
| `/verify` | Run build + tests + git status, produce pass/fail report |
| `/deep-review [feature]` | Launch 5+ parallel agents checking standards, compatibility, efficiency, completeness, and data flow |
| `/research [Class]` | Decompile and analyze TaleWorlds classes before implementing |
| `/new-feature [Name]` | Scaffold a new feature module with IoC, services, adapters, and tests |
| `/build-fix [error]` | Incrementally fix .NET build errors, one at a time, minimal diffs |
| `/issue [type] [desc]` | Create a GitHub issue with all required TAOM sections |
| `/commit-split` | Group changed files by logical concern and commit each group atomically |
| `/review-codex` | Auto-detect what was built, write a Codex adversarial prompt, dispatch, then verify results |
| `/codex-verify` | Dispatch independent Codex verification job in the background |
| `/deslop [path]` | Regression-safe cleanup of AI-generated code bloat (deletion-first, tests-first) |
| `/xslt-check [file]` | Validate XSLT transformations against SandBoxCore vanilla XML |
| `/scope-check [change]` | Assess whether a proposed change fits the current work context |
| `/new-adr [name]` | Scaffold an auto-numbered Architecture Decision Record |
| `/migration-status` | Check v1.2 to v1.3 Bannerlord migration progress |

#### Custom Agents (2 specialized agents)

| Agent | Purpose |
|-------|---------|
| `taleworlds-researcher` | Decompile and analyze TaleWorlds game DLLs for adapter implementations, Harmony patches, and GameModel overrides |
| `feature-builder` | Build complete feature modules following TAOM architecture, TDD, and adapter patterns |

#### Scoped Rules (10 domain-specific rule files)

Rules are automatically loaded based on the file path being edited. When Claude Code opens a file matching a rule's glob pattern, the rule's constraints are injected into the session.

| Rule | Scope | Enforces |
|------|-------|----------|
| `adapters.md` | `Main/Adapters/**` | Adapter pattern, research-first mandate |
| `harmony-patches.md` | `Main/**/Hooks/**` | Patch types, thin entry points, thread-local state |
| `gamemodels.md` | `Main/Features/**/*Model.cs` | GameModel override pattern, base class rules |
| `tests.md` | `TAOM.Tests/**` | TDD, naming conventions, AAA pattern, 100% service coverage |
| `xslt.md` | `**/*.xslt` | XSLT passthrough, SandBoxCore reference |
| `xml-data.md` | `ModuleData/**/*.xml` | NPC naming, region codes, formatting |
| `troops.md` | `troops/**`, `TroopProgression/**` | Troop checklist, races, party templates, save compat |
| `gui-ui.md` | `*Mixin*.cs`, `*Widget*.cs`, `*VM.cs`, `GUI/**` | Sprite verification, UIExtenderEx safety, ViewModel bindings |
| `csharp-architecture.md` | `Main/**/*.cs` | Layer stack, IoC lifetimes, non-negotiable rules |
| `csharp-patterns.md` | `Main/**/*.cs` | Hook/Strategy/GameModel quick reference |

#### Hooks (12 automated triggers)

Hooks are shell scripts that execute automatically in response to Claude Code events — before/after tool calls, on session start, before compaction, and on stop.

| Hook | Trigger | Purpose |
|------|---------|---------|
| `session-start.sh` | SessionStart | Print branch, recent commits, CHANGELOG summary |
| `check-build-before-commit.sh` | PreToolUse (Bash) | Block `git commit` if build fails |
| `config-protection.sh` | PreToolUse (Edit/Write) | Block edits to CLAUDE.md, Directory.Build.props, ADRs without explicit request |
| `suggest-compact.sh` | PreToolUse (*) | Suggest `/compact` after 50 tool calls |
| `mcp-health-check.sh` | PreToolUse (MCP) | Block MCP calls to servers marked unhealthy |
| `notify-csharp-edit.sh` | PostToolUse (Edit/Write) | Log C# file modifications |
| `notify-test-results.sh` | PostToolUse (Bash) | Report test outcomes |
| `mcp-health-mark.sh` | PostToolUseFailure (MCP) | Mark MCP server unhealthy (60s backoff) |
| `pre-compact.sh` | PreCompact | Dump modified files list before context compaction |
| `log-agent.sh` | SubagentStart | Audit log agent invocations |
| `check-changelog-updated.sh` | Stop | Remind to update CHANGELOG.md |
| `check-deep-review.sh` | Stop | Remind to run `/deep-review` if real work was done |

#### MCP Servers (7 servers)

Claude Code connects to Model Context Protocol servers for structured access to external tools and data.

| Server | Purpose |
|--------|---------|
| **Serena** | Symbolic C# code navigation — find symbols, references, overview |
| **GitHub** | PRs, issues, actions, code search |
| **filesystem** | File operations across TAOM, Bannerlord Modules, and LOTRAOM assets |
| **git** | Rich git operations (diff, blame, log, branch management) |
| **ilspy** | Decompile TaleWorlds DLLs on demand |
| **context7** | Library documentation lookup |
| **sequential-thinking** | Extended reasoning for complex design decisions |

#### Memory System

Claude Code maintains a persistent, file-based memory system (`.claude/memory/`) that carries context across sessions. Memories track user preferences, correction patterns, project state, and external references — so each new session starts with full context rather than from scratch.

#### Model Routing

Different Claude models are used for different task types to optimize cost and quality:

| Task | Model | Rationale |
|------|-------|-----------|
| Architecture decisions, complex design | **Opus** | Deepest reasoning for trade-off analysis |
| Feature implementation, code review | **Sonnet** | Best coding model, fast iteration |
| Research, documentation, exploration | **Haiku** | 90% of Sonnet quality at 3x cost savings |
| Explore agents (codebase search) | **Haiku** | Read-only search doesn't need full reasoning |
| Plan agents (design work) | **Sonnet** | Needs coding awareness for implementation plans |

#### Mandatory Completion Workflow

Every feature goes through a 4-phase quality gate before closing:

1. **Build & Internal Review** — `/verify` + `/deep-review` (5+ parallel agents) + fix issues
2. **Codex Adversarial Review** — `/review-codex` dispatches to Codex, then verifies and implements fixes
3. **Self-Review** — `/review-codex` reviews our own fixes through another Codex pass
4. **Close Out** — Final `/verify`, GitHub issue, feature docs, CHANGELOG update

### Codex (OpenAI)

[Codex](https://openai.com/index/introducing-codex/) is OpenAI's coding agent, integrated into TAOM as an **independent adversarial reviewer**. Because Codex shares no session context with Claude Code, it provides a genuine second opinion on code quality.

**Configuration:** `.codex/config.toml` — runs `o4-mini` model with high reasoning effort in workspace-write sandbox mode.

**Review instructions:** [AGENTS.md](AGENTS.md) (541 lines) — a comprehensive review guide covering severity tiers (CRITICAL/HIGH/MEDIUM/LOW), architectural rules, all 31+ GameModels, 28 Harmony patch categories, adapter pattern enforcement, TDD requirements, and lessons learned from prior reviews.

**Integration with Claude Code:**

Codex operates via the `codex-plugin-cc` Claude Code plugin. Key commands:

| Command | Purpose |
|---------|---------|
| `/codex-verify [feature]` | Dispatch background Codex verification while Claude continues building |
| `/deep-review [feature] --codex` | Full review: Codex pre-review + 5+ Claude agents |
| `/review-codex` | Auto-detect changes, write adversarial prompt, dispatch, verify results |

**Track record:** 22 reviews completed, 51 bugs found — including issues Claude's own review missed (wrong field targeting, alignment mismatches, stale state bugs).

## For Developers

### Setup

```powershell
# Configure environment (sets BANNERLORD_GAME_DIR)
.\setup-dev-env.ps1

# Build
.\build.ps1

# Build + run tests
.\build.ps1 -RunTests

# Tests only
dotnet test TAOM.Tests
```

### Project Structure

```
TAOM/
├── Main/                          # Mod source (.NET Framework 4.7.2)
│   ├── Features/                  # Feature modules
│   │   ├── CareerSystem/          # 50 careers, 3 archetypes, choice trees, abilities
│   │   ├── SpecialResources/      # Per-kingdom resources (11 types, 18 kingdoms)
│   │   ├── CulturalFeats/         # 16 custom culture feats + 16 GameModel overrides
│   │   ├── AdvancedCombat/        # Spatial grid, bone collision, custom attacks
│   │   ├── Warg/                  # Behavior tree AI, rage mode
│   │   ├── Arena/                 # Tournament armor + prize pools
│   │   ├── Siege/                 # Timed siege defense events
│   │   ├── SettlementGuards/      # Per-settlement guard customization
│   │   ├── NamedCompanions/       # 18 lore companions as wanderers
│   │   ├── CustomBattles/         # TAOM factions in custom battle mode
│   │   └── ...                    # 30+ more feature modules
│   ├── Core/                      # Core infrastructure and IoC
│   ├── Adapters/                  # Sealed type adapters (IHeroAdapter, etc.)
│   └── _Module/                   # Bannerlord module files
│       ├── SubModule.xml          # Module manifest
│       ├── ModuleData/            # XML/XSLT/JSON configurations
│       └── GUI/                   # Sprite atlases and UI assets
├── TAOM.Tests/                    # Unit tests (MSTest + NSubstitute, 944 tests)
├── docs/
│   ├── adrs/                      # Architecture Decision Records
│   ├── features/                  # Feature documentation (40 files)
│   └── migration/                 # v1.2 -> v1.3 migration tracking
├── tools/                         # Rebalancing scripts (lords, troops, armor, weapons)
├── .claude/                       # Claude Code configuration
│   ├── skills/                    # 14 custom slash commands
│   ├── agents/                    # 2 specialized agents
│   ├── rules/                     # 10 scoped rule files
│   ├── hooks/                     # 12 automated triggers
│   └── memory/                    # Cross-session persistent memory
├── .codex/                        # Codex adversarial reviewer config
├── .vscode/                       # VS Code settings, extensions, MCP, tasks
├── .github/workflows/             # CI/CD (XML validation + build + test)
├── CLAUDE.md                      # AI instruction file (2,000+ lines)
├── AGENTS.md                      # Codex review instructions (541 lines)
├── Directory.Build.props          # Shared build properties
└── build.ps1                      # Build script
```

### Architecture

All mod logic follows this pattern:

```
[HarmonyPatch / GameModel / CampaignBehavior]
    -> IHookInterface
        -> Service (business logic)
            -> IAdapter (sealed type wrappers)
```

Services never touch TaleWorlds sealed types directly — they work through adapter interfaces. This keeps business logic fully unit-testable.

**Key architectural rules:**
- TDD mandatory (red-green-refactor)
- Entry points under 150 lines
- No `#region`, no `[Obsolete]`, no `#if DEBUG` (except IoC registration)
- Research TaleWorlds internals before implementing — never guess

See [Architecture Decision Records](docs/adrs/) for the full set of design constraints.

### CI/CD

GitHub Actions (`.github/workflows/build.yml`) runs three jobs on every push:

1. **Config check** — warns if `BANNERLORD_GAME_DIR` is not set
2. **XML validation** — well-formedness checks on all XML, XSLT, and JSON files
3. **Build + test** — `dotnet restore` -> `dotnet build` -> `dotnet test` (Windows runner)

### Documentation

| Resource | Path |
|----------|------|
| Feature documentation (40 files) | [docs/features/](docs/features/) |
| Architecture decisions | [docs/adrs/](docs/adrs/) |
| Migration tracking | [docs/migration/TRACKING.md](docs/migration/TRACKING.md) |
| AI instruction file | [CLAUDE.md](CLAUDE.md) |
| Codex review instructions | [AGENTS.md](AGENTS.md) |

## Contributing

1. Read [CLAUDE.md](CLAUDE.md) for coding standards and project conventions
2. Write tests first — TDD is mandatory for all new features
3. Use the adapter pattern for any TaleWorlds sealed types
4. Keep Harmony patches and entry points thin (< 150 lines), delegate to services
5. Research TaleWorlds behavior before implementing — use decompilation, not guesswork

## License

**Code** (C# mod source): [MIT License](https://opensource.org/licenses/MIT)

**Content** (art, lore, data, XML assets derived from Tolkien's works): [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/) — non-commercial, attribution required, share-alike.

This mod is a fan project and is not affiliated with or endorsed by the Tolkien Estate, New Line Cinema, or TaleWorlds Entertainment.

## Acknowledgments

- **[The Old Realms (TOR)](https://www.moddb.com/mods/the-old-realms)** — TAOM's Career System and Special Resources were inspired by TOR's Warhammer total conversion mod. Their career progression and resource-gating designs served as the reference architecture that was adapted for a Lord of the Rings setting.
