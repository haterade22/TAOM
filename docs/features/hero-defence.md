# Hero Defence

## Overview

Hero Defence is a planned third campaign-free mission mode, beside The Long Defeat and Fords of War. It is a tower defence where the
towers are named heroes: creeps from every culture walk one serpentine road to an exit, and the player buys heroes (Legolas, Gimli,
Aragorn and others) and places them during a pause. When the wave comes the heroes fight by themselves. Each creep that reaches the
exit costs lives, and the run is endless until the lives run out.

**Status (2026-11-09):** in development under #1106. The pure core is built: the run rules and the wave generator, with their
config readers and shipped data. Nothing is registered in IoC or `SubModule.xml`, so the mode is not in the game yet. The engine
recipes come from spike runs on the Fords of War scene, and their results go into this doc at Gate 1.

## Why This Exists

- **Vanilla behavior:** Bannerlord has no tower defence. Custom Battle sends two armies at each other.
- **TAOM requirement:** a short, replayable mode where the player places Middle-earth heroes against waves of every culture's
  troop tree, with a gold sink (hero levels) and a score to beat.
- **Without this feature:** nothing breaks. It is new content.

## Architecture

### Design Challenge

A Bannerlord AI hero must hold a chosen point and still fight, and creeps must follow one long road in packs without the team AI.
Both are engine behaviour, so the rules stay in a pure domain that an engine director drives, and the risky engine recipes are
proven in spikes before the scaffold is built.

### Solution Approach

- `Domain/` holds plain rules with no engine type (`HeroDefenceDomainPurityTests`):
  - `HeroDefenceRun` (with `.Shop` and `.Wave` partials): phases (`Pause`, `Wave`, `Ended`), gold, lives, kills, the shop, placement,
    wave release, leaks, the wave time limit, the clear bonus, and heroes who fall and return at the next pause. Each `Tick` returns
    a `DefenceEffects` list (hero spawns, moves and resets, creep batches, removals) for the director to apply.
  - `CreepWavePlan` (with `.Troops`): builds each wave from the seed and the wave number alone, so a preview and its wave cannot
    differ. It draws the culture rotation, the level band, the class counts, the troops, the leader and the powers.
  - `PlacementRules`: whether a hero can stand at a clicked point (zone, boundary, distance from the road ends and from other posts).
- `Config/` reads the JSON files. A reader never throws: a bad value reverts to its default with one warning, and a file with a
  repeated key or a zero-byte file reverts as a whole.
- The director, the adapters, the launcher and the client views come in later slices (plan Slices 3 to 14).

### Director contract

The director is the engine side that applies each tick's `DefenceEffects`. The run trusts it to keep these rules:

1. Apply `WaveBegan` before the creep batches of the same tick, because creep armour is written once at spawn.
2. Report every creep of a `CreepBatch` through `CreepSpawned`, with the batch's slot, before the next `Tick`. Until then the run
   counts no creep of that pack.
3. Give each creep a unique `AgentKey`. `CreepSpawned` ignores a key that is already alive, so a repeat cannot count a creep twice.
4. Report each removal through `CreepGone`. A second report for the same key changes nothing.
5. A creep that fails to spawn has no API yet. The run never counts it, so the wave clears without it; Slice 4 decides whether
   that needs a report.

A tick that ends the pause charges only the rest of its time to the wave, so a long tick cannot use up the wave's time limit.

The map's entry and exit come from scene tags in Slice 3. A NaN or infinite position fails every distance test, so no creep would
leak and no post would be too close to a road end: the tag reader must refuse such a map.

### Component Diagram

```
waves.json  cultures.json  rosters.json
        \        |        /
      HeroDefenceConfigCatalog (readers, never throw)
                 |
      WaveCatalog + DefenceRules
         /                 \
  CreepWavePlan       HeroDefenceRun  <-- director (later slice) applies DefenceEffects,
  (wave briefs)       (run rules)         reports spawns, deaths and leaks
```

Reused unchanged from the other modes: `Vec3f` and `AgentKey` (`Main/Features/FordsOfWar/Domain/`), `RunRandom` and `EndlessScale`
(`Main/Features/LongDefeat/Domain/`).

## Configuration

### Config File: `Main/_Module/ModuleData/hero_defence/waves.json`

| Group | Fields | What it controls |
|-------|--------|------------------|
| `rules` | `startingGold`, `startingLives`, `firstPauseSeconds`, `pauseSeconds`, `maxPlaced`, `earlyGoldPerSecond`, `earlyGoldCap`, `waveBonusBase`, `waveBonusPerWave`, `bountyBase`, `bountyPerLevel`, `bountyMountedPercent`, `packGapSeconds`, `exitRadiusMetres`, `waveLimitBaseSeconds`, `waveLimitPerCreepSeconds`, `minPostGapMetres`, `roadEndClearMetres` | The run's numbers |
| `ranks` | `soldier`, `captain`, `lieutenant`, `general`: `lives`, `bounty`, `health` | Life cost of a leak, bounty of a leader, leader health factor |
| `opening` | culture ids | The pinned first waves: Mordor, then Erebor, also when the key is missing or bad. An id is dropped with a warning when it names no culture, repeats the wave before it, or has no infantry at or below its wave's band |
| `count` | `start`, `perWave`, `max`, `packSize` | Creeps per wave: `min(max, round((start + perWave(w-1)) x countFactor))`, at least 1, in packs |
| `troops` | `perLevel`, `nearestLowerLevels`, `highLevelPercent` | Troops drawn per level, how far a class may fall below its band, the share at the band's top level |
| `bands` | `fromWave`, `low`, `high` | The level band per wave range |
| `growth` | `healthPerWave`, `damagePerWave`, `maxFactor`, `armourPercentPerWave`, `armourPercentMax`, `swingPercentPerWave`, `swingPercentMax`, `gapHealthPerLevel`, `gapDamagePerLevel` | How creeps grow each wave. `maxFactor` caps the per-wave health and damage growth only. When a culture has no infantry row in the band, its infantry falls to the tree's top row, and those packs alone get the gap bonus for each level from that row to the band's high level (`CreepPack.HealthBonus`, `DamageBonus`, added to the wave's factors; no cap) |
| `leaders` | `everyWaves` | A captain, lieutenant, then general every N waves |
| `powers` | `schedule` (`fromWave`, `count`), `pool` | How many creep powers a wave has, and which |

### Config File: `Main/_Module/ModuleData/hero_defence/cultures.json`

`signatures` gives each wave shape its arrival order, `shuffled` flag, `countFactor` and first power. `cultures` gives each culture
its signature, class `mix` (infantry, ranged, cavalry, horse archer weights) and three `leaders` (captain, lieutenant, general).
Leaders are curated by hand, because a buyable hero must never lead a wave, and a famous villain leads only as a general
(`ShippedHeroDefenceDataTests` checks both against named lists). A leader replaces one soldier; in a wave with no infantry pack it
takes one creep from the largest pack and walks in a pack of its own, so the count does not change. Culture ids follow the
cheatsheet: `empire` is Dunland, `vlandia` Rohan, `khuzait` the Easterlings, `aserai` Harad, `sturgia` Dale.

### Config File: `Main/_Module/ModuleData/hero_defence/rosters.json` (generated)

Per culture, every troop a wave can draw: id, level and class (`default_group`). `tools/generate_hero_defence_rosters.py` writes it
from `Main/_Module/ModuleData/troops/troops_*.xml`; run it with `--check` for the current troop and culture counts. Its filters: not
`troops_feral_orcs.xml`, the file's main culture (Shaghana merges into Harad), occupation Soldier, not settlement militia, and an
upgrade edge in or out. Never edit the file by hand: `ShippedHeroDefenceDataTests` and the `hero-defence-rosters` data gate fail on
drift. Regenerate with `python tools/generate_hero_defence_rosters.py --out Main/_Module/ModuleData/hero_defence/rosters.json`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/HeroDefence/Domain/HeroDefenceRun*.cs` | Run rules, shop, wave flow |
| `Main/Features/HeroDefence/Domain/CreepWavePlan*.cs` | Wave generator |
| `Main/Features/HeroDefence/Domain/PlacementRules.cs` | Placement rules |
| `Main/Features/HeroDefence/Domain/DefenceTypes.cs`, `DefenceRules.cs`, `WaveCatalog.cs` | Shared records and defaults |
| `Main/Features/HeroDefence/Config/*.cs` | JSON readers and the catalog |
| `Main/_Module/ModuleData/hero_defence/*.json` | Data |
| `tools/generate_hero_defence_rosters.py` | Writes `rosters.json` |

## Dependencies

None yet beyond the reused domain types above. The engine side (director, adapters, client) is not built.

## Tests

- `TAOM.Tests/Features/HeroDefence/Domain/`: the run rules (`HeroDefenceRunShopTests`, `HeroDefenceRunWaveTests`), the wave
  generator (`CreepWavePlanTests`), placement (`PlacementRulesTests`) and the purity gate.
- `TAOM.Tests/Features/HeroDefence/HeroDefenceConfigCatalogTests.cs`: every reader, bad values and missing files.
- `TAOM.Tests/Features/HeroDefence/ShippedHeroDefenceDataTests.cs`: the shipped JSON loads with no warning, and `rosters.json`
  matches the troop trees.
- `tools/tests/test_generate_hero_defence_rosters.py`: the generator's filters and output.
- Run them with `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~HeroDefence"` and
  `python -m unittest tools.tests.test_generate_hero_defence_rosters`.

## How to verify in game

Not reachable yet: nothing registers the mode, so no menu entry, command or log line exists. The first in-game checks arrive with
the scaffold slice (`boot`, `map`, `islands`, `route` through `tools/run_hero_defence.py`), each with a `[Check] id=taom.hero-defence.*`
line and a manifest in `tools/ingame_checks/1106.toml`. Until then the evidence is the unit and shipped-data tests above.

## Changelog

- 2026-11-09: the pure core: run rules, wave generator, config readers, shipped data and the roster generator (plan Slices 1 and 2).
  Review fixes ([RCA](../reviews/rca-hero-defence-s12-2026-11-09.md)): the gap bonus moved onto clamped infantry packs, the pause
  rest carries into the wave, the opening checks entry, waves have at least one creep, leaders keep the count, gold saturates, the
  signature never repeats while another has entered, and the readers reject repeated keys.

## GitHub Issue

- **Issue:** #1106: Hero Defence
- **Status:** Open
