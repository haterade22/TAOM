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
| **TDD Mandatory** | RED → GREEN → REFACTOR. Test first, always. |
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior - read decompiled source |

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
| Research unknown behavior | [research-workflow.md](./docs/ai-includes/research-workflow.md) |
| Ensure code quality | [code-quality.md](./docs/ai-includes/code-quality.md) |
| Secure sensitive data | [security.md](./docs/ai-includes/security.md) |
| Check migration status | [migration/TRACKING.md](./docs/migration/TRACKING.md) |
| v1.3 API changes | [migration/](./docs/migration/) |
| Use agent teams for parallel work | [agent-teams.md](./docs/ai-includes/agent-teams.md) |

## Key Paths

| Component | Path | Framework |
|-----------|------|-----------|
| Mod code | `Main/` | .NET Framework 4.7.2 |
| Mod tests | `TAOM.Tests/` | MSTest + NSubstitute |
| Features | `Main/Features/` | Feature modules |
| Adapters | `Main/Adapters/` | Wraps sealed types |
| Core | `Main/Core/` | Core infrastructure |
| GameModels | `Main/Features/TroopProgression/Models/` | Troop tier, wage, volunteer overrides |
| FactionMap | `Main/Features/FactionMap/` | Interactive faction selection map |
| FactionMap data | `Main/_Module/ModuleData/factionmap/` | regions.json + factions.json |
| FactionMap sprites | `Main/_Module/GUI/SpriteData/FactionMap/` | Map PNG assets (111 files) |
| XML config | `Main/_Module/ModuleData/` | Game configuration |
| XSLT files | `Main/_Module/ModuleData/*.xslt` | Vanilla XML transformations |
| **TaleWorlds DLLs** | `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client` | Decompile on demand |

## XSLT Transformations

TAOM uses XSLT to transform vanilla Bannerlord XML at load time. **Reference format: `SandBoxCore/ModuleData/` is the authoritative source** for vanilla XML structure (NOT `SandBox/ModuleData/`). For example, `SandBoxCore` uses `<notable_templates>` (which the engine reads), while `SandBox` uses `<notable_and_wanderer_templates>` (ignored by engine).

XSLT pattern: copy all vanilla attributes/elements with `<xsl:apply-templates select="@*"/>` and `<xsl:apply-templates select="*[not(...)]"/>`, then override only what we change. Never filter out vanilla attributes — critical ones like `is_main_culture`, `can_have_settlement`, `faction_banner_key` will be silently dropped.

| File | Purpose |
|------|---------|
| `spkingdoms.xslt` | Kingdom names (8) |
| `spcultures.xslt` | Culture overrides for 6 XSLT cultures — names, troops, policies, names lists |
| `spclans.xslt` | Clan names (73) |
| `lords.xslt` | Lords - name, default_group, is_female, BodyProperties, skills, traits (380) |
| `heroes.xslt` | Hero biographies (415) |
| `module_strings.xslt` | Remove vanilla faction strings for 6 remapped cultures |

## Additional XML Files

New entities not in vanilla Bannerlord are added via direct XML (not XSLT):

| File | Purpose |
|------|---------|
| `characters/lords.xml` | New LOTRAOM lords not in vanilla (504) - staged |
| `characters/heroes.xml` | New LOTRAOM heroes not in vanilla - staged |
| `characters/clans.xml` | New LOTRAOM clans not in vanilla (~101) - staged |
| `characters/npcs_{culture}.xml` | Culture-specific NPCs: notables (26 per culture), wanderers, troops, etc. |
| `taom_spcultures.xml` | 10 custom cultures (Erebor, Rivendell, Mirkwood, Lothlorien, Isengard, Gundabad, Umbar, Dol Guldur, Gondor, Mordor) |
| `taom_module_strings.xml` | Faction/culture strings for all 16 cultures (192 strings) |

## TaleWorlds Research Protocol (CRITICAL)

When debugging crashes, fixing bugs, or implementing features that interact with TaleWorlds code:

### 1. After Initial Analysis - Identify Information Gaps
Before proposing a fix, explicitly ask yourself:
- What assumptions am I making about TaleWorlds behavior?
- What code paths haven't I verified?
- Could there be edge cases I'm missing?

### 2. Decompile TaleWorlds Code to Verify
Use `ilspycmd` to decompile relevant TaleWorlds classes and verify your understanding:

```powershell
# Example: Decompile a specific class
ilspycmd "%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.SomeClass"

# Example: Search for patterns in decompiled output
ilspycmd "...\TaleWorlds.CampaignSystem.dll" -t "ClassName" 2>&1 | Select-String -Pattern "MethodName"
```

**Key DLLs to decompile:**
- `TaleWorlds.CampaignSystem.dll` - Campaign logic, kingdom decisions, diplomacy
- `TaleWorlds.CampaignSystem.ViewModelCollection.dll` - UI ViewModels that crash
- `TaleWorlds.Core.dll` - Core game types
- `TaleWorlds.MountAndBlade.dll` - Battle/mission logic

### 3. Validate Plan Against Decompiled Code
Before implementing, verify:
- Method signatures match what you expect
- Event timing (when events fire vs when state changes)
- Null handling expectations (does TaleWorlds expect `TextObject.Empty` vs `null`?)
- Collection modification safety (iterate over `.ToList()` copy)

## Agent Teams

Use agent teams when work can be parallelized across independent directories. See [agent-teams.md](./docs/ai-includes/agent-teams.md) for the full guide.

**When to use:** parallel feature work (independent `Features/` dirs), research + implementation, C# + XML/XSLT, multi-aspect code review, large refactors across multiple features.

**When NOT to use:** single-file changes, sequential tasks, multiple agents editing the same file, trivial fixes.

**Rules:**
- All Critical Rules (TDD, adapters, research-first) apply to **every teammate**
- `IoC.cs` and `SubModule.cs` are single-owner files — lead integrates last
- Never run `./build.ps1` from two agents simultaneously
- Windows uses in-process mode (CLI flag: `--teammate-mode in-process`)

## GameModel Overrides

TAOM overrides vanilla GameModels via `CampaignGameStarter.AddModel()` in `SubModule.OnGameStart`. Last registered model wins. Models are thin entry points that delegate to IoC-resolved services.

| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10), level-bracket recruitment costs, `MaxWagePaymentLimit => 20000` |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) |

## Architecture (One-liner)

**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` → `IHookInterface` → `Service` → `IAdapter` (sealed types)

## Documentation Requirements (MANDATORY)

After every change session, update ALL relevant documentation before finishing:

| Doc | When to update | Path |
|-----|---------------|------|
| **CHANGELOG.md** | Every session — summarize all changes made | `CHANGELOG.md` |
| **CLAUDE.md** | When adding new files, paths, patterns, or rules | `CLAUDE.md` |
| **ADRs** | When making architectural decisions | `docs/adrs/` |
| **Migration tracking** | When completing migration tasks | `docs/migration/TRACKING.md` |

**CHANGELOG rules:**
- Group by date, then by category (features, bug fixes, tooling, etc.)
- Include file names and counts where relevant
- Keep entries concise but specific enough to understand the change
- Most recent date at top

## Commits

50/72 rule. No AI attribution. Example: `feat: add garrison patrol calculation`

## Notes

- Target: Bannerlord v1.3.12
- Migration from v1.2 requires API changes - see `docs/migration/`
- No git actions unless explicitly asked
