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
| uruk | 55 | 10 | 2.0x | Standard Uruk variant |
| pale_uruk | 55 | 10 | 2.0x | Pale Uruk variant |
| dg_uruk | 55 | 10 | 2.0x | Dol Guldur Uruk variant |
| berserker | 40 | 6 | 3.0x | Very short-lived |
| goblin | 50 | 10 | 2.0x | Similar to orcs |
| cave_troll | 500 | 20 | 0.1x | Very long-lived, rare breeding |
| hill_troll | 500 | 20 | 0.1x | Same as cave troll |
| elf | 10000 | 18 | 0.15x | Effectively immortal (maxAge 10000), very rare children |
| nazghul | 10000 | 18 | 0.0x | Immortal flag, no children |
| saruman | 10000 | 18 | 0.0x | Immortal flag, no children |

**Elf vs Nazgul immortality:** Elves use `maxAge: 10000` without the `immortal` flag — they effectively never die of age, but can still have rare children (`fertilityMod: 0.15`, `fertilityEnd: 300`). Nazgul/Saruman use `"immortal": true` which additionally blocks all fertility. Any race not in the config falls back to human defaults.

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
| `Main/Features/RaceAge/Hooks/GetCivilianEquipment_Patch.cs` | Harmony patch — defensive fallback for missing child equipment rosters |
| `Main/Adapters/IHeroAgeAdapter.cs` | Adapter interface |
| `Main/Adapters/HeroAgeAdapter.cs` | TaleWorlds API wrapper |
| `Main/_Module/ModuleData/raceage/race_age_config.json` | Race age data |
| `Main/_Module/ModuleData/taom_child_equipment_templates.xml` | Child equipment rosters for all 10 custom cultures |

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
4. **If the race will be playable in Character Creation, it also needs `as_<race>_facegen` + `as_<race>_female_facegen` entries in LOTRLOME's `action_sets.xml`** — without these the CC parent menu and post-parent stages (Early Childhood, Youth, Adolescence, Adulthood) render the agent as a contorted / lying-down mesh. The slim "declare only the 14 CC parent action types" form is **insufficient** — Bannerlord 1.3 requires the full ~106-action surface declared directly, no inheritance through `base_set`. Copy LOTRLOME's `as_dwarf_facegen` block verbatim, rename `id` + `base_set`, and add to BOTH the live LOTRLOME file AND the tracked snapshot. Full recipe in [`docs/features/character-creation.md`](character-creation.md#lotrlome-as_race_facegen-action_set-requirement-live-in-lotrlome_armory-not-taom) + the RCA at [`docs/reviews/rca-elf-cc-facegen-2026-05-22.md`](../reviews/rca-elf-cc-facegen-2026-05-22.md).

## How Pregnancy Works

`TaomPregnancyModel` **reimplements** `GetDailyChanceOfPregnancyForHero(Hero hero)` rather than calling `base`. This is necessary because the vanilla `DefaultPregnancyModel` hardcodes fertility age bounds to 18-45 in a private `IsHeroAgeSuitableForPregnancy` method — calling `base` would return 0 for any hero over age 45, defeating race-specific fertility windows (e.g., Dwarves with `fertilityEnd: 120`).

### Calculation Steps

1. If the hero's race is immortal → return 0 (no children)
2. If hero has no spouse → return 0
3. If hero's age is outside race-specific `[comesOfAge, fertilityEnd]` window → return 0
4. Calculate age-decline factor: the fertility curve spans the full racial window, declining linearly from peak (1.2) at `comesOfAge` to floor (0.12) at `fertilityEnd`
5. Apply vanilla clan population cap (based on clan tier) and children penalty (quadratic decay)
6. Multiply by race-specific `fertilityMod`
7. Apply Charm.Virile perk bonus (checked on both hero and spouse)

### Age-Decline Formula

```
declineRate = 1.08 / (fertilityEnd - comesOfAge)
ageFactor = 1.2 - (heroAge - comesOfAge) * declineRate
```

This preserves the vanilla curve shape but stretches or compresses it to fit each race's fertility window. A Dwarf at age 60 (early in their 30-120 window) has roughly the same relative fertility as a Human at age 25 (early in their 18-45 window).

### Effective Fertility Rates

| Race | Window | Peak Daily Chance | Modifier | Notes |
|------|--------|-------------------|----------|-------|
| human | 18-45 | ~14.4% | 1.0x | Vanilla baseline |
| dwarf | 30-120 | ~14.4% | 0.6x | Same peak, 60% rate, much wider window |
| orc | 12-50 | ~14.4% | 2.0x | 2x rate, compensates for shorter lifespan |
| uruk_hai | 8-40 | ~14.4% | 2.5x | Highest rate, shortest window |
| elf | 18-300 | ~14.4% | 0.15x | Very rare children, extremely wide window |
| nazghul | — | 0% | 0.0x | Immortal flag blocks fertility entirely |

This means Orc women have 2x the daily pregnancy chance of human women, while Dwarven women have 0.6x and Nazgul have 0x.

## Offspring Equipment

When a hero gives birth, vanilla `DefaultHeroCreationModel.GetCivilianEquipment` calls `GetEquipmentRostersForDeliveredOffspring(hero)`, which filters `EquipmentRoster` entries by `culture == hero.Culture` with flags `IsChildEquipmentTemplate="true"`. Each culture needs 6 child equipment roster entries: noble/townsman/villager × male/female.

### Child Equipment Templates

`taom_child_equipment_templates.xml` provides 60 equipment rosters (6 per culture × 10 custom cultures). Each roster has 2 `EquipmentSet` variants with Body + Leg slots using the lightest civilian items from each culture's Armory. Lothlorien shares rivendell items; umbar shares gondor items.

The 6 XSLT cultures (empire/dunland, aserai/harad, vlandia/rohan, khuzait/rhun, sturgia/dale, battania/variag) are covered by vanilla `sandbox_equipment_sets.xml` since they retain their original culture IDs.

## Performance

The daily tick iterates all alive heroes to check age-based death. Several optimizations minimize per-tick cost:

- **Lazy enumeration with two-pass death** — `IHeroAgeAdapter.GetAllAliveHeroAges()` returns `IEnumerable<HeroAgeInfo>` (not a materialized list). `RaceAgeBehavior` uses a two-pass approach: first iterates the lazy enumerable to collect heroes that should die into a reusable `_deathList` field, then kills them in a second pass after enumeration is complete. This avoids both unnecessary list allocation AND the "collection was modified during enumeration" crash that occurs when killing a hero removes it from `Hero.AllAliveHeroes` mid-iteration. The `_deathList` is `.Clear()`'d each tick — zero GC allocation in the common case (no deaths).
- **O(1) hero lookup** — `KillByOldAge` uses `Hero.Find(heroId)` (dictionary-backed via `CampaignObjectManager`) instead of `Hero.FindFirst` (O(n) linear scan over all characters).
- **Race entry cache** — `RaceAgeService` caches `raceId → RaceAgeEntry` in a `Dictionary<int, RaceAgeEntry>`. The string-based race name lookup (`IRaceManager.GetRaceNameFromId`) happens once per race ID ever, not on every property access for every hero every tick. This cache is purely in-memory on the singleton service — no save/load impact.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
