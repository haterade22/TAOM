# Codex pre-review: removal of BattleLoadStallWatchdog (2026-11-12)

You review an uncommitted change in the working tree of this repository (TAOM, a Bannerlord total
conversion, .NET Framework 4.7.2). Read only. Do not edit any file, do not run git commands that
change a ref, the index or the working tree, and do not run `./build.ps1`.

## The change and why

The maintainer asked for the battle-load stall watchdog to be removed. A thread-pool timer
(`BattleLoadStallWatchdog`) wrote a `WATCHDOG STILL LOADING` line and a crash-bundle ZIP once a mission
load had been open 300 s. A player on a cold shader cache hit it: the engine compiled 1,388 shaders in
300 s while the main thread sat in the native render wait, and the player read the bundle as a crash.
The watchdog is deleted. Phase logging, the 1 Hz `WaitingForRender` stamps, the stale-marker notice on
the next launch and `MissionTickStallWatchdog` (a frozen battle after the load) stay.

Use `git diff -- <path>` for modified files and `git show HEAD:<path>` for deleted files. The working
tree also holds another session's uncommitted work (MissionStartGuard, SpecialResources, EliteEmissary):
ignore it. In `Main/SubModule.cs` only one deleted line is part of this change: the
`BattleLoadStallWatchdog` `.Start()` call.

## Files in scope

Deleted: `Main/Features/BattleLoadDiagnostics/BattleLoadStallWatchdog.cs`, `BattleLoadStallException.cs`,
`BattleLoadRenderWaitProbe.cs`; tests `BattleLoadStallWatchdogTests.cs`, `BattleLoadRenderWaitProbeTests.cs`.

Modified C# (all under `Main/Features/` unless noted):
- `BattleLoadDiagnostics/BattleLoadDiagnosticsIoC.cs`, `BattleLoadDiagnosticsService.cs`,
  `IBattleLoadDiagnosticsService.cs`, `BattleLoadDiagnosticsSettings.cs`,
  `BattleLoadDiagnosticsSettingsProvider.cs`, `IBattleLoadDiagnosticsSettingsProvider.cs`,
  `BattleLoadLoadingWindow.cs`, `Domain/BattleLoadPhase.cs`,
  `Hooks/MissionState_OnTick_RenderWait_Patch.cs` (a Harmony postfix on `MissionState.OnTick`),
  `ExitStallSampler.cs` and `MemoryPressureSampler.cs` (comments only)
- `CoopInterop/CoopSettingsRelevance.cs`
- `MissionPerf/TickProfileLines.cs` (`DiagTokens` lost two parameters), `MissionPerf/Hooks/PerfContextReader.cs`
- `ShaderPrecompilation/ShaderPrecompileRunner.cs` (the walk used to set a static suppress flag on the watchdog)
- `CrashReport/Hooks/AppDomainExceptionHook.cs` (comment only), `Main/SubModule.cs` (one line)
- Tests: `TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsServiceTests.cs`,
  `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `TAOM.Tests/Features/MissionPerf/TickProfileLinesTests.cs`
- `tools/triage_battle_load.py` (comment only)

Three MCM settings were removed: `EnableStallWatchdog`, `EnableStallWatchdogBundle`, `StallWatchdogSeconds`
(class `BattleLoadDiagnosticsSettings`, a MCM json2 `AttributeGlobalSettings`). `IBattleLoadDiagnosticsService.CurrentStatusLine`
was removed because only the watchdog read it. The `[PerfContext] diag=` list lost the `stallWatchdog` and
`stallBundle` tokens; `tools/perf_runs.py` and the Python triage tool were left unchanged on purpose so old logs parse.

## What to look for (known suspects, verify each; do not assume)

1. Anything that still references a removed type, member, setting key or log token, in C#, XML, JSON,
   Python, PowerShell or docs, that the build would not catch (string-bound reflection, MCM setting ids,
   server settings JSON, log parsers, in-game check manifests under `tools/ingame_checks/`).
2. MCM json2 persistence: a player's saved settings file still holds the three removed keys. Does loading
   tolerate unknown keys, and does the dedicated-server settings path (`ServerSettings.ServerSettingsSource`)
   tolerate them? Cite the code that reads them.
3. Co-op: `CoopSettingsRelevance` lists settings that do not change simulation. The three removed settings were
   in that list. Does removing them change the settings fingerprint peers compare, and is that safe? The test
   `SettingsFingerprintTests` pins a total per settings class.
4. The postfix in `MissionState_OnTick_RenderWait_Patch.cs`: it still samples
   `Utilities.GetNumberOfShaderCompilationsInProgress()` per frame while the loading window is open and calls
   `_service?.NoteWaitingForRender(shaders)`. Is the guard order, the exception handling and the thread of the
   call unchanged in behaviour? A diagnostic must never break a mission tick.
5. Lifecycle: `BattleLoadLoadingWindow.Enter()` no longer resets a probe. Anything that read stale state?
   Is the loading window still opened and closed on every path as before?
6. `ShaderPrecompileRunner`: the parked precompile walk no longer sets or clears a watchdog flag. Any path that
   needed the flag for another reason?
7. DI: the `BattleLoadStallWatchdog` registration and `Start()` call are gone; `MissionTickStallWatchdog` is
   still registered and started. Any other consumer resolving the removed type?
8. Dead code left behind by the removal (unused members, constants, usings, settings-provider members, test helpers).
9. Docs that still claim the watchdog exists (`docs/features/battle-load-diagnostics.md`,
   `docs/features/mission-perf-heartbeat.md`, `docs/reference/harmony-patch-registry.md`, `docs/features/coop-interop.md`).

## Output format

List findings, most severe first. For each: severity (P1 breaks behaviour or ships a defect, P2 likely wrong,
P3 minor), `file:line`, the exact evidence (quote the line), and the failure scenario. Say `NO FINDING` for a
suspect you checked and cleared, with the one line that clears it. Mark a claim you could not verify
`UNVERIFIED`. Do not pad. No em dashes.
