---
paths:
  - "Main/**/*Mixin*.cs"
  - "Main/**/*Prefab*.cs"
  - "Main/**/*Widget*.cs"
  - "Main/**/*VM.cs"
  - "Main/**/*ViewModel*.cs"
  - "Client/**/*Mixin*.cs"
  - "Client/**/*Prefab*.cs"
  - "Client/**/*Widget*.cs"
  - "Client/**/*VM.cs"
  - "Client/**/*ViewModel*.cs"
  - "Main/_Module/GUI/**"
---

# GUI / UI / Sprite Rules

A new or changed screen also passes the [Gauntlet screen checklist](../../docs/reference/gauntlet-screen-checklist.md)
in game: empty, many and long-text states, disabled reasons, a way out, consistency with vanilla.

## Sprite AND Brush References (MANDATORY)

Before writing ANY `Sprite="X"` in XML or `GetSprite("X")` in C#:

Use the exact registered name, with no module prefix: a PNG at `SpriteParts/ui_taom/CareerSystem/foo.png` is `CareerSystem\foo`, NOT `TAOM\CareerSystem\foo`. Find the `<Name>` in `Main/_Module/GUI/TAOMSpriteData.xml`. Gate for `Sprite=` in a prefab or PrefabExtension: `PrefabSpriteTests` (vanilla and runtime sprites count). `GetSprite("X")` in C# is not gated, so check that name by hand.

**Why:** Sprite="TAOM\CareerSystem\career_button_placeholder" failed silently (blank button, no crash, no log) because the registered name was "CareerSystem\career_button_placeholder". This class of bug is invisible without in-game testing.

**Brushes and attributes.** Gauntlet drops an unknown `Brush=`, `Brush.<Property>` or widget attribute silently. Run `PrefabExtensionBrushTests`, not a grep (a brush in a vanilla file TAOM replaces whole never loads). Gates: `EveryGuiPrefabBrush_IsDeclaredByAModule`, `EveryGuiPrefabBrushProperty_NamesABrushProperty`, `EveryPrefabAttribute_NamesASettableWidgetProperty`. In a shipped prefab delete a failing attribute: players never saw it apply. In new markup fix it and look in game.

### Adding a NEW sprite (not just referencing an existing one)

A loose PNG plus a hand-added manifest entry renders blank. Run the bake pipeline in [gui-sprite-system.md](../../docs/features/gui-sprite-system.md), verify both the bake and the in-game render, and say so in the `Not-tested:` trailer.

## GUI Prefab Clones Go Stale Across Engine Versions (MANDATORY)

TAOM ships full `<Prefab>` clones of many vanilla prefabs. Vanilla renames widget attributes between versions; a stale clone keeps the old one and the widget silently mis-renders. A prefab clone goes stale across versions: diff it against installed vanilla before editing and after an engine bump; gates and method in [gui-prefab-clones.md](../../docs/reference/gui-prefab-clones.md). Only the running game confirms a prefab renders.

A clone can also stop consuming an engine signal with every attribute still valid: moving the `Sprite` off the widget the engine writes `Color` / `AlphaFactor` / `ColorFactor` to (vanilla's `SettlementNameplateItemWidget`) leaves those writes with nothing to draw, because `Widget.Render` never passes them to children. TAOM's nameplate redesign lost relation colour and plate transparency this way for five months (#591). When a clone re-parents visuals, list what the engine writes on the original element and make a custom container widget consume it: `docs/features/settlement-nameplate-relation.md`.

## UIExtenderEx PrefabExtension Safety (MANDATORY)

Before injecting into ANY vanilla prefab container:

1. **Research the target container** — decompile vanilla code that accesses the container's children
2. **Check for hardcoded indexing** — does vanilla code do `children[i]` with a fixed index?
3. **Check for typed iteration** — does vanilla code cast all children to a specific type?
4. **Check for count assumptions** — does vanilla code assume `children.Count == N`?

If ANY of these are true: **do NOT inject children into that container**. Use bound `[DataSourceProperty]` on a ViewModel mixin + inject into a DIFFERENT container that's safe.

**Why:** the four checks above are the general rule and they stand. Any data-bound `ListPanel` whose contents vanilla indexes by position is unsafe to grow.

`PrefabInsertPositionTests` fails a Child insert without its own Index or not on `/Children`, and an Append or Prepend on `/Children`.

**Safe pattern:**
```
[ViewModelMixin] → [DataSourceProperty] bindings
[PrefabExtension] → inject widget into a NON-data-bound container, bind to mixin properties
```

**Hover targets:** a `HintWidget` relays only its parent's hover, and `EventManager.CollectEnableWidgetsAt` enters a child only when it is enabled and the pointer is inside its own `AreaRect`. So a hint's parent must be enabled and lie inside every ancestor's rectangle (no offset past a parent's edge), with `DoNotPassEventsToChildren` when it has enabled children. Pin the shape with a structural test and hover it in game (`docs/reviews/lessons/localization-ui.md`, #877). `PrefabHintParentGateTests` fails any in-code prefab hint whose parent sets `DoNotAcceptEvents="true"` (#910, the third repeat).

**Discouraged, on ownership grounds:**
```
mapInfo.SecondaryInfoItems.Add(new MapInfoItemVM(...))
```
A mod mutating a vanilla ViewModel collection it does not own can have its addition dropped without warning: `MapInfoVM.CreateItems()` calls `OnFinalize()` on every item, `Clear()`s both `PrimaryInfoItems` and `SecondaryInfoItems`, and re-adds only vanilla's nine. Anything a mixin added is gone. In v1.4.8 and v1.5.5 that method is called from exactly one site, the constructor (`MapInfoVM.cs:138`), before any mixin exists, so the hazard is real in shape but dormant in practice. If you add anyway, do it on every refresh behind `Contains`, so a rebuilt list gets the item back, and remove it when it no longer applies, as `SpecialResourceMapBarMixin` does (#958, #977). Never skip the first refresh: `MapBarVM.Initialize` runs it before the view loads and the next one comes 2 s later.

## Custom GauntletLayer Input Wiring (MANDATORY)

When adding a custom `GauntletLayer` overlay that contains interactive widgets (buttons, click bindings, drag handles) to **either** a `ScreenBase` (via Harmony postfix on `OnInitialize`) **or** a `MissionScreen` (via `MissionView.OnMissionScreenInitialize`, `MissionLogic` overlay attach, or any other feature-overlay attach path), the layer MUST call `_layer.InputRestrictions.SetInputRestrictions(...)` after construction or it paints but never registers with the screen's input dispatcher — mouse clicks pass through. Pair with `_layer.InputRestrictions.ResetInputRestrictions()` in the teardown path before `RemoveLayer`.

**In a battle, pass `(false, InputUsageMask.MouseButtons)`, never the parameterless call** (#970). The defaults are a visible cursor and all input: a visible cursor stops vanilla attack and block (`MissionMainAgentController` runs them only while it is hidden) and turns the camera into an edge scroll. Put `DoNotAcceptEvents="true"` on every non-button widget of the prefab, because any widget that accepts events wins the mouse buttons from the scene layer. Note that with the cursor hidden the engine pins it to the screen centre, so a battle overlay button can be clicked only while another view shows a cursor (the Esc menu).

The input dispatcher does not distinguish between `ScreenBase` and `MissionScreen` hosts for this purpose. Both require explicit input registration. Display-only overlays with zero interactive widgets (no `ButtonWidget`, no `Command.Click`, no `AcceptEvents`) are the only exception; verify with grep before claiming it.

**Correct pattern (MissionScreen overlay via MissionView):**
```csharp
public override void OnMissionScreenInitialize()
{
    base.OnMissionScreenInitialize();
    _vm = new MyOverlayVM(...);
    _layer = new GauntletLayer("GauntletLayer", zOrder, false);
    _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.MouseButtons); // ← REQUIRED; cursor stays hidden
    _layer.LoadMovie(prefabName, _vm);
    MissionScreen.AddLayer(_layer);
}

public override void OnMissionScreenFinalize()
{
    if (_layer != null)
    {
        _layer.InputRestrictions.ResetInputRestrictions();  // ← REQUIRED before Remove
        MissionScreen?.RemoveLayer(_layer);
        _layer = null;
    }
    base.OnMissionScreenFinalize();
}
```

A `ScreenBase` overlay does the same in an `OnInitialize` postfix and an `OnFinalize` prefix, with the parameterless call.

**Do NOT** also set `IsFocusLayer = true` on a parasitic overlay, which steals Esc/Tab/hotkey focus from the live vanilla screen underneath. `IsFocusLayer = true` is appropriate ONLY for full-screen replacements (`GauntletCareerScreen`, `GauntletFiefManagementScreen`), where the layer IS the screen. For overlays, the parent widget in the prefab should have `DoNotAcceptEvents="true"` so non-button areas pass clicks through to vanilla.

A sibling that works by hotkey is no proof its mouse path works (`rca-companiontactics-overlay-input-2026-05-25.md`).

## TaleWorlds VM property setters: verify no-op early returns (MANDATORY)

Before writing `vm.X = value` on any TaleWorlds-owned ViewModel property (especially anything ending in `Index`, `SelectedItem`, `Selected*`, or `Current*`), decompile the setter to confirm whether it returns early when `value == _backingField`. Many setters are guarded:

```csharp
set
{
    if (value != _backingField)  // ← early return when no change
    {
        _backingField = value;
        // ... real state updates: SelectedItem, IsSelected, _onChange.Invoke ...
    }
}
```

If the setter is guarded, **mutating the underlying collection then re-setting the property to the same value is a no-op** — the dependent state (`SelectedItem`, downstream `_onChange` callbacks, child-VM `IsSelected` flags) does not refresh, leaving stale references to objects that are no longer in the collection.

**Concrete pattern caught in the wild (Codex Review #30, 2026-05-04):** TAOM's CustomBattles filter cleared `CharacterSelectionGroup.ItemList`, populated 3 new `CharacterItemVM`s, then set `SelectedIndex = 0`. Because vanilla left `_selectedIndex` at `0` already, the setter's `if (value != _selectedIndex)` guard short-circuited, leaving `SelectedItem` pointing at a `CharacterItemVM` that had just been removed. The Custom Battle launched with the previously-selected commander instead of the filtered first one — visible faction picker disconnected from the actual battle commander.

**Correct patterns (in order of preference):**
1. **Use the type's own `Refresh()` method** when one exists (e.g., `SelectorVM<T>.Refresh(IEnumerable<T>, int, Action<SelectorVM<T>>)`). Vanilla `Refresh` resets the private backing field directly before re-setting, which is the documented escape hatch.
2. **Mirror `Refresh()`'s reset trick via reflection** when the public API doesn't fit. Cache the `FieldInfo` once at `Initialize()`, then `field.SetValue(vm, -1)` (or whatever sentinel the type uses) before assigning the real value. See `Main/Features/CustomBattles/Hooks/CommanderSelectorRebuilder.cs` for the canonical implementation.
3. **Avoid setting the same value back** — if you can detect the no-op case (read the current value first, only assign if different), you sidestep the trap entirely. But this only helps when there's no dependent state that needs refreshing.

**Do NOT** use the indirection of `prop = -1; prop = 0;` — many setters fire downstream callbacks (`_onChange.Invoke(this)`) that crash on the intermediate sentinel value (e.g., `OnCharacterSelection` dereferences `selector.SelectedItem.Character`, which is null at index -1).

**When this rule applies:** any C# that mutates TaleWorlds VM properties after construction (filter patches, refresh hooks, mid-mission UI updates). Read the setter with `taom-src` first; the guard is invisible at the call site.

## TaleWorlds VM property notification: prefer public setter over reflected field+notify (MANDATORY)

When you need to REPLACE an entire VM property's value (not just mutate the existing object), and the field is private but a public property wraps it, **always use the public property setter**. Do NOT reflect on the backing field and then try to fire the change notification yourself.

```csharp
// ❌ WRONG — silently breaks UI rebinding
_raceSelectorField.SetValue(faceGenVM, newSelector);
_onPropertyChangedWithValueMethod?.Invoke(faceGenVM, new object[] { newSelector, "RaceSelector" });

// ✅ RIGHT — vanilla setter handles both field assignment and notification
faceGenVM.RaceSelector = newSelector;
```

The `OnPropertyChangedWithValue` method on `ViewModel` is **generic** (`OnPropertyChangedWithValue<T>(T value, string propertyName) where T : class`). `AccessTools.Method` looking up by `(typeof(object), typeof(string))` returns `null` because the open generic's signature is `(T, string)`, not `(object, string)`. The reflected invoke would fail at runtime — but with a `?.` null-conditional the failure is silent. Result: the field is replaced internally, but Gauntlet's `GauntletView.OnViewModelPropertyChangedWithValue` is never called, `RefreshBindingWithChildren` never fires, and the UI stays bound to the previous value forever.

Initial construction can mask this — `LoadMovie("...", DataSource)` reads the field directly after construction. Subsequent changes are where the bug manifests: any `Refresh(true)` in vanilla VM code that re-creates the property's value will rebind to the vanilla version, NOT your replacement.

**Rule:** before reflecting on a private field, search for a public property that wraps it (`grep -n "public.*get { return _fieldName }\|return _fieldName;"`). If the property exists, use its setter. The setter handles both the field assignment AND the correctly-typed change notification. Only reflect when no such property exists (e.g., the field is `private` with no wrapper).

**Concrete pattern caught in the wild (Codex Review #33, 2026-05-06):** `FaceGenRaceSelectorRebuilder.Apply` mutated `_raceSelector` via reflection, then attempted `OnPropertyChangedWithValue(object, string)` invocation. The lookup returned `null`. Field was replaced; UI dropdown stayed bound to vanilla's unfiltered selector. First Refresh appeared correct (initial construction reads field), but every race-change rebound to vanilla. Fixed by replacing both lines with `faceGenVM.RaceSelector = newSelector`.

**Sister rule:** the setter-guard rule above (no-op early returns) covers the case where you assign the SAME value. This rule covers the case where you assign a DIFFERENT value but bypass the setter. Both must be respected.

## ViewModel Binding Rules

- `@PropertyName` in XML must EXACTLY match `[DataSourceProperty]` name (case-sensitive)
- `Command.Click="ExecuteX"` requires a public `void ExecuteX()` method
- `{CollectionName}` requires `MBBindingList<T>`, NOT `List<T>`
- Every `[DataSourceProperty]` that is set must have a corresponding XML binding — unused properties are dead code

## File Conventions

| File Type | Location | Naming |
|---|---|---|
| Prefab XML | `Main/_Module/GUI/PreFabs/<Feature>/` | `<ScreenName>.xml` |
| ViewModel | `Main/Features/<Feature>/UI/` | `<Name>VM.cs` |
| Mixin | `Client/Features/<Feature>/UI/` (TAOM.Client registers every UIExtenderEx extension, ADR-013; `ClientIsolationGateTests` fails one in TAOM.dll) | `<Target>Mixin.cs` |
| Prefab extension | `Client/Features/<Feature>/UI/` (same rule) | `<Feature>Prefab.cs` |
| Harmony patch that names a TAOM.Client type | `Client/Features/<Feature>/Hooks/` (the patch index reads both assemblies) | `Patch<N>_<Target>.cs` |
| Custom widget | `Client/Features/<Feature>/Widgets/` (TAOM.Client, ADR-013; `ServerLoadGateTests` fails a new widget in TAOM.dll) | `<Name>Widget.cs` |
| MapView, map navigation element, map conversation provider | `Client/Features/<Feature>/`, behind a core interface with a null version (ADR-013) | `<Name>MapView.cs` |
| Map-scene visual service (entities on MapScreen.MapScene) | `Client/Features/<Feature>/` with the source's relative path, behind the core interface with a `Null<Name>` registered by the feature's IoC and replaced in `TaomClientComposition` (ADR-013) | `<Name>VisualService.cs` |
| MissionView, `[DefaultView]` view, visual order provider, CustomBattle game manager, WinForms window | `Client/Features/<Feature>/` with the source's relative path (TAOM.Client, ADR-013; a new one in TAOM.dll fails `ServerLoadGateTests`) | the type name; a MissionView is a `[DefaultView]` the engine builds (never added through `AddTaomBehavior`, which runs after the screen registers views: #970); other types sit behind a core interface with a null version (`IAutonomousMovementController`) |
| Source sprites | `GUI/SpriteParts/ui_taom/<Feature>/` | `<sprite_name>.png` |