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
3. A **headless dedicated server** sets that screen root thousands of times per boot with StoryMode and SandBox not loaded, so both options miss on every single call — one reported server log carried 4,803 MainMenuCustomizer lines. A miss there is expected, not an error

### Solution Approach

Override `MBSubModuleBase.OnBeforeInitialModuleScreenSetAsRoot()` — fires after all module options are registered, before the UI renders. The `MainMenuCustomizerService` overrides options via the adapter on each visit (idempotent). The Pre-compile Shaders option uses a null-guard to add only once.

Constraint 3 is answered by deduping the **logging only** — the customization still runs on every call, because the engine can rebuild the initial-state options between screen-root sets and skipping the work would silently drop the rename on a real client. That reasoning is pinned by `CustomizeMenu_ManyCalls_StillAppliesEveryTime`.

### Component Diagram

```
SubModule.OnBeforeInitialModuleScreenSetAsRoot()  [fires every menu visit]
    │
    ├── IMainMenuCustomizerService.CustomizeMenu()  [idempotent overrides, runs every call]
    │       │
    │       ├── IModuleMenuAdapter
    │       │       HideOption("StoryModeNewGame")   → bool  (false = no such option registered)
    │       │       RenameOption("SandBoxNewGame")   → bool  (false = no such option registered)
    │       │
    │       └── logging deduped by the SERVICE, not the adapter
    │               false → warn once per option id      (HashSet<string> _reportedMisses)
    │               both true → "applied" once per session (_appliedLogged)
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
| `Main/Features/MainMenuCustomizer/IModuleMenuAdapter.cs` | Adapter interface — `HideOption` / `RenameOption`, both returning `bool` |
| `Main/Features/MainMenuCustomizer/ModuleMenuAdapter.cs` | Wraps `Module.CurrentModule` TW API. **Logger-free by design** — a miss is returned as `false` for the caller to dedupe. Re-adding a logger here reintroduces the headless log flood |
| `Main/Features/MainMenuCustomizer/MainMenuCustomizerIoC.cs` | DryIoc registration |
| `Main/SubModule.cs` | Entry point — `OnBeforeInitialModuleScreenSetAsRoot` override + shaders guard |

## Dependencies

- `IModLogger` (Core) — injected into `MainMenuCustomizerService` only. It owns both dedupe latches: a warning per option id that could not be found (`HashSet<string> _reportedMisses`) and one "applied" line per session (`_appliedLogged`). `ModuleMenuAdapter` takes no logger at all.

## Tests

- `TAOM.Tests/Features/MainMenuCustomizer/MainMenuCustomizerServiceTests.cs` — 9 tests:
  - `CustomizeMenu_HidesNewCampaignOption` — `StoryModeNewGame` hidden
  - `CustomizeMenu_DoesNotHideSavedGames` — `CampaignResumeGame` left alone
  - `CustomizeMenu_DoesNotHideContinueCampaign` — `ContinueCampaign` left alone
  - `CustomizeMenu_RenamesSandboxToEnterTheAgeOfMen` — `SandBoxNewGame` renamed
  - `CustomizeMenu_LogsApplied` — logger called
  - `CustomizeMenu_OptionsMissing_WarnsOncePerOptionAcrossManyCalls` — 50 all-missing calls produce one warning per option id
  - `CustomizeMenu_ManyCalls_StillAppliesEveryTime` — the work is never skipped, only the logging
  - `CustomizeMenu_ManyCalls_LogsAppliedOnce` — one applied line per session
  - `CustomizeMenu_OptionAppearsLater_WarnsOnceThenLogsApplied` — a client whose menu opens before StoryMode finishes registering: first pass warns, a later pass succeeds, and the success is still reported

Note: `ModuleMenuAdapter` is not unit tested — it wraps `Module.CurrentModule` (TaleWorlds static state) which requires a live game runtime. That untestability is why the log-or-not decision lives in the service rather than the adapter.

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

## Changelog

- 2026-08-03 — Fix: deduped the headless log flood (4,803 lines in one dedicated-server boot log). `IModuleMenuAdapter.HideOption`/`RenameOption` now return `bool` instead of logging; the service warns once per missing option id and logs "applied" once per session. No issue filed.
- 2026-04-29 — Localized the "Enter The Age Of Men" label: `MainMenuCustomizerService.cs` now wraps the rename text with `{=taom_main_menu_new_game}` (#96).
- 2026-04-04 — Fix: restored "Saved Games" and "Continue Campaign" (only `StoryModeNewGame` stays hidden) and guarded the duplicate "Pre-compile Shaders" entry with a `GetInitialStateOptionWithId` null-check (#55).
- 2026-04-04 — Feat: initial MainMenuCustomizer — hide vanilla "Campaign"/`StoryModeNewGame` and rename "Sandbox" to "Enter The Age Of Men" via `OnBeforeInitialModuleScreenSetAsRoot` + `IModuleMenuAdapter` (#55).

## GitHub Issue

- **Issue:** #55 — Main menu: hide Campaign, rename Sandbox to "Enter The Age Of Men"
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
