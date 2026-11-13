---
paths:
  - "Main/**/*.cs"
  - "Client/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM C# Design Patterns

Thread-local and cache patterns: `docs/ai-includes/patterns.md`. GameModel overrides: `gamemodels.md`.

## Reuse before writing

Top-down, stopping at the first rung that works:

1. The engine already provides it (a GameModel hook, a `CampaignEvent`): verify with `taom-src`.
2. An existing TAOM service or adapter already does it (ADR-002, ADR-007).
3. A one-line delegation into that service would do.
4. Only then write the minimum: an interface only to wrap an engine type (an adapter), for a test
   fake, for a second implementer or to narrow a wide service for a patch (a hook, AGENTS.md
   "Architecture"); no plumbing "for later" (`simplicity-criterion.md`).

Cache `IoC.Resolve` lazily on a hot path.

## Hook layer

Optional layer. Most patches resolve their service at the boundary and call it directly (patch → service → adapter, AGENTS.md "Architecture"). Add an `IOnXxx` hook only when the patch needs a narrow seam over a wide service (`PartyUpgradeResourceCheckHook` narrows `ISpecialResourceService` for the party-upgrade patches) or a test fakes the hook; a hook that forwards one call adds a file and hides nothing.

## Transpiler Note

Transpilers iterate List<CodeInstruction> by hand; Harmony's CodeMatcher is available but unused here.

## View creator modules

A `[ViewCreatorModule]` class may hold only `[ViewMethod]` static methods: the engine reads attribute [0] of every static method unguarded, so a helper throws and breaks every mission view. The gate is `ViewCreatorModuleClasses_EveryStaticMethod_CarriesViewMethod` (`TAOM.Tests/Infrastructure/ViewCreatorModuleGateTests.cs`).

A `ConversationTag` subclass anywhere in TAOM.dll must be concrete, non-generic, public-parameterless and carry a unique `StringId`: the `ConversationManager` constructor instantiates every one at each campaign start and load, so one bad tag breaks every campaign. No shared base class. The gate is `EveryTaomConversationTag_IsInstantiableAndUnique` (`TAOM.Tests/Infrastructure/Localization/LiegeTitleOverrideTests.cs`).

## Renames, moves and deletes

After a rename, move or delete, grep the old identifier across `docs/` and `CLAUDE.md` too: update living docs, leave RCAs and CHANGELOG. The code graph holds no docs.
