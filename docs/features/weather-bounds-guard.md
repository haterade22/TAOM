# Weather Bounds Guard

## Overview

Three Harmony Prefix patches on `DefaultMapWeatherModel` that clamp out-of-bounds map coordinates to valid terrain bounds before vanilla weather lookups run. Prevents crashes / garbage weather data when entities, parties, or scripts query weather at positions just outside the campaign map's terrain extents (which happens on TAOM's larger custom map at the seams and during certain edge-of-map events).

## Why This Exists

- **Vanilla behavior:** `DefaultMapWeatherModel`'s three position-input methods (`GetWeatherEventInPosition`, `GetSnowAndRainDataForPosition`, `UpdateWeatherForPosition`) accept a `Vec2`/`CampaignVec2` without bounds-checking. Internally they index into terrain data with that position. If `x >= terrain.Width` or `y >= terrain.Height` (or negative), the read goes out-of-bounds; on .NET Framework 4.7.2 / Mono this can manifest as either an `AccessViolationException`, a silent NaN, or — most insidiously — garbage weather data.
- **TAOM requirement:** TAOM ships with a much larger custom campaign map (in progress as `TAOM_Map`) and various edge-of-map gameplay (sea travel, faction outposts at the corners). Vanilla code paths sometimes pass slightly out-of-bounds positions during routine ticks (e.g., when computing weather for a party that just traveled past a coast).
- **Without this feature:** Sporadic CTDs and "weird weather" reports — sunny under a stationary thundercloud, snow in mid-summer Mordor — that are hard to repro because the bug only fires when a party's coordinate transiently exceeds terrain bounds by a fraction.

## Architecture

### Design Challenge

The three vanilla methods take their position as `ref Vec2` (or `ref CampaignVec2`), which lets a Prefix mutate the parameter in place rather than having to return false and re-compute the entire weather lookup. This is the simplest patch shape for "bounds-check before vanilla runs" — clamp the ref, fall through to vanilla, done.

The clamping itself isn't deep math but has a subtle requirement: the upper bound must be *strictly less* than `terrainSize`, because vanilla code internally does an array index based on the position. Using `Min(pos, terrainSize)` would still let `pos == terrainSize` reach the index and overflow by 1. The clamper subtracts a small `Epsilon` (`0.01f`) from each axis upper bound to keep the index strictly inside the array.

The terrain size itself comes from `Campaign.Current.MapSceneWrapper.GetTerrainSize()` — which can be null during edge cases like main menu, save loading, or shader pre-compilation runs. Each patch null-checks the wrapper before clamping; on null, the patch is a no-op and lets vanilla run unmodified (vanilla on the menu screen doesn't crash because it's not actually querying terrain).

### Solution Approach

Standard Harmony Prefix-with-ref-mutation pattern. All three patches share `[HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]`. The clamping logic is extracted into a single static helper [WeatherPositionClamper](../../Main/Features/WeatherBoundsGuard/WeatherPositionClamper.cs) so the three patches are nearly identical and the math is unit-testable independently of Harmony.

| Patch | Target | Type | Behavior |
|---|---|---|---|
| `DefaultMapWeatherModel_GetWeatherEventInPosition_Patch` | `GetWeatherEventInPosition(ref Vec2 pos)` | Prefix void | Clamps `pos` to `[0, W-Epsilon] × [0, H-Epsilon]` |
| `DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch` | `GetSnowAndRainDataForPosition(ref Vec2 position)` | Prefix void | Same clamp on `position` |
| `DefaultMapWeatherModel_UpdateWeatherForPosition_Patch` | `UpdateWeatherForPosition(ref CampaignVec2 position)` | Prefix void | Clamps the inner `Vec2`, rebuilds `CampaignVec2(new Vec2(x, y), position.IsOnLand)` |

`WeatherPositionClamper.ClampPosition`:

```csharp
public static (float x, float y) ClampPosition(float posX, float posY, float terrainW, float terrainH)
{
    if (terrainW <= 0f || terrainH <= 0f) return (0f, 0f);
    float x = Math.Max(0f, Math.Min(posX, terrainW - Epsilon));
    float y = Math.Max(0f, Math.Min(posY, terrainH - Epsilon));
    return (x, y);
}
```

`Epsilon = 0.01f`. The `terrainW <= 0` / `terrainH <= 0` guard returns origin to be defensive against pathological terrain sizes (zero or negative) that could otherwise underflow the upper bound to negative.

### Component Diagram

```
SubModule.OnGameInitializationFinished       (Main/SubModule.cs:379)
        |
_harmony.PatchCategory("Patch10_WeatherBoundsGuard")
        |
   +----+----+--------------+
   |         |              |
   v         v              v
GetWeatherEventInPosition   GetSnowAndRainDataForPosition   UpdateWeatherForPosition
        |                        |                                |
        +-----+-----+-------+----+----+-------+--------+----+-----+
              |                                                   |
              v                                                   v
     Campaign.Current?.MapSceneWrapper?.GetTerrainSize()
              |
              v
     WeatherPositionClamper.ClampPosition(pos.x, pos.y, w, h)
              |
              v
     mutates the ref param → vanilla runs
```

## Configuration

None. The feature is purely defensive and has no tunable knobs. `Epsilon` is a private const inside the clamper (`0.01f`); change it only if Bannerlord ever changes its terrain indexing precision.

## Key Files

| File | Purpose |
|---|---|
| [Main/Features/WeatherBoundsGuard/WeatherPositionClamper.cs](../../Main/Features/WeatherBoundsGuard/WeatherPositionClamper.cs) | Static clamping helper — pure function, fully unit-testable |
| [Main/Features/WeatherBoundsGuard/Hooks/DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs](../../Main/Features/WeatherBoundsGuard/Hooks/DefaultMapWeatherModel_GetWeatherEventInPosition_Patch.cs) | Prefix on `GetWeatherEventInPosition` |
| [Main/Features/WeatherBoundsGuard/Hooks/DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs](../../Main/Features/WeatherBoundsGuard/Hooks/DefaultMapWeatherModel_GetSnowAndRainDataForPosition_Patch.cs) | Prefix on `GetSnowAndRainDataForPosition` |
| [Main/Features/WeatherBoundsGuard/Hooks/DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs](../../Main/Features/WeatherBoundsGuard/Hooks/DefaultMapWeatherModel_UpdateWeatherForPosition_Patch.cs) | Prefix on `UpdateWeatherForPosition` (CampaignVec2 variant) |
| [Main/SubModule.cs:379](../../Main/SubModule.cs) | `_harmony.PatchCategory("Patch10_WeatherBoundsGuard")` |

No service, no IoC, no adapters. Stateless.

## Dependencies

- `TaleWorlds.CampaignSystem.GameComponents.DefaultMapWeatherModel` (Harmony target)
- `TaleWorlds.CampaignSystem.Campaign.Current.MapSceneWrapper.GetTerrainSize()` (queried for the bounds at each call)
- `TaleWorlds.Library.Vec2` / `TaleWorlds.CampaignSystem.CampaignVec2` (parameter types)

## Tests

- [TAOM.Tests/Features/WeatherBoundsGuard/WeatherPositionClamperTests.cs](../../TAOM.Tests/Features/WeatherBoundsGuard/WeatherPositionClamperTests.cs) — **7 tests**:
  - `ClampPosition_InsideBounds_ReturnsUnchanged`
  - `ClampPosition_AtExactBoundary_ClampsBelow` (`pos == terrain` → returns `terrain - Epsilon`)
  - `ClampPosition_BeyondBoundary_ClampsToMax`
  - `ClampPosition_NegativePosition_ClampsToZero`
  - `ClampPosition_ZeroTerrainSize_ReturnsZero`
  - `ClampPosition_NegativeTerrainSize_ReturnsZero`
  - `ClampPosition_AtOrigin_ReturnsZero`

The Harmony Prefixes themselves aren't directly tested — the math lives in the static helper, and the patches are trivial wrappers (read terrain size, call clamper, rebuild ref). The patches are verified by playing.

## How to Diagnose "weather is wrong at the edge of the map"

If reports come in that weather queries near terrain bounds still produce garbage:

1. Add a `Debug.Print` to `WeatherPositionClamper.ClampPosition` logging input vs output. If clamping isn't happening, the `terrainW <= 0` guard is firing — check what `MapSceneWrapper.GetTerrainSize()` returns at that moment (likely null wrapper or zero terrain).
2. Confirm `Patch10_WeatherBoundsGuard` is applied — open `rgl_log.txt` and search for Harmony patch confirmation lines on `GetWeatherEventInPosition`, `GetSnowAndRainDataForPosition`, `UpdateWeatherForPosition`.
3. Check whether a sibling patch is also running on these methods. If another mod's transpiler reorders `DefaultMapWeatherModel.GetWeatherEventInPosition` such that the bounds read happens before our Prefix's mutation takes effect, our clamp won't help. The Prefix runs before the original — if another Prefix runs even earlier (Harmony priority) and mutates `pos` to something weirder, that priority wins.

## How to Add Bounds Guarding to Another Vanilla Method

If `DefaultMapWeatherModel` (or another model) gains a new position-input method, mirror the existing pattern:

1. Create `Main/Features/WeatherBoundsGuard/Hooks/<TargetClass>_<Method>_Patch.cs`.
2. `[HarmonyPatch(typeof(...), "<Method>")]` + `[HarmonyPatchCategory("Patch10_WeatherBoundsGuard")]`.
3. `[HarmonyPrefix] public static void Prefix(ref Vec2 pos)` (or `ref CampaignVec2`).
4. Body: pull `Campaign.Current?.MapSceneWrapper`, null-check, call `WeatherPositionClamper.ClampPosition`, mutate the ref.
5. Add a unit test for any new clamping case in `WeatherPositionClamperTests.cs`.

## GitHub Issue

- **Issue:** None — feature predates the mandatory issue-per-feature policy.
- **Status:** Shipping. Stable.
