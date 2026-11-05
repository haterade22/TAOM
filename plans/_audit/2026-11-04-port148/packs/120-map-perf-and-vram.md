# Input pack: WP 120 map-perf-and-vram

Title (draft): Cull hidden settlement nameplates, release the map view's render targets, stop the cheat-mode inventory spike, advise on textures
Wave: 2   Lane: D   Draft priority: P-   Groups: 0   Port commits: 0
Depends on (WP ids): 109

## Orchestrator notes for this WP (binding)
- Take the NameplateCull and MapViewRelease parts of ecaa67d26 (group yotthani-stability-guards, owned by WP109).
- Decision D22 (V03): NameplateCull does not port unchanged: v1.4.8 SettlementNameplateVM.RefreshDynamicProperties (SettlementNameplateVM.cs:500-509) takes a hideout's range from ISpottable.IsSpotted where the adapter mirrors v1.5.4's Settlement.IsVisible, and the binding test pins Settlement.get_FerryTarget, absent on v1.4.8.
- MEMORY-01 (V07, Likely): with cheat mode on, opening the player's own inventory lists every ItemObject (about 5,166 rows); one E4 open added 2,352 MB of managed heap and froze the main thread 31 s. Design a guard after /research on InventoryScreenHelper.OpenInventoryPresentation (v1.4.8).
- MEMORY-04: a one-time, dismissible advisor when total RAM is about 16 GB and texture quality is at the top setting (measured: top to minimum freed 2.3 GB on the map; one step down unmeasured: do not promise a number).
- MapViewRelease: measure VRAM before claiming a saving (yotthani measured on 1.5.5).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V03-skeleton-buffer-v148.md
- plans/_audit/2026-11-04-port148/verify/V07-memory.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- MEMORY-01 [P1/medium] With cheat mode on, the I-key inventory lists all 5,166 items ten times: about 2.4 GB managed heap per open on a 16 GB machine | port:   (report: plans/_audit/2026-11-04-port148/deepdive/memory.md)
- MEMORY-04 [P1/high] Texture quality is the largest lever a player has (2.3 GB on the 1.4.8 map), and nothing tells 16 GB players | port:   (report: plans/_audit/2026-11-04-port148/deepdive/memory.md)
- MEMORY-07 [P3/low] Screens opened over the campaign map may strand the map view's render targets until the next mission (VRAM) | port: ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/memory.md)
- RUNTIME-PERF-11 [P2/medium] ButterLib Distance Matrix rebuilds on every siege end and load; every settlement nameplate updates every map frame (both verified on the v1.4.8 stack) | port: 1325c36ea,ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)
- RUNTIME-PERF-14 [P2/low] One E4 inventory open allocated ~2.5 GB managed heap and pushed the machine to 96% memory load (memory/UI lens; cheat-mode hypothesis unverified) | port:   (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)

## Port groups owned by this WP (from the commit classification)

## SHARED group, owned by WP 109: take ONLY the hunks or commits your notes name; the owner ports the rest

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

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 15
