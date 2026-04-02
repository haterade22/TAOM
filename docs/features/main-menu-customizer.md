# Main Menu Customizer

## Overview

Hides the "Campaign" button from the Bannerlord main screen and renames "Sandbox" to "Enter The Age Of Men". Applied once at startup before the initial menu screen is shown.

## Why This Exists

- **Vanilla behavior:** Bannerlord's main screen shows Campaign, Sandbox, Multiplayer, Options, Credits, Exit.
- **TAOM requirement:** TAOM is a Sandbox-only total conversion. The Campaign button starts vanilla story mode — a completely different game experience that bypasses all TAOM content. It should not be presented to players. The "Sandbox" label is generic and doesn't reflect the mod's identity.
- **Without this feature:** Players see "Campaign" and "Sandbox" — no indication they're launching a total conversion mod, and clicking Campaign would start the wrong game mode entirely.

## Architecture

### Design Challenge

Bannerlord's initial menu options are managed by `Module.CurrentModule` as a list of `InitialStateOption` objects. They're registered by other modules during load, so TAOM must override them after registration completes. The `InitialStateOption` constructor calls `IsDisabledAndReason()` immediately as a validation side effect, so preserving the original delegate is required.

### Solution Approach

Override `MBSubModuleBase.OnBeforeInitialModuleScreenSetAsRoot()` — this fires after all modules are loaded and their options registered, but before the UI screen is rendered. The service calls `Module.CurrentModule.OverrideInitialStateOption()` to replace options in-place, preserving original actions and disabled-state delegates.

### Component Diagram

```
SubModule.OnBeforeInitialModuleScreenSetAsRoot()
        |
IMainMenuCustomizerService.CustomizeMenu()
        |
IModuleMenuAdapter
   HideOption("campaign_single_player")   → Module.CurrentModule.OverrideInitialStateOption (isHidden: true)
   RenameOption("sandbox_single_player")  → Module.CurrentModule.OverrideInitialStateOption (new TextObject name)
```

## Configuration

No configuration file. The option IDs and rename text are hardcoded constants — these are stable Bannerlord identifiers.

| Value | Purpose |
|-------|---------|
| `"campaign_single_player"` | TW option ID for the Campaign button |
| `"sandbox_single_player"` | TW option ID for the Sandbox button |
| `"Enter The Age Of Men"` | Replacement display name for Sandbox |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MainMenuCustomizer/IMainMenuCustomizerService.cs` | Service interface |
| `Main/Features/MainMenuCustomizer/MainMenuCustomizerService.cs` | Calls HideOption + RenameOption |
| `Main/Features/MainMenuCustomizer/IModuleMenuAdapter.cs` | Adapter interface |
| `Main/Features/MainMenuCustomizer/ModuleMenuAdapter.cs` | Wraps `Module.CurrentModule` TW API |
| `Main/Features/MainMenuCustomizer/MainMenuCustomizerIoC.cs` | DryIoc registration |
| `Main/SubModule.cs` | Entry point — `OnBeforeInitialModuleScreenSetAsRoot` override |

## Dependencies

- `IModLogger` (Core) — logs warnings when option IDs are not found, logs success

## Tests

- `TAOM.Tests/Features/MainMenuCustomizer/MainMenuCustomizerServiceTests.cs` — 3 tests covering: hide called with correct ID, rename called with correct ID and text, log called on success

Note: `ModuleMenuAdapter` is not unit tested — it wraps TaleWorlds static state (`Module.CurrentModule`) which requires a live game runtime.

## How to Change the Renamed Text

Edit `MainMenuCustomizerService.cs`:

```csharp
_moduleMenuAdapter.RenameOption("sandbox_single_player", "Enter The Age Of Men");
```

Replace the second argument with the desired display text.

## How to Hide/Show Additional Options

Add calls in `MainMenuCustomizerService.CustomizeMenu()`:

```csharp
_moduleMenuAdapter.HideOption("multiplayer");    // hide multiplayer
// or
_moduleMenuAdapter.RenameOption("multiplayer", "New Name");
```

Known option IDs: `campaign_single_player`, `sandbox_single_player`, `multiplayer`, `exit_game`.

## GitHub Issue

- **Issue:** #55 — Main menu: hide Campaign, rename Sandbox to "Enter The Age Of Men"
- **Status:** Closed
