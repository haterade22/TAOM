# Codex Adversarial Review: FiefManagement (Patch36)

Date: 2026-05-06

## 1. VANILLA CODE — DECOMPILE AND PASTE

### a. `Settlement.CurrentSettlement` getter

Source: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Settlements\Settlement.cs:463-481`

```csharp
public static Settlement CurrentSettlement
{
	get
	{
		if (PlayerCaptivity.CaptorParty != null && PlayerCaptivity.CaptorParty.IsSettlement)
		{
			return PlayerCaptivity.CaptorParty.Settlement;
		}
		if (PlayerEncounter.EncounterSettlement != null)
		{
			return PlayerEncounter.EncounterSettlement;
		}
		if (MobileParty.MainParty.CurrentSettlement != null)
		{
			return MobileParty.MainParty.CurrentSettlement;
		}
		return null;
	}
}
```

Answer: the `_currentSettlement` swap is only effective when the captivity and encounter branches are null. Since `Patch36_MapScreenF6` only checks `ActiveState is MapState`, F6 can still run while a settlement/encounter menu is open; in that case this getter returns `PlayerEncounter.EncounterSettlement` before it ever reaches the swapped `MobileParty.MainParty.CurrentSettlement`.

### b. `TownManagementVM` constructor body

Source: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem.ViewModelCollection\TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.TownManagement\TownManagementVM.cs:573-618`

```csharp
public TownManagementVM()
{
	_settlement = Settlement.CurrentSettlement;
	if (_settlement?.Town == null)
	{
		Debug.FailedAssert("Town management initialized with null settlement and/or town!", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.CampaignSystem.ViewModelCollection\\GameMenu\\TownManagement\\TownManagementVM.cs", ".ctor", 27);
		Debug.Print("Town management initialized with null settlement and/or town!");
	}
	ProjectSelection = new SettlementProjectSelectionVM(_settlement, OnChangeInBuildingQueue);
	GovernorSelection = new SettlementGovernorSelectionVM(_settlement, OnGovernorSelectionDone);
	ReserveControl = new TownManagementReserveControlVM(_settlement, OnReserveUpdated);
	MiddleFirstTextList = new MBBindingList<TownManagementDescriptionItemVM>();
	MiddleSecondTextList = new MBBindingList<TownManagementDescriptionItemVM>();
	Shops = new MBBindingList<TownManagementShopItemVM>();
	Villages = new MBBindingList<TownManagementVillageItemVM>();
	Show = false;
	IsTown = _settlement.IsTown;
	IsThereCurrentProject = _settlement.Town.CurrentBuilding != null;
	CurrentGovernor = new HeroVM(_settlement.Town.Governor ?? CampaignUIHelper.GetTeleportingGovernor(_settlement, Campaign.Current.GetCampaignBehavior<ITeleportationCampaignBehavior>()), useCivilian: true);
	if (CurrentGovernor.Hero != null)
	{
		CurrentGovernorTooltip = new BasicTooltipViewModel(() => CampaignUIHelper.GetHeroGovernorEffectsTooltip(CurrentGovernor.Hero, _settlement));
	}
	else
	{
		CurrentGovernorTooltip = new BasicTooltipViewModel(() => GetAssignGovernorTooltip());
	}
	UpdateGovernorSelectionProperties();
	RefreshCurrentDevelopment();
	RefreshTownManagementStats();
	Workshop[] workshops = _settlement.Town.Workshops;
	foreach (Workshop workshop in workshops)
	{
		WorkshopType workshopType = workshop.WorkshopType;
		if (workshopType != null && !workshopType.IsHidden)
		{
			Shops.Add(new TownManagementShopItemVM(workshop));
		}
	}
	foreach (Village boundVillage in _settlement.BoundVillages)
	{
		Villages.Add(new TownManagementVillageItemVM(boundVillage));
	}
	ConsumptionTooltip = new BasicTooltipViewModel(() => CampaignUIHelper.GetSettlementConsumptionTooltip(_settlement));
	RefreshValues();
}
```

Answer: the constructor is covered by the swap only in the clean map case, but restoring immediately after construction is not enough. Vanilla `TownManagementReserveControlVM.ExecuteConfirm()` later reads `Settlement.CurrentSettlement.Town`, after TAOM has restored the field.

Additional vanilla evidence: `TownManagementReserveControlVM.cs:205-214`

```csharp
public void ExecuteConfirm()
{
	IsEnabled = false;
	BuildingHelper.BoostBuildingProcessWithGold(CurrentReserveAmount + CurrentGivenAmount, Settlement.CurrentSettlement.Town);
	CurrentGivenAmount = 0;
	GameTexts.SetVariable("GOLD_ICON", "{=!}<img src=\"General\\Icons\\Coin@2x\" extend=\"6\">");
	UpdateReserveText();
	MaxReserveAmount = TaleWorlds.Library.MathF.Min(Hero.MainHero.Gold, 10000);
	CurrentReserveAmount = Settlement.CurrentSettlement.Town.BoostBuildingProcess;
	_onReserveUpdated?.Invoke();
}
```

### c. `GameStateScreenManager.CreateScreen`

Source: `E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.View\TaleWorlds.MountAndBlade.View.Screens\GameStateScreenManager.cs:88-110`

```csharp
public ScreenBase CreateScreen(GameState state)
{
	Type type = null;
	if (_screenTypes.TryGetValue(((object)state).GetType(), out var value))
	{
		MBList<Assembly> activeGameAssemblies = ModuleHelper.GetActiveGameAssemblies();
		for (int num = ((List<Type>)(object)value).Count - 1; num >= 0; num--)
		{
			if (((List<Assembly>)(object)activeGameAssemblies).Contains(((List<Type>)(object)value)[num].Assembly))
			{
				type = ((List<Type>)(object)value)[num];
				break;
			}
		}
		if (type != null)
		{
			object? obj = Activator.CreateInstance(type, state);
			return (ScreenBase)((obj is ScreenBase) ? obj : null);
		}
		Debug.FailedAssert($"Failed to create game state screen for state: {((object)state).GetType()}", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade.View\\Screens\\GameStateScreenManager.cs", "CreateScreen", 108);
	}
	return null;
}
```

Answer: the target exists, but the Manage path never reaches it because TAOM directly calls `PushState(new FiefManagementGameState(...))` instead of creating/registering the state first.

### d. `MapScreen.OnFrameTick` signature/body

Source: `E:\Decompiled_Bannerlord\Modules\SandBox.View\SandBox.View.Map\MapScreen.cs:1332-1373`

```csharp
protected override void OnFrameTick(float dt)
{
	((ScreenBase)this).OnFrameTick(dt);
	MBDebug.SetErrorReportScene(MapScene);
	UpdateMenuView();
	TextObject val = default(TextObject);
	if (IsInMenu)
	{
		_menuViewContext.OnFrameTick(dt);
		if (((ScreenLayer)SceneLayer).Input.IsGameKeyPressed(4))
		{
			GameMenuOption leaveMenuOption = Campaign.Current.GameMenuManager.GetLeaveMenuOption(_menuViewContext.MenuContext);
			if (leaveMenuOption != null)
			{
				UISoundsHelper.PlayUISound("event:/ui/default");
				if (_menuViewContext.MenuContext.GameMenu.IsWaitMenu)
				{
					_menuViewContext.MenuContext.GameMenu.EndWait();
				}
				leaveMenuOption.RunConsequence(_menuViewContext.MenuContext);
			}
		}
	}
	else if (Campaign.Current != null && !IsInBattleSimulation && !IsInArmyManagement && !IsMarriageOfferPopupActive && !IsHeirSelectionPopupActive && !IsMapCheatsActive && !IsMapIncidentActive && !IsOverlayContextMenuEnabled && !EncyclopediaScreenManager.IsEncyclopediaOpen && CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(ref val))
	{
		Kingdom kingdom = Clan.PlayerClan.Kingdom;
		if (((kingdom == null) ? null : ((IEnumerable<KingdomDecision>)kingdom.UnresolvedDecisions)?.FirstOrDefault((KingdomDecision d) => d.NeedsPlayerResolution && !d.ShouldBeCancelled())) != null)
		{
			OpenKingdom();
		}
	}
	if (_partyIconNeedsRefreshing)
	{
		_partyIconNeedsRefreshing = false;
		PartyBase.MainParty.SetVisualAsDirty();
	}
	_mapViewsContainer.ForeachReverse(delegate(MapView view)
	{
		view.OnMapScreenUpdate(dt);
	});
	SandBoxViewVisualManager.OnFrameTick(Campaign.Current.CampaignDt);
}
```

Answer: the patch target exists and a postfix may omit `dt`. This body also proves `OnFrameTick` runs while `IsInMenu`, so the current `MapState` guard is not enough to mean "campaign map only".

### e. `MobileParty._currentSettlement` field declaration

Source: `E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Party\MobileParty.cs:81-84`

```csharp
private const int ArrayLength = 6;

[SaveableField(1001)]
private Settlement _currentSettlement;
```

Answer: the field is still `[SaveableField(1001)] private Settlement _currentSettlement` in v1.3.15, so the reflection target name/type are valid.

## 2. RECURSIVE PUSH SCENARIO

Vanilla `CreateState` sets `GameStateManager` and calls `OnCreateState`; direct `PushState` does not:

```csharp
public T CreateState<T>(params object[] parameters) where T : GameState, new()
{
	GameState gameState = (GameState)Activator.CreateInstance(typeof(T), parameters);
	HandleCreateState(gameState);
	return (T)gameState;
}

private void HandleCreateState(GameState state)
{
	state.GameStateManager = this;
	foreach (IGameStateManagerListener listener in _listeners)
	{
		listener.OnCreateState(state);
	}
}
```

```csharp
public void PushState(GameState gameState, int level = 0)
{
	GameStateJob item = new GameStateJob(GameStateJob.JobType.Push, gameState, level);
	_gameStateJobs.Enqueue(item);
	DoGameStateJobs();
}
```

```csharp
void IGameStateManagerListener.OnCreateState(GameState gameState)
{
	ScreenBase val = CreateScreen(gameState);
	if (val == null)
	{
		Debug.FailedAssert($"Create screen for {((MBObjectBase)gameState).GetName()} returned null.", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade.View\\Screens\\GameStateScreenManager.cs", "OnCreateState", 145);
	}
	gameState.RegisterListener((IGameStateListener)(object)((val is IGameStateListener) ? val : null));
}
```

Trace: `FiefHubCampaignBehavior.cs:124` constructs a raw `FiefManagementGameState` and passes it to `PushState`. That skips `HandleCreateState`, so `GameStateManager` is never assigned and `GameStateScreenManager.OnCreateState` never calls `CreateScreen`. `GameStateScreenManager.OnPushState` only pushes an already-registered `ScreenBase` listener; none exists. `PushState` also has no same-type guard and inserts by `Level`, so once the creation path is fixed, double-click stacking still needs a guard.

## 3. CONCURRENT FIEF LOSS

Read `FiefHubCampaignBehavior.cs:90-180`. `OnMenuInit` refreshes `_menuFiefs = _service.GetOrderedFiefs()` and clamps `_selectedIndex`, so reopening after B is lost turns `[A, B, C]` index 1 into `[A, C]` index 1 (C). The stale B reference only persists while the same menu activation remains open; normal `GameMenu.ActivateGameMenu` stops campaign time, so AI conquest during the open menu is not a normal tick path. If scripted/other-mod ownership changes can happen while the menu is open, the Manage consequence at lines 122-124 should revalidate ownership before pushing.

## 4. REFLECTION SWAP UNDER MULTI-THREAD

`GauntletFiefManagementScreen.OnInitialize` swaps, synchronously constructs `TownManagementVM`, and restores in `finally`; I found no yield/await/task boundary in that swap window. Under normal campaign/UI execution this is atomic with respect to campaign ticks. The current design avoids leaving a global `MobileParty` field changed, but it also breaks later vanilla VM methods that read `Settlement.CurrentSettlement` after construction.

## 5. CONFIG CROSS-REFERENCE

| Setting | Consumers |
|---|---|
| `EnableFiefManagement` | `FiefManagementSettingsProvider.cs:5`; `FiefHubCampaignBehavior.cs:58`; `Patch36_MapScreenF6.cs:30` |
| `AllowRemoteBuildingQueue` | `FiefManagementSettingsProvider.cs:6`; `FiefHubCampaignBehavior.cs:113` |
| `FiefManagementDebug` | `FiefManagementSettingsProvider.cs:7` as `IsDebugMode`; `Patch36_MapScreenF6.cs:47`; `Patch36_MapScreenF6.cs:52` |

No setting has zero consumers.

Hint comparison:

- `EnableFiefManagement`: mismatch for runtime toggles. If false at `OnSessionLaunched`, `fief_hub` is not registered; if enabled later, F6 can still call `GameMenu.ActivateGameMenu("fief_hub")` against a missing menu.
- `AllowRemoteBuildingQueue`: option gate matches the hint, but actual remote management is currently blocked by the state-creation bug and reserve-control `CurrentSettlement` bug.
- `FiefManagementDebug`: mismatch. Hint promises HUD messages, but consumers only call `IModLogger`; `FileLogger` writes to `Logs/taom_debug_*.log`.

## Known suspects

1. Reflection target lifecycle: partially confirmed. Getter falls through to `MobileParty.MainParty.CurrentSettlement`, but only after captivity/encounter branches; later vanilla child VMs also read `Settlement.CurrentSettlement` after restore.
2. F6 polling exception swallowing: confirmed but not elevated; `_exceptionLogged` is static and not reset on session reload, so later distinct F6 errors in the same process are suppressed.
3. Game state push without recursion guard: confirmed and worse; direct `new` means screen creation never happens.
4. `_selectedIndex` between save and clamp: mostly disputed for normal reopen; `OnMenuInit` refreshes and clamps.
5. PopState pairing on screen close: superseded by the no-screen finding; if creation is fixed, initialization failures still need fail-safe pop/cleanup.
6. Cached menu snapshot vs concurrent campaign events: observation only; stale within one open menu if ownership mutates externally.
7. Sort stability and case insensitivity: confirmed cosmetic only; equal names can be unstable.
8. F6 enabled but menu unregistered: confirmed.

## 6. FINDINGS OR OBSERVATIONS

| # | Severity | File:line | Issue | Fix |
|---|---|---|---|---|
| 1 | P1 | `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs:124` | `PushState(new FiefManagementGameState(...))` bypasses `GameStateManager.CreateState`, so `GameStateScreenManager.CreateScreen` never runs, the Patch36 prefix never substitutes the screen, no `ScreenBase` listener is registered, and the pushed state has no `GameStateManager`. | Create/register the state through the vanilla creation path or manually perform the same registration before pushing; add a same-state guard after that. |
| 2 | P1 | `Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs:44-52` | The swap is restored immediately after the constructor, but vanilla reserve confirmation later uses `Settlement.CurrentSettlement.Town`; remote reserve confirmation can dereference null or target the wrong settlement. | Patch/proxy the later vanilla consumers or provide a narrow target-settlement context for those interactions without leaving the global field swapped indefinitely. |
| 3 | P2 | `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs:34` | `ActiveState is MapState` does not exclude settlement/game menus; `MapScreen.OnFrameTick` runs in menus, and `Settlement.CurrentSettlement` prioritizes `PlayerEncounter.EncounterSettlement` over the swapped `MobileParty` field. | Also require no current menu/encounter context, or accept `MapScreen __instance` and check `!__instance.IsInMenu`. |
| 4 | P2 | `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs:58` | Menu registration is gated only at session launch while the F6 patch checks the setting live, so runtime re-enable can activate an unregistered `fief_hub`. | Register the menu unconditionally and gate runtime access/options, or reliably register/check before activation. |
| 5 | P2 | `Main/Features/FiefManagement/Models/FiefSummary.cs:7` | `FiefSummary` carries raw sealed `Settlement` through the adapter/service boundary, violating ADR-007 and leaving the important payload unmocked in service tests. | Keep service DTOs to IDs/testable data and resolve/adapt the `Settlement` at the campaign/screen boundary. |
| 6 | P2 | `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs:11` | The CampaignBehavior is 183 lines and owns menu registration, cursor state, cached snapshots, option conditions, and consequences, violating the <150-line thin entry-point rule. | Extract menu state/option logic into a service or presenter. |
| 7 | P3 | `Main/Features/TaomSettings.cs:285-286` | The debug hint promises HUD diagnostics, but the only debug consumers write to `IModLogger`/file. | Change the hint or display debug diagnostics via `InformationManager.DisplayMessage`. |

Quality gates: decompiled every requested vanilla target; read `FiefHubCampaignBehavior.cs` fully; traced all 8 known suspects; cross-referenced all 3 MCM settings.

Summary: P1: 2 | P2: 4 | P3: 1

VERDICT: ISSUES FOUND
