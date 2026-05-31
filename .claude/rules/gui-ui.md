---
paths:
  - "Main/**/*Mixin*.cs"
  - "Main/**/*Prefab*.cs"
  - "Main/**/*Widget*.cs"
  - "Main/**/*VM.cs"
  - "Main/**/*ViewModel*.cs"
  - "Main/_Module/GUI/**"
---

# GUI / UI / Sprite Rules

## Sprite References (MANDATORY)

Before writing ANY `Sprite="X"` in XML or `GetSprite("X")` in C#:

1. **Read `Main/_Module/GUI/TAOMSpriteData.xml`** — find the `<Name>` entry for your sprite
2. **Use the EXACT registered name** — sprite ID = filename without extension, prefixed by subfolder path using backslashes
3. **Do NOT add module prefixes** — a PNG at `SpriteParts/ui_taom/CareerSystem/foo.png` is `CareerSystem\foo`, NOT `TAOM\CareerSystem\foo`
4. **Verify the PNG exists** — check `GUI/SpriteParts/ui_taom/<subfolder>/` before referencing

**Why:** Sprite="TAOM\CareerSystem\career_button_placeholder" failed silently (blank button, no crash, no log) because the registered name was "CareerSystem\career_button_placeholder". This class of bug is invisible without in-game testing.

### Adding a NEW sprite (not just referencing an existing one)

The 4 steps above are for *referencing* a sprite that's already baked. **Adding a new sprite PNG is a different, multi-step process** — a loose PNG + a hand-added `TAOMSpriteData.xml` entry alone renders **BLANK** in the player client (no error, no log). Follow the canonical workflow in [docs/features/gui-sprite-system.md](../../docs/features/gui-sprite-system.md) ("The sprite-bake pipeline" + "Adding a New Sprite" + "Verifying a sprite"):

1. Drop the PNG at a sane size (`feedback_sprite_dimensions`). Do NOT hand-edit the manifest — the generator overwrites it.
2. **Run the sprite generator** (`bin/Win64_Shipping_wEditor/TaleWorlds.TwoDimension.SpriteSheetGenerator.exe`). It writes `AssetSources/GauntletUI/<cat>_<n>.png` + rewrites `GUI/<Module>SpriteData.xml`; the editor's downstream texture-compile pass then builds `Assets/GauntletUI/<cat>_<n>_tex.tpac` (the generator binary does not write the `.tpac` itself). **There is NO `pack0.tpac` for UI sprites.**
3. **Verify BOTH** (they fail independently): the **bake** (crop the sprite's `SheetX/Y` rect out of the regenerated sheet — pixels present?) AND the **render** (in-game — widget big enough + `Color` alpha high enough; **baked ≠ visible** — a thin sprite at ~22px / 27% alpha is invisible). Static review (incl. `/deep-review` + Codex) cannot certify a sprite renders — say so in the CHANGELOG `Not-tested:` line.

Memory: `feedback_sprite_atlas_baked_regen_required` (the career-pip case — baked correctly yet blank because the prefab drew it too small/faint).

## UIExtenderEx PrefabExtension Safety (MANDATORY)

Before injecting into ANY vanilla prefab container:

1. **Research the target container** — decompile vanilla code that accesses the container's children
2. **Check for hardcoded indexing** — does vanilla code do `children[i]` with a fixed index?
3. **Check for typed iteration** — does vanilla code cast all children to a specific type?
4. **Check for count assumptions** — does vanilla code assume `children.Count == N`?

If ANY of these are true: **do NOT inject children into that container**. Use bound `[DataSourceProperty]` on a ViewModel mixin + inject into a DIFFERENT container that's safe.

**Why:** Adding to `SecondaryInfoItems` caused `IndexOutOfRangeException` in vanilla's `HandlePanelSwitchingInput` (hardcoded positional indexing). This pattern applies to any data-bound `ListPanel` where vanilla code indexes by position.

**Safe pattern:**
```
[ViewModelMixin] → [DataSourceProperty] bindings
[PrefabExtension] → inject widget into a NON-data-bound container, bind to mixin properties
```

**Unsafe pattern:**
```
mapInfo.SecondaryInfoItems.Add(new MapInfoItemVM(...))  // NEVER DO THIS
```

## Custom GauntletLayer Input Wiring (MANDATORY)

When adding a custom `GauntletLayer` overlay that contains interactive widgets (buttons, click bindings, drag handles) to **either** a `ScreenBase` (via Harmony postfix on `OnInitialize`) **or** a `MissionScreen` (via `MissionView.OnMissionScreenInitializeFirstTime`, `MissionLogic` overlay attach, or any other feature-overlay attach path), the layer MUST call `_layer.InputRestrictions.SetInputRestrictions()` after construction or it paints but never registers with the screen's input dispatcher — mouse clicks pass through. Pair with `_layer.InputRestrictions.ResetInputRestrictions()` in the teardown path before `RemoveLayer`.

The v1.4.5 input dispatcher does NOT distinguish between `ScreenBase` and `MissionScreen` hosts for this purpose. Both require explicit input registration. Display-only overlays with zero interactive widgets (no `ButtonWidget`, no `Command.Click`, no `AcceptEvents` — e.g., the CareerSystem `AbilityHUD`) are the only exception; verify with grep before claiming it.

**Correct pattern (ScreenBase overlay via Harmony):**
```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(GauntletInventoryScreen), "OnInitialize")]
public static void OnInitialize_Postfix(GauntletInventoryScreen __instance)
{
    _layer = new GauntletLayer("GauntletLayer", zOrder, true);
    _layer.InputRestrictions.SetInputRestrictions();        // ← REQUIRED
    _layer.LoadMovie(prefabName, dataSourceVm);
    __instance.AddLayer(_layer);
}

[HarmonyPrefix]
[HarmonyPatch(typeof(GauntletInventoryScreen), "OnFinalize")]
public static void OnFinalize_Prefix(GauntletInventoryScreen __instance)
{
    if (_layer != null)
    {
        _layer.InputRestrictions.ResetInputRestrictions();  // ← REQUIRED before Remove
        __instance.RemoveLayer(_layer);
        _layer = null;
    }
}
```

**Correct pattern (MissionScreen overlay via MissionView):**
```csharp
public override void OnMissionScreenInitializeFirstTime()
{
    base.OnMissionScreenInitializeFirstTime();
    _vm = new MyOverlayVM(...);
    _layer = new GauntletLayer("GauntletLayer", zOrder, false);
    _layer.InputRestrictions.SetInputRestrictions();        // ← REQUIRED (same as ScreenBase)
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

**Do NOT** also set `IsFocusLayer = true` on a parasitic overlay — that steals Esc/Tab/hotkey focus from the live vanilla screen underneath. `IsFocusLayer = true` is appropriate ONLY for full-screen replacements (`GauntletCareerScreen`, `GauntletFiefManagementScreen`), where the layer IS the screen. For overlays, the parent widget in the prefab should have `DoNotAcceptEvents="true"` so non-button areas pass clicks through to vanilla.

**Why:** Two shipping bugs in 6 days:
1. **EquipPresets "Presets" inventory button** (2026-05-19, #202) — `ScreenBase` overlay missing `SetInputRestrictions()`. Click was a silent no-op. RCA: `docs/reviews/rca-equippresets-presets-button-silent-2026-05-19.md`.
2. **CompanionTactics OOB "Assign Heroes" + "Presets" + BattleActionBar mouse clicks** (2026-05-25, #225) — two `MissionScreen` overlays with the same defect. RCA: `docs/reviews/rca-companiontactics-overlay-input-2026-05-25.md`.

Bug #2 shipped past the rule written for bug #1 because the original rule scoped to ScreenBase only — that scoping was based on the wrong inference that `BattleActionBar` "worked" without `SetInputRestrictions()`. It didn't work; only the hotkey path worked, which masked the broken mouse path. **Lesson: when classifying a sibling as a working precedent, verify it works via the SAME input path you care about. A working alternative input path is not evidence the broken path also works.**

Feedback memory: `feedback_gauntlet_overlay_input_wiring.md`. **Rendering ≠ live.**

**Verification:** `taom-src` confirmed `GauntletLayer.InputRestrictions` is `TaleWorlds.ScreenSystem.InputRestrictions` (defined on base class `ScreenLayer`), and `SetInputRestrictions(bool isMouseVisible = true, InputUsageMask mask = InputUsageMask.All)` has both defaults supplied — parameterless call is valid in v1.4.5.

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

**When this rule applies:** Any C# file that mutates TaleWorlds VM properties post-construction (filter patches, refresh hooks, mid-mission UI updates). Decompile the setter via `ilspycmd` against the installed v1.4.5 DLL before writing the assignment. The guard on `_setter` is invisible at the call site.

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
| Mixin | `Main/Features/<Feature>/UI/` | `<Target>Mixin.cs` |
| Prefab extension | `Main/Features/<Feature>/UI/` | `<Feature>Prefab.cs` |
| Custom widget | `Main/Features/<Feature>/UI/` | `<Name>Widget.cs` |
| Source sprites | `GUI/SpriteParts/ui_taom/<Feature>/` | `<sprite_name>.png` |