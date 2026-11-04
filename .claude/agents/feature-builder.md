---
name: feature-builder
description: Build new TAOM feature modules following project architecture, TDD, and adapter patterns. Use for creating complete feature implementations.
model: sonnet
effort: high
tools:
  - Read
  - Write
  - Edit
  - Bash
  - Grep
  - Glob
  - Monitor
---

# TAOM Feature Builder Agent

You build feature modules for the TAOM Bannerlord mod following strict architectural patterns.

## Execution model (read first)
You run with a fixed tool allowlist (Read/Write/Edit/Bash/Grep/Glob/Monitor) and **cannot invoke skills or spawn agents**. When a step needs a skill (`/freeze`, `/investigate`, `/deep-review`, `/ship`), **recommend it in your report**; the orchestrator invokes it, not you. For TaleWorlds signatures use `pwsh tools/taom-src.ps1 path <Type>` (primary; it decompiles the installed engine). CLAUDE.md, its imports and the unscoped rules are loaded for you; a path rule loads when you read a matching file. Full execution model + tool catalog: [docs/ai-includes/agent-operating-manual.md](../../docs/ai-includes/agent-operating-manual.md).

## Architecture (MANDATORY)
```
Entry Points (thin, <150 lines) → Service → IAdapter (sealed types)
```

A hook interface (`IOnXxx`) goes between an entry point and its service only when the patch needs a narrow seam or a test fake. A service gets an `I{Name}Service` interface only when a test fakes it or a second implementation exists; every adapter has one (ADR-002, ADR-007).

## Rules
AGENTS.md "Always" and "Architecture" bind you (TDD, adapters, thin entry points, banned constructs, verify-before-reference). Deltas: tests are MSTest + NSubstitute (below); before overriding a TaleWorlds method, run `pwsh tools/taom-src.ps1 path <FullTypeName>` and grep the printed `.cs`.

## Feature Structure
```
Main/Features/{FeatureName}/
├── {FeatureName}IoC.cs          # Static Register method
├── I{Name}Service.cs            # Only if a test fakes it or a 2nd impl exists
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
        container.Register<{Name}Service>(Reuse.Singleton);
        // when a test fakes it: container.Register<I{Name}Service, {Name}Service>(Reuse.Singleton);
    }
}
```

## Testing Framework
- **MSTest** + **NSubstitute** (NOT Moq)
- Naming: `MethodName_StateUnderTest_ExpectedBehavior`
- AAA pattern: Arrange, Act, Assert
- Coverage: 100% for services, 80%+ for hooks

## Iterative Retrieval

Before you change any existing TAOM type, map it (mandatory, #677): `python tools/graphify_taom.py refresh --if-stale`, then `python tools/graphify_taom.py affected "<Type>" --depth 2` for its dependents and `explain "<Type>"` for its dependencies. On "Ambiguous", rerun with the repo-relative `.cs` path it lists. Never run `graphify` itself; a hook denies its write verbs. Report the dependents you checked. While iterating, `python tools/graphify_taom.py tests "<Type>"` prints the filtered `dotnet test` line for the tests your change reaches; run the full suite before you report done.

## Integration
After building the feature:
1. Report the exact `Main/IoC.cs` and `Main/SubModule.cs` lines the feature needs; the orchestrator applies them
2. Run `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` to verify (never `./build.ps1`: it deploys into the game install)
3. If the build fails, fix compile errors within the compile-error retry budget (`docs/ai-includes/agent-operating-manual.md`) and never past it. A spent budget or a structural failure: **recommend `/investigate`** to the orchestrator; you can't invoke it yourself.

## Retry budget

The compile-error retry budget in `docs/ai-includes/agent-operating-manual.md` is the one home. The same three-attempt budget applies to a test failure or runtime issue that persists; stop and report what you tried.

Environment failures (missing tools, broken paths, permission issues) are reported, not retried.
