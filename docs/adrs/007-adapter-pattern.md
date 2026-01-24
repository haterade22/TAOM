# ADR-007: Adapter Pattern Enforcement for Sealed Game Classes

**Status**: Mandatory
**Date**: 2025-11-14
**Context**: Mount & Blade II modding with sealed TaleWorlds game classes

## Decision

Service layer MUST use adapter interfaces for all sealed TaleWorlds game classes. Entry points (CampaignBehaviors, GameModels, Harmony patches, MissionViews) convert sealed types to adapters before calling services. **Adapters MUST recursively wrap ALL nested sealed types** - when a sealed class contains properties that return other sealed classes, those nested types must also be wrapped in adapter interfaces. This enables 100% testability of business logic.

## Rationale

### The Problem: Sealed Game Classes Cannot Be Mocked

TaleWorlds game classes are sealed, preventing:
- Unit testing with mocked objects
- Test-driven development (TDD)
- Service isolation during testing
- Fast test execution (no game framework initialization)

**Sealed Classes Requiring Adapters**:
- **Core**: `Hero`, `MobileParty`, `Settlement`, `Clan`, `Kingdom`, `CharacterObject`
- **Party**: `TroopRoster`, `ItemRoster`, `PartyBase`, `MapEvent`
- **Items**: `ItemObject`, `Equipment`, `WeaponComponentData`, `ArmorComponent`
- **Location**: `Town`, `Village`, `Hideout`
- **Combat**: `Agent`, `Mission`, `Formation`
- **Static Actions**: `DestroyPartyAction`, `ChangeRelationAction`, `KillCharacterAction`
- **Singletons**: `MBObjectManager`, `CampaignTime`

### The Solution: Adapter Layer + Factory Pattern

```
Entry Point (Thin) -> IAdapterFactory -> Adapter Interfaces -> Service Layer
       |                    |                  |                   |
GameModel/Behavior     GetXxxAdapter()    IXxxAdapter      Fully Testable
```

**Architecture**:
1. **Entry Point**: Receives sealed game objects from framework events
2. **IAdapterFactory**: Converts sealed objects to cached adapter instances
3. **Adapter Interface**: Mockable interface exposing needed properties/methods
4. **Service Layer**: Receives adapters, contains all business logic, 100% testable

## Rules

### FORBIDDEN in Service Layer

**Services MUST NOT accept sealed TaleWorlds types**:

```csharp
// BAD - Service signature with sealed types
public interface IPatrolService
{
    void CreatePatrol(Settlement settlement, TroopRoster troops); // CANNOT BE TESTED
}

public class PatrolService : IPatrolService
{
    public void CreatePatrol(Settlement settlement, TroopRoster troops)
    {
        // 265 lines of untestable business logic
    }
}
```

**Problems**:
- `Settlement` and `TroopRoster` are sealed, cannot be mocked
- Service requires real game framework to test (slow, brittle)
- TDD impossible (cannot write tests before implementation)
- Cannot isolate service behavior in unit tests

### REQUIRED in Service Layer

**Services MUST accept adapter interfaces**:

```csharp
// GOOD - Service signature with adapter interfaces
public interface IPatrolService
{
    IMobilePartyAdapter CreatePatrol(ISettlementAdapter settlement, ITroopRosterAdapter troops);
}

public class PatrolService : IPatrolService
{
    private readonly IAdapterFactory _adapterFactory;

    public PatrolService(IAdapterFactory adapterFactory)
    {
        _adapterFactory = adapterFactory;
    }

    public IMobilePartyAdapter CreatePatrol(
        ISettlementAdapter settlement,
        ITroopRosterAdapter troops)
    {
        // 265 lines of FULLY TESTABLE business logic
        // All parameters are mockable interfaces
    }
}
```

**Benefits**:
- All parameters mockable with `NSubstitute.For<IXxxAdapter>()`
- No game framework required for testing
- Fast, isolated unit tests
- TDD-friendly: write tests first, implementation second
- >85% test coverage achievable

### Entry Point Conversion Pattern

**Entry points MAY use sealed types** (they receive them from game framework):

```csharp
// CampaignBehavior - Entry Point (Thin)
public class PatrolCampaignBehavior : CampaignBehaviorBase
{
    private readonly IPatrolService _patrolService;
    private readonly IAdapterFactory _adapterFactory;

    public PatrolCampaignBehavior(
        IPatrolService patrolService,
        IAdapterFactory adapterFactory)
    {
        _patrolService = patrolService;
        _adapterFactory = adapterFactory;
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement)
    {
        // Sealed types from game framework

        // Convert to adapters at entry point (single line each)
        var settlementAdapter = _adapterFactory.GetSettlementAdapter(settlement);
        var troopRoster = CreateTroopRoster(); // some logic
        var troopRosterAdapter = _adapterFactory.GetTroopRosterAdapter(troopRoster);

        // Service receives adapters only
        var patrol = _patrolService.CreatePatrol(settlementAdapter, troopRosterAdapter);

        // Use result (adapter returned)
        patrol.Position2D = settlement.GatePosition;
    }
}
```

**Key Points**:
- Entry point receives sealed types (unavoidable, they come from game events)
- Entry point wraps sealed types in adapters (via `IAdapterFactory`)
- Service receives only adapters
- Conversion is minimal (1 line per object)
- Entry point remains thin (<150 lines per ADR-002)

## Adapter Factory Pattern

### IAdapterFactory Interface

**Central factory for all adapter creation**:

```csharp
public interface IAdapterFactory
{
    // Heroes and characters
    IHeroAdapter GetHeroAdapter(Hero hero);
    ICharacterObjectAdapter GetCharacterObjectAdapter(CharacterObject character);

    // Parties and rosters
    IMobilePartyAdapter GetMobilePartyAdapter(MobileParty party);
    ITroopRosterAdapter GetTroopRosterAdapter(TroopRoster roster);
    IItemRosterAdapter GetItemRosterAdapter(ItemRoster roster);

    // Settlements and locations
    ISettlementAdapter GetSettlementAdapter(Settlement settlement);
    ITownAdapter GetTownAdapter(Town town);
    IVillageAdapter GetVillageAdapter(Village village);

    // Clans and factions
    IClanAdapter GetClanAdapter(Clan clan);
    IKingdomAdapter GetKingdomAdapter(Kingdom kingdom);

    // Items and equipment
    IItemObjectAdapter GetItemObjectAdapter(ItemObject item);
    IEquipmentAdapter GetEquipmentAdapter(Equipment equipment);

    // Static actions and singletons
    IPartyActionService PartyActionService { get; }
    IGameObjectManager GameObjectManager { get; }

    // ... more adapter creation methods
}
```

### Factory Implementation: Caching

**AdapterFactory uses `ConcurrentDictionary` for caching**:

```csharp
public class AdapterFactory : IAdapterFactory
{
    private readonly ConcurrentDictionary<Hero, IHeroAdapter> _heroAdapters;
    private readonly ConcurrentDictionary<Settlement, ISettlementAdapter> _settlementAdapters;
    // ... more caches

    public IHeroAdapter GetHeroAdapter(Hero hero)
    {
        if (hero == null) return null;

        return _heroAdapters.GetOrAdd(hero, h => new HeroAdapter(h, this));
    }

    public ISettlementAdapter GetSettlementAdapter(Settlement settlement)
    {
        if (settlement == null) return null;

        return _settlementAdapters.GetOrAdd(settlement, s => new SettlementAdapter(s, this));
    }
}
```

**Benefits**:
- **Thread-Safe**: `ConcurrentDictionary` provides lock-free concurrency
- **Performance**: Avoids repeated wrapper allocations
- **Memory Efficient**: Single adapter instance per game object
- **Null-Safe**: Returns null for null inputs
- **Composition**: Adapters can create child adapters via factory
- **Recursive Wrapping**: Factory enables adapters to wrap nested sealed types by calling other factory methods

## Adapter Interface Design

### Basic Adapter Pattern

```csharp
// Adapter interface - mockable
public interface IItemObjectAdapter
{
    string StringId { get; }
    string Name { get; }
    int Value { get; }
    float Weight { get; }
    bool IsFood { get; }
    ICultureAdapter Culture { get; } // Composed adapter
}

// Adapter implementation - wraps sealed class
public class ItemObjectAdapter : IItemObjectAdapter
{
    private readonly ItemObject _itemObject;
    private readonly IAdapterFactory _factory;

    public ItemObjectAdapter(ItemObject itemObject, IAdapterFactory factory)
    {
        _itemObject = itemObject;
        _factory = factory;
    }

    public string StringId => _itemObject.StringId;
    public string Name => _itemObject.Name?.ToString();
    public int Value => _itemObject.Value;
    public float Weight => _itemObject.Weight;
    public bool IsFood => _itemObject.IsFood;

    // Compose child adapters via factory
    public ICultureAdapter Culture => _factory.GetCultureAdapter(_itemObject.Culture);
}
```

### Recursive Adapter Wrapping (CRITICAL)

**When a sealed class contains properties that return other sealed classes, ALL nested sealed classes MUST also be wrapped in adapters**:

```csharp
// BAD - Adapter exposes nested sealed types
public interface IMobilePartyAdapter
{
    string StringId { get; }
    Vec2 Position2D { get; set; }
    TroopRoster MemberRoster { get; } // WRONG - Returns sealed type!
    Settlement CurrentSettlement { get; } // WRONG - Returns sealed type!
}

// Service receives adapter but immediately hits sealed types
public class MyService
{
    public void ProcessParty(IMobilePartyAdapter party)
    {
        // Still can't test this - TroopRoster is sealed!
        var troopCount = party.MemberRoster.TotalManCount; // Sealed type!

        // Still can't test this - Settlement is sealed!
        var settlementName = party.CurrentSettlement.Name; // Sealed type!
    }
}
```

**Problems**:
- Service receives adapter but immediately accesses sealed nested types
- Cannot mock `party.MemberRoster.TotalManCount` in tests
- Cannot mock `party.CurrentSettlement.Name` in tests
- Adapter pattern breaks at the first property access
- **Service is still untestable despite using adapters!**

**CORRECT - Adapter recursively wraps ALL nested sealed types**:

```csharp
// GOOD - Adapter returns adapter interfaces for nested types
public interface IMobilePartyAdapter
{
    string StringId { get; }
    Vec2 Position2D { get; set; }
    ITroopRosterAdapter MemberRoster { get; } // Returns adapter interface
    ISettlementAdapter CurrentSettlement { get; } // Returns adapter interface
    IHeroAdapter LeaderHero { get; } // Returns adapter interface
    IPartyBaseAdapter Party { get; } // Returns adapter interface
}

// Implementation uses IAdapterFactory for recursive wrapping
public class MobilePartyAdapter : IMobilePartyAdapter
{
    private readonly MobileParty _mobileParty;
    private readonly IAdapterFactory _factory;

    public MobilePartyAdapter(MobileParty mobileParty, IAdapterFactory factory)
    {
        _mobileParty = mobileParty;
        _factory = factory;
    }

    public string StringId => _mobileParty.StringId;
    public Vec2 Position2D
    {
        get => _mobileParty.Position2D;
        set => _mobileParty.Position2D = value;
    }

    // Recursively wrap nested sealed types via factory
    public ITroopRosterAdapter MemberRoster =>
        _factory.GetTroopRosterAdapter(_mobileParty.MemberRoster);

    public ISettlementAdapter CurrentSettlement =>
        _factory.GetSettlementAdapter(_mobileParty.CurrentSettlement);

    public IHeroAdapter LeaderHero =>
        _factory.GetHeroAdapter(_mobileParty.LeaderHero);

    public IPartyBaseAdapter Party =>
        _factory.GetPartyBaseAdapter(_mobileParty.Party);
}

// Service is now 100% testable - all nested types are adapters
public class MyService
{
    public void ProcessParty(IMobilePartyAdapter party)
    {
        // Fully mockable - MemberRoster is an adapter interface
        var troopCount = party.MemberRoster.TotalManCount; // Testable!

        // Fully mockable - CurrentSettlement is an adapter interface
        var settlementName = party.CurrentSettlement.Name; // Testable!

        // Can drill down further - still testable
        var ownerClan = party.CurrentSettlement.OwnerClan; // Returns IClanAdapter
        var leaderName = ownerClan.Leader.Name; // Returns IHeroAdapter
    }
}

// Test demonstrates full mockability
[TestMethod]
public void ProcessParty_AccessesNestedProperties_FullyMockable()
{
    // Arrange - Mock all levels of nesting
    var mockParty = Substitute.For<IMobilePartyAdapter>();
    var mockRoster = Substitute.For<ITroopRosterAdapter>();
    var mockSettlement = Substitute.For<ISettlementAdapter>();
    var mockClan = Substitute.For<IClanAdapter>();
    var mockLeader = Substitute.For<IHeroAdapter>();

    // Set up nested mocks
    mockParty.MemberRoster.Returns(mockRoster);
    mockParty.CurrentSettlement.Returns(mockSettlement);
    mockSettlement.OwnerClan.Returns(mockClan);
    mockClan.Leader.Returns(mockLeader);

    // Configure return values at any nesting level
    mockRoster.TotalManCount.Returns(100);
    mockSettlement.Name.Returns("Sargot");
    mockLeader.Name.Returns("Derthert");

    // Act
    var service = new MyService();
    service.ProcessParty(mockParty);

    // Assert - Can verify interactions at any nesting level
    mockRoster.Received(1).TotalManCount;
    mockSettlement.Received(1).Name;
    mockLeader.Received(1).Name;
}
```

**Key Principles**:
1. **ALL sealed types MUST be wrapped** - No exceptions, even for nested properties
2. **Adapters return adapters** - Properties returning sealed types must return adapter interfaces
3. **Use IAdapterFactory for nesting** - Factory ensures cached instances and consistent wrapping
4. **Testability is transitive** - If any level returns sealed types, the entire chain becomes untestable
5. **Think recursively** - When creating an adapter, check ALL properties it exposes

**Visual: Recursive Wrapping Chain**:

```
Service Layer (100% Testable)
    |
IMobilePartyAdapter (mockable)
    | .MemberRoster
ITroopRosterAdapter (mockable)
    | .GetTroopRoster()
IEnumerable<ITroopRosterElementAdapter> (mockable)
    | .Character
ICharacterObjectAdapter (mockable)
    | .Culture
ICultureAdapter (mockable)

BREAKS if ANY level returns sealed type:
IMobilePartyAdapter
    | .MemberRoster
TroopRoster (SEALED - not mockable!)
    | TESTABILITY CHAIN BROKEN
```

### Encapsulating Reflection in Adapters

**Adapters MUST encapsulate ALL reflection access**:

```csharp
// Adapter interface exposes private field as public property
public interface ISettlementAdapter
{
    string StringId { get; }
    string Name { get; }
    int Prosperity { get; set; } // Wraps private field via reflection
    Vec2 GatePosition { get; }
}

// Adapter implementation uses ReflectionExtensions
public class SettlementAdapter : ISettlementAdapter
{
    private readonly Settlement _settlement;

    public SettlementAdapter(Settlement settlement)
    {
        _settlement = settlement;
    }

    public string StringId => _settlement.StringId;
    public string Name => _settlement.Name?.ToString();
    public Vec2 GatePosition => _settlement.GatePosition;

    // Encapsulate reflection access to private field
    public int Prosperity
    {
        get => _settlement.GetFieldValue<int>("_prosperity");
        set => _settlement.SetFieldValue("_prosperity", value);
    }
}

// Service uses clean interface - no reflection knowledge
public class MyService : IMyService
{
    public void IncreaseProsperity(ISettlementAdapter settlement)
    {
        // Clean domain logic - no reflection!
        settlement.Prosperity += 100;
    }
}
```

**Available Reflection Utilities**:
- `ReflectionExtensions.GetFieldValue<T>()` - Get private field
- `ReflectionExtensions.SetFieldValue()` - Set private field
- `ReflectionExtensions.GetPropertyValue<T>()` - Get private property
- `ReflectionExtensions.SetPropertyValue()` - Set private property
- `ReflectionExtensions.InvokeMethod<T>()` - Call private method
- `ReflectionHelper.CallPrivateMethod()` - Static helper

**Why Encapsulation Matters**:
- Services remain clean and testable
- Reflection complexity isolated in adapters
- Easier to mock (mocks just return values)
- Single source of truth for private member access

## Testing with Adapters

### Unit Testing Example

```csharp
[TestClass]
public class PatrolServiceTests
{
    private IPatrolService _patrolService;
    private IAdapterFactory _mockFactory;

    [TestInitialize]
    public void Setup()
    {
        _mockFactory = Substitute.For<IAdapterFactory>();
        _patrolService = new PatrolService(_mockFactory);
    }

    [TestMethod]
    public void CreatePatrol_WithValidSettlement_CreatesPatrolParty()
    {
        // Arrange - Mock all adapters
        var mockSettlement = Substitute.For<ISettlementAdapter>();
        var mockTroopRoster = Substitute.For<ITroopRosterAdapter>();
        var mockOwnerClan = Substitute.For<IClanAdapter>();
        var mockLeader = Substitute.For<IHeroAdapter>();

        mockSettlement.StringId.Returns("town_V1");
        mockSettlement.Name.Returns("Sargot");
        mockSettlement.OwnerClan.Returns(mockOwnerClan);
        mockOwnerClan.Leader.Returns(mockLeader);

        mockTroopRoster.TotalManCount.Returns(50);

        // Act - Service receives mocked adapters
        var result = _patrolService.CreatePatrol(mockSettlement, mockTroopRoster);

        // Assert - Verify behavior
        Assert.IsNotNull(result);
        mockSettlement.Received(1).StringId; // Verify interactions
    }

    [TestMethod]
    public void CreatePatrol_WithNullSettlement_ReturnsNull()
    {
        // Arrange
        var mockTroopRoster = Substitute.For<ITroopRosterAdapter>();

        // Act
        var result = _patrolService.CreatePatrol(null, mockTroopRoster);

        // Assert
        Assert.IsNull(result);
    }
}
```

**Test Coverage Achievements**:
- **Before Adapters**: 0% coverage (untestable)
- **After Adapters**: >85% coverage
- **Test Speed**: <100ms per test (no game framework)
- **Isolation**: Each test independent
- **TDD-Friendly**: Write tests before implementation

## Exceptions

### Value Types Don't Need Adapters

**These types don't need adapters** (they're value types or simple structures):
- `Vec2`, `Vec3` - Position vectors
- `TextObject` - Localized text (has constructor overloads)
- `ExplainedNumber` - Number with explanation breakdown
- Primitives: `int`, `float`, `string`, `bool`, `enum`

```csharp
// CORRECT - Value types passed directly
public interface IPositionService
{
    float CalculateDistance(Vec2 position1, Vec2 position2);
    void UpdatePosition(IMobilePartyAdapter party, Vec2 newPosition);
}
```

### Entry Points May Use Sealed Types

**These entry points receive sealed types from game framework** (unavoidable):
- **CampaignBehaviors**: Event handlers receive sealed types
- **GameModels**: Override methods have sealed type parameters
- **Harmony Patches**: Patched methods have sealed type parameters
- **MissionViews**: Mission lifecycle methods use sealed types

**Entry points MUST**:
- Convert sealed types to adapters (via `IAdapterFactory`)
- Keep conversion minimal (single line per object)
- Pass only adapters to services

## Migration Strategy (TDD Approach)

### Phase 1: Identify Sealed Types

```csharp
// BEFORE: Service with sealed types (untestable)
public interface IOldService
{
    void ProcessParty(MobileParty party, Settlement settlement);
}
```

1. **Find Sealed Types**: `MobileParty`, `Settlement`
2. **Check Existing Adapters**: Look in `/Main/Adapters/` for `IMobilePartyAdapter`, `ISettlementAdapter`
3. **Identify Missing Properties**: What properties/methods does service need?

### Phase 2: Create/Extend Adapters

```csharp
// Create adapter interface (or extend existing)
public interface IMobilePartyAdapter
{
    string StringId { get; }
    Vec2 Position2D { get; set; }
    ITroopRosterAdapter MemberRoster { get; }
    // ... add properties service needs
}

public interface ISettlementAdapter
{
    string StringId { get; }
    Vec2 GatePosition { get; }
    IClanAdapter OwnerClan { get; }
    // ... add properties service needs
}
```

### Phase 3: Write Tests FIRST (Red)

```csharp
[TestClass]
public class NewServiceTests
{
    private INewService _service;

    [TestInitialize]
    public void Setup()
    {
        _service = new NewService();
    }

    [TestMethod]
    public void ProcessParty_WithValidParty_UpdatesPosition()
    {
        // Arrange - Mock adapters
        var mockParty = Substitute.For<IMobilePartyAdapter>();
        var mockSettlement = Substitute.For<ISettlementAdapter>();
        mockSettlement.GatePosition.Returns(new Vec2(100, 100));

        // Act
        _service.ProcessParty(mockParty, mockSettlement);

        // Assert
        mockParty.Received().Position2D = new Vec2(100, 100);
    }

    // ... 20+ more tests covering all scenarios
}
```

**Tests fail at this point** (Red) - service doesn't use adapters yet.

### Phase 4: Refactor Service to Use Adapters (Green)

```csharp
// AFTER: Service with adapter interfaces (testable)
public interface INewService
{
    void ProcessParty(IMobilePartyAdapter party, ISettlementAdapter settlement);
}

public class NewService : INewService
{
    private readonly IAdapterFactory _adapterFactory;

    public NewService(IAdapterFactory adapterFactory)
    {
        _adapterFactory = adapterFactory;
    }

    public void ProcessParty(IMobilePartyAdapter party, ISettlementAdapter settlement)
    {
        // Business logic using adapters
        party.Position2D = settlement.GatePosition;

        // All logic uses adapter interfaces
        // Fully testable with mocked adapters
    }
}
```

**Tests pass now** (Green) - service uses adapters.

### Phase 5: Update Entry Points

```csharp
// Update entry point to convert types
public class MyCampaignBehavior : CampaignBehaviorBase
{
    private readonly INewService _service;
    private readonly IAdapterFactory _adapterFactory;

    public MyCampaignBehavior(INewService service, IAdapterFactory adapterFactory)
    {
        _service = service;
        _adapterFactory = adapterFactory;
    }

    private void OnPartyArrived(MobileParty party, Settlement settlement)
    {
        // Convert sealed types to adapters at entry point
        var partyAdapter = _adapterFactory.GetMobilePartyAdapter(party);
        var settlementAdapter = _adapterFactory.GetSettlementAdapter(settlement);

        // Service receives adapters
        _service.ProcessParty(partyAdapter, settlementAdapter);
    }
}
```

### Phase 6: Verify and Refine (Refactor)

1. **Run All Tests**: Verify >85% coverage
2. **In-Game Testing**: Manual validation
3. **Refactor**: Simplify code while tests pass
4. **Remove Old Code**: Delete old untestable implementation

## Consequences

### Positive

1. **100% Business Logic Testability**: All service logic can be unit tested
2. **Fast Test Execution**: No game framework initialization (<100ms per test)
3. **TDD-Friendly**: Write tests before implementation
4. **Isolated Testing**: Each service tests independently
5. **Clear Dependencies**: Constructor parameters show all dependencies
6. **Maintainable**: Easier to refactor with comprehensive test suite
7. **Documented**: Adapter interfaces document what properties services need

### Negative

1. **Boilerplate**: Additional adapter interfaces and implementations
2. **Learning Curve**: Developers must understand adapter pattern
3. **Initial Overhead**: Creating adapters for new sealed types
4. **Entry Point Conversion**: One line per sealed object conversion
5. **More Files**: Adapters increase file count in `/Main/Adapters/`

**Mitigation**:
- `IAdapterFactory` reduces boilerplate (cached instances)
- Most common adapters already exist in `/Main/Adapters/`
- Conversion is mechanical (1 line: `_adapterFactory.GetXxxAdapter(obj)`)
- Long-term benefits outweigh short-term overhead

## Enforcement

### Code Review Checklist

Before merging PRs touching service layer:
- [ ] Service interfaces accept adapter interfaces (NOT sealed types)
- [ ] Service implementations use adapters throughout
- [ ] Entry points convert sealed types to adapters
- [ ] All new adapters registered in `AdapterFactory`
- [ ] **Adapters recursively wrap ALL nested sealed types** (properties returning sealed types must return adapter interfaces)
- [ ] Adapter implementations inject `IAdapterFactory` and use it for nested wrapping
- [ ] Services have >80% test coverage
- [ ] Tests use mocked adapters (NSubstitute)
- [ ] Tests mock nested adapter interfaces where properties are accessed

### Architecture Tests (Recommended)

See `TAOM.Tests/Architecture/AdapterPatternArchitectureTests.cs` for automated enforcement.

## Reference Implementations

**Adapter Factory**:
- `Main/Adapters/IAdapterFactory.cs`
- `Main/Adapters/AdapterFactory.cs`

## Related ADRs

- **ADR-002**: Thin Entry Points - Entry points delegate to services (which use adapters)
- **ADR-005**: No #if DEBUG - All code paths must be testable (adapters enable this)
