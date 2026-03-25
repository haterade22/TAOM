# Race Age System

## Overview

The Race Age System introduces race-specific lifespans and fertility rates to TAOM, replacing Bannerlord's one-size-fits-all aging model. Elves are effectively immortal, Dwarves live for centuries, Orcs breed fast but die young, and Nazgul never age.

## Why This Exists

Vanilla Bannerlord treats every character identically — all heroes die around age 128 and have the same fertility window (18-45). In Middle-earth, races have vastly different lifespans:

- **Elves** are immortal — Elrond has lived thousands of years
- **Dwarves** live 250+ years — Dwalin was 340 at death
- **Men** live 60-85 years (Numenoreans longer, but that's a future enhancement)
- **Orcs** are short-lived (~60 years) but breed rapidly
- **Nazgul** are undead and cannot die of age

Without this system, Elven lords die of old age in-game, Orc populations stagnate, and the demographic feel of Middle-earth is lost.

## Architecture

### Design Challenge

Bannerlord's `AgeModel` exposes age thresholds as **single-value properties** (`MaxAge`, `BecomeOldAge`, etc.) — they cannot vary per race. The engine uses `MaxAge` globally to determine when heroes die of natural causes.

### Two-Layer Solution

1. **TaomAgeModel** (GameModel override) — Sets `MaxAge = 10000` to prevent the engine from killing anyone. Overrides `GetAgeLimitForLocation(CharacterObject)` for race-aware NPC age limits in settlements.

2. **RaceAgeBehavior** (CampaignBehavior) — Runs on `DailyTickEvent`, checks every living hero's age against their race-specific maximum. If a hero exceeds their racial lifespan, triggers `KillCharacterAction.ApplyByOldAge()`.

### Component Diagram

```
race_age_config.json
        |
  RaceAgeConfigProvider (loads JSON)
        |
    RaceAgeService (per-race lookups)
       / | \
      /  |  \
TaomAgeModel  TaomPregnancyModel  RaceAgeBehavior
(GameModel)    (GameModel)         (DailyTick)
     |              |                   |
  [Engine]     [Engine]         HeroAgeAdapter
                                     |
                               [TaleWorlds API]
```

## Configuration

### File: `Main/_Module/ModuleData/raceage/race_age_config.json`

Every race has an explicit entry. The `defaultRace` ("human") is used as a fallback for any race not found in the config.

| Field | Type | Description |
|-------|------|-------------|
| `maxAge` | int | Maximum lifespan. Heroes die when they exceed this. |
| `becomeOld` | int | Age when visual aging effects apply |
| `comesOfAge` | int | Minimum age to be considered an adult |
| `middleAge` | int | Middle adulthood threshold |
| `fertilityEnd` | int | Age when fertility drops to zero |
| `fertilityMod` | float | Multiplier on vanilla pregnancy chance (1.0 = normal) |
| `immortal` | bool | If true, hero never dies of old age and has 0 fertility |

### Current Race Values

| Race | Max Age | Comes of Age | Fertility Mod | Notes |
|------|---------|-------------|---------------|-------|
| human | 85 | 18 | 1.0x | Standard baseline |
| dwarf | 250 | 30 | 0.6x | Long-lived, low fertility |
| orc | 60 | 12 | 2.0x | Short-lived, high fertility |
| uruk_hai | 50 | 8 | 2.5x | Even shorter, highest fertility |
| berserker | 40 | 6 | 3.0x | Very short-lived |
| goblin | 50 | 10 | 2.0x | Similar to orcs |
| cave_troll | 500 | 20 | 0.1x | Very long-lived, rare breeding |
| hill_troll | 500 | 20 | 0.1x | Same as cave troll |
| nazghul | 10000 | 18 | 0.0x | Immortal, no children |
| saruman | 10000 | 18 | 0.0x | Immortal, no children |

Elves have no explicit race entry — any race not in the config falls back to human defaults. If you need Elven immortality, add an explicit entry with `"immortal": true`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/RaceAge/Models/RaceAgeConfig.cs` | Config data model |
| `Main/Features/RaceAge/IRaceAgeService.cs` | Service interface |
| `Main/Features/RaceAge/RaceAgeService.cs` | Core logic — race lookups, death checks |
| `Main/Features/RaceAge/IRaceAgeConfigProvider.cs` | Config loading interface |
| `Main/Features/RaceAge/RaceAgeConfigProvider.cs` | JSON loader |
| `Main/Features/RaceAge/Models/TaomAgeModel.cs` | GameModel override (MaxAge=10000) |
| `Main/Features/RaceAge/Models/TaomPregnancyModel.cs` | GameModel override (race fertility) |
| `Main/Features/RaceAge/RaceAgeBehavior.cs` | DailyTick age-death check |
| `Main/Features/RaceAge/RaceAgeIoC.cs` | DryIoc registration |
| `Main/Adapters/IHeroAgeAdapter.cs` | Adapter interface |
| `Main/Adapters/HeroAgeAdapter.cs` | TaleWorlds API wrapper |
| `Main/_Module/ModuleData/raceage/race_age_config.json` | Race age data |

## How Race Is Determined

`hero.CharacterObject.Race` returns an `int` — the index into `monsters.xml`. TAOM's `IRaceManager.GetRaceNameFromId(int)` maps this to a string name (e.g., "dwarf", "orc") which is used to look up the config entry.

## Dependencies

- `IRaceManager` (Core) — Race ID to name mapping
- `IPathService` (Core) — Module data path resolution
- `IModLogger` (Core) — Logging

## Tests

- `TAOM.Tests/Features/RaceAge/RaceAgeServiceTests.cs` — 18 tests covering all lookups, fallback, immortality, death threshold
- `TAOM.Tests/Features/RaceAge/RaceAgeConfigProviderTests.cs` — 4 tests for JSON loading, missing file, invalid JSON, immortal flag

## How to Add a New Race

1. Add the race to `race_age_config.json` with appropriate values
2. No code changes needed — the service automatically picks up new entries
3. The race must already exist in `monsters.xml` / `skins.xml` (see `docs/races-system` in memory)

## How Pregnancy Works

`TaomPregnancyModel` overrides `GetDailyChanceOfPregnancyForHero(Hero hero)`:

1. If the hero's race is immortal → return 0 (no children)
2. Get the base chance from vanilla (which checks age 18-45, spouse exists, etc.)
3. If base chance is 0 → return 0
4. If hero's age exceeds race-specific `fertilityEnd` → return 0
5. Multiply base chance by `fertilityMod`

This means Orc women have 2x the daily pregnancy chance of human women, while Dwarven women have 0.6x and Nazgul have 0x.
