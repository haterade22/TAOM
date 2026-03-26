# Atmosphere Persistence

## Overview

Scenes with "forceatmo" in their name bypass Bannerlord's campaign weather system, preserving scene-embedded atmosphere data. This ensures scenes like the Dead Marshes maintain their intended visual atmosphere (permanent fog, specific lighting) instead of being overwritten by dynamic campaign weather.

## Why This Exists

- **Vanilla behavior:** Bannerlord's campaign system dynamically calculates weather/atmosphere based on season, time of day, and map position. When entering a mission scene, this calculated atmosphere overrides any atmosphere baked into the scene.
- **TAOM requirement:** Certain Middle-earth locations (Dead Marshes, Moria, Fangorn) require permanent atmosphere effects that shouldn't change with seasons or time.
- **Without this feature:** Forced-atmosphere scenes lose their baked lighting, fog, and weather — the Dead Marshes might appear sunny at noon instead of perpetually murky.

## Architecture

### Design Challenge

`MissionInitializerRecord` is a struct stored as a private property on `Mission`. The campaign atmosphere is applied before `Mission.Initialize()` calls the native engine. We need to intercept and clear the campaign atmosphere for specific scenes before it reaches the native layer.

### Solution Approach

Harmony prefix on `Mission.Initialize()` — type-safe, fires before the native `MBAPI.IMBMission.InitializeMission` call. Uses reflection to mutate the private `InitializerRecord` property (struct requires get/modify/set pattern via `PropertyInfo`).

Follows the `WeatherBoundsGuard` pattern: static service + thin patch. No IoC, no adapters — the only data crossing the boundary is a `string` scene name.

### Component Diagram

```
Scene name contains "forceatmo"?
        |
  AtmosphereOverrideService (pure string detection)
        |
  Mission_Initialize_Patch (Harmony prefix)
        |
  Mutates MissionInitializerRecord:
    PlayingInCampaignMode = false
    AtmosphereOnCampaign = invalid
        |
  Mission.Initialize() proceeds with scene-baked atmosphere
```

## Configuration

No configuration needed. Scene creators add "forceatmo" anywhere in the scene name (case-insensitive).

### Scene Naming Examples

| Scene Name | Triggers Override? |
|------------|-------------------|
| `lotr_dead_marshes_forceatmo` | Yes |
| `lotr_helms_deep_night_forceatmo` | Yes |
| `forceatmo_moria` | Yes |
| `scene_FORCEATMO_day` | Yes (case-insensitive) |
| `battania_village_a` | No |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/AtmospherePersistence/AtmosphereOverrideService.cs` | Static service: `RequiresAtmosphereOverride(string)` — pure string detection |
| `Main/Features/AtmospherePersistence/Hooks/Mission_Initialize_Patch.cs` | Harmony prefix on `Mission.Initialize()`, category `Patch16_AtmospherePersistence` |
| `Main/SubModule.cs` | Registers `Patch16_AtmospherePersistence` in `OnGameInitializationFinished` |

## Dependencies

- `TaleWorlds.MountAndBlade.Mission` — patch target
- `TaleWorlds.Core.MissionInitializerRecord` — struct being mutated
- `TaleWorlds.Library.AtmosphereInfo` — `GetInvalidAtmosphereInfo()` for safe struct zeroing
- `HarmonyLib` — patching framework

## Tests

- `TAOM.Tests/Features/AtmospherePersistence/AtmosphereOverrideServiceTests.cs` — 7 tests covering null, empty, case-insensitive, marker at start/middle/end, negative case

## How to Add a Forced-Atmosphere Scene

1. Name the scene with "forceatmo" in the name (e.g., `lotr_location_forceatmo`)
2. Bake the desired atmosphere into the scene in the Bannerlord scene editor
3. Reference the scene in `sp_battle_scenes.xml` or settlement XML as usual
4. No code changes needed — the patch detects the naming convention automatically

## Performance

Minimal — one `string.IndexOf` call per mission load. The `PropertyInfo` for reflection is cached as `static readonly`.

## GitHub Issue

- **Issue:** #43 — [feat: atmosphere persistence for forced-atmosphere scenes](https://github.com/haterade22/TAOM/issues/43)
- **Status:** Closed
