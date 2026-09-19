# Race Age System

## Overview

The Race Age System introduces race-specific lifespans and fertility rates to TAOM, replacing Bannerlord's one-size-fits-all aging model. Elves are effectively immortal, Dwarves live for centuries, Orcs breed fast but die young, and Nazgul never age.

## Why This Exists

Vanilla Bannerlord treats every character identically — all heroes die around age 128 and have the same fertility window (18-45). In Middle-earth, races have vastly different lifespans:

- **Elves** are immortal — Elrond has lived thousands of years
- **Dwarves** live 250+ years — Dwalin was 340 at death
- **Men** (`human`) live up to 200 years in the shipped config, fertile 18-60 (Numenoreans longer, but that's a future enhancement)
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
| `comesOfAge` | int | Start of the race's fertile window. It does not move the engine's adult age: `AgeModel.HeroComesOfAge` stays 18 (`TaomAgeModel` does not override it), and the pregnancy tick gates on that first, so a value below 18 has no effect |
| `middleAge` | int | Middle adulthood threshold |
| `fertilityEnd` | int | Age when fertility drops to zero |
| `fertilityMod` | float | Multiplier on vanilla pregnancy chance (1.0 = normal) |
| `immortal` | bool | If true, hero never dies of old age and has 0 fertility |

### Current Race Values

Read from `race_age_config.json` on 2026-09-19 (#628). Every race comes of age at 18.

| Race | Max Age | Fertile | Fertility Mod | Notes |
|------|---------|---------|---------------|-------|
| human | 200 | 18-60 | 1.0x | Baseline. Lifespan 200, but fertile only to 60 (#628; was 195) |
| dwarf | 250 | 18-220 | 0.6x | Long-lived, low fertility |
| orc | 60 | 18-45 | 1.3x | Short-lived, mildly higher fertility (#628; was 2.0x to 50) |
| uruk_hai | 80 | 18-40 | 1.5x | Highest orc-kin rate (#628; was 2.5x) |
| uruk | 85 | 18-45 | 1.3x | Standard Uruk variant (#628; was 2.0x) |
| pale_uruk | 85 | 18-45 | 1.3x | Pale Uruk variant (#628; was 2.0x) |
| dg_uruk | 85 | 18-45 | 1.3x | Dol Guldur Uruk variant (#628; was 2.0x) |
| berserker | 80 | 18-45 | 1.5x | (#628; was 3.0x to 50) |
| goblin | 50 | 18-40 | 1.3x | Similar to orcs (#628; was 2.0x) |
| cave_troll | 500 | 18-200 | 0.1x | Very long-lived, rare breeding |
| hill_troll | 500 | 18-200 | 0.1x | Same as cave troll |
| elf | 10000 | 18-300 | 0.15x | Effectively immortal (maxAge 10000), very rare children |
| nazghul | 10000 | none | 0.0x | Immortal flag, no children |
| saruman | 10000 | none | 0.0x | Immortal flag, no children |
| sauron | 10000 | none | 0.0x | Immortal flag, no children; lord_1_17's dedicated race (verbatim elf clone, adult min_scale 1.40, NPC-only; #321) |

`ShippedFertilityConfigTests` pins the ceilings: no race above 1.5x, orc-kin fertile no later than 45, humans no later than 60.

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
- `TAOM.Tests/Features/RaceAge/TaomPregnancyModelTests.cs`: `ComputeBaseChance` branches, including the #628 brake ordering and player clamp
- `TAOM.Tests/Features/RaceAge/ShippedFertilityConfigTests.cs`: loads the shipped `race_age_config.json` and `initial_child_generation.json` through the real providers and pins the #628 ceilings, the orc-culture exclusions, and that every excluded id is a real culture

## How to Add a New Race

1. Add the race to `race_age_config.json` with appropriate values
2. No code changes needed — the service automatically picks up new entries
3. The race must already exist in `monsters.xml` / `skins.xml` (see `docs/races-system` in memory)
4. **If the race will be playable in Character Creation, it also needs `as_<race>_facegen` + `as_<race>_female_facegen` entries in LOTRLOME's `action_sets.xml`** — without these the CC parent menu and post-parent stages (Early Childhood, Youth, Adolescence, Adulthood) render the agent as a contorted / lying-down mesh. The slim "declare only the 14 CC parent action types" form is **insufficient** — the engine requires the full ~106-action surface declared directly, no inheritance through `base_set`. Copy LOTRLOME's `as_dwarf_facegen` block verbatim, rename `id` + `base_set`, and add to BOTH the live LOTRLOME file AND the tracked snapshot. Full recipe in [`docs/features/character-creation.md`](character-creation.md#lotrlome-as_race_facegen-action_set-requirement-live-in-lotrlome_armory-not-taom) + the RCA at [`docs/reviews/rca-elf-cc-facegen-2026-05-22.md`](../reviews/rca-elf-cc-facegen-2026-05-22.md).
5. **If the race uses a STANDALONE combat action set** — its own `skeleton=` and NO `base_set` (the dwarf model: `as_dwarf_warrior skeleton="dwarf_skeleton_a"`) — it must maintain full action-type parity with Native's `as_human_warrior`, or the engine **CTDs** the first time it requests an action the set lacks (a dwarf falling into water → `act_dive_*`). Standalone sets inherit nothing, and an engine bump silently widens the gap (the dwarf set drifted to 423 missing types between Native 1.3 and 1.4.6). Bring it to parity with `python tools/patch_dwarf_action_parity.py --target <action_sets.xml> --set-id as_<race>_warrior --apply` (re-run after every engine bump — wired into `/engine-bump`). Races that instead use `base_set="as_human_warrior"` (orc/uruk/goblin, **and both trolls**) inherit everything and are exempt — `as_dwarf_warrior` is the only standalone humanoid set in the LIVE file today. See the lesson in [`docs/reviews/LESSONS-LEARNED.md`](../reviews/LESSONS-LEARNED.md) → Animation & Skeleton.

## How Pregnancy Works

`TaomPregnancyModel` **reimplements** `GetDailyChanceOfPregnancyForHero(Hero hero)` rather than calling `base`. This is necessary because the vanilla `DefaultPregnancyModel` hardcodes fertility age bounds to 18-45 in a private `IsHeroAgeSuitableForPregnancy` method — calling `base` would return 0 for any hero over age 45, defeating race-specific fertility windows (e.g., Dwarves with `fertilityEnd: 220`).

### Calculation Steps

1. If the hero's race is immortal → return 0 (no children)
2. If hero has no spouse → return 0
2b. If the **spouse's** race is immortal → return 0. The engine only ever calls this model for the FEMALE (`PregnancyCampaignBehavior.DailyTickHero` gates on `hero.IsFemale`), so an immortal FATHER (Sauron with consort Morgha, #321) is only blocked by this spouse-side check — his own race entry never reaches step 1.
3. If hero's age is outside race-specific `[comesOfAge, fertilityEnd]` window → return 0
4. Calculate age-decline factor: the fertility curve spans the full racial window, declining linearly from peak (1.2) at `comesOfAge` to floor (0.12) at `fertilityEnd`
5. Apply vanilla clan population cap (based on clan tier) and children penalty (quadratic decay)
6. Multiply by race-specific `fertilityMod`, **but a bonus (above 1.0) only while the clan is at or under its cap.** Once `aliveLords` passes `cap = 4 + 4*clanTier` (population factor below 1), and for any marriage involving the player (which skips the cap, as in vanilla), the modifier is clamped to at most 1.0. A penalty below 1.0 (elf, dwarf, troll) always applies. Before #628 the bonus multiplied after the brake, so an orc clan at 1.5x its cap still bred at vanilla's unbraked rate and orc clans sat at the `2*cap` ceiling.
7. Apply Charm.Virile perk bonus (checked on both hero and spouse)

### Age-Decline Formula

```
declineRate = 1.08 / (fertilityEnd - comesOfAge)
ageFactor = 1.2 - (heroAge - comesOfAge) * declineRate
```

This preserves the vanilla curve shape but stretches or compresses it to fit each race's fertility window. A Dwarf at age 60 (early in their 18-220 window) has roughly the same relative fertility as a Human at age 25 (early in their 18-60 window).

### Effective Fertility Rates

Peak daily chance is for a first child at `comesOfAge`, in an NPC clan at or under its cap: `1.2 * 0.12 * fertilityMod`.

| Race | Window | Peak Daily Chance | Modifier | Notes |
|------|--------|-------------------|----------|-------|
| human | 18-60 | 14.4% | 1.0x | Vanilla peak; window 42 years against vanilla's 27 |
| dwarf | 18-220 | 8.6% | 0.6x | Much wider window |
| orc, uruk, goblin | 18-45 (goblin 40) | 18.7% | 1.3x | Bonus only at or under the clan cap |
| uruk_hai, berserker | 18-40 / 18-45 | 21.6% | 1.5x | Bonus only at or under the clan cap |
| elf | 18-300 | 2.2% | 0.15x | Very rare children, extremely wide window |
| nazghul | none | 0% | 0.0x | Immortal flag blocks fertility entirely |

So an orc woman in an NPC clan at or under its cap has 1.3x a human woman's daily chance; past the cap, or married to the player, she has the human rate. Dwarven women have 0.6x and Nazgul 0x everywhere.

**Start-of-campaign children:** `InitialChildGenerationService` tops every major-faction clan up to `ceil(adults/2)` children on day one, except the cultures in `configs/initial_child_generation.json` `excluded_cultures`. Every orc culture is excluded: `mordor`, `isengard`, `gundabad`, `dolguldur`, and since #628 `goblin`, `mistymountainorcs` and `bluecraig`. `ShippedFertilityConfigTests` derives the orc cultures from `cultures.json` (default race orc-kin), so a new orc kingdom listed there fails the test until it is excluded. A culture never offered at character creation is absent from `cultures.json` and escapes the check. The same tests fail on an `excluded_cultures` id that names no culture, since a typo there excludes nothing.

**Reload scope:** `race_age_config.json` and `initial_child_generation.json` are read once per process (both providers are `Reuse.Singleton`), so an edit needs a full restart of Bannerlord; a new campaign or a save load does not re-read them. After a restart the pregnancy values apply from the next daily tick, on existing saves too; the exclusion list reaches new campaigns only.

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
- [docs/modding/body-properties.md](../modding/body-properties.md)
- [docs/modding/configs-balance.md](../modding/configs-balance.md)
- [docs/modding/recipe-add-a-race-or-creature.md](../modding/recipe-add-a-race-or-creature.md)

<!-- backlinks-end -->
## Changelog

- 2026-09-19: Orcs and goblins overbred (#628). The race bonus now applies only while a clan is under its population cap and never to a player marriage; orc-kin modifiers cut from 2.0 to 3.0 down to 1.3 to 1.5; orc and berserker fertile to 45 (was 50); humans fertile to 60 (was 195); `goblin`, `mistymountainorcs` and `bluecraig` excluded from start-of-campaign children like the other orc cultures. The values table above was also stale against the file and is refreshed.
- 2026-07-27 — Two fixes found while cutting debug-log volume. **Deaths are announced only once they happen:** `IHeroAgeAdapter.KillByOldAge` now returns `bool` (re-reading `IsAlive` after the action) and `RaceAgeBehavior` kills before it logs. `KillCharacterAction.ApplyInternal` marks-and-defers when the victim is in a `MapEvent`/`SiegeEvent`, refuses the player character, and no-ops when the life/death cycle is disabled — all without changing `HeroState` — so the previous log-then-kill order announced deaths that had not occurred, and re-announced them on the next daily tick (16 duplicates in one session). **Immortals keep their authored fertility window:** `RaceAgeConfigProvider` no longer reads `"fertilityEnd": 0` on an immortal race as an inverted range and overwrites it with 18/45. That value is the deliberate "cannot reproduce" sentinel for `nazghul` / `saruman` / `sauron`; the overwrite was masked by `TaomPregnancyModel`'s `IsImmortal` short-circuit but visible through the public `IRaceAgeService.GetFertilityEndAge`, and it warned three times on every session start.
- 2026-06-23 — Restored the `DeliverOffSpring_RaceAssert_Patch` transpiler (`Patch13_RaceAge`) to suppress the harmless `mother.Race == father.Race` SilentAssert on cross-race births (#283).
- 2026-05-13 — RaceAge hardening: `_raceIdCache` reset on session launch, validate-before-lookup in `GetEntry`, semantic validation in `RaceAgeConfigProvider.LoadConfig`; extracted `TaomPregnancyModel.ComputeBaseChance` pure helper (#179) and fixed its `heroAge` int-truncation regression to use float `Hero.Age`.
- 2026-04-06 — Adversarial-review fixes: `comesOfAge=18` standardized and `becomeOld` set per-race.
