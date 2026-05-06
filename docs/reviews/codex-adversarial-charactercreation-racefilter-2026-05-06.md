# SUMMARY

Reviewed the Patch9_RaceFilter re-implementation against TAOM source, `cultures.json`, live race XML, and installed Bannerlord v1.3.15 DLLs decompiled with:

```powershell
C:\Users\mikew\.dotnet\tools\ilspycmd.exe
```

Findings:

```text
CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0
VERDICT: ISSUES FOUND
```

The main bug is not the index-translation wrapper itself. The wrapper is mostly coherent. The bug is the property-change reflection target: it resolves to `null`, so after vanilla `Refresh(true)` fires a `RaceSelector` notification for the unfiltered selector, TAOM silently swaps the private field to the filtered selector without notifying Gauntlet. Initial construction can still appear correct because `BodyGeneratorView` calls `DataSource.Refresh(true)` before `LoadMovie("FaceGen", DataSource)`, but subsequent race refreshes can rebind the UI to vanilla's full selector.

# KNOWN SUSPECTS

## 1. `OnPropertyChangedWithValue` Reflection Target Wrong

VERDICT: CONFIRMED

EVIDENCE:

TAOM looks up a non-generic `(object, string)` overload on `FaceGenVM`:

```csharp
// Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs:168-171
_onPropertyChangedWithValueMethod = AccessTools.Method(
    typeof(FaceGenVM),
    "OnPropertyChangedWithValue",
    new[] { typeof(object), typeof(string) });
```

Installed v1.3.15 has the object-valued overload only as a generic base method:

```csharp
// TaleWorlds.Library.ViewModel, decompiled from installed v1.3.15
public void OnPropertyChangedWithValue<T>(T value, [CallerMemberName] string propertyName = null) where T : class
{
    if (_eventHandlersWithValue != null)
    {
        for (int i = 0; i < _eventHandlersWithValue.Count; i++)
        {
            PropertyChangedWithValueEventHandler handler = _eventHandlersWithValue[i];
            PropertyChangedWithValueEventArgs e = new PropertyChangedWithValueEventArgs(propertyName, value);
            handler(this, e);
        }
    }
}
```

I also verified the exact Harmony lookup in PowerShell with installed assemblies and Harmony 2.4.2:

```text
[HarmonyLib.AccessTools]::Method(FaceGenVM, "OnPropertyChangedWithValue", object,string) => NULL
```

Gauntlet is event-driven for VM replacement:

```csharp
// TaleWorlds.GauntletUI.Data.GauntletView, decompiled from installed v1.3.15
_viewModel.PropertyChanged += OnViewModelPropertyChanged;
_viewModel.PropertyChangedWithValue += OnViewModelPropertyChangedWithValue;

private void OnViewModelPropertyChangedWithValue(object sender, PropertyChangedWithValueEventArgs e)
{
    OnPropertyChanged(e.PropertyName, e.Value);
}

private void OnPropertyChanged(string propertyName, object value)
{
    if (value is ViewModel || value is IMBBindingList)
    {
        ...
        if (BindingPath.IsRelatedWithPathAsString(path, gauntletView.ViewModelPathString))
            gauntletView.RefreshBindingWithChildren();
    }
}
```

SEVERITY: HIGH

PROPOSED FIX: Do not reflect the notification. Use the public property setter:

```csharp
faceGenVM.RaceSelector = newSelector;
```

and remove `_raceSelectorField.SetValue(...)` plus `_onPropertyChangedWithValueMethod`. If reflection is kept, resolve the generic method from `ViewModel` and close it with `SelectorVM<SelectorItemVM>`, but the property setter is simpler and matches vanilla.

## 2. `_selectedIndex` Mutation During `_onChange`

VERDICT: DISPUTED

EVIDENCE:

Vanilla `OnSelectRace` reads only `SelectedIndex`, then synchronously refreshes:

```csharp
private void OnSelectRace(SelectorVM<SelectorItemVM> s)
{
    AddCommand();
    _selectedRace = s.SelectedIndex;
    ...
    UpdateRaceAndGenderBasedResources();
    Refresh(clearProperties: true);
}
```

TAOM restores the old filtered selector's field after vanilla returns:

```csharp
var saved = (int)_selectorVmSelectedIndexField.GetValue(s);
_selectorVmSelectedIndexField.SetValue(s, globalIdx);
try { vanillaOnChange?.Invoke(s); }
finally { _selectorVmSelectedIndexField.SetValue(s, saved); }
```

Because vanilla does not inspect `SelectedItem`, and the finally restores `_selectedIndex` on the old filtered selector, the old selector's final state is internally consistent. The real UI rebinding problem is F1: the new filtered selector assigned during the inner refresh is not announced.

## 3. `needToSwitchRace` Force-Switch Bypasses SelectorVM Lifecycle

VERDICT: DISPUTED

EVIDENCE:

`BuildSelector` manually establishes the selected item before force-switching:

```csharp
_selectorVmSelectedIndexField.SetValue(selector, filteredSelected);
var item = selector.ItemList[filteredSelected];
item.IsSelected = true;
_selectorVmSelectedItemField.SetValue(selector, item);
selector.SetOnChangeAction(wrapped);
```

The direct `wrapped(newSelector)` call is not trying to notify a user click. It translates filtered position `0` to the global race ID and drives vanilla's `OnSelectRace` so `_selectedRace` and `FaceGenerationParams.CurrentRace` are corrected. With F1 fixed, the UI binds to a selector that already has `SelectedIndex`, `SelectedItem`, and `IsSelected` set.

## 4. `_inForceSwitch` ThreadStatic Guard Correctness

VERDICT: DISPUTED

EVIDENCE:

The relevant vanilla path is synchronous:

```csharp
// SelectorVM<T>.SelectedIndex setter
_onChange?.Invoke(this);

// FaceGenVM.OnSelectRace
UpdateRaceAndGenderBasedResources();
Refresh(clearProperties: true);
```

I searched the installed v1.3.15 decompiled `FaceGenVM`, `CharacterCreationState`, and `CharacterCreationManager` snippets for `Task.Run`, `Dispatcher`, and `BeginInvoke`. None appear in this call chain. `CharacterCreationManager.NextStage`, `ActivateStage`, and `CharacterCreationState.Refresh()` also invoke directly. The `[ThreadStatic]` guard is sufficient for the observed recursion.

## 5. Race-Name Lookup Fallback Masks Invalid IDs

VERDICT: CONFIRMED

EVIDENCE:

`RaceManager.GetRaceNameFromId` maps unknown IDs to the name for ID 0:

```csharp
// Main/Core/Domain/RaceManager.cs:126-131
_logger.LogWarning($"Unknown race ID {id} encountered. Defaulting to 'human'. " +
                   $"Known race IDs: {string.Join(", ", _idToName.Keys)}");

var fallback = _idToName.TryGetValue(0, out var value) ? value : "human";
_raceNameCache[id] = fallback;
return fallback;
```

`SetPlayerRace` then accepts the fallback name and preserves the original integer:

```csharp
// Main/Features/CharacterCreation/CharacterCreationContentService.cs:243-255
var faceGenRaceId = _heroRosterAdapter.GetHeroRace(heroStringId);
var faceGenRaceName = _raceManager.GetRaceNameFromId(faceGenRaceId);

bool faceGenChoiceAllowed = cultureData.Races != null
    && cultureData.Races.Length > 0
    && cultureData.Races.Any(r => string.Equals(r, faceGenRaceName, StringComparison.OrdinalIgnoreCase));

if (faceGenChoiceAllowed)
{
    raceName = faceGenRaceName;
    raceId = faceGenRaceId;
}
```

Vanilla allows arbitrary race integers to be written into the player character:

```csharp
// TaleWorlds.Core.BasicCharacterObject, decompiled from installed v1.3.15
public int Race { get; set; }

public virtual void UpdatePlayerCharacterBodyProperties(BodyProperties properties, int race, bool isFemale)
{
    BodyPropertyRange.Init(properties, properties);
    Race = race;
    IsFemale = isFemale;
}
```

And the FaceGen stage writes through that method:

```csharp
// TaleWorlds.MountAndBlade.BodyGenerator, decompiled from installed v1.3.15
public void SaveCurrentCharacter()
{
    Character.UpdatePlayerCharacterBodyProperties(CurrentBodyProperties, Race, IsFemale);
}
```

SEVERITY: MEDIUM

PROPOSED FIX: Treat invalid IDs as disallowed before converting to a name:

```csharp
var faceGenRaceId = _heroRosterAdapter.GetHeroRace(heroStringId);
var faceGenChoiceAllowed = _raceManager.IsValidRaceId(faceGenRaceId)
    && cultureData.Races != null
    && cultureData.Races.Any(r => string.Equals(r, _raceManager.GetRaceNameFromId(faceGenRaceId), StringComparison.OrdinalIgnoreCase));
```

Alternatively, if the fallback name is accepted, set `raceId = _raceManager.GetRaceIdFromName(faceGenRaceName)` instead of preserving `faceGenRaceId`.

## 6. Orphaned SelectorVM Event Subscriptions / GC Pressure

VERDICT: DISPUTED, WITH F1 CAVEAT

EVIDENCE:

The prefab binds the dropdown to `RaceSelector`:

```xml
<!-- Native/GUI/Prefabs/FaceGen/FaceGenBody.xml:48 -->
<Standard.DropdownWithHorizontalControl Id="RaceSelection"
    VerticalAlignment="Center"
    Parameter.SelectorDataSource="{RaceSelector}" />
```

When a ViewModel-valued property change is raised, Gauntlet refreshes related children, and `ReleaseBinding()` unsubscribes old VM events:

```csharp
private void ReleaseBinding()
{
    if (_viewModel != null)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChangedWithValue -= OnViewModelPropertyChangedWithValue;
        ...
    }
    else if (_bindingList != null)
    {
        _bindingList.ListChanged -= OnViewModelBindingListChanged;
    }
}
```

So with a correct `RaceSelector` property notification, this is not a strong leak. Without the notification, the bug is F1: the UI stays bound to the prior selector instead of rebinding to TAOM's filtered selector.

## 7. `cultures.json` ID Consistency

VERDICT: DISPUTED

EVIDENCE:

All `culture_id` values in `Main/_Module/ModuleData/charactercreation/cultures.json` match the provided cheatsheet. No `rohan` or `dol_guldur` appears. The file correctly uses `vlandia` for Rohan and `dolguldur` for Dol Guldur.

## 8. `AccessTools.Method` for Sealed `Refresh` Overload

VERDICT: DISPUTED

EVIDENCE:

Reflection against installed v1.3.15 reports only one declared `Refresh` method on `FaceGenVM`:

```text
Void Refresh(Boolean)
```

The decompiled method is on `FaceGenVM` itself:

```csharp
public void Refresh(bool clearProperties)
{
    if (!_characterRefreshEnabled)
        return;
    ...
}
```

The patch attribute is correctly targeted:

```csharp
[HarmonyPatch(typeof(FaceGenVM), "Refresh", new[] { typeof(bool) })]
```

# FINDINGS

## F1. RaceSelector Replacement Does Not Notify Gauntlet

[HIGH] Main/Features/CharacterCreation/FaceGenRaceSelectorRebuilder.cs:71 — API correctness — The patch replaces `_raceSelector` by field reflection, then tries to invoke a non-existent `OnPropertyChangedWithValue(object,string)` method. Harmony returns `null`, so no `RaceSelector` notification is raised for TAOM's filtered selector. — Fix by assigning through `faceGenVM.RaceSelector = newSelector`.

DESCRIPTION:

Vanilla `Refresh(true)` sets `RaceSelector` through the property and fires a property-change event for the full, unfiltered selector. TAOM then silently swaps the private field to the filtered selector. On first construction this can be masked because `BodyGeneratorView` calls `DataSource.Refresh(true)` before `LoadMovie("FaceGen", DataSource)`, so the initial binding reads the final field. After the UI is loaded, any race-triggered `Refresh(true)` can rebind the dropdown to vanilla's unfiltered selector and TAOM's replacement is not announced.

EVIDENCE:

```csharp
// TAOM
_raceSelectorField.SetValue(faceGenVM, newSelector);
_onPropertyChangedWithValueMethod?.Invoke(faceGenVM, new object[] { newSelector, "RaceSelector" });
```

```csharp
// Vanilla FaceGenVM.RaceSelector
public SelectorVM<SelectorItemVM> RaceSelector
{
    get { return _raceSelector; }
    set
    {
        if (value != _raceSelector)
        {
            _raceSelector = value;
            OnPropertyChangedWithValue(value, "RaceSelector");
        }
    }
}
```

IMPACT:

The race dropdown can initially show the filtered list but fall back to the full vanilla race list after a race refresh. On multi-race cultures like Mordor or Isengard, the first player selection can be translated correctly, then the UI can be rebound to the unfiltered selector.

PROPOSED FIX:

Replace lines 71-72 with:

```csharp
faceGenVM.RaceSelector = newSelector;
```

Remove `_raceSelectorField` for assignment if it is no longer needed, and add a reflection/integration-style test that asserts the notification method path is not `null` or that the public setter is used.

## F2. Invalid FaceGen Race IDs Can Be Preserved When the Fallback Name Is Allowed

[MEDIUM] Main/Features/CharacterCreation/CharacterCreationContentService.cs:243 — Validation — `GetRaceNameFromId` falls back unknown IDs to `"human"`, but `SetPlayerRace` preserves the original invalid integer when `"human"` is allowed for the culture. — Validate the ID before accepting the FaceGen choice, or remap accepted names back to known IDs.

DESCRIPTION:

For a culture that allows `human`, an invalid FaceGen race ID such as `999` becomes `faceGenRaceName == "human"` via `RaceManager`. `faceGenChoiceAllowed` becomes true, and TAOM writes `999` back into `Hero.CharacterObject.Race`.

EVIDENCE:

```csharp
var faceGenRaceName = _raceManager.GetRaceNameFromId(faceGenRaceId);
...
if (faceGenChoiceAllowed)
{
    raceName = faceGenRaceName;
    raceId = faceGenRaceId;
}
...
_heroRosterAdapter.SetHeroRace(heroStringId, raceId);
```

```csharp
public string GetRaceNameFromId(int id)
{
    ...
    var fallback = _idToName.TryGetValue(0, out var value) ? value : "human";
    _raceNameCache[id] = fallback;
    return fallback;
}
```

IMPACT:

If an invalid race ID reaches finalization through corrupted state, mod interop, or a future UI regression, cultures that allow human can silently preserve the invalid ID. Downstream engine calls such as `FaceGen.GetBaseMonsterFromRace(BodyGen.Race)` and body property generation expect valid race indices.

PROPOSED FIX:

Use `_raceManager.IsValidRaceId(faceGenRaceId)` before calling the allowed-list check. Add a test where `GetHeroRace()` returns an invalid ID, `GetRaceNameFromId()` returns `"human"`, and Mordor still falls back to a known valid ID instead of preserving the invalid integer.

# VANILLA CODE BLOCKS

All snippets below are from installed v1.3.15 DLLs via `ilspycmd`, not from `E:\Decompiled_Bannerlord`.

## `TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator.FaceGenVM`

```csharp
private int _selectedRace = -1;

private SelectorVM<SelectorItemVM> _raceSelector;

[DataSourceProperty]
public SelectorVM<SelectorItemVM> RaceSelector
{
    get
    {
        return _raceSelector;
    }
    set
    {
        if (value != _raceSelector)
        {
            _raceSelector = value;
            OnPropertyChangedWithValue(value, "RaceSelector");
        }
    }
}
```

```csharp
public FaceGenVM(BodyGenerator bodyGenerator, IFaceGeneratorHandler faceGeneratorScreen, Action<float> onHeightChanged, Action onAgeChanged, TextObject affirmitiveText, TextObject negativeText, int currentStageIndex, int totalStagesCount, int furthestIndex, Action<int> goToIndex, bool canChangeGender, bool openedFromMultiplayer, IFaceGeneratorCustomFilter filter)
{
    _bodyGenerator = bodyGenerator;
    _faceGeneratorScreen = faceGeneratorScreen;
    ...
    CanChangeRace = _isRaceAvailable;
    RefreshValues();
}
```

```csharp
private void OnSelectRace(SelectorVM<SelectorItemVM> s)
{
    AddCommand();
    _selectedRace = s.SelectedIndex;
    if (_initialRace == -1 && !TryGetInitialValue("SelectedRace", ref _initialRace))
    {
        SetOrAddInitialValue("SelectedRace", _selectedRace);
    }
    UpdateRaceAndGenderBasedResources();
    Refresh(clearProperties: true);
}
```

```csharp
public void Refresh(bool clearProperties)
{
    if (!_characterRefreshEnabled)
    {
        return;
    }
    _characterRefreshEnabled = false;
    OnPropertyChanged("FlipHairCb");
    _selectedRace = _faceGenerationParams.CurrentRace;
    SelectedGender = _faceGenerationParams.CurrentGender;
    ...
    if (clearProperties)
    {
        ...
        RaceSelector = new SelectorVM<SelectorItemVM>(TaleWorlds.Core.FaceGen.GetRaceNames(), _selectedRace, OnSelectRace);
    }
    UpdateRaceAndGenderBasedResources();
    ...
    _characterRefreshEnabled = true;
    UpdateFace();
}
```

```csharp
private void UpdateRaceAndGenderBasedResources()
{
    ...
    UpdateFace(-20, _selectedRace, calledFromInit: true);
    UpdateFace(-1, _selectedGender, calledFromInit: true);
    ...
}

private void UpdateFace(int keyNo, float value, bool calledFromInit, bool isNeedRefresh = true)
{
    ...
    switch ((Presets)keyNo)
    {
    case Presets.Race:
        RestoreRaceGenderBasedSelectedValues();
        _faceGenerationParams.SetRaceGenderAndAdjustParams((int)value, SelectedGender, (int)_faceGenerationParams.CurrentAge);
        break;
    ...
    }
}
```

## `TaleWorlds.Core.ViewModelCollection.Selector.SelectorVM<T>`

```csharp
public class SelectorVM<T> : ViewModel where T : SelectorItemVM
{
    private Action<SelectorVM<T>> _onChange;
    private MBBindingList<T> _itemList;
    private int _selectedIndex = -1;
    private T _selectedItem;

    [DataSourceProperty]
    public int SelectedIndex
    {
        get { return _selectedIndex; }
        set
        {
            if (value != _selectedIndex)
            {
                _selectedIndex = value;
                OnPropertyChangedWithValue(value, "SelectedIndex");
                if (SelectedItem != null)
                    SelectedItem.IsSelected = false;
                SelectedItem = GetCurrentItem();
                if (SelectedItem != null)
                    SelectedItem.IsSelected = true;
                _onChange?.Invoke(this);
            }
        }
    }
```

```csharp
public SelectorVM(int selectedIndex, Action<SelectorVM<T>> onChange)
{
    ItemList = new MBBindingList<T>();
    HasSingleItem = true;
    _onChange = onChange;
}

public SelectorVM(IEnumerable<string> list, int selectedIndex, Action<SelectorVM<T>> onChange)
{
    ItemList = new MBBindingList<T>();
    Refresh(list, selectedIndex, onChange);
}

public void Refresh(IEnumerable<string> list, int selectedIndex, Action<SelectorVM<T>> onChange)
{
    ItemList.Clear();
    _selectedIndex = -1;
    foreach (string item2 in list)
    {
        T item = (T)Activator.CreateInstance(typeof(T), item2);
        ItemList.Add(item);
    }
    HasSingleItem = ItemList.Count <= 1;
    _onChange = onChange;
    SelectedIndex = selectedIndex;
}

public void SetOnChangeAction(Action<SelectorVM<T>> onChange)
{
    _onChange = onChange;
}

public void AddItem(T item)
{
    ItemList.Add(item);
    HasSingleItem = ItemList.Count <= 1;
}
```

## `TaleWorlds.Core.ViewModelCollection.Selector.SelectorItemVM`

```csharp
public class SelectorItemVM : ViewModel
{
    private string _stringItem;
    private bool _canBeSelected = true;
    private bool _isSelected;

    [DataSourceProperty]
    public string StringItem
    {
        get { return _stringItem; }
        set
        {
            if (value != _stringItem)
            {
                _stringItem = value;
                OnPropertyChangedWithValue(value, "StringItem");
            }
        }
    }

    [DataSourceProperty]
    public bool IsSelected
    {
        get { return _isSelected; }
        set
        {
            if (value != _isSelected)
            {
                _isSelected = value;
                OnPropertyChangedWithValue(value, "IsSelected");
            }
        }
    }

    public SelectorItemVM(string s)
    {
        _stringItem = s;
        RefreshValues();
    }
}
```

## `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState`

```csharp
public class CharacterCreationState : PlayerGameState
{
    private CharacterCreationManager _characterCreationManager;

    public CharacterCreationManager CharacterCreationManager
    {
        get { return _characterCreationManager; }
        private set { _characterCreationManager = value; }
    }

    public CharacterCreationState()
    {
        CharacterCreationManager = new CharacterCreationManager(this);
    }

    protected override void OnActivate()
    {
        base.OnActivate();
        CharacterCreationManager.OnStateActivated();
    }

    public void Refresh()
    {
        _handler?.OnRefresh();
    }
}
```

## `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager`

```csharp
public CharacterCreationContent CharacterCreationContent { get; private set; }

public CharacterCreationManager(CharacterCreationState state)
{
    _state = state;
    ...
    CharacterCreationContent = new CharacterCreationContent();
    CampaignEventDispatcher.Instance.OnCharacterCreationInitialized(this);
    foreach (KeyValuePair<int, ICharacterCreationContentHandler> handler in _handlers)
        handler.Value.InitializeContent(this);
    foreach (KeyValuePair<int, ICharacterCreationContentHandler> handler2 in _handlers)
        handler2.Value.AfterInitializeContent(this);
}
```

```csharp
public void NextStage()
{
    _stageIndex++;
    if (CurrentStage != null)
    {
        CurrentStage?.OnFinalize();
        foreach (KeyValuePair<int, ICharacterCreationContentHandler> handler in _handlers)
            handler.Value.OnStageCompleted(CurrentStage);
    }
    _furthestStageIndex = MathF.Max(_furthestStageIndex, _stageIndex);
    if (_stageIndex == _stages.Count)
    {
        ApplyFinalEffects();
        _state.FinalizeCharacterCreationState();
    }
    else
    {
        ActivateStage(_stages[_stageIndex]);
        _state.Refresh();
    }
}
```

```csharp
public void ApplyFinalEffects()
{
    Clan.PlayerClan.Renown = 0f;
    CharacterCreationContent.ApplyCulture(this);
    foreach (KeyValuePair<NarrativeMenu, NarrativeMenuOption> selectedOption in SelectedOptions)
        selectedOption.Value.ApplyFinalEffects(CharacterCreationContent);
    ...
    foreach (KeyValuePair<int, ICharacterCreationContentHandler> handler in _handlers)
        handler.Value.OnCharacterCreationFinalize(this);
}
```

## `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent`

```csharp
public CultureObject SelectedCulture { get; private set; }

public void SetSelectedCulture(CultureObject culture, CharacterCreationManager characterCreationManager)
{
    SelectedCulture = culture;
    characterCreationManager.ResetMenuOptions();
    SelectedTitleType = DefaultSelectedTitleType;
    TextObject textObject = FactionHelper.GenerateClanNameforPlayer();
    Clan.PlayerClan.ChangeClanName(textObject, textObject);
}

public void ApplyCulture(CharacterCreationManager characterCreationManager)
{
    Hero.MainHero.Culture = SelectedCulture;
    Clan.PlayerClan.Culture = SelectedCulture;
    Clan.PlayerClan.ResetPlayerHomeAndFactionMidSettlement();
    Hero.MainHero.BornSettlement = Clan.PlayerClan.HomeSettlement;
}
```

# CONFIG CROSS-REFERENCE

Source checked:

```text
Main/_Module/ModuleData/charactercreation/cultures.json
Main/_Module/ModuleData/taom_spcultures.xml
E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/Native/ModuleData/skins.xml
E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/skins.xml
E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/LOTRLOME_Armory/ModuleData/monsters.xml
```

Live race IDs found:

```text
Native skins: human
LOTRLOME_Armory skins: berserker, cave_troll, dg_uruk, dwarf, elf, goblin, hill_troll, nazghul, orc, pale_uruk, saruman, uruk, uruk_hai
LOTRLOME_Armory monsters includes all configured player races plus settlement/child variants.
```

Culture/race table:

| culture_id | races[] | Result |
|---|---|---|
| `gondor` | `human` | Valid custom culture ID; valid race |
| `mordor` | `uruk`, `orc`, `human` | Valid; matches spec; no `goblin` |
| `erebor` | `dwarf` | Valid |
| `rivendell` | `elf`, `human` | Valid elven allow-list |
| `mirkwood` | `elf`, `human` | Valid elven allow-list |
| `lothlorien` | `elf`, `human` | Valid elven allow-list |
| `isengard` | `uruk_hai`, `berserker`, `human` | Valid; matches spec; no `saruman` |
| `gundabad` | `pale_uruk`, `goblin`, `orc`, `human` | Valid |
| `dolguldur` | `dg_uruk`, `goblin`, `orc`, `human` | Valid spelling; no `dol_guldur` |
| `umbar` | `human` | Valid |
| `empire` | `human` | Valid vanilla/XSLT culture ID for Dunland |
| `vlandia` | `human` | Valid vanilla/XSLT culture ID for Rohan; no `rohan` |
| `sturgia` | `human` | Valid |
| `aserai` | `human` | Valid |
| `shaghana` | `human` | Valid custom culture ID |
| `abanissa` | `human` | Valid custom culture ID |
| `battania` | `human` | Valid |
| `khuzait` | `human` | Valid |

No config finding.

# OBSERVATIONS

1. The filtered-to-global index map preserves engine order, not config order. That is correct because `FaceGenVM` treats the global array position from `FaceGen.GetRaceNames()` as the race ID.

2. Human-only culture flow is internally correct on initial load. If `_selectedRace = 0`, no force-switch runs. If `_selectedRace = 5`, `filteredSelected = -1`, TAOM builds a single-item selector at filtered index `0`, translates to global human index `0`, and vanilla `OnSelectRace` sets `_selectedRace = 0`.

3. Erebor initial force-switch is internally correct. With `_selectedRace = 0` and allowed `[dwarf]`, TAOM maps filtered index `0` to dwarf's global index, vanilla sets `_selectedRace` to that global index and refreshes, and `_inForceSwitch` prevents a second force-switch. F1 affects UI rebinding after the movie is already loaded, not this initial internal state transition.

4. The Harmony target itself is valid for installed v1.3.15: `FaceGenVM` declares exactly one `Refresh(Boolean)` method.

5. Current tests cover pure mapping helpers and service allow-lists, but they do not exercise the reflection notification path or a live `SelectorVM` + `FaceGenVM` callback cycle. F1 would not be caught by the present test set.
