# K3: critic of the draft work-package map (wp_map.json, WPs 101-136)

Run 2026-11-04-port148, read-only verifier, adversarial stance. Inputs read in full: brief.md, brief2.md,
docs/ai-includes/agent-operating-manual.md, SP\agg\wp_map.json, SP\agg\batch_notes.md, SP\agg\summary.txt,
SP\agg\groups_compact.txt; SP\agg\groups.json and SP\agg\classified.jsonl were queried with Python (throwaway
read-only scripts in SP\k3\, deleted at the end of this session; this report is the only file kept). Lens reports: summaries, rankings and port recommendations of all nine files in
plans/_audit/2026-11-04-port148/deepdive/, plus the full finding bodies where a placement depended on them.

Refs checked this session: HEAD c5b84fb41, origin/bannerlord-1.4.5 1b00d1acd, origin/fix/public-armory-ids-145
8df90444c, origin/bannerlord-1.5.x e45f6df2c. Engine checks: v1.4.8 dump
`C:\Decompiled_Bannerlord\_categories_v1.4.8` and `pwsh -NoProfile -File tools/taom-src.ps1 path <type>`.
No build, test, or ref-changing git command was run.

## 0. Headline

1. Coverage is complete at the group level: all 331 groups in groups.json sit in exactly one WP (checked; no
   duplicates, no strays).
2. The map's wave order breaks 17 of the 111 cross-WP prerequisite edges recorded in classified.jsonl (the
   prerequisite sits in a later wave). Seven of the 17 are one missing file (TAOM.Tests/Infrastructure/RepoPaths.cs,
   parked in WP133 at wave 6); three are one missing mechanism (the generic PatchShieldPolicy.ExcludedTargetMethods
   from 30abf550b, parked in WP129 at wave 5 with a "Leave" note that contradicts its three dependents).
3. Seven of the 92 lens finding ids have no WP; four more are owned by the wrong WP (section 5).
4. Two harness pieces that decide whether executor sessions are safe sit in wave 6 or in a crash WP instead of
   wave 0 (section 1, rows 1-2). On origin/bannerlord-1.4.5, eight PreToolUse gates print an undocumented
   top-level `permissionDecision` and none of them exits 2, so they very likely never block.
5. WP134 (107 commits), WP135 (65), WP136 (64), WP133 (44), WP132 (33) and WP128 (32, 40 feature folders) are too
   big for one session; WP106 and WP112 each bundle several unrelated features (section 3).
6. Waves 1-5 almost all edit Main/SubModule.cs, and 15 to 17 WPs each edit CoopSettingsRelevance.cs,
   SettingsFingerprintTests.cs and TaomSettings.cs (section 4).

## 1. Misplaced groups

| # | Group (commits) | Map | Proposed | Evidence |
|---|---|---|---|---|
| 1 | harness hunk of `crash-report-rethrow-stack` (c79a58521 part 2) | WP108 (w1) | WP102 (w0) | classified c79a58521 port_notes part (2): the nine PreToolUse gates and check-freeze.sh move to `hookSpecificOutput`. Verified on origin/bannerlord-1.4.5: check-commit-subject-version.sh:72-74 prints `{"permissionDecision":"deny",...}` then `sys.exit(0)`; block-dangerous-git.sh:113 prints `{"permissionDecision":"ask",...}` then `exit 0`; the same top-level form with no `hookSpecificOutput` is in block-broad-git-add, check-changelog-changed, check-claude-files-tracked, check-doc-config-drift, check-moduledata-validation and check-native-dll-crt. The hooks doc (code.claude.com/docs/en/hooks, "PreToolUse decision control", fetched this session) says PreToolUse returns its decision inside `hookSpecificOutput`; a top-level `permissionDecision` is not a documented field. 1.5.x harness-facts.md:63 records it as ignored (EMPIRICAL there, #647). Status: undocumented CONFIRMED, "ignored" PLAUSIBLE (not re-tested here). Only the exit-2 gates block (block-no-verify, config-protection, validate-push.sh:122-123). |
| 2 | `harness-git-commit-msg-hook` (0382d5d2a) | WP134 (w6) | WP102 (w0) | No prereqs, real_conflicts empty (classified). A tracked `.githooks/commit-msg` binds every committer, including non-Claude executors, to the v2.0.28 label; batch 29 note 12 says port it early. With row 1's evidence it is the only subject gate known to work on 1.4.x today. |
| 3 | `tests-repopaths-infra` (06252ae2c) plus RepoPaths.cs itself | WP133 (w6) | WP104 (w0) | RepoPaths.cs is absent on origin/bannerlord-1.4.5 (`git cat-file -e` fails). Tests in 91f369984, dc1c37031, 85c608963 (WP111), 15e52d7af (WP106), 2719e59e8 (WP113), eb5cf0289 (WP114), c74ad5aed, 180696430 (WP126), 9304a4cb9 (WP124), 406dd739f (WP128) and a0db0558b need it (their port_notes). Take the 88-line origin/bannerlord-1.5.x file with `CodeRoots = { "Main" }` (the tip lists `"Client"`, added by 03d9db8dc). |
| 4 | step 0 of `plan-018-composition-root` (a0db0558b) | WP118 (w2) | WP104 (w0) | Test-only; closes a false pass that exists on 1.4.x (GameModelOverrideBindingTests reads SubModule.cs raw and counts the commented-out TaomPartyNavigationModel AddModel); "worth taking even if plan 018 is not adopted" (port_notes). It is a prereq of ecaa67d26 and 2719e59e8 (wave 1). |
| 5 | `ui-prefab-validation-gates` (62fb6bb1d, 103636439, 450332b74, abb718c2f) | WP115 (w2) | WP132b (w6) | Test gates, not texture memory; 62fb6bb1d needs PrefabExtensionBrushTests created by 2c7f7fae9 (WP128, w5), so it cannot land in wave 2; gui-prefab-copy-drift (WP132) needs 450332b74. |
| 6 | `perf-programme-2026-10-02` (af990ca76) | WP136 (w6) | WP112a (w2) | It creates docs/reference/engine/mission-frame-threads-and-native-costs.md, which 685c6a1e0 needs (prereq) and to which ecaa67d26 adds section 10; docs-engine-mithrilforge-findings also needs it. Its engine claims were verified on v1.5.3 only: re-run the page's "How to re-verify" steps on v1.4.8. |
| 7 | `improve-run-pipeline` (15 commits) | WP135 (w6) | WP134b (w6) | Cycle: harness-improve-run-pipeline (WP134) needs it; batch 11: port the /improve rebuild as one unit at its end state. If executors will run these plans through `/improve` execute mode on 1.4.x, move both to wave 0 (decision D10). |
| 8 | `tools-ingame-verdict` (c8d69e426), `issue-1074-ingame-verdict-tool` (3) | WP135 | WP133b | Cycle WP133 <-> WP135; batch 32 order: cb4eeaa3d, c8d69e426, check lines, skill hooks, ratchet. |
| 9 | `issue-987-tool-followups`, `tools-review-yield-provenance-loc` | WP135 | WP134b | 09f3126a7 needs plan 027 (b23dc8999) and bc30b76da/fa8c06f82 and is needed by ad3967222; 69d2e8c0a needs the harness_retro chain (c8a3ac9d9, 4358d1622, 3f53e4623). |
| 10 | `tools-lint-docs-commit-view` (35ff005dc) | WP135 | WP134c | lint_docs.py hub: needs 0382d5d2a and ad3967222; needed by 434d1d795 and 2129f753c. |
| 11 | `harness-hang-triage` (9c6d4962b) | WP134 | WP135a | Needs tools/native_decompile.py from d51e43028 (issue-688, WP135). |
| 12 | `harness-context-mode-adoption` (9d327f43e) | WP136 | WP134b | Needed by 9a2a8dae9 (issue-1003-reviewer-rule-rereads). |
| 13 | harness gate groups: `plan-013-bash-hook-prefilter`, `plan-011-stop-reminders-trunk-guard`, `plan-027-powershell-gate-coverage`, `harness-commit-subject-gate`, `harness-config-protection`, `harness-upstream-review`, `harness-upstream-watch`, `harness-ecc-rereview` | WP134 (w6) | WP134a (w1) | Executor safety on a line that will use parallel worktrees: origin/bannerlord-1.4.5 .claude/settings.json has 28 relative registrations and PreToolUse matchers for `Bash`, `Edit|Write`, `''` and `mcp__.*` only, no PowerShell matcher (parsed this session); validate-push.sh reads only the first command line (batch 6, 787fd3663); the git gates cd to CLAUDE_PROJECT_DIR and judge the main checkout for a worktree commit (7e26d77fe, 12d96adc1 port_notes). Batch 10: one end-state transplant. |
| 14 | `plan-014-enlistment-session-scope` (9 commits) | WP124 (w5) | WP114 (w2) | Memory lens answer 2 names e99d705a3 and a67792c40 (with 4263535a1) as the 1.5.x fixes for MEMORY-05, which WP114 owns; DECISIONS #28 lists plan 014 among Mike's 1.4.5 ports. No incoming prereq besides 6fb37c9f7/f6ac2d335 inside the group; issue-830 (WP125) depends on it, so earlier is safe. Optional, medium confidence. |
| 15 | `issue-898-messenger-session-rules` (1dc0a8d7c) | WP114 (w2) | WP128a (w4) | A messenger gameplay fix (no army joins, troop moves or board game at a distance), not memory; it also edits EnlistmentDialogBehavior.cs and ServiceVocabulary.cs; the other #898/#944/#989 messenger groups are in WP128. Optional. |
| 16 | `issue-751-shader-cache-keeper` (372773d1a), `issue-916-bow-shader-sources` (84f0459bc) | WP110 (w1) | HOLD (D11, D12) | Both UNSURE. 372773d1a adds a native proxy xinput9_1_0.dll project (Native/ShaderCacheKeeper) that is safe only if a v1.4.8-written shader_mapping.bin matches its parser; 84f0459bc matters only if a 1.4.8 Armory ships taom_deformer materials (batch 20 note 2: it does not today). Keep them out of the wave-1 critical path. |

Group splits that move a hunk, not a whole group, are in section 3.

## 2. Cross-WP prerequisites

### 2.1 The 17 edges that point to a later wave (classified.jsonl `prereqs`)

| Dependent (WP, wave) | Prereq (WP, wave) | Resolution |
|---|---|---|
| d01bddf10 (102, 0) | fff6d66e9 (134, 6) | Split: WP102 takes only the 7j frontmatter check and the block-no-verify comment (port_notes); the push-judge half goes to WP134a. |
| 332f2ac71 (105, 1) | fa8c06f82 (132, 6) | Port 332f2ac71 onto the 1.4.x validator without the test_validate_xml_schemas.py TAOM_Map pin; WP132a later hand-merges fa8c06f82's SCHEMA_INVALID pass on top (fa8c06f82 port_notes allow "apply that first or hand-merge"). |
| ecaa67d26 (106, 1) | 9ed3ee308 (118, 2) | Adapt: register in IoC.cs/SubModule.cs (brief2). Only real if D2 adopts plan 018. |
| ecaa67d26, 2719e59e8 (1) | a0db0558b (118, 2) | Moved to WP104 (section 1, row 4). |
| 15e52d7af, 85c608963, eb5cf0289, a0db0558b | 35007dbb8 (126, 5) | The dependency is RepoPaths.cs only; satisfied by WP104. 35007dbb8's field-commission fix stays in WP126. |
| 91f369984 (111, 1) | 06252ae2c (133, 6) | Moved to WP104. |
| 6eb2e1b90 (112, 1), f6bf0e588 (112, 1), 346f3e093 (117, 2) | 30abf550b (129, 5) | Move the generic ExcludedTargetMethods mechanism (no Patch92 entries) to WP108. The 30abf550b row says "Leave PatchShieldPolicy.ExcludedTargetMethods"; its three dependents' port_notes require it. Contradiction resolved in favour of the dependents. |
| d99398f12 (110, 1) | a4b5d6258 (123, 4) | Split d99398f12: triage hunk in WP110; package_release.py missing_armory_shaders gate to WP123 after a4b5d6258. |
| 685c6a1e0 (112, 1) | af990ca76 (136, 6) | Moved to WP112a. |
| 62fb6bb1d (115, 2) | 2c7f7fae9 (128, 5) | Group moved to WP132b (wave after WP128). |
| bd1ac8292 (131, 5) | 6ce20f6ed (134, 6) | Spurious on 1.4.x: bd1ac8292 reverts 6ce20f6ed's co-op rethrows, which never reached 1.4.x (bd1ac8292 why; freeze lens "Checked and ruled out"). Only CollectAssemblyTypesPolicy needs 6ce20f6ed's co-op file, and that part is held under D7. |

### 2.2 Hidden edges found in port_notes (not in `prereqs`)

Scanned every PORT/PORT-PARTIAL port_notes for commit hashes that live in another WP.
- 1165a00de, de90a2451 (WP111, P0 child-birth CTD) add an `IOutcomeLog` ctor parameter from cb4eeaa3d (WP133, w6): "port cb4eeaa3d first or the IoC resolve fails". Swap to `IModLogger` in WP111; batch 32 note 2 says do not queue the P0 behind the logging work. STOP condition for WP111.
- ab78564e0 (WP111): two IL-fingerprint tests need IlCallScanner.Fingerprint from a27c6b338 (WP117, w2). Defer those two tests to WP117. Its desertion test file was created by 423d948d8 (WP120); create it fresh.
- ec2a3026f (WP113): HasPlayerParty uses IPlayerControlQuery from 7f3636b2e (WP131, co-op). Adapt in WP113 unless D7 brings 7f3636b2e first.
- a4b5d6258 (WP123): its release-skill text cites a pre-flight 5 grep that arrived with 709649c3f (issue-646, WP134). Move that hunk of 709649c3f to WP123.
- f83a63e57 (WP128): TranspilerSiteBindingTests rows (8b9f0a233, WP133); fold into ReflectionSiteBindingTests or schedule WP133a first.
- 0103a8271 (WP129): lesson prescribes tools/native_decompile.py (719074072, WP135); reword or wait.
- 1d94df9c4 (WP108): "port alongside f75288ebf (#634)" (WP107): the allowlist drops TAOM's swallow on Agent_OnDismount and other shims that #634 makes safe.
- 6eb2e1b90 (WP112): "land 5ee49d05f first or together" (WP108).

### 2.3 Wave-1 ordering inside the wave (the map left these implicit)

Same-wave cross-WP edges from classified.jsonl: WP106 needs WP107 (83ad1eab5), WP108 (649cc30ff needs fb52e619d) and WP112 (ecaa67d26 needs TickProfilerTranspiler from 6eb2e1b90 and ClipBudgetSignature from a9f10688f); WP112 needs WP106 (fdee18f02 needs c286e2393), WP107 (6eb2e1b90 needs Patch91), WP109 (GraphicsOptionsAdapter from c17541f2c); WP111 needs WP108 (RethrowStackPreserver). That is a WP106 <-> WP112 cycle.

Break it by pinning the shared files once (the agent-teams rule for a sub-problem in two briefs): WP106 brings
`Main/Features/MissionPerf/TickProfilerTranspiler.cs` (137 lines, holds `CallSwap`),
`Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs` and `.../PeSectionTable.cs` (88 lines each) verbatim
from origin/bannerlord-1.5.x. Checked: `git log ecaa67d26..origin/bannerlord-1.5.x` over those three paths is
empty, so the tip equals what ecaa67d26 compiled against. WP106 also takes NativePinGateTests at its tip shape
(which includes fdee18f02's summary). WP112 then reuses and never re-adds them, and moves to wave 2.

### 2.4 The Patch91 and PatchShield coupling (re-checked, weaker than the classifier says)

6eb2e1b90's port_notes say its TickAgentsAndTeamsImp PatchShield exclusion "must ship whenever Patch91 lands".
Checked on 1.4.x and v1.4.8:
- origin/bannerlord-1.4.5 Dependencies/Foundation/PatchShield.cs:123-231 shields every Harmony-patched method
  not declared in a TAOM assembly, except three UI namespace prefixes (TaleWorlds.GauntletUI,
  TaleWorlds.TwoDimension, TaleWorlds.MountAndBlade.GauntletUI at :60-69) and SaveShield's own targets;
  :260-296 swallows only MissingMethod, MissingField and TypeLoad.
- origin/bannerlord-1.4.5 Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs:29-49: `HandleAndSwallow`
  returns null (swallow); Native2ManagedPatcher.cs:72 attaches it to every `*CallbacksGenerated` static,
  Mission_TickAgentsAndTeams included.
- v1.4.8 dump Mission.cs: field `tickCompleted = true` :1040; `WaitTickCompletion` spins on it :3585-3591;
  `TickAgentsAndTeamsImp` :3601 sets it true at :3613; `OnTick` clears it at :3740.

So today any exception in the agent tick is already swallowed by the Native2Managed bridge (freeze route 2 of
FREEZE-THREADING-01). Patch91 plus PatchShield adds a new freeze path only after plan 006's 16-name allowlist
(WP108) drops Mission_TickAgentsAndTeams from the bridge. Hence: the ExcludedTargetMethods mechanism and the
TickAgentsAndTeamsImp entry belong in WP108, in the same merge as the allowlist, and WP107's new tickCompleted
restore finalizer (FREEZE-THREADING-01 fix b) should merge before WP108. WP107 then needs no Dependencies edit.

## 3. Size: splits and merges

| WP | Size (wp_map n_commits; real-conflict commits from classified) | Proposal |
|---|---|---|
| 134 | 107; 54 | 134a hook-gate end state (wave 1): row 13 groups plus 30abf550b's config-protection hunk; 134b deep-review lenses, /improve pipeline, retro and pstack/ralph tools (+ rows 7, 9, 12, fa8c06f82 lens half); 134c rules review 2026-11-03, lint_docs chain, .githooks pre-commit, dead-memory lint and the small one-commit harness items. |
| 135 | 65; 39 | 135a native, decompile, taom-src, API snapshot, build and security tools (+ row 11, 30abf550b's native_crash_triage register dump); 135b art, armory, map, creature and graphify tools. |
| 136 | 64; 10 | 136a engine docs, each claim re-checked on v1.4.8 (never carry the 1.5.x "GetActionSet returns the default set" claim: v1.4.8 MBGlobals.cs:33-45 throws, brief2); 136b process docs. |
| 133 | 44; 37 | 133a binding and architecture gates (transpiler-site, HasBody, plan 008 per D8, reflection labels, patch-number, viewmodel-mixin, quality gates, architecture gates); 133b in-game verdict programme (#1071-#1074 plus row 8); 133c plan 010 and plan 019 (D9: CI was removed on 1.4.x in ccef172ad, so low value). If 133a runs early (wave 2), leave tests-architecture-gates (8caac7da5) in wave 5: it uses PrefabExtensionBrushTests.GameDir from 2c7f7fae9 (wave 4); 8b9f0a233's gate is valuable early because the 1.4.5 to 1.4.8 move makes a silently non-matching transpiler plausible (its port_notes), and the line has no CI. |
| 132 | 33; 21 | 132a validators, XSD and tool gates (validate_moduledata.py is serial after WP105); 132b GUI prefab and Gauntlet gates (+ row 5). |
| 128 | 32; 22; 40 feature folders | 128a messengers, recruitment and troop weight (+ row 15); 128b inventory, UI data and banner sweep; 128c data defects that need a scope call (issue-749 Lindon balance rows, issue-757 backstories, issue-758 UNSURE, issue-756 policy ids, issue-957 alignment sides with 57 real-conflict paths). |
| 106 | 5 commits, but ecaa67d26 is 133 files and four features | Keep MissionStartGuard (+ BattleLoad `finally`), SkeletonBuffer watch and guard, native pin gate, 649cc30ff, 83ad1eab5 and the three pinned helper files. New 106b (wave 2) takes NameplateCull and MapViewRelease from ecaa67d26 (the classifier itself recommends this split) plus MEMORY-01, MEMORY-04, MEMORY-07, RUNTIME-PERF-11B, RUNTIME-PERF-14. |
| 112 | 13; 12; 123 real-conflict paths | 112a mission frame instruments (5dfa8f4cb extract, 6eb2e1b90, daba6f0a0, the 0273e2ea4 trio, 685c6a1e0, 43ca92ee9, 67eff55cf, row 6); 112b map and memory instruments (f6bf0e588, a9f10688f minus pinned helpers, 3dac7ab5b, 639df895b). Both wave 2. FREEZE-THREADING-06 (heartbeat watchdog, new work) moves to WP110 so one WP owns "main thread is silent" detection for both the render-wait notice and the freeze watchdog. |
| 119 + 120 | 4 + 9 | Merge: both are always-on runtime cost cuts and share TaomSettings.cs, CoopSettingsRelevance.cs, SettingsFingerprintTests.cs. |
| 103 | 7, all UNSURE | Merge into WP102 if D1 adopts plan 020 (same files: .claude/settings.json, tools/test_hooks.sh); drop if D1 rejects. |
| 131 | 6, mostly UNSURE | Dissolve if D7 keeps co-op out: bd1ac8292's re-cited lesson and comments go to WP107, 175b149fd's /issue duplicate-search step to WP134c, the rest held. |
| 127 | 5 | Keep; no incoming prereq, so it can run as filler in wave 3. |

Hunk-level splits of mixed commits (each owner takes only its own hunk):
- c79a58521: harness hunk to WP102; RethrowStackPreserver, crash signature, CombatLogData fix to WP108.
- 30abf550b: ExcludedTargetMethods mechanism to WP108; config-protection normalisation to WP134a;
  validate_mesh_refs loose-first to WP105; native_crash_triage registers to WP135a; lint_docs regex to WP134c;
  BannerBearers race gate stays in WP129.
- 6ae924638 (batch 29 note 1, "port it split by group"): NRE-finalizer narrowing in WP111 with 0002cb561;
  AutoResolveDiagnostics deletion to the merged WP119+120 with 158408c73; HeroRace `_isNewCampaign` to WP114 with
  eb5cf0289; career-quest owner fix to WP126 with 180696430. This removes three cross-WP edges into WP111.
- 25995dc91: ShaderPrecompilation re-park (D4) stays in WP110; TaomCustomBattleBannerBearersModel to WP129.
- fa8c06f82: validate_xml_schemas.py and SCHEMA_INVALID to WP132a; deep-review lens split to WP134b.
- d99398f12, 709649c3f, ab78564e0, fdee18f02, d01bddf10: see section 2.

## 4. Single-owner and hot-file contention

Every WP in a wave runs in its own git worktree (the repo-root tests accept a worktree `.git` file:
origin/bannerlord-1.4.5 ExitStallDisarmTests.cs:125-128 checks `Directory.Exists || File.Exists`). One integrator
merges in the order of section 6 and runs the full suite after each merge.

| File | WPs that edit it (proposed ids) | Rule |
|---|---|---|
| Main/SubModule.cs | 104, 105, 106, 107, 108, 109, 110, 111, 113, 116 (new probes, likely), 123; 106b, 112a, 112b, 114, 117, 118; 119+120, 121, 122; 124, 125, 126, 128a, 128b, 129, 130, 131; 133b | Every 1.5.x registration through Main/Composition or a `*Module.cs` lands here on 1.4.x (brief2). One contiguous, commented block per feature; new categories through `TryPatchCategory` after WP104. |
| Main/IoC.cs | 105, 106, 106b, 108, 109, 111, 112a, 112b, 117, 118, 122, 126, 128a, 128b, 133b | One registration line per feature IoC; same merge order. |
| Main/TAOM.csproj | 110 (82645b53f WinForms refs), 123 (e6ee8c998 Game Pass staging), 117 (6027ecfae pins), 133c (4d2a6c9ac, b8c000452) | Merge 110, 123, 117, 133c. |
| Directory.Build.props | 123 (057628a2b, 9cedb92df, 3c747f4fb), 133a (affcee2ea) | 123 before 133a. |
| Dependencies/ | owner 108 (PatchShield, PatchShieldPolicy, RethrowStackPreserver, Dependencies/SubModule.cs); list entries or small edits by 112a, 112b, 115 (b41de91e8, 9985f2db3 csproj), 117 (346f3e093, 6027ecfae, d31026b5b), 122 (943ab74c0), 123 (9c310bc23, e6ee8c998), 131 (bd1ac8292 optional), 132a (48d66b59a comment), 133c, 135a (4e408e0e0) | 108 first; every later WP only adds entries. Each of these needs a release that ships TAOM and TAOM.Dependencies from one build (#371); E3 shows the public build already mismatched. |
| .claude/settings.json, tools/test_hooks.sh, gate scripts | 102, 103, 134a, 134b, 134c; settings hunks also in 135b (b8833474e) and 136b (470ee64e9, 7d13adc36) | One harness lane owns them: 102, 103, 134a, 134b, 134c. Hand 135b's and 136b's settings hunks to 134c. WP105's hook lines (check-moduledata-validation.sh `--code MISSING_COLLISION_BODY`, session-start.sh drift block) merge before 134a. |
| tools/validate_moduledata.py | 105, 132a, 134b (09f3126a7), 135a/b if any | Serial: 105, 132a, 134b. The 1.4.x file is the older 230-line shape (batch 31 note 5). |
| tools/package_release.py | 123 (owner, incl. the d99398f12 gate), 115 if b41de91e8 touches it | 123 first. |
| CoopSettingsRelevance.cs (15 WPs), SettingsFingerprintTests.cs (17), TaomSettings.cs (17), BattleLoadDiagnosticsSettings.cs (106, 107, 112a/b, 117, 119+120, 122) | counted from groups.json files | Bookkeeping hot spot: the integrator recounts the fingerprint census on each merge. |
| taom_module_strings.xml and the 12 language files | 104, 106, 106b, 109, 110, 112a, 121, 123, 126, 128a/b, 129, 130, 132, 133, 134 | Earlier WPs add English rows only; WP130 merges last in its wave and runs /localize. |

## 5. Lens findings with no WP, and wrong owners

92 finding ids exist in the nine reports; 85 appear in some WP's `extra` (ranges expanded).

Unowned:
- LOADING-HANG-05: drop, not a defect (brief fact 6 is a misread clock: 107.5 s of menu time, a 3.8 s load).
  Optional `sinceOpenMs=` token in WP110's battle-load work.
- RUNTIME-PERF-02: drop, same finding as LOADING-HANG-05.
- RUNTIME-PERF-01: WP110, same mechanism as LOADING-HANG-01 and FREEZE-THREADING-02.
- RUNTIME-PERF-14: WP106b with MEMORY-01 (same inventory transient, cheat-mode hypothesis UNVERIFIED for this player).
- RUNTIME-PERF-15: WP106, same as FREEZE-THREADING-07 and LOADING-HANG-06 (patterns verified on the installed
  v1.4.8 native DLL by the freeze lens).
- XML-DATA-02: WP101 environment step (install the public 2026-09-29 Armory on the laptop; update the CLAUDE.md
  laptop trap) plus WP132a (validator header prints each module's SubModule.xml Version and newest file date).
- XML-DATA-04: WP123 with RELEASE-INTEGRITY-08 (Armory pairing check) and a log-triage doc line. Decision D15:
  the xml-data lens says allowlist taom_body_length in the XSD gate; the 709649c3f port_notes say do not, because
  on 1.4.x the attribute signals a 1.5.x-era Armory.

Wrong or partial owners:
- RUNTIME-PERF-11 part A (ButterLib Distance Matrix, 1325c36ea) belongs to WP117, not WP106.
- LOAD-TIME-03 part 1 (TAOM's own Patch61 finalizers binding `__originalMethod`) belongs to WP116 (same
  SaveLoadDiagnostics hooks as SAVE-TIME-07); part 2 (5ee49d05f) stays in WP108.
- FREEZE-THREADING-06 moves from WP112 to WP110; FREEZE-THREADING-01 fix (a) is plan 006 in WP108, fixes (b) and
  (c) are new work in WP107.
- MEMORY-01, MEMORY-04, MEMORY-07, RUNTIME-PERF-11B move to WP106b; MEMORY-09 to WP112b.

Batch-note leads with no lens id (recommend homes, not required by the brief): #774 raid counted twice (WP113,
new); ShaderPrecompilePlayerAgentGuard calls FinishDeployment inside Mission.Tick's OnMissionTick loop while the
walk is ACTIVE on 1.4.x (batch 18 note 2; WP110 under D4); `[MissionDiag] Campaign: <time read failed:
DivideByZeroException>` at E3:56 and E4:55 (batch 22 note 8; WP119+120); NavigationCache swallow-and-retry load
loop on old saves (batch 24 note 5, UNVERIFIED on 1.4.x single player; WP111 investigation).

Baseline note for WP101: the BHA0001 warning on Patch88_InitializeLordPartyPropertiesScope.cs(22,2) is an analyzer
false positive on a nested type. taom-src on the installed v1.4.8 DLLs shows `LordPartyComponent.InitializationArgs`
(C:\Users\mikew\.taom-src\v1.4.8\TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent.cs:14) with
`InitializeLordPartyProperties(MobileParty mobileParty, Hero owner)` at :29, which is what the patch binds.

## 6. Final wave and lane schedule

Concurrency: at most four executor worktrees plus one integrator. Commands in every plan:
`dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` and
`dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` (baseline on 1b00d1acd merged with the
public branch: 9154 tests, 9151 passed, 3 skipped, per brief2). Never ./build.ps1. Re-subject every pick to
`v2.0.28` with `Ported-from: <1.5.x hash>` in the body; drop 1.5.x CHANGELOG hunks; keep 1.5.x patch numbers
(1.4.x tops out at Patch88); port plans 006, 007, 009, 013, 018 from their post-merge end states.

Wave 0 (gate for everything): lane H: 101, then 102 (with 103 folded in if D1 adopts); lane C: 104 after 101.
Merge order 101, 102, (103), 104.

Wave 1 (player symptoms): lane A 107, 106, 109; lane B 108, 123; lane C 105, 110; lane D 111; lane E 113, 116;
lane H 134a. Merge order 107, 108, 105, 106, 113, 109, 111, 110, 116, 123, 134a.

Wave 2 (instruments, load time, memory): 118 first if D2 adopts plan 018; then 112a, 117, 114, 106b, 112b, 115
(desktop Modding Kit session), 133a (optional early binding gates).

Wave 3 (runtime perf, measured with wave-2 instruments): 119+120, 121, 122 (D6), 127 as filler.

Wave 4 (shared-feature fixes): lanes enlistment 124 then 125 (never parallel: 79 and 106 real-conflict paths in
the same Enlistment files), career 126, campaign 128a, 128b, 128c, battle 129, co-op 131 (D7). Merge order 124,
126, 129, 128a, 125, 128b, 128c, 131, then 130 last.

Wave 5 (gates, harness, tools, docs): 132a, 135a, 134b, 133b, 132b, 135b, 136b, 134c, 136a, 133c (D9).

Release rule: no player release before WP123 merges and its gate runs; any release containing WP108, 112a/b,
115, 117 or 135a output ships TAOM.Dependencies from the same build.

## 7. Maintainer decisions that gate the schedule

D1 plan 020 (CHANGELOG from commit bodies) on 1.4.x. D2 plan 018 composition root. D3 Serena removal
(9cfcadd07, 2992763f6; this session still offers Serena tools). D4 ShaderPrecompilation re-park (25995dc91) or
keep ACTIVE and fix the in-tick behaviour removal. D5 SkeletonBuffer guard, TAOM's first write to engine code (ship
the watch first). D6 ShadowfaxScreens as perf or feature. D7 co-op scope (no Coop install here). D8 plan 008 (a
#652 comment says "no port to bannerlord-1.4.5"). D9 plans 010 and 019. D10 /improve execute mode for these plans.
D11 ShaderCacheKeeper (needs a v1.4.8 shader_mapping.bin). D12 ShaderSourceInstall (only if a 1.4.8 Armory ships
taom_ shaders). D13 the WP128c data rows. D14 TAOM_Map package pin for 1.4.x releases (b30 note 3). D15 the
taom_body_length XSD treatment.

## 8. STOP conditions plan writers must copy

- Laptop Armory is v2.0.26 (XML-DATA-02, validator run reported 3,423 BROKEN_ITEM_REF; not re-run by me). Item,
  mesh and body verdicts on the laptop are invalid. Once WP102's gate-output fix is live,
  check-moduledata-validation.sh (origin/bannerlord-1.4.5 :97-105, :130) denies every ModuleData commit on the
  laptop until the public Armory is installed; its 45 s budget is also close to the 41 s validator run the lens
  measured.
- Decide D1 before merging the gate-output fix: it makes check-changelog-changed.sh enforce.
- Executors use the Bash tool for git until WP134a lands (PowerShell is ungated on 1.4.x).
- WP110: no "please wait, compiling shaders" notice that cannot tell a draining count from a frozen one (#612
  lesson in brief2; E3 is not established as a slow compile).
- WP111: do not add the IOutcomeLog ctor parameter without cb4eeaa3d; swap to IModLogger.
- WP106: SkeletonBuffer guard only with D5; watch first; v1.4.8 KnownBuilds row (14185944, 0x88681, 0xD96D18,
  0x886B4, 0x88475, 0x898F0) comes from the freeze lens scan and must pass SkeletonBufferInstalledBinaryTests.
- WP116: no SAVE-TIME-01 fix until the census names the carrier.
- WP123: do not drop module shader sacks (e4c2c159a) before a v1.4.8 sack test (batch 13 note 7).
- Do not pop or drop the existing `stash@{0}` ("discarded pre-pull: mcp blender + serena regen +
  settings.local") or touch the uncommitted .serena/project.yml; create worktrees from origin refs.

## 9. What I verified, and what stays UNVERIFIED

Verified this session: group coverage and the 111/17 edge counts (Python over groups.json, classified.jsonl,
wp_map.json); the gate output form and exit codes of every 1.4.x hook; the 1.4.x settings.json matchers; the
hooks doc wording; PatchShield shielding scope and swallow set; HandleAndSwallow; the v1.4.8 tickCompleted lines;
RepoPaths absence on 1.4.x and the tip file's CodeRoots; the three ecaa67d26 helper files and their history; the
InitializationArgs member on installed v1.4.8; the worktree-tolerant repo-root probe; current refs, stash and
working-tree state.
UNVERIFIED: that Claude Code ignores a top-level `permissionDecision` (documented as absent, observed on 1.5.x);
whether `git cherry-pick` runs the commit-msg hook; the laptop validator numbers (lens-reported); every 1.5.x
measurement quoted in classifier notes.

Recommended skills for the orchestrator: /security-scan after WP102 and WP134a; /verify-bindings after WP104,
WP106, WP107, WP108, WP112a, WP117; /research before WP106 (SkeletonBuffer, AfterStart transpiler) and WP117
(MBObjectManager merge); /deep-review per WP; /investigate for SAVE-TIME-01 once WP116's census exists.
