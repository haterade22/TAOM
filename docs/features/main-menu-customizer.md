# Main Menu Customizer

## Overview

Hides the "New Campaign" button (vanilla story mode) and renames "Sandbox" to "Enter The Age Of Men" on the Bannerlord main screen. "Saved Games" and "Continue Campaign" remain visible so players can load existing saves. Also guards the "Pre-compile Shaders" button against duplicate registration when returning from a game session.

## Why This Exists

- **Vanilla behavior:** Bannerlord's main screen shows Saved Games, Continue Campaign, New Campaign, Sandbox, Multiplayer, Options, Credits, Exit.
- **TAOM requirement:** TAOM is a Sandbox-only total conversion. "New Campaign" starts vanilla story mode — a completely different experience that bypasses all TAOM content. "Sandbox" is a generic label that doesn't reflect the mod's identity.
- **Without this feature:** Players see "New Campaign" alongside TAOM's entry point, risk clicking into the wrong game mode, and see a generic "Sandbox" label with no mod identity.

## Final Menu Order

```
Enter The Age Of Men    ← SandBoxNewGame (renamed)
Saved Games             ← CampaignResumeGame (kept)
Continue Campaign       ← ContinueCampaign (kept)
Pre-compile Shaders     ← TaomPrecompileShaders (guarded, single instance)
Custom Battle
Host Co-op
Join Co-op
Options
Credits
Exit Game
```

`New Campaign` (`StoryModeNewGame`) is hidden.

## Architecture

### Design Challenge

Bannerlord's initial menu options are managed by `Module.CurrentModule` as a list of `InitialStateOption` objects. They're registered by other modules in `OnSubModuleLoad` (SandBox.View.dll, StoryMode.View.dll), so TAOM must override them after registration completes. Two additional constraints:

1. `InitialStateOption`'s constructor calls `IsDisabledAndReason()` immediately as a validation side effect — the original delegate must be preserved when overriding
2. `OnBeforeInitialModuleScreenSetAsRoot()` fires on **every** main menu visit (including returning from a game), so any `AddInitialStateOption` call must be guarded against duplication

### Solution Approach

Override `MBSubModuleBase.OnBeforeInitialModuleScreenSetAsRoot()` — fires after all module options are registered, before the UI renders. The `MainMenuCustomizerService` overrides options via the adapter on each visit (idempotent). The Pre-compile Shaders option uses a null-guard to add only once.

### Component Diagram

```
SubModule.OnBeforeInitialModuleScreenSetAsRoot()  [fires every menu visit]
    │
    ├── IMainMenuCustomizerService.CustomizeMenu()  [idempotent overrides]
    │       │
    │       └── IModuleMenuAdapter
    │               HideOption("StoryModeNewGame")   → OverrideInitialStateOption (isHidden: true)
    │               RenameOption("SandBoxNewGame")   → OverrideInitialStateOption (new TextObject name)
    │
    └── if GetInitialStateOptionWithId("TaomPrecompileShaders") == null  [guard]
            → AddInitialStateOption("TaomPrecompileShaders", ...)
```

## Configuration

No configuration file. Option IDs and the rename text are hardcoded — these are stable Bannerlord identifiers confirmed by decompiling SandBox.View.dll and StoryMode.View.dll.

### Known Option IDs (decompiled)

| ID | Display Text | Source DLL | Action |
|----|-------------|------------|--------|
| `"CampaignResumeGame"` | Saved Games | SandBox.View | Kept |
| `"ContinueCampaign"` | Continue Campaign | SandBox.View | Kept |
| `"StoryModeNewGame"` | New Campaign | StoryMode.View | **Hidden** |
| `"SandBoxNewGame"` | SandBox | SandBox.View | **Renamed** |
| `"Multiplayer"` | Multiplayer | Multiplayer module (orderIndex 9997) | Kept |
| `"TaomPrecompileShaders"` | Pre-compile Shaders | TAOM (SubModule.cs) | Guarded add |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/MainMenuCustomizer/IMainMenuCustomizerService.cs` | Service interface |
| `Main/Features/MainMenuCustomizer/MainMenuCustomizerService.cs` | Hides StoryModeNewGame, renames SandBoxNewGame |
| `Main/Features/MainMenuCustomizer/IModuleMenuAdapter.cs` | Adapter interface — `HideOption` / `RenameOption` |
| `Main/Features/MainMenuCustomizer/ModuleMenuAdapter.cs` | Wraps `Module.CurrentModule` TW API |
| `Main/Features/MainMenuCustomizer/MainMenuCustomizerIoC.cs` | DryIoc registration |
| `Main/SubModule.cs` | Entry point — `OnBeforeInitialModuleScreenSetAsRoot` override + shaders guard |

## Dependencies

- `IModLogger` (Core) — logs warnings when option IDs are not found, logs success on apply

## Tests

- `TAOM.Tests/Features/MainMenuCustomizer/MainMenuCustomizerServiceTests.cs` — 5 tests:
  - `CustomizeMenu_HidesNewCampaignOption` — `StoryModeNewGame` hidden
  - `CustomizeMenu_DoesNotHideSavedGames` — `CampaignResumeGame` left alone
  - `CustomizeMenu_DoesNotHideContinueCampaign` — `ContinueCampaign` left alone
  - `CustomizeMenu_RenamesSandboxToEnterTheAgeOfMen` — `SandBoxNewGame` renamed
  - `CustomizeMenu_LogsApplied` — logger called

Note: `ModuleMenuAdapter` is not unit tested — it wraps `Module.CurrentModule` (TaleWorlds static state) which requires a live game runtime.

## How to Change the Renamed Text

Edit `MainMenuCustomizerService.cs`:

```csharp
_moduleMenuAdapter.RenameOption("SandBoxNewGame", "Enter The Age Of Men");
```

Replace the second argument with the desired display text.

## How to Hide or Show Additional Options

Add or remove calls in `MainMenuCustomizerService.CustomizeMenu()`:

```csharp
_moduleMenuAdapter.HideOption("Multiplayer");         // hide multiplayer
_moduleMenuAdapter.RenameOption("Multiplayer", "PvP"); // or rename it
```

The `HideOption` and `RenameOption` calls are idempotent — safe to call on every menu visit.

## GitHub Issue

- **Issue:** #55 — Main menu: hide Campaign, rename Sandbox to "Enter The Age Of Men"
- **Status:** Closed
