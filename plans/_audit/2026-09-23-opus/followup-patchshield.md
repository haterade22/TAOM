# Follow-up: PatchShield pass 2 on the first campaign load

Auditor: Opus follow-up, run `2026-09-23-opus`, baseline `b2e387db`. Read-only. Code reads are
`git show b2e387db:<path>`. Runtime evidence is read-only: `Modules/TAOM.Dependencies/diag.log`, the
game's `bin\Win64_Shipping_Client\Logs\taom_debug_*.log` and `C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_*.txt`.

Question: is PatchShield's second pass a real, large, reducible cost on the first campaign load, and
what is the cheapest safe reduction?

## Progress notes (appended as I go)

- Source read: `Dependencies/Foundation/PatchShield.cs` (452 lines at `b2e387db`),
  `Dependencies/Foundation/PatchShieldPolicy.cs`, `Dependencies/SubModule.cs:164-292`.
  Pass 1 is `OnSubModuleLoad` (`Dependencies/SubModule.cs:234`); pass 2 is
  `OnGameInitializationFinished(Game)` (`:288`). `Install` (`PatchShield.cs:130-244`) enumerates
  `Harmony.GetAllPatchedMethods()` and calls `harmony.Patch(method, finalizer: ...)` once per method
  not yet in `_shielded` (`:222-223`), skipping TAOM-declared methods (`:184-190`), the Gauntlet and
  TwoDimension namespaces (`:60-69`, `:197`) and SaveShield targets (`:210`). The whole loop runs
  under `lock (_lock)` on the calling thread.

- Measured (script `parse_diag.py` in my session scratchpad: pairs every `PatchShield.Install (pass N)`
  line with the next `shield pass: +N new` line in `diag.log`, 35,502 lines, 2026-05-27 to 2026-09-23).
  Latest 16 days: first pass 2 of each process is `+372 new` in 69.1 to 69.4 s (median 69.3 s),
  186 ms per attach; pass 1 is `+30 new` in 5.6 s, the same 186 ms per attach. The 69 s holds.
- **The per-attach cost is bimodal and environmental, not code-driven.** 2026-05-27 to 2026-06-12
  13:38 and again 2026-09-01 to 2026-09-03: pass 2 `+329..+354` in 1.9 to 3.4 s (5.4 to 10 ms per
  attach). 2026-06-12 14:08 onward (except those three days): 62 to 70 s (164 to 199 ms per attach).
  The diag.log content of a fast session (2026-09-01 10:26:34, lines 28524-28602) and a slow one
  (2026-08-31 17:51:59, lines 28445-28523) is identical once timestamps are cut (`diff` of
  `cut -c25-`): same modlist, same counts, same Harmony `0Harmony v2.4.2.0`.
- The slowdown hits EVERY `Harmony.Patch` in the process, including first-time patches of
  unpatched methods: `CollectAssemblyTypesShim` patching `Assembly.GetTypes` took 5 ms on
  2026-09-01 (10:26:35.843 to .848) and 134 ms on 2026-08-31 (17:52:11.308 to .442);
  `SubModuleConstructionGuard` on `Module.AddSubModule` 6 ms against 123 ms; the UIExtenderEx cctor
  (its own patches) 0.56 s against 4.04 s. So the cost is NOT the "re-patch rebuilds a method with
  many prior patches" shape the brief hypothesised; it is a flat per-`Patch` tax of about 120 to
  190 ms whenever the process runs in the slow mode.
- Engine read (v1.5.3 decompile cache): `MBSubModuleBase.OnGameInitializationFinished` is NOT a
  main-menu hook. `MBGameManager.OnGameInitializationFinished` (`TaleWorlds.MountAndBlade.MBGameManager.cs:110-115`)
  fans out to every submodule; it is called from `Campaign.OnInitialize`
  (`TaleWorlds.CampaignSystem.Campaign.cs:1471`, reached from `Game.Initialize`, `TaleWorlds.Core.Game.cs:301`,
  in the `InitializeFirstStep` loading step, `Campaign.cs:1661-1663`) and from `CustomGame`
  (`TaleWorlds.MountAndBlade.CustomBattle.CustomGame.cs:56`). So pass 2 runs synchronously inside the
  game-loading screen of the FIRST campaign load, new campaign OR custom battle, of each process.
  The diag label "(main menu reached)" (`Dependencies/SubModule.cs:276`) is wrong, and so is the
  crash-loop detector's premise that it marks "reached main menu" (`:267-272`, `MarkSessionLaunchSuccessful`).
- Player-visible share, session `taom_debug_2026-09-23_12-43-26.log` (a CUSTOM BATTLE load, not a
  campaign): `[MemStation] enter screen='GameLoadingScreen'` 12:45:44 (line 18507) to
  `exit screen='GameLoadingScreen'` 12:47:31 (line 18567) = about 107 s of loading screen; pass 2 is
  12:45:59.364 to 12:47:08.623 in diag.log (lines 35330-35331) = 69.3 s, about 65% of it. TAOM's own
  `[SaveLoad] seq=1 phase=GameInitializationFinished` lands at 12:47:08 (line 18552), right after
  pass 2 returns, confirming the same main-thread call chain (TAOM.Dependencies is first in the
  module list, so its override runs first).
- **Value delivered locally: none observed.** `grep -c "\[PatchShield\] swallowed" diag.log` = 0 over
  466 sessions (`grep -c "session start"`); every one of the 914 `[PatchShield]` lines is a
  `shield pass` or a `SESSION SUMMARY`, and all four summaries read `swallowed 0 exception(s)`,
  `unpatched 0 target(s)` (diag.log lines 7327, 12880, 27538, 30409). The six `swallowed` lines in
  the file are SaveShield's (lines 3284-3289).
- **Most of pass 2 is the Native2Managed sweep, shielded a second time.** Native2Managed attaches
  its finalizer to every static method of `ManagedCallbacks.LibraryCallbacksGenerated`,
  `CoreCallbacksGenerated` and `EngineCallbacksGenerated` in TAOM's `OnSubModuleLoad`
  (`Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:62-88`), so those methods are
  Harmony-patched by the time pass 2 runs. PatchShield's filters do not exclude them: the declaring
  assemblies are `TaleWorlds.*.AutoGenerated` (not `TAOM*`, `PatchShield.cs:184-185`) and the
  namespace is `ManagedCallbacks` (not in `ExcludedTargetNamespacePrefixes`, `:60-69`). Count check
  (ilspy decompile of the three installed v1.5.3 DLLs into my scratchpad): 43 + 112 + 86 declared
  static methods plus a static `Delegates` property (getter and setter) on each type = 45 + 114 + 88
  = **247**, exactly the logged `Native2Managed: attached 247`. So about 247 of pass 2's 372 attaches
  (66%) are those shims: about 46 s of the 69 s at the measured 186 ms per attach. The other ~125
  are TAOM's own TaleWorlds patch targets plus the vendored BUTR stack's. (Attribution is by code
  path and count identity; no runtime log lists pass 2's methods, so the exact split is UNVERIFIED.)

## Measurements

**Pass 2 per process, whole diag.log** (`parse_diag.py` then a grouping script; slow = more than
100 ms per attach, fast = under 20 ms): 413 first-in-process pass 2 runs; 259 slow (median 66.7 s),
154 fast (median 2.3 s), none in between. Since 2026-09-04: 67 of 67 slow. The slow runs add up to
4.9 hours of pass-2 stall on this machine.

**Recent first loads, diag.log pass 2 against the taom_debug loading screen**
(`grep "screen='GameLoadingScreen'"` in each `taom_debug_*.log`):

| Process (diag session) | Load kind | Loading screen (taom_debug) | Pass 2 (diag.log) | Share |
|---|---|---|---|---|
| 2026-09-22 11:01 | custom battle | 11:04:23 to 11:06:11, 108 s | 69.1 s, `+372` | 64% |
| 2026-09-22 13:48 | custom battle | 13:50:46 to 13:52:35, 109 s | 69.4 s | 64% |
| 2026-09-22 15:59 | custom battle | 16:01:54 to 16:03:43, 109 s | 69.3 s | 64% |
| 2026-09-22 16:22 | custom battle | 16:24:18 to 16:26:05, 107 s | 69.2 s | 65% |
| 2026-09-22 16:37 | custom battle | 16:38:56 to 16:40:42, 106 s | 69.2 s | 65% |
| 2026-09-22 16:48 | custom battle | 16:49:55 to 16:51:41, 106 s | 69.3 s | 65% |
| 2026-09-22 17:02 | custom battle | 17:04:12 to 17:05:59, 107 s | 69.4 s | 65% |
| 2026-09-23 07:46 | new campaign | 07:48:38 to 07:50:35, 117 s | 62.5 s | 53% |
| 2026-09-23 12:42 | custom battle | 12:45:44 to 12:47:31, 107 s | 69.3 s | 65% |
| 2026-09-23 13:04 | custom battle | 13:05:58 to 13:07:46, 108 s | 69.3 s | 64% |
| 2026-09-23 13:43 (1st) | custom battle | 13:50:16 to 13:52:04, 108 s | 69.4 s | 64% |
| 2026-09-23 13:43 (2nd) | new campaign | 13:54:32 to 13:55:30, 58 s | 26.5 s, `+141` | 46% |

A second game start in the same process pays again for whatever was patched since the first pass:
`+139` to `+141` new in 26.0 to 26.5 s (diag.log lines 34127-34128, 34534-34535, 35500-35501). Those
are patches applied after the first pass 2 (TAOM's own late batch lands right after it, for example
the `[TableauDiag] PatchCategory ... applied OK` lines at 12:47:08 to 12:47:10 in
`taom_debug_2026-09-23_12-43-26.log` lines 18553-18562); their full composition is UNVERIFIED.

**Per-attach cost mechanism (Harmony 2.4.2, the vendored `0Harmony.dll` decompiled into my
scratchpad):** every `Harmony.Patch` deserialises the method's `PatchInfo`, adds the new patch,
rebuilds the whole replacement from every prefix, postfix, transpiler and finalizer, re-detours, and
re-serialises (`HarmonyLib/PatchProcessor.cs:127-139`, `PatchFunctions.cs:21-45`,
`HarmonySharedState.cs:97-126`; the serialiser is `BinaryFormatter`, `PatchInfoSerialization.cs:32-44`).
So a re-patch does cost more on a method with many patches, as the brief suspected, but the logs
show that component is small: in fast mode pass 1 costs about 4 ms and pass 2 about 7 ms per
attach; in slow mode both cost 186 ms. The dominant term is the flat per-`Patch` tax of the slow
mode, and the only lever inside TAOM's control is the NUMBER of attaches. PatchShield already
attaches once per method, not per patch (`_shielded` dedupe, `PatchShield.cs:179,224`).

**Why the slow mode exists: UNVERIFIED.** No commit on any branch between the last fast launch
(2026-06-12 13:38) and the first slow one (14:08) (`git log --all --since="2026-06-12 10:00"
--until="2026-06-12 15:00"` is empty); engine version unchanged across both transitions (diag.log
`VersionProbe` lines: 1.4.6 from 2026-06-11, 1.4.8 from 2026-08-20 to 2026-09-14). Ruled out: the
user environment variable `HARMONY_LOG_FILE=E:\Logs` (`HKCU\Environment`) only sets
`FileLog.LogPath` (`HarmonyLib/FileLog.cs:42`), which is written only when `Harmony.DEBUG` or a
`[HarmonyDebug]` patch is on (`Harmony.cs:27-31`, `PatchFunctions.cs:23`); `HARMONY_DEBUG` is unset,
TAOM has no `HarmonyDebug`/`FileLog.` use (`git grep`), and `E:\Logs` does not exist, so nothing
has ever been written there. MonoMod picks the `DynamicMethod` generator on .NET Framework (no
per-patch `Assembly.Load(byte[])`, `MonoMod/Utils/DynamicMethodDefinition.cs:354,520-536`). Not
ruled out: Defender real-time and behaviour monitoring (both on and tamper-protected per
`Get-MpComputerStatus`; exclusions need admin to read), a profiler or debugger at launch, or another
machine-level change. This is an environment item: report, don't fix. Player machines may be in
either mode; nothing here measures a player's per-attach rate.

## Reduction options, cheapest first

1. **Exclude `ManagedCallbacks` from PatchShield** (add the namespace to
   `ExcludedTargetNamespacePrefixes`, `PatchShield.cs:60-69`). Removes about 247 of 372 pass-2
   attaches: about 46 s per first load in slow mode (about 1.5 s in fast mode), plus a stacked
   `__originalMethod` finalizer on the engine's native-to-managed callback shims (Harmony emits
   `ldtoken` + `MethodBase.GetMethodFromHandle` on every call for that parameter,
   `HarmonyLib/MethodPatcherTools.cs:145,186`; per-call cost UNMEASURED). Coverage lost: PatchShield
   only ever saw those shims because TAOM's own sweep patched them; on them its rescue half is inert
   (the shims carry only finalizers, and the rescue strips prefixes, postfixes and transpilers only,
   `PatchShield.cs:398-400`; see the correction section below), and its swallow
   half duplicates Native2Managed's finalizer, which swallows every exception while crash capture is
   on (`Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:29-53`). The one uncovered case is the non-default mix
   `EnableCrashCapture` off with `EnableNativeToManagedCapture` on, for a missing-API exception from
   unpatched mod code bubbling to a callback shim. It also removes a finalizer stack that PatchShield
   already refuses for SaveShield targets for the shared-exception-slot reason (`:204-215`), and
   follows the #331 "never shield the hot layer" rationale (`:50-59`). Effort S; risk LOW. It reaches
   every existing install, including the ones whose persisted json2 pins Native2Managed on (trap
   index "Persisted MCM defaults"), which PERF-L4-01's default flip alone does not.
2. **PERF-L4-01's fix does most of this by itself for new defaults.** With the sweep off, the 247
   shims are not Harmony-patched, so pass 2 never sees them. Option 1 is what keeps opt-in users
   and existing json2 files from paying the sweep twice.
3. **Skip targets whose every owner is protected** (read `Harmony.GetPatchInfo` owners, skip when
   all match `PatchShieldPolicy.IsProtectedOwner`, and do NOT add them to `_shielded` so a later
   foreign patch still gets shielded on the next pass). In a TAOM-only modlist this removes the
   remaining ~125 attaches (about 23 s slow, about 1 s fast), and the `+139..+141` second pass. The
   tracked `crashz/report.txt:48-66` (a v2.0.18 report) shows the owner set in such a process:
   `com.taom.mod` plus the vendored ButterLib, UIExtenderEx and MCM ids and TAOM.Dependencies' own.
   All are protected EXCEPT `com.taom.mod` itself (see the correction below), so this option needs
   that id added to the protected list first. Cost: TAOM's own patch targets lose the silent trinity swallow after engine drift;
   those exceptions would surface through TAOM's crash reporter instead (arguably more visible, but a
   posture change the maintainer must decide). The owner lookup deserialises `PatchInfo` per method
   (`HarmonySharedState.cs:97-108`); its cost in slow mode is UNMEASURED. Effort S to M; risk MED.
4. **Make the tax visible.** Append elapsed ms and ms per attach to the existing `shield pass` line
   (`PatchShield.cs:237`); diag.log already rides in crash bundles (`CrashBundleWriter.cs:19,48`), so
   player bundles would show which mode players run in. Effort S; risk nil.
5. **Rejected:** deferring pass 2 off the loading path (a background-thread `Harmony.Patch` rewrites
   code the main thread may be executing, and spreading it over gameplay frames turns one hidden stall
   into visible hitches); batching (Harmony has no batch API, each `Patch` rebuilds and re-detours);
   "attach once per method" (already so).
6. **Fix the label.** `OnGameInitializationFinished` fires at a game start, not the main menu
   (`Dependencies/SubModule.cs:276` and `docs/migration/v1.5.2-impact.md:141`, which places the 70 s
   before the main menu). Doc-only.

## Relation to PERF-L4-01

Same cost shape: count of `Harmony.Patch` calls times a flat per-call tax that is about 186 ms on
this machine in slow mode and about 5 ms in fast mode. And the SAME 247 methods: Native2Managed pays
about 31 s at boot to patch them, then PatchShield pays about 46 s at the first game start to patch
them again. PERF-L4-01's true per-process cost in slow mode is therefore about 77 s, not 31 s.
Neither finding's absolute seconds can be generalised to players until a player's per-attach rate
is seen (option 4).

## Side observation (not verified as a finding)

`IncompatibleModDetector.MarkSessionLaunchSuccessful` runs from the same `OnGameInitializationFinished`
(`Dependencies/SubModule.cs:278`), so a process that reaches the main menu but never starts a game
leaves the launch marker behind. diag.log: 453 `RunEarlyPhase: entered`, 413 first pass 2 runs, and
exactly 40 `previousCrashLoop=True` (453 minus 413 = 40), 35 of them naming "1 mod(s) added ...
likely culprit(s)". The crash-loop heuristic may be firing on normal quit-from-menu sessions. Lane 1
material; I did not trace it further.

## Correction and a second observation: TAOM's own Harmony id is not a protected owner

While checking option 1's "rescue half is inert" claim I found it true for a different reason than I
first wrote. TAOM's single Harmony instance is `new Harmony("com.taom.mod")` (`Main/SubModule.cs:194`;
`git grep -h -o 'new Harmony("[^"]*")' b2e387db -- Main` finds only that id). The protected list
matches by case-insensitive `StartsWith` (`PatchShieldPolicy.cs:84-95`) against prefixes that begin
`"TAOM"`, `"Bannerlord..."`, `"MCM"` and so on (`:23-63`); none is a prefix of `com.taom.mod`, and
`coop-modules.txt` adds none (its `[harmony-owner-prefixes]` section is empty). So:

- On the 247 Native2Managed shims the rescue is still a no-op, but only because the shims carry
  nothing but finalizers and `TryUnpatchOffendingPatches` strips prefixes, postfixes and
  transpilers only (`PatchShield.cs:398-400`). Option 1's conclusion stands.
- On every other TaleWorlds method TAOM patches, a `MissingMethodException`,
  `MissingFieldException` or `TypeLoadException` crossing the shield (from any patch on it, or from
  the original body's callees) makes PatchShield call `harmony.Unpatch(..., owner: "com.taom.mod")`
  for that method's prefixes, postfixes and transpilers (`:378-401`), silently disabling that TAOM
  feature for the rest of the session outside co-op. The 2026-07-31 comment at
  `PatchShieldPolicy.cs:29-37` shows the author meant TAOM's own patches to be protected (it fixed
  the same gap for UIExtenderEx's ids). Never fired locally (0 `unpatched owner` lines in diag.log).
  Not a performance item and not verified beyond the code read; handing it to the orchestrator as a
  correctness lead (Lane 1). A one-line `"com.taom."` entry plus a `PatchShieldPolicyTests` case would
  close it, and is a prerequisite for option 3.

## Verdict

- **Real:** yes. 69.1 to 69.4 s on every first game start since 2026-09-04 (67 of 67), about 64% of
  a custom battle's loading screen and 53% of a new campaign's; a second game start in the same
  process adds 26 s more.
- **Large:** on this machine, yes; for players, UNVERIFIED. The seconds are count times a per-`Patch`
  tax that is environmental (186 ms here since 2026-06-12, 5 to 10 ms before and on 2026-09-01 to
  09-03). In the fast mode the same pass costs 2.3 s.
- **Reducible:** yes, by cutting the attach count, which helps in either mode. Cheapest safe step:
  exclude `ManagedCallbacks` (about 247 of 372 attaches, about 46 s here), which are only patched
  because of PERF-L4-01's sweep.
- **Refuted:** no.

## What I did not cover

- No game launch, no build, no Stopwatch: all timings come from log timestamps (diag.log has
  millisecond resolution, taom_debug has seconds). The per-attach rate is total time over attach
  count, not a direct measurement of one `Harmony.Patch`.
- Root cause of the slow mode: not found. I did not read Defender exclusions (admin only), the
  Defender or application event logs, or look for a profiler registration beyond the environment
  variables of this shell and `HKCU\Environment`.
- The exact method list of pass 2 (no runtime census of it exists in the logs); the 247 figure is by
  code path plus count identity, the remaining ~125 by subtraction.
- The composition of the `+139..+141` second-game-start pass.
- The per-call runtime cost of PatchShield's `__originalMethod` finalizer on the callback shims
  (UNMEASURED; `[MissionPerf]` A/B would measure it).
- rgl logs: only 2026-09-23 files remain on disk, and they add nothing to the diag.log timeline for
  this question, so I did not use them beyond confirming the date range.
- Player crash bundles held elsewhere (the only per-attach evidence would be their diag.log).
- The `com.taom.mod` protected-owner gap and the crash-loop marker observation are leads, not
  verified findings; I did not try to refute them beyond the code read and the counts given.
