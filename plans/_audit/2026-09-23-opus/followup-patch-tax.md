# Follow-up: is the ~186 ms per Harmony.Patch "slow mode" local to this desktop?

Auditor: Opus follow-up, run `2026-09-23-opus`, baseline `b2e387db`. Read-only. Question from
`critic.md` section 4 and `followup-patchshield.md` ("Why the slow mode exists: UNVERIFIED").

## Progress notes (appended as I go)

- **Sources found (step 1).** No diag.log from any machine other than this desktop exists: the four
  player bundles in `C:\Users\mikew\Downloads` carry `report.txt`, `report.json`, `taom_debug.log`,
  `rgl_log.txt`, `manifest.txt` only (`unzip -l`, `ls -la`), and the repo's only tracked bundle,
  `crashz/`, is this desktop's (its `0Harmony.dll` path is `E:\Steam\...`, `crashz/report.txt`). So I
  used the brief's second method: the `taom_debug` gap from `[SaveDefiners]` to
  `[CrashReport] Native2Managed: attached 247 Finalizer(s)`. In every log read (player and desktop)
  the two lines are adjacent (0 lines between), so the gap is the Native2Managed sweep of 247
  `Harmony.Patch` calls plus nothing logged. Script: `gap.py` in my session scratchpad. Resolution
  is 1 s, so a 0 to 1 s gap bounds the rate at under about 8 ms per attach (2 s / 247).
- **Machine identity** comes from the absolute `0Harmony.dll` path each bundle's `report.txt` prints,
  and the commit limit in `manifest.txt` (`commit used/limitMB`). This desktop is `E:\Steam\...`.

## Measurements: players against this desktop

| Source | Install (0Harmony path) | Commit limit | TAOM / engine | Gap, 247 attaches | ms per attach | Mode |
|---|---|---|---|---|---|---|
| `Downloads\taom_crash_20260904_211527_b18f3441\taom_debug.log:3-4` | `C:\Program Files (x86)\Steam\...` | 37,205 MB | v2.0.26 / 1.4.8 | 22:20:02 to 22:20:02, 0 s | under 4 | fast |
| `Downloads\TAOM_crash_report_light_2026-09-06\taom_crash_20260905_181715_065939b6.zip!taom_debug.log:3-4` and `:558-559` | `E:\SteamLibrary\...` | 35,553 MB | v2.0.27 / 1.4.8 | 0 s, 0 s | under 4 | fast |
| same player, loose `taom_debug_2026-09-05_20-19-39.log`, `09-06_15-41-12`, `09-06_16-13-19`, `09-06_16-24-25` | (same folder) | | | 1, 0, 0, 0 s | under 8 | fast |
| `Downloads\taom_crash_20260914_221339_fae8a756.zip!taom_debug.log:3-4` | `W:\SteamLibrary\...` | 42,664 MB | v2.0.28 / 1.4.8 | 23:56:28 to 23:56:29, 1 s | under 8 | fast |
| `Downloads\taom_crash_20260923_010057_2d446100\taom_debug.log:3-4` | `C:\Program Files (x86)\Steam\...`, plus `Bannerlord.Harmony` and `TAOM_Standalone_Compat_RBM` modules | 69,371 MB | v2.0.28 / 1.4.8 | 20:34:19 to 20:34:20, 1 s | under 8 | fast |
| loose `Downloads\taom_debug_2026-09-20_19-27-03.log`, `taom_debug_2026-09-23_02-24-36.log` (install unknown; BuildStamps `build.20260914-125931Z` and `build.20260915-183030Z`) | unknown | | v2.0.28 era | 0 s, 0 s | under 4 | fast |
| **This desktop**, `crashz/report.txt:407-408` (2026-08-08, v2.0.18, engine 1.4.7) | `E:\Steam\...` | | | 21:36:44 to 21:37:18, 34 s | about 138 | slow |
| **This desktop**, all 30 `taom_debug_*.log` on disk, 2026-09-19 to 09-23 (game `Logs` folder) | `E:\Steam\...` | | v2.0.30 / 1.5.3 | 29 to 33 s each | 117 to 134 | slow |

At least three distinct player installs (three install roots: `C:\Program Files (x86)\Steam`,
`E:\SteamLibrary`, `W:\SteamLibrary`), probably four: the two `C:\Program Files (x86)\Steam` bundles
differ in commit limit (37,205 against 69,371 MB) and modlist. 11 distinct player processes (the 09-05 zip's log is the same file as the loose
`taom_debug_2026-09-05_20-15-48.log`, counted once), all fast
(0 or 1 s); no player process is slow. Every desktop process on disk is slow (29 to 34 s).
Caveat: the player builds are v2.0.26 to v2.0.28 on engine 1.4.8, the recent desktop ones v2.0.30 on
1.5.3; but the desktop was already slow on 1.4.7 (crashz, 2026-08-08) and, per
`followup-patchshield.md`, on 1.4.8 from 2026-09-04 to 09-14, so engine version does not explain it.

## Step 2: what differs on this desktop (read-only)

- **Exact flip times** (script `modes.py` in my scratchpad; per diag.log session, pass 1 and pass 2
  ms per attach): last fast 2026-06-12 13:38:28 (diag.log L11220, pass 1 `+46` in 0.05 s), first
  slow 14:08:30 (L11286, pass 1 8.71 s, pass 2 62.93 s, 187 ms per attach). Fast window
  2026-09-01 10:26:34 (L28524) to 2026-09-03 18:30:24 (L29439), 12 sessions, 3.3 to 9.6 ms per
  attach; slow on both sides (2026-08-31 17:51:59 L28445; 2026-09-04 10:31:15 L29518).
- **The slow mode is not Harmony-specific.** The phase between TAOM.Dependencies'
  `InstallAssemblyResolveHandler: hook registered` and its `OnSubModuleLoad: entered` (the engine
  constructing every module's submodules and loading assemblies) takes 0.47 to 0.80 s in fast
  sessions (L11157, L11220, L28524, L29439) and 7.4 to 7.5 s in slow ones (L11286, L11362, L28445,
  L29518). The whole managed startup slows about tenfold, not just `Harmony.Patch`: the shape of a
  process under a debugger or instrumentation, not of a Harmony code path.
- **No reboot at the September flips.** System log boots (Kernel-General 12): 2026-08-30 07:03,
  then 2026-09-08 19:44. Fast began and ended inside one boot session, so boot-time settings
  (core isolation, HVCI, drivers loaded at boot) cannot explain it. The June flip predates the
  System and Application logs (they begin 2026-06-27 and 06-23).
- **Ruled out or not correlated:**
  - Windows updates: `Get-HotFix` lists only 2025-10-17, 2026-09-09 and 09-14; the Setup log (from
    2026-06-10) has no event 2026-06-11 to 06-13 or 2026-08-30 to 09-04.
  - Software installs: Uninstall-key `InstallDate` has nothing on 06-11 to 06-13 or 08-31 to 09-04
    (nearest: VC++ runtimes 06-10, DaVinci Resolve 06-16, "Bannerlord Online" 08-25).
  - Harmony and MonoMod binaries: the only `0Harmony.dll` copies (TAOM.Dependencies client and
    server, DOTS.Dependencies) are all 2025-11-12 18:23:40, 2,461,696 bytes; no MonoMod DLLs in any
    `Modules/*/bin`. `Bannerlord.Harmony` on this desktop is a `_Module`-only folder created
    2026-09-14 and is not in the active list (`[Engine] ... modules(10)=[...]`, today's
    `taom_debug_2026-09-23_13-43-40.log` line 3).
  - Environment: only `HARMONY_LOG_FILE=E:\Logs` (user). Re-verified in my own decompile of the
    shipped `0Harmony.dll`: `FileLog.LogPath` reads it (`HarmonyLib/FileLog.cs:42`), but
    `FileLog.Debug` writes only `if (Harmony.DEBUG)` (`FileLog.cs:250-256`), and `DEBUG` is set only
    from `HARMONY_DEBUG` (`Harmony.cs:27-31`), which is unset; no `harmony.log.txt` on the Desktop,
    `E:\Logs` absent. No `COR_*`, `COMPlus_*`, `DOTNET_*` or `MONOMOD_*` variables at machine or
    user level (MonoMod reads `MONOMOD_*` switches, `MonoMod/Switches.cs:63-71`). No Image File
    Execution Options key for `Bannerlord.exe` or the launchers.
  - Defender: exclusions for `...\Mount & Blade II Bannerlord\bin` and process `Bannerlord.exe` were
    added 2026-05-20 13:18 (Operational event 5007) and never removed in the log, so they span fast
    and slow alike. The only 5007 near the June flip is an ECS `ETag` change at 14:10:43, two
    minutes AFTER the first slow launch. Feature `Controls\77` was removed 2026-08-31 20:18 (before
    the fast window) but re-added 2026-09-02 22:40 while 09-03 stayed fast, and was absent from June
    to 08-28 while the desktop was slow: not the cause.
  - `cpuz163` kernel driver installs (System 7045, `C:\ProgramData\CPUID Software\sdk\`) precede
    launches by seconds in both fast (09-01 10:26:27, 09-03 18:30:22) and slow (08-31 17:51:46,
    09-04 10:31:04) sessions: not discriminating.
- **Most likely local cause (UNVERIFIED, strong circumstantial evidence): the Visual Studio launch
  profile runs the game under the mixed-mode (managed plus native) debugger.**
  - `Main/Properties/launchSettings.json`, profile `Bannerlord` (the `ActiveDebugProfile` in the
    untracked `Main/TAOM.csproj.user`): `"debugEngines": "managed-framework,native"`. History:
    `git show 4236e6c9 -- Main/Properties/launchSettings.json` (committed 2026-06-13 18:24, inside an
    unrelated "harvest.py" commit) replaces `"nativeDebugging": false` with that line. The previous
    commit to the file is `dbba8e9a` (2026-05-25), so the edit was made in the working tree between
    05-25 and 06-13 18:24; the slow mode starts 06-12 14:08. The timing fits; the edit's exact time
    is not recoverable.
  - Today's slow sessions were launched from that profile: `rgl_log_42764.txt:35`, `rgl_log_91864`,
    `rgl_log_90868` (the 12:42, 13:04 and 13:43 sessions, all slow in diag.log) carry
    `Command Args: /singleplayer _MODULES_*TAOM.Dependencies*Native*SandBoxCore*CustomBattle*Sandbox*StoryMode*BirthAndDeath*FastMode*LOTRLOME_Armory*TAOM_Map*TAOM*_MODULES_`,
    the profile's string exactly (CustomBattle before Sandbox, plus `FastMode`, which is not
    installed on this desktop). The desktop bundle `crashz/rgl_log.txt` (2026-08-08, slow) carries
    the older profile string with `Alliance.Wargs`. Player launcher lines differ (for example
    `...Native*SandBoxCore*Sandbox*StoryMode*CustomBattle*...` in the 09-04 and 09-14 bundles; the
    09-05 player also passes `FastMode`, but in launcher order, so the signature is the order, not
    that name).
    `devenv` is running now (started 2026-09-23 07:43).
  - Why it fits the numbers: a native debugger in interop mode stops the process for managed debug
    events, and every `Harmony.Patch` emits and JITs a DynamicMethod and detours native code; the
    same overhead would hit assembly loading (the 7.4 s pre-`OnSubModuleLoad` phase). This is general
    debugger behaviour, not measured here.
  - NOT proven: that a debugger was attached in the slow sessions (Start Without Debugging uses the
    same command line), and how the 09-01 to 09-03 sessions were launched (no rgl or taom_debug log
    from those days survives; diag.log records neither the command line nor a debugger flag). A
    one-launch check settles it: start the same profile with Ctrl+F5, or set the profile to
    managed-only, and read the next `shield pass` pair in diag.log (fast is about 2.5 s for pass 2).

## Verdict

- **Players: fast.** Every non-desktop log available (11 processes, at least three and probably four
  installs, TAOM v2.0.26 to v2.0.28) sweeps the same 247 Native2Managed attaches in 0 to 1 s, under
  about 8 ms per attach. Proving lines, for example
  `Downloads\taom_crash_20260923_010057_2d446100\taom_debug.log:3-4`:
  `[2026-09-22 20:34:19] [DEBUG] [SaveDefiners] 80 definer(s) ...` then
  `[2026-09-22 20:34:20] [INFO] [CrashReport] Native2Managed: attached 247 Finalizer(s)`, against
  this desktop's 32 s for the same pair (`taom_debug_2026-09-23_13-43-40.log:4-5`).
- **The 186 ms tax is local to this desktop**, and it is a whole-startup slowdown, not a Harmony one.
  Consequence for the ranking: PERF-L4-01 (31 s) and PatchShield pass 2 (69 s) are about 1 to 3 s on
  player machines. Their fixes stay cheap and worth doing, but they do not belong at the top of a
  player-facing list; the 69 s is a developer-loop cost on this desktop.
- **Local cause: most likely the VS `Bannerlord` profile's mixed-mode debugging**
  (`launchSettings.json` `debugEngines: managed-framework,native`, introduced in the working tree
  before `4236e6c9`). UNVERIFIED until one Ctrl+F5 or managed-only launch is timed. Environment and
  tooling item: report, don't fix.

## What I did not cover

- GitHub issues: I did not run `gh issue view` on attachments; the Downloads bundles already gave
  at least three installs. Other players' bundles attached to issues may exist.
- No player diag.log exists in any bundle (bundles carry taom_debug, rgl, report and manifest only),
  so the player rate uses the Native2Managed gap at 1 s resolution, not the PatchShield pass pairing,
  and pass 2 itself is not measured on any player machine. Player builds are v2.0.26 to v2.0.28 on
  engine 1.4.8; the desktop was slow on 1.4.7, 1.4.8 and 1.5.3 alike.
- Did not read the Defender exclusion list itself (registry needs admin, open error 5) or
  `Get-ProcessMitigation` for Bannerlord.exe; no IFEO key exists for it.
- Did not identify how the 2026-09-01 to 09-03 sessions were launched; no surviving log records it.
- No game launch, no build, no settings change.
