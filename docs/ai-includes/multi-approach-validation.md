# Multi-Approach Validation Guide

When the solution isn't obvious, explore multiple approaches before committing to one.

---

## When to Use Multi-Approach Validation

Use this protocol when:

- The problem has **multiple valid solutions**
- You're **unsure which approach is best**
- The task involves **significant complexity** or architectural decisions
- Previous attempts have **failed unexpectedly**
- The **requirements are ambiguous** and interpretations vary

Skip this protocol for:

- Simple, mechanical tasks with obvious solutions
- Well-established patterns already in the codebase (check ADRs first)
- Bug fixes with clear root causes

---

## The Protocol

```
+-------------------------------------------------------------------------+
|                    MULTI-APPROACH VALIDATION                            |
+-------------------------------------------------------------------------+
|                                                                         |
|  1. ENUMERATE    ->  List 2-3 valid approaches                          |
|  2. ANALYZE      ->  Evaluate trade-offs of each                        |
|  3. IMPLEMENT    ->  Build simplest viable approach first               |
|  4. VALIDATE     ->  Test against requirements                          |
|  5. COMPARE      ->  If multiple work, select best fit                  |
|                                                                         |
|  Consensus indicator: If multiple approaches produce identical          |
|  results independently, confidence in correctness is HIGH               |
|                                                                         |
+-------------------------------------------------------------------------+
```

---

## Phase 1: Enumerate Approaches

Before coding, explicitly list alternative approaches.

### Approach Documentation Template

```
APPROACH A: [Name/Description]
+-- Core idea: [One sentence summary]
+-- Implementation sketch: [High-level steps]
+-- Pros: [Advantages]
+-- Cons: [Disadvantages]
+-- Complexity: [Low/Medium/High]
+-- Risk: [What could go wrong]
+-- Fits codebase patterns: [Yes/No/Partially - check ADRs]

APPROACH B: [Name/Description]
+-- Core idea: ...
+-- ...

APPROACH C: [Name/Description]
+-- Core idea: ...
+-- ...
```

### Minimum Viable Approaches

Always identify at least:

1. **The obvious approach** - What would a junior developer do?
2. **The elegant approach** - What would an expert do?
3. **The pragmatic approach** - What requires least change to existing code?

These often converge, but explicitly considering all three prevents blind spots.

### Example: Adding Party Speed Calculation

```
APPROACH A: Direct Calculation in Service
+-- Core idea: Calculate speed directly in PartySpeedService
+-- Implementation: Add CalculateSpeed method with all factors
+-- Pros: Simple, all logic in one place
+-- Cons: Becomes complex as factors grow
+-- Complexity: Low initially, High long-term
+-- Risk: Violates thin service principle as complexity grows
+-- Fits codebase patterns: Partial (services should be thin)

APPROACH B: Strategy Pattern
+-- Core idea: IPartySpeedModifier strategies composed together
+-- Implementation: Create interface, implement per-factor, compose in service
+-- Pros: Extensible, testable, follows existing hook patterns
+-- Cons: More files, higher initial complexity
+-- Complexity: Medium initially, Low long-term
+-- Risk: Over-engineering if only 2-3 factors
+-- Fits codebase patterns: Yes (matches hook pattern in ADR-002)

APPROACH C: GameModel Override
+-- Core idea: Override DefaultPartySpeedCalculatingModel
+-- Implementation: Harmony patch or GameModel subclass
+-- Pros: Native integration with game engine
+-- Cons: Thin entry point must delegate to service (ADR-002)
+-- Complexity: Medium
+-- Risk: Game updates may break compatibility
+-- Fits codebase patterns: Yes (GameModel is approved entry point)
```

---

## Phase 2: Analyze Trade-offs

### Trade-off Matrix

| Factor | Approach A | Approach B | Approach C |
|--------|-----------|-----------|-----------|
| Complexity | Low | Medium | Medium |
| Testability | Medium | High | Medium |
| Readability | High | Medium | Medium |
| Fits ADRs | Partial | Yes | Yes |
| Future flexibility | Low | High | Medium |
| Risk of bugs | Low | Medium | Medium |

### Decision Criteria Priority

When trade-offs conflict, prioritize in this order:

1. **Correctness** - Must solve the actual problem
2. **Fits codebase patterns** - Consistency trumps "better" solutions (check ADRs!)
3. **Readability** - Others must maintain this code
4. **Simplicity** - Fewer moving parts = fewer bugs
5. **Testability** - Must be 100% unit testable per ADR-008
6. **Performance** - Only if measurably necessary
7. **Elegance** - Nice to have, not essential

### Red Flags

Deprioritize approaches that:

- Violate ADRs (check docs/adrs/ first!)
- Require significant refactoring of unrelated code
- Introduce new patterns not used elsewhere in codebase
- Have complex failure modes
- Are hard to test with adapter pattern
- Require special knowledge to understand

---

## Phase 3: Implement Simplest First

### Why Simplest First?

1. **Faster feedback** - Know sooner if you're on the right track
2. **Baseline comparison** - Simple solution sets the bar
3. **Often good enough** - Complex solutions are rarely needed
4. **Easier to abandon** - Less sunk cost if it doesn't work

### Implementation Rules

```
DO:
- Implement the simplest viable approach completely
- Make it work correctly before considering alternatives
- Write tests that validate the requirements (not the approach)
- Keep the implementation clean enough to compare fairly

DON'T:
- Half-implement multiple approaches
- Add complexity "in case we need it later"
- Skip tests because "we might change the approach"
- Gold-plate the simple solution
```

### Minimum Viable Implementation

Ask: "What is the least amount of code that solves the problem correctly?"

Implement that first. If it works, is testable, and follows ADRs, you're done.

---

## Phase 4: Validate Against Requirements

### Requirement Checklist

Before considering an approach "working":

```
FUNCTIONAL REQUIREMENTS:
- [ ] Handles happy path correctly
- [ ] Handles edge cases (null adapters, dead heroes, empty collections)
- [ ] Handles error cases gracefully
- [ ] Produces correct output for all test inputs

NON-FUNCTIONAL REQUIREMENTS:
- [ ] Performance acceptable (profile if complex calculation)
- [ ] Memory usage reasonable (check object pooling if allocating)
- [ ] No crashes in edge cases
- [ ] Logging adequate for debugging

CODE QUALITY (TAOM-SPECIFIC):
- [ ] Tests pass (TDD - tests written first!)
- [ ] Services use adapter interfaces only (ADR-007)
- [ ] Entry points < 150 lines (ADR-002)
- [ ] No #region, #if DEBUG, [Obsolete] (ADR-003, 004, 005)
- [ ] Coverage > 80%
```

### Test Independence

Write tests that validate **requirements**, not **implementation**:

```csharp
// Test tied to implementation
// "verify that _internal_helper is called 3 times"
_mockHelper.Received(3).DoSomething();

// Test tied to requirements
// "verify that output contains all transformed items"
Assert.AreEqual(3, result.Items.Count);
Assert.IsTrue(result.Items.All(i => i.IsTransformed));
```

This allows swapping implementations without changing tests.

---

## Phase 5: Compare and Select

### When Multiple Approaches Work

If you implemented multiple approaches and both pass all tests:

```
COMPARISON CRITERIA:

1. Identical outputs?
   - Yes -> High confidence both are correct
   - No  -> One or both has a bug, investigate

2. Code complexity (lines, branches, dependencies)?
   - Prefer fewer lines and branches
   - Prefer fewer external dependencies

3. Fits existing patterns?
   - Prefer approach that looks like surrounding code
   - Consistency > local optimization

4. Maintainability?
   - Who will maintain this? Can they understand it?
   - Prefer approach a junior dev could modify

5. Performance (only if measured)?
   - Prefer faster only if difference is significant
   - Don't optimize prematurely
```

### The Consensus Signal

**Key insight:**

When multiple independent approaches produce the **same result**, confidence in correctness increases significantly.

```
IF approach_A.result == approach_B.result == approach_C.result:
    confidence = HIGH
    # All approaches agree - likely correct
    # Choose simplest implementation

ELIF approach_A.result == approach_B.result != approach_C.result:
    confidence = MEDIUM
    # Two agree, one differs
    # Investigate the outlier - it may have found an edge case
    # Or it may have a bug

ELSE:
    confidence = LOW
    # All differ - requirements may be ambiguous
    # Clarify requirements before proceeding
```

### Selection Decision Tree

```
Start
  |
  +-> Does the simplest approach work correctly?
  |     |
  |     +-> YES: Use it. Done.
  |     |
  |     +-> NO: Why not?
  |           |
  |           +-> Performance issue (measured, not assumed)
  |           |     +-> Try next simplest approach
  |           |
  |           +-> Doesn't handle edge case
  |           |     +-> Can simple approach be extended?
  |           |           +-> YES: Extend it
  |           |           +-> NO: Try next approach
  |           |
  |           +-> Doesn't fit codebase patterns (ADR violation)
  |                 +-> Is the pattern worth breaking?
  |                       +-> YES (rare): Create new ADR to justify
  |                       +-> NO: Adapt to existing patterns
  |
  +-> If all approaches fail -> requirements may be impossible
        +-> Escalate with specific failure evidence
```

---

## Documentation

### When to Create an ADR

Create an Architecture Decision Record when:

- Multiple viable approaches existed
- The chosen approach has non-obvious trade-offs
- Future developers might question the decision
- The decision affects multiple parts of the codebase

### ADR Quick Template

Use the template in `docs/adrs/000-template.md`:

```markdown
# ADR-XXX: [Decision Title]

## Status
Accepted

## Context
[What problem were we solving? What constraints existed?]

## Approaches Considered

### Approach A: [Name]
- Pros: [...]
- Cons: [...]

### Approach B: [Name]
- Pros: [...]
- Cons: [...]

## Decision
We chose Approach [X] because [specific reasons].

## Consequences
- [What changes as a result]
- [What we're giving up]
- [What we need to monitor]
```

---

## Quick Reference

```
1. ENUMERATE (before coding)
   - List 2-3 valid approaches
   - Document pros/cons of each
   - Check ADRs for existing patterns
   - Identify simplest viable option

2. ANALYZE (before coding)
   - Create trade-off matrix
   - Apply decision criteria (correctness -> fits ADRs -> readability -> simplicity)
   - Note red flags

3. IMPLEMENT (simplest first)
   - Complete implementation, not sketches
   - Write requirement-based tests (TDD!)
   - Keep clean for fair comparison

4. VALIDATE (against requirements)
   - All functional requirements met
   - All tests passing
   - Follows ADRs and code quality standards

5. COMPARE (if multiple work)
   - Check for result consensus
   - Prefer simplicity and consistency
   - Document decision in ADR if non-obvious
```

---

## Anti-Patterns

### Analysis Paralysis

- Spending hours analyzing before writing any code
- Timebox enumeration to 10-15 minutes, then start implementing

### Premature Optimization

- Choosing complex approach because "it might be faster"
- Implement simple first, optimize only when measured

### Sunk Cost Fallacy

- Sticking with failing approach because you invested time
- Abandon quickly when evidence shows approach won't work

### NIH Syndrome (Not Invented Here)

- Rejecting existing patterns because your idea is "better"
- Fit into codebase patterns unless clearly justified (and create ADR)

### Gold Plating

- Adding features/flexibility "while we're here"
- Solve exactly the stated problem, nothing more

---

## Related Guides

- [tdd-enforcement.md](./tdd-enforcement.md) - Hypothesis-driven development workflow
- [iterative-problem-solving.md](./iterative-problem-solving.md) - Systematic debugging approach
- [../problem-solving-guide.md](../problem-solving-guide.md) - Research and investigation
- [ADRs](../adrs/) - Existing architectural decisions
