# ButterLib Distance Matrix switch

## Overview

TAOM switches off ButterLib's Distance Matrix subsystem at the first main menu. That removes a map stall of about 170
to 280 ms whenever a settlement passes to another clan (a siege capture, or a fief granted to another clan) and the distance tables
ButterLib builds at every save load and new game. Nothing installed reads those tables.

## Why This Exists

- **ButterLib behavior:** ButterLib ([BUTR stack](../reference/provenance-register.md), MIT), shipped in
  TAOM.Dependencies, enables its Distance Matrix by default. On Bannerlord v1.5.4 it loads
  `Bannerlord.ButterLib.Implementation.1.5.1.dll`, the newest it has. Its `GeopoliticsBehavior` builds settlement, clan
  and kingdom distance tables at every new game and load, and rebuilds the clan table (and the kingdom table when the
  kingdoms differ) synchronously whenever a settlement changes clan. The settlement table counts towns, castles and
  villages: 843 of TAOM_Map's 1,002 settlements, about 355,000 pairs.
- **TAOM requirement:** nothing reads the tables. ButterLib calls the subsystem a "Mod Developer feature"; a scan of
  every module DLL on the desktop install, Workshop included, found no reader outside ButterLib itself, and no TAOM code
  reads them.
- **Without this feature:** yotthani measured the owner-change rebuild on TAOM's campaign (Bannerlord v1.5.3): 173 ms per
  call on average, up to 281 ms in one frame, and the six worst campaign-map frames were all siege ends
  ([MithrilForge](../reference/provenance-register.md), `docs/engine/perf.md`, runs of 2026-10-03 and 2026-10-04). The
  load-time build has not been measured.

## Architecture

### Design Challenge

The subsystem type is internal to a DLL whose version follows the game build. ButterLib enables it in its
`OnSubModuleLoad`, MCM's ButterLib settings page applies its own stored toggle in MCM's first main-menu hook, and
ButterLib honours `Disable()` only until a campaign starts (`if (IsEnabled && !GameInitialized)`).

### Solution Approach

A feature module runs once at `ApplyPhase.MainMenu`. That is the earliest phase that holds: it comes after ButterLib's
`OnSubModuleLoad` and after MCM's main-menu hook (TAOM.Dependencies loads before TAOM), and before any campaign. A switch
at ProcessLoad would be undone when MCM applies a stored "on". `DistanceMatrixSwitch` calls
`DistanceMatrixSubSystem.Disable()` by reflection, the same way the crash report suspends ButterLib's exception handler.
With the subsystem off, ButterLib's `OnGameStart` never adds `GeopoliticsBehavior`, so no table is built and no owner
change rebuilds one. There is no Harmony patch.

ButterLib also exposes subsystems through a public `ISubSystem` API (`GetSubSystem("Distance Matrix")`; MCM's settings page enumerates the same `ISubSystem` instances).
TAOM keeps the reflection instead because it needs no compile-time ButterLib reference, matching the crash report.

Every outcome logs one line tagged `[ButterLibDistance]`: `switched off ...`, `... was already off` (ButterLib's saved
options: a player's own choice, or a value MCM stored when the player saved its ButterLib page under TAOM), a warning
`not switched off: <reason>` (ButterLib or the type missing, the type's shape changed, or `Disable()` did not take), or a warning if anything threw. Nothing throws.

**There is no way to keep the subsystem on.** An opt-in was tried and dropped in review (2026-10-06): MCM's ButterLib page
owns its own key in the same `Options.json`, applies it before TAOM's switch, and rewrites the file without any other key
whenever it saves, so an opt-in would not hold. **The cost of the choice:** another mod that reads the tables gets null
from `Campaign.GetDefault*DistanceMatrix`, and ButterLib's `DistanceMatrix<Clan>.Create()` or `<Kingdom>` throws inside
ButterLib. That is what any player gets by switching the subsystem off in ButterLib's options. The first design, a lazy
rebuild of the tables on their first read, was dropped in the same review: it needed four patches on ButterLib's
private code, missed readers that reach the clan table directly, and left the load-time build in place.

### Component Diagram

```
ButterLibDistanceMatrixModule (OnPhase MainMenu)
        |
DistanceMatrixSwitch (decides, logs)
        |
ButterLibDistanceMatrixAdapter (reflection: Instance, IsEnabled, Disable())
```

## Configuration

No TAOM setting. Two side effects on ButterLib's own settings:

- MCM's ButterLib page (Mod Options, ButterLib) shows the Distance Matrix as off, because it reads the live state. Ticking
  it there (outside a campaign; MCM marks it as needing a restart) turns the subsystem on at most until the next start,
  when TAOM switches it off again.
- If the player saves any change on that page, MCM writes `"Distance Matrix Enabled": false` into
  `Documents\Mount and Blade II Bannerlord\Configs\ModSettings\ButterLib\Options.json`. That value outlives TAOM: a player
  who later removes TAOM keeps the subsystem off until they tick it again.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/ButterLibDistanceMatrix/ButterLibDistanceMatrixModule.cs` | Registers the switch and runs it at the first main menu |
| `Main/Features/ButterLibDistanceMatrix/DistanceMatrixSwitch.cs` | The call and its log line |
| `Main/Adapters/IButterLibDistanceMatrixAdapter.cs` | Adapter interface |
| `Main/Adapters/ButterLibDistanceMatrixAdapter.cs` | Reflection on `DistanceMatrixSubSystem` |
| `Main/Composition/FeatureModules.cs` | The module's one line |

## Dependencies

- `IModLogger` (Core) for the one log line.
- ButterLib's `Bannerlord.ButterLib.Implementation.DistanceMatrix.DistanceMatrixSubSystem`, by name.

## Tests

- `TAOM.Tests/Features/ButterLibDistanceMatrix/DistanceMatrixSwitchTests.cs`: the four log outcomes (switched off,
  already off, not switched off with a reason, an exception).
- `TAOM.Tests/Features/ButterLibDistanceMatrix/ButterLibDistanceMatrixAdapterTests.cs`: `Disable()` over stand-in types
  (enabled, already off, campaign already started, type missing, no instance, shape changed).
- `TAOM.Tests/Features/ButterLibDistanceMatrix/ButterLibDistanceMatrixBindingTests.cs`: the newest implementation DLL
  tracked in `Dependencies/_Module/bin/` still has the members the adapter calls (`BindingVerification`).
- `TAOM.Tests/Features/ButterLibDistanceMatrix/ButterLibDistanceMatrixWiringTests.cs`: listed once, buildable, and the switch fires only at
  the MainMenu phase (`OnPhase_SwitchesOnlyAtMainMenu`).

## How to check it in game

1. Start the game to the main menu and open the newest `taom_debug_*.log`: expect
   `[ButterLibDistance] switched off ButterLib's Distance Matrix for this session`.
2. Open Mod Options, ButterLib: the Distance Matrix subsystem shows as not enabled.
3. Load a campaign and let a siege end: the campaign map should not stall for a quarter second at the capture.

ButterLib's own log is no help here: it logs nothing about the distance tables, on or off.

## Performance

Removes about 170 ms (up to 281 ms) per change of a settlement's clan, measured upstream on v1.5.3, plus the
unmeasured load-time table build and the memory the tables held for the whole session. The switch itself is one
reflection call per process.

## Known limits

- With a second BUTR stack enabled beside TAOM.Dependencies (another module's ButterLib copy), a second
  `DistanceMatrixSubSystem` type could load; TAOM switches off the first one it finds. Not tested.

## Changelog

- 2026-10-06: switch the subsystem off at the first main menu (#740). Replaces a lazy-rebuild patch that never shipped.

## GitHub Issue

- **Issue:** #740, [Campaign map: ButterLib Distance Matrix rebuild stalls every siege end](https://github.com/haterade22/TAOM/issues/740)
- **Status:** Open until the in-game check
