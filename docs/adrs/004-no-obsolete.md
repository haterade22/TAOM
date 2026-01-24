# ADR-004: No Obsolete Markers - Migrate to New Patterns

**Status**: Accepted
**Date**: 2025-11-02
**Context**: Code evolution and deprecation strategy

## Decision

The use of `[Obsolete]` attributes is prohibited in the codebase. When creating new patterns or refactoring existing code, all usage sites MUST be migrated to the new implementation immediately.

## Rationale

### Problems with Obsolete Markers

1. **Technical Debt Accumulation**: Obsolete markers allow old code to linger, creating parallel implementations
2. **Confusion**: Developers must choose between old and new approaches, leading to inconsistent patterns
3. **Maintenance Burden**: Supporting both old and new implementations requires maintaining duplicate logic
4. **Delayed Migration**: Teams often defer migration indefinitely, leaving obsolete code permanently
5. **Codebase Bloat**: Both implementations remain in codebase, increasing complexity and file size
6. **Testing Overhead**: Both code paths require tests and maintenance

### Better Approach: Immediate Migration

- **Complete Migration**: When introducing new pattern, migrate ALL usage sites in same PR/commit
- **Breaking Changes**: Accept that improvements require immediate updates across codebase
- **Clean Breaks**: Remove old implementation once new one is ready
- **No Half-Measures**: Avoid gradual deprecation periods that extend indefinitely

## Examples

### BAD: Using Obsolete to Defer Migration

```csharp
// Old implementation
[Obsolete("Use CalculateWithAdapter instead")]
public int Calculate(Hero hero)
{
    // Old logic accepting sealed types
}

// New implementation
public int CalculateWithAdapter(IHeroAdapter heroAdapter)
{
    // New logic with adapters
}

// Result: Both methods exist, confusion about which to use
```

### GOOD: Complete Migration

```csharp
// BEFORE refactoring:
public interface IMyService
{
    int Calculate(Hero hero);
}

// AFTER refactoring - old method completely removed:
public interface IMyService
{
    int Calculate(IHeroAdapter heroAdapter);
}

// All usage sites updated in same PR:
// - File1.cs: Updated to use heroAdapter
// - File2.cs: Updated to use heroAdapter
// - File3.cs: Updated to use heroAdapter
```

## Migration Strategy

When introducing new patterns or refactoring:

1. **Identify All Usage Sites**: Use IDE "Find All References" to locate every usage
2. **Update All Usages**: Migrate every call site to new pattern in same commit
3. **Remove Old Implementation**: Delete old method/class completely
4. **Update Tests**: Modify all tests to use new implementation
5. **Verify Build**: Ensure no compilation errors remain
6. **Run Tests**: Confirm all tests pass with new implementation
7. **Commit Together**: One atomic commit with complete migration

## Acceptable Alternatives

Instead of `[Obsolete]`:
- **Immediate Refactoring**: Update all usage sites when introducing new pattern
- **Feature Branches**: Use feature branches for large migrations, merge when complete
- **Architecture Decision**: Document pattern changes in ADR (like this file)
- **Breaking Changes**: Accept that improvements may require immediate codebase updates

## Exceptions

**NONE**: There are no acceptable uses of `[Obsolete]` in this codebase.

If a migration is too large to complete atomically:
1. Use long-lived feature branch
2. Complete migration on branch
3. Merge when all usage sites updated
4. **Still no `[Obsolete]` markers**

## Consequences

### Positive
- **No Technical Debt**: Old code removed immediately, preventing accumulation
- **Clear Patterns**: Only one way to do things, no confusion
- **Reduced Complexity**: Fewer code paths to maintain and test
- **Forced Discipline**: Requires complete thinking before introducing changes
- **Clean Codebase**: No deprecated code lingering in production

### Negative
- **Larger PRs**: Pattern changes require updating all usage sites
- **More Initial Effort**: Cannot defer migration work
- **Breaking Changes**: Collaborators must update their branches

## Enforcement

1. **Code Review**: Reviewers REJECT any PR containing `[Obsolete]` attributes
2. **Static Analysis**: Consider analyzer rule to flag `[Obsolete]` usage
3. **PR Guidelines**: PRs introducing new patterns must migrate all existing usages
4. **Documentation**: This ADR linked in CLAUDE.md
5. **Build Warnings**: Treat obsolete warnings as errors (if any slip through)

## Real-World Example

When migrating from direct game object usage to adapter pattern:

```csharp
// Step 1: Create adapter interface and implementation
public interface IHeroAdapter { /* ... */ }
public class HeroAdapter : IHeroAdapter { /* ... */ }

// Step 2: Update service interface (NO [Obsolete]!)
public interface IMyService
{
    // OLD: int Calculate(Hero hero);  // DELETED, not marked obsolete
    int Calculate(IHeroAdapter heroAdapter);  // New signature
}

// Step 3: Update ALL usage sites in SAME PR
// - GameModel1.cs: Wrap hero in adapter
// - Behavior1.cs: Wrap hero in adapter
// - Patch1.cs: Wrap hero in adapter

// Step 4: Old implementation GONE, no deprecation period
```
