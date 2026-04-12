# UIExtenderEx Reference

## What It Is

Bannerlord.UIExtenderEx v2.13.2 is BUTR's UI extension framework. It allows mods to inject custom properties, commands, and XML widgets into Bannerlord's Gauntlet UI system without replacing entire screens — multiple mods can extend the same screen without conflicting.

## How It Works — Deep Mechanics

### The 5 System Patches

UIExtenderEx applies these in its static constructor (before any mod code runs):

#### 1. UIConfigPatch
**Target:** `UIConfig.DoNotUseGeneratedPrefabs` (property setter)
**Mechanism:** Harmony prefix that returns `false` — blocks the setter entirely.
**Purpose:** Bannerlord can skip XML parsing and use pre-compiled prefab caches. This patch ensures XML is always parsed, which is required for prefab modifications to take effect.
**CRITICAL:** The setter must also be SET to `true` explicitly before Native loads. The patch only blocks it from being set back to `false`.

#### 2. ViewModelPatch
**Target:** `ViewModel` constructor + `ExecuteCommand`
**Mechanism:** 
- Constructor prefix: handles `BUTRViewModel` subclasses (skips default property map setup)
- ExecuteCommand prefix: redirects command execution through `ViewModelWrapper` for mixin commands
**Purpose:** Enables the ViewModel mixin system to intercept command dispatch.

#### 3. WidgetPrefabPatch
**Target:** `WidgetPrefab.LoadFrom(PrefabExtensionContext, WidgetAttributeContext, string path)`
**Mechanism:** Harmony **transpiler** that inserts a call to `ProcessMovie(path, xmlDocument)` immediately after `new WidgetPrefab()` but before the prefab is populated.
**Purpose:** Gives UIExtenderEx access to the raw `XmlDocument` mid-parse, allowing XPath-based modifications before the widget tree is built.
**Also creates:** A reverse patch `LoadFromDocument(context, attrContext, name, xmlDocument)` for creating prefabs from pre-built documents.

#### 4. BrushFactoryManager
**Target:** `BrushFactory.GetBrush(name)` + `BrushFactory.Brushes` getter
**Mechanism:** 
- GetBrush prefix: checks a custom brush dictionary before vanilla's `_brushes`
- Brushes postfix: appends custom brushes to the enumeration
- 5 blank transpilers on small methods to prevent JIT inlining (required for hooks to intercept)
**Purpose:** Makes mod-defined brushes discoverable by Gauntlet's rendering system. Without this, custom brush names resolve to null and widgets render blank.

#### 5. WidgetFactoryManager
**Target:** `WidgetFactory.CreateBuiltinWidget`, `GetCustomType`, `IsCustomType`, `GetWidgetTypes`, `OnUnload`
**Mechanism:**
- CreateBuiltinWidget prefix: instantiates mod-registered widget types via cached constructor delegates
- GetCustomType prefix: serves mod-registered prefab-based widget types
- IsCustomType prefix: reports mod types as custom
- GetWidgetTypes postfix: includes mod type names in enumeration
- OnUnload prefix: handles reference-counted cleanup for mod types
- 3 blank transpilers for anti-inlining
**Purpose:** Makes mod-defined C# widget classes (like `SpecialResourceSpriteWidget`) instantiable by Gauntlet's widget factory.

### The Runtime Flow

```
1. SubModule static ctor:
   Assembly.Load("TaleWorlds.Engine.GauntletUI")
   UIConfig.DoNotUseGeneratedPrefabs = true

2. First reference to UIExtender type:
   → Static ctor fires → 5 system patches applied

3. UIExtender.Create("TAOM"):
   → Allocates instance with module name

4. UIExtender.Register(assembly):
   → Scans assembly for [PrefabExtension] and [ViewModelMixin] decorated classes
   → For each [ViewModelMixin]: applies Harmony transpilers to the target ViewModel's
     constructor, RefreshValues, and OnFinalize methods
   → For each [PrefabExtension]: registers the patch action in PrefabComponent
   → All patches start DISABLED (enabled flag = false)

5. UIExtender.Enable():
   → Flips all enabled flags to true
   → ViewModel mixins now instantiate on next ViewModel construction
   → Prefab patches now apply on next prefab load
```

### ViewModel Mixin Mechanics

When a ViewModel (e.g., `CharacterDeveloperVM`) is constructed:

1. The transpiler-injected code calls `ViewModelComponent.InitializeMixinsForVMInstance(vm)`
2. For each registered mixin type targeting this VM:
   a. Instantiate the mixin via its constructor (receives the VM as parameter)
   b. Collect all `[DataSourceProperty]` properties from the mixin type
   c. Create `WrappedPropertyInfo` for each — redirects `GetValue`/`SetValue` to the mixin instance
   d. Collect all methods matching Execute* pattern
   e. Create `WrappedMethodInfo` for each — redirects `Invoke` to the mixin instance
   f. **Clone** the VM's `_propertiesAndMethods` dictionary (from the shared type-level cache)
   g. **Inject** the wrapped properties/methods into the cloned dictionary
   h. **Replace** the VM's `_propertiesAndMethods` field with the expanded clone
3. Gauntlet XML bindings (e.g., `@HasCareer`, `Command.Click="ExecuteOpenCareerScreen"`) now resolve through the wrapped entries to the mixin instance

When `RefreshValues()` is called on the VM:
- The transpiler-injected code calls `mixin.OnRefresh()` on all registered mixins
- Mixins update their properties and call `vm.OnPropertyChanged(name)` to notify Gauntlet

### Prefab Patching Mechanics

When `WidgetPrefab.LoadFrom()` is called:

1. The transpiler intercepts after `new WidgetPrefab()` but before population
2. Calls `PrefabComponent.ProcessMovieIfNeeded(movieName, xmlDocument)` on all runtimes
3. For each registered prefab patch targeting this movie name:
   a. **InsertPatch**: finds target node via XPath, inserts XML child (append/prepend/replace)
   b. **SetAttributePatch**: finds target node via XPath, modifies attribute values
4. The modified `XmlDocument` is then parsed into `WidgetTemplate` tree by vanilla code
5. Result: the widget tree contains the mod's additions transparently

## What TAOM Uses

### 3 ViewModel Mixins

| Class | Target | What It Adds |
|-------|--------|-------------|
| `CharacterDeveloperCareerMixin` | `CharacterDeveloperVM` | `HasCareer` property + `ExecuteOpenCareerScreen` command |
| `SpecialResourceMapBarMixin` | `MapInfoVM` | Resource item in `SecondaryInfoItems` collection |
| `TimeAccelerationMixin` | `MapTimeControlVM` | `IsExtraFastForwardActive` + `ExtraFastForwardHint` properties |

### 7 Prefab Patches

| Class | Movie | Type | What It Does |
|-------|-------|------|-------------|
| `CareerButtonPrefab` | CharacterDeveloper | Insert (append) | Adds career button to TopPanelParent |
| `SpecialResourcePrefab` | MapBar | Insert (replace) | Replaces IconBrushWidget with SpecialResourceSpriteWidget |
| `PrefabCenterPanel` | MapBar | SetAttribute | Widens CenterPanel to 500px |
| `PrefabFastForwardButton` | MapBar | SetAttribute | Shifts left -105px |
| `PrefabPlayButton` | MapBar | SetAttribute | Shifts left -145px |
| `PrefabPauseButton` | MapBar | SetAttribute | Shifts left -185px |
| `PrefabInsertExtraFastForward` | MapBar | Insert (append) | Adds fast-fast-forward button |

## How Our Fork Differs

| Change | Why | Location |
|--------|-----|----------|
| Harmony ID `"com.taom.uiextender"` | Avoid collision with external UIExtenderEx | `Core/UIExtender.cs` |
| `ModuleInfoHelper` replaced | Eliminates 22-file Bannerlord.ModuleManager dependency | `BUTR/ModulePathHelper.cs` |
| `UIExtenderExSettings` stubbed | Eliminates MCM dependency; DumpXML always false | `Core/UIExtenderExSettings.cs` |
| `AccessTools2` / `HarmonyExtensions` deleted | Provided by Harmony.Extensions NuGet | N/A |
| Obsolete Prefabs v1 code removed | Dead code (TAOM uses Prefabs2) | `Core/UIExtenderRuntime.cs` |
| CS8619 nullability warnings fixed | Decompiler artifacts | Various |
| `Path` disambiguated | `System.IO.Path` vs `TaleWorlds.Engine.Path` | `Prefabs/ModulePrefabExtensionInsertPatch.cs` |

## How to Update From Upstream

When BUTR releases a new UIExtenderEx version:

1. **Decompile** the new DLL:
   ```
   ilspycmd <new-dll> -p -o C:/tmp/uiextenderex-new/
   ```

2. **Diff** against our fork:
   ```
   diff -r C:/tmp/uiextenderex-new/Bannerlord.UIExtenderEx/ Dependencies/ThirdParty/UIExtenderEx/Core/
   ```

3. **Port changes** while preserving our modifications:
   - Keep Harmony ID `"com.taom.uiextender"`
   - Keep `ModulePathHelper` (don't restore `ModuleInfoHelper`)
   - Keep `UIExtenderExSettings` stub
   - Apply any new patches or bug fixes from upstream

4. **Rebuild and test** — build, run all 1055 tests, verify in-game.

5. **Check for new system patches** — if upstream adds a 6th system patch in the static constructor, add it to our fork.

## Key Files in Our Fork

```
Dependencies/ThirdParty/UIExtenderEx/
├── Core/UIExtender.cs              ← Entry point, static ctor with 5 patches
├── Core/UIExtenderRuntime.cs       ← Per-module Create/Register/Enable
├── Core/UIExtenderExSettings.cs    ← Stubbed (DumpXML=false)
├── Components/PrefabComponent.cs   ← Prefab patch registration + dispatch
├── Components/ViewModelComponent.cs ← ViewModel mixin lifecycle
├── Patches/                        ← The 5 system Harmony patches
├── Prefabs2/                       ← Current prefab patch base classes
├── ViewModels/BaseViewModelMixin.cs ← Mixin base class with OnRefresh/OnFinalize
├── ResourceManager/                ← BrushFactory + WidgetFactory patches
├── BUTR/ModulePathHelper.cs        ← Our replacement for ModuleInfoHelper
├── BUTR/WrappedPropertyInfo.cs     ← Gauntlet binding redirect
└── BUTR/WrappedMethodInfo.cs       ← Command dispatch redirect
```

UIExtenderEx source: https://github.com/BUTR/Bannerlord.UIExtenderEx
