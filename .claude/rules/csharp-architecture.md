---
paths:
  - "Main/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM Architecture Quick Reference

Full guide: `docs/ai-includes/architecture.md`

## Layer Stack

```
HarmonyPatch / GameModel / CampaignBehavior   ← THIN (<150 lines, no logic)
                    │ delegates to
              Service (IXxxService)            ← ALL business logic here
                    │ uses
              Adapter (IXxxAdapter)            ← wraps sealed TaleWorlds types
                    │ wraps
         TaleWorlds Engine (Hero, Agent…)      ← sealed, never cross boundary
```

## Non-Negotiable Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service |
| No sealed types in services | ADR-007: `IHeroAdapter` not `Hero` |
| Constructor injection only | No service locator in services |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check — see `adapters.md` |

## IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

## Test Coverage Requirements (ADR-008)

| Component | Required | Notes |
|-----------|----------|-------|
| Services | 100% | Must be mockable via constructor injection |
| Engines | 100% | Pure functions — easy to test |
| Hooks | 80%+ | Use `NSubstitute` mocks for adapters |
| Entry Points | Not required | Harmony/GameModel — test via game |

## Entity State Matrix (MANDATORY for OnGameLoaded behaviors)

Any `CampaignBehaviorBase` that **mutates Hero/Settlement/Clan state on load** must enumerate all possible entity states before writing the mutation code. Build a state matrix:

| State | Key Properties | Should mutate? |
|-------|---------------|----------------|
| (each possible state) | (property values) | Yes/No + why |

**Why:** Review #23 found a HIGH bug where `EnsureCompanionsPlaced()` teleported recruited companions out of the player's party on load because the "skip if already placed" check didn't account for traveling-with-party state. The state matrix would have caught this at design time.

**Rule:** If your OnGameLoaded handler calls `ChangeState`, `EnterSettlementAction`, `SetHeroRace`, or any other state-mutating action on a Hero, enumerate:
- Unrecruited / idle in settlement
- Recruited / in player party (traveling on map)
- Recruited / in player party (visiting settlement)
- Dead / disabled
- Prisoner
- Fugitive

Skip any state where mutation would corrupt the entity.

**Idempotent vs destructive:** Before copying a behavior pattern from another feature, ask: "Is this operation idempotent?" Injecting a banner color twice is harmless. Moving a Hero between locations is destructive. Destructive load-path operations need stricter guards than their new-game counterparts.

## Config Providers MUST Validate (MANDATORY for user-editable JSON/XML)

Any provider that loads `Main/_Module/ModuleData/` JSON or XML the player is expected to edit (retuning knobs, enable/disable flags, tunable thresholds) must validate semantic constraints after deserialization, not just syntax. Parse success is NOT validation success.

**Rule:** If the feature doc tells the user "edit this file to retune," the provider's `LoadConfig` (or equivalent) must:
1. Range-check every numeric field against its engine-valid bounds
2. Enforce ordering invariants between related fields (e.g., warning-threshold ≥ trigger-threshold)
3. Reject sign flips on fields whose meaning is directional (penalties must be ≤ 0; bonuses must be ≥ 0)
4. Log a warning and fall back to the compiled default for any field that fails — never silently apply a bad value
5. Emit a summary warning when any reversion occurred so the user knows to look at prior warnings

**Why:** Review #25 (RevoltTuning) found a HIGH bug where the provider logged "Loaded" success for any parseable file. A plausible user edit like a sign-flipped penalty `1.0` (should be `-1.0`) would silently flip the feature from "soften revolts" to "accelerate revolts" with no warning. Syntax-error tests (missing file, malformed JSON) did not cover this class of failure.

**Test requirement:** Tests must cover semantically-invalid-but-parseable values for every validated field — not just missing-file and malformed-JSON cases. One test per validation rule.

**Doc requirement:** When documenting "edit this file to retune," state the reload scope explicitly. `Reuse.Singleton` providers (the TAOM default) cache for the entire Bannerlord process — changes require a full application restart, not a new campaign or save-load. Never claim "next game load" without cross-checking the DryIoc lifetime.

## File Layout

```
Main/Features/MyFeature/
├── IMyFeatureService.cs
├── MyFeatureService.cs
├── MyFeatureIoC.cs          ← Reuse.Singleton registrations
├── Models/
│   └── TaomMyModel.cs       ← GameModel override (if needed)
└── Hooks/
    └── MyPatch.cs           ← Harmony patch (if needed)
Main/Adapters/
├── IMyTypeAdapter.cs
└── MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
└── MyFeatureServiceTests.cs
```

## Stale-file re-read

Long sessions edit many files. Cached `Read` content drifts: a teammate-agent may have re-written the same file, a hook or skill may have run `dotnet format`, the user may have edited via the IDE. Editing against stale content produces opaque "no match" failures that look like permission/conflict bugs.

**Rule:** Before editing any C# file you have not Read in the last ~10 tool calls of the current turn, re-Read it.

- Hard signal to re-Read: another agent ran in this turn; `git status` shows changes you didn't make; the Edit tool returns a "string not found" error.
- Soft signal to re-Read: you're about to make >1 edit to the same file, the file is in a hot area (Main/Adapters, GameModels), or it's been more than ~5 minutes wall-clock since you last looked.

The re-Read costs nothing. The Edit failure plus diagnosis costs minutes.
