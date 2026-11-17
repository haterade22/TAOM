# Engine bug guards

## Overview

Small Harmony guards for crashes and hangs in vanilla Bannerlord code that no TAOM feature owns. Each guard changes
only the failing case and leaves every other call to the engine. The first two came from Silas He's v1.5.6 fix
package for the Chinese community (#1429): a saved-game load that fails and then spins for ever, and a 3D scene
widget that closes the game when it is sized before it has a scene.

## Why This Exists

- **Vanilla behavior:** `SandBoxGameManager.DoLoadingForGameManager` step 3 sets `_loadedGameResult` to null before
  `Game.Current.DoLoading()`. If a later line throws, no engine frame catches it before the native tick. TAOM's
  crash-capture finalizer on `Module.OnApplicationTick` (Patch37, on by default) swallows it, the step does not
  advance, and the next frame runs step 3 again with no result. `SceneTableau.SetTargetSize` calls `CreateTexture()`,
  which allocates a render target and a scene view, then passes a null `_tableauScene` to `SceneView.SetScene`; that
  throws a managed NullReferenceException (v1.5.7 SceneView.cs:20-23).
- **TAOM requirement:** a broken load must say so and let the player quit; a scene widget must wait for its scene.
- **Without this feature:** an endless loading screen whose last engine log line is "Initializing saved game
  begin...", and a game that closes as a scene notification (Silas saw the wedding notification) opens.

## Architecture

### Design Challenge

Both targets are private or protected engine methods with no TAOM seam. The load hang is only recognisable by
state: step 3 of a saved-game load whose result is already gone can only be a retry after a failure.

### Solution Approach

Two numbered patch categories, each a thin class with a pure, tested decision method, wired from
`Main/SubModule.cs` in `OnSubModuleLoad` (a save load and a scene widget can both run before any campaign exists).

- **Patch160_LoadRetryStop:** a void finalizer logs the first throw of each load with its stack (the exception
  continues as before). A prefix stops step 3 of a saved-game load with no result left, logs once, disables the
  global loading window (vanilla's own cancel path does the same) and shows one inquiry with a Quit Game button. The
  target is on `PatchShieldPolicy.ExcludedTargetMethods`: the override sets `nextStep` to `None` on its first line,
  and step 5 reads `None` as "loading finished", so a PatchShield swallow would open a half-loaded campaign.
- **Patch161_SceneTableauNullSceneGuard** (`[ClientOnly]`): a prefix skips `CreateTexture` while the scene is null.
  That tableau is a throwaway: when the scene arrives, `SceneTextureProvider` builds a new tableau for it, and the
  widget's next size update makes the texture there. The warning is written once per game process.

The registry sections in [harmony-patch-registry.md](../reference/harmony-patch-registry.md) hold the line
references in the v1.5.7 decompile.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/EngineBugGuards/Hooks/Patch160_LoadRetryStop.cs` | Load retry stop and first-failure log |
| `Main/Features/EngineBugGuards/Hooks/Patch161_SceneTableauNullSceneGuard.cs` | Scene widget null-scene guard |
| `Main/SubModule.cs` | Initializes and applies both categories in `OnSubModuleLoad` |
| `Dependencies/Foundation/PatchShieldPolicy.cs` | Keeps PatchShield off Patch160's target |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_load_failed_title`, `taom_load_failed_body`, `taom_load_failed_quit` |

## Dependencies

- `IModLogger`: the log lines.

## Tests

- `TAOM.Tests/Features/EngineBugGuards/Patch160LoadRetryStopTests.cs`: the stop rule over every step, the
  first-failure log per load, the binding of the target, its parameter names, the injected field and
  `LoadingSavedGame`, and the PatchShield exclusion.
- `TAOM.Tests/Features/EngineBugGuards/Patch161SceneTableauNullSceneGuardTests.cs`: the skip, the once-per-session
  warning, and the binding of `CreateTexture` and `_tableauScene`.

## How to verify in game

**Preconditions:** a TAOM build with #1429. Neither failure can be caused on purpose from the console, so both checks
read the log of a session where the failure happened.

1. Patch160: when a saved game fails to load, the TAOM log has `[EngineBugGuards] load step
   SecondInitializeThirdState threw:` with a stack, then `the saved game failed to load in step 3; the retry loop is
   stopped (Patch160)`, and the screen shows "The saved game failed to load" with Quit Game. Whether the inquiry
   renders and takes a click is unverified; a person must look. Click Quit Game: the game closes, and the lines
   above are still in the TAOM log.
2. Patch161: open a scene notification (a marriage, then its notification). If the guard fired, the TAOM log has
   `a scene widget was sized before it had a scene` once. The 3D scene appears when the widget next updates its
   size; if it stays blank, record that on #1429.

## Changelog

- 2026-11-17: feature created with Patch160 and Patch161, ported from Silas He's v1.5.6 fix package (#1429).

## GitHub Issue

- **Issue:** #1429: [Crashes and hangs from Silas's v1.5.6 fix package](https://github.com/haterade22/TAOM/issues/1429)
- **Status:** Open
