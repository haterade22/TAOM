# Feature Port Session: FiefManagement

You are porting feature #4 of 7 from the external-developer drop at `Downloads/Features_fixed/FiefManagement/` into TAOM's `Main/Features/FiefManagement/`. The other 6 features are tracked separately. Don't touch them.

## Prerequisites — read before writing any code

1. **The integration plan**: `C:/Users/mikew/.claude/plans/one-of-our-coders-steady-raccoon.md` — section "7. FiefManagement" has the planned file layout.

2. **This prompt** — end to end.

3. **Pattern templates**:
   - [Main/Features/SiegeDismount/](../../Main/Features/SiegeDismount/) — singleton-service + thin MissionBehavior pattern
   - [Main/Features/MixedFormations/](../../Main/Features/MixedFormations/) — Harmony Prefix patch pattern
   - [docs/features/mixed-formations.md](../features/mixed-formations.md) — feature doc template

4. **The decompiled source you're porting**:
   `C:/Users/mikew/Downloads/Features_fixed/_decompiled/FiefManagement/FiefManagement.decompiled.cs`

   Read it end-to-end. Critical sections: `FiefHubCampaignBehavior` (game menu `fief_hub` with carousel prev/next/manage/leave), `FiefManagementMapBarPatches` (F6 hotkey via Postfix on `MapScreen.OnFrameTick`), `FiefManagement_CreateScreen_Patch` (Prefix on `GameStateScreenManager.CreateScreen` to substitute screen), `FiefManagementState` (custom GameState), `GauntletFiefManagementScreen` (the **`MobileParty.MainParty._currentSettlement` reflection swap** — temporarily swap settlement, instantiate vanilla TownManagementVM, restore. **This is the trickiest part of the feature** — vanilla TownManagementVM reads `MobileParty.MainParty.CurrentSettlement` in its constructor; the swap fools it into building a VM for a remote fief).

5. **GUI prefab note**: the decompiled module ships three XML prefabs (`FiefHub.xml`, `FiefManagement.xml`, `FiefNavOverlay.xml`) but the DLL **does NOT load any of them** — the `GauntletFiefManagementScreen` loads the vanilla `"TownManagement"` movie. Drop all three prefabs on port. Don't copy them to `Main/_Module/GUI/Prefabs/`.

## Goal in one sentence

Press F6 on the campaign map → game menu shows your owned fiefs in a carousel → "Manage" opens the vanilla TownManagementVM screen for the chosen fief without traveling there.

## Architecture — what to build

### Files to create

```
Main/Features/FiefManagement/
├── IFiefHubService.cs                    ← list owned fiefs, OpenForFief(settlement), CycleNext/Prev
├── FiefHubService.cs                     ← state: currently selected index; computes ordered fief list (towns first, then castles, both alphabetical)
├── IFiefManagementSettingsProvider.cs
├── FiefManagementSettingsProvider.cs     ← wraps TaomSettings.Instance
├── Models/
│   └── FiefManagementGameState.cs        ← port of FiefManagementState : GameState; carries Settlement Fief reference
├── UI/
│   ├── GauntletFiefManagementScreen.cs   ← port of screen incl. the _currentSettlement reflection swap; wraps reflection in IRemoteFiefSettlementSwapper
│   ├── FiefManagementNavItemVM.cs        ← port of nav-bar VM (F6 hotkey indicator)
│   └── (no .xml — DLL only loads the vanilla "TownManagement" movie)
├── FiefManagementIoC.cs
└── Hooks/
    ├── FiefHubCampaignBehavior.cs        ← OnSessionLaunched registers game menu "fief_hub" with carousel
    ├── Patch36_MapScreenF6.cs            ← Postfix on MapScreen.OnFrameTick; reads Input.IsKeyPressed(InputKey.F6); calls FiefHubService.OpenIfOwnsFiefs
    └── Patch36_GameStateScreenManager.cs ← Prefix on GameStateScreenManager.CreateScreen; substitutes GauntletFiefManagementScreen for FiefManagementGameState

Main/Adapters/
├── ISettlementOwnershipAdapter.cs        ← NEW — Settlement.All filtered by OwnerClan == Clan.PlayerClan, town-vs-castle classification
├── SettlementOwnershipAdapter.cs
├── IMapScreenInputAdapter.cs             ← NEW — wraps Input.IsKeyPressed(InputKey.F6) for testability
├── MapScreenInputAdapter.cs
├── IRemoteFiefSettlementSwapper.cs       ← NEW — wraps the MobileParty.MainParty._currentSettlement reflection swap
└── RemoteFiefSettlementSwapper.cs

TAOM.Tests/Features/FiefManagement/
├── FiefHubServiceTests.cs                ← carousel cycle wraparound, "no fiefs" path, sort order (towns before castles)
└── FiefManagementSettingsProviderTests.cs ← MCM passthrough, defaults
```

### Adapter usage

| Adapter | Source | Why |
|---|---|---|
| `ISettlementOwnershipAdapter` | NEW | `Settlement.All` is sealed; filter by `OwnerClan == Clan.PlayerClan` and classify (`IsTown`/`IsCastle`) inside the adapter. Service consumes `IReadOnlyList<FiefSummary>` (your DTO). |
| `IMapScreenInputAdapter` | NEW | Wraps `Input.IsKeyPressed(InputKey.F6)` so the F6 patch's hook is testable. |
| `IRemoteFiefSettlementSwapper` | NEW | Wraps the **`MobileParty.MainParty._currentSettlement`** reflection-based swap (a private field). The screen calls `swap → new TownManagementVM() → restore`. The reflection MUST be in the adapter, not the screen, so the screen is testable in isolation. **Verify the field name is still `_currentSettlement` in v1.3.15** before relying on it. |

### Harmony patches

Reserve **`Patch36_FiefManagement`**. Two patches under this category:

1. `MapScreen.OnFrameTick` (Postfix) — reads `Input.IsKeyPressed(InputKey.F6)`. If pressed AND player owns ≥1 fief, call `GameMenu.ActivateGameMenu("fief_hub")`. If owns 0 fiefs, display yellow message "You don't own any fiefs yet."
2. `GameStateScreenManager.CreateScreen` (Prefix) — when `state is FiefManagementGameState fmState`, set `__result = new GauntletFiefManagementScreen(fmState)` and return `false` (skip vanilla).

Wire in `Main/SubModule.cs` `OnGameInitializationFinished` (UI patches need View assembly):
```csharp
_harmony.PatchCategory("Patch36_FiefManagement");
```

### Game menu registration (CampaignBehavior, not Harmony)

`FiefHubCampaignBehavior` listens to `CampaignEvents.OnSessionLaunchedEvent` and registers menu `"fief_hub"` via `CampaignGameStarter.AddGameMenu/AddGameMenuOption`. Four options:
- `fief_hub_prev` — cycle to previous fief (enabled if count > 1)
- `fief_hub_next` — cycle to next fief (enabled if count > 1)
- `fief_hub_manage` — push `FiefManagementGameState` onto `Game.Current.GameStateManager`
- `fief_hub_leave` — `GameMenu.ExitToLast()`

Game menu title shows `"Fief Management — {SETTLEMENT_NAME} ({Town|Castle}) [X/Y]"`. Use `MenuOverlayType.Normal`.

### MCM settings — append to `Main/Features/TaomSettings.cs`

Group: `Fief Management`, GroupOrder = 25.

```csharp
[SettingPropertyGroup("Fief Management", GroupOrder = 25)]
[SettingPropertyBool("Enable Fief Management", Order = 0,
    HintText = "Master toggle. When off, F6 hotkey is inert and the fief_hub menu is not registered. Default: true.")]
public bool EnableFiefManagement { get; set; } = true;

[SettingPropertyGroup("Fief Management")]
[SettingPropertyBool("Allow Remote Building Queue", Order = 1,
    HintText = "When on, the manage-fief screen lets you add buildings to the construction queue from anywhere. When off, view-only — must visit the fief to queue. Default: true.")]
public bool AllowRemoteBuildingQueue { get; set; } = true;

[SettingPropertyGroup("Fief Management")]
[SettingPropertyBool("Fief Management Debug Mode", Order = 2,
    HintText = "Show diagnostic [FiefManagement] messages on the in-game HUD. Off = file log only.")]
public bool FiefManagementDebug { get; set; } = false;
```

### IoC registration

Add `using TAOM.Features.FiefManagement;` to `Main/IoC.cs`, then in `Configure()`:
```csharp
FiefManagementIoC.RegisterFiefManagementFeature(container);
```

`FiefManagementIoC.cs` registers (Reuse.Singleton):
- `IFiefManagementSettingsProvider → FiefManagementSettingsProvider`
- `ISettlementOwnershipAdapter → SettlementOwnershipAdapter`
- `IMapScreenInputAdapter → MapScreenInputAdapter`
- `IRemoteFiefSettlementSwapper → RemoteFiefSettlementSwapper`
- `IFiefHubService → FiefHubService`

### CampaignBehavior wiring in SubModule.cs

In `OnGameStart` after `RevoltTuningBehavior` (or similar campaign-behavior registrations):
```csharp
campaignStarter.AddBehavior(new FiefHubCampaignBehavior());
```
The behavior resolves `IFiefHubService` from IoC in its constructor.

## Cross-session memory rules that apply to THIS feature

| Memory | How it applies here |
|---|---|
| `feedback_substring_keyword_matches_external_data.md` | NOT APPLICABLE — feature uses no scene-name or substring matching. |
| `feedback_adapter_modifier_preserving_overload.md` | NOT APPLICABLE — no inventory/equipment APIs. |
| `feedback_user_facing_promise_must_match_code.md` | **APPLIES.** Trace each MCM setting (`EnableFiefManagement`, `AllowRemoteBuildingQueue`, debug) to the implementation. The `AllowRemoteBuildingQueue` setting in particular must be checked: does the actual screen consult this setting to gate the queue UI, or does it always show the queue regardless? If the setting is dead, either implement the gating (block the queue's "Add" button when off) or drop the setting. |

## Per-feature gotchas (from the decompiler agent's analysis)

1. **`MobileParty.MainParty._currentSettlement` reflection.** The screen swaps this private field to fool `TownManagementVM`. Verify the field name in v1.3.15 with `ilspycmd`:
   ```bash
   ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Party.MobileParty" 2>&1 | grep -i "currentSettlement"
   ```
   If the field has been renamed/removed, the feature breaks silently. The adapter must log `LogError` once at startup if the reflection target is null.

2. **Three orphaned XML prefab files.** `FiefHub.xml`, `FiefManagement.xml`, `FiefNavOverlay.xml` ship in the original module's `GUI/Prefabs/` but the DLL never loads them — the screen uses the vanilla `"TownManagement"` movie. **Drop all three.** Don't copy to `Main/_Module/GUI/Prefabs/`. Note this in the feature doc.

3. **F6 conflict check.** Verify TAOM doesn't bind F6 elsewhere:
   ```bash
   grep -rni "InputKey\.F6\|IsKeyDown.*F6\|IsKeyPressed.*F6" Main/ --include "*.cs"
   ```
   If anything matches outside the new FiefManagement files, change the default key OR coordinate.

4. **`Mission.IsSiegeBattle` interaction with SiegeDismount.** F6 fires only on the campaign map, so it can't interact with mission features. But verify: if the player is INSIDE a settlement (visiting), is `MapScreen.OnFrameTick` still called? If yes, F6 inside a settlement could trigger the hub menu, which might conflict with the vanilla "manage town" menu. Test in-game.

5. **Game menu registration on every campaign load.** `FiefHubCampaignBehavior.OnSessionLaunched` re-registers the menu every session. Verify there's no duplicate-registration crash if the user reloads the same save twice.

6. **`_selectedIndex` static state.** The decompiled `FiefHubCampaignBehavior` has a `static int _selectedIndex` — this leaks across save/load (the int doesn't reset when a new campaign starts). Fix: make it instance state on the behavior, OR clear it on `OnNewGameCreated`/`OnGameLoaded`.

## Verification of v1.3.15 API surface (do this BEFORE writing the patch)

```bash
# Verify MapScreen.OnFrameTick signature
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/SandBox.View.Map.dll" -t "SandBox.View.Map.MapScreen" 2>&1 | grep -A1 "OnFrameTick"

# Verify GameStateScreenManager.CreateScreen
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.GameStateScreenManager" 2>&1 | grep -A 2 "CreateScreen"

# Verify TownManagementVM constructor (no args; reads MainParty.CurrentSettlement)
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/SandBox.GauntletUI.dll" -t "SandBox.GauntletUI.TownManagementVM" 2>&1 | grep "public TownManagementVM\|protected TownManagementVM" -A 3

# Verify MobileParty._currentSettlement field (private)
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.Party.MobileParty" 2>&1 | grep -i "currentSettlement"

# Verify GameMenu API for AddGameMenu
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.GameMenus.GameMenu" 2>&1 | grep -E "AddGameMenu|ActivateGameMenu"
```

## Acceptance gates

- Build clean — 0 errors
- Tests: at least 12 tests covering: cycle wraparound (next/prev), no-fiefs path, sort order (towns before castles, alphabetical within), MCM toggle, settings provider passthrough
- Full suite stays green
- `docs/features/fief-management.md` from TEMPLATE; cite the orphaned-prefab note; cite the reflection target verification
- CHANGELOG.md entry at top
- `/deep-review FiefManagement` and `/review-codex FiefManagement` — fix every confirmed finding in same session
- New feedback memory if RCA produced one

**Do NOT commit** — leave dirty for in-game test.

## Verification — in-game golden path

1. Start a campaign and own ≥2 fiefs.
2. MCM → TAOM → "Fief Management" → confirm `Enable=true`.
3. On the campaign map, press F6 — `fief_hub` menu opens.
4. Title shows `Fief Management — {fief_1_name} (Town/Castle) [1/N]`.
5. Click "Next fief" — title updates to fief 2.
6. Click "Previous fief" — back to fief 1.
7. Click "Manage" — TownManagementVM screen opens for that specific fief (NOT the player's currently-visited settlement).
8. Verify prosperity/loyalty/security/garrison/income display the REMOTE fief's values, not the local settlement's.
9. Add a building to the queue — verify it persists when you close the screen and re-open.
10. Press Escape — back to campaign map.
11. Disable round-trip: set `Enable Fief Management = false`, reload — F6 does nothing.

## Final report format

When done, output:
```
FiefManagement port complete.
- Files created: [count] (services, adapters, hooks, screen + VM, tests, doc)
- Orphaned prefabs dropped (not copied): FiefHub.xml, FiefManagement.xml, FiefNavOverlay.xml
- Files modified: TaomSettings.cs, IoC.cs, SubModule.cs (Patch36 + behavior registration)
- Reflection target verified: MobileParty._currentSettlement (v1.3.15) — [present / renamed / not found]
- Tests: NN/NN FiefManagement tests pass; XXXX/XXXX total
- /deep-review verdict: [PASS / N findings fixed]
- /review-codex verdict: [PASS / N findings fixed]
- New feedback memories codified: [list]
- Awaiting in-game verification before commit.
```
