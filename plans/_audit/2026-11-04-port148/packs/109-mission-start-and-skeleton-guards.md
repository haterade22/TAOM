# Input pack: WP 109 mission-start-and-skeleton-guards

Title (draft): Survive mission-start throws (no endless reload) and guard the engine's per-frame skeleton buffers
Wave: 1   Lane: A   Draft priority: P0   Groups: 3   Port commits: 5
Depends on (WP ids): 106, 107

## Orchestrator notes for this WP (binding)
- Port only the MissionStartGuard and SkeletonBuffer parts of ecaa67d26 here; NameplateCull and MapViewRelease go to WP120.
- Decision D5: guard AND watch, both on by default; v1.4.8 KnownBuilds row from V03: [14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0); on any mismatch the guard self-disables and the watch reports. Native pin gate 15e52d7af + c286e2393 at tip with the v1.4.8 row (and fdee18f02's NativePinGateTests summary).
- Bring the pinned helpers verbatim from origin/bannerlord-1.5.x (unchanged since ecaa67d26): MissionPerf/TickProfilerTranspiler.cs (trimmed TickProfileLines), MissionPerf/AnimMemory/ClipBudgetSignature.cs and PeSectionTable.cs; V03 lists further hidden prereqs (GameAssemblies 3-arg ResolveGameDir, RecordingLogger, SyntheticNativeImage, PatchShieldPolicy exclusion API, MIT text for THIRD-PARTY-LICENSES.txt).
- V03: without plan 008's binding-gate runsettings (WP122) an unlisted engine build reads Skipped, not Failed; note it.
- Provenance: yotthani VanillaTuning is MIT; keep the notice and the provenance-register row (.claude/rules/provenance.md).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V03-skeleton-buffer-v148.md
- plans/_audit/2026-11-04-port148/verify/V04-tick-handshake-and-start-swallow.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- LOADING-HANG-03 [P1/high] Any throw inside Mission.AfterStart becomes an infinite loading screen because TAOM's Native2Managed finalizer swallows it and MissionState reloads every frame | port: ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)
- LOADING-HANG-06 [P1/high] Unchecked per-frame skeleton reservation that freezes crowded battles for good is byte-identical on v1.4.8 | port: ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)
- FREEZE-THREADING-04 [P1/high] A throw anywhere in mission start makes the mission reload every frame (infinite loading screen) | port: ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)
- FREEZE-THREADING-07 [P1/medium] Per-frame skeleton buffers have no bounds check; identical code in v1.4.8 freezes a crowded battle for good with audio playing | port: ecaa67d26,c17541f2c  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)
- RUNTIME-PERF-15 [P2/low] Freeze-lens lead: unchecked per-frame skeleton pools in the native draw path freeze battles for good (1.5.x guard is native and engine-specific) | port: ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)

## Port groups owned by this WP (from the commit classification)

### group `mission-start-guard`  P2  track=diagnostics  commits=1  sim: clean=0 conflict=1
symptoms: loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/CrashReport/CrashReportService.cs, Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs
- `649cc30ff` 2026-10-01 PORT-PARTIAL P2 pick=conflict :: feat(crash-report): add diagnostic triggers for exception handling and mission start throws
  - why: Adds a QA trigger that throws from a mission behavior's AfterStart, plus temporary #699 tracing of exceptions that TAOM's crash capture drops. Both help diagnose silent mission-load loops on 1.4.x.
  - port notes: Hand-apply: the 1.4.x CrashReportPatchHelper.HandleAndSwallow has no offMainThread parameter and no HandBack, so add the Diag699Entry call and helper by hand. The CrashReportService re-entry trace, the compose checkpoints, the CrashReportSettings toggle and CrashReportDevTriggerMissionBehavior.AfterStart apply as written. CrashBundleThrottle.IsLoggedOccurrence comes from fb52e619d (absent on 1.4.x); port that or inline the powers-of-ten check. Update CoopSettingsRelevance and SettingsFingerprintTests. The diag699 code is marked temporary but is still on the 1.5.x tip, so weigh it alongside the later MissionStartGuard work (ecaa67d26). E3 does not show this loop: it logs one MissionAfterStartBegin/Done pair.
  - prereqs: fb52e619d
  - conflicts: Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/CrashReport/CrashReportService.cs, Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs

### group `yotthani-stability-guards`  P0  track=freeze-threading  commits=2  sim: clean=0 conflict=2
symptoms: battle-freeze, loading-hang, memory
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/CrashReport/CrashReportSettings.cs, Main/SubModule.cs, Main/_Module/THIRD-PARTY-LICENSES.txt, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs, TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs, TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
- `ecaa67d26` 2026-10-08 PORT-PARTIAL P0 pick=conflict :: feat(stability): v2.0.34 - mission start, freeze and map guards
  - why: Adds four engine guards that all apply to v1.4.8: a Mission.AfterStart guard ending the forever-reload of a mission whose start call throws, a native skeleton-buffer overflow guard and watch for the silent permanent battle freeze, a settlement nameplate cull, and a map-view GPU memory release.
  - port notes: Verified this session. (1) v1.4.8 has the reload loop: MissionState.cs:89-95 ticks loading while Initializing, :221-233 reloads when _missionInitializing is false, FinishMissionLoading clears it (:335) before AfterStart (:345); the six wrapped calls sit once each in Mission.AfterStart (Mission.cs:3807-3829). (2) A read-only scan of the installed v1.4.8 TaleWorlds.Native.dll found every SkeletonBufferSignature pattern exactly once and no foreign-guard pattern: pool-1 site RVA 0x886B4 (0x33 after the watch site), pool-2 site RVA 0x898F0, the pool-2 call 0x29 after its load landing 0x20 before the site, both loads naming one aligned .data global (RVA 0xD96D18), the same shape the doc gives for v1.5.4. So the guard should install unchanged; re-pin SkeletonBufferInstalledBinaryTests and doc RVAs. (3) SettlementNameplatesVM.Update and fields _mapCamera, _bindIsVisibleOnMap, _worldPos exist in the v1.4.8 dump; ScreenLayer.OnLayerActiveStateChanged and SceneLayer.ClearRuntimeGPUMemory exist. Replace or port: the Main/Composition feature-module runner (9ed3ee308, plan 018; or wire the four modules in SubModule/IoC), TickProfilerTranspiler.Rewrite (6eb2e1b90) used by the AfterStart transpiler, ClipBudgetSignature (a9f10688f) used by the scan, RepoPaths for tests. Also take the BattleLoadPhaseBehavior closer moved into a finally around TAOM's mission wiring: on 1.4.x it is registered at the end of OnMissionBehaviorInitialize with no try (origin/bannerlord-1.4.5:Main/SubModule.cs:1757-1889), so any TAOM wiring throw both loops the load and leaves the loading window open. Take MCM settings, the two localized strings, the VanillaTuning notice, feature docs, engine doc section 10, registry rows (Patch103/104 are free on 1.4.x, highest there is 88). Splits into packages: MissionStartGuard plus the finally and the SkeletonBuffer guard are P0; NameplateCull and MapViewRelease P1/P2. Leave the adoption review records, codex prompts and plans/_audit.
  - prereqs: 9ed3ee308, 6eb2e1b90, a9f10688f, a0db0558b
  - conflicts: Main/Composition/FeatureModules.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/CrashReport/CrashReportSettings.cs, Main/SubModule.cs, Main/_Module/THIRD-PARTY-LICENSES.txt, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs, TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs, TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
  - issues: 775, 776, 777, 778, 779, 780, 782, 786
- `83ad1eab5` 2026-10-08 PORT P3 pick=conflict :: docs(ai): v2.0.34 - pair Harmony prefix and finalizer via __state
  - why: One-line Harmony rule that a prefix and finalizer pair must link through __state, never a static flag, because Harmony reruns finalizers and PatchShield strips prefixes but not finalizers; it holds for every 1.4.x patch.
  - port notes: The 1.4.x harmony-patches.md lacks the Finalizer bullet this line sits under (added on 1.5.x by f75288ebf, the #634 off-thread fix) and lessons/harmony-il.md lacks the cited 'bracket must survive its prefix being stripped' lesson (added by daba6f0a0). Land after those, or carry the bullet and the lesson with it.
  - prereqs: f75288ebf, daba6f0a0

### group `issue-806-native-pin-gate`  P2  track=tests-ci  commits=2  sim: clean=0 conflict=2
symptoms: battle-freeze
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs, TAOM.Tests/Migration/NativePinGateTests.cs
- `15e52d7af` 2026-10-15 PORT-PARTIAL P2 pick=conflict :: test(skeleton-buffer): v2.0.34 - pin v1.5.5 targets, gate native pins
  - why: Keeps the SkeletonBuffer freeze guard's native pin tables failing, not skipping, on an unlisted engine build; worth taking with the SkeletonBuffer port, whose patterns this session's read-only scan resolved on the installed v1.4.8 TaleWorlds.Native.dll.
  - port notes: Take only with the SkeletonBuffer port (ecaa67d26). Take the BindingVerification class tag, NativePinGateTests in its final shape (563, 566) and a new engine-bump SKILL.md paragraph naming the C# KnownBuilds tables (the 1.4.x skill has no native-pin paragraph; drop the Python PINS lines unless native_decompile and its tests are ported). Leave the v1.5.5 row. v1.4.8 row derived this session from the installed DLL (14,185,944 bytes, build 119303), meeting every assertion of InstalledNativeDll_Patterns_EachMatchOnceAndNeverInTheForeignShape: [14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0); global in .data and 8-aligned, guard 0x33 after the watch site (same spacing as v1.5.5), pool-2 call E8 at 0x8849E enters 0x20 before the pool-2 site, both foreign shapes 0 matches. The ClipBudget class needs the AnimMemory port; its pattern matches once on v1.4.8 (load site 0x23AADF; other row fields not derived). NativePinGateTests reads the skill through TAOM.Tests/Infrastructure/RepoPaths.cs, absent on 1.4.x (first added on 1.5.x in 35007dbb8; take that file only).
  - prereqs: ecaa67d26, 35007dbb8
  - conflicts: TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs
  - issues: 806
- `c286e2393` 2026-10-15 PORT-PARTIAL P2 pick=conflict :: test(native-pins): v2.0.34 - pin gate reads compiled attributes
  - why: Makes the native-pin gate find KnownBuilds tables by reflection and read compiled TestCategory attributes instead of source text; take it with 557 if SkeletonBuffer is ported.
  - port notes: Take TAOM.Tests/Migration/NativePinGateTests.cs; the skeleton-buffer-guard.md changelog and issue lines go only with the SkeletonBuffer feature doc.
  - prereqs: 15e52d7af
  - conflicts: TAOM.Tests/Migration/NativePinGateTests.cs
  - issues: 806

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 12, batch 15, batch 18
