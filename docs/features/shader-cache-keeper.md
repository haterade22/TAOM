# Shader Cache Keeper

## Overview

A proxy `xinput9_1_0.dll` in the game's `bin\Win64_Shipping_Client` folder. It keeps Bannerlord's compiled
runtime shaders when the module list changes but no module with shader content changed, so the game skips the
roughly 1,340 shader compiles (about 40 s on the dev machine) it would otherwise run at startup. It is
Yotthani's ShaderCacheKeeper (MIT), vendored and fixed for TAOM, and shipped through the launcher manifest.

## Why This Exists

- **Vanilla behavior:** the engine keeps its runtime shader cache in
  `C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11`, indexed by `shader_mapping.bin`, which
  records the game build and the sorted list of active module ids. About two seconds after the process starts,
  before any module loads, the engine compares that list with the current start; any difference, even a module
  without shader content, shows "Mod change detected" and deletes the cache. The native function is `FUN_1801d91a0`
  in the v1.5.3 decompile
  ([shader-cache-native.md](../../plans/_audit/2026-10-02-perf/evidence/load/shader-cache-native.md)).
- **TAOM requirement:** a TAOM start compiles about 1,336 `pbr_metallic` variants for the Armory's materials on a
  cold cache, 32.5 to 42.5 s on the dev machine (FOR-MIKE item 1 of the 2026-10-02 perf audit). A player who adds or
  removes a small mod pays that again.
- **Without this feature:** every module-list change costs a full recompile. No managed mod can prevent it, because
  the check runs before any module's code.

## Architecture

### Design Challenge

The check runs in native code before `SubModule.OnSubModuleLoad`. The only TAOM-controlled code that runs that
early is a DLL Windows loads into the process with the engine.

### Solution Approach

`TaleWorlds.Native.dll` statically imports `XInputSetState` and `XInputGetState` from `xinput9_1_0.dll` (read with
pefile, v1.5.4, 2026-10-06). Windows loads a file of that name from the game's `bin` folder before the system copy,
for every launcher. On load (`DllMain`), only in processes whose name starts with `Bannerlord`:

1. Read this start's module list from the command line (`_MODULES_*a*b*_MODULES_`), or, for a launcher that becomes
   the game (BLSE), from `Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml`, and only when its
   `<GameType>` is `Singleplayer`.
2. Read the list in `shader_mapping.bin`. If it is the same, note the signature (below) when none is noted, and stop.
3. Compare **signatures**: id, `SubModule.xml` version and newest file time (`*.log` ignored) of the `Shaders` folder
   of every listed module that has one. The old signature comes from `keeper_signature.txt` beside the cache when it
   was noted for exactly the cached list; otherwise from today's files (first start with the keeper).
4. Same signature: rewrite only the list text (temp file, then `MoveFileEx`). The engine then keeps its cache.
   Different signature, no cache, or a signature too long to hold: touch nothing, and the engine compiles as
   always. The signature it compiles for is noted.

XInput calls go to Windows' own `xinput9_1_0.dll`, loaded from `GetSystemDirectoryW` on the first call (`InitOnce`,
never under the loader lock); without it every call answers `ERROR_DEVICE_NOT_CONNECTED`.

### `shader_mapping.bin` header (v1.5.4, read 2026-10-06)

| Offset | Content |
|---|---|
| 0 | u32 format, `0x0783` |
| 4 | u32 length, then the game build as text (`123627`) |
| 8 + n | u32 length, then the module ids sorted by character code, joined with `;` (`SandBoxCore` before `Sandbox`) |
| after | the shader records, untouched |

A header that does not parse is left alone ("not understood ... nothing done").

### Component Diagram

```
Windows loader ── TaleWorlds.Native.dll imports xinput9_1_0.dll
                        |
              bin\Win64_Shipping_Client\xinput9_1_0.dll (this file)
               /                                   \
   DllMain: keep()                            XInputGetState/SetState/... stubs
   list (command line | LauncherData.xml)        -> %SystemRoot%\System32\xinput9_1_0.dll
   signature of Modules\<id>\Shaders
   rewrite list text in shader_mapping.bin
```

## Configuration

No config file. Off switch: an empty file `ShaderCacheKeeper.off` beside the DLL. Log:
`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\ShaderCacheKeeper.log` (restarted past 256 KB). If anything
draws wrongly after a start, deleting `...\Shaders\CoreShaders` makes the game rebuild once.

### What it does not cover

- **TAOM, TAOM_Map and LOTRLOME_Armory all have a `Shaders` folder**, so switching between vanilla and TAOM, or
  updating any of the three, still recompiles. That is correct, not a gap.
- **A changed game build** (a game update, or the Modding Kit, which shares the cache folder and has its own build)
  still drops the cache: only the list text is rewritten, never the build text.
- **The first start with the keeper** has no noted signature, so it trusts today's files for the cached list.

## Key Files

| File | Purpose |
|------|---------|
| `Native/ShaderCacheKeeper/keeper.c` | The whole DLL: `keep()` and the XInput stubs |
| `Native/ShaderCacheKeeper/keeper.def` | Exports, with the system file's ordinals |
| `Native/ShaderCacheKeeper/keeper.rc` | Version resource (keep in step with `KEEPER_VERSION`) |
| `Native/ShaderCacheKeeper/build.py` | Builds `out/xinput9_1_0.dll` (MSVC through vswhere, or `VCVARS64`) |
| `Native/ShaderCacheKeeper/test/run_tests.ps1` | Hermetic tests: stand-in game, Modules and cache under `%TEMP%\keeper_test` |
| `Native/ShaderCacheKeeper/test/parse_tests.c` | LauncherData parsing on in-memory buffers |
| `Native/ShaderCacheKeeper/test/host.c` | Stand-in game executable |
| `Native/ShaderCacheKeeper/LICENSE` | MIT, (c) 2026 yotthani |

## Dependencies

- Windows' `xinput9_1_0.dll` (system directory).
- MSVC x64 build tools, for building only. Not built by `build.ps1` or CI.

## Tests

`python Native/ShaderCacheKeeper/build.py`, then `pwsh -File Native/ShaderCacheKeeper/test/run_tests.ps1`; the
exit code is the number of failed checks. 13 cases: no forwarded export; the list rewrite and an unchanged
record tail; Windows' XInput reached (`XInputGetState(4)` answers 160, which only the real DLL does); the same
start again; a shader module removed; the off switch; another program loading the proxy; no cache; the launcher
selection (reads the real `LauncherData.xml`, skipped without one); no list; a shader module updated after the
engine built the cache; the same after the engine compiled for a new list; too many shader modules; plus
`parse_tests.exe` (single player selection, multiplayer start, no GameType, nothing selected). Cases 0 and 9 to 12
failed against the upstream code at `1bbdc17d` before the fixes.

## How to ship a build

1. At the release tag: `python Native/ShaderCacheKeeper/build.py` and the tests, both exit 0.
2. Copy `Native/ShaderCacheKeeper/out/xinput9_1_0.dll` to
   `E:\LOTRAOM_Releases\<channel>\bin\Win64_Shipping_Client\` before the launcher manifest is generated; record its
   SHA-256 in the release note. Testing channel first; patreon and public only after the in-game check below.
3. A version change needs `KEEPER_VERSION` in `keeper.c` and both numbers in `keeper.rc`.

**What the launcher does with it** (launcher repository `C:\Users\mikew\source\repos\LOTRAOM`, read 2026-10-06):
`LOTRAOM.ReleaseManager`'s `ConsoleManifestGenerator` hashes every file in the channel folder except its fixed
exclusions (RuntimeDataCache, `.rtemp`, `.vs`, AssetSources), so the DLL enters the manifest and installs with no
launcher change. The launcher never deletes it: `ObsoleteDeletionScope` deletes a `bin/` file only under a prefix it
owns (`Bannerlord.BLSE`, `LOTRAOM`) and only while the manifest still ships that prefix, which leaves a player's own
copy of the keeper, or another mod's `xinput9_1_0.dll`, alone. Adding `xinput9_1_0` as an owned prefix would not
change that, because the DLL is the only file under it. So dropping the DLL from a later manifest leaves it in place
on players' machines: retiring it means shipping a build that does nothing, or an explicit removal step in the
launcher.

## Verification owed

In game, on the maintainer's word, game closed for the copy: install into the game's `bin\Win64_Shipping_Client`;
start through TAOM's BLSE LauncherEx (its log line names the launcher selection or the command line); restart with
one module without shader content toggled (`rgl_log`: no "Mod change detected", `compile_shader` near zero; keeper
log: "list text in the shader cache rewritten"); restart without the Armory (keeper log: "shader content changed",
the engine compiles); `ShaderCacheKeeper.off` gives no log line; a gamepad if one is at hand. Not tested anywhere:
a PC whose Windows is not on C: (the reason the forwarders were replaced).

## Changelog

- 2026-10-06: vendored from yotthani/bannerlord `HoN/ShaderCacheKeeper` at `1bbdc17d` (1.0.0) as 1.1.0, with five
  changes: XInput stubs instead of forwarders to `C:\Windows\System32` (the engine could not load where Windows is
  elsewhere), the signature noted whenever the cache is the list's or the engine compiles (an updated shader module
  could keep a stale cache), `LauncherData.xml` read only for a single player start, a signature that does not fit
  means nothing is done, and the `DllMain` exception filter narrowed to access violations and in-page errors.
  Review: [adopt-yotthani-shadercachekeeper-2026-10-06.md](../reviews/adopt-yotthani-shadercachekeeper-2026-10-06.md);
  RCA of the review: [rca-shader-cache-keeper-2026-10-06.md](../reviews/rca-shader-cache-keeper-2026-10-06.md) (#751).

## GitHub Issue

- **Issue:** [#751, feat: ship ShaderCacheKeeper, keep the shader cache across mod list changes](https://github.com/haterade22/TAOM/issues/751)
- **Status:** Closed with `triage-needs-ingame` (the in-game check above is owed)
