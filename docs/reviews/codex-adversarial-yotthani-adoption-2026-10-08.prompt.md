TAOM CODEX ADVERSARIAL REVIEW: yotthani adoption, items 1 to 4 (2026-10-08)

You review the uncommitted work in the git worktree E:\repos\taom-yotthani (branch feat/yotthani-20261008, base commit d6b537092). List it with: git -C E:/repos/taom-yotthani status --short. Four features were adapted from yotthani's VanillaTuning (MIT) for TAOM, a Lord of the Rings total conversion for Mount and Blade II Bannerlord v1.5.4, C# on .NET Framework 4.7.2:

1. MissionStartGuard (Patch103): one Harmony transpiler on Mission.AfterStart swaps its six start calls (submodule OnBeforeMissionBehaviorInitialize and OnMissionBehaviorInitialize, behaviour OnBehaviorInitialize, EarlyStart and AfterStart, mission object AfterMissionStart) for static helpers that wrap the call in try plus an exception filter, so one throw no longer makes the engine load the mission again every frame for ever. A void prefix is a canary; a void finalizer (Priority.First) logs escapes.
2. SkeletonBuffer: TAOM's only runtime write to engine code. At the first main menu it scans the in-memory .text of TaleWorlds.Native.dll for two 13-byte reservation sites (v1.5.4: pool 1 at RVA 0x69D14, pool 2 at 0x6AF50), allocates two pages below the module (code page and data page), writes a 43-byte block (pool 1, yotthani's) and a 42-byte block (pool 2, TAOM's own) that refuse a reservation past the pool's end, makes the code page execute-read, then writes a jump at each site (compare, write, verify, restore on failure). A mission behaviour reads both pools' fill with ReadProcessMemory and logs peaks.
3. NameplateCull (Patch104): a bool prefix on SettlementNameplatesVM.Update that runs vanilla's update over only the plates that are not hidden-and-staying-hidden, and falls back to the whole vanilla method on the toggle, an unbound member or any error.
4. MapViewRelease: a handler on the engine event ScreenLayer.OnLayerActiveStateChanged counts covers of the campaign map's own scene layer and on every Nth return of that layer calls SceneLayer.ClearRuntimeGPUMemory(false).

Two Claude review rounds (deep-review lenses, a re-review of the fixes, a final-state review with a completeness critic) already ran and their fixes are in the tree. Their record is docs/reviews/rca-yotthani-adoption-2026-10-08.md: do not re-report a settled item unless the fix itself is wrong, and say so when it is.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- AGENTS.md and .ai/review-reference.md (rules, ADRs, severity and evidence format).
- docs/features/mission-start-guard.md, docs/features/skeleton-buffer-guard.md, docs/features/nameplate-cull.md, docs/features/map-view-release.md
- docs/reference/engine/mission-frame-threads-and-native-costs.md section 10 (the native layout, the four callers of pool 2, the inventory of eleven unbounded per-frame pools)
- docs/reviews/rca-yotthani-adoption-2026-10-08.md and docs/reviews/adopt-yotthani-2026-10-08.md

ENGINE SOURCES:
- Managed v1.5.4: C:\Users\mikew\.taom-src\v1.5.4\ (decompiled from the installed DLLs). For a type not there: pwsh tools/taom-src.ps1 path <Full.Type.Name>
- Native: E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.Native.dll (14,209,888 bytes). Use Python with pefile and capstone; python tools/native_decompile.py --rva 0x<rva> prints Ghidra C for a function.
- Harmony: 0Harmony 2.4.2.0 in the TAOM.Dependencies module bin.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence):
S1. Pool 2's 42-byte block (SkeletonBufferCave.BuildPool2Cave) must leave every register, flag and stack slot that FUN_18006af30 reads after the resume point 0x6AF5D exactly as the original 13 bytes would, return start index 0 on a refusal, and leave the fill counter unchanged on a refusal. Decode the golden bytes in SkeletonBufferCaveTests and compare with the disassembly of 0x6AF30 to 0x6B020.
S2. Install safety: is there any order of events in SkeletonBufferGuardService.InstallGuards and SkeletonBufferMemoryAdapter.PatchCode where a site jump can execute into a block that is not yet written or not yet executable, or where the pages are freed while a site may still jump into them?
S3. Refusal semantics under concurrency: the block adds, compares, and on overflow subtracts. Can a refusal leave the counter permanently wrong, or let a later reservation start past the end (for example a buffer reset between the add and the subtract)? Is the doc's stated price (slots near the end given twice, a fitting reservation refused) complete?
S4. MissionStartGuard: with the toggle on, can a survived throw leave state worse than the reload loop it replaces? Check TAOM's own SubModule.OnMissionBehaviorInitialize (the BattleLoad loading-window closer is now registered first) and any other TAOM code that runs inside the six wrapped calls.
S5. NameplateCull: can a skipped plate need an update this frame (tracked settlement, fading in, tutorial target, camera jump, owner or state change, a plate created this frame)? Compare NameplateCullRule.MaySkip and NameplateCullAdapter with vanilla SettlementNameplatesVM.Update, SettlementNameplateVM.UpdateNameplateMT, RefreshPosition, RefreshBindValues and RefreshDynamicProperties.
S6. MapViewRelease: the release now runs when the map's scene layer is activated again (it used to run on the cover, which raised the global loading window over the covering screen). Check the new timing against ScreenBase.HandleActivate and HandleResume, MapScreen.OnActivate, OnResume and HandleIfSceneIsReady, and native SceneView.ClearAll (it zeroes the view's ready word at +0x8A0). Can the map now be left behind a loading window, can a pending release fire on the wrong layer or after the campaign ended, and does a map conversation (SetSuspendLayer) count as a cover?

FILES (git status of the worktree):
Main code (57):
- Main/Adapters/IMapViewReleaseAdapter.cs
- Main/Adapters/IMissionStartGuardAdapter.cs
- Main/Adapters/INameplateCullAdapter.cs
- Main/Adapters/ISkeletonBufferEngineAdapter.cs
- Main/Adapters/ISkeletonBufferMemoryAdapter.cs
- Main/Adapters/MapViewReleaseAdapter.cs
- Main/Adapters/MissionStartGuardAdapter.cs
- Main/Adapters/NameplateCullAdapter.cs
- Main/Adapters/SkeletonBufferEngineAdapter.cs
- Main/Adapters/SkeletonBufferMemoryAdapter.cs
- Main/Composition/FeatureModules.cs
- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsSettings.cs
- Main/Features/CoopInterop/CoopSettingsRelevance.cs
- Main/Features/CrashReport/CrashReportSettings.cs
- Main/Features/MapViewRelease/Hooks/MapViewReleaseLayerEvents.cs
- Main/Features/MapViewRelease/IMapViewReleaseService.cs
- Main/Features/MapViewRelease/IMapViewReleaseSettingsProvider.cs
- Main/Features/MapViewRelease/MapViewReleaseCalls.cs
- Main/Features/MapViewRelease/MapViewReleaseCounter.cs
- Main/Features/MapViewRelease/MapViewReleaseLines.cs
- Main/Features/MapViewRelease/MapViewReleaseModule.cs
- Main/Features/MapViewRelease/MapViewReleaseService.cs
- Main/Features/MapViewRelease/MapViewReleaseSettingsProvider.cs
- Main/Features/MissionStartGuard/Hooks/MissionStartGuardCalls.cs
- Main/Features/MissionStartGuard/Hooks/MissionStartGuardSwaps.cs
- Main/Features/MissionStartGuard/Hooks/Mission_AfterStart_MissionStartGuard_Patch.cs
- Main/Features/MissionStartGuard/IMissionStartGuardService.cs
- Main/Features/MissionStartGuard/IMissionStartGuardSettingsProvider.cs
- Main/Features/MissionStartGuard/MissionStartGuardLines.cs
- Main/Features/MissionStartGuard/MissionStartGuardModule.cs
- Main/Features/MissionStartGuard/MissionStartGuardService.cs
- Main/Features/MissionStartGuard/MissionStartGuardSettingsProvider.cs
- Main/Features/MissionStartGuard/Models/StartCall.cs
- Main/Features/NameplateCull/Hooks/SettlementNameplatesVM_Update_NameplateCull_Patch.cs
- Main/Features/NameplateCull/INameplateCullService.cs
- Main/Features/NameplateCull/INameplateCullSettingsProvider.cs
- Main/Features/NameplateCull/Models/CullCounts.cs
- Main/Features/NameplateCull/Models/NameplateFacts.cs
- Main/Features/NameplateCull/NameplateCullCalls.cs
- Main/Features/NameplateCull/NameplateCullLines.cs
- Main/Features/NameplateCull/NameplateCullModule.cs
- Main/Features/NameplateCull/NameplateCullRule.cs
- Main/Features/NameplateCull/NameplateCullService.cs
- Main/Features/NameplateCull/NameplateCullSettingsProvider.cs
- Main/Features/SkeletonBuffer/Hooks/SkeletonBufferWatchMissionBehavior.cs
- Main/Features/SkeletonBuffer/ISkeletonBufferGuardService.cs
- Main/Features/SkeletonBuffer/ISkeletonBufferSettingsProvider.cs
- Main/Features/SkeletonBuffer/SkeletonBufferCave.cs
- Main/Features/SkeletonBuffer/SkeletonBufferGuardService.cs
- Main/Features/SkeletonBuffer/SkeletonBufferLines.cs
- Main/Features/SkeletonBuffer/SkeletonBufferModule.cs
- Main/Features/SkeletonBuffer/SkeletonBufferSettingsProvider.cs
- Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs
- Main/Features/SkeletonBuffer/SkeletonBufferTarget.cs
- Main/Features/SkeletonBuffer/SkeletonBufferWatchService.cs
- Main/Features/SkeletonBuffer/SkeletonBufferWatchState.cs
- Main/SubModule.cs
Module data (2):
- Main/_Module/ModuleData/taom_module_strings.xml
- Main/_Module/THIRD-PARTY-LICENSES.txt
Tests (37):
- TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs
- TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
- TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs
- TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseBindingTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseCounterTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseHookTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseLinesTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseModuleTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseServiceTests.cs
- TAOM.Tests/Features/MapViewRelease/MapViewReleaseSettingsProviderTests.cs
- TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs
- TAOM.Tests/Features/MissionStartGuard/MissionStartGuardBindingTests.cs
- TAOM.Tests/Features/MissionStartGuard/MissionStartGuardCallsTests.cs
- TAOM.Tests/Features/MissionStartGuard/MissionStartGuardCanaryTests.cs
- TAOM.Tests/Features/MissionStartGuard/MissionStartGuardHookTests.cs
- TAOM.Tests/Features/MissionStartGuard/MissionStartGuardModuleTests.cs
- TAOM.Tests/Features/MissionStartGuard/MissionStartGuardServiceTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullBindingTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullHookTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullLinesTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullModuleTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullPrefixOrderTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullRuleTests.cs
- TAOM.Tests/Features/NameplateCull/NameplateCullServiceTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferCaveTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferGuardServiceTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferLinesTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferMemoryAdapterTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferSignatureTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferWatchMissionBehaviorTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferWatchServiceTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferWatchStateTests.cs
- TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferWiringTests.cs
- TAOM.Tests/Infrastructure/HotPathSettingsProvidersTests.cs
- TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
Docs, rules and skills (30):
- .claude/rules/harmony-patches.md
- .claude/skills/engine-bump/SKILL.md
- .claude/skills/native-crash-triage/SKILL.md
- docs/INDEX.md
- docs/ai-includes/external-repo-adoption.md
- docs/ai-includes/orientation.md
- docs/features/bannerlord-together-compat.md
- docs/features/coop-interop.md
- docs/features/crash-report.md
- docs/features/map-perf-profiler.md
- docs/features/map-view-release.md
- docs/features/mcm.md
- docs/features/mission-start-guard.md
- docs/features/nameplate-cull.md
- docs/features/skeleton-buffer-guard.md
- docs/reference/engine/mission-and-missionbehavior-lifecycle.md
- docs/reference/engine/mission-frame-threads-and-native-costs.md
- docs/reference/feature-map.md
- docs/reference/harmony-patch-registry.md
- docs/reference/provenance-register.md
- docs/reference/taleworlds-api-snapshot/patch-targets.md
- docs/reference/taleworlds-api-snapshot/reflection-sites.md
- docs/reviews/adopt-yotthani-2026-10-08.md
- docs/reviews/lessons/build-tooling-workflow.md
- docs/reviews/lessons/harmony-il.md
- docs/reviews/lessons/native-cpp-port.md
- docs/reviews/lessons/testing-qa.md
- docs/reviews/rca-yotthani-adoption-2026-10-08.md
- plans/_audit/2026-10-02-perf/REPORT.md
- tools/README.md

REQUIRED SECTIONS:
1. VANILLA AND NATIVE CODE: paste the v1.5.4 code you relied on as code blocks: Mission.AfterStart (Mission.cs 3815-3852), MissionState.LoadMission and FinishMissionLoading, SettlementNameplatesVM.Update, SettlementNameplateVM.UpdateNameplateMT and RefreshBindValues, ScreenBase.HandlePause, HandleActivate and HandleResume, MapScreen.OnPause and HandleIfBlockerStatesDisabled, SceneLayer.ClearRuntimeGPUMemory; and the disassembly of TaleWorlds.Native.dll at 0x69AA0 to 0x69E80 and 0x6AF30 to 0x6B020.
2. DEEP ANALYSIS per feature: concrete scenarios with inputs and the resulting state, including failure paths (a throwing logger, MCM not ready, a dedicated server, another mod (VanillaTuning) holding the pool-1 site, a second game start in the same process, PatchShield stripping prefixes and transpilers).
3. CONFIG CROSS-REFERENCE: the MCM settings in CrashReportSettings and BattleLoadDiagnosticsSettings against their providers, defaults, CoopSettingsRelevance classification and the settings tests; the two localization keys in Main/_Module/ModuleData/taom_module_strings.xml against their use.
4. FINDINGS: for each: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the claim, evidence (code you read, with line numbers), a concrete reproduction or proving input, and the fix. Mark anything you could not prove UNVERIFIED.

QUALITY GATES:
- Every claim cites a file and line you read in this run. No finding from a name or a guess.
- Vanilla behaviour that TAOM copies on purpose is not a bug; say when code matches vanilla.
- Do not re-report items the RCA settled unless the fix is wrong.
- Prefer one proven HIGH to ten speculative LOWs, but report every proven defect.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write any file: the dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-yotthani-adoption-2026-10-08.md, so never write that path yourself. Do not edit, create or delete any file in the repository or elsewhere. Do not run git commands that change state. If you build or test, use only: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (never ./build.ps1, which deploys into the game).
