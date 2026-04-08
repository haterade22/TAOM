---
paths:
  - "Main/**/Hooks/**"
  - "Main/**/Patches/**"
  - "Main/**/*Patch.cs"
---

# Harmony Patch Rules

## Research First (MANDATORY)
ALWAYS decompile the target method with `ilspycmd` before writing a patch. Verify:
- Exact method signature (parameters, return types, access modifiers)
- Whether the method is virtual, sealed, or static
- Correct namespace and class hierarchy
- Method existence in Bannerlord v1.3.12

## Patch Types
- **Prefix** — Runs before original method. Return `false` to skip original.
- **Postfix** — Runs after original method. Can modify `__result`.
- **Transpiler** — Modifies IL instructions. Most fragile — use sparingly.

## Architecture Requirements
- Patches are **thin entry points** — delegate ALL logic to services via `IHookInterface`
- Entry point files MUST be <150 lines (ADR-002)
- Resolve services from IoC container, never instantiate directly
- Use thread-local state pattern for multi-patch coordination

## Patch Organization
- Place in `Main/Features/{FeatureName}/Hooks/` directory
- Name: `{TargetClass}{TargetMethod}Patch.cs`
- Register in `SubModule.cs` patch categories (Patch0 through Patch6)

## Common Pitfalls
- Collection modification during iteration — use `.ToList()` copy
- Null handling — TaleWorlds often expects `TextObject.Empty` not `null`
- Event timing — verify when events fire vs when state changes
- Static state — avoid unless using thread-local pattern
- **Reflection in hot paths** — `AccessTools.Method` / `AccessTools.Field` lookups MUST be cached in a static field during `Initialize()`, never resolved inside `Prefix()`/`Postfix()`. Guard spawning calls the patch ~20x per settlement visit; uncached reflection means ~20 redundant lookups per entry.
