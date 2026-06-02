# Codex Adversarial Review — CareerSystem Switch Picker + Effect-Scope Badges — 2026-06-01

(Full review extracted from background task `bo71dfzxs` transcript; `--output-last-message` overwrote the in-place save.)

Build: not rerun by this reviewer. Prompt reports `./build.ps1 -RunTests` = 2894 passed / 2 skipped / 0 failed.

## Findings

### HIGH

**[HIGH]** `Main/Features/CareerSystem/UI/GauntletCareerScreen.cs:68` — GameState lifecycle — `switchMode` is written after vanilla has already constructed the screen, so the dialogue opens the normal career screen instead of the switch picker — **Fix:** move the mode read out of the screen constructor and into `OnInitialize`, or construct `CareerScreenGameState` with the flag already set before `HandleCreateState` runs. Rework-introduced.

Evidence: TAOM calls `CreateState<CareerScreenGameState>()` at line 68, sets `state.IsSwitchMode = switchMode` at line 69, and only then pushes the state. But vanilla `GameStateManager.CreateState<T>()` constructs the state and immediately calls `HandleCreateState`; `HandleCreateState` calls all `IGameStateManagerListener.OnCreateState` listeners. Decompiling `TaleWorlds.MountAndBlade.View.Screens.GameStateScreenManager` shows `OnCreateState` calls `CreateScreen(gameState)`, and `CreateScreen` uses `Activator.CreateInstance(type, state)`. TAOM's `GauntletCareerScreen` constructor reads `_isSwitchMode = state?.IsSwitchMode ?? false` at line 48, before line 69 has ever run. Runtime result: `CareerSwitchDialogueBehavior.OpenCareerSwitchScreen` calls `OpenCareerScreen(switchMode: true)` at `CareerSwitchDialogueBehavior.cs:85`, but `_isSwitchMode` is false, `_heroAdapter` is null, and `CareerScreenVM` is created in normal mode.

**[HIGH]** `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml:363` — GUI brush registry — `Popup.GreenButton` and `Popup.GreenButton.Text` do not exist in vanilla v1.4.5 brushes, so the switch target Choose button risks rendering unstyled or with missing text styling — **Fix:** use existing vanilla brushes such as `Popup.Done.Button.NineGrid` and `Popup.Button.Text`, or add TAOM-owned brush definitions and register them. Rework-introduced.

Evidence: the installed Native brush file `Modules/Native/GUI/Brushes/Popup.xml` defines `Popup.Button.Base`, `Popup.Done.Button`, `Popup.Done.Button.NineGrid`, `Popup.Cancel.Button`, `Popup.Delete.Button`, and `Popup.Button.Text`; it does not define `Popup.GreenButton`. A broad installed-file grep for `Popup.GreenButton` found only TAOM's deployed `CareerScreen.xml` copy.

### LOW

**[LOW]** `Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs:67` — Effect-scope clarity — `EffectScopeTooltip` and its two localization strings are never bound by the prefab, so the tooltip half of the feature is dead and passive bullets remain visually unlabelled — **Fix:** bind `@EffectScopeTooltip` to the badge/description tooltip surface, or remove the property and strings if the intended UX is badge-only. Rework-introduced.

**[LOW]** `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml:318` — Switch picker empty-state — the picker panel is gated on `@IsSwitchMode` instead of the VM's `@IsBrowsingTargets`, so a forced or stale switch-mode open shows an empty modal with only close controls — **Fix:** either gate the target list/panel on `@IsBrowsingTargets` and add an explicit empty-state message, or make `OpenCareerScreen(switchMode: true)` refuse to open when the rebuilt target list is empty. Rework-introduced.

## Suspect Disposition

1. `Popup.GreenButton` brush: **CONFIRMED**. Finding HIGH above.
2. Nested `ListPanel` item command binding: **DISPUTED**. Native `BannerEditor.xml:36-38` binds `Command.Click="ExecuteSelectIcon"` inside an item template. `PrefabDatabindingExtension` creates a child `GauntletView` for item templates and binds commands against that view model path.
3. Empty-state picker gating: **CONFIRMED** as an edge-case UX gap. Finding LOW above.
4. Unused `ICareerSwitchService` constructor parameter in `CareerSwitchDialogueBehavior`: **DISPUTED** as a runtime bug. Lines 22 and 30-32 document the compatibility reason. DryIoc still validates and injects the dependency; this is dead dependency surface, not broken behavior.
5. `TextObject` allocation per computed getter: **DISPUTED** as a hot-path issue. `ViewModel.RefreshValues()` is empty by default in vanilla and the Career screen is not a mission tick path. The real issue is that `EffectScopeTooltip` is not bound at all.
6. GameState flag race: **CONFIRMED**. Finding HIGH above.
7. Same-career rejection bypass when `hero.StringId` is null/empty: **DISPUTED** for production. `Hero.MainHero.StringId` is expected to be populated; the guard skips only invalid mock/adaptor state. `SwitchCareer(heroStringId, hero, newCareerStringId)` still uses the explicit `heroStringId` for mutation.
8. Dialogue condition caching: **DISPUTED**. Vanilla conversation option collection re-runs sentence conditions each time options are gathered.

## Vanilla Code Checked

### `GameStateManager.CreateState<T>()` / `PushState(state)`

Source: `E:\Decompiled_Bannerlord\Core\TaleWorlds.Core\TaleWorlds.Core\GameStateManager.cs:163-183`:

```csharp
public T CreateState<T>() where T : GameState, new()
{
    T val = new T();
    HandleCreateState(val);
    return val;
}

private void HandleCreateState(GameState state)
{
    state.GameStateManager = this;
    foreach (IGameStateManagerListener listener in _listeners)
        listener.OnCreateState(state);
}
```

Required screen-manager trace from installed `TaleWorlds.MountAndBlade.View.dll`:

```csharp
public ScreenBase CreateScreen(GameState state)
{
    ...
    object? obj = Activator.CreateInstance(type, state);
    return (ScreenBase)((obj is ScreenBase) ? obj : null);
}

void IGameStateManagerListener.OnCreateState(GameState gameState)
{
    ScreenBase val = CreateScreen(gameState);
    gameState.RegisterListener((IGameStateListener)(object)((val is IGameStateListener) ? val : null));
}
```

**Conclusion: screen construction is synchronous during `CreateState`, not deferred until `PushState`.**

### `Brush="Popup.GreenButton"`

Installed Native `Popup.xml` defines `Popup.Button.Base`, `Popup.Done.Button`, `Popup.Done.Button.NineGrid`, `Popup.Cancel.Button`, `Popup.Delete.Button`, and `Popup.Button.Text`. No installed vanilla brush file defines `Popup.GreenButton` or `Popup.GreenButton.Text`.

## Summary

CRITICAL: 0 | HIGH: 2 | MEDIUM: 0 | LOW: 2

VERDICT: **ISSUES FOUND**
