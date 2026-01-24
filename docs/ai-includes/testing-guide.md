# Testing Guide

Comprehensive guide for testing in the TAOM project.

---

## Testing Philosophy

> **Tests are documentation.** They show how code should behave.
> **Tests are safety nets.** They catch regressions before users do.
> **Tests enable refactoring.** Change with confidence.

---

## Testing Stack

| Tool | Purpose |
|------|---------|
| MSTest | Test framework |
| NSubstitute | Mocking library |
| Adapter Pattern | Enables mocking sealed types |
| DryIoC | Dependency injection (production) |

---

## Test Types

### 1. Unit Tests (Required)

Test individual components in isolation.

**Characteristics:**
- Fast (< 100ms per test)
- Isolated (no external dependencies)
- Deterministic (same result every time)
- Focused (one behavior per test)

```csharp
[TestClass]
public class CultureBonusEngineTests
{
    private CultureBonusEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _engine = new CultureBonusEngine();
    }

    [TestMethod]
    public void GetModifier_EmpireVsSturgia_ReturnsBonus()
    {
        // Arrange - no external dependencies

        // Act
        var result = _engine.GetModifier("empire", "sturgia");

        // Assert
        Assert.AreEqual(1.15f, result);
    }
}
```

### 2. Integration Tests (Optional)

Test component interactions (used sparingly).

```csharp
[TestClass]
public class CultureBonusIntegrationTests
{
    [TestMethod]
    public void FullBonusCalculationFlow()
    {
        // Setup real IoC container for integration
        var container = new Container();
        container.Register<ICultureBonusEngine, CultureBonusEngine>();
        container.Register<ICultureBonusService, CultureBonusService>();

        var service = container.Resolve<ICultureBonusService>();

        // Test full flow
        var mockAttacker = CreateMockAgent("empire");
        var mockVictim = CreateMockAgent("sturgia");

        var result = service.CalculateBonus(mockAttacker, mockVictim);

        Assert.AreEqual(1.15f, result);
    }
}
```

---

## Testing with Adapters

### The Problem: Sealed Types

TaleWorlds types are sealed and cannot be mocked:

```csharp
// ❌ FAILS - Cannot mock sealed class
var agent = Substitute.For<Agent>(); // Throws exception

// ❌ FAILS - Cannot instantiate (no public constructor)
var agent = new Agent(); // Compile error
```

### The Solution: Adapter Interfaces (ADR-007)

Create interfaces that wrap sealed types:

```csharp
// Adapter interface
public interface IAgentAdapter
{
    string Name { get; }
    bool IsActive();
    bool IsFadingOut();
    string Culture { get; }
    ICharacterAdapter Character { get; }
}

// Now mockable!
var mockAgent = Substitute.For<IAgentAdapter>();
mockAgent.Name.Returns("Test Agent");
mockAgent.IsActive().Returns(true);
mockAgent.Culture.Returns("empire");
```

### Service Signature Rules

Services MUST use adapter interfaces, never sealed types:

```csharp
// ❌ WRONG - Uses sealed type (untestable)
public class BadService
{
    public float Calculate(Agent agent) { ... }
}

// ✅ CORRECT - Uses adapter interface (testable)
public class GoodService
{
    public float Calculate(IAgentAdapter agent) { ... }
}
```

---

## Mocking Patterns

### Basic Mocking

```csharp
// Create mock
var mockAgent = Substitute.For<IAgentAdapter>();

// Setup return values
mockAgent.Name.Returns("Test Hero");
mockAgent.IsActive().Returns(true);
mockAgent.Culture.Returns("vlandia");

// Setup conditional returns
mockAgent.GetSkillValue(Arg.Is("OneHanded")).Returns(150);
mockAgent.GetSkillValue(Arg.Is("TwoHanded")).Returns(100);

// Setup exception
mockAgent.When(x => x.DoSomething())
    .Do(_ => throw new InvalidOperationException());
```

### Verifying Calls

```csharp
// Verify method was called
mockService.Received(1).ProcessAgent(Arg.Any<IAgentAdapter>());

// Verify method was NOT called
mockService.DidNotReceive().ProcessAgent(Arg.Any<IAgentAdapter>());

// Verify with specific argument
mockService.Received().ProcessAgent(Arg.Is<IAgentAdapter>(a => a.Culture == "empire"));

// Verify call count
mockService.Received(3).ProcessAgent(Arg.Any<IAgentAdapter>());
```

### Mocking Nested Objects

```csharp
// Create nested mocks
var mockCharacter = Substitute.For<ICharacterAdapter>();
mockCharacter.Culture.Returns("empire");
mockCharacter.Level.Returns(25);

var mockAgent = Substitute.For<IAgentAdapter>();
mockAgent.Character.Returns(mockCharacter);

// Now agent.Character.Culture works
Assert.AreEqual("empire", mockAgent.Character.Culture);
```

### Mocking Factory Methods

```csharp
var mockFactory = Substitute.For<IAdapterFactory>();

// Return specific mock for specific input
mockFactory.GetAgentAdapter(Arg.Is<Agent>(a => a != null))
    .Returns(callInfo =>
    {
        var mock = Substitute.For<IAgentAdapter>();
        mock.Name.Returns("Wrapped Agent");
        return mock;
    });
```

---

## Test Patterns

### Testing Services

```csharp
[TestClass]
public class CultureBonusServiceTests
{
    private ICultureBonusService _service = null!;
    private ICultureBonusEngine _mockEngine = null!;
    private ICultureResolver _mockResolver = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockEngine = Substitute.For<ICultureBonusEngine>();
        _mockResolver = Substitute.For<ICultureResolver>();
        _service = new CultureBonusService(_mockEngine, _mockResolver);
    }

    [TestMethod]
    public void GetBonus_WithValidAgents_ReturnsEngineResult()
    {
        // Arrange
        var mockAttacker = Substitute.For<IAgentAdapter>();
        var mockVictim = Substitute.For<IAgentAdapter>();

        _mockResolver.GetCulture(mockAttacker).Returns("empire");
        _mockResolver.GetCulture(mockVictim).Returns("sturgia");
        _mockEngine.GetModifier("empire", "sturgia").Returns(1.15f);

        // Act
        var result = _service.GetBonus(mockAttacker, mockVictim);

        // Assert
        Assert.AreEqual(1.15f, result);
    }

    [TestMethod]
    public void GetBonus_WithNullAttacker_ReturnsDefault()
    {
        // Arrange
        var mockVictim = Substitute.For<IAgentAdapter>();

        // Act
        var result = _service.GetBonus(null, mockVictim);

        // Assert
        Assert.AreEqual(1.0f, result);
        _mockEngine.DidNotReceive().GetModifier(Arg.Any<string>(), Arg.Any<string>());
    }
}
```

### Testing Engines

```csharp
[TestClass]
public class CultureBonusEngineTests
{
    private CultureBonusEngine _engine = null!;

    [TestInitialize]
    public void Setup()
    {
        _engine = new CultureBonusEngine();
    }

    [TestMethod]
    public void GetModifier_KnownCulturePair_ReturnsConfiguredBonus()
    {
        var result = _engine.GetModifier("empire", "sturgia");
        Assert.AreEqual(1.15f, result);
    }

    [TestMethod]
    public void GetModifier_UnknownCulturePair_ReturnsDefault()
    {
        var result = _engine.GetModifier("unknown1", "unknown2");
        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void GetModifier_SameCulture_ReturnsNoBonus()
    {
        var result = _engine.GetModifier("empire", "empire");
        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void GetModifier_NullCulture_ReturnsDefault()
    {
        var result = _engine.GetModifier(null, "empire");
        Assert.AreEqual(1.0f, result);
    }

    [TestMethod]
    public void GetModifier_EmptyCulture_ReturnsDefault()
    {
        var result = _engine.GetModifier("", "empire");
        Assert.AreEqual(1.0f, result);
    }
}
```

### Testing Hooks

```csharp
[TestClass]
public class CultureBonusDamageHookTests
{
    private CultureBonusDamageHook _hook = null!;
    private ICultureBonusService _mockService = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockService = Substitute.For<ICultureBonusService>();
        _hook = new CultureBonusDamageHook(_mockService);
    }

    [TestMethod]
    public void OnCalculateDamage_AppliesServiceModifier()
    {
        // Arrange
        var mockAttacker = Substitute.For<IAgentAdapter>();
        var mockVictim = Substitute.For<IAgentAdapter>();
        _mockService.GetBonus(mockAttacker, mockVictim).Returns(1.15f);

        float damage = 100f;

        // Act
        _hook.OnCalculateDamage(ref damage, mockAttacker, mockVictim);

        // Assert
        Assert.AreEqual(115f, damage);
    }

    [TestMethod]
    public void OnCalculateDamage_WithNullAttacker_DoesNotModifyDamage()
    {
        // Arrange
        float damage = 100f;

        // Act
        _hook.OnCalculateDamage(ref damage, null, Substitute.For<IAgentAdapter>());

        // Assert
        Assert.AreEqual(100f, damage);
        _mockService.DidNotReceive().GetBonus(Arg.Any<IAgentAdapter>(), Arg.Any<IAgentAdapter>());
    }
}
```

### Testing Adapters

```csharp
[TestClass]
public class AgentAdapterTests
{
    // Note: Adapter tests are limited because we can't instantiate
    // the wrapped sealed type. Focus on null safety and interface contract.

    [TestMethod]
    public void IsActive_WhenAgentIsNull_ReturnsFalse()
    {
        // This tests the adapter's null safety
        // Implementation should handle null wrapped object
    }

    [TestMethod]
    public void Culture_WhenCharacterIsNull_ReturnsNull()
    {
        // Verify adapter handles null nested objects
    }
}
```

---

## Testing Edge Cases

### Null Handling

```csharp
[TestMethod]
public void ProcessAgent_WhenAgentIsNull_ReturnsDefault()
{
    var result = _service.ProcessAgent(null);
    Assert.AreEqual(default, result);
}

[TestMethod]
public void ProcessAgent_WhenAgentCultureIsNull_HandlesGracefully()
{
    var mockAgent = Substitute.For<IAgentAdapter>();
    mockAgent.Culture.Returns((string)null);

    var result = _service.ProcessAgent(mockAgent);

    Assert.AreEqual(1.0f, result);
}
```

### Empty Collections

```csharp
[TestMethod]
public void ProcessAgents_WhenCollectionIsEmpty_ReturnsEmptyResult()
{
    var result = _service.ProcessAgents(Array.Empty<IAgentAdapter>());
    Assert.AreEqual(0, result.Count);
}

[TestMethod]
public void ProcessAgents_WhenCollectionIsNull_ReturnsEmptyResult()
{
    var result = _service.ProcessAgents(null);
    Assert.AreEqual(0, result.Count);
}
```

### Boundary Values

```csharp
[TestMethod]
public void CalculateModifier_AtMaxValue_HandlesCorrectly()
{
    var result = _engine.CalculateModifier(float.MaxValue);
    Assert.IsTrue(float.IsFinite(result));
}

[TestMethod]
public void CalculateModifier_AtZero_ReturnsDefault()
{
    var result = _engine.CalculateModifier(0f);
    Assert.AreEqual(1.0f, result);
}

[TestMethod]
public void CalculateModifier_NegativeValue_ReturnsDefault()
{
    var result = _engine.CalculateModifier(-100f);
    Assert.AreEqual(1.0f, result);
}
```

### Invalid Input

```csharp
[TestMethod]
public void GetCultureBonus_WithWhitespaceOnly_ReturnsDefault()
{
    var result = _engine.GetCultureBonus("   ", "empire");
    Assert.AreEqual(1.0f, result);
}

[TestMethod]
public void GetCultureBonus_CaseInsensitive()
{
    var result1 = _engine.GetCultureBonus("EMPIRE", "sturgia");
    var result2 = _engine.GetCultureBonus("empire", "STURGIA");
    var result3 = _engine.GetCultureBonus("Empire", "Sturgia");

    Assert.AreEqual(result1, result2);
    Assert.AreEqual(result2, result3);
}
```

---

## Testing Limitations

### What Cannot Be Unit Tested

| Component | Reason | Alternative |
|-----------|--------|-------------|
| Harmony Patches | Require game runtime | Manual verification |
| GameModels | Require game context | Manual verification |
| MissionLogics | Require active mission | Manual verification |
| CampaignBehaviors | Require campaign | Manual verification |
| Sealed type constructors | No public access | Use adapters |

### Testing Strategy for Untestable Code

1. **Keep untestable code thin** (ADR-002)
2. **Delegate to testable services immediately**
3. **Log extensively for manual verification**
4. **Create test scenarios in-game**

```csharp
// Thin entry point - delegates immediately
[HarmonyPatch(typeof(SomeClass), "Method")]
public class SomeClass_Method_Patch
{
    public static void Postfix(ref float __result, Agent agent)
    {
        // Convert to testable types immediately
        var adapter = IoC.Resolve<IAdapterFactory>().GetAgentAdapter(agent);

        // Delegate to testable service
        var service = IoC.Resolve<IMyService>();
        __result = service.Calculate(adapter);

        // Log for verification
        IoC.Resolve<IModLogger>().LogDebug($"Calculated: {__result} for {adapter.Name}");
    }
}
```

---

## Test Organization

### File Structure

```
TAOM.Tests/
├── Features/
│   ├── CombatBonus/
│   │   ├── CultureBonusEngineTests.cs
│   │   ├── CultureBonusServiceTests.cs
│   │   └── CultureBonusDamageHookTests.cs
│   ├── PartySpeed/
│   │   ├── PartySpeedEngineTests.cs
│   │   ├── PartySpeedServiceTests.cs
│   │   └── PartySpeedModifierHookTests.cs
│   └── ...
├── Adapters/
│   ├── AgentAdapterTests.cs
│   ├── HeroAdapterTests.cs
│   ├── PartyBaseAdapterTests.cs
│   └── ...
├── Core/
│   ├── IoCTests.cs
│   └── LoggerTests.cs
├── TestUtilities/
│   ├── MockFactories.cs
│   ├── TestHelpers.cs
│   └── AssertExtensions.cs
└── GlobalUsings.cs
```

### Test Class Template

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatBonus;
using TAOM.Adapters;

namespace TAOM.Tests.Features.CombatBonus;

[TestClass]
public class CultureBonusServiceTests
{
    private CultureBonusService _sut = null!;  // System Under Test
    private ICultureBonusEngine _mockEngine = null!;
    private ICultureResolver _mockResolver = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockEngine = Substitute.For<ICultureBonusEngine>();
        _mockResolver = Substitute.For<ICultureResolver>();
        _sut = new CultureBonusService(_mockEngine, _mockResolver);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Clean up any resources if needed
    }

    // Happy path tests
    [TestMethod]
    public void GetBonus_WithValidAgents_ReturnsExpectedValue()
    {
        // Arrange
        // Act
        // Assert
    }

    // Edge case tests
    [TestMethod]
    public void GetBonus_WithNullAttacker_ReturnsDefault()
    {
        // Arrange
        // Act
        // Assert
    }

    // Error condition tests
    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void GetBonus_WhenEngineThrows_PropagatesException()
    {
        // Arrange
        // Act
        // Assert
    }
}
```

---

## Running Tests

### Command Line

```powershell
# Run all tests
dotnet test

# Run specific test project
dotnet test TAOM.Tests/TAOM.Tests.csproj

# Run with verbose output
dotnet test -v normal

# Run specific test class
dotnet test --filter "ClassName=CultureBonusServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~GetBonus_WithValidAgents"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio

1. **Test Explorer** (View → Test Explorer)
2. **Run All** (Ctrl+R, A)
3. **Run Tests in Current Context** (Ctrl+R, T)
4. **Debug Tests** (right-click → Debug)

---

## Coverage Requirements

### Minimum Coverage by Component (ADR-008)

| Component | Required | Rationale |
|-----------|----------|-----------|
| Services | 100% | Core business logic |
| Engines | 100% | Algorithm implementations |
| Hooks | 80%+ | Integration layer |
| Adapters | Core methods | Mostly passthrough |
| Entry Points | 0% | Not unit testable |

### Coverage Tools

```powershell
# Install ReportGenerator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

---

## Test Utilities

### Mock Factory

```csharp
public static class MockFactories
{
    public static IAgentAdapter CreateMockAgent(
        string name = "Test Agent",
        string culture = "empire",
        bool isActive = true)
    {
        var mock = Substitute.For<IAgentAdapter>();
        mock.Name.Returns(name);
        mock.Culture.Returns(culture);
        mock.IsActive().Returns(isActive);
        return mock;
    }

    public static IHeroAdapter CreateMockHero(
        string name = "Test Hero",
        string culture = "empire",
        int level = 1)
    {
        var mock = Substitute.For<IHeroAdapter>();
        mock.Name.Returns(name);
        mock.Culture.Returns(culture);
        mock.Level.Returns(level);
        return mock;
    }
}
```

### Assert Extensions

```csharp
public static class AssertExtensions
{
    public static void AreApproximatelyEqual(float expected, float actual, float tolerance = 0.001f)
    {
        Assert.IsTrue(
            Math.Abs(expected - actual) < tolerance,
            $"Expected {expected} but was {actual} (tolerance: {tolerance})");
    }

    public static void IsInRange(float value, float min, float max)
    {
        Assert.IsTrue(
            value >= min && value <= max,
            $"Value {value} is not in range [{min}, {max}]");
    }
}
```

---

## Checklist

### Before Submitting Tests

- [ ] Tests follow AAA pattern (Arrange, Act, Assert)
- [ ] Test names describe behavior: `MethodName_StateUnderTest_ExpectedBehavior`
- [ ] Happy path tests exist
- [ ] Edge case tests exist (null, empty, boundary)
- [ ] Error condition tests exist
- [ ] All tests pass locally
- [ ] Coverage meets requirements
- [ ] No hardcoded values (use constants or setup)
- [ ] Tests are isolated (no shared state)
- [ ] Tests are deterministic (same result every run)

---

## Related Guides

- [tdd-enforcement.md](./tdd-enforcement.md) - TDD workflow and rules
- [patterns.md](./patterns.md) - Design patterns for testability
- [architecture.md](./architecture.md) - Testable architecture overview
- [code-quality.md](./code-quality.md) - Clean code principles
