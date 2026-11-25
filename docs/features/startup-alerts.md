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
  is not detected here; the engine stops the game first (see "Wrong game version" for the version case).
  TAOM.Dependencies constructs before Native, whose `GauntletUISubModule` creates the inquiry manager in
  its own `OnBeforeInitialModuleScreenSetAsRoot`; so Dependencies' hook only arms the notice, and its
  `OnApplicationTick` in the same frame (after every module's hook) shows it, once per process, never on a
  dedicated server. The text is plain English with no language rows: TAOM's language files load only with
  TAOM. It logs to `Modules\TAOM.Dependencies\diag.log` (`MissingTaomNotice: reason=...`).

### Wrong game version

- **Vanilla behavior:** `Module.LoadSubModules` (v1.5.7) loads every module's DLLs and constructs every
  submodule in load order, and runs `OnSubModuleLoad` only after that loop. When TAOM's DLLs were built
  for another Bannerlord version, `TAOM.Client` (or `TAOM`) fails there with `CriticalError`, and the
  engine shows "Error while loading TAOM: ... submodule could not be loaded due to a missing or
  conflicting dependency" and aborts the game. Players do not read that as a version problem. The
  vanilla launcher ignores `DependedModuleMetadatas`, so TAOM's `Native` version pin warns nobody.
- **Fix:** TAOM.Dependencies loads first, so its `SubModule` constructor runs before any other TAOM submodule
  loads and before the engine's box. `GameVersionNotice` reads the `Native` pin (`version="v1.5.7.*"`) from the loaded
  TAOM's own `SubModule.xml` (the pin travels with the build it describes) and compares it with
  `VersionProbe` (the version text compiled into `TaleWorlds.Library`, read through `VirtualFolders` by `ApplicationVersion.FromParametersFile`) through `GameVersionCheck` (pure). On a difference
  it turns TAOM's code off for the session: a prefix on `Module.CheckIfSubmoduleCanBeLoadable`, which the
  same loop asks for every submodule before it loads the DLL, answers false for the submodules of TAOM
  and of every module that depends on TAOM. The engine's box then never comes, the game reaches the main
  menu, and `MissingTaomNotice` shows the TAOM popup there (title "TAOM") with both versions,
  "TAOM is turned off for this session ... Press Exit Game to close the game", and the two fixes: switch
  the game version in Steam (Betas), or, if one is out, install the TAOM made for that game version from
  the TAOM launcher. Plain English, no language rows.
  This popup's only button is Native's "Exit Game" (`{=YbpzLHzk}`, translated without TAOM's language
  files) and it closes the game (`Utilities.QuitGame`, the same call TAOM's autorun hosts use; owner
  decision 16 Nov 2026), because TAOM's code is off and there is nothing to play. Enter does the same;
  Escape does nothing, as the popup has no second button. The missing-TAOM popup (War Sails, not ticked)
  keeps a plain Ok that only closes the popup. The engine call sits in a NoInlining helper with a catch,
  so a failure never throws at the menu.
- **Fallback:** if the prefix can't be installed, a native message box (`TaleWorlds.Library.Debug.ShowMessageBox`,
  the call the engine uses for its own box) says the same before the engine's box, which then follows.
- **Limits:** TAOM's XML is still registered (the loop applies every module's XML list before it loads
  any DLL), so the menu is reached with TAOM's data and none of its code. The game's save check sees TAOM
  in the module list at its own version, so a TAOM save would load with no warning and no TAOM code; the
  popup's only button therefore closes the game. A version difference does not always break TAOM (a hotfix can keep the
  API), but on any version other than the pin TAOM is now turned off; the pin must follow the game
  version the build targets. Not on a dedicated server (the engine's box also checks
  `MBDebug.TestModeEnabled`, which `/runTest` sets only after the submodules load, so it is not read);
  once per process; nothing happens when TAOM is not in the loaded list, the
  version can't be read, or the pin is missing. If TAOM.Dependencies itself fails to load on that game
  version, nothing runs. The same holds if an engine member `Run` reads differs on the player's game
  version: nothing runs and the engine's own box shows as before. Not in the Modding Kit (its engine can
  lag the client's; logs `skipped: Modding Kit`). The TAOM launcher and BLSE use their own version rules
  (launcher: same major.minor series; BUTR: minimum), so on a same-minor game revision TAOM can turn off
  while the launcher calls the game compatible. Logs `GameVersionNotice: game v..., TAOM ... pins Native ...: Match|Mismatch|Unknown`
  and `TAOM's code is off for this session: N submodule(s) of ...` to `diag.log`.

### Engine load-error box

- **Vanilla behavior:** when the guard above does not fire (the version check says Match or Unknown, a
  stale TAOM.Dependencies as in #371, a mixed install, or a TAOM older than the guard), the engine box
  still names no cause. Its text comes from the private `Module.GetAssemblyLoadResultsMessage(module, ...)`
  (v1.5.7 `Module.cs:1199`), hardcoded English with no language key.
- **Fix:** `GameVersionNotice.Run` calls `LoadErrorHint.TryInstall` on every client start, after the
  server and Modding Kit skips. It patches that method with a postfix (same Harmony id) which, for TAOM
  and the modules that depend on it, adds one paragraph from `GameVersionCheck.LoadErrorHint`: "This
  error is usually caused by the wrong Bannerlord version." On a mismatch it names both versions and the
  Steam Betas step; on a matching game it says the TAOM files may be mixed or out of date and to install
  TAOM again with the TAOM launcher; with no pin or no game version it names no version. On a critical
  error the engine's own "Please update or disable this mod" line follows; the non-critical "dependency
  conflict" box (a failed `<Assemblies>` entry, which no TAOM submodule declares today) gets the paragraph
  too. The paragraph is added once per process: the engine joins one message per failed submodule into
  one box (`Module.cs:1144`), and `TAOM.Client` fails with `TAOM.dll`. The owners are the guard's own list
  (`GameVersionNotice.OwnerModules`). Logs `LoadErrorHint: installed for ...` to `diag.log`.
- **Limits:** nothing is added if TAOM.Dependencies is missing or fails to load, if `Run` fails on a far-off
  game version, or if the method is gone (`not installed: target=False`). BUTR/BLSE boxes are not changed.

### Separate library copies

- **Vanilla behavior:** the v1.5.7 launcher lists every `Modules\<dir>\SubModule.xml` and then every
  Workshop module (`ModuleHelper.GetModulesForLauncher`) and keeps duplicate Ids. It stores ticks by
  module Id (`LauncherModsVM.LoadSubModules` reads `GetUserModData(isMultiplayer, item3.Id)`), and
  unticking a module unticks every module that depends on its Id (`ChangeIsSelectedOf`).
- **TAOM requirement:** TAOM.Dependencies ships alias stubs with the ids `Bannerlord.Harmony`,
  `Bannerlord.ButterLib`, `Bannerlord.UIExtenderEx` and `Bannerlord.MBOptionScreen`
  ([dr3-maintenance.md](../migration/dr3-maintenance.md) "Stub modules"). Luiz decided
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
| `Dependencies/Foundation/GameVersionCheck.cs` | Reads TAOM's Native pin, compares it with the game version, the box text (pure) |
| `Dependencies/GameVersionNotice.cs`, `Dependencies/SubModule.cs` (constructor) | Turns TAOM's code off on a wrong version (prefix on `Module.CheckIfSubmoduleCanBeLoadable`), else the native box; `MissingTaomNotice` shows the menu popup |
| `Dependencies/LoadErrorHint.cs` | Postfix on `Module.GetAssemblyLoadResultsMessage`: adds the wrong-version paragraph to the engine's box for TAOM |

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
- `TAOM.Tests/Infrastructure/Dependencies/GameVersionCheckTests.cs`: the compare (same, other
  revision, minor and major, wildcards, no letter, undetected game, bad pins), the pin reader (the
  shipped file pins `v1.5.7.*`, other metadata, no pin, broken XML), the text, that the constructor runs
  the notice, the guard order (server/test, mismatch, turn code off, else box), which submodules are
  skipped, the engine target's shape (binds on the referenced game, has a try region so it is not
  inlined), the menu popup before the missing-TAOM check; the load-error hint text (wrong version, right
  version, unknown pin or game version), where `Run` installs it, its owner filter, and its engine
  target's shape; LiveInstall: the installed game matches the shipped pin.

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
5. **Wrong game version.** Precondition: a game on another version than TAOM's pin (in Steam, Properties,
   Betas, pick another version), or a copy of TAOM whose `SubModule.xml` pins another version (edit
   `version="v1.5.7.*"` on the Native line to `v1.5.6.*`; nothing else changes, so TAOM still loads).
   Start the game. Pass: no "Error while loading TAOM" box; at the main menu one TAOM popup names both
   versions and says TAOM is turned off, and Exit Game closes the game; `diag.log` has `GameVersionNotice: ... Mismatch` and `TAOM's code
   is off for this session`. Undo the edit afterwards. On a matching game: no popup, `diag.log` has `... Match`.
6. **Engine load-error box.** Precondition: a matching game, and a deployed `TAOM.Client.dll` that cannot
   load (back it up on E:, then replace it with a broken file). Start the game. A "Cannot load: ..." box
   titled ERROR comes first (`AssemblyLoader.LoadFrom`); press Ok. Pass: the "Error while loading TAOM"
   box has the paragraph "This error is usually caused by the wrong Bannerlord version..." once, before
   "Please update or disable this mod"; `diag.log` has `LoadErrorHint: installed for TAOM`. Restore the
   DLL afterwards. On a normal start the `installed for` line is the proof the patch applies.

## Changelog

- 2026-11-15: added (War Sails and separate library copies).
- 2026-11-15: TAOM.Dependencies says when the launcher dropped TAOM (War Sails ticked, or TAOM not loaded).
- 2026-11-15: TAOM.Dependencies turns TAOM's code off and tells the player when the game is not the Bannerlord version TAOM was built for.
- 2026-11-18: the engine's "Error while loading TAOM" box now says the usual cause is the wrong Bannerlord version (#1461).

## GitHub Issue

- **Issue:** none (Luiz's decision, 2026-11-15)
- **Status:** n/a
