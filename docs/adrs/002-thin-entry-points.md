# ADR-002: Thin Entry Points Pattern

**Status**: Accepted (Mandatory)
**Date**: 2025-01-11
**Context**: Mount & Blade II modding architecture

## Decision

All game integration entry points (Harmony patches, CampaignBehaviors, GameModels, MissionViews) MUST be thin orchestration layers that delegate to dedicated service classes. Game framework objects (sealed classes) MUST be wrapped in adapter interfaces to enable testability.

## Rationale

### Problems with Logic in Entry Points

1. **Untestable**: Harmony patches and behaviors depend on game lifecycle, making unit testing impossible
2. **Tight Coupling**: Business logic becomes coupled to game framework
3. **Violation of SRP**: Entry points handle both framework integration AND domain logic
4. **Poor Reusability**: Logic trapped in entry points cannot be reused
5. **Difficult to Mock**: Cannot test components that depend on behaviors/patches
6. **Sealed Game Classes**: TaleWorlds game objects (Hero, Settlement, ItemObject, etc.) are sealed and cannot be mocked

### Solution: Three-Layer Architecture

```
Entry Point (Thin Orchestration) -> Adapter Layer -> Service Layer (Domain Logic)
    |                                    |                |
GameModel/Behavior/Patch         IXxxAdapter          IService Interface
                                                      Service Implementation
```

**Layer Responsibilities**:
1. **Entry Point**: Receives game events, resolves dependencies, delegates to services
2. **Adapter Layer**: Wraps sealed game objects with mockable interfaces for testability
3. **Service Layer**: Contains all business logic, receives adapter interfaces, fully testable

## Rules

### FORBIDDEN in Entry Points

- Complex business logic
- Data transformations
- Algorithm implementations
- Direct game state mutations beyond delegation
- Multiple responsibilities

### ALLOWED in Entry Points

- Dependency injection via constructor (services and adapter factories)
- Event registration
- Simple delegation to services
- Adapter creation at the boundary, through a feature factory or a static helper
- Parameter extraction from game events
- UI message display (for behaviors only)

## Examples

Real code to copy, not samples:

- **GameModel:** `Main/Features/CulturalFeats/Models/TaomPartyMoraleModel.cs`
- **Harmony patch:** a thin patch under a feature's `Hooks/` folder, for example `Main/Features/AlignmentRecruitment/Hooks/Patch114_AiVolunteerEstimate_Postfix.cs`
- **CampaignBehavior that delegates:** `Main/Features/ArmyTargeting/ArmyTargetingLifecycleBehavior.cs`

### GameModel base calls

Inherit and call base. Never delegate to `MBGameModel.BaseModel` or a held model instance: your property overrides and initialized state do not reach it (`TaomBattleBannerBearersModel`).

## Service Design Guidelines

1. **Single Responsibility**: One service per feature domain
2. **Interface When Needed**: Define an `IServiceName` interface only when a test fakes the service or a second implementation exists; every adapter always has one (ADR-007)
3. **Constructor Injection**: Services receive dependencies via constructor
4. **No Game Dependencies**: Services should not depend on game lifecycle or sealed game types, except through protected-virtual boundary seams (ADR-007 "Exceptions")
5. **Adapter Interfaces Only**: Services MUST accept adapter interfaces (IHeroAdapter), NEVER sealed game types (Hero)
6. **No Reflection in Services**: Reflection on engine members belongs in an adapter, a boundary seam, or a Hooks patch, using `ReflectionHelper` or cached `AccessTools`. A service's decision logic never reflects (ADR-007)
7. **Testable**: All services must have unit tests using mocked adapters, or a test subclass that overrides the service's boundary seams (ADR-007 "Exceptions")

### Amendment (2026-09-24): service interfaces and boundary seams

Guideline 2 used to read "Always define `IServiceName` interface". The 2026-09-23 audit found most
service interfaces had exactly one implementation and many were faked by no test
(`plans/_audit/2026-09-23-opus/lane-2.findings.md`, ARCH-01), while `.claude/rules/think-before-coding.md`
forbade single-implementation interfaces. An interface now earns its file for one of two reasons: a
test fakes it, or a second class implements it. Adapters keep their interfaces unconditionally
(ADR-007). Existing interfaces are not deleted in bulk; one goes when its file is next touched and
nothing fakes it. Guidelines 4 and 7 now name the protected-virtual boundary seam that ADR-007
records.

## Adapter Pattern Implementation

### When to Use Adapters

Use adapters for ALL TaleWorlds sealed classes that need to be passed to services:
- `Hero`, `Settlement`, `Clan`, `Kingdom`
- `CharacterObject`, `ItemObject`, `Equipment`
- `MobileParty`, `PartyBase`, `MapEvent`
- `Agent`, `Town`, `Village`
- `CultureObject`, `WeaponComponentData`

### Adapter Factory Pattern

There is no central adapter factory. A new adapter is built at the boundary by a feature factory (`CareerHeroAdapterFactory`, `QuestHeroAdapterFactory`, `MissionAdapterFactory`, all under `Main/Adapters/`) or a static helper (`CultureFeatAdapter.FromOrNull`).

## Entry Point Checklist

Before merging code touching entry points, verify:
- [ ] Entry point class is <150 lines (excluding interface definitions)
- [ ] All complex logic delegated to services
- [ ] Service is registered in IoC container
- [ ] Service has an interface if a test fakes it or a second implementation exists (not otherwise)
- [ ] Service has unit tests using mocked adapters, or a test subclass that overrides its boundary seams
- [ ] Entry point only contains orchestration logic
- [ ] Game objects wrapped in adapters before passing to services
- [ ] Services accept adapter interfaces, NOT sealed game types

## Migration Strategy

When refactoring existing code:
1. Identify sealed game types in service signatures
2. Create adapter interfaces for those types (or use existing)
3. Implement adapters if needed (check `/Main/Adapters/` first)
4. Give the service adapter parameters, behind an `IServiceName` interface only when a test fakes the service or a second implementation exists (Service Design Guideline 2)
5. Extract logic to service implementation
6. Register service and adapters in IoC
7. Update entry point to build adapters at the boundary and delegate
8. Write tests for service using mocked adapters
9. Remove old logic from entry point

## Consequences

### Positive
- Testable business logic
- Clear separation of concerns
- Reusable services across entry points
- Easier to maintain and extend
- Better code organization

### Negative
- More files per feature (interface + implementation + entry point)
- Requires discipline to maintain pattern
- Initial setup overhead for new features

## Enforcement

Enforcement: `EntryPointLineCountGateTests` fails an entry point of 150 lines or more, against the shrink-only list of older classes in `EntryPointLineCountBaseline.txt`. `/deep-review` lens 1 rule 5 judges what a line count cannot, such as a patch holding logic.

### Amendment (2026-11-03): samples and the adapter factory

The BAD and GOOD code samples left this ADR. The GOOD patch passed `Agent` into a service, against ADR-007, and the GOOD model used an adapter factory interface that TAOM never had. The property-override section, which taught full reimplementation of model methods, also left; the warning it guarded survives as "GameModel base calls" above. The central `IAdapterFactory`, its caching benefits and every mention of it left too. Adapters are built per feature, as the "Adapter Factory Pattern" section now says. The earlier claims of architecture tests and a CLAUDE.md link did not hold. The same day `EntryPointLineCountGateTests` landed to back the line limit, and `ServiceLocatorGateTests` to hold the rule that only a boundary class calls `IoC.Resolve`. <!-- illustrative-name -->

## Related ADRs

- **ADR-007**: Adapter Pattern for Sealed Classes - Detailed adapter pattern implementation

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/009-self-documenting-code.md](./009-self-documenting-code.md)
- [docs/adrs/README.md](./README.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
