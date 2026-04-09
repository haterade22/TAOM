# ButterLib Reference

## What It Was

Bannerlord.ButterLib was BUTR's (Bannerlord Unofficial Tools & Resources) foundational library that provided shared infrastructure for the Bannerlord modding ecosystem. It loaded before Native and served as a dependency for MCM and other BUTR tools.

## What It Accomplished

### 1. Hot Key Management
ButterLib provided `HotKeyManager` for registering custom keyboard shortcuts. It patched TaleWorlds' `HotKeyManager.RegisterInitialContexts()` to inject mod-defined key contexts alongside vanilla ones.

**This is what broke on 1.4.0:** TaleWorlds changed `RegisterInitialContexts(IEnumerable<GameKeyContext>, bool)` to `RegisterInitialContexts(IEnumerable<GameKeyContext>)` (removed the `bool loadKeys` parameter). ButterLib 1.3.9 called the old 2-parameter version, causing `MissingMethodException` at startup.

### 2. Module Management
- `ModuleInfoHelper` — discovered loaded modules, resolved paths, validated load order
- `ModuleInfoExtendedHelper` — record wrapper with module path/external status
- Module dependency validation and sorting

### 3. Shared Utilities
- `AccessTools2` — null-safe reflection wrapper over Harmony's AccessTools
- `HarmonyExtensions` — `TryPatch()` exception-safe wrapper
- `WrappedPropertyInfo` / `WrappedMethodInfo` — PropertyInfo/MethodInfo subclasses that redirect Get/Set/Invoke to a different instance
- `DictionaryExtensions` — `Deconstruct`, `AddRange` helpers
- `MBSubModuleBaseSimpleWrapper` — delegate-based SubModule forwarding

### 4. Error Reporting
- `MessageBoxDialog` — Win32 P/Invoke wrapper for showing error dialogs during module initialization (before game UI is available)
- `MessageUtils` — logging helpers using `System.Diagnostics.Trace`

### 5. Decorator Model Pattern
- `DecoratorModelHelper` — generic helper for the decorator/chain pattern in `IGameStarter.Models`

## What TAOM Uses From ButterLib

**Nothing directly.** TAOM never imported `ButterLib` namespaces. ButterLib was only present as MCM's transitive dependency.

However, the forked UIExtenderEx (in `Dependencies/ThirdParty/UIExtenderEx/`) uses several ButterLib utilities that we inlined:

| ButterLib Utility | Inlined Location | TAOM Usage |
|-------------------|-----------------|------------|
| `WrappedPropertyInfo` | `Dependencies/ThirdParty/UIExtenderEx/BUTR/` | Gauntlet ViewModel property binding redirect |
| `WrappedMethodInfo` | `Dependencies/ThirdParty/UIExtenderEx/BUTR/` | Gauntlet command dispatch redirect |
| `DictionaryExtensions` | `Dependencies/ThirdParty/UIExtenderEx/BUTR/` | Dictionary deconstruct in PrefabComponent |
| `ModuleInfoHelper` | **REPLACED** with `ModulePathHelper` | Module path resolution |
| `AccessTools2` | **PROVIDED** by `Harmony.Extensions` NuGet | Null-safe reflection |
| `HarmonyExtensions` | **PROVIDED** by `Harmony.Extensions` NuGet | TryPatch() |

## How to Maintain

ButterLib itself is NOT forked — only the specific utilities UIExtenderEx needed are inlined. If UIExtenderEx upstream changes which ButterLib utilities it uses, check:

1. Is the new utility already in our `BUTR/` folder? If yes, no action.
2. Is it provided by the `Harmony.Extensions` NuGet? If yes, no action.
3. Otherwise, inline the new utility from ButterLib's source (MIT license) into `Dependencies/ThirdParty/UIExtenderEx/BUTR/`.

ButterLib source: https://github.com/BUTR/Bannerlord.ButterLib

## Why We Don't Need It

TAOM eliminated ButterLib by:
1. Replacing MCM (which depended on ButterLib) with a JSON settings singleton
2. Inlining the 5 specific ButterLib utilities UIExtenderEx needed
3. Replacing `ModuleInfoHelper` with a simpler `ModulePathHelper` that walks directory tree

The crash that started this work (`HotKeyManager.RegisterInitialContexts` signature change) is irrelevant to TAOM because we never used ButterLib's HotKey system.
