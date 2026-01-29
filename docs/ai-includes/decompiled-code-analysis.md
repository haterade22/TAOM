# Decompiled Code Analysis Framework

Guide for analyzing decompiled Bannerlord mod code and translating it into TAOM-compatible implementations.

---

## Decompilation Tools

```powershell
# Decompile a specific class
ilspycmd "%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.SomeClass"

# Decompile an entire DLL
ilspycmd "path\to\SomeMod.dll" -o "output\directory"

# Search decompiled output
ilspycmd "...\SomeDLL.dll" -t "ClassName" 2>&1 | Select-String -Pattern "MethodName"
```

**Key DLLs:**
- `TaleWorlds.CampaignSystem.dll` - Campaign logic, kingdoms, diplomacy
- `TaleWorlds.CampaignSystem.ViewModelCollection.dll` - UI ViewModels
- `TaleWorlds.Core.dll` - Core game types (FaceGen, Monster, etc.)
- `TaleWorlds.MountAndBlade.dll` - Battle/mission logic

**Harmony 2.4.2 API** (bundled in `Bannerlord.Harmony` module v2.4.2.225):
- Core: `PatchAll()`, `CreateProcessor()`, `CreateReversePatcher()`, `Patch()`, `UnpatchAll()`
- Attributes: `HarmonyPatch`, `HarmonyReversePatch`, `HarmonyPriority`, `HarmonyBefore`/`HarmonyAfter`, `HarmonyDebug`
- Utilities: `AccessTools`, `Traverse`, `CodeTranspiler`, `FastAccess`, `CodeMatcher`
- Bannerlord wrapper: `DebugUI` (in-game patch visualization)

---

## Analysis Workflow

### Phase 1: Initial Assessment

**Identify the feature:**
- What functionality does the decompiled code provide?
- What problem does it solve?
- What are the key user-facing behaviors?

**Classify the implementation type:**
- Harmony Patch (Prefix/Postfix/Transpiler/Finalizer)
- GameModel modification
- MissionLogic
- CampaignBehavior
- UI Extension
- XML-driven feature
- Hybrid approach

**Determine scope:**
- Single class or multi-component system?
- Dependencies on other mod systems?
- Requires XML data or purely code-based?

### Phase 2: Technical Deep Dive

**Architecture analysis:**
```
- Entry point(s):
- Core logic flow:
- Data structures used:
- Event hooks utilized:
- External dependencies:
```

**Integration points:**
```
- TaleWorlds APIs called:
- Game events subscribed to:
- Models extended/replaced:
- UI components modified:
- Persistence requirements:
```

**Critical implementation details:**
- Initialization sequence
- Timing considerations (when does it run?)
- Performance implications
- Thread safety concerns
- Edge cases handled

### Phase 3: TAOM Adaptation

**Namespace placement:**
```csharp
namespace TAOM.Features.[FeatureName]
{
    // Implementation here
}
```

**Map to TAOM architecture** (see [architecture.md](architecture.md)):
```
Decompiled code → TAOM layers:

Entry Point (Harmony Patch / GameModel / MissionLogic)
    → Hook Interface (IOn[EventName])
        → Service (business logic)
            → Adapter (wraps sealed TaleWorlds types)
```

**DryIoc registration:**
```csharp
// In feature-specific IoC file (e.g., HeroRaceIoC.cs)
container.Register<IFeatureService, FeatureService>(Reuse.Singleton);
container.Register<IOnSomeEvent, FeatureHook>(Reuse.Transient);
```

**Configuration** (if user-configurable):
```csharp
// JSON-based configuration (TAOM pattern)
var config = RacePositionConfig.LoadConfig("FeatureName");
```

### Phase 4: Implementation Plan

**Step-by-step integration:**

1. **Create feature folder structure:**
```
Main/Features/[FeatureName]/
├── Hooks/
│   ├── [TargetClass]_[Method]_Patch.cs
│   └── IOn[EventName].cs
├── Services/
│   ├── I[Feature]Service.cs
│   └── [Feature]Service.cs
├── Configuration/
│   └── [Feature]Config.cs (if needed)
└── [Feature]IoC.cs
```

2. **Write tests first** (TDD mandatory - see [tdd-enforcement.md](tdd-enforcement.md)):
```
TAOM.Tests/Features/[FeatureName]/
├── [Feature]ServiceTests.cs
└── [Feature]HookTests.cs
```

3. **Implement Harmony patches** (thin entry points, ADR-002):
```csharp
[HarmonyPatch(typeof(TargetClass), "TargetMethod")]
public class TargetClass_TargetMethod_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ref float __result, SealedType __instance)
    {
        try
        {
            var service = IoC.Resolve<IFeatureService>();
            service.HandleEvent(__instance, ref __result);
        }
        catch (Exception) { }
    }
}
```

4. **Register in IoC** (feature-specific registration file)

### Phase 5: Risk Assessment

**Potential issues:**
- Compatibility with existing TAOM features
- Performance for large-scale battles/campaigns
- Save game compatibility
- Known Bannerlord version dependencies (target: v1.3.12)

**Mitigation:**
- Graceful degradation with try-catch in patches
- Feature toggles via JSON configuration
- Logging via `IModLogger` for debugging

---

## Output Format

When presenting analysis results, use this structure:

### Executive Summary
```
Feature: [Name]
Purpose: [One sentence]
Complexity: [Low/Medium/High]
Implementation Type: [Harmony Patch / GameModel / etc.]
Dependencies: [List]
```

### Detailed Analysis

**1. What It Does** - Clear explanation of functionality

**2. How It Works** - Technical explanation with code flow

**3. Key Code Patterns**
```csharp
// Highlight important patterns with complete examples
```

**4. TAOM Implementation** - Complete, copy-paste ready code following TAOM conventions

**5. Integration Checklist**
- [ ] Write tests (RED phase)
- [ ] Create feature files
- [ ] Register in IoC
- [ ] Add JSON config (if configurable)
- [ ] Create/modify XML data (if needed)
- [ ] Make tests pass (GREEN phase)
- [ ] Refactor if needed
- [ ] Build succeeds (`./build.ps1`)
- [ ] All tests pass (`./build.ps1 -RunTests`)
- [ ] Test in-game

---

## TAOM-Specific Considerations

### Faction Structure

TAOM has 13 kingdoms total:
- **Good factions (4)**: Erebor, Rivendell, Mirkwood, Lothlorien
- **Evil factions (4)**: Isengard, Gundabad, Dolguldur, Umbar
- **Transformed vanilla (5)**: Gondor, Rohan, Mordor, Harad, Rhun, Dale, Dunland, Khand

Alignment is implicit through kingdom relationships in XML, not explicit code data.

### Race System

TAOM adds custom races (Human, Dwarf, Elf, Orc, Uruk-hai, Hobbit) via:
- `RaceManager` - Maps race names to IDs
- `FaceGen_GetBaseMonsterFromRace_Patch` - Custom monster/race resolution
- `races/monsters.xml` - Monster type definitions

### Logging Pattern

```csharp
// Use IModLogger, NOT Debug.Print
private readonly IModLogger _logger;

_logger.LogInfo("Feature initialized");
_logger.LogWarning($"Unexpected state: {value}");
_logger.LogError($"Failed to process: {ex.Message}");
```

### Architecture Rules (Critical)

| Rule | Details |
|------|---------|
| **TDD Mandatory** | RED -> GREEN -> REFACTOR |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **No business logic in patches** | Patches delegate to hooks/services |
| **No `#region`** | Use class decomposition (ADR-003) |
| **Research First** | Decompile TaleWorlds code before implementing |

### Pattern Matching

```csharp
// Match existing TAOM patterns:
// - Harmony patch naming: TargetClass_TargetMethod_Patch
// - Hook interfaces: IOn[EventName]
// - Service injection: IoC.Resolve<IService>()
// - Adapter wrapping: _factory.GetAdapter(sealedType)
// - InformationManager for user-visible messages
// - IModLogger for debug/error logging
```

---

## Questions to Always Answer

1. What game events/hooks does this use?
2. Does it require new XML definitions?
3. Are there performance implications?
4. How does it interact with TaleWorlds' existing systems?
5. What could break if Bannerlord updates?
6. How can we test this feature?
7. Does it fit TAOM's architecture (patches -> hooks -> services -> adapters)?

---

## Related Guides

- [architecture.md](architecture.md) - TAOM architecture overview
- [patterns.md](patterns.md) - Design patterns (Hook, Strategy, Builder)
- [tdd-enforcement.md](tdd-enforcement.md) - Test-driven development workflow
- [taleworlds-research-guide.md](taleworlds-research-guide.md) - TaleWorlds decompilation workflow
