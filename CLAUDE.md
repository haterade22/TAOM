# CLAUDE.md

Bannerlord 1.3 total conversion mod (TAOM - Tales From the Age of Men)

## Commands

| Task | Command |
|------|---------|
| Build mod | `./build.ps1` |
| Build + test | `./build.ps1 -RunTests` |
| Run tests | `dotnet test TAOM.Tests` |

## Critical Rules (NEVER VIOLATE)

| Rule | Details |
|------|---------|
| **TDD Mandatory** | RED -> GREEN -> REFACTOR. Test first, always. |
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior - use `/research` skill or read decompiled source |
| **`/deep-review` Mandatory** | Run before EVERY commit touching C# — catches adapter violations, v1.3 incompatibilities, missing tests |

## Skills (Slash Commands)

| Command | Purpose |
|---------|---------|
| `/research [Class]` | Decompile and analyze TaleWorlds classes |
| `/new-feature [Name]` | Scaffold a new feature module with IoC, services, tests |
| `/xslt-check [file]` | Validate XSLT against SandBoxCore vanilla XML |
| `/migration-status` | Check v1.2 -> v1.3 migration progress |
| `/scope-check [change]` | Assess whether a proposed change fits current work context |
| `/build-fix [error]` | Incrementally fix dotnet build errors, one at a time, minimal diffs |
| `/verify [quick\|full]` | Run build + test + git status and produce pass/fail report |
| `/deslop [path]` | Regression-safe C# AI-slop cleanup: deletion-first, tests-first |
| `/new-adr [name]` | Scaffold an auto-numbered ADR with context pre-filled from git log + CHANGELOG |
| `/commit-split` | Group changed files by concern and commit each group atomically |
| `/deep-review [feature]` | Launch 4 parallel agents: standards, Bannerlord 1.3 compat, efficiency, completeness |

## Scoped Rules (auto-loaded by file path)

| Rule | Scope | Content |
|------|-------|---------|
| `xslt.md` | `**/*.xslt` | XSLT passthrough, SandBoxCore reference |
| `adapters.md` | `Main/Adapters/**` | Adapter pattern, research-first |
| `tests.md` | `TAOM.Tests/**` | TDD, naming, AAA pattern, coverage |
| `xml-data.md` | `ModuleData/**/*.xml` | NPC naming, region codes, formatting |
| `troops.md` | `troops/**`, `taom_partyTemplates.xml`, `TroopProgression/**` | Troop checklist, races, party templates, save compat |
| `harmony-patches.md` | `Main/**/Hooks/**` | Patch types, thin entry points, thread-local state |

## Custom Agents

| Agent | Purpose |
|-------|---------|
| `taleworlds-researcher` | Decompile and analyze TaleWorlds DLLs |
| `feature-builder` | Build features following TAOM architecture |

## Model Routing

| Task | Model | Why |
|------|-------|-----|
| Architecture decisions, complex design | **Opus** | Deepest reasoning for trade-off analysis |
| Feature implementation, code review | **Sonnet** | Best coding model, fast enough for iteration |
| Lightweight research, documentation, exploration | **Haiku** | 90% of Sonnet capability at 3x cost savings |
| Explore agents (codebase search) | **Haiku** | Read-only search doesn't need full reasoning |
| Plan agents (design work) | **Sonnet** | Needs coding awareness for implementation plans |

> **Haiku 3 deprecation:** April 19, 2026. The `haiku` alias already maps to `claude-haiku-4-5` — no action needed.

## Doc Lookup

| Need to... | Read |
|------------|------|
| Write tests / TDD workflow | [tdd-enforcement.md](./docs/ai-includes/tdd-enforcement.md) |
| Research TaleWorlds mechanics | [taleworlds-research-guide.md](./docs/ai-includes/taleworlds-research-guide.md) |
| Debug / iterate on problem | [iterative-problem-solving.md](./docs/ai-includes/iterative-problem-solving.md) |
| Compare multiple approaches | [multi-approach-validation.md](./docs/ai-includes/multi-approach-validation.md) |
| Understand architecture | [architecture.md](./docs/ai-includes/architecture.md) |
| Check design patterns | [patterns.md](./docs/ai-includes/patterns.md) |
| Check ADR rules | [docs/adrs/](./docs/adrs/README.md) |
| Ensure code quality | [code-quality.md](./docs/ai-includes/code-quality.md) |
| Check migration status | [migration/TRACKING.md](./docs/migration/TRACKING.md) |
| Use agent teams | [agent-teams.md](./docs/ai-includes/agent-teams.md) |

## Key Paths

| Component | Path |
|-----------|------|
| Mod code | `Main/` (.NET Framework 4.7.2) |
| Mod tests | `TAOM.Tests/` (MSTest + NSubstitute) |
| Features | `Main/Features/` |
| Adapters | `Main/Adapters/` |
| Core | `Main/Core/` |
| CharacterCreation | `Main/Features/CharacterCreation/` |
| AtmospherePersistence | `Main/Features/AtmospherePersistence/` |
| AdvancedCombat | `Main/Features/AdvancedCombat/` (SpatialGrid, BoneCollision, CustomAttacks) |
| CulturalFeats | `Main/Features/CulturalFeats/` (TaomCulturalFeats, 16 GameModel overrides) |
| CustomBattles | `Main/Features/CustomBattles/` (Custom battle factions, commanders, troops) |
| Arena | `Main/Features/Arena/` (TaomTournamentModel — per-participant culture armor) |
| Warg Combat | `Main/Features/Warg/` (BT elements, WargAttackService, WargMissionBehavior) |
| BT DLLs | `Main/_Module/bin/Win64_Shipping_Client/BehaviorTrees.dll`, `BehaviorTreeWrapper.dll` |
| Alliance.Wargs | External module: Monster id="warg", animations, items |
| CC narrative data | `Main/_Module/ModuleData/charactercreation/` (JSON) |
| XML config | `Main/_Module/ModuleData/` |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| Custom lords XML | `Main/_Module/ModuleData/characters/lords.xml` |
| StartupResources config | `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` |
| TaleWorlds DLLs | `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client` |
| CI/CD | `.github/workflows/build.yml` |
| Shared build props | `Directory.Build.props` |
| Skills | `.claude/skills/` |
| Rules | `.claude/rules/` |
| Agents | `.claude/agents/` |

## Architecture (One-liner)

**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)

## GameModel Overrides

| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10) + culture wage/garrison/Rohan mounted feats |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | Culture army influence award/cost feats |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | Culture forest speed + Rohan infantry speed feats |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | Culture production feats |
| `TaomCaravanModel` | `DefaultCaravanModel` | Umbar caravan cost feat |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | Umbar renown feat |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | Mounted recruit cost feats (Isengard, Rohan) |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats (Mordor, Gundabad, DG, Isengard, Gondor) |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats (elves, Dol Guldur) |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats (Gondor, Erebor, elves, Rohan) |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | Party morale feats (Gondor, Rohan, Erebor, elves) |
| `TaomSmithingModel` | `DefaultSmithingModel` | Smithing energy cost feats (Erebor, Isengard) |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | Tariff income feat (Umbar) |
| `TaomRaidModel` | `DefaultRaidModel` | Raid damage feats (Mordor, Gundabad, Isengard) |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | Configurable T7-T10 troop power (MCM + JSON) |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | Configurable blunt/cut damage ratio per battle type (MCM) |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | Cultural survival bonuses (JSON per-faction death chance multiplier) |
| `TaomTournamentModel` | `DefaultTournamentModel` | Per-participant culture armor — prevents cross-skeleton equipment glitches in tournaments |

## Harmony Patch Categories

| Category | Feature | Target |
|----------|---------|--------|
| `Patch0_BattleScenes` | Battle scenes (DISABLED) | `Campaign.InitializeScenes` |
| `Patch1_FirstTimeInit` | First-time initialization | Various |
| `Patch2_RefreshTableau` | Banner tableau refresh | Various |
| `Patch3_SetRace` | Race assignment | Various |
| `Patch4_CharacterSpawner` | Character spawning | Various |
| `Patch5_FaceGen` | Face generation | Various |
| `Patch6_BannerEditor` | Banner editor | Various |
| `Patch7_FactionMap` | Faction map | Various |
| `Patch8_SiegeCampGuard` | Siege camp guard | Various |
| `Patch9_RaceFilter` | Race filter | Various |
| `Patch10_WeatherBoundsGuard` | Weather bounds clamping | `DefaultMapWeatherModel` |
| `Patch11_Diplomacy` | Diplomacy system | Various |
| `Patch12_WarOfTheRing` | War of the Ring | Various |
| `Patch14_Execution` | Execution system | Various |
| `Patch15_BannerLayerLimit` | Banner layer limit | Various |
| `Patch16_AtmospherePersistence` | Forced-atmosphere scenes | `Mission.Initialize` |
| `Patch17_TroopWeight` | Troop weight system | `PartyBase`, `TroopRoster` |
| `Patch18_CulturalFeats` | Custom culture feat registration | `Campaign.InitializeDefaultCampaignObjects` |
| `Patch19_CustomBattles` | Custom battle TAOM factions/commanders/troops | `CustomBattleData`, `CustomBattleHelper`, `BannerlordMissions` |
| `Patch20_NarrativeHorseGuard` | Suppress CC narrative horse crashes for no-mount cultures | `CharacterCreationCampaignBehavior`, `CharacterCreationNarrativeStageView` |

## Agent Teams

Use when work can be parallelized. See [agent-teams.md](./docs/ai-includes/agent-teams.md).

**Rules:** All Critical Rules apply to every teammate. `IoC.cs`/`SubModule.cs` are single-owner. Never run `./build.ps1` from two agents simultaneously.

## Documentation Requirements (MANDATORY)

| Doc | When to update | Path |
|-----|---------------|------|
| **CHANGELOG.md** | Every session | `CHANGELOG.md` |
| **CLAUDE.md** | New files, paths, patterns | `CLAUDE.md` |
| **ADRs** | Architectural decisions | `docs/adrs/` |
| **Migration tracking** | Migration tasks | `docs/migration/TRACKING.md` |
| **GitHub Issues** | Every feature, bug, crash, system fix | `gh issue create/close` |
| **Feature docs** | Every completed feature | `docs/features/<name>.md` |

## GitHub Issue & Knowledge Base Requirements (MANDATORY)

### GitHub Issues — Create for ALL Work

Every feature, bug fix, crash fix, or system change MUST have a GitHub issue. No exceptions.

**When to create:**
- Starting a new feature → create issue BEFORE implementation
- Fixing a bug/crash → create issue documenting the problem FIRST
- Completing a fix that was done without an issue → create issue retroactively with full details

**Issue content — be exhaustive:**

For **bug/crash fixes**, the issue body MUST include:
1. **Problem** — exact error message, stack trace, reproduction steps
2. **Analysis** — root cause investigation, what was examined, why it happened
3. **Solution** — what was changed and WHY that approach was chosen
4. **Files changed** — list of modified files with one-line descriptions
5. **Testing** — how the fix was verified

For **features**, the issue body MUST include:
1. **Motivation** — why this feature exists, what problem it solves
2. **Design** — architecture decisions, alternatives considered
3. **Implementation** — key files, patterns used, configuration
4. **Testing** — test coverage, how to verify it works

**Lifecycle:**
- Label issues appropriately (`bug`, `feature`, `crash`, `enhancement`)
- Reference the issue number in commits when possible
- **Close the issue** with `gh issue close` when the work is complete and verified

**Commands:** Use `gh issue create` and `gh issue close` via Bash.

### Feature Documentation — `docs/features/`

Every completed feature MUST have a documentation file at `docs/features/<feature-name>.md`. This is the **knowledge base** that prevents future sessions from re-analyzing solved problems.

**Use template:** `docs/features/TEMPLATE.md`

**Sections required:**
- Overview — what it does in 2-3 sentences
- Why This Exists — the problem it solves, with specific examples
- Architecture — design challenge, solution approach, component diagram
- Configuration — config files, data formats, current values
- Key Files — table of all files with their purpose
- Dependencies — what it relies on
- Tests — test file locations and coverage summary
- How-To — common operations (e.g., "How to add a new X")
- Performance — any optimization notes (if applicable)

**Existing examples:** `docs/features/race-age-system.md`, `docs/features/offspring-race-inheritance.md`

**Rule:** If a future session needs to understand a feature, the doc should contain enough detail that ZERO decompilation, code reading, or re-analysis is needed for the conceptual understanding. Code reading is only for the current state of the implementation.

### Completion Workflow (MANDATORY)

Before closing out any feature or fix, run this sequence:

1. `/verify` — build + tests pass
2. `/deep-review [feature-name]` — launches 4 parallel agents (standards, Bannerlord 1.3 compat, efficiency, completeness)
3. Fix any issues flagged by the review
4. Create/close GitHub issue with full details
5. Write/update `docs/features/<name>.md`
6. Update `CHANGELOG.md`

**Do not skip `/deep-review`.** It catches adapter pattern violations, v1.3 API incompatibilities, performance issues, and missing docs/tests before they become bugs.

## Commits

50/72 rule. No AI attribution. Example: `feat: add garrison patrol calculation`

**Optional trailers** (add when relevant — each on its own line after the blank line):

| Trailer | When to use | Example |
|---------|------------|---------|
| `Constraint:` | TaleWorlds limitation blocked the ideal solution | `Constraint: Hero is sealed, can't subclass` |
| `Rejected:` | Alternative approach considered and dropped | `Rejected: Prefix patch — fires too early before state init` |
| `Not-tested:` | Parts that can't be unit tested | `Not-tested: Harmony patch invocation (requires live game)` |
| `Research:` | What was decompiled to inform this change | `Research: DefaultPartyWageModel.GetCharacterWage` |
| `Save-compat:` | Save file impact | `Save-compat: New field — safe, defaults to 0 on load` |

## MCP Servers

| Server | Scope | Purpose | Config |
|--------|-------|---------|--------|
| **Serena** | Project | Symbolic code navigation (C# classes, methods, references) | Global |
| **GitHub** | Project | PRs, issues, actions, code search | Global |
| **sequential-thinking** | Global | Extended reasoning for complex design decisions | Global |
| **context7** | Global | Library documentation lookup | Global |
| **filesystem** | Project | File operations across TAOM, Bannerlord Modules, LOTRAOM assets | `.vscode/mcp.json` |
| **git** | Project | Rich git operations (diff, blame, log, branch management) | `.vscode/mcp.json` |
| **ilspy** | Project | Decompile TaleWorlds DLLs — use for `/research` and adapter work | `.vscode/mcp.json` |

### MCP Usage Guide

| Task | Use This MCP | Instead Of |
|------|-------------|------------|
| Navigate C# symbols, find references | **Serena** (`find_symbol`, `get_symbols_overview`) | Grep for class names |
| Decompile TaleWorlds classes | **ilspy** (`decompile_type`, `list_types`) | `ilspycmd` via Bash |
| Read files across Bannerlord modules | **filesystem** (`read_file`, `search_files`) | Bash `cat` on long paths |
| Git blame, diff analysis | **git** (`git_blame`, `git_diff`) | `git` via Bash |
| Create/close GitHub issues | **GitHub** | `gh` via Bash |
| Research before implementing | **ilspy** + **Serena** together | Manual decompilation workflow |

### ILSpy MCP for TaleWorlds Research

The `ilspy` MCP server wraps `ilspycmd` and provides direct decompilation tools. Use it instead of manual `ilspycmd` bash commands when researching TaleWorlds internals:

```
# These MCP tools replace manual ilspycmd usage:
mcp__ilspy__decompile_type  — Decompile a specific class/type from a DLL
mcp__ilspy__list_types      — List all types in an assembly
```

**Accessible DLL path**: `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

### Configuration

Project-level MCP servers are configured in `.vscode/mcp.json`. Global servers (Serena, GitHub, sequential-thinking, context7) are configured in VS Code extension settings.

## Hooks

| Hook | Event | Purpose |
|------|-------|---------|
| `check-build-before-commit.sh` | PreToolUse (Bash) | Blocks `git commit` if build fails |
| `notify-csharp-edit.sh` | PostToolUse (Edit\|Write) | Logs C# file modifications |
| `check-changelog-updated.sh` | Stop | Reminds to update CHANGELOG.md |
| `session-start.sh` | SessionStart | Prints branch, recent commits, CHANGELOG summary on startup |
| `pre-compact.sh` | PreCompact | Dumps modified files list before context compaction |
| `log-agent.sh` | SubagentStart | Audit logs agent invocations to `.claude/logs/agent-audit.log` |
| `config-protection.sh` | PreToolUse (Edit\|Write) | Blocks edits to CLAUDE.md, Directory.Build.props, ADRs without explicit request |
| `suggest-compact.sh` | PreToolUse (*) | Suggests `/compact` after 50 tool calls, then every 25 |
| `mcp-health-check.sh` | PreToolUse (mcp__*) | Blocks MCP calls to servers marked unhealthy in last 60s |
| `mcp-health-mark.sh` | PostToolUseFailure (mcp__*) | Marks MCP server unhealthy after failed tool call, 60s backoff |
| `check-deep-review.sh` | Stop | Reminds to run `/deep-review` if real work was done |

## Notes

- Target: Bannerlord v1.3.12
- Migration from v1.2 requires API changes - see `docs/migration/`
- No git actions unless explicitly asked
- **Claude Code v2.1.63** — update to v2.1.80+ for: effort frontmatter on skills, PowerShell tool, background agent fixes, 64k/128k output tokens

## PowerShell Tool (Windows)

Opt-in preview (requires v2.1.78+). Runs PowerShell natively instead of routing through Git Bash.

**Enable:** Add to `settings.json` env block:
```json
"CLAUDE_CODE_USE_POWERSHELL_TOOL": "1"
```

**Additional settings:**
| Setting | Location | Effect |
|---------|----------|--------|
| `"defaultShell": "powershell"` | `settings.json` | Routes `!` commands through PowerShell |
| `"shell": "powershell"` | Hook definition | Runs that hook in PowerShell |
| `shell: powershell` | Skill frontmatter | Runs code blocks in PowerShell |

**Limitations:** No auto mode, no profile loading, no sandboxing, Windows-only (not WSL), Git Bash still required to start Claude Code.

## Equipment & Armory

| Item | Details |
|------|---------|
| **Armory dependency** | `LOTRLOME_Armory` (NOT `Armory_2` — it will be deleted) |
| **Item definitions** | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<culture>\` |
| **Item files per culture** | `body_armors.xml`, `head_armors.xml`, `leg_armors.xml`, `shoulder_armors.xml`, `arm_armors.xml` |
| **Global items** | `LOTRLOME_items\LOTRAOM_weapons.xml`, `LOTRAOM_shields.xml`, `LOTRAOM_horses.xml` |
| **Gondor prefix** | `sk_gd_ano_` (Anorien), `sk_gd_mns_` (Minas Tirith), `sk_gd_osg_` (Osgiliath), `sk_gd_cair_` (Cair Andros), `sk_gd_ith_` (Ithilien) |

**Validation:** When adding/changing equipment, always verify item IDs exist in Armory. Characters appear in underwear when items are missing. Cross-reference with `grep -o 'id="[^"]*"' <armory-file>` to get valid IDs.

## Rebalancing Tools

| Tool | Purpose | CLI |
|------|---------|-----|
| `tools/complete_lords_xslt.py` | Make all vanilla lord attributes explicit in XSLT | `--dry-run`, `--apply`, `--export-csv` |
| `tools/rebalance_lords.py` | Balance lord skills (XSLT + XML) via baseline + cultural mod + age | `--dry-run`, `--apply`, `--export-csv` |
| `tools/rebalance_troops.py` | Balance troop skills | `--dry-run`, `--apply` |
| `tools/rebalance_armor.py` | Balance armor stats | `--dry-run`, `--apply` |
| `tools/rebalance_weapons.py` | Balance weapon stats | `--dry-run`, `--apply` |
