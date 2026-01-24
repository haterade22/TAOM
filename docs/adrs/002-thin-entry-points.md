# ADR-002: Thin Entry Points Pattern

**Status**: Accepted (Mandatory)
**Date**: 2025-01-11
**Context**: Mount & Blade II modding architecture

## Decision

All game integration entry points (Harmony patches, CampaignBehaviors, GameModels, MissionViews) MUST be thin orchestration layers that delegate to dedicated service classes. Game framework objects (sealed classes) MUST be wrapped in adapter interfaces to enable testability.

## Rationale

### Problems with Logic in Entry Points

1. **Untestable**: Harmony patches and behaviors depend on game lifecycle, making unit testing impossible
2. **Tight Coupling**: Business logic becomes coupled to game framework
3. **Violation of SRP**: Entry points handle both framework integration AND domain logic
4. **Poor Reusability**: Logic trapped in entry points cannot be reused
5. **Difficult to Mock**: Cannot test components that depend on behaviors/patches
6. **Sealed Game Classes**: TaleWorlds game objects (Hero, Settlement, ItemObject, etc.) are sealed and cannot be mocked

### Solution: Three-Layer Architecture

```
Entry Point (Thin Orchestration) -> Adapter Layer -> Service Layer (Domain Logic)
    |                                    |                |
GameModel/Behavior/Patch         IAdapterFactory      IService Interface
                                 IXxxAdapter          Service Implementation
```

**Layer Responsibilities**:
1. **Entry Point**: Receives game events, resolves dependencies, delegates to services
2. **Adapter Layer**: Wraps sealed game objects with mockable interfaces for testability
3. **Service Layer**: Contains all business logic, receives adapter interfaces, fully testable

## Rules

### FORBIDDEN in Entry Points

- Complex business logic
- Data transformations
- Algorithm implementations
- Direct game state mutations beyond delegation
- Multiple responsibilities

### ALLOWED in Entry Points

- Dependency injection via constructor (services and IAdapterFactory)
- Event registration
- Simple delegation to services
- Adapter creation via IAdapterFactory
- Parameter extraction from game events
- UI message display (for behaviors only)

## Examples

### BAD: Logic in Harmony Patch

```csharp
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class DamagePatch
{
    static void Postfix(ref int __result, Agent attacker, Agent victim)
    {
        // 50 lines of damage calculation logic HERE - WRONG!
        var raceMultiplier = GetRaceMultiplier(attacker.Race, victim.Race);
        __result = (int)(__result * raceMultiplier);
    }
}
```

### GOOD: Thin Patch Delegating to Service

```csharp
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class DamagePatch
{
    static void Postfix(ref int __result, Agent attacker, Agent victim)
    {
        var service = IoC.Resolve<IDamageCalculationService>();
        __result = service.ApplyRaceBonuses(__result, attacker, victim);
    }
}
```

### BAD: Logic in CampaignBehavior

```csharp
public class MyBehavior : CampaignBehaviorBase
{
    private void OnDailyTick()
    {
        // 100 lines of relationship calculation - WRONG!
        foreach (var hero1 in allHeroes)
        {
            foreach (var hero2 in allHeroes)
            {
                // Complex alignment logic...
            }
        }
    }
}
```

### GOOD: Thin Behavior Delegating to Service

```csharp
public class MyBehavior : CampaignBehaviorBase
{
    private readonly IRelationshipService _service;

    public MyBehavior(IRelationshipService service)
    {
        _service = service;
    }

    private void OnDailyTick()
    {
        _service.EnforceDailyRelationshipRules();
    }
}
```

### BAD: GameModel with Sealed Game Objects

```csharp
public class MyGameModel : GameModel
{
    private readonly IMyService _service;

    public override int CalculateSomething(Hero hero, Settlement settlement)
    {
        // Passing sealed game objects directly - CANNOT BE TESTED!
        return _service.Calculate(hero, settlement);
    }
}

// Service interface accepting sealed types - WRONG!
public interface IMyService
{
    int Calculate(Hero hero, Settlement settlement); // Cannot mock Hero/Settlement!
}
```

### GOOD: GameModel with Adapter Layer

```csharp
public class MyGameModel : GameModel
{
    private readonly IMyService _service;
    private readonly IAdapterFactory _adapterFactory;

    public MyGameModel(IMyService service, IAdapterFactory adapterFactory)
    {
        _service = service;
        _adapterFactory = adapterFactory;
    }

    public override int CalculateSomething(Hero hero, Settlement settlement)
    {
        // Wrap game objects in adapters
        var heroAdapter = _adapterFactory.GetHeroAdapter(hero);
        var settlementAdapter = _adapterFactory.GetSettlementAdapter(settlement);

        // Pass adapters to service
        return _service.Calculate(heroAdapter, settlementAdapter);
    }
}

// Service interface accepting adapter interfaces - testable!
public interface IMyService
{
    int Calculate(IHeroAdapter hero, ISettlementAdapter settlement);
}

// Adapter interface wrapping sealed Hero class
public interface IHeroAdapter
{
    string Name { get; }
    ICultureAdapter Culture { get; }
    int Gold { get; }
}
```

### CRITICAL: GameModel Property Overrides and Method Delegation

**Rule**: If a GameModel overrides properties, methods that use those properties MUST be fully reimplemented. NEVER delegate methods to a base model instance when properties are overridden.

**Why**: Property overrides don't propagate through method delegation because C# preserves the `this` context of the delegated object. The base model's method will use its own property values, completely ignoring your overrides.

#### BAD: Property Override + Method Delegation

```csharp
public class CustomSettlementFoodModel : SettlementFoodModel
{
    private readonly SettlementFoodModel _defaultModel;

    public CustomSettlementFoodModel(SettlementFoodModel defaultModel)
    {
        _defaultModel = defaultModel;
    }

    // Override property (custom wants slower food consumption)
    public override int NumberOfProsperityToEatOneFood => 80; // IGNORED!

    // Delegate method to base model
    public override ExplainedNumber CalculateTownFoodStocksChange(Town town, ...)
    {
        // Base model uses ITS NumberOfProsperityToEatOneFood (40)
        // Our override (80) is completely ignored!
        return _defaultModel.CalculateTownFoodStocksChange(town, ...);
    }
}
```

#### GOOD: Full Method Reimplementation

```csharp
public class CustomSettlementFoodModel : SettlementFoodModel
{
    // No base model dependency!

    // Override property
    public override int NumberOfProsperityToEatOneFood => 80;

    // Reimplement method to use OUR property
    public override ExplainedNumber CalculateTownFoodStocksChange(Town town, ...)
    {
        ExplainedNumber result = new ExplainedNumber(0f, includeDescriptions);

        // Direct usage of OUR overridden property
        float consumption = -town.Prosperity / (float)NumberOfProsperityToEatOneFood;
        //                                              Uses OUR override (80) correctly!

        result.Add(consumption, ProsperityText);

        // ... complete reimplementation of calculation logic

        return result;
    }
}
```

## Service Design Guidelines

1. **Single Responsibility**: One service per feature domain
2. **Interface-Based**: Always define `IServiceName` interface
3. **Constructor Injection**: Services receive dependencies via constructor
4. **No Game Dependencies**: Services should not depend on game lifecycle or sealed game types
5. **Adapter Interfaces Only**: Services MUST accept adapter interfaces (IHeroAdapter), NEVER sealed game types (Hero)
6. **No Reflection in Services**: ALL reflection access MUST be encapsulated in adapter implementations
7. **Testable**: All services must have unit tests using mocked adapters

## Adapter Pattern Implementation

### When to Use Adapters

Use adapters for ALL TaleWorlds sealed classes that need to be passed to services:
- `Hero`, `Settlement`, `Clan`, `Kingdom`
- `CharacterObject`, `ItemObject`, `Equipment`
- `MobileParty`, `PartyBase`, `MapEvent`
- `Agent`, `Town`, `Village`
- `CultureObject`, `WeaponComponentData`

### Adapter Factory Pattern

**Central Factory**: `IAdapterFactory` provides cached adapter instances

```csharp
public interface IAdapterFactory
{
    IHeroAdapter GetHeroAdapter(Hero hero);
    ISettlementAdapter GetSettlementAdapter(Settlement settlement);
    // ... other adapter creation methods
}
```

**Benefits**:
- **Caching**: Avoids repeated wrapper allocations using `ConcurrentDictionary`
- **Thread-Safe**: Lock-free concurrent access
- **Null-Safe**: Returns null for null inputs
- **Composition**: Adapters can create child adapters via factory
- **Reflection Encapsulation**: Private member access hidden within adapters

## Entry Point Checklist

Before merging code touching entry points, verify:
- [ ] Entry point class is <150 lines (excluding interface definitions)
- [ ] All complex logic delegated to services
- [ ] Service is registered in IoC container
- [ ] Service has corresponding interface
- [ ] Service has unit tests using mocked adapters
- [ ] Entry point only contains orchestration logic
- [ ] Game objects wrapped in adapters before passing to services
- [ ] Services accept adapter interfaces, NOT sealed game types

## Migration Strategy

When refactoring existing code:
1. Identify sealed game types in service signatures
2. Create adapter interfaces for those types (or use existing)
3. Implement adapters if needed (check `/Main/Adapters/` first)
4. Create service interface with adapter parameters
5. Extract logic to service implementation
6. Register service and adapters in IoC
7. Update entry point to use `IAdapterFactory` and delegate
8. Write tests for service using mocked adapters
9. Remove old logic from entry point

## Consequences

### Positive
- Testable business logic
- Clear separation of concerns
- Reusable services across entry points
- Easier to maintain and extend
- Better code organization

### Negative
- More files per feature (interface + implementation + entry point)
- Requires discipline to maintain pattern
- Initial setup overhead for new features

## Enforcement

1. **Code Review**: Reviewers reject PRs violating this pattern
2. **Architecture Tests**: Automated tests verify entry point line counts
3. **Documentation**: This ADR is linked in CLAUDE.md
4. **Examples**: Reference implementations for each entry point type

## Related ADRs

- **ADR-007**: Adapter Pattern for Sealed Classes - Detailed adapter pattern implementation
