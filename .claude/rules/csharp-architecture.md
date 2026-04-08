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
