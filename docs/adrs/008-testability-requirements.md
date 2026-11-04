# ADR-008: Service Testability Requirements

## Status
**Accepted** - Mandatory for all services (2025-01-22)

## Context
Services must be 100% unit testable without requiring game framework initialization. This ADR defines explicit rules preventing services from using TaleWorlds static methods that make unit testing impossible.

## Decision

### Rule 1: No Static TaleWorlds Calls in Services

Services MUST NOT call static methods or properties from TaleWorlds game framework:

**Exception (2026-09-24):** a static method call or property access inside a protected-virtual boundary seam that meets ADR-007's "Protected-Virtual Boundary Seams" conditions is allowed; the service's test subclass overrides the seam, so no test touches the static.

**FORBIDDEN** (causes test failures):
```csharp
public class MyService
{
    public void DoSomething()
    {
        // WRONG - static calls require game initialization
        var time = CampaignTime.Now;                    // NullReferenceException in tests
        var basePath = Utilities.GetBasePath();          // NullReferenceException in tests
        var player = Hero.MainHero;                      // NullReferenceException in tests
        var encounter = PlayerEncounter.Current;         // NullReferenceException in tests
        var campaign = Campaign.Current;                 // NullReferenceException in tests
    }
}
```

A service reaches the engine only through an adapter or a boundary seam (ADR-007).

### Rule 2: All New Abstractions Must Be Fully Integrated

A new abstraction migrates every consumer in the same change.

### Rule 4: Test Verification Checklist

Before marking any abstraction task as complete:

- [ ] Interface created in `Main/Adapters/` or `Main/Core/Services/`
- [ ] Implementation created
- [ ] Registered in `Main/IoC.cs` or feature-specific IoC
- [ ] ALL services using old static calls refactored
- [ ] Full test suite run: `dotnet test TAOM.Tests`
- [ ] **Zero test failures** - not "most tests pass"
- [ ] Coverage maintained or improved

## Consequences

### Positive
- Services are 100% unit testable without game framework
- Tests run fast (no game initialization required)
- Clear dependency graph (all dependencies explicit)
- Refactoring is safer (compiler catches missing dependencies)

### Negative
- More boilerplate (extra interfaces + implementations)
- Larger constructor parameter lists
- Requires discipline to complete integration fully

## Enforcement

Enforcement: review (lens 1). There is no coverage gate and no CI; the suite runs through /verify.

A text search for `CampaignTime.Now` cannot tell a violation from a boundary seam (the Rule 1
exception), so static calls in services are a review check, not a grep.

## Related ADRs
- **ADR-007**: Adapter Pattern for Sealed Classes - Services use adapters, not sealed types
- **ADR-002**: Thin Entry Points - Entry points may use static calls; services only inside a boundary seam (ADR-007 "Exceptions")

## Revision History
- 2025-01-22: Initial version

### Amendment (2026-11-03): provider rules removed

The provider types this ADR told services to use (`ICampaignTimeProvider`, `IModulePathProvider`, `IPlayerHeroProvider`, `ICampaignProvider`) do not exist in TAOM code, and the "Create if needed" rows were never created. Seams and adapters (ADR-007) met the need. Rule 3, the provider items in Rule 4, Rule 5, Implementation Patterns 1 to 3, the codebase examples and the Migration Guide left with them. Pattern 3 taught catching `NullReferenceException` as control flow in a service, which contradicts `.claude/rules/adapters.md`.

What is true now: engine statics in a service live only in a boundary seam (ADR-007). Rule 2 keeps one requirement: a new abstraction migrates every consumer in the same change. The enforcement claims of a pre-commit hook with a coverage minimum and a CI gate were false; the section now says so.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/README.md](./README.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
