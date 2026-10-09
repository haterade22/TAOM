# Fief Management

## Overview

Press `F6` on the campaign map, or click the "Fiefs" button on the map navigation bar, to open a "fief hub" menu listing every fief the player owns. Cycle through the list with Previous/Next; click "Manage" to open the vanilla `TownManagementVM` screen for the chosen fief without traveling there. Built on top of vanilla TaleWorlds UI with no custom prefab.

## Why This Exists

- **Vanilla behavior:** Town/castle management requires the player party to be physically inside the settlement. Building queues, governor changes, garrison composition all require travel.
- **TAOM requirement:** A LOTR campaign covers Middle-earth-scale distances. Traveling to manage a remote fief consumes days of in-game time and breaks the strategic flow.
- **Without this feature:** Players ignore distant fiefs, leading to suboptimal building queues and unmanaged garrisons.

## Architecture

### Design Challenge

Vanilla `TownManagementVM` (in `TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement`) has a parameterless constructor that internally reads `Settlement.CurrentSettlement` (a static getter that falls back through the captor chain to `MobileParty.MainParty.CurrentSettlement`). To build a VM for a remote fief without actually moving the player there, we must temporarily fool the static lookup.

### Solution Approach

1. **F6 hotkey**: `Patch36_MapScreenF6` adds a `Postfix` on `SandBox.View.Map.MapScreen.OnFrameTick`. It polls `IMapScreenInputAdapter.IsF6Pressed` and, on a press, calls `FiefHubOpener.TryOpen("F6")`. It holds no guard of its own.
2. **Game menu** — `FiefHubCampaignBehavior.OnSessionLaunched` registers a `fief_hub` menu UNCONDITIONALLY (regardless of `EnableFiefManagement`) via `CampaignGameStarter.AddGameMenu`. Four options: Previous fief / Next fief / Manage / Leave. Each option's condition lambda checks `EnableFiefManagement` live so MCM toggles take effect immediately at runtime. Manage uses `manager.CreateState<FiefManagementGameState>()` + `state.Initialize(settlement)` + `manager.PushState(state)` — the `CreateState` path is what triggers `GameStateScreenManager.OnCreateState` → `CreateScreen`. (Codex review #38 caught the original `new + PushState` path bypassing all of this — see `feedback_gamestate_creation_pattern.md`.)
3. **Screen substitution** — `Patch36_GameStateScreenManager` adds a `Prefix` on `TaleWorlds.MountAndBlade.View.Screens.GameStateScreenManager.CreateScreen`. When the pushed state is `FiefManagementGameState`, substitutes our `GauntletFiefManagementScreen` and skips the vanilla call.
4. **Reflection swap (lifetime = screen lifetime)** — `IRemoteFiefSettlementSwapper` reflects on `MobileParty._currentSettlement` (a private `[SaveableField]`). The screen swaps the field to the target fief in `OnInitialize`, constructs `TownManagementVM`, and KEEPS the swap active until `OnFinalize`. This is necessary because vanilla child VMs (`TownManagementReserveControlVM.ExecuteConfirm`, `SettlementGovernorSelectionItemVM.OnGovernorChosen`) read `Settlement.CurrentSettlement.Town` at click time — NOT cached at construction. Safe because `IsMenuState=true` stops campaign time, so no other code runs while the swap is active. Layer setup mirrors vanilla `SandBox.GauntletUI.Menu.GauntletMenuTownManagementView`. (Codex review #38 caught the original swap-construct-restore-immediately pattern — see `feedback_static_singleton_swap_runtime_audit.md`.)
5. **Menu state caching (presenter)** — `IFiefHubMenuPresenter` / `FiefHubMenuPresenter` owns the cached fief list, selected-index cursor, "is player at this fief" flag, title rendering, and "is the manage option enabled" derivation. Refreshed once per `OnMenuInit`; the option-condition lambdas (which the engine polls every frame the menu is visible) read the cache without re-iterating `Settlement.All`. Keeps `FiefHubCampaignBehavior` as a thin (~85-line) registration shim per ADR-002.
6. **Shared opener and gate**: `FiefHubOpener` (a concrete class, no interface) is the one place that decides whether the hub opens and opens it. `GetAvailability()` is a cheap, side-effect-free query in a fixed order: the `EnableFiefManagement` toggle (`FeatureDisabled`), then the map state (`MapBusy`: `IFiefHubHostAdapter.IsMapClearForMenu`, which is `MapMenuGate`, plus not enlisted-attached), then the fief count (`NoFiefs`), else `Available`. `MapMenuGate` (`Main/Adapters/MapMenuGate.cs`) is the single list of conditions under which TAOM may push a game menu onto the map; the field camp's "Make camp" query delegates to it too. It mirrors vanilla `MapScreen.OnFrameTick`'s full set (map state active; not in a menu, battle simulation, army management, the marriage or heir popup, map cheats, a map incident, the overlay context menu or the encyclopedia; and `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`, which refuses for a prisoner, an encounter, the raft and ferry states) and adds two refusals of its own: the escape menu being open, and `Campaign.CurrentMenuContext` being set (a click can see a one-frame-stale `IsInMenu`). `TryOpen(source)` opens `fief_hub` when available, shows the yellow "You don't own any fiefs yet." message for `NoFiefs`, and stays silent for the other two. F6 and the button share the opener and the gate; the button additionally honours vanilla's `IsNavigationBarEnabled`, which is a property of the bar. A source test (`TheMapModalGuardList_IsWrittenOnce`) fails if a second file names the map's modal flags.
7. **Navigation-bar button**: see [The navigation-bar button](#the-navigation-bar-button).

### Component Diagram

```
F6 keypress (campaign map)                 "Fiefs" button (map navigation bar)
        |                                              |
  Patch36_MapScreenF6                        TaomFiefsNavigationElement
  (Postfix on MapScreen.OnFrameTick)         (added by Patch36_MapNavigationElements)
        \                                            /
         ---------- FiefHubOpener.TryOpen ----------
                          |
              IFiefHubHostAdapter.OpenHub
              (GameMenu.ActivateGameMenu("fief_hub"))
                          |                      ___ ISettlementOwnershipAdapter
  FiefHubCampaignBehavior (carousel) -----------/
        |
  Game.Current.GameStateManager.PushState(FiefManagementGameState)
        |
  Patch36_GameStateScreenManager (Prefix on GameStateScreenManager.CreateScreen)
        |
  GauntletFiefManagementScreen
        |          \___ IRemoteFiefSettlementSwapper.Swap(targetFief)
  TownManagementVM (vanilla) -- built against the swapped settlement
```

## The navigation-bar button

Issue #789. A "Fiefs" button sits at the right end of the campaign map's navigation bar and does what F6 does.

**How it gets there.** `MapNavigationHandler`'s constructor builds its element array from the protected virtual `OnCreateElements`. `Patch36_MapNavigationElements` is a Postfix on it that appends one `TaomFiefsNavigationElement` (StringId `taom_fiefs`) through a pure `AppendOnce`, which leaves the array alone when it is null, already holds the element, or the factory returns null; a throwing build reaches the Postfix's log-once catch and `__result` stays vanilla's array. NavalDLC's `NavalMapNavigationHandler` overrides `OnCreateElements` and calls the base, so the Postfix runs for both handlers; NavalDLC then inserts Manage Fleet at index 3, which only moves the Fiefs button one slot right. The Postfix runs inside the handler constructor, so it holds no handler state. The Postfix resolves `FiefHubOpener` and `IModLogger` and passes them into the element's constructor; the element never calls IoC itself.

**Rendering.** `MapBar.xml` (vanilla prefab) renders every element generically with `IconID="@ItemId"`, so no prefab change is needed. The button's art is one `taom_fiefs` layer in each of the two brushes in `Main/_Module/GUI/Brushes/MapBar.xml`: `MapBar.Left.Button.Backgrounds` (reuses `MapBar\bottom_left_button4_party`, the same background vanilla gives NavalDLC's `manage_fleet`) and `MapBar.Left.Icons` (`SaveLoad\icon_fiefs`, a 50x50 castle tower already in `TAOMSpriteData.xml`).

**Permission.** `IsEnabled` is polled every frame, so `GetPermission` stays cheap. It asks vanilla's `MapNavigationHelper.IsNavigationBarEnabled(handler)` first (a disabled bar gives a not-authorized item with no reason, as vanilla does), then `FiefHubOpener.GetAvailability()`:

| Availability | Button | Tooltip text |
|---|---|---|
| `Available` | enabled | "Fiefs [F6]" through vanilla's `str_hotkey_with_hint` (`{TEXT} [{HOTKEY}]`); plain "Fiefs" on a gamepad |
| `FeatureDisabled` | disabled | `{=taom_fief_nav_disabled}` Fief management is turned off in the mod options. |
| `MapBusy` | disabled | `{=taom_fief_nav_busy}` The fief hub cannot be opened right now. |
| `NoFiefs` | disabled | `{=taom_fief_no_fiefs}` You don't own any fiefs yet. |

The element is always present, so the MCM toggle takes effect live. `IsActive`, `IsLockingNavigation` and `HasAlert` are all false: `fief_hub` is a game menu, not a pushed state, and an active element would leak the bar's selected look onto screens pushed from it. `OpenView` re-checks the permission, then calls `TryOpen("nav button")`; any exception is caught and logged once. The hotkey hint is the literal "F6" (`HotkeyLabel` in the element): F6 is a raw `InputKey` with no game-key binding to look up, and the literal matches every shipped translation of `str_game_key_text.f6`.

## Configuration

### MCM Settings (group `Fief Management`, GroupOrder 26)

| Setting | Default | Effect |
|---------|---------|--------|
| `Enable Fief Management` | `true` | Master toggle. When off, F6 is inert and the navigation button is disabled with a reason. The `fief_hub` menu stays registered so the toggle works live. |
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
| `Main/Features/FiefManagement/FiefManagementSettingsProvider.cs` | Wraps `TaomSettings.Instance` through a cached lazy accessor (the button polls it every frame) |
| `Main/Features/FiefManagement/FiefManagementIoC.cs` | DryIoc registration (Reuse.Singleton across the board) |
| `Main/Features/FiefManagement/Models/FiefSummary.cs` | DTO — Id / Name / IsTown / IsCastle. No sealed Settlement reference (resolved via adapter at the consequence boundary). |
| `Main/Features/FiefManagement/Models/FiefManagementGameState.cs` | `GameState` subclass carrying the target fief |
| `Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs` | Mirrors vanilla `GauntletMenuTownManagementView`; performs the reflection swap inside `OnInitialize` |
| `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs` | Registers `fief_hub` menu; owns `_selectedIndex` and resets on new game / load |
| `Main/Features/FiefManagement/FiefHubOpener.cs` | The shared opener (concrete class): `GetAvailability()` (cheap query) and `TryOpen(source)` |
| `Main/Features/FiefManagement/FiefHubAvailability.cs` | The four answers: `Available`, `FeatureDisabled`, `MapBusy`, `NoFiefs` |
| `Main/Features/FiefManagement/UI/TaomFiefsNavigationElement.cs` | The navigation-bar button: a `MapNavigationElementBase` subclass |
| `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs` | F6 hotkey Postfix on `MapScreen.OnFrameTick`; calls the opener |
| `Main/Features/FiefManagement/Hooks/Patch36_MapNavigationElements.cs` | Postfix on `MapNavigationHandler.OnCreateElements` that appends the button |
| `Main/Features/FiefManagement/Hooks/Patch36_GameStateScreenManager.cs` | Prefix on `GameStateScreenManager.CreateScreen` substituting our screen |
| `Main/Adapters/ISettlementOwnershipAdapter.cs` + impl | `Settlement.All` filter by `OwnerClan == Clan.PlayerClan`; current-settlement check by string id |
| `Main/Adapters/IFiefHubHostAdapter.cs` + impl | The engine side of the opener: the map gate (delegates to `MapMenuGate`), the "no fiefs" message, `GameMenu.ActivateGameMenu("fief_hub")` |
| `Main/Adapters/MapMenuGate.cs` | The one list of conditions for pushing a game menu onto the map; used by the host adapter and by the field camp's `MapScreenCampMenuActivationQuery` |
| `Main/Adapters/IMapScreenInputAdapter.cs` + impl | Wraps `Input.IsKeyPressed(InputKey.F6)` for testability |
| `Main/Adapters/IRemoteFiefSettlementSwapper.cs` + impl | Reflection on `MobileParty._currentSettlement`; logs once at startup if the field is missing |

## Dependencies

- `ISettlementOwnershipAdapter` — produces `IReadOnlyList<FiefSummary>` filtered by `OwnerClan == Clan.PlayerClan`
- `IMapScreenInputAdapter` — wraps the F6 keypress (testability)
- `IFiefHubHostAdapter`: the map gate (`MapMenuGate`), the "no fiefs" message and the menu activation (used by `FiefHubOpener`)
- `IEnlistmentStateQuery`: the enlisted-attached state counts as a busy map
- `IRemoteFiefSettlementSwapper` — wraps reflection on `MobileParty._currentSettlement`
- `IModLogger` — debug + once-per-process error logging if reflection target is missing

### Engine API surface verified

| API | Verified location |
|-----|-------------------|
| `MapScreen.OnFrameTick` | `protected override void OnFrameTick(float dt)` in `Modules/SandBox/.../SandBox.View.dll` |
| `MapNavigationHandler.OnCreateElements` | `protected virtual INavigationElement[] OnCreateElements()` in `SandBox.View.Map.Navigation` (SandBox.View.dll, v1.5.5); NavalDLC overrides it and calls the base |
| `MapNavigationElementBase` | abstract class in `SandBox.View.Map.Navigation`; abstract `IsActive`, `IsLockingNavigation`, `HasAlert`, `StringId`, `OpenView()`, `OpenView(params object[])`, `GoToLink()` and protected abstract `GetPermission`, `GetTooltip`, `GetAlertTooltip` |
| `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason` | `public static bool GetMapScreenActionIsEnabledWithReason(out TextObject disabledReason)` in `TaleWorlds.CampaignSystem.ViewModelCollection`; reads `Hero.MainHero` and `MobileParty.MainParty` unguarded, so `MapMenuGate` calls it only after the map-state check |
| `MapNavigationHelper.IsNavigationBarEnabled` | `public static bool IsNavigationBarEnabled(MapNavigationHandler handler)`; dereferences the handler, so never pass null |
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

- `TAOM.Tests/Features/FiefManagement/FiefHubOpenerTests.cs`: each guard in `GetAvailability` (toggle, map not clear, enlisted-attached, no fiefs, every other enlistment state stays available), the fixed guard order, that a busy map never reads the fief count, and `TryOpen` for all four outcomes plus debug logging.
- `TAOM.Tests/Features/FiefManagement/FiefsNavButtonPostfixTests.cs`: `AppendOnce` appends exactly one element at the end, never twice, tolerates another mod's element, a null array and a null factory result, and never mutates its input; the Postfix, invoked by reflection with no Campaign, throws nothing and leaves `__result` as vanilla's array.
- `TAOM.Tests/Features/FiefManagement/FiefsNavButtonWiringTests.cs`: F6 and the element both route through `FiefHubOpener.TryOpen` (source pins), the element takes its dependencies through its constructor and is never active, both brushes carry a `taom_fiefs` layer, the icon sprite is declared in `TAOMSpriteData.xml`, the map's modal flags are named in `MapMenuGate.cs` only and both consumers delegate to it, the gate keeps every guard and runs vanilla's helper after the map-state check, and a real DryIoc container closes the `FiefHubOpener` graph.
- `TAOM.Tests/Migration/FiefsNavButtonBindingTests.cs`: binding pins against the installed engine for `OnCreateElements`, `CampaignUIHelper.GetMapScreenActionIsEnabledWithReason`, `MapNavigationHelper.IsNavigationBarEnabled` and every `MapNavigationElementBase` member the element overrides. `HarmonyPatchBindingTests` resolves the new patch target automatically.
- `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`: `FiefManagementSettingsProvider` reads `TaomSettings.Instance` only in its lazy accessor, resolves from a real container, and reads through the cached instance.

`FiefManagementSettingsProvider` is a passthrough over `TaomSettings.Instance`. Its `internal` constructor takes a `TaomSettings` so the read-through is tested without MCM (`TaomSettings.Instance` itself triggers an `MCMv5` assembly load that fails outside the game runtime).

## Orphaned XML prefabs (NOT copied to TAOM)

The original 1.2.x module ships three XML prefabs in `GUI/Prefabs/`:

- `FiefHub.xml`
- `FiefManagement.xml`
- `FiefNavOverlay.xml`

The DLL **does not load any of them** — `GauntletFiefManagementScreen` loads the vanilla `TownManagement` movie via `_layer.LoadMovie("TownManagement", _dataSource)`. Per the integration plan and `/deep-review` standards, dead prefabs are not copied. If a future iteration wants a custom screen layout, the prefabs would need to be authored fresh against the current widget schemas anyway.

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
4. The navigation button's tooltip carries a separate literal, `HotkeyLabel` in `TaomFiefsNavigationElement`; change it with the key.

## Known limitations

- The reflection target `MobileParty._currentSettlement` is a private TaleWorlds field. If TaleWorlds renames or removes it in a future patch, the swap silently no-ops and `TownManagementVM` will read the player's actual current settlement (or null). The adapter logs `LogError` once at startup if the FieldInfo lookup returns null.
- Pressing Confirm/Exit inside the screen pops back to the `fief_hub` menu (not all the way to the campaign map). This matches vanilla town-management UX where Done returns to the settlement menu.
- The button and F6 share one gate (`MapMenuGate`), so while any game menu is up (including a town or castle menu: inside a settlement the party is at a menu, which `IsInMenu` and `CurrentMenuContext` both report) or a screen such as the clan screen is on top, the button is disabled with the "cannot be opened right now" reason and F6 does nothing. This also keeps the hub from interleaving with the vanilla settlement menu. The button does not pop a screen to open the hub.
- On a gamepad the tooltip drops the "F6" hint, and F6 itself has no gamepad binding; the button is the only gamepad route. Not tested on a gamepad.
- The button look (the castle icon on the party-button background, as the last button after Kingdom) is verified from the sprite files only, not in game.

## Changelog

- 2026-10-09: "Fiefs" navigation-bar button (#789). F6 and the button now share `FiefHubOpener` and `MapMenuGate`; `Patch36_MapScreenF6` lost its own guards, message and menu call. The gate also refuses while the escape menu is open, while a menu context exists and where vanilla's map-action gate refuses (prisoner, encounter, raft, ferry), and the field camp's "Make camp" query now uses it too. `FiefManagementSettingsProvider` caches its settings reference. Removed the unused `FiefManagementNavItemVM`. New keys `taom_fief_nav_disabled` and `taom_fief_nav_busy`.
- 2026-05-14 — F6 fast-path: `FiefHubService.Count` now delegates to `ISettlementOwnershipAdapter.GetPlayerOwnedFiefCount()` (iterates the cached `Clan.PlayerClan.Settlements`) instead of building a `FiefSummary` list off `Settlement.All`; closes deferred #143 (+5 tests).
- 2026-05-13 — Swap restore safety + presenter reset: `RemoteFiefSettlementSwapper.Restore` uses the party ref captured at `Swap` time (no longer leaves `_currentSettlement` pointing at a remote fief), and `FiefHubMenuPresenter.Reset()` clears all 4 stateful fields to drop stale cross-campaign state; partial #143.
- 2026-05-13 — Behavior-callback test coverage: added `FiefHubCampaignBehaviorTests.cs` (7 tests covering presenter-reset delegation, SyncData, and event/menu wiring); closes #177.

## GitHub Issue

- **Issue:** #789 (the Fiefs navigation-bar button). The F6 hub itself: N/A, ported as part of the 7-feature LOTRAOM port queue.
- **Status:** #789 is built, tested offline and reviewed; the in-game rows below are owed. The original F6 hub still awaits in-game verification.

### In-game checklist (#789, owed)

| Case | Expect |
|---|---|
| Available | The Fiefs button is last on the bar, tooltip "Fiefs [F6]"; a click opens `fief_hub` exactly as F6 does |
| Feature off, then on | Toggle Enable Fief Management in MCM mid-session: the button greys with the "turned off in the mod options" reason, then enables again, without a reload; F6 follows |
| No fiefs | The button is greyed with "You don't own any fiefs yet."; F6 shows the yellow message |
| Map busy | The button is greyed with "cannot be opened right now", and F6 does nothing, in: a town or castle menu, army management, the encyclopedia, enlisted-attached service, the escape menu, and as a prisoner |
| Bar disabled | Mid-save, or with the bar locked, the button is greyed with no reason, like its neighbours |
| NavalDLC | With the DLC off and on, the button is still last (Manage Fleet sits at index 3) |
| Co-op | A Coop launch: the bar builds, the button behaves |
| Gamepad | The button takes focus and opens the hub; the tooltip has no F6 hint |
| Look | The castle icon on the party background after Kingdom's 90 px background reads as a button and lines up with the frame |
| Field camp | "Make camp" is now also refused in the newly gated states (escape menu, prisoner, encounter, raft, ferry, a stale menu context) |

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
