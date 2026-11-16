# Startup Alerts

## Overview

At the main menu TAOM checks for two setup problems the player has to fix outside the game, and shows
one inquiry that lists every problem it found:

- **War Sails loaded.** The War Sails module (`NavalDLC`) is in the loaded module list.
- **Separate library copies.** A real copy of Harmony, ButterLib, UIExtenderEx or MCM is installed, in
  the game's `Modules` folder or as a Steam Workshop item. TAOM.Dependencies already provides all four.
  The alert names each copy and where it is, and asks the player to remove it.

## Why This Exists

### War Sails

- **Vanilla behavior:** War Sails is `DefaultModule="true"` and ships with the game for DLC owners.
- **TAOM requirement:** TAOM does not support it. `Main/_Module/SubModule.xml` lists `NavalDLC` under
  `IncompatibleModules` (its comment gives the reasons: XSLT collisions with TAOM_Map, and the
  "nordinvasion" start option dereferencing a `nord` kingdom TAOM does not have). The vanilla launcher
  honours that list (`LauncherModsVM.ChangeIsSelectedOf`, `AreAllDependenciesOfModulePresent`).
- **Without this alert:** BLSE or a hand-edited module list can still load both, and nothing in game
  says why things then break. The check reads the loaded list, not the install, because every DLC owner
  has the module installed.

### TAOM dropped by the launcher

- **Vanilla behavior:** with War Sails ticked, the v1.5.7 launcher honours TAOM's `IncompatibleModules`
  entry by dropping TAOM itself, but still starts TAOM.Dependencies, TAOM_Map and the Armory (project
  owner's start 2026-11-15 20:31, rgl_log line 61: no TAOM in the loaded list, no TAOM log).
- **Without this notice:** TAOM's own War Sails alert never runs, and the player gets the plain War Sails
  menu with no word on why.
- **Fix:** TAOM.Dependencies loads either way, so it says it: `MissingTaomCheck` (pure) and
  `MissingTaomNotice` show one inquiry when TAOM_Map is loaded and TAOM is not, naming War
  Sails when `NavalDLC` is loaded, otherwise saying TAOM is not ticked or the launcher left it out.
  TAOM.Dependencies alone does not count: its alias stubs are `DefaultModule` and serve the BUTR ids to
  other playsets, so it loads without TAOM on purpose. A TAOM whose DLL fails after the launcher listed it
  is not detected here.
  TAOM.Dependencies constructs before Native, whose `GauntletUISubModule` creates the inquiry manager in
  its own `OnBeforeInitialModuleScreenSetAsRoot`; so Dependencies' hook only arms the notice, and its
  `OnApplicationTick` in the same frame (after every module's hook) shows it, once per process, never on a
  dedicated server. The text is plain English with no language rows: TAOM's language files load only with
  TAOM. It logs to `Modules\TAOM.Dependencies\diag.log` (`MissingTaomNotice: reason=...`).

### Separate library copies

- **Vanilla behavior:** the v1.5.7 launcher lists every `Modules\<dir>\SubModule.xml` and then every
  Workshop module (`ModuleHelper.GetModulesForLauncher`) and keeps duplicate Ids. It stores ticks by
  module Id (`LauncherModsVM.LoadSubModules` reads `GetUserModData(isMultiplayer, item3.Id)`), and
  unticking a module unticks every module that depends on its Id (`ChangeIsSelectedOf`).
- **TAOM requirement:** TAOM.Dependencies ships alias stubs with the ids `Bannerlord.Harmony`,
  `Bannerlord.ButterLib`, `Bannerlord.UIExtenderEx` and `Bannerlord.MBOptionScreen`
  ([dr3-maintenance.md](../migration/dr3-maintenance.md) "Stub modules"). The project owner decided
  (2026-11-15) that every player uses the stubs.
- **Without this alert:** a player subscribed to the Workshop BUTR modules sees each id twice in the
  launcher and can't untick one copy without the other. Over a Workshop copy the stub wins in game:
  `ModuleHelper.InitializeModules` takes the first module per Id and reads the `Modules` folder before
  the Workshop. How BLSE orders two modules with one Id is not verified. A real copy in `Modules\<ID>`
  takes the stub's own folder and replaces its `SubModule.xml`, so no stub exists for that Id: the alert
  asks the player to reinstall TAOM, never to delete that folder, which would unload every mod that
  depends on the library.

## Architecture

### Design Challenge

The launcher can't tell which of two same-Id modules is ticked, so for the libraries "active" is not
knowable; that check uses the folders on disk. The alert must show once per launch, survive the splash
video, and never stop the game.

### Solution Approach

`StartupAlertsModule` is a `TaomFeatureModule`; `SubModule.cs` and `IoC.cs` carry nothing for it. At
`ApplyPhase.MainMenu` (the first `OnBeforeInitialModuleScreenSetAsRoot`, once per process), skipped on a
dedicated server, it runs each check in its own try, collects one paragraph per finding, and
`StartupAlertsEntry.Show` puts them in one inquiry with the vanilla Ok button.

- War Sails: `WarSailsCheck.IsLoaded` over `ILoadedModuleIds` (`ModuleHelper.GetModules()`, the
  modules the game was started with).
- Library copies: `BundledLibraryCopyService` scans `<game>\Modules` and
  `<library>\steamapps\workshop\content\261550` (Steam keeps Workshop items in the library that holds
  the game). A folder counts when its `SubModule.xml` has one of the four ids and no `DLLName` of
  `TAOM.Dependencies.dll`; that `DLLName` marks TAOM's own stub. A folder, or a whole root, that can't
  be read is skipped.

Every finding is also a `[StartupAlerts]` warning in the TAOM log.

### Component Diagram

```
StartupAlertsModule (MainMenu phase)
     |                         |
WarSailsCheck            BundledLibraryCopyService
     |                         |
ILoadedModuleIds         IModuleFolderReader
     \                        /
      StartupAlertsEntry (one InformationManager.ShowInquiry)
```

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/StartupAlerts/StartupAlertsModule.cs` | Feature module: registration, MainMenu checks, log lines |
| `Main/Features/StartupAlerts/StartupAlertsEntry.cs` | The shared inquiry and the paragraph texts |
| `Main/Features/StartupAlerts/WarSailsCheck.cs` | War Sails in the loaded list |
| `Main/Features/StartupAlerts/BundledLibraryCopyService.cs` | Library copy scan and classification, Workshop folder |
| `Main/Adapters/ILoadedModuleIds.cs`, `LoadedModuleIds.cs` | Loaded module ids |
| `Main/Adapters/IModuleFolderReader.cs`, `ModuleFolderReader.cs` | Folder listing and SubModule.xml reads |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_startup_alert_war_sails`, `taom_bundled_library_copies_body`, `taom_bundled_library_copy_workshop`, `taom_bundled_library_copy_modules` |
| `Dependencies/Foundation/MissingTaomCheck.cs` | Who gets the missing-TAOM notice (pure) |
| `Dependencies/MissingTaomNotice.cs`, `Dependencies/SubModule.cs` | The notice: armed at the menu hook, shown on the next tick; rows `taom_missing_taom_war_sails`, `taom_missing_taom_not_loaded` |

## Tests

- `TAOM.Tests/Features/StartupAlerts/BundledLibraryCopyServiceTests.cs`: classification (the four ids,
  the repo stubs, other ids, bad XML, missing name), the scan over both roots and its fail-open paths, and
  the Workshop folder.
- `TAOM.Tests/Features/StartupAlerts/WarSailsCheckTests.cs`: the loaded-list check, and that its id is
  the one `Main/_Module/SubModule.xml` declares incompatible.
- `TAOM.Tests/Features/StartupAlerts/StartupAlertsWiringTests.cs`: the module is listed once, declares
  nothing else, and reads nothing outside MainMenu or on a dedicated server.
- `TAOM.Tests/Infrastructure/Dependencies/MissingTaomCheckTests.cs`: who gets the missing-TAOM notice
  (the owner's War Sails start, TAOM loaded, not ticked, ids in other case or only containing TAOM), the
  ids against both SubModule.xml files, and the inline English against the base rows.

## How to verify in game

1. **Library copies.** Precondition: a real BUTR module folder in `Modules` or a Workshop subscription to
   Harmony (2859188632). Start the game to the main menu. Pass: one TAOM inquiry lists each copy; the
   TAOM log has `[StartupAlerts] N separate library copies found` and one warning per copy. A result only
   a person can see: the text is readable and Ok closes it.
2. **War Sails.** Precondition: a module list with both TAOM and NavalDLC (BLSE, or a hand-edited
   launcher list). Pass: the inquiry has the War Sails paragraph; the log has
   `[StartupAlerts] NavalDLC is loaded with TAOM`.
3. **Clean setup.** No copies, no War Sails. Pass: no inquiry; the log line says 0 copies.
4. **TAOM dropped for War Sails.** Precondition: tick War Sails in the launcher with TAOM installed (the
   launcher unticks TAOM). Start the game. Pass: at the main menu one TAOM inquiry says TAOM did not load
   because of War Sails; `Modules\TAOM.Dependencies\diag.log` has `MissingTaomNotice: reason=WarSails`.

## Changelog

- 2026-11-15: added (War Sails and separate library copies).
- 2026-11-15: TAOM.Dependencies says when the launcher dropped TAOM (War Sails ticked, or TAOM not loaded).

## GitHub Issue

- **Issue:** none (project owner's decision, 2026-11-15)
- **Status:** n/a
