# TDD Enforcement Guide

Test-Driven Development is **mandatory** for this project. This guide enforces the TDD workflow.

---

## The TDD Cycle

```
+------------------------------------------------------------------------+
|                         TDD CYCLE (RED-GREEN-REFACTOR)                 |
+------------------------------------------------------------------------+
|                                                                        |
|   +-------+                                                            |
|   |  RED  |  1. Write a failing test for the desired behavior          |
|   +-------+                                                            |
|       |                                                                |
|       v                                                                |
|   +-------+                                                            |
|   | GREEN |  2. Write minimum code to make the test pass               |
|   +-------+                                                            |
|       |                                                                |
|       v                                                                |
|   +----------+                                                         |
|   | REFACTOR |  3. Improve the code while keeping tests green          |
|   +----------+                                                         |
|       |                                                                |
|       +-----> Loop back to RED for next behavior                       |
|                                                                        |
+------------------------------------------------------------------------+
```

---

## Mandatory Workflow

### Before Writing Any Production Code

1. **Identify the behavior** to implement
2. **Write a test** that demonstrates the expected behavior
3. **Run the test** - it MUST fail (Red)
4. **Only then** write production code to make it pass (Green)
5. **Refactor** if needed, keeping tests green

### Enforcement Rules

| Rule | Description |
|------|-------------|
| No untested code | Every feature must have tests FIRST |
| Tests must fail first | Verify RED state before writing code |
| Minimum code | Only write enough to pass the current test |
| Continuous verification | Run tests after every change |
| Refactor with confidence | Tests protect against regressions |

---

## Test Structure

### The AAA Pattern

```csharp
[TestMethod]
public void MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange - Set up the test scenario
    var mockDependency = Substitute.For<IDependency>();
    mockDependency.GetValue().Returns(42);
    var sut = new SystemUnderTest(mockDependency);

    // Act - Execute the behavior
    var result = sut.CalculateResult();

    // Assert - Verify the outcome
    Assert.AreEqual(84, result);
}
```

### Naming Convention

```
[MethodName]_[StateUnderTest]_[ExpectedBehavior]

Examples:
- CalculateDamage_WhenAttackerIsEmpire_ReturnsBonus
- GetPartySpeed_WithHeavyLoad_ReducesSpeed
- ApplyModifier_WhenVictimIsNull_ReturnsDefaultValue
```

---

## TDD Example: Culture Damage Bonus

### Step 1: RED - Write Failing Test

```csharp
[TestClass]
public class CultureBonusEngineTests
{
    [TestMethod]
    public void GetDamageModifier_EmpireVsSturgia_ReturnsBonus()
    {
        // Arrange
        var engine = new CultureBonusEngine();

        // Act
        var result = engine.GetDamageModifier("empire", "sturgia");

        // Assert
        Assert.AreEqual(1.15f, result);
    }
}
```

**Run test → RED (class doesn't exist)**

### Step 2: GREEN - Write Minimum Code

```csharp
public class CultureBonusEngine
{
    public float GetDamageModifier(string attackerCulture, string victimCulture)
    {
        if (attackerCulture == "empire" && victimCulture == "sturgia")
        {
            return 1.15f;
        }
        return 1.0f;
    }
}
```

**Run test → GREEN**

### Step 3: Add More Tests (RED)

```csharp
[TestMethod]
public void GetDamageModifier_SturgiaVsEmpire_ReturnsBonus()
{
    var engine = new CultureBonusEngine();
    var result = engine.GetDamageModifier("sturgia", "empire");
    Assert.AreEqual(1.10f, result);
}

[TestMethod]
public void GetDamageModifier_SameCulture_ReturnsNoBonus()
{
    var engine = new CultureBonusEngine();
    var result = engine.GetDamageModifier("empire", "empire");
    Assert.AreEqual(1.0f, result);
}
```

### Step 4: Expand Implementation (GREEN)

```csharp
public float GetDamageModifier(string attackerCulture, string victimCulture)
{
    if (attackerCulture == victimCulture) return 1.0f;

    return (attackerCulture, victimCulture) switch
    {
        ("empire", "sturgia") => 1.15f,
        ("sturgia", "empire") => 1.10f,
        ("vlandia", "battania") => 1.12f,
        _ => 1.0f
    };
}
```

### Step 5: REFACTOR (Still GREEN)

```csharp
public class CultureBonusEngine : ICultureBonusEngine
{
    private readonly Dictionary<(string, string), float> _bonusTable;

    public CultureBonusEngine()
    {
        _bonusTable = new Dictionary<(string, string), float>
        {
            { ("empire", "sturgia"), 1.15f },
            { ("sturgia", "empire"), 1.10f },
            { ("vlandia", "battania"), 1.12f },
            // Add more...
        };
    }

    public float GetDamageModifier(string attackerCulture, string victimCulture)
    {
        if (attackerCulture == victimCulture) return 1.0f;

        return _bonusTable.TryGetValue((attackerCulture, victimCulture), out var bonus)
            ? bonus
            : 1.0f;
    }
}
```

**Run tests → Still GREEN**

---

## Test Categories

### Unit Tests (Required)

Test individual components in isolation with mocked dependencies.

```csharp
[TestClass]
public class CultureBonusServiceTests
{
    private ICultureBonusService _service;
    private ICultureBonusEngine _mockEngine;

    [TestInitialize]
    public void Setup()
    {
        _mockEngine = Substitute.For<ICultureBonusEngine>();
        _service = new CultureBonusService(_mockEngine);
    }

    [TestMethod]
    public void GetBonus_DelegatestoEngine()
    {
        // Arrange
        var mockAttacker = Substitute.For<IAgentAdapter>();
        var mockVictim = Substitute.For<IAgentAdapter>();
        mockAttacker.Culture.Returns("empire");
        mockVictim.Culture.Returns("sturgia");
        _mockEngine.GetDamageModifier("empire", "sturgia").Returns(1.15f);

        // Act
        var result = _service.GetBonus(mockAttacker, mockVictim);

        // Assert
        Assert.AreEqual(1.15f, result);
        _mockEngine.Received(1).GetDamageModifier("empire", "sturgia");
    }
}
```

### Edge Case Tests (Required)

Always test edge cases and error conditions.

```csharp
[TestMethod]
public void GetBonus_WhenAttackerIsNull_ReturnsDefault()
{
    var result = _service.GetBonus(null, Substitute.For<IAgentAdapter>());
    Assert.AreEqual(1.0f, result);
}

[TestMethod]
public void GetBonus_WhenCultureIsNull_ReturnsDefault()
{
    var mockAgent = Substitute.For<IAgentAdapter>();
    mockAgent.Culture.Returns((string)null);

    var result = _service.GetBonus(mockAgent, mockAgent);

    Assert.AreEqual(1.0f, result);
}

[TestMethod]
public void GetBonus_WhenCultureIsEmpty_ReturnsDefault()
{
    var mockAgent = Substitute.For<IAgentAdapter>();
    mockAgent.Culture.Returns(string.Empty);

    var result = _service.GetBonus(mockAgent, mockAgent);

    Assert.AreEqual(1.0f, result);
}
```

---

## Testing with Adapters

### Why Adapters Enable TDD

TaleWorlds types are **sealed** - they cannot be mocked directly. Adapters solve this:

```csharp
// ❌ Cannot mock sealed TaleWorlds type
var agent = Substitute.For<Agent>(); // FAILS - sealed class

// ✅ Can mock adapter interface
var agentAdapter = Substitute.For<IAgentAdapter>(); // WORKS
agentAdapter.Name.Returns("Test Agent");
agentAdapter.Culture.Returns("empire");
agentAdapter.IsActive().Returns(true);
```

### Testing Services with Adapter Mocks

```csharp
[TestMethod]
public void ProcessAgent_WhenActive_AppliesBonus()
{
    // Arrange
    var mockAgent = Substitute.For<IAgentAdapter>();
    mockAgent.IsActive().Returns(true);
    mockAgent.Culture.Returns("vlandia");

    var service = new AgentProcessingService(_mockBonusEngine);

    // Act
    service.ProcessAgent(mockAgent);

    // Assert
    _mockBonusEngine.Received(1).ApplyBonus(mockAgent);
}

[TestMethod]
public void ProcessAgent_WhenInactive_SkipsProcessing()
{
    // Arrange
    var mockAgent = Substitute.For<IAgentAdapter>();
    mockAgent.IsActive().Returns(false);

    var service = new AgentProcessingService(_mockBonusEngine);

    // Act
    service.ProcessAgent(mockAgent);

    // Assert
    _mockBonusEngine.DidNotReceive().ApplyBonus(Arg.Any<IAgentAdapter>());
}
```

---

## Coverage Requirements (ADR-008)

### Minimum Coverage by Component

| Component | Required Coverage |
|-----------|------------------|
| Services | 100% |
| Engines | 100% |
| Hooks | 80%+ |
| Adapters | Core methods |
| Entry Points | Not testable* |

*Entry points (Harmony patches, GameModels) cannot be unit tested - verified manually.

### Running Coverage Reports

```powershell
# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report (requires ReportGenerator tool)
reportgenerator -reports:coverage.cobertura.xml -targetdir:coveragereport
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
│   │   └── PartySpeedServiceTests.cs
│   └── ...
├── Adapters/
│   ├── AgentAdapterTests.cs
│   ├── HeroAdapterTests.cs
│   └── ...
├── TestUtilities/
│   ├── MockFactories.cs
│   └── TestHelpers.cs
└── GlobalUsings.cs
```

### Test Class Template

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatBonus;

namespace TAOM.Tests.Features.CombatBonus;

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
    public void MethodName_StateUnderTest_ExpectedBehavior()
    {
        // Arrange

        // Act

        // Assert
    }
}
```

---

## Common TDD Mistakes

### 1. Writing Code Before Tests

```csharp
// ❌ WRONG - Code first
public float Calculate() => value * 2;

// Then write test
[TestMethod]
public void Calculate_ReturnsDouble() { ... }

// ✅ CORRECT - Test first
[TestMethod]
public void Calculate_ReturnsDouble()
{
    var result = sut.Calculate(5);
    Assert.AreEqual(10, result);
}

// Then write code
public float Calculate(int value) => value * 2;
```

### 2. Testing Implementation Instead of Behavior

```csharp
// ❌ WRONG - Tests implementation details
[TestMethod]
public void GetBonus_CallsPrivateMethod()
{
    // Testing that a private method was called - fragile
}

// ✅ CORRECT - Tests observable behavior
[TestMethod]
public void GetBonus_WithEmpireVsSturgia_ReturnsCorrectValue()
{
    var result = _engine.GetBonus("empire", "sturgia");
    Assert.AreEqual(1.15f, result);
}
```

### 3. Multiple Assertions Without Focus

```csharp
// ❌ WRONG - Too many assertions, unclear purpose
[TestMethod]
public void ProcessAgent_Works()
{
    _service.ProcessAgent(mockAgent);

    Assert.AreEqual("empire", mockAgent.Culture);
    Assert.IsTrue(mockAgent.IsActive());
    Assert.AreEqual(100, mockAgent.Health);
    _mockEngine.Received(1).ApplyBonus(mockAgent);
}

// ✅ CORRECT - Focused single behavior
[TestMethod]
public void ProcessAgent_WhenActive_AppliesBonus()
{
    mockAgent.IsActive().Returns(true);

    _service.ProcessAgent(mockAgent);

    _mockEngine.Received(1).ApplyBonus(mockAgent);
}
```

### 4. Not Testing Edge Cases

```csharp
// ❌ WRONG - Only happy path
[TestMethod]
public void GetBonus_ReturnsValue()
{
    var result = _service.GetBonus("empire", "sturgia");
    Assert.AreEqual(1.15f, result);
}

// ✅ CORRECT - Include edge cases
[TestMethod]
public void GetBonus_WithNullCulture_ReturnsDefault() { ... }

[TestMethod]
public void GetBonus_WithEmptyCulture_ReturnsDefault() { ... }

[TestMethod]
public void GetBonus_WithSameCulture_ReturnsNoBonus() { ... }

[TestMethod]
public void GetBonus_WithUnknownCulture_ReturnsDefault() { ... }
```

---

## TDD Checklist

Before marking any code as complete:

- [ ] All tests were written BEFORE the implementation
- [ ] Each test failed (RED) before writing code
- [ ] Tests pass (GREEN) with minimum code
- [ ] Code was refactored while keeping tests green
- [ ] Edge cases are covered (null, empty, invalid)
- [ ] Coverage meets requirements (100% for services/engines)
- [ ] Test names describe the behavior being tested
- [ ] No implementation details tested (only behavior)
- [ ] All dependencies are mocked via adapter interfaces

---

## Quick Reference

### Test Naming
```
MethodName_StateUnderTest_ExpectedBehavior
```

### AAA Pattern
```csharp
// Arrange - Setup
// Act - Execute
// Assert - Verify
```

### Mock Setup (NSubstitute)
```csharp
var mock = Substitute.For<IInterface>();
mock.Method().Returns(value);
mock.Property.Returns(value);
mock.Received(1).Method();
mock.DidNotReceive().Method();
```

### Common Assertions
```csharp
Assert.AreEqual(expected, actual);
Assert.IsNull(result);
Assert.IsNotNull(result);
Assert.IsTrue(condition);
Assert.IsFalse(condition);
Assert.ThrowsException<ExceptionType>(() => code);
```

---

## Related Guides

- [testing-guide.md](./testing-guide.md) - Comprehensive testing patterns
- [patterns.md](./patterns.md) - Design patterns for testability
- [architecture.md](./architecture.md) - Testable architecture overview
- [code-quality.md](./code-quality.md) - Clean code principles
