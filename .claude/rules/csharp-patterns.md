---
paths:
  - "Main/**/*.cs"
  - "Client/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM C# Design Patterns

Thread-local and cache patterns: `docs/ai-includes/patterns.md`. GameModel overrides: `gamemodels.md`.

## Hook layer

Optional layer. Most patches resolve their service at the boundary and call it directly (patch → service → adapter, AGENTS.md "Architecture"). Add an `IOnXxx` hook only when the patch needs a narrow seam over a wide service (`PartyUpgradeResourceCheckHook` narrows `ISpecialResourceService` for the party-upgrade patches) or a test fakes the hook; a hook that forwards one call adds a file and hides nothing.

## Transpiler Note

Transpilers iterate List<CodeInstruction> by hand; Harmony's CodeMatcher is available but unused here.

## View creator modules

A `[ViewCreatorModule]` class may hold only `[ViewMethod]` static methods: the engine reads attribute [0] of every static method unguarded, so a helper throws and breaks every mission view. The gate is `ViewCreatorModuleClasses_EveryStaticMethod_CarriesViewMethod` (`TAOM.Tests/Infrastructure/ViewCreatorModuleGateTests.cs`).

A `ConversationTag` subclass anywhere in TAOM.dll must be concrete, non-generic, public-parameterless and carry a unique `StringId`: the `ConversationManager` constructor instantiates every one at each campaign start and load, so one bad tag breaks every campaign. No shared base class. The gate is `EveryTaomConversationTag_IsInstantiableAndUnique` (`TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`).

## Anti-Patterns

- Business logic in Harmony patches (must delegate)
- Sealed types crossing service boundaries (use adapters)
- Regular null checks on computed TaleWorlds properties (use `?.` — see adapters.md)
- Multiple responsibilities in one service (split it)
- A local fear effect that drains morale below 0.01 with nothing said: each rout spreads a morale loss to up to 10 allies, from the whole formation when fewer stand within 4 m. Floor the morale, or say in the feature doc that its routs spread ([lesson](../../docs/reviews/lessons/adapters-taleworlds-api.md))
