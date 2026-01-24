# ADR-008: Service Testability Requirements

## Status
**Accepted** - Mandatory for all services (2025-01-22)

## Context
Services must be 100% unit testable without requiring game framework initialization. This ADR defines explicit rules preventing services from using TaleWorlds static methods that make unit testing impossible.

## Decision

### Rule 1: No Static TaleWorlds Calls in Services

Services MUST NOT call static methods or properties from TaleWorlds game framework:

**FORBIDDEN** (causes test failures):
```csharp
public class MyService
{
    public void DoSomething()
    {
        // WRONG - static calls require game initialization
        var time = CampaignTime.Now;                    // NullReferenceException in tests
        var basePath = Utilities.GetBasePath();          // NullReferenceException in tests
        var player = Hero.MainHero;                      // NullReferenceException in tests
        var encounter = PlayerEncounter.Current;         // NullReferenceException in tests
        var campaign = Campaign.Current;                 // NullReferenceException in tests
    }
}
```

**CORRECT** (testable with dependency injection):
```csharp
public class MyService
{
    private readonly ICampaignTimeProvider _timeProvider;
    private readonly IModulePathProvider _pathProvider;

    public MyService(ICampaignTimeProvider timeProvider, IModulePathProvider pathProvider)
    {
        _timeProvider = timeProvider;
        _pathProvider = pathProvider;
    }

    public void DoSomething()
    {
        var time = _timeProvider.Now;                    // Mockable in tests
        var basePath = _pathProvider.GetModulePath("TAOM"); // Mockable in tests
    }
}
```

### Rule 2: All New Abstractions Must Be Fully Integrated

When creating a new abstraction (interface + implementation):

1. **Create the interface and implementation** (e.g., `ICampaignTimeProvider`)
2. **Register in IoC IMMEDIATELY**
3. **Refactor ALL existing consumers** - not just new code
4. **Update ALL test files** with mocked providers
5. **Run full test suite** - must pass 100%

**INCOMPLETE** (causes test failures):
```
- Created ICampaignTimeProvider interface
- Created CampaignTimeProvider implementation
- Registered in IoC
- Updated only 2 of 5 services to use it
- Left MomentumEventService using CampaignTime.Now
- Left CasualtyCalculationService using CampaignTime.Now
- Result: 50+ tests failing
```

**COMPLETE** (proper integration):
```
- Created ICampaignTimeProvider interface
- Created CampaignTimeProvider implementation
- Registered in IoC
- Updated ALL 5 services to use provider
- Updated ALL test files with mocks
- Ran full test suite - 100% passing
```

### Rule 3: Required Provider Interfaces

All services MUST use these providers instead of static calls:

| Static Call | Provider Interface | Registration Location |
|-------------|-------------------|----------------------|
| `CampaignTime.Now`, `.YearsFromNow()`, etc. | `ICampaignTimeProvider` | `Main/IoC.cs` |
| `Utilities.GetBasePath()` | `IModulePathProvider` | `Main/IoC.cs` |
| `Hero.MainHero` | `IPlayerHeroProvider` | (Create if needed) |
| `Campaign.Current` | `ICampaignProvider` | (Create if needed) |
| `PlayerEncounter.Current` | Wrap in try-catch + early return | N/A (optional property) |

### Rule 4: Test Verification Checklist

Before marking any abstraction task as complete:

- [ ] Interface created in `Main/Adapters/` or `Main/Core/Services/`
- [ ] Implementation created
- [ ] Registered in `Main/IoC.cs` or feature-specific IoC
- [ ] ALL services using old static calls refactored
- [ ] ALL test Setup() methods inject mocked provider
- [ ] Full test suite run: `dotnet test TAOM.Tests`
- [ ] **Zero test failures** - not "most tests pass"
- [ ] Coverage maintained or improved

### Rule 5: Constructor Dependency Rule

When adding a new provider to a service constructor:

1. **Update the service constructor signature**
2. **Update IoC registration** (if constructor changed)
3. **Update ALL test files** that instantiate the service
4. **Search codebase** for `new MyService(` to find manual instantiations

Example:
```bash
# Find all places that might need updating
git grep -n "new MomentumEventService("
git grep -n "Substitute.For<IMomentumEventService>"
```

## Consequences

### Positive
- Services are 100% unit testable without game framework
- Tests run fast (no game initialization required)
- Clear dependency graph (all dependencies explicit)
- Refactoring is safer (compiler catches missing dependencies)

### Negative
- More boilerplate (extra interfaces + implementations)
- Larger constructor parameter lists
- Requires discipline to complete integration fully

## Implementation Patterns

### Pattern 1: Creating a New Provider

```csharp
// 1. Create interface
namespace TAOM.Adapters;
public interface ICampaignTimeProvider
{
    CampaignTime Now { get; }
    CampaignTime YearsFromNow(float years);
}

// 2. Create implementation
namespace TAOM.Adapters;
public class CampaignTimeProvider : ICampaignTimeProvider
{
    public CampaignTime Now => CampaignTime.Now;
    public CampaignTime YearsFromNow(float years) => CampaignTime.YearsFromNow(years);
}

// 3. Register in IoC
container.Register<ICampaignTimeProvider, CampaignTimeProvider>(Reuse.Singleton);

// 4. Inject in service
public class MyService
{
    private readonly ICampaignTimeProvider _timeProvider;

    public MyService(ICampaignTimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }
}

// 5. Mock in tests
[TestInitialize]
public void Setup()
{
    _mockTimeProvider = Substitute.For<ICampaignTimeProvider>();
    _mockTimeProvider.Now.Returns(new CampaignTime()); // Use CampaignTimeTestHelper
    _service = new MyService(_mockTimeProvider);
}
```

### Pattern 2: Test Helper for CampaignTime

Use `CampaignTimeTestHelper` instead of mocking `CampaignTime` structs:

```csharp
// WRONG - CampaignTime is a struct, can't be mocked
_mockTimeProvider.Now.Returns(CampaignTime.Now); // NullReferenceException

// CORRECT - Use test helper
using TAOM.Tests.TestHelpers;

_mockTimeProvider.Now.Returns(CampaignTimeTestHelper.Now());
_mockTimeProvider.YearsFromNow(1).Returns(CampaignTimeTestHelper.YearsFromNow(1));
```

### Pattern 3: Optional Game Framework Calls

For optional game framework features that enhance behavior but aren't required:

```csharp
public class CasualtyCalculationService
{
    public float CalculateModifiedSurvivalChance(...)
    {
        float baseChance = CalculateBaseChance(...);

        // Optional modifier based on battle type (requires active encounter)
        try
        {
            var encounter = PlayerEncounter.Current;
            if (encounter != null)
            {
                baseChance *= GetBattleTypeModifier(encounter.BattleState);
            }
        }
        catch (NullReferenceException)
        {
            // No active encounter in unit tests - skip battle type modifier
        }

        return baseChance;
    }
}
```

## Enforcement

### Code Review Checklist
- [ ] No direct `CampaignTime.X` calls in services
- [ ] No direct `Utilities.GetBasePath()` calls
- [ ] All new providers fully integrated
- [ ] All test files updated
- [ ] Full test suite passes

### Pre-Commit Hook
Install pre-commit hook to catch violations:
```bash
./scripts/install-pre-commit-hook.ps1
```

Hook runs: `./build.ps1 -RunTests -MinCoverage 80`
- Blocks commits if tests fail
- Blocks commits if coverage drops

### CI/CD Gate (Recommended)
```yaml
# .github/workflows/pr-validation.yml
- name: Run Tests
  run: dotnet test --no-build --verbosity normal
- name: Verify Coverage
  run: dotnet test /p:CollectCoverage=true /p:Threshold=80
- name: Check for Static Calls
  run: |
    # Fail if services call CampaignTime.Now directly
    git grep -n "CampaignTime\\.Now" Main/Features/*/Services/ && exit 1 || exit 0
```

## Related ADRs
- **ADR-007**: Adapter Pattern for Sealed Classes - Services use adapters, not sealed types
- **ADR-002**: Thin Entry Points - Entry points may use static calls, but services cannot

## Examples from Codebase

### Good: MomentumEventService (After Fix)
```csharp
public class MomentumEventService
{
    private readonly ICampaignTimeProvider _campaignTimeProvider;

    public MomentumEventService(
        ICampaignTimeProvider campaignTimeProvider,
        // ... other dependencies
    )
    {
        _campaignTimeProvider = campaignTimeProvider;
    }

    public void ProcessExpiredEvents(...)
    {
        var now = _campaignTimeProvider.Now; // Testable
        // ...
    }
}
```

### Bad: MomentumConfigLoader (Before Fix)
```csharp
public class MomentumConfigLoader
{
    public MomentumConfigLoader(IModLogger logger)
    {
        _logger = logger;
        string basePath = Utilities.GetBasePath(); // NullReferenceException in tests
        _configPath = Path.Combine(basePath, "Modules", "TAOM", ...);
    }
}
```

### Good: MomentumConfigLoader (After Fix)
```csharp
public class MomentumConfigLoader
{
    public MomentumConfigLoader(IModLogger logger, IModulePathProvider pathProvider)
    {
        _logger = logger;
        _pathProvider = pathProvider;
        _configPath = Path.Combine(
            pathProvider.GetModuleDataPath("TAOM"), // Testable
            "Momentum"
        );
    }
}
```

## Migration Guide

When you find a service using static TaleWorlds calls:

1. **Identify the static call** (e.g., `CampaignTime.Now`)
2. **Check if provider exists** (see table in Rule 3)
3. **If provider exists**: Add to constructor, refactor calls
4. **If provider missing**: Create interface + implementation first
5. **Update IoC registration**
6. **Update all test files**
7. **Run full test suite**
8. **Do NOT commit until 100% passing**

## Revision History
- 2025-01-22: Initial version
