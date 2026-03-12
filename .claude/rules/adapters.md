---
paths:
  - "Main/Adapters/**"
  - "Main/**/I*Adapter.cs"
  - "Main/**/*Adapter.cs"
---

# Adapter Pattern Rules (ADR-007)

## Core Principle
Services NEVER accept sealed TaleWorlds types directly. Always wrap with adapter interfaces.

## Creating New Adapters
1. **Research first** — Decompile the TaleWorlds class with `ilspycmd` before creating the adapter interface
2. **Interface in `Main/Adapters/`** — `I{TypeName}Adapter.cs` with only the properties/methods the feature needs
3. **Implementation in `Main/Adapters/`** — `{TypeName}Adapter.cs` wrapping the sealed type
4. **Recursive wrapping** — If the sealed type exposes other sealed types, wrap those too
5. **Defensive validity** — Check for dead agents, null references in computed properties

## Property Guidelines
- Identify read-only (get-only) vs read-write properties from decompiled source
- Use null-conditional operators (`?.`) for computed properties accessing nested objects
- Cache expensive property lookups where appropriate

## Testing
- Adapters themselves are thin wrappers — test coverage via service tests that mock the adapter interface
- Use `NSubstitute.Substitute.For<IXxxAdapter>()` in tests
