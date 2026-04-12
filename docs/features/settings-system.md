# Settings System (MCM Replacement)

## Overview

Plain JSON singleton replacing the external MCM (Mod Configuration Menu) dependency. 29 configurable settings across 7 subsystems, loaded from `ModuleData/configs/taom_settings.json`.

## Why This Exists

Bannerlord 1.4.0 broke ButterLib (MCM's dependency) due to a `HotKeyManager.RegisterInitialContexts` signature change. Rather than wait for BUTR to update, we replaced MCM's ~50K LOC framework with a 100-line JSON singleton. This eliminates the ButterLib crash and removes the MCM/ButterLib/MBOptionScreen external module dependencies entirely.

## Architecture

`TaomSettings` is a plain POCO with default values on all properties. On startup, `Initialize(moduleDataPath)` loads the JSON file. If the file is missing, malformed, or empty, defaults are used silently (with a log message on parse failure).

All 33 consumer callsites use the null-safe pattern: `TaomSettings.Instance?.Property ?? default`. This means settings work even if initialization fails.

### Lifecycle
1. `SubModule.OnSubModuleLoad()` calls `TaomSettings.Initialize(pathService.ModuleDataPath)`
2. Consumers read via `TaomSettings.Instance?.Property ?? defaultValue`
3. `SubModule.OnSubModuleUnloaded()` calls `TaomSettings.Reset()`

### No In-Game UI
Players edit `ModuleData/configs/taom_settings.json` directly. Changes take effect on next game launch. An in-game settings screen is a future enhancement.

## Settings Reference

| Property | Type | Default | Subsystem |
|----------|------|---------|-----------|
| ShowAllEncyclopediaCharacters | bool | true | Encyclopedia |
| EnableTroopWeight | bool | true | TroopWeight |
| WarOfTheRingEnabled | bool | true | War of the Ring |
| Phase1TriggerDay | int | 30 | War of the Ring |
| Phase2TriggerDay | int | 45 | War of the Ring |
| TestMode | bool | false | War of the Ring |
| EnableCustomTroopPower | bool | true | Battle Balance |
| OverrideVanillaTierPower | bool | false | Battle Balance |
| Tier7Power | float | 2.91 | Battle Balance |
| Tier8Power | float | 3.26 | Battle Balance |
| Tier9Power | float | 3.61 | Battle Balance |
| Tier10Power | float | 3.96 | Battle Balance |
| HeroMultiplier | float | 1.5 | Battle Balance |
| MountedMultiplier | float | 1.2 | Battle Balance |
| EnableCustomCasualtyRatios | bool | true | Battle Balance |
| PlayerBluntDamageChance | float | 0.30 | Battle Balance |
| AIBluntDamageChance | float | 0.10 | Battle Balance |
| EnableCulturalSurvivalBonuses | bool | true | Battle Balance |
| EnableSiegeDefenseEvents | bool | true | Siege Defense |
| SiegeDefenseResponseDays | int | 3 | Siege Defense |
| EnableArmyStrategicIntelligence | bool | true | Army AI |
| ArmyCommitmentMultiplier | float | 4.0 | Army AI |
| ArmyPriorityBoost | float | 3.0 | Army AI |
| EvilFactionAggressionScale | float | 1.0 | Army AI |
| LongRangePriorityBoostScale | float | 1.0 | Army AI |
| ArmyBorderProximityFloor | float | 0.15 | Army AI |
| FastForwardMultiplier | int | 4 | Time Acceleration |
| ExtraFastForwardMultiplier | int | 8 | Time Acceleration |
| CtrlSpaceMultiplier | int | 16 | Time Acceleration |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TaomSettings.cs` | Singleton + Load/Save |
| `Main/_Module/ModuleData/configs/taom_settings.json` | Default settings template |
| `TAOM.Tests/Features/TaomSettingsTests.cs` | 7 tests (load, save, round-trip, defaults, malformed, partial, empty) |

## How-To: Add a New Setting

1. Add property with default to `TaomSettings.cs`: `public bool MyNewSetting { get; set; } = true;`
2. Add key to `taom_settings.json`: `"MyNewSetting": true`
3. Access in code: `TaomSettings.Instance?.MyNewSetting ?? true`
4. Add test assertion to `AllDefaults_MatchExpectedValues` in `TaomSettingsTests.cs`
