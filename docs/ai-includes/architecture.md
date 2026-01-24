# Architecture Guide

Overview of the TAOM project architecture and design decisions.

---

## Architectural Overview

```
+------------------------------------------------------------------+
|                        ENTRY POINTS                               |
|  (Harmony Patches, GameModels, MissionLogics, CampaignBehaviors) |
|                        [THIN LAYER]                              |
+------------------------------------------------------------------+
                              |
                              | delegates to
                              v
+------------------------------------------------------------------+
|                         SERVICES                                  |
|      (Business Logic, Feature Implementation, Calculations)      |
|                    [CORE LOGIC LAYER]                            |
+------------------------------------------------------------------+
                              |
                              | uses
                              v
+------------------------------------------------------------------+
|                         ADAPTERS                                  |
|      (Wrap TaleWorlds sealed types for testability)              |
|                    [ABSTRACTION LAYER]                           |
+------------------------------------------------------------------+
                              |
                              | wraps
                              v
+------------------------------------------------------------------+
|                     TALEWORLDS ENGINE                            |
|           (Agent, Hero, Kingdom, CultureObject, etc.)            |
|                      [SEALED TYPES]                              |
+------------------------------------------------------------------+
```

---

## Layer Responsibilities

### 1. Entry Points (Thin Layer)

Entry points are the bridge between TaleWorlds and our code. They MUST be thin.

**Types of Entry Points:**
- **Harmony Patches** - Intercept game method calls
- **GameModels** - Override game calculations
- **MissionLogics** - Mission lifecycle hooks
- **CampaignBehaviors** - Campaign lifecycle hooks

**Rules for Entry Points (ADR-002):**
- Maximum 150 lines of code
- NO business logic (only delegation)
- NO complex conditions
- Convert sealed types to adapters immediately
- Delegate to services for all logic

```csharp
// GOOD: Thin entry point
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class AgentApplyDamageModel_CalculateDamage_Patch
{
    private static IAdapterFactory _factory => IoC.Resolve<IAdapterFactory>();

    public static void Postfix(ref float __result, Agent attacker, Agent victim)
    {
        // Convert sealed types to adapters immediately
        var attackerAdapter = _factory.GetAgentAdapter(attacker);
        var victimAdapter = _factory.GetAgentAdapter(victim);

        // Delegate to hooks (which delegate to services)
        foreach (var hook in IoC.ResolveAll<IOnCalculateDamage>())
        {
            hook.OnCalculateDamage(ref __result, attackerAdapter, victimAdapter);
        }
    }
}

// BAD: Fat entry point with business logic
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class BadPatch
{
    public static void Postfix(ref float __result, Agent attacker, Agent victim)
    {
        // ❌ Business logic in patch
        if (attacker.Character?.Culture?.StringId == "empire")
        {
            if (victim.Character?.Culture?.StringId == "sturgia")
            {
                __result *= 1.15f; // Empire vs Sturgia bonus
            }
        }
        // ❌ More logic...
    }
}
```

### 2. Services (Core Logic Layer)

Services contain all business logic. They are 100% unit testable.

**Rules for Services:**
- All dependencies injected via constructor
- Only use adapter interfaces, never sealed types (ADR-007)
- Single responsibility
- Fully unit testable with mocked adapters

```csharp
// Service interface
public interface ICultureBonusService
{
    float GetDamageModifier(IAgentAdapter attacker, IAgentAdapter victim);
}

// Service implementation
public class CultureBonusService : ICultureBonusService
{
    private readonly ICultureBonusEngine _engine;
    private readonly ICultureService _cultureService;

    public CultureBonusService(
        ICultureBonusEngine engine,
        ICultureService cultureService)
    {
        _engine = engine;
        _cultureService = cultureService;
    }

    public float GetDamageModifier(IAgentAdapter attacker, IAgentAdapter victim)
    {
        // All logic uses adapters - fully testable
        var attackerCulture = _cultureService.GetCulture(attacker);
        var victimCulture = _cultureService.GetCulture(victim);

        return _engine.CalculateModifier(attackerCulture, victimCulture);
    }
}
```

### 3. Adapters (Abstraction Layer)

Adapters wrap TaleWorlds sealed types to enable testing.

**Rules for Adapters (ADR-007):**
- One adapter interface per sealed type
- Methods return adapter interfaces or primitives
- Created by IAdapterFactory
- Never expose sealed types through interface
- Use null-conditional operators for computed properties

```csharp
// Adapter interface
public interface IAgentAdapter
{
    string Name { get; }
    bool IsActive();
    bool IsFadingOut();
    ICharacterAdapter Character { get; }
    ICultureAdapter Culture { get; }
}

// Adapter implementation
public class AgentAdapter : IAgentAdapter
{
    private readonly Agent _agent;
    private readonly IAdapterFactory _factory;

    public AgentAdapter(Agent agent, IAdapterFactory factory)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public string Name => _agent.Name ?? "Unknown";

    public bool IsActive() => _agent?.IsActive() ?? false;

    public bool IsFadingOut() => _agent?.IsFadingOut() ?? false;

    // CRITICAL: Use null-conditional for computed properties
    public ICultureAdapter Culture => _agent.Character?.Culture != null
        ? _factory.GetCultureAdapter(_agent.Character.Culture)
        : null;
}
```

---

## CRITICAL: Null-Conditional Operators for Computed Properties

When wrapping TaleWorlds properties, **ALWAYS** use null-conditional operators (`?.`) for computed properties that internally access nested objects.

### The Problem

Many TaleWorlds properties are computed - they access other properties internally. If intermediate objects are null, the crash occurs INSIDE the property getter before your null check executes.

```csharp
// Decompiled TaleWorlds code
public class PartyBase
{
    public IFaction MapFaction { get; }

    // Computed property - accesses MapFaction.Culture WITHOUT null check!
    public CultureObject Culture
    {
        get { return this.MapFaction.Culture; } // Crashes if MapFaction is null
    }
}
```

### The Solution

```csharp
// ❌ WRONG - Crashes when MapFaction is null
public ICultureAdapter Culture => _partyBase.Culture != null
    ? _factory.GetCultureAdapter(_partyBase.Culture)
    : null;

// ✅ CORRECT - Safe with null-conditional operator
public ICultureAdapter Culture => _partyBase.MapFaction?.Culture != null
    ? _factory.GetCultureAdapter(_partyBase.MapFaction.Culture)
    : null;
```

### When to Use

**ALWAYS** use `?.` when wrapping properties that:
- Are computed (property getter accesses other properties/fields)
- Decompiled TaleWorlds source shows NO null checks in getter
- Property "feels like" a convenience accessor

**Research Workflow:**
1. Decompile the sealed TaleWorlds class
2. Examine the property getter (not just signature)
3. Identify any intermediate property/field access
4. Use `?.` for properties with nested access

---

## Dependency Flow

```
Entry Point → Hook Interface → Service → Engine → Adapter
     ↓            ↓              ↓         ↓         ↓
   Thin      Orchestrate      Logic     Algorithm   Wrap
```

### Example Flow: Damage Calculation

1. **Harmony Patch** intercepts `CalculateDamage`
2. **Converts** `Agent` to `IAgentAdapter`
3. **Resolves** `IOnCalculateDamage` hooks
4. **Hook** builds context, calls `ICultureBonusService`
5. **Service** uses `ICultureBonusEngine` for calculation
6. **Engine** performs culture-based calculation
7. **Result** flows back through the chain

---

## Folder Structure

```
Main/
├── Adapters/                    # Adapter interfaces and implementations
│   ├── Interfaces/
│   │   ├── IAgentAdapter.cs
│   │   ├── IHeroAdapter.cs
│   │   └── ...
│   ├── Implementations/
│   │   ├── AgentAdapter.cs
│   │   ├── HeroAdapter.cs
│   │   └── ...
│   └── IAdapterFactory.cs
├── Core/                        # Core infrastructure
│   ├── IoC/
│   │   └── IoC.cs
│   ├── Logging/
│   │   └── ModLogger.cs
│   └── Configuration/
│       └── ModSettings.cs
├── Features/                    # Feature implementations
│   ├── PartySpeed/
│   │   ├── Hooks/
│   │   ├── Services/
│   │   └── Engines/
│   ├── CombatBonus/
│   │   ├── Hooks/
│   │   ├── Services/
│   │   └── Engines/
│   └── ...
├── Patches/                     # Harmony patches (thin entry points)
│   ├── Combat/
│   ├── Campaign/
│   └── Mission/
└── SubModule.cs                 # Entry point

TAOM.Tests/
├── Adapters/                    # Adapter tests
├── Features/                    # Feature tests (mirrors Main structure)
│   ├── PartySpeed/
│   └── CombatBonus/
└── TestUtilities/               # Test helpers and mocks
```

---

## Testing Strategy

### Unit Test Coverage Requirements (ADR-008)

| Component | Min Coverage | Test Location |
|-----------|-------------|---------------|
| Services | 100% | `TAOM.Tests/Features/` |
| Engines | 100% | `TAOM.Tests/Features/` |
| Adapters | Core methods | `TAOM.Tests/Adapters/` |
| Hooks | 80%+ | `TAOM.Tests/Features/` |
| Entry Points | Not testable | Manual verification |

### Test Patterns

```csharp
[TestClass]
public class CultureBonusServiceTests
{
    private ICultureBonusService _service;
    private ICultureBonusEngine _mockEngine;
    private ICultureService _mockCultureService;

    [TestInitialize]
    public void Setup()
    {
        _mockEngine = Substitute.For<ICultureBonusEngine>();
        _mockCultureService = Substitute.For<ICultureService>();
        _service = new CultureBonusService(_mockEngine, _mockCultureService);
    }

    [TestMethod]
    public void GetDamageModifier_WithValidCultures_ReturnsEngineResult()
    {
        // Arrange
        var mockAttacker = Substitute.For<IAgentAdapter>();
        var mockVictim = Substitute.For<IAgentAdapter>();

        _mockCultureService.GetCulture(mockAttacker).Returns("empire");
        _mockCultureService.GetCulture(mockVictim).Returns("sturgia");
        _mockEngine.CalculateModifier("empire", "sturgia").Returns(1.15f);

        // Act
        var result = _service.GetDamageModifier(mockAttacker, mockVictim);

        // Assert
        Assert.AreEqual(1.15f, result);
    }

    [TestMethod]
    public void GetDamageModifier_WithNullAttacker_ReturnsDefaultModifier()
    {
        // Arrange
        var mockVictim = Substitute.For<IAgentAdapter>();

        // Act
        var result = _service.GetDamageModifier(null, mockVictim);

        // Assert
        Assert.AreEqual(1.0f, result);
    }
}
```

---

## IoC Container (DryIoC)

### Registration Patterns

```csharp
public static class IoC
{
    private static Container _container;

    public static void Initialize()
    {
        _container = new Container();

        // Adapters
        _container.Register<IAdapterFactory, AdapterFactory>(Reuse.Singleton);

        // Services (Singleton - maintain state)
        _container.Register<ICultureBonusService, CultureBonusService>(Reuse.Singleton);
        _container.Register<IPartySpeedService, PartySpeedService>(Reuse.Singleton);

        // Engines (Singleton - stateless calculations)
        _container.Register<ICultureBonusEngine, CultureBonusEngine>(Reuse.Singleton);

        // Hooks (Transient - created per use)
        _container.Register<IOnCalculateDamage, CultureBonusDamageHook>(Reuse.Transient);

        // Multiple implementations
        _container.RegisterMany<ICultureFormationStrategy>(
            new[] { typeof(EmpireFormationStrategy), typeof(VlandiaFormationStrategy) },
            Reuse.Singleton);
    }

    public static T Resolve<T>() => _container.Resolve<T>();

    public static IEnumerable<T> ResolveAll<T>() => _container.ResolveMany<T>();
}
```

### Lifetime Guidelines

| Lifetime | Use For |
|----------|---------|
| Singleton | Services with state, caches, expensive objects |
| Transient | Hooks, stateless helpers, factories |
| Scoped | Per-request objects (rare in game mods) |

---

## Key Architectural Decisions

| ADR | Title | Impact |
|-----|-------|--------|
| ADR-001 | XML Configuration | All bonuses defined in XML |
| ADR-002 | Thin Entry Points | Max 150 lines, delegation only |
| ADR-003 | No Regions | Class decomposition instead |
| ADR-004 | No Obsolete | Complete migrations |
| ADR-005 | No Preprocessor | IoC-based environment |
| ADR-007 | Adapter Pattern | Testability for sealed types |
| ADR-008 | Testability | 100% service coverage |
| ADR-009 | Self-Documenting | Names over comments |

---

## Common Patterns Quick Reference

### Entry Point Pattern
```csharp
[HarmonyPatch(typeof(GameClass), "Method")]
public class GameClass_Method_Patch
{
    public static void Postfix(ref float __result, SealedType param)
    {
        var adapter = IoC.Resolve<IAdapterFactory>().GetAdapter(param);
        foreach (var hook in IoC.ResolveAll<IHookInterface>())
        {
            hook.OnMethod(ref __result, adapter);
        }
    }
}
```

### Service Pattern
```csharp
public class MyService : IMyService
{
    private readonly IDependency _dependency;

    public MyService(IDependency dependency)
    {
        _dependency = dependency;
    }

    public Result DoWork(IAdapter input)
    {
        // Business logic using adapter interfaces only
    }
}
```

### Adapter Pattern
```csharp
public interface IMyAdapter
{
    string Property { get; }
    IChildAdapter Child { get; }
}

public class MyAdapter : IMyAdapter
{
    private readonly SealedType _wrapped;

    public string Property => _wrapped.Property;

    // Use ?. for computed properties
    public IChildAdapter Child => _wrapped.Parent?.Child != null
        ? new ChildAdapter(_wrapped.Parent.Child)
        : null;
}
```

---

## Anti-Patterns to Avoid

1. **Business logic in entry points** - Delegate to services
2. **Sealed types in service signatures** - Use adapters
3. **Direct TaleWorlds access in tests** - Mock adapters
4. **God classes** - Single responsibility
5. **Hidden dependencies** - Constructor injection
6. **Regular null checks for computed properties** - Use `?.`

---

## Related Guides

- [patterns.md](./patterns.md) - Design patterns (Hook, Strategy, Builder)
- [tdd-enforcement.md](./tdd-enforcement.md) - Test-driven development workflow
- [testing-guide.md](./testing-guide.md) - Testing patterns and examples
- [code-quality.md](./code-quality.md) - Clean code principles
- [taleworlds-research-guide.md](./taleworlds-research-guide.md) - TaleWorlds investigation
