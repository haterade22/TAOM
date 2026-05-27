# Time Acceleration

## Overview

Configurable campaign map time acceleration with three speed tiers (Space, E, Ctrl+Space) and a visible Extra Fast-Forward button on the MapBar. Native replacement for the BetterTime mod (Nexus #2849).

## Why This Exists

- **Vanilla behavior:** Bannerlord's fast-forward (Space/Ctrl+Space) uses fixed speed multipliers with no configurability and only two tiers
- **TAOM requirement:** Players need faster map traversal across the larger Middle-earth map; configurable multipliers let players tune speed to their hardware
- **Without this feature:** Players must install BetterTime as a separate dependency, which may conflict with other TAOM systems and lacks MCM integration

## Architecture

### Design Challenge

Time acceleration must run every frame via `OnApplicationTick` (not campaign tick). The input system (`MapScreen.Instance.Input`) and campaign state (`Campaign.Current`) are sealed TaleWorlds types that must be wrapped. The Extra Fast-Forward button requires UIExtenderEx prefab patches and a ViewModel mixin to inject into the vanilla MapBar UI.

### Solution Approach

- **MBSubModuleBase.OnApplicationTick** — per-frame input polling (not a Harmony patch or CampaignBehavior)
- **UIExtenderEx prefab patches** — shift existing time buttons and insert a new Extra Fast-Forward button
- **UIExtenderEx ViewModel mixin** — hooks `MapTimeControlVM.RefreshValues()` to data-bind button state
- **MCM settings** — three integer sliders for multiplier configuration

### Component Diagram

```
TaomSettings (MCM)
        |
  SettingsProvider (reads MCM values)
        |
  TimeAccelerationService (OnTick logic)
       / \
      /   \
IMapInputAdapter   ITimeControlAdapter
(MapScreen.Input)  (Campaign.Current speed/mode)

UIExtenderEx:
  TimeAccelerationMixin → MapTimeControlVM.RefreshValues()
  TimeAccelerationPrefab → MapBar XML (5 patch classes)
```

## Configuration

### MCM Settings (TaomSettings.cs)

| Setting | Range | Default | Description |
|---------|-------|---------|-------------|
| Fast Forward Multiplier | 1–128 | 4 | Speed when pressing Space |
| Extra Fast Forward Multiplier | 1–128 | 8 | Speed when pressing E |
| Turbo Multiplier (Ctrl+Space) | 1–128 | 16 | Speed while holding Ctrl+Space |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TimeAcceleration/ITimeAccelerationService.cs` | Service interface |
| `Main/Features/TimeAcceleration/TimeAccelerationService.cs` | Core tick logic: input detection, multiplier application, Ctrl+Space save/restore |
| `Main/Features/TimeAcceleration/IMapInputAdapter.cs` | Input abstraction (no TaleWorlds types) |
| `Main/Features/TimeAcceleration/MapInputAdapter.cs` | Wraps `MapScreen.Instance.Input` |
| `Main/Features/TimeAcceleration/ITimeControlAdapter.cs` | Time control abstraction (no TaleWorlds types) |
| `Main/Features/TimeAcceleration/TimeControlAdapter.cs` | Wraps `Campaign.Current` speed/mode/lock |
| `Main/Features/TimeAcceleration/ITimeAccelerationSettingsProvider.cs` | Settings interface |
| `Main/Features/TimeAcceleration/TimeAccelerationSettingsProvider.cs` | Reads `TaomSettings.Instance` |
| `Main/Features/TimeAcceleration/TimeAccelerationIoC.cs` | DryIoc registration (4 singletons) |
| `Main/Features/TimeAcceleration/UI/TimeAccelerationMixin.cs` | ViewModel mixin: `IsExtraFastForwardActive` + tooltip |
| `Main/Features/TimeAcceleration/UI/TimeAccelerationPrefab.cs` | 5 prefab patches: widen panel, shift buttons, insert EFF button |

## Dependencies

- `Bannerlord.UIExtenderEx` — Direct DLL reference from installed module; `SubModule.xml` declares `LoadBeforeThis`
- `TaomSettings` (MCM) — Provides the three multiplier values
- `MapScreen` (SandBox.View.dll) — Per-frame input polling
- `Campaign.Current` — Speed multiplier and time control mode

## Tests

- `TAOM.Tests/Features/TimeAcceleration/TimeAccelerationServiceTests.cs` — 14 tests covering:
  - Guard conditions (campaign inactive, map inactive, menu open)
  - Menu open + locked bypass
  - Space: sets multiplier, no SetTimeSpeed call
  - E: sets multiplier + forces fast-forward
  - Ctrl+Space: saves state, applies turbo, restores on release
  - Ctrl+Space priority over Space alone
  - Configurable multiplier values

## How to Add a New Key Binding

1. Add a new property to `IMapInputAdapter` (e.g., `bool IsQKeyPressed { get; }`)
2. Implement in `MapInputAdapter` using `MapScreen.Instance?.Input?.IsKeyPressed(InputKey.Q)`
3. Add the logic branch in `TimeAccelerationService.OnTick()`
4. Add a new MCM setting in `TaomSettings.cs` if configurable
5. Update `ITimeAccelerationSettingsProvider` and its implementation
6. Write tests for the new key behavior

## Performance

`OnTick()` runs every frame (~60+ fps). The method is allocation-free — no LINQ, no collections, no string operations. Each adapter property access reads a static singleton (`MapScreen.Instance`, `Campaign.Current`). Settings are read from MCM via simple property getters with null-coalescing fallbacks.

## GitHub Issue

- **Issue:** #181 — Time acceleration feature
- **Status:** Open

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
