---
paths:
  - "TAOM.Tests/**"
  - "**/*Tests.cs"
  - "**/*Test.cs"
---

# Testing Rules (TDD Mandatory)

## Workflow: RED -> GREEN -> REFACTOR
1. Write a failing test FIRST (verify RED state)
2. Write minimum production code to pass (GREEN)
3. Refactor while keeping tests green

## Naming Convention
`MethodName_StateUnderTest_ExpectedBehavior`

Examples:
- `LoadRegions_ValidJson_ReturnsRegionList`
- `GetWage_Tier10_ReturnsExtendedWage`
- `LoadRegions_MissingFile_ReturnsEmptyAndLogs`

## Structure: AAA Pattern
```csharp
[TestMethod]
public void MethodName_State_Expected()
{
    // Arrange
    var mock = Substitute.For<IMyAdapter>();

    // Act
    var result = _sut.DoSomething();

    // Assert
    Assert.AreEqual(expected, result);
}
```

## Framework
- **MSTest** — `[TestClass]`, `[TestMethod]`, `[TestInitialize]`, `[TestCleanup]`
- **NSubstitute** — `Substitute.For<T>()`, `.Returns()`, `.Received()`
- **No Moq** — Project uses NSubstitute exclusively

## Coverage Requirements
| Layer | Required |
|-------|----------|
| Services/Engines | 100% |
| Hooks | 80%+ |
| Entry Points | N/A (thin delegation) |
| Adapters | Via service tests |

## Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`
