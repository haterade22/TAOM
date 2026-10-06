# Adopting yotthani's ShaderCacheKeeper (2026-10-06)

Written for: TAOM maintainers, and for yotthani (the "For yotthani" section is meant to be forwarded).
Procedure: `/adopt-external` ([external-repo-adoption.md](../ai-includes/external-repo-adoption.md)). Mike's
decision after the review: vendor the source, fix the defects test-first, build it ourselves, ship it in `bin/`
through the launcher manifest, testing channel first.

## Source, and what was read

`yotthani/bannerlord` (private, shared with Mike by its author), folder `HoN/ShaderCacheKeeper`, at commit
`1bbdc17d7602d55eee4050f921b762333f38c99c` (2026-10-06, "docs(ShaderCacheKeeper): MIT licence, shipped in the
package"). All 11 files were read in full, fetched one by one through the GitHub API: `README.md`, `LICENSE`,
`keeper.c` (336 lines), `keeper.rc`, `build.py`, `make_release.ps1`, `package/README.txt`, `test/host.c`,
`test/run_tests.ps1`, `test/game_test.ps1`, `test/wait_then_game_test.ps1`.

## Security

- **Safe to learn from:** yes. **Safe to run:** yes, once built from source by us; the repository holds no
  binary.
- **What runs:** `DllMain` of a proxy `xinput9_1_0.dll`, only in processes whose name starts with `Bannerlord`,
  and not if `ShaderCacheKeeper.off` sits beside it. It reads the command line, the HKCU `User Shell Folders\Personal`
  value, `LauncherData.xml`, and `SubModule.xml` and `Shaders` folders of listed modules. It writes
  `shader_mapping.bin` (through a temp file and `MoveFileEx`), `keeper_signature.txt` and a log, all under
  `%ProgramData%\Mount and Blade II Bannerlord\Shaders`. No network access, no process start, nothing persistent,
  no credentials. `build.py` runs only the local MSVC tools.
- **Licence:** MIT, (c) 2026 yotthani, in the folder's own `LICENSE`. The rest of the repository has none, so this
  folder gets its own `cleared` register row.

## Claims checked on this machine

| Claim | Evidence |
|---|---|
| The engine imports XInput from this DLL name | pefile on `TaleWorlds.Native.dll` (v1.5.4): `XINPUT9_1_0.dll` imports `XInputSetState`, `XInputGetState` (static imports, so a load failure of this file fails the engine) |
| The `shader_mapping.bin` layout keeper parses | hex dump of the live file: u32 `0x0783`, u32 6 + `123627`, u32 72 + `CustomBattle;Native;SandBoxCore;Sandbox;StoryMode;TAOM;TAOM.Dependencies`, then records |
| The list is sorted by character code | `SandBoxCore` precedes `Sandbox` in that file |
| `LauncherData.xml` has a game type | `<GameType>Singleplayer</GameType>` at line 3 of the local file (OneDrive-redirected Documents, which the registry lookup resolves) |
| No clash in the game's bin | only `dxgi.dll` (ReShade 6.8.0) uses a system DLL name there |
| TAOM's modules with shader content | `TAOM`, `TAOM_Map`, `LOTRLOME_Armory` each have a `Shaders` folder; `Native` and `SandBoxCore` have none |
| The upstream build forwards to a fixed path | pefile on the upstream build: all five exports forward to `C:\Windows\System32\xinput9_1_0.<name>`, `DllMain` included |

## Defects found, and how each was fixed

Each fix has a test that failed against the upstream code first (the RED run: cases 0, 9, 10, 11 and the parse
tests failed; everything else passed).

1. **The engine cannot load where Windows is not on C: (Likely; not run on such a machine).** `build.py` turned every
   export into a forwarder to `C:\Windows\System32\xinput9_1_0.<name>`. Forwarders are resolved when
   `TaleWorlds.Native.dll` is loaded, so a missing target fails the game's start. **Fix:** four exported stubs
   (`keeper.def`, the system file's ordinals 2 to 5) that load the real DLL from `GetSystemDirectoryW` on the first
   call through `InitOnceExecuteOnce`, and answer `ERROR_DEVICE_NOT_CONNECTED` without it. The test proves the
   calls reach Windows: `XInputGetState(4)` answers 160 (`ERROR_BAD_ARGUMENTS`), which only the real DLL does.
2. **A stale cache could be kept.** The signature was noted only after a rewrite. When the engine had built the
   cache, a shader module was updated, and the list then changed, the "before" signature was computed from today's
   files, matched, and the cache built from the old shaders was kept; the README says an updated shader module
   always rebuilds. **Fix:** note the signature also when the cache is already the list's (only if none is noted,
   since an earlier note still describes what the cache holds) and when the engine is left to compile (no cache,
   or changed shader modules). Only the very first start with the keeper still has to trust today's files.
3. **A multiplayer start through a launcher could lose a valid cache.** The list came from `SingleplayerData`
   whatever `<GameType>` said, and a wrong list makes the engine drop its cache. **Fix:** `list_from_launcher_xml`
   returns no list unless the game type is `Singleplayer`.
4. **A long signature was cut short silently.** Past its buffer the signature stopped (`break`), so a difference in
   a later module was invisible. That takes many modules with shader content (about 55 with 60-character versions,
   far more with ordinary ones), so it is minor. **Fix:** `signature_of` returns NULL and nothing is done.
5. **Calibration, not a defect upstream:** `__except (EXCEPTION_EXECUTE_HANDLER)` around `keep()` caught everything.
   TAOM's `native-cpp-ports.md` (item 2) narrows SEH filters, so it now catches access violations and in-page errors
   only.

## Not taken

`make_release.ps1` (a zip release from the deployed DLL; TAOM ships through the launcher manifest),
`test/game_test.ps1` and `test/wait_then_game_test.ps1` (yotthani's D: paths, MithrilForge's `run_scenario.ps1`,
his machine's job names), `package/README.txt` (player text for his zip). `test/run_tests.ps1` was reworked to run
hermetically: a synthetic `Modules` folder and cache instead of his game folder and a copy of the real cache.

## What it is worth to TAOM players

About 40 s at a start after any module-list change that involves no module with a `Shaders` folder (the roughly
1,336 `pbr_metallic` compiles measured in the 2026-10-02 perf audit, FOR-MIKE item 1). Switching between vanilla
and TAOM, or updating TAOM, TAOM_Map or the Armory, still rebuilds, correctly. It does not help Mike's Modding Kit
sessions: the Kit's build text differs from the game's, and only the list text is ever rewritten.

## Open

- **Launcher (settled, no change):** the launcher lives in `C:\Users\mikew\source\repos\LOTRAOM` (Mike, 2026-10-06;
  `E:\repos\lotraom-launcher-staging` is not it). Its manifest generator ships every file of the channel folder, so
  the DLL installs as is. Its `ObsoleteDeletionScope` deletes a `bin/` file only under an owned prefix that the
  current manifest still ships, so adding `xinput9_1_0` as a prefix (this review's first proposal) would never
  remove anything and was dropped. A retired keeper stays on players' machines until a launcher removal step or a
  do-nothing build replaces it; [shader-cache-keeper.md](../features/shader-cache-keeper.md) records this.
- **In-game check** on the testing channel: the protocol is in
  [shader-cache-keeper.md](../features/shader-cache-keeper.md), "Verification owed". The BLSE LauncherEx path TAOM
  ships has not been run with the keeper (yotthani tested the BLSE launcher).
- **Antivirus:** the DLL is unsigned and carries a Windows library's name. Expect occasional warnings; the source
  is in the repository and the release note carries the SHA-256.
- **Issue:** [#751](https://github.com/haterade22/TAOM/issues/751), filed from the draft below on Mike's word and
  closed with `triage-needs-ingame`.

## For yotthani

Thank you for ShaderCacheKeeper; TAOM ships it with these changes (all in `Native/ShaderCacheKeeper/` of the TAOM
repository, MIT kept):

1. `build.py`'s forwarders name `C:\Windows\System32\xinput9_1_0.<fn>`. On a PC whose Windows is on another drive,
   the forwarder cannot resolve and `TaleWorlds.Native.dll` fails to load, so the game does not start. We export
   four stubs instead that load the system file from `GetSystemDirectoryW` on first use (outside `DllMain`).
   `XInputGetState(4)` returning 160 is a cheap test that the call reached Windows.
2. `keeper_signature.txt` is written only after a rewrite. Cache built by the engine for list L; shader module M
   (in L) updated; start with L' (M still in it): the old signature is computed from today's files, matches, and
   the cache built from M's old shaders is kept. We also note the signature when the cache is already the list's
   (if none is noted) and when the engine is left to compile.
3. `LauncherData.xml`'s `SingleplayerData` is read whatever `<GameType>` says. A multiplayer start through the
   launcher could rewrite the list to the wrong one and drop a valid cache. We return no list unless
   `<GameType>Singleplayer</GameType>`.
4. `signature_of` stops with `break` when the buffer is full, so a change in a later module is not seen (needs many
   shader modules). We return NULL and do nothing.
5. Smaller: `build.py` also forwards the system file's `DllMain` export, which nothing imports; the
   `EXCEPTION_EXECUTE_HANDLER` filter also swallows a stack overflow or heap corruption.

## Issue draft

**Title:** feat: ship ShaderCacheKeeper, keep the shader cache across mod list changes

**Body:** Bannerlord deletes its runtime shader cache whenever the module list changes, before any mod loads, and
TAOM players then wait about 40 s for roughly 1,336 recompiles. yotthani's ShaderCacheKeeper (MIT) is a proxy
`xinput9_1_0.dll` that rewrites the list in `shader_mapping.bin` when no module with shader content changed. Vendored
into `Native/ShaderCacheKeeper/` with four fixes (forwarders to a fixed C: path, a stale-cache gap, the launcher's
game type, a silently shortened signature), shipped in `bin/Win64_Shipping_Client` through the launcher manifest,
testing channel first. Owed: the in-game check. Doc:
`docs/features/shader-cache-keeper.md`. Labels: enhancement.
