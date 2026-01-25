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

## Key Paths

| Component | Path | Framework |
|-----------|------|-----------|
| Mod code | `Main/` | .NET Framework 4.7.2 |
| Mod tests | `TAOM.Tests/` | MSTest + NSubstitute |
| Features | `Main/Features/` | Feature modules |
| Adapters | `Main/Adapters/` | Wraps sealed types |
| Core | `Main/Core/` | Core infrastructure |
| XML config | `Main/_Module/ModuleData/` | Game configuration |
| XSLT files | `Main/_Module/ModuleData/*.xslt` | Vanilla XML transformations |
| **TaleWorlds DLLs** | `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client` | Decompile on demand |

## XSLT Transformations

TAOM uses XSLT to transform vanilla Bannerlord XML at load time:

| File | Purpose |
|------|---------|
| `spkingdoms.xslt` | Kingdom names (8) |
| `spcultures.xslt` | Culture names (6) |
| `spclans.xslt` | Clan names (73) |
| `splords.xslt` | Lord names (~350) |
| `spheroes.xslt` | Hero biographies (415) |

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

## Architecture (One-liner)

**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` → `IHookInterface` → `Service` → `IAdapter` (sealed types)

## Commits

50/72 rule. No AI attribution. Example: `feat: add garrison patrol calculation`

## Notes

- Target: Bannerlord v1.3.12
- Migration from v1.2 requires API changes - see `docs/migration/`
- No git actions unless explicitly asked
