# UIExtenderEx Fork

## Overview

43-file fork of Bannerlord.UIExtenderEx v2.13.2 compiled into `TAOM.Dependencies.dll`. Provides ViewModel mixins, prefab XML patching, and custom brush/widget factory support with zero external BUTR module dependencies.

## Why Forked

UIExtenderEx was an external module requiring separate player installation. By forking the source, TAOM controls its own UI extension infrastructure and eliminates version coupling with BUTR's release cycle. The fork also removes dependencies on ButterLib and Bannerlord.ModuleManager.

## What It Does

5 system-level Harmony patches applied in UIExtender's static constructor:

| Patch | Target | Purpose |
|-------|--------|---------|
| UIConfigPatch | `UIConfig.DoNotUseGeneratedPrefabs` setter | Blocks pre-generated prefab optimization so XML overrides work |
| ViewModelPatch | `ViewModel` ctor + `ExecuteCommand` | Enables mixin injection into ViewModels |
| WidgetPrefabPatch | `WidgetPrefab.LoadFrom` | Transpiles prefab loading to inject XML modifications |
| BrushFactoryManager | `BrushFactory.GetBrush` + `Brushes` getter | Makes custom brushes discoverable |
| WidgetFactoryManager | `WidgetFactory.CreateBuiltinWidget` + `GetCustomType` + more | Makes custom widget types instantiable |

## TAOM Usage

### ViewModel Mixins (3)

| Mixin | Target ViewModel | What it adds |
|-------|-----------------|-------------|
| `CharacterDeveloperCareerMixin` | `CharacterDeveloperVM` | Career button + screen |
| `SpecialResourceMapBarMixin` | `MapInfoVM` | Resource bar on map HUD |
| `TimeAccelerationMixin` | `MapTimeControlVM` | Extra fast-forward button |

### Prefab Patches (7)

| Patch | Target Prefab | Type |
|-------|--------------|------|
| `CareerButtonPrefab` | CharacterDeveloper | Insert (append button) |
| `SpecialResourcePrefab` | MapBar | Insert (replace icon widget) |
| `PrefabCenterPanel` | MapBar | SetAttribute (widen) |
| `PrefabFastForwardButton` | MapBar | SetAttribute (shift left) |
| `PrefabPlayButton` | MapBar | SetAttribute (shift left) |
| `PrefabPauseButton` | MapBar | SetAttribute (shift left) |
| `PrefabInsertExtraFastForward` | MapBar | Insert (append button) |

## Changes From Original

| Change | Why |
|--------|-----|
| Harmony ID `"com.taom.uiextender"` | Avoids collision if external UIExtenderEx also installed |
| `ModuleInfoHelper` replaced with `ModulePathHelper` | Eliminates 22-file Bannerlord.ModuleManager dependency |
| `UIExtenderExSettings` stubbed (`DumpXML => false`) | Eliminates MCM/SettingsSubModuleTags dependency |
| `AccessTools2.cs` / `HarmonyExtensions.cs` deleted | Provided by Harmony.Extensions NuGet (3.2.0.77) |
| CS8619 nullability warnings fixed | Decompiler artifacts |
| Obsolete Prefabs v1 code paths removed | Dead code (TAOM uses Prefabs2) |
| `Path` disambiguated in `ModulePrefabExtensionInsertPatch` | `System.IO.Path` vs `TaleWorlds.Engine.Path` |

## Key Files

| File | Purpose |
|------|---------|
| `Dependencies/ThirdParty/UIExtenderEx/Core/UIExtender.cs` | Entry point, static constructor with 5 patches |
| `Dependencies/ThirdParty/UIExtenderEx/Core/UIExtenderRuntime.cs` | Per-module runtime (Create/Register/Enable) |
| `Dependencies/ThirdParty/UIExtenderEx/Core/UIExtenderExSettings.cs` | Stubbed settings (DumpXML=false) |
| `Dependencies/ThirdParty/UIExtenderEx/BUTR/ModulePathHelper.cs` | Assembly-location path walker |
| `Dependencies/ThirdParty/UIExtenderEx/Components/PrefabComponent.cs` | Prefab patch registration + dispatch |
| `Dependencies/ThirdParty/UIExtenderEx/Components/ViewModelComponent.cs` | ViewModel mixin lifecycle |
| `Dependencies/ThirdParty/UIExtenderEx/BUTR/WrappedPropertyInfo.cs` | Gauntlet binding redirect |
| `Dependencies/ThirdParty/UIExtenderEx/BUTR/WrappedMethodInfo.cs` | Command dispatch redirect |

## Namespaces (Preserved)

All original namespaces kept to avoid changing `using` statements:
- `Bannerlord.UIExtenderEx.*` — core + attributes + patches + prefabs + viewmodels
- `HarmonyLib.BUTR.Extensions` — AccessTools2 (from NuGet)
- `Bannerlord.BUTR.Shared.Utils` — WrappedPropertyInfo/MethodInfo
- `Bannerlord.BUTR.Shared.Helpers` — ModulePathHelper
- `Bannerlord.BUTR.Shared.Extensions` — DictionaryExtensions

## How-To: Update From Upstream

1. Decompile latest UIExtenderEx: `ilspycmd <dll> -p -o <output>`
2. Diff against `Dependencies/ThirdParty/UIExtenderEx/`
3. Port changes while preserving: Harmony ID (`com.taom.uiextender`), `ModulePathHelper`, `UIExtenderExSettings` stub
4. Rebuild and test in-game
