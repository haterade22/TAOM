---
name: new-feature
description: Scaffold a new TAOM feature module with IoC, services, adapters, and tests following project conventions
argument-hint: [FeatureName]
---

# New Feature Scaffold

Create a new feature module following TAOM architecture from @docs/ai-includes/architecture.md

## Feature Name: `$ARGUMENTS`

## Map what the feature touches first (mandatory, #677)

Before the first file, run `python tools/graphify_taom.py refresh --if-stale` (background when stale, about 2.5 minutes), then for every existing TAOM service, adapter, interface or GameModel the feature will call, extend or change: `python tools/graphify_taom.py explain "<Type>"` (what it injects and calls) and `python tools/graphify_taom.py affected "<Type>" --depth 2` (who depends on it). A type with many dependents is one to extend, not modify. On "Ambiguous", rerun with the repo-relative `.cs` path it lists; on no result for a type name, retry with its file path before concluding nothing uses it (an IoC registration shows as a file import). graphify reads no XML: data refs go through the taom-moduledata MCP or `python tools/validate_moduledata.py`.

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
Follow @docs/ai-includes/tdd-enforcement.md

Create under `TAOM.Tests/Features/$ARGUMENTS/`:
- `{FeatureName}ServiceTests.cs` — 100% service coverage
- Use MSTest + NSubstitute
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`
- Write tests FIRST (RED), then implement (GREEN), then refactor

## Scope-lock recommendation

Before scaffolding, suggest `/freeze` to the user with the new feature dir as the boundary — prevents drift into adjacent code while the feature is taking shape. Widen the boundary (or `/unfreeze`) only when wiring `Main/IoC.cs` or `Main/SubModule.cs` for integration.

## Checklist
- [ ] Touched TAOM types mapped with `tools/graphify_taom.py explain` / `affected` before the first file
- [ ] Tests written first (RED state verified)
- [ ] All services use adapter interfaces, not sealed types
- [ ] IoC registered in `Main/IoC.cs`
- [ ] Entry points <150 lines
- [ ] No `#region`, `[Obsolete]`, or `#if DEBUG`
- [ ] Build passes: `./build.ps1 -RunTests` — if it fails, route to `/build-fix`; if structural, `/investigate`
- [ ] `/deep-review` clean before commit (per the CLAUDE.md Skills index)
