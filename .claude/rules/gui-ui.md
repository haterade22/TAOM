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