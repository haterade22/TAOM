# Codex Adversarial Review: FactionMap

**Date:** 2026-04-05
**Target:** working tree diff
**Verdict:** needs-attention

No-ship: the `TrySwitchToNextMenu` bugfix avoids the crash, but it does not preserve vanilla's `ModifyMenuCharacters()` side effect, and the banner widget fails open to stale visuals when a data-driven asset lookup misses.

## Section 1: Vanilla Code

### CharacterCreationCultureStageView (decompiled v1.3.15)

```csharp
// Constructor
public CharacterCreationCultureStageView(...)
    : base(...)
{
    _characterCreationManager = characterCreationManager;
    GauntletLayer = new GauntletLayer("CharacterCreationCulture", 1, true) { IsFocusLayer = true };
    ((ScreenLayer)GauntletLayer).InputRestrictions.SetInputRestrictions(true, (InputUsageMask)7);
    ((ScreenLayer)GauntletLayer).Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericPanelGameKeyCategory"));
    ScreenManager.TrySetFocus((ScreenLayer)(object)GauntletLayer);
    _dataSource = new CharacterCreationCultureStageVM(_characterCreationManager, (Action)NextStage, affirmativeActionText, (Action)PreviousStage, negativeActionText, (Action<CultureObject>)OnCultureSelected);
    _movie = GauntletLayer.LoadMovie("CharacterCreationCultureStage", (ViewModel)(object)_dataSource);
    _dataSource.SetCancelInputKey(...GetHotKey("Exit"));
    _dataSource.SetDoneInputKey(...GetHotKey("Confirm"));
    _characterCreationCategory = UIResourceManager.LoadSpriteCategory("ui_charactercreation");
    if (_characterCreationManager.GetStage<CharacterCreationBannerEditorStage>() != null)
        _bannerEditorCategory = UIResourceManager.LoadSpriteCategory("ui_bannericons");
}
```

```csharp
// OnFinalize
protected override void OnFinalize()
{
    base.OnFinalize();
    GauntletLayer = null;
    CharacterCreationCultureStageVM dataSource = _dataSource;
    if (dataSource != null)
        ((ViewModel)dataSource).OnFinalize();
    _dataSource = null;
    _characterCreationCategory.Unload();
}
```

```csharp
// Tick
public override void Tick(float dt)
{
    base.Tick(dt);
    if (_dataSource.IsActive)
    {
        HandleEscapeMenu(this, (ScreenLayer)(object)GauntletLayer);
        HandleLayerInput();
    }
}
```

### CharacterCreationManager.TrySwitchToNextMenu (decompiled v1.3.15)

```csharp
public bool TrySwitchToNextMenu()
{
    string stringId = CurrentMenu.StringId;
    SelectedOptions[CurrentMenu].OnConsequence(this);
    foreach (NarrativeMenu narrativeMenu in NarrativeMenus)
    {
        if (narrativeMenu.InputMenuId.Equals(stringId))
        {
            CurrentMenu = narrativeMenu;
            ModifyMenuCharacters();
            return true;
        }
    }
    return false;
}
```

## Section 2: Patch Target Analysis

- **CultureStageView patches** use dynamic `FindCultureStageViewType()` resolution over two hard-coded names. In v1.3.15 the decompiled class is `SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView`. If the class moves or is renamed, `Prepare()` returns false and Harmony skips the patch silently — no logging.
- **TrySwitchToNextMenu_Patch** prevents the `KeyNotFoundException` from `SelectedOptions[CurrentMenu]` but does not call `ModifyMenuCharacters()` after setting `CurrentMenu`. See Finding 1.
- **No missing lifecycle hooks.** v1.3.15 has no `OnActivate`/`OnDeactivate` on this class. The concrete surface is: constructor, `Tick`, `NextStage`, `PreviousStage`, `LoadEscapeMenuMovie`, `ReleaseEscapeMenuMovie`, `OnFinalize`.

## Section 3: Widget Analysis

- **PolygonWidget.cs (1,107 lines):** Uses Gauntlet lifecycle correctly — layout/state in `OnLateUpdate`, custom drawing in `OnRender`, explicit `OnPropertyChanged` on bound properties. The `base.OnRender` bypass is intentional (BrushRenderer darkens the texture).
- **Per-frame allocation:** Render path creates multiple `SimpleMaterial`s per widget per frame (shadow, up to 7 edge slices, pulse, banner overlay, pin passes). `OnLateUpdate()` avoids per-frame managed collection churn, but global hover resolution is O(n²) — every widget calls `ResolveGlobalHover()` which scans all widgets.
- **BannerWidget.cs (316 lines):** Does not parse Bannerlord banner codes. Loads `GUI/SpriteData/FactionMap/<BannerImage>.png`. Malformed identifiers degrade as file misses, leaving previous sprite resident. See Finding 2.

## Findings

### [HIGH] TrySwitchToNextMenu bugfix drops vanilla menu-character refresh

**File:** `TrySwitchToNextMenu_Patch.cs:16-33`

**TAOM code:** The prefix short-circuits by checking `!__instance.SelectedOptions.ContainsKey(__instance.CurrentMenu)` and assigns `CurrentMenu` directly (lines 18-29).

**Vanilla code:** After advancing, vanilla does two things: `CurrentMenu = narrativeMenu; ModifyMenuCharacters(); return true;`

**Evidence of divergence:** The patch reproduces the assignment but omits `ModifyMenuCharacters()`. The code path this patch rescues can advance into a narrative menu with stale/default character slots, because menu-specific `NarrativeMenuCharacterArgs` were never applied. Directly visible in character creation flows that depend on menu character recomputation.

**Remediation:** After setting `CurrentMenu`, invoke `ModifyMenuCharacters()` before returning `true`, or call the underlying method through a transpiled/reflective helper that preserves vanilla side effects while guarding the missing-key lookup.

### [MEDIUM] Banner asset load failures keep rendering previous faction's banner

**File:** `BannerWidget.cs:105-116`

**TAOM code:** Changing `BannerImage` resets `_textureLoaded`, `_loadFailed`, `_loadedSprite` but never clears the inherited `Sprite`. If the next lookup misses (`LoadTextureFromPath` returns null), the widget logs and bails without assigning a new sprite, then still calls `base.OnRender(...)`.

**Evidence:** A bad/missing banner asset for faction B continues showing faction A's banner after selection changed. Fails misleadingly rather than failing blank.

**Remediation:** Clear `Sprite` immediately when `BannerImage` changes and on load failure. If a fallback is desired, set an explicit neutral placeholder instead of leaving the previous runtime sprite attached.

## Observations

- Dynamic CultureStageView target resolution is valid for v1.3.15 but brittle and currently silent on miss — consider adding a log warning in `Prepare()` failure paths
- The stale-sprite reset pattern also exists in `FactionImageWidget` — audit for the same bug
- PolygonWidget O(n²) hover resolution is acceptable for 16 factions but would need optimization if faction count grows significantly

## Recommended Next Steps

1. Restore `ModifyMenuCharacters()` call in the TrySwitchToNextMenu prefix path
2. Clear `Sprite` on `BannerImage` change and on load failure in `BannerWidget`
3. Audit `FactionImageWidget` for same stale-sprite issue
4. Verify the no-selection narrative path in-game after fix, especially cultures that previously triggered `KeyNotFoundException`
