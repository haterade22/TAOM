# MCM (Mod Configuration Menu) Reference

## What It Was

Bannerlord.MCM v5 was BUTR's in-game settings framework. It provided an options screen accessible from the game's escape menu where players could configure mod settings via sliders, toggles, and dropdowns — all defined declaratively via C# attributes.

## What It Accomplished

### 1. Attribute-Based Settings Declaration
Mods defined settings by decorating properties on a class inheriting `AttributeGlobalSettings<T>`:

```csharp
public class MySettings : AttributeGlobalSettings<MySettings>
{
    [SettingPropertyGroup("Combat")]
    [SettingPropertyFloatingInteger("Damage Multiplier", 0.5f, 3.0f, "#0.0")]
    public float DamageMultiplier { get; set; } = 1.0f;
}
```

MCM attributes:
- `[SettingPropertyBool]` — checkbox toggle
- `[SettingPropertyInteger]` — integer slider with min/max
- `[SettingPropertyFloatingInteger]` — float slider with min/max and format string
- `[SettingPropertyGroup]` — visual grouping with nesting support
- `[SettingPropertyDropdown]` — selection from a list

### 2. Automatic Settings Discovery
MCM scanned all loaded assemblies at startup for classes inheriting `AttributeGlobalSettings<T>`. No manual registration needed — just define the class and MCM finds it.

### 3. Singleton Instance Pattern
`AttributeGlobalSettings<T>` provided a static `Instance` property populated by MCM's framework. Consumers accessed settings via:
```csharp
MySettings.Instance?.DamageMultiplier ?? 1.0f
```

### 4. JSON Persistence
Settings were serialized to JSON and stored at `%AppData%/Mount and Blade II Bannerlord/Configs/{ModName}/`. MCM handled load/save automatically — mods never called save methods directly.

### 5. In-Game UI Rendering
MCM registered an options screen via Bannerlord's `MBOptionScreen` module. The UI was built dynamically from the attribute metadata — groups became collapsible sections, ranges became sliders, bools became checkboxes.

### 6. Settings Versioning
MCM supported format versioning via `FormatType` property (e.g., `"json2"`). This allowed migrating settings between mod versions.

## What TAOM Used

TAOM used MCM for **one purpose**: 29 configurable properties in `TaomSettings`. No events, no versioning, no presets, no per-save settings. The complete API surface was:

- `AttributeGlobalSettings<TaomSettings>` base class
- `[SettingPropertyBool]`, `[SettingPropertyInteger]`, `[SettingPropertyFloatingInteger]` attributes
- `[SettingPropertyGroup]` for organization
- `TaomSettings.Instance?.Property ?? default` access pattern

## How TAOM Replaced It

### What We Built
`Main/Features/TaomSettings.cs` — a 100-line plain POCO singleton:
- Properties with default values (no attributes needed)
- `LoadFrom(path)` — reads JSON via `Newtonsoft.Json`, returns defaults on any error
- `SaveTo(settings, path)` — writes formatted JSON
- `Initialize(moduleDataPath)` — called from `SubModule.OnSubModuleLoad()`
- `Reset()` — called from `SubModule.OnSubModuleUnloaded()`

### What We Lost
- **In-game UI** — players now edit `ModuleData/configs/taom_settings.json` directly. Acceptable for a total conversion mod. Future enhancement: build a native Gauntlet settings screen.
- **Automatic discovery** — settings must be explicitly initialized in SubModule.cs.
- **Range validation** — MCM enforced min/max from attributes. Our JSON has no validation. Invalid values flow through to runtime.

### What We Gained
- **Zero external dependencies** — no ButterLib, no MCM, no MBOptionScreen module
- **Crash-proof** — `LoadFrom` never throws (catches all exceptions, returns defaults)
- **Faster startup** — no reflection scanning of all assemblies
- **Debuggable** — a JSON file you can read and edit vs a framework with 50K LOC

## How to Maintain

### Adding a Setting
1. Add property with default to `Main/Features/TaomSettings.cs`
2. Add key to `Main/_Module/ModuleData/configs/taom_settings.json`
3. Access: `TaomSettings.Instance?.NewSetting ?? defaultValue`
4. Add assertion to `TaomSettingsTests.AllDefaults_MatchExpectedValues`

### If MCM Updates Break Other Mods
Not our problem anymore — TAOM doesn't depend on MCM.

### If We Want In-Game UI Later
Build a native Gauntlet settings screen (similar to `GauntletCareerScreen`):
1. Create a `SettingsScreenVM` with properties bound to `TaomSettings`
2. Create a Gauntlet XML prefab with sliders/toggles
3. Add a menu option to open it (Harmony patch on escape menu)
4. Call `TaomSettings.SaveTo()` on close

MCM source (for reference): https://github.com/Aragas/Bannerlord.MBOptionScreen
