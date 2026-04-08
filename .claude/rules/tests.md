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

## Skip-Guard Exhaustion (MANDATORY)

When a service method has `if (condition) continue/return` guard clauses, write a test for **every entity state that should be skipped**, not just the obvious ones.

**Why:** Review #23 found a HIGH bug where `EnsureCompanionsPlaced()` had guards for dead heroes, disabled entries, and already-placed companions -- but missed the recruited-and-traveling state. The most important negative case (companion in player's party) was untested.

**Rule:** For any method that iterates entities and conditionally skips:
1. List every possible entity state (use the state matrix from `csharp-architecture.md`)
2. Write one test per skip condition
3. The test name must identify the specific state: `Method_RecruitedCompanion_SkipsPlacement`
4. Prioritize the most common real-world states first -- a companion traveling with the player is more common than a dead companion

**Pattern to apply:**
```
// For each guard clause:  if (!X) continue;
// Write: Method_XisFalse_Skips()
// AND:   Method_XisTrue_Proceeds()
```

## Test Organization
Mirror source structure: `TAOM.Tests/Features/{FeatureName}/{ServiceName}Tests.cs`
