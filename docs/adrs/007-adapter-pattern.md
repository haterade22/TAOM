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
- **Singletons**: `MBObjectManager`

### The Solution: Adapter Layer

```
Entry Point (Thin) -> Adapter Interfaces -> Service Layer
       |                     |                   |
GameModel/Behavior      IXxxAdapter        Fully Testable
```

**Architecture**:
1. **Entry Point**: Receives sealed game objects from framework events and converts them at the boundary
2. **Adapter Interface**: Mockable interface exposing needed properties/methods
3. **Service Layer**: Receives adapters, contains all business logic, 100% testable

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

### Entry Point Conversion Pattern

**Entry points MAY use sealed types** (they receive them from game framework):

```csharp
// CampaignBehavior - Entry Point (Thin)
public class PatrolCampaignBehavior : CampaignBehaviorBase
{
    private readonly IPatrolService _patrolService;

    public PatrolCampaignBehavior(IPatrolService patrolService)
    {
        _patrolService = patrolService;
    }

    private void OnSettlementEntered(MobileParty party, Settlement settlement)
    {
        // Sealed types from game framework

        // Convert to adapters at entry point (single line each), by a feature
        // factory or a static helper; there is no central factory
        var settlementAdapter = ToSettlementAdapter(settlement);
        var troopRosterAdapter = ToTroopRosterAdapter(CreateTroopRoster());

        // Service receives adapters only
        var patrol = _patrolService.CreatePatrol(settlementAdapter, troopRosterAdapter);

        // Use result (adapter returned)
        patrol.Position2D = settlement.GatePosition;
    }
}
```

**Key Points**:
- Entry point receives sealed types (unavoidable, they come from game events)
- Entry point wraps sealed types in adapters (through a feature factory or a static helper)
- Service receives only adapters
- Conversion is minimal (1 line per object)
- Entry point remains thin (<150 lines per ADR-002)

## Adapter Factory Pattern

There is no central factory. A new adapter is built at the boundary by a feature factory (`CareerHeroAdapterFactory`, `QuestHeroAdapterFactory`, `MissionAdapterFactory`, all under `Main/Adapters/`) or a static helper (`CultureFeatAdapter.FromOrNull`).

## Adapter Interface Design

Existing adapters under `Main/Adapters/` are the models to copy.

### Recursive Adapter Wrapping (CRITICAL)

An adapter's members return adapter interfaces, ids or value types, never a sealed type, because one sealed return breaks testing for the whole chain. Add only the members a feature needs.

### Reflection

Reflection on engine members belongs in an adapter, a boundary seam, or a Hooks patch, using `ReflectionHelper` or cached `AccessTools`. A service's decision logic never reflects.

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
- Convert sealed types to adapters (through a feature factory or a static helper)
- Keep conversion minimal (single line per object)
- Pass only adapters to services

### Protected-Virtual Boundary Seams (amendment, 2026-09-24)

A service may keep its engine access in `protected virtual` members of its own class, instead of
behind an adapter, when all four conditions hold:

1. **Seam signatures use ids, value types and TAOM types only**: string ids, primitives, enums,
   TAOM data classes, engine structs such as `Vec2` and `CampaignTime`, and the value-like types
   under "Value Types Don't Need Adapters"; never a sealed TaleWorlds class.
2. **Each seam body is one engine operation**: the engine calls or queries for one action, with
   their null guards and id lookups, and no TAOM decision logic.
3. **The service's interface stays engine-free**: callers still pass ids or adapters.
4. **A test subclass overrides every seam the tests reach**, so every decision path runs in unit
   tests without the game.

A private helper that only seams call, such as an id lookup (`FindParty`, `FindHero`), is part of
the seam body: condition 1 applies to the seam that calls it, not to the helper's own signature
(decision 55).

Why: `RefugeService`, `CampService`, `SupplyOrderService` and `WardenService` use this pattern,
tested through test subclasses (for example `RefugeService.MainPartyId()` and `TestableRefugeService`
in `RefugeServiceTests.cs`); the 2026-09-23 audit counted 94 seam members
(`plans/_audit/2026-09-23-opus/verify-a-batch-02.md`, ARCH-05). The adapter route for the same
coverage would need several new adapter interfaces per service for no behaviour gain. The cost is
that seam bodies are untested engine code inside a service, so they stay thin. The plan 021
review found four seams, written before this rule, that carry decision logic against condition 2
(`SupplyOrderService.ChargePlayer`, `RefugeService.FindNearestHostile`,
`WardenService.CompanionsInMainParty` and `WardenService.MintCompanionFromTroop`); plan 026 moves
that logic into the services (decision 56). A seam that breaks condition 2 is a finding wherever it
is found, not only in this list.

Reviewers: a seam that meets these conditions is not an ADR-007 violation. A seam that breaks one,
or engine use in a service outside a seam, still is. This exception also qualifies ADR-002 Service
Design Guideline 4 and ADR-008 Rule 1, which point here.


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
- Most common adapters already exist in `/Main/Adapters/`
- Conversion is mechanical: one line at the boundary, through a feature factory or a static helper
- Long-term benefits outweigh short-term overhead

## Enforcement

Enforcement: `/deep-review` lens 1 rule 1.

Review checklist for PRs touching the service layer:
- [ ] Service interfaces accept adapter interfaces (NOT sealed types)
- [ ] Service implementations use adapters throughout, or protected-virtual boundary seams that meet the Exceptions conditions
- [ ] Entry points convert sealed types to adapters
- [ ] New adapters are created at the boundary by a feature factory or static helper
- [ ] **Adapters recursively wrap ALL nested sealed types** (properties returning sealed types must return adapter interfaces)
- [ ] Tests use mocked adapters (NSubstitute), or a test subclass that overrides the service's boundary seams
- [ ] Tests mock nested adapter interfaces where properties are accessed

## Related ADRs

- **ADR-002**: Thin Entry Points - Entry points delegate to services (which use adapters)
- **ADR-005**: No #if DEBUG - All code paths must be testable (adapters enable this)

### Amendment (2026-11-03): no central factory, shorter samples

The central `IAdapterFactory` and `AdapterFactory` never existed in TAOM code, yet this ADR named them throughout, along with `ReflectionExtensions` and an architecture test file that is not in the repository. All of that left. Adapters are built at the boundary by a feature factory or a static helper, as "Adapter Factory Pattern" says. The recursive wrapping rule is unchanged but now takes a paragraph instead of 160 lines of invented samples. The reflection rule now names the real helpers and the places reflection may live. The testing samples and the six-phase migration plan also left, because `.claude/rules/tests.md` carries the workflow. The coverage threshold left with them; no gate checks one. Enforcement is review (lens 1 rule 1).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/013-server-safe-core-client-ui-submodule.md](./013-server-safe-core-client-ui-submodule.md)
- [docs/adrs/README.md](./README.md)
- [docs/ai-includes/taleworlds-research-guide.md](../ai-includes/taleworlds-research-guide.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
