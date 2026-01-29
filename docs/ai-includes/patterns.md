# Design Patterns Guide

## Core Patterns

This project follows three core patterns that work together to create a testable, maintainable architecture.

### 1. Hook Pattern + Harmony Integration

Separation between game integration and business logic.

```csharp
// Hook Interface
public interface IOnAgentApplyDamageModelCalculateDamage
{
    void OnAgentApplyDamageModelCalculateDamage(
        ref float __result,
        Agent attacker,
        Agent victim,
        WeaponComponentData weaponData
    );
}

// Hook Implementation (delegates to strategy)
public class CultureBonusDamageHook : IOnAgentApplyDamageModelCalculateDamage
{
    private readonly ICultureBonusEngine _bonusEngine; // Strategy pattern

    public CultureBonusDamageHook(ICultureBonusEngine bonusEngine)
    {
        _bonusEngine = bonusEngine;
    }

    public void OnAgentApplyDamageModelCalculateDamage(
        ref float __result,
        Agent attacker,
        Agent victim,
        WeaponComponentData weaponData)
    {
        // Fluent context building
        var context = new BonusContext
        {
            AttackerCulture = GetCulture(attacker),
            DefenderCulture = GetCulture(victim)
        }.ToLowerInvariantAllStrings();

        // Delegate to strategy
        _bonusEngine.ApplyBonuses(ref __result, context, BonusType.MeleeAttack);
    }
}

// Harmony Patch (thin, delegates to hook)
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class AgentApplyDamageModel_CalculateDamage_Patch
{
    static void Postfix(
        ref float __result,
        Agent attacker,
        Agent victim,
        WeaponComponentData weaponData)
    {
        var hooks = IoC.ResolveAll<IOnAgentApplyDamageModelCalculateDamage>();
        foreach (var hook in hooks)
        {
            hook.OnAgentApplyDamageModelCalculateDamage(ref __result, attacker, victim, weaponData);
        }
    }
}

// IoC Registration
container.Register<IOnAgentApplyDamageModelCalculateDamage, CultureBonusDamageHook>(Reuse.Transient);
```

#### Transpiler Implementation Note

TAOM uses manual `List<CodeInstruction>` iteration for transpilers (see `RefreshCharacterEntityAuxPatch.cs`). Harmony 2.4.2 (shipped with Bannerlord 1.3) includes an expanded **CodeMatcher** API with methods like `RemoveSearchForward`, `InsertAfter`, and `Do` that can simplify transpiler code. When writing new transpilers, evaluate whether CodeMatcher provides a cleaner approach than manual list iteration. For simple insertion-after-match scenarios, manual iteration remains acceptable.

### 2. Strategy Pattern

Algorithm family with interchangeable implementations.

```csharp
// Strategy Interface
public interface IPaymentStrategy
{
    void Pay(decimal amount);
}

// Concrete Strategy: Credit Card
public class CreditCardStrategy : IPaymentStrategy
{
    private readonly string _cardNumber;

    public CreditCardStrategy(string cardNumber)
    {
        _cardNumber = cardNumber;
    }

    public void Pay(decimal amount)
    {
        Console.WriteLine($"Paid ${amount} by credit card {_cardNumber}");
    }
}

// Concrete Strategy: PayPal
public class PayPalStrategy : IPaymentStrategy
{
    private readonly string _email;

    public PayPalStrategy(string email)
    {
        _email = email;
    }

    public void Pay(decimal amount)
    {
        Console.WriteLine($"Paid ${amount} via PayPal to {_email}");
    }
}

// Context using Strategy
public class PaymentProcessor
{
    private readonly IPaymentStrategy _strategy;

    public PaymentProcessor(IPaymentStrategy strategy)
    {
        _strategy = strategy;
    }

    public void ProcessPayment(decimal amount)
    {
        _strategy.Pay(amount);
    }
}

// Usage
var processor1 = new PaymentProcessor(new CreditCardStrategy("1234-5678"));
processor1.ProcessPayment(100); // Paid $100 by credit card 1234-5678

var processor2 = new PaymentProcessor(new PayPalStrategy("user@example.com"));
processor2.ProcessPayment(50); // Paid $50 via PayPal to user@example.com
```

#### Real-World Example: Damage Calculation

```csharp
// Strategy Interface
public interface IDamageCalculationStrategy
{
    int Calculate(int baseDamage, ICombatContext context);
}

// Default Strategy
public class DamageCalculationStrategy : IDamageCalculationStrategy
{
    private readonly ICultureBonusEngine _cultureBonusEngine;

    public DamageCalculationStrategy(ICultureBonusEngine cultureBonusEngine)
    {
        _cultureBonusEngine = cultureBonusEngine;
    }

    public int Calculate(int baseDamage, ICombatContext context)
    {
        var result = (float)baseDamage;

        // Apply culture bonuses
        var bonusContext = new BonusContext
        {
            AttackerCulture = context.AttackerCulture,
            DefenderCulture = context.DefenderCulture
        }.ToLowerInvariantAllStrings();

        _cultureBonusEngine.ApplyBonuses(ref result, bonusContext, BonusType.MeleeAttack);

        return (int)result;
    }
}

// IoC Registration
container.Register<IDamageCalculationStrategy, DamageCalculationStrategy>(Reuse.Singleton);
```

#### Real-World Example: CultureAI Formation Strategies

Culture-specific formation AI with one strategy per culture, orchestrated by a service.

```csharp
// Strategy Interface
public interface ICultureFormationStrategy
{
    string CultureName { get; }

    float ModifyBehaviorWeight(
        IFormationAdapter formation,
        BehaviorType behaviorType,
        float originalWeight,
        ITacticalContext context);
}

// Concrete Strategy: Vlandia (Aggressive Cavalry)
public class VlandiaFormationStrategy : ICultureFormationStrategy
{
    public string CultureName => "vlandia";

    public float ModifyBehaviorWeight(
        IFormationAdapter formation,
        BehaviorType behaviorType,
        float originalWeight,
        ITacticalContext context)
    {
        if (context.IsCavalryFormation)
            return ApplyCavalryModifiers(behaviorType, originalWeight, context);

        return originalWeight;
    }

    private float ApplyCavalryModifiers(BehaviorType behaviorType, float originalWeight, ITacticalContext context)
    {
        return behaviorType switch
        {
            BehaviorType.Charge when context.EnemyIsInfantry => originalWeight * 1.6f,  // Aggressive vs infantry
            BehaviorType.Flank => originalWeight * 1.7f,  // Strong flanking preference
            BehaviorType.Skirmish => originalWeight * 0.7f,  // Avoid skirmishing
            _ => originalWeight
        };
    }
}

// Concrete Strategy: Empire (Disciplined Defense)
public class EmpireFormationStrategy : ICultureFormationStrategy
{
    public string CultureName => "empire";

    public float ModifyBehaviorWeight(
        IFormationAdapter formation,
        BehaviorType behaviorType,
        float originalWeight,
        ITacticalContext context)
    {
        if (context.IsInfantryFormation)
            return ApplyInfantryModifiers(behaviorType, originalWeight, context);

        return originalWeight;
    }

    private float ApplyInfantryModifiers(BehaviorType behaviorType, float originalWeight, ITacticalContext context)
    {
        return behaviorType switch
        {
            BehaviorType.DefensiveLine when context.HasShield => originalWeight * 1.6f,  // Shield wall specialists
            BehaviorType.Charge when context.CurrentPhase == BattlePhase.Early => originalWeight * 0.6f,  // Wait for right moment
            BehaviorType.TacticalPosition => originalWeight * 1.5f,  // Coordinated positioning
            _ => originalWeight
        };
    }
}

// Concrete Strategy: Khuzait (Horse Archer Swarm)
public class KhuzaitFormationStrategy : ICultureFormationStrategy
{
    public string CultureName => "khuzait";

    public float ModifyBehaviorWeight(
        IFormationAdapter formation,
        BehaviorType behaviorType,
        float originalWeight,
        ITacticalContext context)
    {
        if (context.IsHorseArcherFormation)
            return ApplyHorseArcherModifiers(behaviorType, originalWeight, context);

        return originalWeight;
    }

    private float ApplyHorseArcherModifiers(BehaviorType behaviorType, float originalWeight, ITacticalContext context)
    {
        return behaviorType switch
        {
            BehaviorType.Skirmish => originalWeight * 1.8f,  // Hit and run specialists
            BehaviorType.Flank => originalWeight * 1.5f,  // Flanking preference
            BehaviorType.Charge => originalWeight * 0.5f,  // Avoid direct charges
            _ => originalWeight
        };
    }
}

// Service: Strategy Orchestrator
public class CultureFormationAIService : ICultureFormationAIService
{
    private readonly ITacticalContextBuilder _contextBuilder;
    private readonly Dictionary<string, ICultureFormationStrategy> _strategies;

    public CultureFormationAIService(
        ITacticalContextBuilder contextBuilder,
        IEnumerable<ICultureFormationStrategy> strategies)  // ← All strategies auto-injected
    {
        _contextBuilder = contextBuilder;

        // Build strategy dictionary keyed by culture name
        _strategies = strategies.ToDictionary(
            s => s.CultureName,
            s => s,
            StringComparer.OrdinalIgnoreCase);
    }

    public float ModifyBehaviorWeight(
        IFormationAdapter formation,
        BehaviorType behaviorType,
        float originalWeight)
    {
        var culture = formation.GetDominantCulture();
        if (!_strategies.TryGetValue(culture, out var strategy))
            return originalWeight;  // No strategy for this culture

        var context = _contextBuilder.BuildContext(formation);
        return strategy.ModifyBehaviorWeight(formation, behaviorType, originalWeight, context);
    }
}

// IoC Registration (all strategies auto-injected into service)
container.Register<ICultureFormationStrategy, VlandiaFormationStrategy>(Reuse.Singleton, serviceKey: "vlandia");
container.Register<ICultureFormationStrategy, EmpireFormationStrategy>(Reuse.Singleton, serviceKey: "empire");
container.Register<ICultureFormationStrategy, KhuzaitFormationStrategy>(Reuse.Singleton, serviceKey: "khuzait");
// ... more cultures to be registered

container.Register<ICultureFormationAIService>(
    made: Made.Of(() => new CultureFormationAIService(
        Arg.Of<ITacticalContextBuilder>(),
        Arg.Of<ICultureFormationStrategy[]>())),  // ← DryIoC auto-resolves all registered strategies
    reuse: Reuse.Singleton);

// Usage: Harmony Patch delegates to service
[HarmonyPatch(typeof(BehaviorComponent), "GetAIWeight")]
public static class BehaviorComponent_GetAIWeight_Patch
{
    [HarmonyPostfix]
    public static void Postfix(BehaviorComponent __instance, ref float __result)
    {
        var formationAdapter = _adapterFactory.GetFormationAdapter(__instance.Formation);
        var behaviorType = BehaviorTypeMapper.GetBehaviorType(__instance.GetType().FullName);

        __result = _aiService.ModifyBehaviorWeight(formationAdapter, behaviorType, __result);
    }
}
```

**Why Strategy Pattern for CultureAI**:
1. **Open/Closed Principle**: Adding new cultures = new strategy class, no changes to service
2. **Multiple Cultures**: Each culture gets its own strategy (Vlandia, Empire, Khuzait, Sturgia, Aserai, Battania)
3. **DryIoC Auto-Injection**: `Arg.Of<ICultureFormationStrategy[]>()` resolves all registered strategies automatically
4. **Dictionary Lookup**: O(1) culture → strategy mapping at runtime
5. **100% Testable**: Each strategy independently testable with mocked `ITacticalContext`

### 3. Builder Pattern + Fluent APIs

Complex object construction with method chaining.

```csharp
// Pizza Builder
public class PizzaBuilder
{
    private Pizza _pizza = new Pizza();

    public PizzaBuilder WithDough(string type)
    {
        _pizza.Dough = type;
        return this;
    }

    public PizzaBuilder WithSauce(string type)
    {
        _pizza.Sauce = type;
        return this;
    }

    public PizzaBuilder AddTopping(string topping)
    {
        _pizza.Toppings.Add(topping);
        return this;
    }

    public PizzaBuilder WithSize(PizzaSize size)
    {
        _pizza.Size = size;
        return this;
    }

    public Pizza Build()
    {
        return _pizza;
    }
}

// Usage
var pizza = new PizzaBuilder()
    .WithDough("thin")
    .WithSauce("tomato")
    .AddTopping("cheese")
    .AddTopping("pepperoni")
    .WithSize(PizzaSize.Large)
    .Build();
```

#### Real-World Example: Bonus Context

```csharp
// Fluent Extension Methods
public static class BonusContextExtensions
{
    public static BonusContext ToLowerInvariantAllStrings(this BonusContext context)
    {
        context.AttackerCulture = context.AttackerCulture?.ToLowerInvariant();
        context.DefenderCulture = context.DefenderCulture?.ToLowerInvariant();
        context.WeaponType = context.WeaponType?.ToLowerInvariant();
        return context;
    }

    public static BonusContext WithAttacker(this BonusContext context, string culture)
    {
        context.AttackerCulture = culture;
        return context;
    }

    public static BonusContext WithDefender(this BonusContext context, string culture)
    {
        context.DefenderCulture = culture;
        return context;
    }

    public static BonusContext WithWeapon(this BonusContext context, string weaponType)
    {
        context.WeaponType = weaponType;
        return context;
    }
}

// Usage
var context = new BonusContext()
    .WithAttacker("empire")
    .WithDefender("sturgia")
    .WithWeapon("sword")
    .ToLowerInvariantAllStrings();

// Or using object initializer + fluent
var context2 = new BonusContext
{
    AttackerCulture = "Empire",
    DefenderCulture = "Sturgia",
    WeaponType = "Sword"
}.ToLowerInvariantAllStrings();
```

## Pattern Integration

The three patterns work together:

```
Hook Pattern → Strategy Pattern → Builder/Fluent APIs

[HarmonyPatch] → IHookInterface → IStrategy → Fluent Context
     ↓               ↓                ↓            ↓
 Thin Layer    Orchestration    Algorithm    Object Building
```

### Complete Example

```csharp
// 1. BUILDER: Fluent Context Building
var context = new BonusContext
{
    AttackerCulture = "Empire",
    DefenderCulture = "Sturgia"
}.ToLowerInvariantAllStrings();

// 2. STRATEGY: Algorithm Implementation
public class CultureBonusEngine : ICultureBonusEngine
{
    public void ApplyBonuses(ref float result, BonusContext context, BonusType bonusType)
    {
        // Strategy algorithm...
    }
}

// 3. HOOK: Game Integration
public class CultureBonusDamageHook : IOnAgentApplyDamageModelCalculateDamage
{
    private readonly ICultureBonusEngine _bonusEngine;

    public void OnAgentApplyDamageModelCalculateDamage(ref float __result, ...)
    {
        // Build context (BUILDER)
        var context = new BonusContext { ... }.ToLowerInvariantAllStrings();

        // Delegate to strategy (STRATEGY)
        _bonusEngine.ApplyBonuses(ref __result, context, BonusType.MeleeAttack);
    }
}

// 4. HARMONY PATCH: Thin Entry Point
[HarmonyPatch(typeof(AgentApplyDamageModel), "CalculateDamage")]
public class AgentApplyDamageModel_CalculateDamage_Patch
{
    static void Postfix(ref float __result, ...)
    {
        // Delegate to hooks (HOOK)
        var hooks = IoC.ResolveAll<IOnAgentApplyDamageModelCalculateDamage>();
        foreach (var hook in hooks)
        {
            hook.OnAgentApplyDamageModelCalculateDamage(ref __result, ...);
        }
    }
}
```

## IoC Registration Patterns

### Singleton
Use for services that maintain state or are expensive to create:
```csharp
container.Register<ICultureBonusEngine, CultureBonusEngine>(Reuse.Singleton);
container.Register<IModLogger, ModLogger>(Reuse.Singleton);
```

### Transient
Use for stateless services or hooks that are called frequently:
```csharp
container.Register<IOnAgentApplyDamageModelCalculateDamage, CultureBonusDamageHook>(Reuse.Transient);
container.Register<IDamageCalculationStrategy, DamageCalculationStrategy>(Reuse.Transient);
```

### Scoped (rarely used)
Use for services that need to be created per-request but shared within that request:
```csharp
container.Register<ICampaignContext, CampaignContext>(Reuse.InWebRequest);
```

## Common Anti-Patterns to Avoid

### ❌ Direct Harmony Logic
```csharp
[HarmonyPatch(typeof(SomeClass), "Method")]
class BadPatch
{
    static void Postfix(ref float __result)
    {
        if (condition)
        {
            __result *= 1.5f; // Business logic in patch - WRONG!
        }
    }
}
```

### ✅ Hook Delegation
```csharp
[HarmonyPatch(typeof(SomeClass), "Method")]
class GoodPatch
{
    static void Postfix(ref float __result)
    {
        var hooks = IoC.ResolveAll<IOnSomeMethodHook>();
        foreach (var hook in hooks)
        {
            hook.OnSomeMethod(ref __result);
        }
    }
}
```

### ❌ Property Override + Method Delegation

**Problem**: Overriding properties while delegating methods to base model
```csharp
public class BadGameModel : SettlementFoodModel
{
    private readonly SettlementFoodModel _baseModel;

    // Override property
    public override int MyValue => 150; // ❌ IGNORED!

    // Delegate method
    public override int Calculate(Town town)
    {
        // Base model uses ITS MyValue (100), not our override (150)
        return _baseModel.Calculate(town); // ❌ Wrong value used
    }
}
```

### ✅ Full Method Reimplementation

**Solution**: Reimplement methods that use overridden properties
```csharp
public class GoodGameModel : SettlementFoodModel
{
    public override int MyValue => 150;

    public override int Calculate(Town town)
    {
        // Directly use OUR property
        int value = MyValue; // ✓ Uses 150

        // Reimplement calculation logic
        return town.Prosperity / value;
    }
}
```

**Why**: C# method delegation preserves base object's `this` context. Property lookups resolve on delegated object, not derived instance.

### ❌ Monolithic Classes
```csharp
// 500-line mega engine with mixed responsibilities - WRONG!
public class MegaEngine
{
    public void DoCombat() { /* 100 lines */ }
    public void DoEconomy() { /* 100 lines */ }
    public void DoAI() { /* 100 lines */ }
    public void DoUI() { /* 100 lines */ }
    public void DoPhysics() { /* 100 lines */ }
}
```

### ✅ Focused Strategies
```csharp
// Single-responsibility implementations
public class CombatStrategy : ICombatStrategy { /* 50 lines */ }
public class EconomyStrategy : IEconomyStrategy { /* 50 lines */ }
public class AIStrategy : IAIStrategy { /* 50 lines */ }
```

### ❌ Using `object` in Hook Interfaces (ADR-007 Violation)
```csharp
// WRONG - Uses object parameters for TaleWorlds sealed types
public interface IOnBehaviorGetAiWeight
{
    void OnBehaviorGetAiWeight(
        ref float weight,
        string behaviorType,
        object formation,      // ❌ Not testable!
        object teamAI          // ❌ Requires reflection mocking
    );
}
```

### ✅ Using Adapter Interfaces in Hooks (ADR-007 Compliant)
```csharp
// CORRECT - Uses adapter interfaces
public interface IOnBehaviorGetAiWeight
{
    void OnBehaviorGetAiWeight(
        ref float weight,
        string behaviorType,
        IFormationAdapter formation,           // ✅ Mockable!
        ITeamAISiegeComponentAdapter teamAI    // ✅ Testable
    );
}

// Harmony patch converts sealed types to adapters
[HarmonyPatch(typeof(BehaviorAssaultWalls), "GetAiWeight")]
public class BehaviorAssaultWalls_GetAiWeight_Patch
{
    private static IAdapterFactory _factory => IoC.Resolve<IAdapterFactory>();

    public static void Postfix(ref float __result, BehaviorAssaultWalls __instance)
    {
        // ✅ Patch layer handles type conversion
        var formationAdapter = _factory.GetFormationAdapter(__instance.Formation);
        var teamAIAdapter = _factory.CreateTeamAISiegeComponentAdapter(
            __instance.Formation?.Team?.TeamAI);

        foreach (var hook in IoC.ResolveAll<IOnBehaviorGetAiWeight>())
        {
            hook.OnBehaviorGetAiWeight(ref __result, "AssaultWalls",
                formationAdapter, teamAIAdapter);
        }
    }
}
```

**Why**: Using `object` parameters makes hooks untestable. Adapter interfaces enable clean mocking:

```csharp
// ✅ Easy to test with adapter mocks
var mockFormation = Substitute.For<IFormationAdapter>();
mockFormation.GetDominantCulture().Returns("empire");

_hook.OnBehaviorGetAiWeight(ref weight, "AssaultWalls", mockFormation, mockTeamAI);
```

**Rule**: The Harmony patch is the ONLY place that should touch TaleWorlds sealed types. All business logic operates on adapter interfaces.

### ❌ Primitive Construction
```csharp
// Manual property assignment line-by-line - WRONG!
var context = new BonusContext();
context.AttackerCulture = "empire";
context.DefenderCulture = "sturgia";
context.WeaponType = "sword";
context.AttackerCulture = context.AttackerCulture.ToLowerInvariant();
context.DefenderCulture = context.DefenderCulture.ToLowerInvariant();
context.WeaponType = context.WeaponType.ToLowerInvariant();
```

### ✅ Fluent Building
```csharp
var context = new BonusContext
{
    AttackerCulture = "empire",
    DefenderCulture = "sturgia",
    WeaponType = "sword"
}.ToLowerInvariantAllStrings();
```

### ❌ Using Regions
```csharp
public class BadClass
{
    #region Fields
    private int _field1;
    private int _field2;
    #endregion

    #region Properties
    public int Property1 { get; set; }
    #endregion

    #region Methods
    public void Method1() { }
    #endregion
}
```

### ✅ Class Decomposition
```csharp
// Split into focused classes with single responsibilities
public class DataClass
{
    public int Field1 { get; }
    public int Field2 { get; }
}

public class BehaviorClass
{
    private readonly DataClass _data;

    public void Method1() { }
}
```

### ❌ Obsolete Markers
```csharp
[Obsolete("Use NewMethod instead")]
public void OldMethod() { }

public void NewMethod() { }
```

### ✅ Complete Migration
```csharp
// Migrate ALL usage sites immediately, remove old implementation
public void NewMethod() { }
```

### ❌ Preprocessor Directives
```csharp
public void DoSomething()
{
#if DEBUG
    Console.WriteLine("Debug mode");
#endif
    // Business logic...
}
```

### ✅ DI-Based Environment Behavior
```csharp
public class DebugService : IDebugService
{
    public void Log(string message) => Console.WriteLine(message);
}

public class NoOpDebugService : IDebugService
{
    public void Log(string message) { }
}

// In IoC.cs (ONLY place preprocessor directives allowed)
#if DEBUG
container.Register<IDebugService, DebugService>(Reuse.Singleton);
#else
container.Register<IDebugService, NoOpDebugService>(Reuse.Singleton);
#endif
```

## Thread-Local State Pattern for Multi-Patch Coordination

### When to Use

Use this pattern when:
- Multiple Harmony patches need to coordinate and share context
- A Prefix patch needs to pass data to a Postfix patch
- One patch sets context that another patch consumes
- No GameModel override point exists for the desired behavior
- Patches operate on related but separate method calls

### Pattern Structure

```csharp
// Patch 1: Context Capture - Stores data in thread-local storage
[HarmonyPatch(typeof(SomeGameClass), "MethodWithParameters")]
public class ContextCapture_Patch
{
    /// <summary>
    /// Thread-local storage for sharing context between patches.
    /// Each thread has its own copy, preventing race conditions.
    /// </summary>
    [ThreadStatic]
    private static SomeContext? _currentContext;

    /// <summary>
    /// Public accessor for other patches to read the context.
    /// </summary>
    public static SomeContext? CurrentContext => _currentContext;

    /// <summary>
    /// Prefix: Capture parameters and store in thread-local storage.
    /// </summary>
    public static void Prefix(ParamType param1, ParamType param2, ...)
    {
        // Store parameters for later consumption
        _currentContext = new SomeContext(param1, param2);
    }

    /// <summary>
    /// Postfix: CRITICAL - Always cleanup thread-local storage.
    /// </summary>
    public static void Postfix()
    {
        // MUST set to null to prevent data leaking to unrelated operations
        _currentContext = null;
    }
}

// Patch 2: Context Consumer - Reads from thread-local storage
[HarmonyPatch(typeof(OtherGameClass), "RelatedMethod")]
public class ContextConsumer_Patch
{
    /// <summary>
    /// Prefix: Use context from thread-local storage to make decisions.
    /// Returns false to skip original method execution.
    /// </summary>
    public static bool Prefix()
    {
        // Retrieve context from thread-local storage
        var context = ContextCapture_Patch.CurrentContext;

        // CRITICAL: Always check for null (safety)
        if (context == null)
        {
            return true; // No context available, allow normal processing
        }

        // Use context to make decisions
        var hooks = IoC.ResolveAll<ISomeHook>();
        foreach (var hook in hooks)
        {
            if (!hook.ShouldProceed(context))
            {
                return false; // Block original method execution
            }
        }

        return true; // Allow normal processing
    }
}
```

### Critical Rules

1. **Always Cleanup in Postfix**: Set thread-static fields to `null` in Postfix to prevent data leaking across unrelated operations
2. **Null Safety**: Consumer patches MUST check for null context before use
3. **Thread Isolation**: `[ThreadStatic]` ensures each thread has its own copy (safe for parallel execution)
4. **Minimal Scope**: Only use when patches MUST coordinate; prefer simpler patterns when possible
5. **Public Accessor**: Provide read-only property for other patches to access context
6. **Documentation**: Document the coordination between patches clearly

### Real-World Example: Execution System

The execution system demonstrates this pattern for blocking honor penalties based on character alignment:

**Problem**: `TraitLevelingHelper.OnLordExecuted()` applies -1000 honor penalty but has NO parameters (can't know victim/killer). The method is called AFTER `KillCharacterAction.ApplyInternal(victim, killer)` which HAS the parameters we need.

**Solution**: Use thread-local storage to pass victim/killer from one patch to another.

```csharp
// Step 1: Capture execution context
[HarmonyPatch(typeof(KillCharacterAction), "ApplyInternal")]
public class KillCharacterAction_ApplyInternal_Patch
{
    [ThreadStatic]
    private static Hero? _currentExecutionVictim;

    [ThreadStatic]
    private static Hero? _currentExecutionKiller;

    public static Hero? CurrentExecutionVictim => _currentExecutionVictim;
    public static Hero? CurrentExecutionKiller => _currentExecutionKiller;

    public static void Prefix(Hero victim, Hero? killer, bool showNotification)
    {
        // Store execution context
        _currentExecutionVictim = victim;
        _currentExecutionKiller = killer;
    }

    public static void Postfix()
    {
        // CRITICAL: Cleanup to prevent leaking to unrelated kills
        _currentExecutionVictim = null;
        _currentExecutionKiller = null;
    }
}

// Step 2: Consume context to block honor penalties
[HarmonyPatch(typeof(TraitLevelingHelper), "OnLordExecuted")]
public class TraitLevelingHelper_OnLordExecuted_Patch
{
    public static bool Prefix()
    {
        // Get context from thread-local storage
        var victim = KillCharacterAction_ApplyInternal_Patch.CurrentExecutionVictim;
        var killer = KillCharacterAction_ApplyInternal_Patch.CurrentExecutionKiller;

        // Safety check
        if (victim == null || killer == null)
        {
            return true; // No context, allow normal penalty
        }

        // Check if alignment-based filtering should block penalty
        var hooks = IoC.ResolveAll<IOnCharacterExecuted>();
        foreach (var hook in hooks)
        {
            if (!hook.ShouldApplyHonorPenalty(victim, killer))
            {
                return false; // Block -1000 honor penalty
            }
        }

        return true; // Allow normal penalty
    }
}
```

### When NOT to Use

- **GameModel exists**: Use GameModel override instead (see [architecture.md](architecture.md))
- **Single patch suffices**: Don't add complexity if one patch can handle everything
- **Parameters available**: If the method you're patching has the data you need, use it directly
- **Global state acceptable**: If data should persist beyond single operation, use service-level state

## Cache Pattern for Performance Optimization

### When to Use

Use caching when:
- Operation is called frequently (hot paths: combat, AI, per-frame)
- Underlying operation is expensive (dictionary lookups, string operations, reflection)
- Data is relatively stable within a scope (mission, campaign)
- Thread safety is required for concurrent access

### Cache Pattern Structure

```csharp
// Interface
public interface IMyCache
{
    TValue Get(TKey key);
    void Clear();
}

// Implementation with ConcurrentDictionary
public class MyCache : IMyCache
{
    private readonly ConcurrentDictionary<TKey, TValue> _cache = new();
    private readonly IExpensiveService _service;

    public MyCache(IExpensiveService service)
    {
        _service = service;
    }

    public TValue Get(TKey key)
    {
        return _cache.GetOrAdd(key, k => _service.ExpensiveOperation(k));
    }

    public void Clear()
    {
        _cache.Clear();
    }
}

// IoC Registration
container.Register<IMyCache, MyCache>(Reuse.Singleton);
```

### Critical Rules

1. **Thread Safety**: Always use `ConcurrentDictionary` for caches accessed from multiple threads
   ```csharp
   // ✓ Thread-safe
   private readonly ConcurrentDictionary<K, V> _cache = new();

   // ✗ Not thread-safe
   private readonly Dictionary<K, V> _cache = new();
   ```

2. **Null Safety**: Check for null inputs before caching
   ```csharp
   if (key == null) return defaultValue;
   ```

3. **Cleanup**: Clear mission-scoped caches when missions end
   ```csharp
   public void ClearMissionScopedCache() { _missionCache.Clear(); }
   ```

4. **Singleton Lifetime**: Register caches as singletons for shared access
   ```csharp
   container.Register<ICache, Cache>(Reuse.Singleton);
   ```

5. **LRU Eviction**: Never clear ALL cache entries on overflow - use LRU eviction instead
   ```csharp
   // ✗ ANTI-PATTERN: Full cache clear causes thrashing
   if (_cache.Count > MaxSize) _cache.Clear();

   // ✓ CORRECT: Remove oldest 25% on overflow
   if (_cache.Count > MaxSize)
       RemoveOldestEntries(_cache.Count / 4);
   ```

6. **Avoid O(n) in Hot Paths**: Never iterate all entities in frequently-called methods
   ```csharp
   // ✗ ANTI-PATTERN: O(n) per call × thousands of calls
   return Campaign.Current.Factions.Count(f => IsAllied(f));

   // ✓ CORRECT: Cache computed metrics, invalidate on changes
   return _factionMetricsCache.GetAlliedCount(faction);
   ```

## Defensive Validity Checking Pattern for Adapters

### Purpose

When adapters wrap volatile game objects (agents, missions, formations), implement validity checks before accessing properties that may become invalid during gameplay. This pattern prevents memory corruption and crashes when accessing disposed or dead game objects.

### Pattern Structure

```csharp
public interface IAgentAdapter
{
    bool IsActive();
    bool IsFadingOut();
    IAgentVisualsAdapter AgentVisuals { get; }
}

public class AgentAdapter : IAgentAdapter
{
    private readonly Agent _agent;
    private readonly IAdapterFactory _factory;

    public bool IsActive() => _agent?.IsActive() ?? false;
    public bool IsFadingOut() => _agent?.IsFadingOut() ?? false;

    // Defensive check before accessing property that may cause memory corruption
    public IAgentVisualsAdapter AgentVisuals =>
        _agent != null && _agent.IsActive() && !_agent.IsFadingOut() && _agent.AgentVisuals != null
            ? new AgentVisualsAdapter(_agent.AgentVisuals)
            : null; // Return null for invalid agents instead of crashing
}
```

## Null-Conditional Operator Pattern for Computed Properties

### Purpose

When wrapping TaleWorlds properties in adapters, use null-conditional operators (`?.`) for **computed properties** that internally access nested objects without null checks.

**Real-World Bug - PartyBase.Culture**:

```csharp
// Decompiled TaleWorlds.CampaignSystem.Party.PartyBase
public class PartyBase
{
    public IFaction MapFaction { get; } // Can be null for bandit parties!

    // Computed property - accesses MapFaction.Culture WITHOUT null check
    public CultureObject Culture
    {
        get { return this.MapFaction.Culture; } // ❌ Crashes if MapFaction is null!
    }
}
```

**Why Traditional Null Checking Fails**:

```csharp
// ❌ WRONG - This CRASHES when MapFaction is null
public ICultureAdapter Culture => _partyBase.Culture != null
    ? _factory.GetCultureAdapter(_partyBase.Culture)
    : null;

// ✅ CORRECT - Safe with null-conditional operator
public ICultureAdapter Culture => _partyBase.MapFaction?.Culture != null
    ? _factory.GetCultureAdapter(_partyBase.MapFaction.Culture)
    : null;
```

## Anti-Patterns: Common Null Safety Mistakes

These patterns have caused production bugs and **MUST be avoided**.

### 1. FirstOrDefault with Null-Forgiving Operator

```csharp
// ❌ BAD - Silently injects null, crashes at unknown location
var kingdom = kingdoms.FirstOrDefault(k => k.StringId == id)!;

// ✅ GOOD - Explicit null check with early return
var kingdom = kingdoms.FirstOrDefault(k => k.StringId == id);
if (kingdom == null)
{
    _logger.LogWarning($"Kingdom '{id}' not found, skipping");
    continue;  // or return
}
```

### 2. Direct Dictionary Access Without Key Check

```csharp
// ❌ BAD - Throws KeyNotFoundException
var value = _config[key];

// ✅ GOOD - TryGetValue for read
if (_config.TryGetValue(key, out var value))
{
    // Use value
}
```

### 3. Singleton Access Without Null Check

```csharp
// ❌ BAD - Assumes Instance is always valid
var data = MySingleton.Instance.SomeData;

// ✅ GOOD - Defensive null check
var instance = MySingleton.Instance;
if (instance?.SomeData == null)
{
    return defaultValue;
}
```

### 4. Chained Property Access Without Null Checks

```csharp
// ❌ BAD - Any null in chain crashes
var name = hero.Clan.Leader.Name.ToString();

// ✅ GOOD - Null-conditional chain
var name = hero?.Clan?.Leader?.Name?.ToString() ?? "Unknown";
```

## Implementation Checklist

When implementing patterns, ensure:

✓ Hook interface + implementation delegates to strategy
✓ Strategy interface + focused implementations
✓ IoC registration with appropriate lifetimes (Singleton/Transient)
✓ Fluent builder methods with chaining (return `this`)
✓ Pattern integration working cohesively
✓ No business logic in Harmony patches
✓ No monolithic classes (single responsibility)
✓ No `#region` directives (use class decomposition)
✓ No `[Obsolete]` attributes (complete migration)
✓ No `#if DEBUG` in application code (use DI)
✓ No `FirstOrDefault()!` - use null checks or pattern matching
✓ No direct dictionary access - use TryGetValue
✓ No singleton access without null check
