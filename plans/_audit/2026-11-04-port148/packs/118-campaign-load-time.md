# Input pack: WP 118 campaign-load-time

Title (draft): Cut campaign load time: load stamps, the one-document XML merge (plan 042), ButterLib's distance matrix off
Wave: 2   Lane: B   Draft priority: P1   Groups: 9   Port commits: 13
Depends on (WP ids): 104, 107

## Orchestrator notes for this WP (binding)
- V08: the v1.4.8 merge is quadratic exactly as plan 042 says (MBObjectManager.cs:966-982, :997-1010, :833-845). Port a27c6b338 only together with 346f3e093 and: the PatchShield method-level exclusion (mechanism from WP107), a TAOM-side guard for a mismatched TAOM.Dependencies.dll, the IlCallScanner.Fingerprint hunk, and a live-harness run on the v1.4.8 install. Plan 009 and 018 are NOT prerequisites of the merge itself.
- Decision D14: DROP LOAD-TIME-07 (one generated NPCCharacters file): after plan 042 it saves under 0.5 s and the 1.5.x maintainer decided against a build-step merge.
- 502f7cde9 and plan 012 (8028bf3df, e6c03411d, bce3c27f4, 15e5475a4, a4492fa89) together.
- ab78564e0's two IL-fingerprint tests (owned by WP112's group) land here.
- 6027ecfae (BUTR stack refresh) and every Dependencies change: paired release (#371); MCM and crash-report smoke test.
- Before figures (V08): E1 window 27.2 s, NPCCharacters 15.10 s, TAOM's 42 files 11.80 s; E2 later loads 22.6 to 29.7 s; 12 loads in E2 spent 292.9 s in merge windows.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V08-load-time.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- LOAD-TIME-01 [P1/high] The engine's quadratic module-XML merge costs 23 to 37 s of every campaign load and is rebuilt identically each time | port: a27c6b338,346f3e093,43ca92ee9,plan 042  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-05 [P2/high] No 1.4.x log line attributes TAOM's share of a load (no per-type XML, patch-group or handler timing; one-second log resolution) | port: 346f3e093,43ca92ee9,plan 040  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-07 [P1/high] TAOM's 43 NPCCharacters files cost 10.7 to 11.9 s of every load; one generated file would remove most of it without an engine patch | port:   (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-08 [P3/high] Launch to main menu is about 50 s, and TAOM's managed share of it is a few seconds at most | port: d31026b5b  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-09 [P2/medium] The last 9 to 17 s of every load (game init to map, about +2 GB) is unattributed, and the 1.4.x line lacks the map-load timeline that solved this blind spot on 1.5.0 | port: 502f7cde9,8028bf3df,e6c03411d,bce3c27f4,15e5475a4,a4492fa89,6f7ddd394  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- LOAD-TIME-10 [P3/high] lords.xslt costs 1 to 2 s of every load; the 1.5.x dedent barely affects load, the XSLT cache does | port: 5e45d2782,9165a6727  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)
- RELEASE-INTEGRITY-10 [P3/medium] Bundled BUTR stack runs ButterLib/MBOptionScreen builds made for game 1.4.5 on 1.4.8; 1.5.x refreshed it with 1.4.6-1.4.8 builds | port: 6027ecfae  (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- RUNTIME-PERF-11 [P2/medium] ButterLib Distance Matrix rebuilds on every siege end and load; every settlement nameplate updates every map frame (both verified on the v1.4.8 stack) | port: 1325c36ea,ecaa67d26  (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)
- SAVE-TIME-09 [P3/medium] TAOM ships ButterLib with its Save System subsystem on; it replaces TypeExtensions.IsContainer on every save | port: 1325c36ea  (report: plans/_audit/2026-11-04-port148/deepdive/save-time.md)

## Port groups owned by this WP (from the commit classification)

### group `map-load-diagnostics`  P2  track=diagnostics  commits=1  sim: clean=0 conflict=1
symptoms: loading-hang, slow-load
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/SubModule.cs
- `502f7cde9` 2026-09-14 PORT P2 pick=conflict :: feat(diagnostics): v2.0.28 - map-load timeline, renumbered Patch89
  - why: Patch89 campaign-map load timeline (5 s heartbeat from Campaign.RealTick plus a game-state, loading-window and scene-ready lifecycle trace) is diagnostics infrastructure; 1.4.x has battle-load diagnostics but nothing for the map load.
  - port notes: Targets checked on v1.4.8 (_categories_v1.4.8): internal Campaign.RealTick (Campaign.cs:885), GameStateManager PushState/PopState/CleanAndPushState/CleanStates (:221-250), LoadingWindow Enable/DisableGlobalLoadingWindow, SceneView.CheckSceneReadyToRender/ReadyToRender, MapScreen (and HandleIfBlockerStatesDisabled) in _modules_build SandBox.View. Patch89 is free on 1.4.x (highest category Patch88). Conflicts: Main/SubModule.cs and Main/IoC.cs registration. Bring the plan-012 follow-ups with it (seen in git log of Main/Features/MapLoadDiagnostics): 8028bf3df, e6c03411d, 15e5475a4, a4492fa89; leave 6d7e67f3f (co-op server split). The doc evidence added later by 70517552c is 1.5.x-specific.
  - conflicts: Main/SubModule.cs

### group `deps-butr-refresh`  P2  track=release-packaging  commits=1  sim: clean=0 conflict=1
- `6027ecfae` 2026-09-14 PORT-PARTIAL P2 pick=conflict :: fix(deps): v2.0.28 - BUTR stack refreshed for the 1.5.x line
  - why: origin/bannerlord-1.4.5 still vendors ButterLib and MBOptionScreen engine builds only up to 1.4.5, so v1.4.8 runs the 1.4.5 implementations; this refresh adds the 1.4.6, 1.4.7 and 1.4.8 builds and bumps MCM and UIExtenderEx.
  - port notes: Take ButterLib 2.12.0 core + Implementation.1.4.6/1.4.7/1.4.8, MBOptionScreen 5.12.3 v1.4.6-v1.4.8 + MCM.UI.Adapter.MCMv5, Bannerlord.MCM 5.12.3 and Bannerlord.UIExtenderEx 2.13.3 in Dependencies/TAOM.Dependencies.csproj and Main/TAOM.csproj (1.4.x pins 5.12.1 / 2.13.2), the ButterLib stub version, THIRD-PARTY-LICENSES and the dr3-maintenance record. The 1.5.1 builds are optional dead weight on 1.4.x. UIExtenderEx is repacked into TAOM.Dependencies.dll, so the #371 pairing (Dependencies/_Module/SubModule.xml v2.0.6 plus Main's DependedModuleMetadata pin) must move together at the next 1.4.x release. Benefit UNVERIFIED (no ButterLib error lines found in E2's rgl log); owes an in-game MCM and crash-reporter smoke on v1.4.8 plus BundledDependencyManifestTests.

### group `plan-012-loading-window-trace`  P2  track=diagnostics  commits=5  sim: clean=1 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs, Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs
- `8028bf3df` 2026-09-23 PORT P2 pick=clean :: perf(map-load): v2.0.30 - add the loading-window real-lower gate
  - why: Plan 012 step 1: a pure before/after gate that decides whether DisableGlobalLoadingWindow really lowered the window, needed by the per-frame log fix.
  - port notes: Meaningful only if MapLoadDiagnostics (502f7cde9, committed on 1.5.x 2026-09-14 after the fork despite its v2.0.28 label, absent on origin/bannerlord-1.4.5) is ported. Port it together with that commit and the rest of plan 012 (e6c03411d, 15e5475a4, a4492fa89). Clean add of two new files; Patch89 is unused on 1.4.x (git grep of Patch8x categories on the tip).
  - prereqs: 502f7cde9
- `e6c03411d` 2026-09-23 PORT P2 pick=conflict :: perf(map-load): v2.0.30 - log a loading-window lower only when it drops
  - why: Without it the MapLoadDiagnostics Disable postfix walks the stack and flushes a log line every frame of the main menu and many screens (84 MB per 35 min and 1.16 GB per 3 h measured on 1.5.x).
  - port notes: Mandatory companion to any 1.4.x port of 502f7cde9; consider squashing 502f7cde9 with plan 012 so the per-frame postfix never ships. Engine premise verified on v1.4.8: LoadingWindow.cs:41 sets IsLoadingWindowActive=false regardless of its prior value (inside `if (LoadingWindowManager != null)`), MBInitialScreenBase.cs:80 calls it from the initial screen's frame tick, and _modules_build/SandBox__SandBox.GauntletUI.cs has 13 call sites, at least ten inside OnFrameTick overrides (e.g. :370, :431, :616). Conflicts only because the patch file does not exist on 1.4.x.
  - prereqs: 502f7cde9, 8028bf3df
  - conflicts: Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs
- `bce3c27f4` 2026-09-23 PORT P3 pick=conflict :: docs(map-load): v2.0.30 - loading-window lowers trace on real drops
  - why: Feature doc and Harmony registry text describing the real-lower trace; ships with the plan 012 code.
  - port notes: docs/features/map-load-diagnostics.md and the Patch89 registry rows arrive with 502f7cde9. The cost sentences cite v2.0.29 and v2.0.30, which are 1.5.x labels; restate them for 1.4.x.
  - prereqs: 502f7cde9, e6c03411d
- `15e5475a4` 2026-09-24 PORT-PARTIAL P2 pick=conflict :: fix(map-load): v2.0.30 - review follow-ups for plan 012
  - why: Plan 012 review follow-ups: a Prefix-to-Postfix round-trip test, a stricter lowered-trace test, and corrected patch summary and docs.
  - port notes: Take the patch, gate and both test files plus the feature-doc, feature-map and registry text. The deep-review report, RCA and Codex prompt are 1.5.x review records (optional); the lessons hunks (harmony-il, testing-qa, build-tooling-workflow) are generic and can ride along. The screen list in the patch summary was written against 1.5.x: keep it non-exhaustive, as a4492fa89 does.
  - prereqs: e6c03411d
  - conflicts: Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs, Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs
- `a4492fa89` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(map-load): v2.0.30 - convergence fixes for plan 012
  - why: Plan 012 convergence fixes: comment and doc corrections (a non-exhaustive screen list naming the barber and face generator, and a correct helper-hop explanation).
  - port notes: Take the patch summary comment, the test comment and the feature-doc text; skip the review-record and CHANGELOG hunks. Squash into the 15e5475a4 port.
  - prereqs: 15e5475a4
  - conflicts: Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs
skipped in this group: `6f7ddd394` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - plan 012 loading-window trace entry

### group `plan-040-load-time-stamps`  P1  track=diagnostics  commits=1  sim: clean=0 conflict=1
symptoms: slow-load
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Dependencies/Foundation/PatchShieldPolicy.cs, Main/Composition/FeatureModules.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/PatchCategoryApplier.cs, Main/SubModule.cs, TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
- `346f3e093` 2026-10-04 PORT-PARTIAL P1 pick=conflict :: feat(load-stamps): v2.0.32 - time module XML, patch groups and handlers
  - why: Adds always-on load-time stamps ([LoadXml] per XML type, [PatchApply] per patch group, [Lifecycle] per campaign dispatch) plus the PatchShield exclusions that the XML merge fast path (a27c6b338) must ship with.
  - port notes: Take Main/Features/LoadTimeStamps/**, CampaignListenerAdapter, IStampClock/StopwatchStampClock, the settings entry, tests and docs. Also take the seven PatchShield exclusions (MBObjectManager.LoadXML, CreateMergedXmlFile and five CampaignEventDispatcher lifecycle methods); they need the ExcludedTargetMethods mechanism from 30abf550b. The per-group [PatchApply] timing lives in PatchCategoryApplier (45bcf80bd, plan 009), and the module registers through Main/Composition (9ed3ee308, plan 018); port those or adapt to 1.4.x's SubModule patch loop and IoC. CampaignListenerAdapter reflects into CampaignEventDispatcher, so verify its fields on v1.4.8 with taom-src. Priority is P1 because a27c6b338 must not ship without these exclusions.
  - prereqs: 45bcf80bd, 9ed3ee308, 30abf550b
  - conflicts: Dependencies/Foundation/PatchShieldPolicy.cs, Main/Composition/FeatureModules.cs, Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs, Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsSettingsProvider.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/PatchCategoryApplier.cs, Main/SubModule.cs, TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettingsProviderTests.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
  - issues: 722

### group `plan-042-xml-merge-fast-path`  P1  track=perf-load-time  commits=1  sim: clean=0 conflict=1
symptoms: slow-load
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, TAOM.Tests/Migration/BindingGateRunSettingsTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, TAOM.Tests/binding-gate.runsettings
- `a27c6b338` 2026-10-04 PORT-PARTIAL P1 pick=conflict :: perf(xml-merge): v2.0.32 - merge module XML in one document per load
  - why: Replaces the engine's quadratic module-XML merge, which re-converts the whole merged document for every file and compiles every XSLT on every call, with a fast path that keeps one document and compiles each XSLT once; the merge took about 28 s of a 50 s new-campaign load on the maintainer's desktop.
  - port notes: The v1.4.8 dump has the same quadratic path: MBObjectManager.CreateMergedXmlFile (MBObjectManager.cs:966) loops over ApplyXslt (:984, a new XslCompiledTransform per call) and MergeTwoXmls (:997, ToXDocument/ToXmlDocument per file). Take Main/Features/XmlMerge/**, IXmlMergeEngineAdapter/XmlMergeEngineAdapter, the tests and docs, and the engine-bump SKILL.md note. Register through IoC/SubModule instead of Main/Composition (9ed3ee308). It must ship with 346f3e093's PatchShield exclusion for CreateMergedXmlFile. Re-pin the IL fingerprint test to v1.4.8, and before shipping run the opt-in equivalence harness (TAOM_RUN_BENCHMARKS=1) against the 1.4.x module set (TAOM, TAOM_Map, LOTRLOME_Armory and vanilla). Take the binding-gate.runsettings edits only if 1.4.x has that gate. The XSLT cache costs about 11 MB of managed memory.
  - prereqs: 346f3e093
  - conflicts: Main/Composition/FeatureModules.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, TAOM.Tests/Migration/BindingGateRunSettingsTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, TAOM.Tests/binding-gate.runsettings
  - issues: 724

### group `issue-725-earlylog-flush`  P2  track=diagnostics  commits=1  sim: clean=0 conflict=1
- `d31026b5b` 2026-10-04 PORT P2 pick=conflict :: fix(logging): v2.0.32 - flush TAOM.Dependencies startup lines
  - why: TAOM.Dependencies buffers its startup lines in EarlyLog until TAOM hands it a logger, and nothing ever did, so every [TAOM.Dependencies] line was dropped; TAOM now drains the buffer in OnSubModuleLoad.
  - port notes: The same defect is on origin/bannerlord-1.4.5: Dependencies/EarlyLog.cs is identical to the 1.5.x parent, and no Main code calls EarlyLog.DrainTo. Take the EarlyLog change, Main/Core/Logging/EarlyLogBridge.cs, the four SubModule.cs lines (single-owner file), EarlyLogBridgeTests and the dr3-maintenance note. It spans both modules, so a paired Dependencies release goes with it.
  - issues: 725

### group `issue-740-butterlib-distance-matrix`  P1  track=perf-campaign  commits=1  sim: clean=0 conflict=1
symptoms: memory, slow-load
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs
- `1325c36ea` 2026-10-06 PORT-PARTIAL P1 pick=conflict :: perf(campaign): v2.0.34 - switch off ButterLib's Distance Matrix
  - why: Switches off ButterLib's Distance Matrix, which nothing installed reads but which rebuilds clan and kingdom tables on every settlement owner change (about 170 ms, up to 281 ms on v1.5.3), builds a 355,000-pair settlement table at each load and holds it all session; the 1.4.x TAOM.Dependencies ships the same subsystem.
  - port notes: origin/bannerlord-1.4.5 ships Bannerlord.ButterLib.dll and Implementation 1.4.0 to 1.4.5 in Dependencies/_Module/bin/Win64_Shipping_Client; the 1.4.5 implementation DLL contains the DistanceMatrixSubSystem, GeopoliticsBehavior, IsEnabled and Disable strings, so the reflection adapter should bind, but confirm the Instance/IsEnabled/Disable() shape with the binding test against that DLL (which implementation v1.4.8 loads is UNVERIFIED). Replace ButterLibDistanceMatrixModule (Main/Composition, plan 018, 1.5.x-only) with IoC.cs registrations and a DistanceMatrixSwitch.Apply() call from SubModule.OnBeforeInitialModuleScreenSetAsRoot, which must run after MCM's first main-menu hook. Re-measure the siege-end stall and the load-time build on v1.4.8.
  - conflicts: Main/Composition/FeatureModules.cs
  - issues: 740

### group `issue-950-lords-xslt-whitespace`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/ModuleData/lords.xslt
- `5e45d2782` 2026-10-20 PORT-PARTIAL P2 pick=conflict :: perf(lords): v2.0.35 - lords.xslt sheds 360 KB of runaway indentation
  - why: The 1.4.x lords.xslt carries the same runaway indentation (max 4,960 leading spaces, 320 lines over 200, 1.49 MB; the fix took the 1.5.x file from 1.50 MB to 1.13 MB), which slows the lord-skill regeneration tool; the change is whitespace only with byte-identical transform output.
  - port notes: Do not cherry-pick: lords.xslt content differs between the lines (21,304 vs 21,321 lines). Re-run the same whitespace normalisation on the 1.4.x file and prove byte-identical output under lxml and the .NET transform, as 5e45d2782 did. A grep of the 1.5.x log finds no matching fix to the generator that grew the indentation, so it can regrow. The game load-time gain is negligible.
  - conflicts: Main/_Module/ModuleData/lords.xslt
  - issues: 950

### group `issue-959-lord-generator-append-only`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/complete_lords_xslt.py, tools/tests/test_complete_lords_xslt.py
- `9165a6727` 2026-10-21 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - lord generator only adds, never rewrites
  - why: The 1.4.x complete_lords_xslt.py equals the fork version, whose --apply rebuild drops hand-authored lord equipment and races; the rewritten tool only appends and embeds no 1.5.x content.
  - port notes: Replace tools/complete_lords_xslt.py with the post-9165a6727 version (428 lines; 1.4.x has 645) and port tools/tests/test_complete_lords_xslt.py, re-running it against the 1.4.x lords.xslt. Do not copy tools/lords_inventory.csv, which was regenerated from 1.5.x data: regenerate it on 1.4.x with --export-csv. The rewrite drops the two intervening 1.5.x edits to the tool (546d1181f wanderer retag, 5e43518df minor-clan block), so neither is needed. Most doc hunks touch 1.5.x-only feature docs: take only the tools/README and lessons lines.
  - conflicts: tools/complete_lords_xslt.py, tools/tests/test_complete_lords_xslt.py
  - issues: 959

## SHARED group, owned by WP 112: take ONLY the hunks or commits your notes name; the owner ports the rest

### group `issue-1064-supply-desertion-port`  P0  track=crash-robustness  commits=1  sim: clean=0 conflict=1
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/SupplyLines/SupplyCaravanService.cs, Main/Features/SupplyLines/SupplyRouteVisualService.cs, TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs
- `ab78564e0` 2026-10-30 PORT-PARTIAL P0 pick=conflict :: fix(campaign): v2.0.36 - port PR #1064 supply and desertion fixes
  - why: 1b00d1acd already brought the PR #1064 fixes to 1.4.x, and this port adds an endpoint-face validity guard before the native path query, mirroring vanilla ComputePath's checks (v1.4.8 MobileParty.cs:3696-3712), plus the MSTest coverage 1.4.x never got.
  - port notes: Take the AddLandPath extraction, with its !fromFace.IsValid() || !toFace.IsValid() early return, into Main/Features/SupplyLines/SupplyCaravanService.cs. ComputeNavPathPoints on the 1.4.x tip already passes the land rules (1b00d1acd), so only the extraction and the guard are new. The 1.5.x RCA (finding 1) names TAOM's teleported caravan position as the likely off-navmesh input, a candidate for the unisolated 1.4.8 crash. Engine fit on v1.4.8: PathFaceRecord.IsValid() exists (TaleWorlds.Library/PathFaceRecord.cs:20), and IMapScene.GetPathBetweenAIFaces(..., regionSwitchCostFromLandToSea, regionSwitchCostFromSeaToLand) at IMapScene.cs:34 matches the 1.5.x argument slots. Take the three NSubstitute AddLandPath tests in TAOM.Tests/Features/SupplyLines/SupplyNativeQueryTests.cs. Its two IL-fingerprint tests need IlCallScanner.Fingerprint, which 1.5.x added in a27c6b338 and 1.4.x's IlCallScanner lacks. The arrows test must be rewritten for 1.4.x's own fallback in Main/Features/SupplyLines/SupplyRouteVisualService.cs (scene.GetHeightAtPoint(Vec2, BodyFlags, ref float)). Optionally adopt the RCA's design D, IMapScene.GetHeightAtPoint(in CampaignVec2, ref float), which exists on v1.4.8 (IMapScene.cs:52). Adapt the map-event desertion tests to 1.4.x's two-argument AlignmentDesertionBehavior constructor; the test file is absent on 1.4.x (1.5.x created it in 423d948d8). Leave Client/: 1b00d1acd already guards the arrows in Main/. Build the 1.4.x tip first, because 1b00d1acd was merged unbuilt.
  - conflicts: Main/Features/SupplyLines/SupplyCaravanService.cs, Main/Features/SupplyLines/SupplyRouteVisualService.cs, TAOM.Tests/Features/AlignmentDesertion/AlignmentDesertionBehaviorTests.cs
  - issues: 1064, 1068, 750

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 4, batch 13, batch 14, batch 21, batch 22, batch 30
