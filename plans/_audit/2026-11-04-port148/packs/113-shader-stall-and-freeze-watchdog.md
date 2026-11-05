# Input pack: WP 113 shader-stall-and-freeze-watchdog

Title (draft): Tell a slow shader compile from a wedge: triage fixes, a main-thread heartbeat with stack capture, honest player notices
Wave: 1   Lane: C   Draft priority: P1   Groups: 6   Port commits: 10
Depends on (WP ids): 106

## Orchestrator notes for this WP (binding)
- V02: E3 cannot be decided from the logs (E4's 68 s silent load completed). The 1.4.x watchdog cannot settle it because the shader count is published only by the blocked main thread. So the core new work is a heartbeat watchdog OFF the main thread (Managed.ApplicationTick-fed timestamp, a background checker that logs [Freeze] with main-thread stacks at 10/20/40/80 s, optional minidump) reusing ThreadStackCapture from f75288ebf (WP106).
- D3: 82645b53f ShaderCompileNotice GO (P2; it covers only compiles before the main menu). ShaderCacheKeeper 372773d1a NO-GO (decision D11): record it as deferred with its gate. 84f0459bc NO-GO (decision D12).
- Decision D4: keep ShaderPrecompilation active on 1.4.x unless your research shows 25995dc91's park reason applies on v1.4.8; check whether E3/E4's in-battle compiles are scene shaders (scene passes are OFF by default since #560) and whether the precompile walk ran in those sessions; record the decision. The banner-bearers model hunk of 25995dc91 goes to WP130.
- A render-wait notice may only claim 'compiling' when it can tell a draining count from a frozen one (#612); the triage tool reports the frozen case when the gap to the watchdog line is 30 s or more (89374fc4f).
- d99398f12: take only the triage hunk here; its package_release.py missing_armory_shaders gate goes to WP115.
- Player-facing guidance from V02 (wait 5 minutes after an update or mod change, compile_shader lines in rgl_log, dump when stuck, lower Stall Threshold to 120) belongs in the feature doc.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V02-e3-wedge-or-compile.md
- plans/_audit/2026-11-04-port148/verify/D3-shader-cache-keeper.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- LOADING-HANG-01 [P0/high] First-battle freeze is a cold shader compile that blocks the main thread past FinishMissionLoading; players kill working loads and TAOM shows and logs nothing | port: 372773d1a,82645b53f,89374fc4f,1581d4d0a  (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)
- LOADING-HANG-04 [P2/high] E3's stale in-flight marker: the same fight on a vanilla scene died twice; the marker cannot tell a compile kill from a hang | port:   (report: plans/_audit/2026-11-04-port148/deepdive/loading-hang.md)
- FREEZE-THREADING-02 [P1/medium] Harad first-battle freeze (E3) is a main-thread stall in the render-ready wait before any mission or agent tick; the 1.4.x watchdog never fires in time and captures no stack | port: 89374fc4f,1581d4d0a  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)
- FREEZE-THREADING-06 [P1/high] No diagnostic on either line names the frame a frozen game is stuck in; proposed heartbeat watchdog with stack capture and an optional self-dump | port: f75288ebf,89374fc4f,1581d4d0a,daba6f0a0  (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)
- RUNTIME-PERF-01 [P0/high] E3 first-battle freeze is a main-thread block during the engine's shader compile, not TAOM per-frame code (negative result; freeze lens owns the fix) | port:   (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)
- #612 Warg-mount wedge lesson

## Port groups owned by this WP (from the commit classification)

### group `shader-precompile-repark`  P2  track=shared-feature-fix  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/TrollBruteForce/ITrollBruteForceService.cs, Main/Features/TrollBruteForce/TrollBruteForceConfig.cs, Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs, Main/Features/TrollBruteForce/TrollBruteForceService.cs, Main/SubModule.cs, TAOM.Tests/Features/TrollBruteForce/TrollBruteForceServiceTests.cs, tools/bind_hill_troll_action_set.py, tools/tests/test_bind_hill_troll_action_set.py
- `25995dc91` 2026-09-26 PORT-PARTIAL P2 pick=conflict :: feat: Add custom battle banner bearers model to restrict trolls from carrying banners
  - why: Mixed troll-sprint commit: mostly the new TrollBruteForce feature and hill troll tooling (OUT), but it also parks Shader Pre-compilation again (its RCA says on both lines) and adds a Custom Battle race gate so trolls never carry banners (shared BannerBearers fix).
  - port notes: Take (1) the Shader Pre-compilation re-park, by hand: on the 1.4.x tip comment out the Patch21 trio (SubModule.cs:421-426, which uses _harmony.PatchCategory, not TryPatchCategory), the TaomPrecompileShaders AddInitialStateOption block (SubModule.cs:638-664) and both MCM attribute stacks in TaomSettings.cs, keeping both properties (SettingsFingerprintTests count); bring rca-shader-precompile-repark-2026-09-25.md, the build-tooling lesson and the doc edits, and update the 1.4.x CLAUDE.md ShaderPrecompilation trap row and Patch21 status row (1.5.x keeps those in docs/ai-includes/orientation.md, absent on 1.4.x). Confirm with the maintainer first: on 1.4.x the walk is the only player-side shader pre-warm, and the 1.5.x replacement (Native/ShaderCacheKeeper, 372773d1a) is absent there. (2) TaomCustomBattleBannerBearersModel: compiles against the 1.4.x IBannerBearerService (IsEnabled, IsRaceAllowed); v1.4.8 CustomBattleBannerBearersModel.CanAgentBecomeBannerBearer has no race check (it tests IsHuman, !IsMainAgent, !IsHero, IsAIControlled), so cave trolls can carry banners in 1.4.x Custom Battle. 1.4.x has no RegisterCustomBattleModels seam (1.5.x added it in feature commit eab517aad, #608): add a minimal BasicGameStarter-only registration in OnGameStart plus the gamemodels.md and gamemodel-registry rows. (3) Optional: the two animation-skeleton lessons (face morph channels, custom melee release clips) as engine notes, marking the native offsets as measured on v1.5.3. Leave TrollBruteForce (Patch92, TrollFormationSpacingTracker, TrollActionTrace, which 1.5.x later deleted), IFormationAdapter.UnitDiameter and the MixedFormations UnitPitch change (not a pure no-op: a mounted formation's vanilla UnitDiameter is 1.6 m, QuadrupedalRadius 0.8 in Native managed_core_parameters.xml), bind_hill_troll_action_set.py, add_face_morph_channels.py and the hill troll docs.
  - conflicts: Main/Features/TrollBruteForce/ITrollBruteForceService.cs, Main/Features/TrollBruteForce/TrollBruteForceConfig.cs, Main/Features/TrollBruteForce/TrollBruteForceMissionBehavior.cs, Main/Features/TrollBruteForce/TrollBruteForceService.cs, Main/SubModule.cs, TAOM.Tests/Features/TrollBruteForce/TrollBruteForceServiceTests.cs, tools/bind_hill_troll_action_set.py, tools/tests/test_bind_hill_troll_action_set.py

### group `issue-751-shader-cache-keeper`  P1  track=shader  commits=1  sim: clean=0 conflict=1
symptoms: shader-stall, slow-load
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/THIRD-PARTY-LICENSES.txt
- `372773d1a` 2026-10-06 UNSURE P1 pick=conflict :: feat(shaders): v2.0.34 - keep the shader cache across mod list changes
  - why: A proxy xinput9_1_0.dll that stops the engine deleting its compiled shader cache when only shader-less mods change; worth it on 1.4.x only if v1.4.8 writes the shader_mapping.bin header keeper.c parses.
  - port notes: For: the installed v1.4.8 TaleWorlds.Native.dll imports XINPUT9_1_0.dll and contains 'Mod change detected' and 'shader_mapping.bin', and 1.4.x's own precompile hint (taom_module_strings.xml:826) says the game clears compiled shaders on any module-list change, which would also discard a player's shader pre-compilation (for example after toggling BetterTime). Against: keeper.c parses the v1.5.4 layout (u32 0x0783, build text, sorted module list) and bails on lengths only; the only shader_mapping.bin on this laptop (2026-08-14, build 77991, the same build as the newest rgl log there) holds u32 0x05EF, the build text and no module list. Decide by reading a shader_mapping.bin written by v1.4.8 build 119303 (desktop or a player). If it matches, port Native/ShaderCacheKeeper (new native project, built at release) and the channel copy step; if not, adapt the parser after decompiling v1.4.8's check.
  - conflicts: Main/_Module/THIRD-PARTY-LICENSES.txt
  - issues: 751

### group `issue-752-shader-compile-notice`  P2  track=shader  commits=1  sim: clean=0 conflict=1
symptoms: shader-stall
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/SubModule.cs, Main/TAOM.csproj
- `82645b53f` 2026-10-06 PORT-PARTIAL P2 pick=conflict :: feat(shaders): v2.0.34 - show shader compile progress during rebuilds
  - why: A small window that counts compile_shader lines in rgl_log during the pre-menu shader rebuild so the game does not look hung, lowering process priority meanwhile; the v1.4.8 native carries the same 'compile_shader: %s' log format.
  - port notes: Load-time infrastructure rather than gameplay; nothing depends on it. Adapt SubModule.cs (Start right after EarlyLogBridge in OnSubModuleLoad, Stop in OnBeforeInitialModuleScreenSetAsRoot), the System.Windows.Forms and System.Drawing references in TAOM.csproj, and the three strings (taom_module_strings.xml plus the 12 language files). IDedicatedServerProvider exists on 1.4.x. A cold-cache v1.4.8 rgl log was not read; a build-77991 log on this laptop has the marker lines. It closes at the main menu, so it does nothing for E3's in-battle WaitingForRender stall.
  - conflicts: Main/SubModule.cs, Main/TAOM.csproj
  - issues: 752

### group `battle-load-triage`  P1  track=diagnostics  commits=5  sim: clean=0 conflict=5
symptoms: loading-hang, shader-stall, battle-freeze
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .gitattributes, Client/ClientSubModule.cs, Client/ClientUiExtensions.cs, Main/SubModule.cs, TAOM.Tests/Composition/ClientSubModuleTests.cs, TAOM.Tests/Infrastructure/ClientIsolationGateTests.cs, tools/package_release.py, tools/tests/test_package_release.py, tools/tests/test_triage_battle_load.py, tools/triage_battle_load.py
- `89374fc4f` 2026-10-18 PORT P1 pick=conflict :: fix(tools): v2.0.34 - battle-load triage spots a frozen shader count
  - why: triage_battle_load.py called a render-wait hang a draining shader cache when the 1 Hz WaitingForRender stream had gone silent long before the watchdog, which is exactly E3's shape (2720 then 2697, then silence); the fix reports a frozen count.
  - port notes: Applies cleanly: the 1.4.x triage_battle_load.py differs from the pre-commit 1.5.x file only in two log-text strings (keep 1.4.x's 'Patch43 diagnostics failed to apply'). The detector needs a watchdog line, which E3 lacks: the 1.4.x watchdog waits StallWatchdogSeconds=300 (BattleLoadDiagnosticsSettings.cs:36) and E3 ends 92 s after its last sample, so extend the port to measure the silence to the last [MemSample] line when no watchdog line exists. Follow-up 1581d4d0a (report only this load's watchdog) continues the series.
- `d99398f12` 2026-10-19 PORT-PARTIAL P1 pick=conflict :: Add shader source files for bow deformers and implement shader source installation service
  - why: A mixed commit: the bow-deformer shader sources and installer (#916), plus an Armory shader-source release gate and a triage 'post-load freeze' check (flat heapMB [MemSample] tail after BattlePlayable, the #634 shape) that its message never mentions.
  - port notes: Take the triage_battle_load.py post-load freeze check and its tests, adapting the next-step text that cites v2.0.35 [MissionStall] and [SkeletonBuffer] lines 1.4.x lacks; it needs BattlePlayable, so it would not fire on E3, though E3's tail has the same shape (heapMB=466 on [MemSample] lines 220-223 after the last WaitingForRender). Take the package_release.py missing_armory_shaders gate and tests (hand insert, as for a4b5d6258). Take ShaderSourceInstall (18 .rs files, service, adapter, .gitattributes -text rule, warning string) only when a 1.4.x Armory package carries the 2026-10-15 metallic bow materials: E1 and E2 contain 0 taom_deformer lines and every 'Missing shader from sack' line names a pbr_terrain variant, so the public 1.4.8 Armory predates them. If taken, port the finished form from 84f0459bc, wired from SubModule.cs as here (1.4.x has no Main/Composition).
  - prereqs: 89374fc4f, a4b5d6258
  - conflicts: .gitattributes, Main/SubModule.cs, tools/package_release.py, tools/tests/test_package_release.py
- `b9eabd712` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: Refactor UIExtenderEx registration to OnGameStart for improved stability
  - why: The UIExtenderEx move is TAOM.Client-only, but the bundled triage_battle_load.py change, which measures render-wait silence to the end of the log when there is no watchdog line, fits E3 exactly (last WaitingForRender at 20:02:50, then only [MemSample] lines to 20:04:22).
  - port notes: Take the tools/triage_battle_load.py and tools/tests/test_triage_battle_load.py hunks after 89374fc4f (it added _render_silence_s; the 1.4.x tool has none). Also take the harmony-il.md lesson, whose v1.4.8 facts hold (CampaignUIHelper.cs:210 initializes a static from GameTexts.FindText; EncyclopediaHeroPageVM.cs:948 reads SkillObjectComparerInstance inside Refresh). Leave the Client/ registration move. 1.4.x registers UIExtender at OnSubModuleLoad (Main/SubModule.cs:179), but no 1.4.x mixin patches a method that reads a CampaignUIHelper static field: MessengerEncyclopediaMixin patches RefreshValues, and MapInfoVM.Refresh only calls FloatToString. The hazard is therefore latent there.
  - prereqs: 89374fc4f
  - conflicts: Client/ClientSubModule.cs, Client/ClientUiExtensions.cs, Main/SubModule.cs, TAOM.Tests/Composition/ClientSubModuleTests.cs, TAOM.Tests/Infrastructure/ClientIsolationGateTests.cs, tools/tests/test_triage_battle_load.py, tools/triage_battle_load.py
- `76a395729` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: feat: Implement LastSeenTextGuard to prevent NullReferenceException in HeroHelper.GetLastSeenText
  - why: The LastSeenText guard adapts to a v1.5.x rewrite (v1.4.8 HeroHelper.GetLastSeenText, HeroHelper.cs:20-34, reads only LastKnownClosestSettlement), but the bundled triage fix stops an older watchdog line from anchoring the render-wait silence.
  - port notes: Take only tools/triage_battle_load.py, tools/tests/test_triage_battle_load.py and the state-lifecycle-save.md lesson rewrite, after 89374fc4f and the triage hunks of b9eabd712. Leave Patch123 (HeroHelper_GetLastSeenText_Patch, LastSeenState), their tests, the v1.5.5-impact note and the registry row.
  - prereqs: 89374fc4f, b9eabd712
  - conflicts: Main/SubModule.cs, tools/tests/test_triage_battle_load.py, tools/triage_battle_load.py
- `1581d4d0a` 2026-10-26 PORT P2 pick=conflict :: fix(tools): v2.0.35 - battle-load triage reports only this load's watchdog
  - why: The battle-load triage tool, which exists on 1.4.x and is the tool for reading the Harad freeze logs, now judges a stuck load only by that load's own watchdog line and words a shaders=0 render wait consistently.
  - port notes: Apply after 89374fc4f and the triage hunks of b9eabd712 and 76a395729. The 1.4.x tool (1,413 lines against 1,648 on the 1.5.x tip) lacks _render_silence_s and the frozen-count rule. docs/features/battle-load-diagnostics.md exists on 1.4.x; the RCA doc is new.
  - prereqs: 89374fc4f, b9eabd712, 76a395729
  - conflicts: tools/tests/test_triage_battle_load.py, tools/triage_battle_load.py
  - issues: 1025

### group `issue-916-bow-shader-sources`  P2  track=shader  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .editorconfig, LICENSE-CONTENT.md, Main/Adapters/IShaderSourceFileSystem.cs, Main/Adapters/ShaderSourceFileSystem.cs, Main/Composition/FeatureModules.cs, Main/Features/ShaderSourceInstall/ShaderSourceInstallEntry.cs, Main/Features/ShaderSourceInstall/ShaderSourceInstallService.cs, Main/SubModule.cs, Main/_Module/THIRD-PARTY-LICENSES.txt, TAOM.Tests/Features/ShaderSourceInstall/ShaderSourceInstallServiceTests.cs, tools/package_release.py, tools/tests/test_package_release.py
- `84f0459bc` 2026-10-19 UNSURE P2 pick=conflict :: fix(shaders): v2.0.35 - TAOM installs its bow shader sources at startup
  - why: Installs the 18 taom_deformer bow shader sources that the 2026-10-15 Armory art drop needs, but the 1.4.8 Armory does not reference those shaders today.
  - port notes: Decided by whether a 1.4.8 Armory release carries the taom_deformer_{bow,longbow,crossbow} materials (lotraom-assets db981663). Today it does not: the laptop Armory (v2.0.26) shader_compile_report.log has 0 taom_ rows and uses native bow_deformer/longbow_deformer/crossbow_deformer, 92a30ba42 says the public 1.4.8 build ships the 2026-09-29 Armory, and the E2 rgl has no taom_ shader lines. If it ever does: port d99398f12 plus this commit (18 .rs files, ShaderSourceInstallService, IShaderSourceFileSystem, the MainMenu inquiry, 13 string rows), registering through SubModule/IoC instead of Main/Composition. The tools/package_release.py shader gate needs the 1.5.x package_release.py evolution first (1.4.x copy is 397 lines vs 1022 at d99398f12). Becomes urgent the day a 1.4.8 Armory ships taom_ shaders (invisible archers).
  - prereqs: d99398f12
  - conflicts: .editorconfig, LICENSE-CONTENT.md, Main/Adapters/IShaderSourceFileSystem.cs, Main/Adapters/ShaderSourceFileSystem.cs, Main/Composition/FeatureModules.cs, Main/Features/ShaderSourceInstall/ShaderSourceInstallEntry.cs, Main/Features/ShaderSourceInstall/ShaderSourceInstallService.cs, Main/SubModule.cs, Main/_Module/THIRD-PARTY-LICENSES.txt, TAOM.Tests/Features/ShaderSourceInstall/ShaderSourceInstallServiceTests.cs, tools/package_release.py, tools/tests/test_package_release.py
  - issues: 916

### group `battle-load-triage-frozen-count`  P3  track=diagnostics  commits=1  sim: clean=0 conflict=1
symptoms: battle-freeze
- `0b74cc2dd` 2026-10-19 PORT P3 pick=conflict :: fix(state-lifecycle): clarify outcome of battle load issue related to mount swapping
  - why: Lessons-doc bullet recording that a public-1.4.8 battle-load stall with a frozen shaders= count was a wedge cured by swapping the hero's Warg mount for a horse.
  - port notes: Docs only: add the 3-line Outcome bullet to docs/reviews/lessons/state-lifecycle-save.md after 89374fc4f, which adds the parent lesson entry and the RENDER_WAIT_SILENCE_THRESHOLD_S frozen-count check to tools/triage_battle_load.py (both absent on origin/bannerlord-1.4.5). The claim is about a 1.4.8 player, so it holds for this line; it matches E3's WaitingForRender signature.
  - prereqs: 89374fc4f

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 9, batch 14, batch 19, batch 20, batch 28
