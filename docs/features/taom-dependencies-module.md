# TAOM.Dependencies Module

## Overview

Pre-Native submodule that applies UIExtenderEx system Harmony patches before any game UI loads. Forces `UIConfig.DoNotUseGeneratedPrefabs = true` so TAOM's custom prefab overrides (nameplates, brushes, sprites) are always parsed from XML.

## Why This Exists

UIExtenderEx's 5 system patches (BrushFactory, WidgetFactory, UIConfig, WidgetPrefab, ViewModel) must be applied before Native loads any prefabs or brushes. TAOM loads after SandBox — far too late. Without a pre-Native module, custom brushes and sprites render as transparent/missing.

The critical line is `UIConfig.DoNotUseGeneratedPrefabs = true` — without it, the game uses pre-compiled prefab caches that don't contain TAOM's custom brush references.

## Architecture

### Load Order
```
1. Bannerlord.Harmony
2. TAOM.Dependencies     <- System patches applied here (pre-Native)
3. Native
4. SandBoxCore
5. SandBox
6. TAOM                  <- UIExtender.Create/Register/Enable here (post-SandBox)
```

### Lifecycle Split
- **Dependencies module**: Static constructor sets `UIConfig.DoNotUseGeneratedPrefabs = true`. `OnSubModuleLoad` touches `typeof(UIExtender)` which triggers UIExtender's static constructor, applying all 5 Harmony patches.
- **Main TAOM module**: Calls `UIExtender.Create("TAOM")`, `Register(assembly)`, `Enable()` to activate TAOM's specific ViewModel mixins and prefab patches.

### SubModule.xml
Uses `<ModulesToLoadAfterThis>` to declare Native, SandBoxCore, SandBox, StoryMode, CustomBattle must load AFTER this module. Combined with `<DependedModuleMetadata order="LoadAfterThis">` for launcher compatibility.

## Key Files

| File | Purpose |
|------|---------|
| `Dependencies/SubModule.cs` | Static ctor + OnSubModuleLoad — triggers UIExtender patches |
| `Dependencies/_Module/SubModule.xml` | Pre-Native load order declaration |
| `Dependencies/TAOM.Dependencies.csproj` | Minimal project — Harmony + game DLLs only |
| `Dependencies/ThirdParty/UIExtenderEx/` | Forked UIExtenderEx source (43 files) |

## Dependencies

- `Bannerlord.Harmony` (must load before this module)
- Game DLLs (TaleWorlds.*, referenced at compile time)
- `Harmony.Extensions` NuGet (provides AccessTools2)

## Tests

No unit tests — Harmony patch invocation requires a live game. Verified in-game: settlement nameplates render correctly, career button visible, resource bar present, time acceleration controls work.

## How-To: Rebuild

```bash
dotnet build Dependencies
```

The DLL deploys to `Dependencies/_Module/bin/Win64_Shipping_Client/TAOM.Dependencies.dll` and is copied to the game's Modules folder by BuildResources.
