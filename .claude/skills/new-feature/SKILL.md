---
name: new-feature
description: Scaffold a new TAOM feature module with IoC, services, adapters, and tests following project conventions
argument-hint: [FeatureName]
---

# New Feature Scaffold

Create a new feature module following TAOM architecture from @docs/ai-includes/architecture.md

## Feature Name: `$ARGUMENTS`

A feature adopted from another mod gets a Tolkien-flavoured TAOM name, never the source mod's, for its folder and MCM group too. Grep for clashes first (maintainer, 2026-10-18).

## Required Structure

Create the following files under `Main/Features/$ARGUMENTS/`:

### 1. IoC Registration
- `{FeatureName}IoC.cs` — Static `Register{FeatureName}Feature(IContainer container)` method
- Register all services as `Reuse.Singleton`
- Wire into `Main/IoC.cs` by calling from `Configure()`

### 2. Service Layer
- `I{FeatureName}Service.cs`: only when a test fakes the service or a second implementation exists (ADR-002); otherwise register the concrete class, `container.Register<{FeatureName}Service>(Reuse.Singleton)`
- `{FeatureName}Service.cs` — Implementation with constructor-injected dependencies
- Services MUST use adapter interfaces (`ICareerHeroAdapter`, `IAgentAdapter`, etc.), NEVER sealed TaleWorlds types (ADR-007)

### 3. Entry Points (if needed)
- `Hooks/{PatchName}Patch.cs` — Harmony patches (thin, delegate to service)
- Entry points MUST be <150 lines (ADR-002)

### 4. Adapters (if needed)
- `I{TypeName}Adapter.cs` + `{TypeName}Adapter.cs` under `Main/Adapters/`
- Only create if wrapping a new sealed TaleWorlds type not already adapted

### 5. Tests (MANDATORY — TDD)
Follow .claude/rules/tests.md (it loads when you open a test).

Create under `TAOM.Tests/Features/$ARGUMENTS/`:
- `{FeatureName}ServiceTests.cs`: one test per behaviour and guard, judged by the two-edit test
- Use MSTest + NSubstitute
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Write tests FIRST (RED), then implement (GREEN), then refactor

## Scope-lock recommendation

Before scaffolding, suggest `/freeze` to the user with the new feature dir as the boundary — prevents drift into adjacent code while the feature is taking shape. Widen the boundary (or `/unfreeze`) only when wiring `Main/IoC.cs` or `Main/SubModule.cs` for integration.

## Checklist
- [ ] Tests written first (RED state verified)
- [ ] All services use adapter interfaces, not sealed types
- [ ] IoC registered in `Main/IoC.cs`
- [ ] Entry points <150 lines
- [ ] No `#region`, `[Obsolete]`, or `#if DEBUG`
- [ ] `/verify` passes; if it fails, route to `/build-fix`; if structural, `/investigate`
- [ ] `/deep-review` clean before commit (per CLAUDE.md Critical Rules)
