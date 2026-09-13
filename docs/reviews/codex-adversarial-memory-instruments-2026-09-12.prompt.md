You are an adversarial reviewer for TAOM, a Bannerlord v1.4.8 total conversion (C#, .NET Framework 4.7.2). Repo root: E:\repos\TAOM. Review the UNCOMMITTED working tree (`git status --porcelain`, `git diff`), not HEAD. Read AGENTS.md rules first. Report findings with file:line, impact, and a reproduction or proving code. Try to refute each of your own claims before reporting it. Do not edit files.

FEATURE UNDER REVIEW: three memory-attribution instruments built 2026-09-12 after the first live commit-attribution run (docs/investigations/native-commit-audit-2026-08.md, Phase 2 results). Nothing in game content changed. A five-agent deep review already ran; its RCA is docs/reviews/rca-memory-instruments-2026-09-12.md (two findings fixed: a doc claim about OnGameLoaded firing for new campaigns, and a dump-path marker ordering). Do not re-report those two; look for what it missed.

The three instruments:
1. `taom.print_memory` probe extension. Main/Features/BattleLoadDiagnostics/EngineMemoryStatsReader.cs now also reads TaleWorlds.Engine.Utilities.GetVertexBufferChunkSystemMemoryUsage() (int) and Utilities.GetGPUMemoryStats(ref float x5), carried in Domain/EngineMemoryStats.cs and Domain/GpuMemorySplit.cs, rendered by MemoryProbeReportFormatter.cs as raw engine units. It also stops claiming a GPU dump was written unless File.Exists says so.
2. Main/Features/BattleLoadDiagnostics/ProcessMemoryTokens.cs: the `gc=/heapMB=/privMB=/wsMB=` token tail extracted from BattleLoadDiagnosticsService.MemStats() (must be byte-identical) and now also used as the detail of two new [SaveLoad] phases, `GameLoaded` and `GameInitializationFinished` (Main/Features/SaveLoadDiagnostics/Domain/SaveLoadPhase.cs), stamped from `public override void OnGameLoaded(Game, object)` and a line added to the existing `OnGameInitializationFinished(Game)` override in Main/SubModule.cs (see `git diff Main/SubModule.cs`; the file is 1,300+ lines, review only the diff).
3. Main/Features/BattleLoadDiagnostics/ScreenCloseHeapRelease.cs: subscribes to the public static event TaleWorlds.ScreenSystem.ScreenManager.OnPopScreen and, when the popped screen's CLR type name is GauntletInventoryScreen, GauntletPartyScreen or GauntletCharacterDeveloperScreen AND GC.GetTotalMemory(false) >= 1,024 MB, calls TaleWorlds.Library.Common.MemoryCleanupGC() (a blocking GC.Collect()) and writes one `[HeapRelease]` INFO line. Registered as a DryIoc singleton in BattleLoadDiagnosticsIoC.cs, started from SubModule.OnBeforeInitialModuleScreenSetAsRoot right after the sibling MemoryStationSampler.Start(). No MCM toggle by design. Motivation: the live run measured the inventory screen at +2.9 GB per open, almost all managed garbage that stayed committed until a full GC happened to run five minutes later.

TAOM ID CHEATSHEET (not load-bearing for this review, included for completeness):
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa. "rohan" is NOT a valid ID.

READ FIRST:
- docs/features/battle-load-diagnostics.md sections "Screen-transition anchors", "Heap release on heavy-screen close", "What taom.print_memory reports since 2026-09-12"
- docs/features/save-load-diagnostics.md "Log contract" table
- docs/reviews/rca-memory-instruments-2026-09-12.md
- Main/Features/BattleLoadDiagnostics/MemoryStationSampler.cs (the sibling this class copies its guards from)
- Main/Core/Logging/FileLogger.cs (INFO is a synchronous flush by design; do not propose downgrading crash-forensic lines to DEBUG)

VANILLA CODE: verify against the INSTALLED v1.4.8 DLLs via the ilspy MCP server or `pwsh tools/taom-src.ps1 path <Full.Type.Name>`, never E:\Decompiled_Bannerlord for signatures. Paste the relevant bodies as code blocks in your report:
- TaleWorlds.ScreenSystem.ScreenManager: every raise site of OnPopScreen (PopScreen, ReplaceTopScreen, CleanScreens, DeactivateAndFinalizeAllScreens) and what engine state each is in when the event fires
- TaleWorlds.Library.Common.MemoryCleanupGC and every engine caller of it
- TaleWorlds.CampaignSystem.Campaign.OnInitialize (around lines 1380-1460) for the OnGameLoaded / OnGameInitializationFinished call order and GameLoadingType branches
- TaleWorlds.MountAndBlade.MBGameManager and Module for how MBSubModuleBase.OnGameLoaded / OnGameInitializationFinished are dispatched, including for CustomBattle / tutorial Game instances
- TaleWorlds.Engine.Utilities: GetVertexBufferChunkSystemMemoryUsage, GetGPUMemoryStats, DumpGPUMemoryStatistics

KNOWN SUSPECTS (confirm or dispute each with evidence):
S1. A blocking full GC inside the OnPopScreen delegate chain. ReplaceTopScreen raises OnPopScreen for the old screen BEFORE the new screen's HandleInitialize runs; DeactivateAndFinalizeAllScreens raises it once per screen and then calls Common.MemoryCleanupGC() itself. Is there a raise site where a collection is unsafe (engine holding a native handle it expects untouched, a screen mid-transition), or one where TAOM's collection is simply doubled with the engine's own? Is the 1,024 MB threshold enough to keep the CleanScreens / return-to-menu path from paying twice?
S2. Subscription order and lifetime. MemoryStationSampler subscribes first in the same SubModule method, so its `[MemStation] exit` line reads the heap before the release; is that order guaranteed by C# multicast delegate invocation order, and does anything (Dispose then Start of one of the two, a second OnBeforeInitialModuleScreenSetAsRoot firing) reorder them? Neither class is explicitly disposed; both are DryIoc singletons implementing IDisposable. Is a late OnPopScreen after IoC.Dispose() (crash/teardown paths) harmful?
S3. `[SaveLoad]` stamps on non-campaign games. MBSubModuleBase.OnGameInitializationFinished fires for CustomBattle and tutorial Game instances too; the stamp then writes a [SaveLoad] line with no LoadRequested and the previous attempt's seq/clock. Does anything parse [SaveLoad] lines and misbehave (tools/*.py, the crash bundle renderer, the triage tool), or is it noise only? Does StampSaveLoadPhase resolving ISaveLoadDiagnosticsService via IoC ever run before the container is built?
S4. Raw float rendering. MemoryProbeReportFormatter.Raw(float) uses "0.##" with InvariantCulture; a NaN or Infinity from the engine prints "NaN" / the infinity glyph inside a log line. Under the repo's never-fabricate rule, should a non-finite value render as <invalid> instead? Is int overflow possible on GetVertexBufferChunkSystemMemoryUsage (int, unit unknown) and how would a negative value read?
S5. ProcessMemoryTokens.Format() must be byte-identical to the old BattleLoadDiagnosticsService.MemStats() body, including the failure branch. Diff them.
S6. ScreenCloseHeapRelease matches screens by bare CLR type name. Are there OTHER heavy screens in v1.4.8 (encyclopedia is not a ScreenBase; what about GauntletClanScreen, GauntletKingdomScreen, the crafting screen, the tournament/arena screens, GauntletSaveLoadScreen) whose exclusion is a mistake given their view-model size? Do not propose adding screens without evidence of size; the live run measured clan and kingdom at zero retained.

REQUIRED SECTIONS in your report:
1. VANILLA CODE (the bodies above as code blocks)
2. KNOWN SUSPECTS: S1..S6 each CONFIRMED / DISPUTED with file:line evidence
3. LIFECYCLE TRACE: OnBeforeInitialModuleScreenSetAsRoot (first launch, and every return to the main menu) -> Start() -> a campaign -> screen pops -> return to menu -> second campaign in the same process -> quit. Say what each instrument does at each step and whether any state is stale.
4. THREAD SAFETY: which thread raises OnPopScreen; which thread runs StampSaveLoadPhase; whether ISaveLoadDiagnosticsService.LogPhase's thread-safety contract covers them.
5. CONFIG CROSS-REFERENCE: none of this feature has config; confirm that no MCM property was added and say whether the settings fingerprint test (TAOM.Tests, SettingsFingerprintTests) still holds.
6. FINDINGS: P1 (ship-blocking), P2 (should fix), P3 (nit), each with file:line, impact, and proving code or a reproduction; OR OBSERVATIONS if nothing rises to a finding.

QUALITY GATES: every claim about engine behavior cites the installed-DLL source with a line number; every "missing" claim is backed by a grep you ran; a finding you could not reproduce or prove is reported as UNVERIFIED, not as P2.

PRIOR REVIEW LESSONS:
SUCCESSES: vanilla decompilation caught missing gates and wrong call order; lifecycle tracing caught stale caches and process-scoped latches; a Codex pass on the sibling MemoryStationSampler found that [MemStation] deltas exclude screen construction (OnPushScreen fires after HandleInitialize), which reshaped the measurement protocol.
FAILURES: Codex has flagged vanilla-matching code as bugs; assumed a static accessor meant a process-static object (CampaignEvents.Instance is per campaign); rated cost claims HIGH without reading the body; skipped the hard sections. Do not do these.

Write the full report to stdout (the harness captures it to docs/reviews/raw/codex-adversarial-memory-instruments-2026-09-12.md).
