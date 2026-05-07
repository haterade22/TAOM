# Fief Management

## Overview

Press `F6` on the campaign map to open a "fief hub" menu listing every fief the player owns. Cycle through the list with Previous/Next; click "Manage" to open the vanilla `TownManagementVM` screen for the chosen fief without traveling there. Built on top of vanilla TaleWorlds UI — no custom prefab.

## Why This Exists

- **Vanilla behavior:** Town/castle management requires the player party to be physically inside the settlement. Building queues, governor changes, garrison composition all require travel.
- **TAOM requirement:** A LOTR campaign covers Middle-earth-scale distances. Traveling to manage a remote fief consumes days of in-game time and breaks the strategic flow.
- **Without this feature:** Players ignore distant fiefs, leading to suboptimal building queues and unmanaged garrisons.

## Architecture

### Design Challenge

Vanilla `TownManagementVM` (in `TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement`) has a parameterless constructor that internally reads `Settlement.CurrentSettlement` (a static getter that falls back through the captor chain to `MobileParty.MainParty.CurrentSettlement`). To build a VM for a remote fief without actually moving the player there, we must temporarily fool the static lookup.

### Solution Approach

1. **F6 hotkey** — `Patch36_MapScreenF6` adds a `Postfix` on `SandBox.View.Map.MapScreen.OnFrameTick`. Guards: `EnableFiefManagement` setting, `ActiveState is MapState`, `__instance.IsInMenu == false` (rejects vanilla settlement menus that layer on top of MapScreen), `IsKeyPressed(F6)`. Activates the menu and shows the "no fiefs" message at the boundary — the service stays query-only.
2. **Game menu** — `FiefHubCampaignBehavior.OnSessionLaunched` registers a `fief_hub` menu UNCONDITIONALLY (regardless of `EnableFiefManagement`) via `CampaignGameStarter.AddGameMenu`. Four options: Previous fief / Next fief / Manage / Leave. Each option's condition lambda checks `EnableFiefManagement` live so MCM toggles take effect immediately at runtime. Manage uses `manager.CreateState<FiefManagementGameState>()` + `state.Initialize(settlement)` + `manager.PushState(state)` — the `CreateState` path is what triggers `GameStateScreenManager.OnCreateState` → `CreateScreen`. (Codex review #38 caught the original `new + PushState` path bypassing all of this — see `feedback_gamestate_creation_pattern.md`.)
3. **Screen substitution** — `Patch36_GameStateScreenManager` adds a `Prefix` on `TaleWorlds.MountAndBlade.View.Screens.GameStateScreenManager.CreateScreen`. When the pushed state is `FiefManagementGameState`, substitutes our `GauntletFiefManagementScreen` and skips the vanilla call.
4. **Reflection swap (lifetime = screen lifetime)** — `IRemoteFiefSettlementSwapper` reflects on `MobileParty._currentSettlement` (a private `[SaveableField]`). The screen swaps the field to the target fief in `OnInitialize`, constructs `TownManagementVM`, and KEEPS the swap active until `OnFinalize`. This is necessary because vanilla child VMs (`TownManagementReserveControlVM.ExecuteConfirm`, `SettlementGovernorSelectionItemVM.OnGovernorChosen`) read `Settlement.CurrentSettlement.Town` at click time — NOT cached at construction. Safe because `IsMenuState=true` stops campaign time, so no other code runs while the swap is active. Layer setup mirrors vanilla `SandBox.GauntletUI.Menu.GauntletMenuTownManagementView`. (Codex review #38 caught the original swap-construct-restore-immediately pattern — see `feedback_static_singleton_swap_runtime_audit.md`.)
5. **Menu state caching (presenter)** — `IFiefHubMenuPresenter` / `FiefHubMenuPresenter` owns the cached fief list, selected-index cursor, "is player at this fief" flag, title rendering, and "is the manage option enabled" derivation. Refreshed once per `OnMenuInit`; the option-condition lambdas (which the engine polls every frame the menu is visible) read the cache without re-iterating `Settlement.All`. Keeps `FiefHubCampaignBehavior` as a thin (~85-line) registration shim per ADR-002.

### Component Diagram

```
F6 keypress (campaign map)
        |
  Patch36_MapScreenF6 (Postfix on MapScreen.OnFrameTick)
        |
  IFiefHubService.OpenIfOwnsFiefs()
        |
  GameMenu.ActivateGameMenu("fief_hub")
        |                                  ___ ISettlementOwnershipAdapter
  FiefHubCampaignBehavior (carousel) ----/
        |
  Game.Current.GameStateManager.PushState(FiefManagementGameState)
        |
  Patch36_GameStateScreenManager (Prefix on GameStateScreenManager.CreateScreen)
        |
  GauntletFiefManagementScreen
        |          \___ IRemoteFiefSettlementSwapper.Swap(targetFief)
  TownManagementVM (vanilla) — built against the swapped settlement
```

## Configuration

### MCM Settings (group `Fief Management`, GroupOrder 26)

| Setting | Default | Effect |
|---------|---------|--------|
| `Enable Fief Management` | `true` | Master toggle. When off, F6 is inert and the `fief_hub` menu is not registered. |
| `Allow Remote Building Queue` | `true` | When on, "Manage" is enabled regardless of where the player is. When off, "Manage" is disabled (with hint text) unless the player is physically at the selected fief. |
| `Fief Management Debug Mode` | `false` | Diagnostic `[FiefManagement]` messages on the in-game HUD. |

### `AllowRemoteBuildingQueue` gating — design decision

The original 1.2.x module shipped this MCM setting but never consulted it in code (a "user-facing promise that doesn't match implementation" violation per `feedback_user_facing_promise_must_match_code.md`).

For the TAOM port we **gate the menu option** rather than UI-shimming the vanilla `TownManagementVM` (which exposes no clean enable/disable hook for the queue alone). When `AllowRemoteBuildingQueue=false` AND the player is not at the selected fief, the `fief_hub_manage` option is disabled with the hint text `"Remote building queue disabled — visit the fief to manage."`. When the player travels to the fief, the option becomes enabled again.

The trade-off: with the toggle off, you can't view stats remotely either. The cleaner alternative (full view-only mode) requires reflection on the vanilla VM's project-selection state and is fragile across game patches.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FiefManagement/IFiefHubService.cs` | Service interface — pure query layer (list ordered fiefs, cycle, count, lookup, PlayerIsAt). No engine side effects. |
| `Main/Features/FiefManagement/FiefHubService.cs` | Stateless implementation. No TaleWorlds engine imports. |
| `Main/Features/FiefManagement/IFiefHubMenuPresenter.cs` | Presenter interface — menu state caching, title rendering, manage-option enable derivation. |
| `Main/Features/FiefManagement/FiefHubMenuPresenter.cs` | Owns `_selectedIndex`, cached `_menuFiefs`, `_menuCurrentFief`, `_menuCurrentAtPlayer`. Refreshed in `OnMenuInit`. |
| `Main/Features/FiefManagement/IFiefManagementSettingsProvider.cs` | MCM passthrough interface |
| `Main/Features/FiefManagement/FiefManagementSettingsProvider.cs` | Wraps `TaomSettings.Instance` |
| `Main/Features/FiefManagement/FiefManagementIoC.cs` | DryIoc registration (Reuse.Singleton across the board) |
| `Main/Features/FiefManagement/Models/FiefSummary.cs` | DTO — Id / Name / IsTown / IsCastle. No sealed Settlement reference (resolved via adapter at the consequence boundary). |
| `Main/Features/FiefManagement/Models/FiefManagementGameState.cs` | `GameState` subclass carrying the target fief |
| `Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs` | Mirrors vanilla `GauntletMenuTownManagementView`; performs the reflection swap inside `OnInitialize` |
| `Main/Features/FiefManagement/UI/FiefManagementNavItemVM.cs` | Optional nav-bar VM (F6 indicator) |
| `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs` | Registers `fief_hub` menu; owns `_selectedIndex` and resets on new game / load |
| `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs` | F6 hotkey Postfix on `MapScreen.OnFrameTick` |
| `Main/Features/FiefManagement/Hooks/Patch36_GameStateScreenManager.cs` | Prefix on `GameStateScreenManager.CreateScreen` substituting our screen |
| `Main/Adapters/ISettlementOwnershipAdapter.cs` + impl | `Settlement.All` filter by `OwnerClan == Clan.PlayerClan`; current-settlement check by string id |
| `Main/Adapters/IMapScreenInputAdapter.cs` + impl | Wraps `Input.IsKeyPressed(InputKey.F6)` for testability |
| `Main/Adapters/IRemoteFiefSettlementSwapper.cs` + impl | Reflection on `MobileParty._currentSettlement`; logs once at startup if the field is missing |

## Dependencies

- `ISettlementOwnershipAdapter` — produces `IReadOnlyList<FiefSummary>` filtered by `OwnerClan == Clan.PlayerClan`
- `IMapScreenInputAdapter` — wraps the F6 keypress (testability)
- `IRemoteFiefSettlementSwapper` — wraps reflection on `MobileParty._currentSettlement`
- `IModLogger` — debug + once-per-process error logging if reflection target is missing

### v1.3.15 API surface verified

| API | Verified location |
|-----|-------------------|
| `MapScreen.OnFrameTick` | `protected override void OnFrameTick(float dt)` in `Modules/SandBox/.../SandBox.View.dll` |
| `GameStateScreenManager.CreateScreen` | `TaleWorlds.MountAndBlade.View.Screens` in `Modules/Native/.../TaleWorlds.MountAndBlade.View.dll` (NOT `TaleWorlds.Core` — the original-module assumption was wrong) |
| `TownManagementVM` | Parameterless ctor, in `TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement` (NOT `SandBox.GauntletUI` — the original-module assumption was wrong) |
| `MobileParty._currentSettlement` | `[SaveableField(1001)] private Settlement _currentSettlement` — reflection target valid |
| `Settlement.CurrentSettlement` static getter | Falls through to `MobileParty.MainParty.CurrentSettlement` — confirms the reflection target is the right field to swap |

## Tests

- `TAOM.Tests/Features/FiefManagement/FiefHubServiceTests.cs` — 22 tests:
  - `GetOrderedFiefs`: empty / sort (towns first) / alphabetical-within-class / null-skip
  - `Count`: empty / 3 fiefs
  - `Next/Previous`: empty / single / wraparound (fwd + reverse) / middle-step
  - `Clamp`: negative / beyond-count / empty
  - `GetAt`: empty (null) / beyond-count (clamps to last)
  - `PlayerIsAt`: null / adapter true / adapter false

`FiefManagementSettingsProvider` is a 4-line passthrough over `TaomSettings.Instance`. It cannot be exercised in MSTest because `TaomSettings.Instance` triggers an `MCMv5` assembly load that fails outside the game runtime — same reason `SiegeDismountSettingsProvider` has no tests.

## Orphaned XML prefabs (NOT copied to TAOM)

The original 1.2.x module ships three XML prefabs in `GUI/Prefabs/`:

- `FiefHub.xml`
- `FiefManagement.xml`
- `FiefNavOverlay.xml`

The DLL **does not load any of them** — `GauntletFiefManagementScreen` loads the vanilla `TownManagement` movie via `_layer.LoadMovie("TownManagement", _dataSource)`. Per the integration plan and `/deep-review` standards, dead prefabs are not copied. If a future iteration wants a custom screen layout, the prefabs would need to be authored fresh against v1.3.15 widget schemas anyway.

## How to Add a New Menu Option

1. In `FiefHubCampaignBehavior.RegisterMenu`, add a new `starter.AddGameMenuOption` block. Mirror the existing four (`fief_hub_prev/next/manage/leave`).
2. Pick an `index:` integer to order it within the menu. Existing slots: 0 (prev), 1 (next), 2 (manage), 3 (leave).
3. Use `args.optionLeaveType` and `args.IsEnabled` for vanilla styling cues.
4. Add a localization string id of the form `{=taom_fief_hub_<id>}`.

## How to Change the Hotkey

The hotkey is currently hard-coded as `InputKey.F6` in `MapScreenInputAdapter.IsF6Pressed`. To make it configurable:
1. Add an MCM setting (e.g., `FiefHubHotkey`) of type `[SettingPropertyText]`.
2. Update the adapter to parse the setting via `Enum.TryParse<InputKey>(...)` with `F6` as fallback.
3. Wire the setting through `FiefManagementSettingsProvider`.

## Known limitations

- The reflection target `MobileParty._currentSettlement` is a private TaleWorlds field. If TaleWorlds renames or removes it in a future patch, the swap silently no-ops and `TownManagementVM` will read the player's actual current settlement (or null). The adapter logs `LogError` once at startup if the FieldInfo lookup returns null.
- Pressing Confirm/Exit inside the screen pops back to the `fief_hub` menu (not all the way to the campaign map). This matches vanilla town-management UX where Done returns to the settlement menu.
- When the player is currently INSIDE a settlement, F6 is guarded by `ActiveState is MapState` — the hotkey is inert. This prevents interleaving the hub menu with the vanilla settlement menu.

## GitHub Issue

- **Issue:** N/A — feature ported as part of the 7-feature LOTRAOM port queue
- **Status:** Awaiting in-game verification before commit
