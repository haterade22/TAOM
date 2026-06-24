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

## Not the `_forceatmo` battle-load crash cause (2026-06-19 audit)

A tester suspected this patch (`Patch16_AtmospherePersistence`) of causing the native access violation seen at `Mission.Initialize` when loading `_forceatmo` **battle** scenes. The correlation that prompted the check is real but circumstantial: the crash sits at `Mission.Initialize`, and this patch is a Harmony **Prefix on `Mission.Initialize`** that fires for exactly the scenes whose names contain `forceatmo` (`Mission_Initialize_Patch.Prefix`, lines 27-52; the `RequiresAtmosphereOverride` gate, line 32). On that surface it looks like the obvious culprit.

**The patch is exonerated.** What the Prefix writes is benign:

- It sets `rec.PlayingInCampaignMode = false` and `rec.AtmosphereOnCampaign = AtmosphereInfo.GetInvalidAtmosphereInfo()` (`Mission_Initialize_Patch.cs:42-43`).
- `TaleWorlds.Library.AtmosphereInfo` is a value **struct** (`AtmosphereInfo.cs:5`). Its `AtmosphereName` field is `[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]` (`AtmosphereInfo.cs:9-10`) — a **fixed inline character buffer**, not a pointer. `GetInvalidAtmosphereInfo()` returns `new AtmosphereInfo { AtmosphereName = "" }` (`AtmosphereInfo.cs:37-43`), and `IsValid => !string.IsNullOrEmpty(AtmosphereName)` (`AtmosphereInfo.cs:35`). So "invalid" means a **zeroed value struct with an empty (not null) name** — there is no null pointer for native code to dereference.
- The whole `MissionInitializerRecord` is marshalled by-ref into native `MBAPI.IMBMission.InitializeMission`. There is no managed branch on `PlayingInCampaignMode`, and the only managed reads of `AtmosphereOnCampaign` are the record's own `SerializeTo`/`DeserializeFrom`. Whether native reads `AtmosphereOnCampaign` when `PlayingInCampaignMode == false` is therefore invisible to managed decompile.

**The vanilla precedent settles the invisible part.** `BannerlordMissions.OpenCustomBattleLordsHallMission` (`BannerlordMissions.cs:242`) constructs a `MissionInitializerRecord` with `PlayingInCampaignMode = false` (`BannerlordMissions.cs:270`) and **omits** `AtmosphereOnCampaign` entirely — so it stays at the struct default, which is exactly `GetInvalidAtmosphereInfo()` (`MissionInitializerRecord.cs:51`). It then opens a **live combat mission** via `MissionState.OpenNew("CustomBattleLordsHall", ..., new MissionBehavior[17] { ... })` (`BannerlordMissions.cs:267`). Vanilla ships the precise `PlayingInCampaignMode = false` + invalid-atmosphere field pair that this patch produces into the same native `InitializeMission`, in a real fighting mission, and runs clean. Whatever native does with `AtmosphereOnCampaign` under `PlayingInCampaignMode == false`, the engine already does it on a shipping code path.

**Root cause remains unproven.** The affected player's crash log carries no faulting-module or offset, so attribution is open. The live, unfalsified hypothesis is the terrain-shader vista permutation (`Shaders/Sources/terrain_pixel_functions.rsh:818` — `normalize(final_world_space_normal)` after a `lerp(..., vista_blend_weight)` that folds to zero when `weight_accumulation == 0` at vista distance, giving X4008 divide-by-zero escalated to a hard error by X3129). The gate to confirm is native triage on an affected player's Event Log "Application Error" offset (or a crash dump) through `tools/native_crash_triage.py`; the native shader-compile-guard hook plan is blocked on that data. In the meantime the `_forceatmo` battle scenes were disabled at the data layer (the Rohan scenes in `ee2cb04b`, the Mordor scenes in `62470413`) rather than by touching this patch — which the audit confirms is not implicated. See [shader-precompilation.md](shader-precompilation.md) + [battle-load-diagnostics.md](battle-load-diagnostics.md).

## Changelog

- 2026-03-26 — Feature added: scenes with "forceatmo" in their name bypass campaign weather to preserve scene-embedded atmosphere; 1.3 refactor replaced the fragile string-based patch with a type-safe `Mission.Initialize()` prefix (`Patch16_AtmospherePersistence`), static `AtmosphereOverrideService`, 7 scene-name-detection tests.

## GitHub Issue

- **Issue:** #43 — [feat: atmosphere persistence for forced-atmosphere scenes](https://github.com/haterade22/TAOM/issues/43)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
