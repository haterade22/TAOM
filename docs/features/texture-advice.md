# Texture advice

## Overview

One popup at the first main menu, on machines with about 16 GB of memory or less whose Texture Quality is at its highest
setting. It suggests one step lower, then Foliage Quality. It never shows again on that install. Every launch writes one
`[TextureAdvice]` line with the memory size and the setting, so a player log carries both. TAOM's own work, 1.4.x line
only (MEMORY-04 of the 2026-11-04 port audit).

## Why This Exists

- **Vanilla behavior:** no advice of any kind.
- **TAOM requirement:** Texture Quality is the largest memory lever a player has. Highest to lowest freed 2.3 GB on the
  map in the maintainer's 2026-09-12 run, live, before the 2026-09-13 map downsizing; foliage freed 0.77 GB. One step down
  was never measured, so the popup carries no number.
- **Without this feature:** nothing tells a 16 GB player that the setting exists as a memory lever.

## Architecture

### Solution Approach

`TextureAdviceModule` runs at `ApplyPhase.MainMenu`, once per process, and registers no patch. The pure
`TextureAdviceRule` advises when installed memory is above 0 and at most 17,408 MB (17 GiB: every machine sold as 16 GB or
less) and Texture Quality is at the top level the build offers, unless the advice was shown before. The adapter
`TextureAdviceAdapter` makes the four reads (`TextureQuality`, `TopTextureQuality`, `TotalPhysicalMemoryMb`,
`WasAdviceShown`) and shows the popup. The marker is `Logs/taom-texture-advice.shown`, relative to the game's working
directory. The order is show, then remember: a marker that cannot be written costs one repeat at the next launch.

### Component Diagram

```
TextureAdviceModule (MainMenu)
        |
ITextureAdviceService (TextureAdviceService) --- TextureAdviceRule, TextureAdviceLines
        |
ITextureAdviceAdapter (TextureAdviceAdapter): NativeOptions, MemorySampleReader, the marker file, the popup
```

## Configuration

None. The popup is dismissed by its one button. Delete the marker to see the popup again.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TextureAdvice/TextureAdviceModule.cs` | Registrations, the MainMenu step |
| `Main/Features/TextureAdvice/TextureAdviceRule.cs` | When the advice shows (pure) |
| `Main/Features/TextureAdvice/TextureAdviceService.cs` | The once-per-launch decision and the show-then-remember order |
| `Main/Features/TextureAdvice/TextureAdviceLines.cs` | The log lines |
| `Main/Adapters/ITextureAdviceAdapter.cs`, `TextureAdviceAdapter.cs` | The option reads, the memory read, the marker, the popup |

## Logging

All lines carry `[TextureAdvice]`:

- `totalPhysMB=<n> textureQuality=<q>/<top> shownBefore=<true|false>: <advice shown (lower Texture Quality one step)|no advice>`, every launch.
- `no advice: <Type>: <message>`, a warning when a read failed.
- `the advice was shown but could not be recorded (<Type>: <message>); it shows again at the next launch`, a warning.

## Tests

`TextureAdviceRuleTests` (9), `TextureAdviceServiceTests` (6), `TextureAdviceAdapterTests` (2),
`TextureAdviceModuleTests` (5) and `TextureAdviceBindingTests` (3).

## How to check it in game

1. On a machine with 16 GB or less and Texture Quality High, first launch: one popup at the main menu;
   `[TextureAdvice] totalPhysMB=... textureQuality=2/2 shownBefore=false: advice shown ...`; the file
   `taom-texture-advice.shown` appears in the `Logs` folder beside the TAOM log.
2. Next launch: no popup; the line ends `shownBefore=true: no advice`.
3. On a machine with more than 17 GB: no popup; the line ends `no advice`. Lowering Texture Quality and deleting the
   marker on a 16 GB machine: no popup either.

## Changelog

- 2026-11-04: added on the 1.4.x line by plan 120.

## GitHub Issue

- **Issue:** to be created by the orchestrator (plan 120 Part B).
- **Status:** built and unit-tested; not yet checked in game.
