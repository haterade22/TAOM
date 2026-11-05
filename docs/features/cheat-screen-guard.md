# Cheat-screen guard

## Overview

With vanilla cheat mode on, opening your own inventory lists every item in the game and the party screen lists every
troop. With TAOM's content that is about 5,166 items, and the list costs gigabytes of memory per open. The guard opens
both screens as they open without cheat mode, unless MCM "Cheat Mode Lists All Items And Troops" (TAOM Battle Load
Diagnostics page, group "Map Performance", default off, no restart) asks for the cheat lists. TAOM's own work, 1.4.x line
only (MEMORY-01 of the 2026-11-04 port audit).

## Why This Exists

- **Vanilla behavior:** `InventoryScreenHelper.OpenInventoryPresentation` (v1.4.8, taom-src `:174-196`) adds every
  `ItemObject` to the left roster, ten of each, when `Game.CheatMode` is on; `PartyScreenHelper.OpenScreenAsNormal`
  (`:185-195`) opens `OpenScreenAsCheat`, every troop. `Game.CheatMode` is `NativeConfig.CheatMode`, the `cheat_mode`
  line of `engine_config.txt`. Loot, trade, stash and warehouse screens never read it.
- **TAOM requirement:** every `taom.*` console command needs cheat mode (`DevConsoleGuard.CheckCheatMode`), including
  the rescue commands, so TAOM steers players into it. On a 16 GB machine one inventory open in a 2026-11-01 player log
  (cheat mode inferred) grew the managed heap from 489 to 2,841 MB, reached 96% memory load and silenced the main thread
  for 31 s.
- **Without this feature:** every I or P key press in cheat mode pays that cost again.

## Architecture

### Design Challenge

The cheat read is inside a private engine method that also builds the screen's logic with private helpers, so a prefix
would have to copy them. A transpiler can change the one read and nothing else.

### Solution Approach

Two transpilers (`Patch_CheatScreenGuard`, applied at `ApplyPhase.GameInit` by `CheatScreenGuardModule`) reuse the
soft-failing call-site swap `TickProfilerTranspiler.Rewrite`: the single `Game.get_CheatMode` read in each opener
becomes `CheatScreenGuardCalls.InventoryCheatMode(Game)` or `PartyCheatMode(Game)`. Each reads the real flag and,
only while it is on, asks `CheatScreenGuardService.ShowCheatList`, which returns the MCM setting. Any other site count
leaves the method vanilla; with no service, or when the service throws, the answer is the real flag. The first hidden
list per launch writes one log line and one chat line.

### Component Diagram

```
CheatScreenGuardModule (GameInit: Patch_CheatScreenGuard, then the install line)
        |
two transpilers -> CheatScreenGuardCalls (bool (Game) helpers, real flag first)
        |
ICheatScreenGuardService (CheatScreenGuardService) --- ICheatScreenGuardSettingsProvider, IInquiryAdapter, IModLogger
```

## Configuration

MCM, TAOM Battle Load Diagnostics page, group "Map Performance": `Cheat Mode Lists All Items And Troops`, default off,
`RequireRestart = false`, read at each screen open through a cached settings reference (off while MCM has no instance).
Player-local in `CoopSettingsRelevance`.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/CheatScreenGuard/CheatScreenGuardModule.cs` | Registrations, the category at GameInit, the install line |
| `Main/Features/CheatScreenGuard/Hooks/*_CheatScreenGuard_Patch.cs` | The two transpilers |
| `Main/Features/CheatScreenGuard/Hooks/CheatScreenGuardCalls.cs`, `CheatScreenGuardSwaps.cs` | The swapped-in helpers and the swap table |
| `Main/Features/CheatScreenGuard/CheatScreenGuardService.cs` | The decision, the once-per-launch line and notice |
| `Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs` | The MCM setting |

## Logging

All lines carry `[CheatScreenGuard]`: one install line at game init (`ON: ...`, or a warning naming each gate that is
off), and one line the first time this launch a cheat list is hidden.

## Tests

`CheatScreenGuardCallsTests` (5), `CheatScreenGuardServiceTests` (8), `CheatScreenGuardSettingsProviderTests` (2),
`CheatScreenGuardLinesTests` (3), `CheatScreenGuardModuleTests` (6) and `CheatScreenGuardBindingTests` (8: the targets
and the one cheat read each in the installed IL, the swap applied to the real IL and through real Harmony, the setting).

## How to check it in game

Set `cheat_mode = 1` in `engine_config.txt`, load a campaign, press I: your own items only, no Discard list of
thousands; the `[MemStation]` enter and exit lines of `GauntletInventoryScreen` differ by well under 100 MB heap; one
`[CheatScreenGuard] cheat mode is on` line and one chat line. Press P: the normal party screen. Turn the setting on and
press I: the vanilla list (and its memory cost) returns.

## Changelog

- 2026-11-04: added on the 1.4.x line by plan 120.

## GitHub Issue

- **Issue:** to be created by the orchestrator (plan 120 Part A).
- **Status:** built and unit-tested; not yet checked in game.
