# D3 verify: ShaderCacheKeeper (372773d1a) and ShaderCompileNotice (82645b53f) on Bannerlord v1.4.8

Run 2026-11-04-port148, read-only verifier, adversarial stance. Refs: origin/bannerlord-1.5.x (e45f6df2c),
origin/bannerlord-1.4.5 (1b00d1acd), working tree (c5b84fb41). Engine: the installed
`TaleWorlds.Native.dll` on this laptop (14,185,944 bytes, PE TimeDateStamp 0x6a732505, embedded string
`v1.4.8.119303` at file offset 0xae15d8), disassembled with pefile 2024.8.26 and capstone 5.0.7; managed
signatures from `tools/taom-src.ps1` (cache `C:\Users\mikew\.taom-src\v1.4.8`) and the dump
`C:\Decompiled_Bannerlord\_categories_v1.4.8`. RVAs below are relative to image base 0x180000000.
Scratch disassembly listings: `SP\d3_dis_*.txt`.

## Bottom line

| Item | Decision | One-line reason |
|---|---|---|
| 372773d1a ShaderCacheKeeper | **NO-GO now (defer behind a gate)** | v1.4.8 matches the keeper on import, deletion trigger and header layout, but the engine's compared module list is not proven equal to the command-line list the keeper writes, and v1.4.8 already filters one token (NavalDLC). A mismatch makes the keeper drop the cache on every start. No 1.4.x delivery route is documented, and the keeper never shipped or ran in game on 1.5.x either. |
| 82645b53f ShaderCompileNotice | **GO, low priority (P2), with three 1.4.x adaptations** | Fail-open, no engine calls off-thread, the v1.4.8 native logs the same `compile_shader:` line. Covers only compiles before the main menu; useless for the E3/E4 battle waits; value on 1.4.8 unproven until a cold v1.4.8 start shows 10 or more pre-menu compiles. |
| In-battle compile notice | **Not a port** (1.5.x never built one) | Needs its own window thread and an rgl_log tail; requirements in section 3. A separate decision for Mike. |

## Claim-by-claim verdicts

| # | Claim (source) | Verdict | Evidence |
|---|---|---|---|
| 1 | TaleWorlds.Native.dll statically imports XInput from `xinput9_1_0.dll` (372773d1a, 1.5.4 read) | **CONFIRMED on v1.4.8** | pefile: static import `XINPUT9_1_0.dll` with `XInputSetState`, `XInputGetState`; no delay-load imports. `xinput9_1_0` is not among the 38 KnownDLLs (HKLM `Session Manager\KnownDLLs`, read this session), so a same-named file in the game's bin folder is found first. |
| 2 | The engine throws away its compiled shaders when the active module list changes (372773d1a) | **CONFIRMED on v1.4.8**: a change of the module *set*, or of the game build | Check function RVA 0x1f5f10, deletion in caller RVA 0x1f3ba0 (section 2). docs/migration/v1.4.8-impact.md:63 (working tree) already lists this as new in v1.4.8. |
| 3 | "even by a module with no shader content" | **CONFIRMED** | The compare is a string compare of the sorted id list (0x1f622f-0x1f626d); no content is consulted. |
| 4 | The check runs before any mod code | **CONFIRMED (call site)** | Called with a null argument at RVA 0x72c8e inside the native startup routine 0x71500 (which also runs `SteamAPI_Init`, EOS and `SetUnhandledExceptionFilter`). E2 timing: shader package read at rgl line 35 (17:21:03.568) precedes `Mono Loading Step::managed_interface_init` at line 81 (17:21:07.508). The check itself logs nothing on a warm start, so its own timestamp is inferred. |
| 5 | v1.4.8 writes `shader_mapping.bin` with the header keeper.c parses (the classifier's decisive question) | **CONFIRMED by disassembly; UNVERIFIED on a real file** | Writer RVA 0x1f6630: u32 0x783, u32 length + `"%d"` of 0x1d207 (= "119303"), then for the core cache u32 length + module list. keeper.c reads exactly that (section 1). No file written by v1.4.8 exists on this laptop. |
| 6 | "The only shader_mapping.bin on this laptop holds 0x05EF and no module list" (classifier, against) | **CONFIRMED, but it is not a v1.4.8 file** | Written 2026-08-14 by build 77991 (section 2f). The installed v1.4.8 engine has never started in this install. |
| 7 | Module ids sorted by character code, joined with `;` | **CONFIRMED on v1.4.8** | List builder 0x1f6470 copies the vector, sorts it (0x689d0, quick-sort with a 28-element insertion threshold at 0x68a32; element compare at 0x69560-0x69575 is an unsigned byte loop, strcmp semantics) and joins with the constant `";"` at RVA 0xaf475c. keeper.c uses qsort + strcmp + `;` (keeper.c:92-108). |
| 8 | The engine's compared list equals the raw `_MODULES_` command-line tokens (implicit in keeper.c:110-130) | **UNVERIFIED, and contradicted for one token** | v1.4.8's `_MODULES_` parser (RVA 0x549d0) pushes the token `NavalDLC` only if a platform-service call with "Mount & Blade II: Bannerlord - War Sails" returns true (0x54c50-0x54d35). What fills the vector the check reads (`[[0xd96cf0]+0x8a8]`) before the check was not traced (section 2e). |
| 9 | "about 1,340 shaders, about 40 s" at a TAOM start (372773d1a body) | **UNVERIFIED for v1.4.8** | A 1.5.3/1.5.4 dev-machine measurement (plans/_audit/2026-10-02-perf/evidence/load/shader-cache-native.md on 1.5.x). No v1.4.8 cold-start rgl log was available: this laptop never ran v1.4.8; E1/E2 are warm (0 `compile_shader` lines each). |
| 10 | The launcher installs the DLL from the channel's bin folder and never deletes it (1.5.x shader-cache-keeper.md:133-141) | **UNVERIFIABLE here** | The launcher repo (`C:\Users\mikew\source\repos\LOTRAOM`) is not on this laptop. The 1.4.x release-process.md (177 lines) has no channel or manifest step at all; the 1.5.x one does (line 82 and step 9). |
| 11 | "a launcher reordering the module list discards the work" / "re-run after adding, removing or reordering mods" (1.4.x docs/features/shader-precompilation.md:136 and :19, origin/bannerlord-1.4.5) | **REFUTED** | The list is sorted before it is compared and before it is written (claim 7), so order alone cannot trigger the drop. |
| 12 | 82645b53f "closes at the main menu, so it does nothing for E3's in-battle WaitingForRender stall" (classifier) | **CONFIRMED** | Stops at the first `OnBeforeInitialModuleScreenSetAsRoot` (82645b53f Main/SubModule.cs diff; ShaderCompileNotice.cs:42-43, :91-95). |
| 13 | "E3 is a slow shader compile" | **UNVERIFIED either way** | Arithmetic in section 3b: E3 was killed inside the time the same player's machine needed for a smaller compile. Brief2's #612 lesson stands: a wedge is not excluded. |

## 1. What 372773d1a is, and every later 1.5.x commit

**History.** `git log origin/bannerlord-1.5.x -- Native/` lists only 372773d1a (2026-10-06, 21 files,
+1182/-2). Later commits on the feature doc only: 82645b53f (two lines linking the notice), cd1fbdb6f
(backlinks footer), 3cdffd8a2 (2026-10-27: format word 0x0784 on v1.5.6, the Kit carries the game's build
number, and "not yet shipped in any channel" in docs/migration/v1.5.6-impact.md), 826e77aef (2026-11-03:
the Kit and the game running at once delete each other's cache; tools/run_fords_autorun.py refuses beside
an open Kit). No release note on 1.5.x mentions the keeper (`git grep` over docs/releases). The in-game
check owed under #751 is recorded nowhere. The Opus deep-review lenses never ran on it
(docs/reviews/rca-shader-cache-keeper-2026-10-06.md:3-8).

**What keeper.c parses** (origin/bannerlord-1.5.x:Native/ShaderCacheKeeper/keeper.c):

- File: `%ProgramData%\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11\shader_mapping.bin`
  (ProgramData from `GetEnvironmentVariableW`, keeper.c:292-303).
- Under 16 bytes or unreadable: treated as no cache; the signature is noted and the engine compiles
  (keeper.c:313-320).
- `length1` = u32 at offset 4; must be at most 256 and fit (keeper.c:321-322). `length2` = u32 at
  `8 + length1`; must be at most 65,536 and fit (keeper.c:323-324). The list is bytes
  `[12 + length1, 12 + length1 + length2)` (keeper.c:325-328). The u32 at offset 0 (format word) is never
  read: it bails on length sanity only.
- Wanted list: the command-line tokens between `_MODULES_*` and `*_MODULES_`, ASCII 32-126 only, split on
  `*`, qsort with strcmp, joined with `;` (keeper.c:92-130). Only when there is no command-line list and the
  exe name contains `Launcher`: the selected ids of `<SingleplayerData>` in
  `Documents\Mount and Blade II Bannerlord\Configs\LauncherData.xml`, and only for
  `<GameType>Singleplayer</GameType>` (keeper.c:132-170, :307-309; Documents resolved from the HKCU
  `User Shell Folders\Personal` value).
- Signature: for each listed module that has `Modules\<id>\Shaders`: `id@<SubModule.xml Version
  value>@<newest file mtime below Shaders, *.log excluded, depth 6>`; NULL (do nothing) if it does not fit
  (keeper.c:172-235). Recorded per list in `keeper_signature.txt` beside the cache (keeper.c:237-266).
- Decision (keeper.c:331-353): same list, done (note a signature if none). Different list: old signature
  from the sidecar (if noted for exactly the cached list) else computed from today's files; differs, the
  engine compiles; equal, rewrite only the list text: header bytes `[0, 8 + length1)` copied, new u32
  length + list, tail copied verbatim, via `shader_mapping.bin.keeper` and `MoveFileExW(REPLACE_EXISTING)`
  (keeper.c:355-369).
- Gates: process name must start with `Bannerlord` (keeper.c:295-299); off switch `ShaderCacheKeeper.off`
  beside the DLL (keeper.c:300-301). All of it runs inside `DllMain(DLL_PROCESS_ATTACH)` under an SEH filter
  that catches only access violations and in-page errors (keeper.c:431-447). XInput calls go through four
  exported stubs (keeper.def ordinals 2-5) to the system DLL loaded on first call via
  `InitOnceExecuteOnce` (keeper.c:378-429). Log: `...\Shaders\ShaderCacheKeeper.log`, restarted past
  256 KB (keeper.c:52-72).

**How it is deployed on 1.5.x.** Built by `python Native/ShaderCacheKeeper/build.py` (MSVC x64 via vswhere
or `VCVARS64`; `cl /O2 /MT /W4 /LD` with kernel32 and advapi32; not part of build.ps1 or CI), tested by
`pwsh -File Native/ShaderCacheKeeper/test/run_tests.ps1` (13 hermetic cases plus parse_tests.c, exit code =
failures). At release the maintainer copies `out/xinput9_1_0.dll` into
`E:\LOTRAOM_Releases\<channel>\bin\Win64_Shipping_Client\` before the launcher manifest is generated and
records the SHA-256 (372773d1a diff of .claude/skills/release/SKILL.md, new step 5, and
docs/reference/release-process.md step 9). The LOTRAOM launcher then installs it into the player's
`<game>\bin\Win64_Shipping_Client\xinput9_1_0.dll`. Uninstall: per the 1.5.x doc the launcher never deletes
it, so retiring it needs a do-nothing build or a launcher removal step (shader-cache-keeper.md:133-141;
launcher not verifiable here, claim 10). Players can disable it only by creating `ShaderCacheKeeper.off`
or deleting the DLL by hand.

## 2. v1.4.8: what the engine actually does

### 2a. Strings in the installed native DLL (file offset, RVA)

`Mod change detected` (0xaf3080, 0xaf4680); `A change in your active mods was detected. Deleting the runtime
shader cache to avoid rendering issues. ` (0xaf30f0, 0xaf46f0); `Shader cache version is old` (0xaf30a8);
`Your object shader cache version is old. Deleting the shader cache...` (0xaf2fd0); `shader_mapping.bin`
(0xaf2f88); `CoreShaders/D3D11/` (0xaf3160); `Could not write shader mapping file.\n` (0xaf30c8);
`compile_shader: %s, %s, %s, %d, %d, %d, %d.\n` (0xae6e50). The RIP-relative scan finds the two mod-change
strings referenced only inside 0x1f5f10-0x1f646c.

### 2b. The check, RVA 0x1f5f10 (v1.4.8 counterpart of v1.5.3's FUN_1801d91a0)

1. Builds `<cache dir>shader_mapping.bin`, opens it `"rb"` (mode string at RVA 0xae83f0). No file: returns
   valid.
2. `fread` u32, `cmp ..., 0x783` (0x1f6030). Mismatch: invalid.
3. Formats `"%d"` (RVA 0xae2be8) of the literal 0x1d207 = 119303 (0x1f6074-0x1f6085), reads a u32 length,
   rejects 9 or more (0x1f60a7-0x1f60aa), reads the text and compares (0x1f6113-0x1f614e).
4. Non-null argument (per-scene terrain caches): returns valid without a module check (0x1f618d-0x1f6190),
   which matches 1.5.x's note that terrain caches survive list changes.
5. Null argument (core runtime cache): reads a u32 length, requires 1 to 4095 (0x1f61b4-0x1f61bc), reads
   the list, builds the current list with 0x1f6470, compares (0x1f622f-0x1f626d).
6. Mismatch: `MessageBoxA(NULL, <message>, "Mod change detected", 0x41040)` (MB_ICONINFORMATION |
   MB_SYSTEMMODAL | MB_TOPMOST; import resolved by pefile, call at 0x1f62b6) unless the flags at RVA
   0xd92ce0, `[[0xd96d90]+0x5b0]` or 0xd92cc2 route it to the log line `Messagebox [%s] message: %s`
   (0x1f6273-0x1f631c). Then sets `[[0xd9ae90]+0x439] = 1` and returns invalid (0x1f638a-0x1f63a5).

A v1.4.8 player whose module set changed therefore sees a modal "Mod change detected" box at startup. That
is a cheap triage question for the E3 player (see plan inputs).

### 2c. The deletion, caller RVA 0x1f3ba0

Called from the startup routine at 0x72c8e with `edx = 0` (core cache; a second caller at 0x445fdc in
0x445f70 serves the per-scene path). It first runs the object-cache version check 0x1f5bf0 (deletes
`*.sack` on failure), then 0x1f5f10 (0x1f3f80). On invalid it calls helper 0x72a490 with `.sacx`, `.bin`
and `.changeset` on the folder from 0x1f6bc0 (0x1f3f92-0x1f4178); 0x72a490 enumerates with
`FindFirstFileW`/`FindNextFileW` (0x72baf0) and deletes with `DeleteFileW` (0x72c880). It then rewrites
the header through 0x1f6630 (0x1f41e9). The folder: 0xc69b0 calls `SHGetFolderPathW(CSIDL 0x23 =
COMMON_APPDATA)` and appends `Mount and Blade II Bannerlord` and `/Shaders/`; 0x1f6bc0 appends
`CoreShaders/D3D11/`. So the deleted cache is
`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11\*.sacx|*.bin|*.changeset`, and with
it every variant compiled in earlier sessions: battle compiles and any ShaderPrecompilation walk output.

### 2d. The writer, RVA 0x1f6630, and the list builder, RVA 0x1f6470

Writer: returns early if `[[0xd96cf0]+0x1d0] != 0`; opens `"wb"` (RVA 0xaf46c4), so it truncates to the
header; `fwrite` u32 0x783 (0x1f685a-0x1f6873); u32 length + "119303" (0x1f68ac-0x1f68f5); for the core
cache u32 length + the list from 0x1f6470 (0x1f6900-0x1f693d); on open failure logs
`Could not write shader mapping file.\n`. List builder: copies the 32-byte string vector at
`[[0xd96cf0]+0x8a8 .. +0x8b0]`, sorts (claim 7), joins with `;`. Layout and sort match keeper.c byte for
byte. Engine limits are tighter than the keeper's (build text under 9 bytes, list 1 to 4095 bytes versus the
keeper's 256 and 65,536); that only means an over-long list is invalid with or without the keeper.

### 2e. The open question: is the compared list the command-line list?

The `_MODULES_` parser (0x549d0) splits the text between `_MODULES_*` and `*_MODULES_` on `*` and copies
every token into a caller-supplied vector, except that the token `NavalDLC` is copied only when the platform
service at `[0xda4ca0]` answers true for "Mount & Blade II: Bannerlord - War Sails" (0x54c50-0x54d35). Its
callers pass stack vectors (0x57358, 0x529cc4) or a caller vector (0x56e4c8); the startup routine reaches it
through 0x57310 at 0x72cd1, which is *after* the shader check at 0x72c8e. So the vector the check reads was
filled earlier by a path not traced this session. If that path applies the same NavalDLC filter, or drops
modules that are missing on disk, then for those players `engine list != keeper list` on every start. The
keeper then rewrites the list to its own form each start, the engine compares its own form, mismatches,
shows the box and deletes the cache: an every-start cache drop that did not exist without the keeper. The
1.5.x hermetic tests cannot catch this (they fake the engine). The 1.5.x adoption review checked one live
1.5.4 file whose list matched its command line, which says nothing about NavalDLC without War Sails.
NavalDLC has no `Shaders` folder on this install, so the keeper's signature treats it as shader-less and
would rewrite.

### 2f. The cache on this laptop (read-only)

`C:\ProgramData\Mount and Blade II Bannerlord\Shaders\CoreShaders\D3D11`: 363 `.sacx` (333 `p`, 30 `v`;
359 dated 2026-08-13, 4 dated 2026-08-14) and `shader_mapping.bin` (38,461 bytes, 2026-08-14 08:44).
Header: u32 0x000005EF, u32 5, `77991`, then records directly: 13 + 1,602 x 24 bytes exactly; the last u32
of the records takes 363 distinct values (the `.sacx` count). keeper.c would read `length1 = 5` (accepted)
and `length2 = 0xCE0EF976` (rejected: "not understood (module list) - nothing done"), so the keeper is
safe on this older format. v1.4.8 would reject it at the format word and rebuild.

Who wrote it: the newest rgl log in `C:\ProgramData\Mount and Blade II Bannerlord\logs` is
`rgl_log_34608.txt` (2026-08-14 07:37 to 09:13), line 4 `Build Version: 77991`, line 46 an old LOTRAOM
module set (Bannerlord.Harmony, LOTRAOM.Diagnostics, LOTR_Map, Alliance.Wargs, LOTRAOM), 10
`compile_shader` lines. The installed v1.4.8 native DLL is dated 2026-09-06. The game folder holds no file
modified on 2026-11-04, and `steam_appid.txt` (which the startup routine 0x71500 opens with mode `w+`
and writes) was last written 2026-08-14 07:37:20.765, the same millisecond rgl_log_34608 starts. Only one
Steam library exists (`libraryfolders.vdf`). Conclusion: v1.4.8 has never started in this install.
Today's `DualWield_debug.log` (08:35-08:36, TAOM loaded, a custom battle) and `LauncherData.xml` (11:39)
sit in the OneDrive-redirected `C:\Users\mikew\OneDrive\Documents\Mount and Blade II Bannerlord`, most
likely synced from the desktop. Side note for the keeper: on a OneDrive-synced profile, the LauncherData.xml
it reads may describe another machine's last launcher session.

### 2g. Modules with a `Shaders` folder on this install

TAOM (`Shaders/D3D11/shader_compile_report.log` only, 366 bytes, so its keeper mtime term is 0), TAOM_Map
(`shader_mapping.bin` 3,135,496 bytes + report), LOTRLOME_Armory (`shader_mapping.bin` 13,674,304 bytes +
report). None for TAOM.Dependencies, Native, SandBoxCore, Sandbox, StoryMode, CustomBattle, NavalDLC.
Executables in bin whose names pass the keeper's `Bannerlord*` filter: Bannerlord.exe (a managed x64 CLR
image with no native import table, so TaleWorlds.Native.dll is loaded from managed code),
Bannerlord.Native.exe, Bannerlord.BLSE.Launcher/LauncherEx/Standalone.exe, BannerlordOnlineRunner.exe
(third-party). Not passing: Launcher.Native.exe, TaleWorlds.MountAndBlade.Launcher.exe. Whether the
vanilla launcher hosts the game in its own process (where the keeper would do nothing) is unverified.

## 3. 82645b53f and the in-battle compile

### 3a. What it shows and when

Files: `Main/Features/ShaderCompileNotice/{ShaderCompileNotice.cs, ShaderCompileNoticeEntry.cs,
ShaderCompileNoticeTexts.cs, ShaderCompileNoticeWindow.cs, ShaderCompileProgress.cs}`, 16 tests in
`TAOM.Tests/Features/ShaderCompileNotice/ShaderCompileProgressTests.cs`, Main/SubModule.cs (+11),
Main/TAOM.csproj (`System.Windows.Forms`, `System.Drawing`), three strings. Started from `OnSubModuleLoad`
right after `EarlyLogBridge`; a background STA thread runs a WinForms `ApplicationContext`; every 300 ms it
reads the new bytes (up to 4 MB) of `%CommonApplicationData%\Mount and Blade II Bannerlord\logs\
rgl_log_<pid>.txt` and counts `compile_shader` (ShaderCompileNotice.cs:27-29, :73-118); from 10 compiles
it shows a borderless, non-activating tool window owned by the game window, "Compiling shaders: N of about
T" with T remembered in `Shaders\taom_shader_compile_total.txt` when N exceeds 200
(ShaderCompileProgress.cs:15-31, :48-64); it lowers the process priority from Normal to BelowNormal while
shown and restores it (ShaderCompileNotice.cs:150-171); it stops at the first
`OnBeforeInitialModuleScreenSetAsRoot` or after 30 minutes; it stands down on a dedicated server or when a
module named `VanillaTuning` is active (ShaderCompileProgress.cs:70-80). It never calls the engine off the
game thread (texts resolved first, ShaderCompileNoticeEntry.cs:17-52).

1.5.x afterwards: c91380274 moved the window and message loop into TAOM.Client behind
`IShaderCompileNoticeWindowHost` (ADR-013); 4d79eca85, 0bb1f3cae, eed20591a are doc-only (the VanillaTuning
mutual stand-aside risk, MIT provenance); e339df075 only added a backlinks footer. It shipped to 1.5.x
players in v2.0.35 (docs/releases/v2.0.35-discord.md:98); the in-game check (#752) is recorded nowhere.
1.5.x never extended it to battles (`git grep`).

v1.4.8 facts for it: the native logs the same format (2a). docs/features/shader-precompilation.md:5
(working tree) records bundle b18f3441 (TAOM v2.0.26, Bannerlord 1.4.8, 2026-09-04): 818
`compile_shader` lines in the engine log during one battle load, so the line is logged in battle on
v1.4.8. A cold v1.4.8 start's pre-menu compile count was not read (claim 9). On 1.4.x: no `EarlyLogBridge`
exists (origin/bannerlord-1.4.5 has no Main/Core/Logging/EarlyLogBridge.cs); `OnSubModuleLoad` does
`IoC.Configure()` at Main/SubModule.cs:114 and the #371 BuildStamp block at :116;
`OnBeforeInitialModuleScreenSetAsRoot` is at :525-527; `IDedicatedServerProvider` exists; Main/TAOM.csproj
has no WinForms or Drawing reference; the working tree's LogTailCollector.cs:51-75 already probes
CommonApplicationData then MyDocuments for rgl logs.

### 3b. What E3 and E4 show

The 1.4.x render-wait sampler runs in a `MissionState.OnTick` postfix
(Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnTick_RenderWait_Patch.cs:52-76), reads
`Utilities.GetNumberOfShaderCompilationsInProgress()` on the main thread (:67), and emits at most once per
1,000 ms regardless of change (BattleLoadDiagnosticsService.cs:37, :291, :306-307). `MissionState.OnTick`
gates `TickMission` on `Handler.RenderIsReady()` (dump MissionState.cs:110-112). So a gap with no
WaitingForRender line means `MissionState.OnTick` did not run: the game thread was blocked.

- E3 (taom_debug_2026-11-01_20-00-00.log): line 218 (20:02:48) shaders=2720 at +7911 ms; line 219
  (20:02:50) shaders=2697 at +10168 ms; then only MemSample lines to line 223 (20:04:22). Silent for 92 s.
- E4 arena (taom_debug_2026-11-01_20-16-56.log): line 2125 (20:21:20) shaders=1569 at +88359 ms; MemSample
  at 20:21:49 and 20:22:19 inside the gap; line 2131 (20:22:29) shaders=0, waitedMs=68290; line 2153
  BattlePlayable `arena_aserai_a`, 559 agents, at +157939 ms. A 68 s blocked wait that completed.
- E4 town: line 5138 (20:48:25) shaders=806 at +111327 ms, then only MemSample lines to line 5164
  (20:51:51). At least 206 s silent.

Rates: E4 arena about 23 compiles per second (1,569 in 68.3 s); E3's two samples about 10 per second (23 in
2.26 s). 2,697 at 10 to 23 per second needs 117 to 270 s, so the E3 player may have killed a working compile
at 92 s; E4's town (806 pending, 35 s expected at the arena rate) ran far past that and fits the wedge
shape. Both readings stay open for E3.

### 3c. What an in-battle notice would need on v1.4.8 (new work, not a port)

- **The engine call.** `public static int GetNumberOfShaderCompilationsInProgress()` at
  `C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.Engine.Utilities.cs:597-599`, forwarding to
  `EngineApplicationInterface.IUtil` (dump IUtil.cs:187). No vanilla managed caller exists (dump grep).
  `CheckShaderCompilation()` (Utilities.cs:362-364) also exists; its meaning was not researched.
- **Callable from the render-wait path?** Yes, and 1.4.x already does it (MissionState_OnTick_RenderWait_Patch.cs:67),
  publishing to `BattleLoadRenderWaitProbe` for the watchdog. But it runs only while frames tick: during the
  blocked phase (E4: 68 s) nothing on the main thread runs, so a main-thread notice (Gauntlet overlay,
  InformationManager) freezes exactly when it matters.
- **So it needs:** 82645b53f's own window thread, opened when the main-thread probe reports a large pending
  count at FinishMissionLoadingDone and closed at BattlePlayable or mission end; progress from the
  rgl_log `compile_shader` tail (a file read, safe off-thread), shown as moving versus not moving for N s,
  which also separates a working compile from a wedge for triage. Never call the native counter from the
  window thread (thread safety unverified; BattleLoadRenderWaitProbe.cs:16-19 keeps it on the main thread
  by design, #592/#595). Never lower process priority during a battle compile. Open questions: rgl log
  flush latency on v1.4.8 and visibility over exclusive fullscreen (both owed checks on 1.5.x too).

## 4. Go or no-go

### 372773d1a ShaderCacheKeeper: NO-GO now

What argues for it on 1.4.x (stronger than on 1.5.x): 1.4.x still ships the ShaderPrecompilation walk
active (origin/bannerlord-1.4.5 Main/SubModule.cs:421-426 and :638-664; the 1.5.x re-park 25995dc91 did not
land there), a cold walk costs 20 to 70 minutes (shader-precompilation.md:19), and v1.4.8 deletes the whole
CoreShaders cache, walk output included, on any set change (2c). Toggling a shader-less mod (BetterTime,
FastMode, NavalDLC) therefore costs a 1.4.x player the walk and cold battles of the E4 kind.

Why not now:

1. Claim 8 is unproven and has a known counterexample path (NavalDLC without War Sails). Its failure mode is
   strictly worse than no keeper.
2. No documented 1.4.x delivery route for a bin-folder file (claim 10).
3. On 1.5.x it never shipped, the in-game check never ran, and the review lenses never ran. Porting an
   unverified native component to the older line first inverts the risk order.
4. The frequency of shader-less list changes among TAOM 1.4.8 players is unmeasured: E1/E2 kept the same
   list and a warm cache; E3/E4 have no rgl log.

**Player-install implications (flag to Mike before any GO):** the file lands in
`<game>\bin\Win64_Shipping_Client\xinput9_1_0.dll`, outside TAOM's module folder, and loads into every
`Bannerlord*` process that loads the engine: vanilla, multiplayer and other mods, not only TAOM. It is
unsigned and carries a Windows library's name (the DLL-hijack pattern antivirus heuristics look for). It
survives a TAOM uninstall and a Steam integrity check (Steam does not remove extra files), and per the
1.5.x doc the launcher never removes it; retiring it needs a do-nothing build. The launcher would overwrite
any other tool's `xinput9_1_0.dll` proxy, including yotthani's own copy. A defect in `DllMain` outside the
narrowed SEH filter stops the game from starting for vanilla too, and players will not know the `.off`
switch. It silently suppresses TaleWorlds' own "Mod change detected" safeguard, whose text says it exists
to avoid rendering issues, so any rendering fault after a kept cache will be reported as a TAOM bug.

### 82645b53f ShaderCompileNotice: GO, low priority, adapted

Port the 82645b53f shape (not c91380274's Client split; 1.4.x has no TAOM.Client). Adaptations and gates are
in the plan inputs. Do not present it as a fix for the E3/E4 battle freezes.

### Related findings for other work packages

- Doc fix: shader-precompilation.md:19 and :136 on 1.4.x claim reordering discards the cache (claim 11).
- Optional new diagnostic (not a port): at `OnSubModuleLoad`, stat `shader_mapping.bin` and count `*.sacx`;
  a mapping written after this process started means the engine dropped or first built its runtime cache
  this start (the writer truncates to a fresh header, 2d). One log line would answer "was this battle cold
  because the cache was dropped at startup?" in future player logs. Read-only file access, no engine call.

## 5. Plan inputs

See the structured result's `plan_inputs`; it is the self-contained version of this section.

## 6. Recommended skills (this verifier cannot invoke them)

`/research` (trace what fills `[[0xd96cf0]+0x8a8]` before RVA 0x72c8e, if Mike wants the keeper gate
settled statically); `/native-crash-triage` tooling (`tools/native_crash_triage.py`) for naming the
functions above; `/verify` then `/deep-review` for the notice port (C# in two or more files plus a
feature module); `/localize` for the three strings (paid, Mike's call); `/adopt-external` was already run
on 1.5.x for the keeper and should not be repeated; `/security-scan` is not applicable (no .claude change
in the notice port; the keeper port touches .claude/rules/native-cpp-ports.md and the release skill, so run
it if the keeper ever goes ahead).
