# CompanionTactics Adversarial Review

Date: 2026-05-06  
Reviewer: Codex  
TAOM target version: Bannerlord 1.3.15  
Scope: CompanionTactics port, Patch35, including CompanionRoles, FormationPresets, BattleActionBar, tests, and GUI prefabs.

## Executive Summary

Severity counts: P1: 0 | P2: 3 | P3: 1 | P4: 2 | OBS: 1

Top risks:

1. FormationPresets does not actually capture, load, or auto-assign Order of Battle hero assignments. The UI can create named empty presets and delete them, but "load" and "assign" are notification-only paths.
2. Formation preset persistence is unsafe even after the UI is fixed: the save path calls `SyncData` before copying current service state into the ref buffer, so the engine records the previous snapshot; the singleton service also has no new-campaign reset.
3. BattleActionBar stance state and the visible active indicators diverge. `CancelStanceOnMove` clears the manager, but the VM button remains active and the selected-formation refresh short-circuits for the same formation.

## Known Suspect Verdicts

1. DISPUTED - SaveableTypeDefiner BaseId 726900601 collision risk.

TAOM-wide BaseId collision is not present. `FormationPresetSaveableTypeDefiner` uses BaseId `726900601` and class id `101` at `Main\Features\CompanionTactics\FormationPresets\Models\FormationPresetSaveableTypeDefiner.cs:18-22`; EquipPresets uses `726900501`, so the two ranges are distinct. The definer registers the required containers at lines 27-30. `FormationPresetCampaignBehavior.SyncData` catches exceptions and resets only CompanionTactics presets at lines 47-52, so a deserialization fault should not take down other features. However, this review found a separate P2 save-order bug in the same behavior.

2. DISPUTED - OOBOverlayService reflection on `_dataSource` and `_isActive`.

v1.3.15 has both fields in `MissionGauntletOrderOfBattleUIHandler` (see appendix). `OOBOverlayService.EnsureInitialized` checks both `AccessTools.Field` results at `Main\Features\CompanionTactics\FormationPresets\OOBOverlayService.cs:57-64`; if either is missing, `_inertMode = true` and subsequent `OnTick` returns immediately at lines 66-69. `Detach` is null-safe at lines 87-99.

3. CONFIRMED - CompanionRoleService cache leak.

`CompanionRoleService` stores `_cache` as `Dictionary<string, (long sig, CombatRole role)>` at `Main\Features\CompanionTactics\Roles\CompanionRoleService.cs:18` and only ever adds/updates at lines 28-32. There is no clear path on game load, new campaign, hero death, or role-service lifecycle reset. The leak is bounded by distinct hero StringIds encountered in the process, so this is P4 rather than P2.

4. DISPUTED - `Patch35_Mission_OnTick` hot-path allocations.

The patch body is allocation-free after first settings resolution: it caches `_settings`, reads `EnableFormationPresets`, and intentionally does no per-tick service work at `Main\Features\CompanionTactics\FormationPresets\Hooks\Patch35_Mission_OnTick.cs:19-33`. No LINQ, foreach, closures, or heap collection creation are present in the hot path.

5. DISPUTED - `MultiSelectionInquiryData` parameter order.

The v1.3.15 constructor is `(titleText, descriptionText, inquiryElements, isExitShown, minSelectableOptionCount, maxSelectableOptionCount, ...)` (see appendix). `OOBButtonsVM` uses named arguments at `Main\Features\CompanionTactics\FormationPresets\UI\OOBButtonsVM.cs:112-122`, so the call is safe despite the older template's positional-order drift.

6. DISPUTED - `TextInquiryData` ctor signature drift.

The v1.3.15 names match the call: `isAffirmativeOptionShown`, `isNegativeOptionShown`, `affirmativeText`, `negativeText`, `affirmativeAction`, `negativeAction`, `shouldInputBeObfuscated` (see appendix). `OOBButtonsVM.ShowSavePrompt` uses those names at `Main\Features\CompanionTactics\FormationPresets\UI\OOBButtonsVM.cs:156-165`.

7. DISPUTED - `GauntletLayer` ctor parameter order.

v1.3.15 signature is `GauntletLayer(string name, int localOrder, bool shouldClear = false)` (see appendix). `OOBOverlayService.Attach` uses `new GauntletLayer("GauntletLayer", 200, false)` at `Main\Features\CompanionTactics\FormationPresets\OOBOverlayService.cs:110`; `BattleActionBarMissionView.OnMissionScreenInitialize` uses `new GauntletLayer("GauntletLayer", 100, false)` at `Main\Features\CompanionTactics\BattleActionBar\Hooks\BattleActionBarMissionView.cs:54`.

8. DISPUTED - Manual `GetCaptainTooltip` patch wiring structure.

The target method is private in v1.3.15 and returns `List<TooltipProperty>` (see appendix). The patch method signature is structurally correct at `Main\Features\CompanionTactics\Roles\Hooks\Patch35_OOBHeroItem_GetCaptainTooltip.cs:20`: `Postfix(OrderOfBattleHeroItemVM __instance, ref List<TooltipProperty> __result)`. The runtime wiring is still commented in `Main\SubModule.cs:427-437`; that is reported as OBS-1, not a defect in the patch class.

## Deep Analysis Coverage

A. SaveableType safety: BaseId is TAOM-unique and required containers exist. Findings: P2 save-order bug and P2 missing new-campaign reset.

B. Hot-path allocation audit: no finding for `Patch35_Mission_OnTick`; it is a near no-op after the cached settings provider.

C. Reflection robustness: no finding. `_inertMode` disables subsequent ticks, and `Detach` is null-safe.

D. CombatRole color coverage: no finding for the recent `OneHanded` and `Slinger` color fix; all enum roles now have explicit non-white cases except `Unknown`.

E. State leak audit: P4 cache leak in `CompanionRoleService`; `TroopStanceManager` clears on mission finalization; `FormationAdapter` cache is instance-scoped; no `_currentPreset` exists in `FormationPresetService`.

F. OOB `_wasActive` state machine: no finding. `Detach` only fires on `true -> false`; `false` initial state and closed state do not collide.

G. UI binding completeness: no binding-name finding. `OOBButtonsOverlay.xml` resolves to `OOBButtonsVM` properties/methods, and `BattleActionBar.xml` resolves to `BattleActionBarVM` / `ActionButtonVM` members. Behavioral issues are in the command bodies, not missing bindings.

H. Adapter boundary verification: no sealed TaleWorlds types cross the core service interfaces listed in the prompt. Boundary decorators, VMs, patches, MissionView, and overlay code do use TaleWorlds VMs/engine types.

## Findings

### P2 - FormationPresets UI does not capture, apply, or auto-assign OOB assignments

File: `Main\Features\CompanionTactics\FormationPresets\UI\OOBButtonsVM.cs:73`

```csharp
public void ExecuteAssignCharacters()
{
    var vm = _vmTracker.Current;
    if (vm == null)
    {
        DisplayMessage("No Order of Battle screen detected.", Colors.Red);
        return;
    }
    // Show user a notification - the heavy auto-assign reflection lives in HeroAutoAssigner
    // (service layer). For Phase 1 we surface the intent here; the actual VM mutation is
    // hooked up by the dispatching session's preset apply step.
    DisplayMessage("Auto-assign requested. Hero scoring runs against the current OOB.", Colors.Yellow);
}
```

File: `Main\Features\CompanionTactics\FormationPresets\UI\OOBButtonsVM.cs:133`

```csharp
if (id.StartsWith("load:"))
{
    var presetId = id.Substring("load:".Length);
    var preset = _presetService.GetPresetById(presetId);
    if (preset != null)
        DisplayMessage($"Preset \"{preset.Name}\" selected for load.", Colors.Yellow);
    UpdatePresetsButtonText();
    return;
}
```

File: `Main\Features\CompanionTactics\FormationPresets\UI\OOBButtonsVM.cs:168`

```csharp
private void SaveCurrent(string name, OrderOfBattleVM vm)
{
    if (string.IsNullOrWhiteSpace(name))
    {
        DisplayMessage("Preset name cannot be empty.", Colors.Red);
        return;
    }
    var preset = new HoNFormationPreset(name.Trim());
    var result = _presetService.SavePreset(preset);
```

Why it is wrong: the feature promise is "save/load named OOB hero-to-formation assignments" and an "Assign Heroes" command. The current command bodies never read assignments from `OrderOfBattleVM`, never populate `HeroFormationAssignments`, `CaptainHeroIds`, or `FormationClasses`, never apply a saved preset back to the OOB VM, and never call `HeroAutoAssigner`. The saved object is a name-only `HoNFormationPreset`, so persisted presets are semantically empty.

Suggested fix or verification step: implement a boundary adapter for `OrderOfBattleVM` capture/apply, wire `ExecuteAssignCharacters` to actual assignment mutation or remove the button, and add UI-level tests that prove a populated VM produces a populated `HoNFormationPreset` and that loading mutates the VM. Current tests only prove `FormationPresetService` can store a manually populated POCO.

### P2 - Formation preset save path serializes the previous snapshot

File: `Main\Features\CompanionTactics\FormationPresets\Hooks\FormationPresetCampaignBehavior.cs:32`

```csharp
public override void SyncData(IDataStore dataStore)
{
    try
    {
        dataStore.SyncData("TAOM_FormationPresets", ref _savedPresets);

        if (dataStore.IsLoading)
        {
            _service.OnGameLoaded(_savedPresets ?? new List<HoNFormationPreset>());
        }
        else if (dataStore.IsSaving)
        {
            _savedPresets = _service.GetPresetsForSaving();
        }
```

Vanilla save contract, decompiled from `CampaignBehaviorDataStore`:

```csharp
public bool SyncData<T>(string key, ref T data)
{
    if (IsSaving)
    {
        _records.Add(key, data);
        return true;
    }
    if (_records.TryGetValue(key, out var value))
    {
        data = (T)value;
        return true;
    }
    return false;
}
```

Why it is wrong: on save, TaleWorlds records the current ref value inside `SyncData`. CompanionTactics updates `_savedPresets` only after that call, so the save file receives the previous buffer. On the first save after creating presets, the buffer is still the loaded/default list, so the new presets are not written until a later save. This contradicts any passing service tests because those tests do not execute `FormationPresetCampaignBehavior.SyncData` against the real save contract.

Suggested fix or verification step: branch before `SyncData`: if saving, copy `_service.GetPresetsForSaving()` into a local `snapshot` and call `SyncData` with that local; if loading, initialize a local null/list, call `SyncData`, then pass the result into `OnGameLoaded`. Add a unit test with a fake `IDataStore` that records the exact object passed during saving.

### P2 - FormationPresetService singleton is not reset for new campaigns in the same process

File: `Main\Features\CompanionTactics\CompanionTacticsIoC.cs:25`

```csharp
// FormationPresets
container.Register<IFormationPresetService, FormationPresetService>(Reuse.Singleton);
container.Register<IHeroAutoAssigner, HeroAutoAssigner>(Reuse.Singleton);
container.Register<IOrderOfBattleVMTracker, OrderOfBattleVMTracker>(Reuse.Singleton);
container.Register<IOOBOverlayService, OOBOverlayService>(Reuse.Singleton);
```

File: `Main\Features\CompanionTactics\FormationPresets\Hooks\FormationPresetCampaignBehavior.cs:27`

```csharp
public override void RegisterEvents()
{
    // No events needed - load/save state is fully driven by SyncData callbacks below.
}

public override void SyncData(IDataStore dataStore)
{
```

Why it is wrong: TAOM registers the preset service as a DryIoc singleton, so its `_presets` list can survive across campaign sessions in the same Bannerlord process. `SyncData(IsLoading)` replaces it on save load, but a new campaign has no load snapshot to force a reset. EquipPresets already handles this pattern with `OnNewGameCreatedEvent`; CompanionTactics does not. The root cause is treating save/load as the only lifecycle when the service lifetime is process-scoped.

Suggested fix or verification step: register `CampaignEvents.OnNewGameCreatedEvent` and call `_service.OnGameLoaded(null)` or a dedicated `Clear()` method. Add a lifecycle test that seeds presets, simulates a new campaign, and asserts the list is empty.

### P3 - BattleActionBar stance indicators are local toggles, not a view of stance state

File: `Main\Features\CompanionTactics\BattleActionBar\UI\ActionButtonVM.cs:50`

```csharp
public void ExecuteAction()
{
    _executeAction?.Invoke();
    IsActive = !IsActive;
}
```

File: `Main\Features\CompanionTactics\BattleActionBar\Hooks\Patch35_Formation_SetMovementOrder.cs:26`

```csharp
_settings ??= IoC.Resolve<ICompanionTacticsSettingsProvider>();
if (_settings == null || !_settings.CancelStanceOnMove) return;
if (__instance == null) return;

_stances ??= IoC.Resolve<ITroopStanceManager>();
_stances?.ClearStance((int)__instance.FormationIndex);
```

File: `Main\Features\CompanionTactics\BattleActionBar\Hooks\BattleActionBarMissionView.cs:100`

```csharp
private void RefreshFromSelectedFormation()
{
    var team = Mission.Current?.PlayerTeam;
    var orderController = team?.PlayerOrderController;
    var selected = orderController?.SelectedFormations;
    var formation = (selected != null && selected.Count > 0) ? selected[0] : null;

    if (formation == _lastFormation) return;
    _lastFormation = formation;
```

Why it is wrong: the UI highlight is stored only on each `ActionButtonVM`. `TroopStanceManager` can replace a stance or clear it on movement, but no property notification reaches existing buttons. The same-formation short-circuit prevents the twice-per-second refresh from rebuilding buttons after a movement order, casualty-driven composition change, or MCM toggle. The result is a visible stale active indicator, directly contradicting the golden path "order the formation to move -> highlight clears." Tests miss this because they exercise `TroopStanceManager` and `BattleActionBarService` in isolation, not MissionView + VM synchronization.

Suggested fix or verification step: make button active state derive from `ITroopStanceManager.GetStance(formationIndex)` on every refresh, refresh the selected formation even when the reference is unchanged, and/or raise an event when `ClearStance` runs. Add a test around `BattleActionBarVM` or MissionView that executes a stance action, clears the stance, and asserts the active indicator clears.

### P4 - CompanionRoleService cache has no lifecycle clear

File: `Main\Features\CompanionTactics\Roles\CompanionRoleService.cs:18`

```csharp
private readonly Dictionary<string, (long sig, CombatRole role)> _cache = new();

public CombatRole GetPrimaryRole(IHeroCombatAdapter hero)
{
    if (hero == null) return CombatRole.Unknown;
    var equipment = hero.Equipment;
    if (equipment == null) return CombatRole.Unknown;

    var sig = ComputeSignature(equipment, hero.HasMount);
    var id = hero.StringId ?? string.Empty;
    if (_cache.TryGetValue(id, out var cached) && cached.sig == sig)
        return cached.role;

    var role = ComputeRole(equipment, hero.HasMount);
    _cache[id] = (sig, role);
```

Why it is wrong: the cache is process-lifetime through `Reuse.Singleton` and has no eviction or reset. Killed/disabled heroes and previous-campaign heroes remain as keys. Equipment changes are handled by the signature, so stale values are unlikely; the issue is memory/state retention, not role correctness. The practical impact is bounded by the number of distinct hero IDs observed, so P4 is appropriate.

Suggested fix or verification step: add a `ClearCache()` method and invoke it on game load/new game, or document the bounded cache explicitly if the team accepts the process-lifetime retention.

### P4 - WeaponClass coverage omits real enum values without diagnostics

File: `Main\Features\CompanionTactics\Roles\CompanionRoleService.cs:120`

```csharp
private static CombatRole ClassifyWeapon(WeaponClass wc, bool hasShield) => wc switch
{
    WeaponClass.Bow => CombatRole.Archer,
    WeaponClass.Crossbow => CombatRole.Crossbow,
    WeaponClass.Sling => CombatRole.Slinger,
    WeaponClass.Stone or WeaponClass.ThrowingAxe or WeaponClass.ThrowingKnife or WeaponClass.Javelin
        => CombatRole.Skirmisher,
    WeaponClass.TwoHandedSword or WeaponClass.TwoHandedAxe or WeaponClass.TwoHandedMace
        => CombatRole.TwoHanded,
    WeaponClass.OneHandedPolearm or WeaponClass.TwoHandedPolearm or WeaponClass.LowGripPolearm
        => CombatRole.Polearm,
    WeaponClass.OneHandedSword or WeaponClass.OneHandedAxe or WeaponClass.Mace or WeaponClass.Dagger
        => hasShield ? CombatRole.ShieldInfantry : CombatRole.OneHanded,
    _ => CombatRole.Unknown,
};
```

Relevant v1.3.15 enum values include `Pick`, `Boulder`, `Pistol`, `Musket`, `SmallShield`, `LargeShield`, `Banner`, and ammo classes. Some are reasonably ignorable, but `Pick` is a melee weapon class and currently becomes `Unknown`; there is also no debug warning for unmapped non-ammo classes. This violates the port prompt's weapon-class coverage gate and can silently hide a role label for valid loadouts. The current tests cover the mapped happy paths but not unmapped enum values.

Suggested fix or verification step: explicitly classify `Pick` or document it as unsupported; add a diagnostic path for unexpected non-ammo weapon classes. Add a table-driven test over `WeaponClass` values to prove every non-ammo/non-shield class has an intentional outcome.

### OBS - CompanionTactics integration is still partly auto-commented

File: `Main\IoC.cs:90`

```csharp
// TEMP-SMARTCAVALRY-EXCLUDE: CompanionTactics parallel-port has compile errors; restore when ready.
// CompanionTacticsIoC.RegisterCompanionTacticsFeature(container);
FiefManagementIoC.RegisterFiefManagementFeature(container);
```

File: `Main\SubModule.cs:427`

```csharp
// TEMP-SMARTCAVALRY-EXCLUDE: CompanionTactics parallel-port has compile errors; restore when ready.
// CompanionTactics - manual patch for the PRIVATE method
// OrderOfBattleHeroItemVM.GetCaptainTooltip (cannot use [HarmonyPatch] attribute
// binding since the method is private in v1.3.15).
// var captainTooltipTarget = AccessTools.Method(typeof(OrderOfBattleHeroItemVM), "GetCaptainTooltip");
// if (captainTooltipTarget != null)
//     _harmony.Patch(captainTooltipTarget, postfix: new HarmonyMethod(
```

File: `Main\SubModule.cs:501`

```csharp
// TEMP-SMARTCAVALRY-EXCLUDE: CompanionTactics parallel-port has compile errors; restore when ready.
// mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());
```

Why it matters: this matches the prompt's parallel-port note and is not treated as a code-design defect, but it means the reviewed implementation is not fully active in-game. `Patch35_CompanionTactics` is still patched at `Main\SubModule.cs:423`, but IoC registration, persistence behavior, manual private-method patching, and MissionView registration are commented.

Suggested fix or verification step: restore the integration once the parallel-port hook stops commenting it, then run a build and an in-game smoke test. This report did not run builds/tests per the action-safety instruction.

## Vanilla Signature Appendix

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.ViewModelCollection.dll" -t "TaleWorlds.CampaignSystem.ViewModelCollection.Party.PartyCharacterVM"
```

```csharp
public override void RefreshValues()
{
    base.RefreshValues();
    Name = Troop.Character.Name.ToString();
    LockHint = new HintViewModel(GameTexts.FindText("str_inventory_lock"));
    Upgrades?.ApplyActionOnAllItems(delegate(UpgradeTargetVM x)
    {
        x.RefreshValues();
    });
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.ViewModelCollection.dll" -t "TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM"
```

```csharp
public readonly Agent Agent;

private List<TooltipProperty> _cachedTooltipProperties;

public override void RefreshValues()
{
    _cachedTooltipProperties = GetAgentTooltip?.Invoke(Agent);
}

private List<TooltipProperty> GetCaptainTooltip()
{
    return _cachedTooltipProperties;
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.ViewModelCollection.dll" -t "TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleVM"
```

```csharp
public OrderOfBattleVM()
{
    _allFormations = new List<OrderOfBattleFormationItemVM>();
    _allHeroes = new List<OrderOfBattleHeroItemVM>();
    _selectedHeroes = new List<OrderOfBattleHeroItemVM>();
    Game.Current.EventManager.RegisterEvent<TutorialNotificationElementChangeEvent>(OnTutorialNotificationElementIDChange);
    RefreshValues();
}

public override void OnFinalize()
{
    base.OnFinalize();
    Game.Current.EventManager.UnregisterEvent<TutorialNotificationElementChangeEvent>(OnTutorialNotificationElementIDChange);
    FinalizeFormationCallbacks();
    DoneInputKey?.OnFinalize();
    ResetInputKey?.OnFinalize();
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll" -t "TaleWorlds.MountAndBlade.GauntletUI.Mission.Singleplayer.MissionGauntletOrderOfBattleUIHandler"
```

Note: the prompt's root `bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.GauntletUI.dll` path does not exist in this install; the v1.3.15 type is in the Native module DLL above.

```csharp
public class MissionGauntletOrderOfBattleUIHandler : MissionView
{
    private OrderOfBattleVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _movie;
    private SpriteCategory _orderOfBattleCategory;
    private MissionGauntletSingleplayerOrderUIHandler _orderUIHandler;
    private AssignPlayerRoleInTeamMissionController _playerRoleMissionController;
    private OrderTroopPlacer _orderTroopPlacer;

    private bool _isActive;

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        if (_isActive)
        {
            _wereHotkeysEnabledLastFrame = _dataSource.AreHotkeysEnabled;
            HandleLayerFocus(out _isAnyHeroSelected, out _isClassSelectionEnabled);
            TickInput();
        }
    }

    public override void OnMissionScreenFinalize()
    {
        DestroyView();
        base.OnMissionScreenFinalize();
    }
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Mission"
```

```csharp
public void OnTick(float dt, float realDt, bool updateCamera, bool doAsyncAITick)
{
    ApplyGeneratedCombatLogs();
    if (InputManager == null)
    {
        InputManager = new EmptyInputContext();
    }
    for (int i = 0; i < _tickActions.Count; i++)
    {
        ...
    }
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll" -t "TaleWorlds.MountAndBlade.Formation"
```

```csharp
public void SetMovementOrder(MovementOrder input)
{
    this.OnBeforeMovementOrderApplied?.Invoke(this, input.OrderEnum);
    if (input.OrderEnum == MovementOrder.MovementOrderEnum.Invalid)
    {
        input = MovementOrder.MovementOrderStop;
    }
    bool num = !_movementOrder.AreOrdersPracticallySame(_movementOrder, input, IsAIControlled);
    if (num)
    {
        _movementOrder.OnCancel(this);
        ...
    }
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Engine.GauntletUI.dll" -t "TaleWorlds.Engine.GauntletUI.GauntletLayer"
```

```csharp
public GauntletLayer(string name, int localOrder, bool shouldClear = false)
    : base(name, localOrder)
{
    _movieIdentifiers = new MBList<GauntletMovieIdentifier>();
    ResourceDepot resourceDepot = UIResourceManager.ResourceDepot;
    TwoDimensionView = TwoDimensionView.CreateTwoDimension(name);
    if (shouldClear)
    {
        TwoDimensionView.SetClearColor(255u);
    }
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.MultiSelectionInquiryData"
```

```csharp
public MultiSelectionInquiryData(string titleText, string descriptionText, List<InquiryElement> inquiryElements,
    bool isExitShown, int minSelectableOptionCount, int maxSelectableOptionCount, string affirmativeText, string negativeText,
    Action<List<InquiryElement>> affirmativeAction, Action<List<InquiryElement>> negativeAction,
    string soundEventPath = "", bool isSeachAvailable = false)
{
    TitleText = titleText;
    DescriptionText = descriptionText;
    InquiryElements = inquiryElements;
    IsExitShown = isExitShown;
    AffirmativeText = affirmativeText;
    NegativeText = negativeText;
    AffirmativeAction = affirmativeAction;
    NegativeAction = negativeAction;
    MinSelectableOptionCount = minSelectableOptionCount;
    MaxSelectableOptionCount = maxSelectableOptionCount;
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Library.dll" -t "TaleWorlds.Library.TextInquiryData"
```

```csharp
public TextInquiryData(string titleText, string text, bool isAffirmativeOptionShown, bool isNegativeOptionShown,
    string affirmativeText, string negativeText, Action<string> affirmativeAction, Action negativeAction,
    bool shouldInputBeObfuscated = false, Func<string, Tuple<bool, string>> textCondition = null,
    string soundEventPath = "", string defaultInputText = "")
{
    TitleText = titleText;
    Text = text;
    IsAffirmativeOptionShown = isAffirmativeOptionShown;
    IsNegativeOptionShown = isNegativeOptionShown;
    AffirmativeText = affirmativeText;
    NegativeText = negativeText;
    AffirmativeAction = affirmativeAction;
    NegativeAction = negativeAction;
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Core.dll" -t "TaleWorlds.Core.WeaponClass"
```

```csharp
public enum WeaponClass
{
    Undefined,
    Dagger,
    OneHandedSword,
    TwoHandedSword,
    OneHandedAxe,
    TwoHandedAxe,
    Mace,
    Pick,
    TwoHandedMace,
    OneHandedPolearm,
    TwoHandedPolearm,
    LowGripPolearm,
    Arrow,
    Bolt,
    SlingStone,
    Cartridge,
    Bow,
    Crossbow,
    Sling,
    Stone,
    Boulder,
    ThrowingAxe,
    ThrowingKnife,
    Javelin,
    Pistol,
    Musket,
    BallistaBoulder,
    BallistaStone,
    SmallShield,
    LargeShield,
    Banner,
    NumClasses
}
```

Command:

```powershell
ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" -t "TaleWorlds.CampaignSystem.CampaignBehaviorDataStore"
```

```csharp
public bool SyncData<T>(string key, ref T data)
{
    if (IsSaving)
    {
        _records.Add(key, data);
        return true;
    }
    if (_records.TryGetValue(key, out var value))
    {
        data = (T)value;
        return true;
    }
    return false;
}
```

## ADR-007 Adapter Audit

No sealed TaleWorlds types were found crossing the core service interfaces named in the prompt:

- `CompanionRoleService` accepts `IHeroCombatAdapter`; it uses `WeaponClass`, a TaleWorlds enum, not a sealed engine object.
- `BattleActionBarService` accepts `IFormationAdapter`.
- `FormationCompositionAnalyzer` accepts `IFormationAdapter`.
- `TroopStanceManager` accepts primitive `int formationIndex`.
- `FormationPresetService` accepts `HoNFormationPreset` and `List<HoNFormationPreset>`.
- `HeroAutoAssigner` accepts `IHeroCombatAdapter` plus primitive `int formationClass`.

Boundary/non-adapter TaleWorlds references observed:

- Hooks and MissionView use `PartyCharacterVM`, `OrderOfBattleHeroItemVM`, `OrderOfBattleVM`, `MissionGauntletOrderOfBattleUIHandler`, `Mission`, `Formation`, `MovementOrder`, and `GauntletLayer`.
- `OOBButtonsVM` uses `OrderOfBattleVM`, `MultiSelectionInquiryData`, and `TextInquiryData`; this is boundary/UI code.
- `OOBOverlayService` uses `MissionGauntletOrderOfBattleUIHandler`, `MissionView`, `MissionScreen`, and `GauntletLayer`; this is explicitly the reflection boundary.
- `RoleTooltipDecorator` is a boundary decorator that receives TaleWorlds VMs and converts resolved `Hero` objects into `HeroCombatAdapter` before calling `CompanionRoleService`. I did not count this as a core service leak, but it is the closest boundary placement to watch.

## Test Coverage Observations

- FormationPresets tests cover only pure CRUD over manually populated `HoNFormationPreset` instances. They do not cover `OOBButtonsVM.SaveCurrent`, capture from `OrderOfBattleVM`, applying a preset to `OrderOfBattleVM`, or the `ExecuteAssignCharacters` path.
- No test exercises `FormationPresetCampaignBehavior.SyncData` with a fake `IDataStore`, so the one-save-lag bug is invisible.
- No test covers new-campaign lifecycle reset for singleton CompanionTactics services.
- BattleActionBar tests cover service action list construction and `TroopStanceManager`, but not `BattleActionBarVM`, MissionView refresh behavior, hotkey execution, movement-order clearing, or active-indicator synchronization.
- Role tests cover mapped role classes but not a table-driven audit of all v1.3.15 `WeaponClass` enum values.
- Prefab binding names resolve: `BattleActionBar.xml` binds `IsVisible`, `FormationName`, `ActionButtons`, `CategoryColor`, `IsActive`, `DisplayText`, `Hotkey`, `ExecuteAction`; `OOBButtonsOverlay.xml` binds `IsVisible`, `PresetsButtonText`, `ExecuteAssignCharacters`, and `ExecuteManagePresets`.

