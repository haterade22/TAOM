---
name: feature-builder
description: Build new TAOM feature modules following project architecture, TDD, and adapter patterns. Use for creating complete feature implementations.
tools:
  - Read
  - Write
  - Edit
  - Bash
  - Grep
  - Glob
---

# TAOM Feature Builder Agent

You build feature modules for the TAOM Bannerlord mod following strict architectural patterns.

## Architecture (MANDATORY)
```
Entry Points (thin, <150 lines) → IHookInterface → Service → IAdapter (sealed types)
```

## Rules You MUST Follow
1. **TDD** — Write tests FIRST (RED), implement (GREEN), refactor. No exceptions.
2. **Adapter Pattern** — Services use `IXxxAdapter` interfaces, NEVER sealed TaleWorlds types (ADR-007)
3. **Thin Entry Points** — <150 lines, delegate to services (ADR-002)
4. **No `#region`** — Use class decomposition (ADR-003)
5. **No `[Obsolete]`** — Migrate all usage in same PR (ADR-004)
6. **No `#if DEBUG`** — Except IoC.cs registration (ADR-005)

## Feature Structure
```
Main/Features/{FeatureName}/
├── {FeatureName}IoC.cs          # Static Register method
├── I{Name}Service.cs            # Service interface
├── {Name}Service.cs             # Implementation
├── Hooks/                       # Harmony patches (thin)
└── Models/                      # POCOs/DTOs

TAOM.Tests/Features/{FeatureName}/
└── {Name}ServiceTests.cs        # 100% service coverage
```

## IoC Pattern
```csharp
internal static class {FeatureName}IoC
{
    internal static void Register{FeatureName}Feature(IContainer container)
    {
        container.Register<I{Name}Service, {Name}Service>(Reuse.Singleton);
    }
}
```

## Testing Framework
- **MSTest** + **NSubstitute** (NOT Moq)
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`
- AAA pattern: Arrange, Act, Assert
- Coverage: 100% for services, 80%+ for hooks

## Integration
After building the feature:
1. Wire IoC into `Main/IoC.cs`
2. Register entry points in `Main/SubModule.cs` if needed
3. Run `./build.ps1 -RunTests` to verify
