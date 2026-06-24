# Settlement Nameplate Fade

## Overview

Fades settlement nameplates (the banner-style labels floating above towns/castles/villages/hideouts on the campaign map) with camera distance. Settlements within a configurable "near" radius render at full vanilla alpha; settlements past the "far" radius are fully hidden; in between, alpha interpolates linearly. MCM toggle + two sliders expose the behavior.

## Why This Exists

- **Vanilla behavior:** `SettlementNameplateWidget.DetermineTargetAlphaValue()` returns alpha based solely on tracked/in-window/relation state — `DistanceToCamera` is exposed but only used for depth-sort, never to drive visibility. Result: **all** nameplates on screen render at full alpha regardless of distance.
- **TAOM requirement:** TAOM ships 863 settlements on the LOTR map (vs ~96 vanilla). At default zoom the screen fills with overlapping banners covering far-away settlements the player doesn't care about, making nearby fiefs hard to read.
- **Without this feature:** Map screen is visually noisy; players struggle to identify their immediate surroundings.

## Architecture

### Design Challenge

`DetermineTargetAlphaValue` is a `private` method on a sealed-by-default engine widget called every frame from `OnParallelUpdate` (multi-threaded). Hot-path constraints: ~3000 calls/second on a populated map. The patch must not allocate, must be thread-safe, and must early-out cleanly for the common cases.

### Solution Approach

Harmony Postfix on `SettlementNameplateWidget.DetermineTargetAlphaValue()`. The postfix multiplies the vanilla `__result` (target alpha) by a fade multiplier in [0, 1] computed from `__instance.DistanceToCamera`. Vanilla's lerp toward the new target then smooths visual transitions automatically — no `OnTick` choreography needed.

Linear fade: `multiplier = clamp01(1 - (distance - near) / (far - near))`.

Disabled / NaN-input / collapsed-range paths short-circuit to `multiplier = 1.0`, which leaves vanilla behavior untouched (`__result *= 1f` is a no-op).

### Component Diagram

```
TaomSettings.Instance (MCM v5)
        |
NameplateFadeSettingsProvider  (cached ref, hot-path safe)
        |
INameplateFadeService.ComputeAlphaMultiplier(distance) -> [0, 1]
        |
SettlementNameplateWidget_DetermineTargetAlphaValue_Patch (Postfix)
        |
        __result *= multiplier
```

## Configuration

### MCM Settings — `Map UI / Settlement Nameplates`

| Setting | Type | Range | Default | Description |
|---------|------|-------|---------|-------------|
| `EnableNameplateFade` | bool | — | true | Master toggle. When off, behavior is vanilla. |
| `NameplateFadeNearDistance` | float | 5–500 | 80 | Camera distance at which fade begins. Closer = fully opaque. |
| `NameplateFadeFarDistance` | float | 10–1000 | 200 | Camera distance at which fade completes. Farther = fully hidden. |

Defaults are tuned for a typical TAOM map view — adjust to taste. The service guards against `Far <= Near` (collapsed range) and NaN/Infinity inputs by reverting to vanilla behavior.

## Key Files

| File | Purpose |
|------|---------|
| [Main/Features/SettlementNameplateFade/INameplateFadeService.cs](../../Main/Features/SettlementNameplateFade/INameplateFadeService.cs) | Service contract |
| [Main/Features/SettlementNameplateFade/NameplateFadeService.cs](../../Main/Features/SettlementNameplateFade/NameplateFadeService.cs) | Linear-fade math + NaN/range guards |
| [Main/Features/SettlementNameplateFade/INameplateFadeSettingsProvider.cs](../../Main/Features/SettlementNameplateFade/INameplateFadeSettingsProvider.cs) | Settings contract |
| [Main/Features/SettlementNameplateFade/NameplateFadeSettingsProvider.cs](../../Main/Features/SettlementNameplateFade/NameplateFadeSettingsProvider.cs) | MCM bridge with cached `TaomSettings` reference |
| [Main/Features/SettlementNameplateFade/NameplateFadeIoC.cs](../../Main/Features/SettlementNameplateFade/NameplateFadeIoC.cs) | DryIoc Singleton registration |
| [Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs](../../Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs) | Harmony Postfix, thin entry point |
| [Main/Features/TaomSettings.cs](../../Main/Features/TaomSettings.cs) | 3 new MCM properties (`EnableNameplateFade`, `NameplateFadeNearDistance`, `NameplateFadeFarDistance`) |
| [Main/IoC.cs](../../Main/IoC.cs) | Feature registration |
| [Main/SubModule.cs](../../Main/SubModule.cs) | `Initialize` cached service + `_harmony.PatchCategory("Patch38_SettlementNameplateFade")` in `OnGameInitializationFinished` |

## Dependencies

- `INameplateFadeService` (this feature) — fade-multiplier calculation
- `INameplateFadeSettingsProvider` (this feature) — MCM bridge
- `TaomSettings` (Features) — MCM properties
- TaleWorlds: `SettlementNameplateWidget` (read `DistanceToCamera`, mutate target alpha return value)

## Tests

- [TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeServiceTests.cs](../../TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeServiceTests.cs) — 18 tests covering disabled toggle, boundary at near/far, midpoint + quarter-band interpolation, NaN/Infinity distance, negative distance, NaN/Infinity in near & far settings, `Far <= Near` collapsed range, custom band tuning.
- [TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeSettingsProviderTests.cs](../../TAOM.Tests/Features/SettlementNameplateFade/NameplateFadeSettingsProviderTests.cs) — 3 tests verifying provider defaults match `TaomSettings` MCM slider defaults (Near=80, Far=200, Enabled=true). Catches drift between MCM declared defaults and provider-fallback defaults.

The Harmony patch itself is not unit-tested (requires live game). Verified in-game by moving the camera in the campaign map and observing nameplate alpha transitions.

## How to Adjust the Fade Band at Runtime

1. Open `Options` → `Mod Configuration Menu` → `TAOM - Tales From the Age of Men` → `Map UI/Settlement Nameplates`.
2. Drag `Fade Start Distance` to set when fade begins; drag `Fade End Distance` to set when nameplates fully disappear.
3. Changes are picked up live — no save reload needed.

## Performance

This patch runs at ~3000 calls/second on a populated map (60 FPS × ~50 visible settlements). Optimizations applied:

- **No per-call IoC resolve.** `INameplateFadeService` is captured once via `SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.Initialize(...)` at `OnGameInitializationFinished` time. Subsequent calls read a static field directly.
- **Cached `TaomSettings.Instance` reference.** `NameplateFadeSettingsProvider` captures the singleton once in its constructor. Each property read is then a single reference dereference instead of a static accessor + null-check chain.
- **Early-out at first opportunity.** Postfix returns immediately if vanilla already returned 0 (off-screen, untracked) — avoids invoking the service for the majority of off-screen settlements.
- **No allocations.** Pure arithmetic, no LINQ, no closures, no string formatting.
- **Branch order optimized for common case.** Disabled → invalid input → close-up → far-away → between (linear).

## Changelog

- 2026-05-25 — feat(map) #223: distance-based settlement nameplate fade — Harmony Postfix on `SettlementNameplateWidget.DetermineTargetAlphaValue()` multiplies vanilla target alpha by a [0,1] fade factor from `DistanceToCamera`; 3 MCM settings (`EnableNameplateFade`, `NameplateFadeNearDistance` 5-500/80, `NameplateFadeFarDistance` 10-1000/200); disabled/NaN/`Far<=Near` short-circuit to vanilla; deep-review fixed 1 HIGH + 1 MED + 1 LOW (cached `TaomSettings.Instance`, `Initialize(svc)` static-field capture, Infinity-near regression test).

## GitHub Issue

- **Issue:** [#223 — feat(map): distance-based settlement nameplate fade](https://github.com/haterade22/TAOM/issues/223)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
