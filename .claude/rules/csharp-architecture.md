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
