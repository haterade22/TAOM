# Input pack: WP 137 dev-tools-native-and-src

Title (draft): Developer tools: taom-src per build, native build archive and match, native decompile, API snapshot, security hygiene
Wave: 5   Lane: B   Draft priority: P2   Groups: 12   Port commits: 21
Depends on (WP ids): 103

## Orchestrator notes for this WP (binding)
- Take fe20eddff's taom-src docs (cache per game build) with 97219836c and 857734c64.
- Take 30abf550b's native_crash_triage register-dump hunk.
- D4 WP-D: refresh the stale API snapshot (6 classes missing) and fix the mis-catalogued reflection row.
- Closes the 1.4.x TODO plan 005 (security hygiene) using the 1.5.x implementation.
- Never vendor MCMv5.dll.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D4-patch88-and-binding-health.md
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `plan-005-security-hygiene`  P2  track=dev-tools  commits=4  sim: clean=1 conflict=3
- `6b34fd00f` 2026-09-24 PORT P2 pick=conflict :: fix(tools): v2.0.30 - faction-map helpers take paths as arguments
  - why: tools/process_faction_map.py pasted file paths into generated python -c source as r'<path>', so a quote in a path broke the script and let path text run as code; the fix passes paths through sys.argv, and both touched files are unchanged on 1.4.x since the fork.
  - port notes: The only pick conflict is CHANGELOG. The checklist line this adds is reworded by a0fa3cff2 and 01b970185, so take all three. Do not carry this body's claim that the credential is gone from disk; a0fa3cff2 retracts it.
- `a0fa3cff2` 2026-09-24 PORT P2 pick=conflict :: fix(tools): v2.0.30 - review follow-ups for plan 005
  - why: Adds tools/tests/test_process_faction_map.py (folder names containing a quote or Python text) and fixes the vendored-credential checklist grep so it can see inside .tar.gz drops.
  - port notes: tools/tests/ already exists on 1.4.x. Take the test module and the external-repo-adoption.md line; the review record, RCA and lessons are optional. Check that no credential value travels with the port (brief rule 5): the text should name paths only.
  - prereqs: 6b34fd00f
- `01b970185` 2026-09-24 PORT P3 pick=conflict :: fix(docs): v2.0.30 - convergence fixes for plan 005
  - why: Convergence fix: the checklist's archive loop now finds .tar.gz and .tgz files at any depth using find, plus corrections to the records.
  - port notes: Take the external-repo-adoption.md line. Take the record and count edits only if the records are ported, and recount LESSONS-LEARNED on 1.4.x.
  - prereqs: a0fa3cff2
- `337372d84` 2026-09-24 PORT P3 pick=clean :: docs(reviews): v2.0.30 - Codex prompt for plan 005
  - why: Adds the Codex prompt record for plan 005's review as a new file.
  - port notes: Record only; optional.

### group `plan-024-managed-debug-profile`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
- `37ff41e4a` 2026-09-24 PORT P2 pick=conflict :: chore(debug): v2.0.30 - managed-only default launch profile
  - why: Makes the default Bannerlord launch profile attach only the managed debugger, with mixed native debugging moved to a second profile, to target the desktop's 20 to 40 times slower Harmony patching; 1.4.x's launchSettings.json is still the managed-plus-native form.
  - port notes: Applies as-is; the only conflict is CHANGELOG. The speed-up is marked UNVERIFIED in the commit itself, and the port inherits that. Later 1.5.x edits to this file (a7cca2d5f, 4c58ea114) change commandLineArgs; classify them on their own, not with this one.

### group `issue-688-native-decompile`  P2  track=diagnostics  commits=2  sim: clean=0 conflict=2
symptoms: crash, battle-freeze, loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/TrollBruteForce/BehaviorTreeElements/BruteForceTask.cs, Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs, Main/Features/TrollBruteForce/ITrollBruteForceService.cs, Main/Features/TrollBruteForce/TrollBruteForceConfig.cs, Main/Features/TrollBruteForce/TrollBruteForceService.cs, Main/Features/TrollBruteForce/TrollClipLog.cs, Main/Features/TrollBruteForce/TrollClipTrace.cs, TAOM.Tests/Features/BannerBearers/BannerBearerServiceTests.cs, TAOM.Tests/Features/TrollBruteForce/TrollBruteForceConfigTests.cs, TAOM.Tests/Features/TrollBruteForce/TrollBruteForceServiceTests.cs, TAOM.Tests/Features/TrollBruteForce/TrollClipLogTests.cs, tools/bind_hill_troll_action_set.py, tools/blender/export_rig_for_kit.py, tools/gen_troll_anim_clips.ps1, tools/lint_docs.py, tools/native_decompile.py, tools/oneoff/add_hill_troll_hammer_items.py, tools/set_clip_balance_name.py, tools/tests/test_add_hill_troll_hammer_items.py, tools/tests/test_bind_hill_troll_action_set.py, tools/tests/test_check_race_morph_channels.py, tools/tests/test_collision_body_borrowed.py, tools/tests/test_model_registry.py, tools/tests/test_native_decompile.py, tools/tests/test_set_clip_balance_name.py, tools/tests/test_validate_mesh_refs.py, tools/tests/test_validate_moduledata.py, tools/tests/test_wire_hill_troll_race.py, tools/validate_mesh_refs.py, tools/validate_moduledata.py ...
- `719074072` 2026-09-26 PORT-PARTIAL P2 pick=conflict :: Add unit tests for animation clip generation and native decompilation
  - why: Another mislabelled sprint commit whose portable core is the first version of tools/native_decompile.py (headless Ghidra pseudo-C for a native RVA) and its feature doc; the rest is TrollBruteForce, hill-troll content tooling and community creature guides.
  - port notes: Take tools/native_decompile.py with tools/tests/test_native_decompile.py, docs/features/ghidra-native-decompile.md, the Ghidra rows of docs/reference/development-machines.md (desktop only; the E: defaults are env-overridable via GHIDRA_INSTALL_DIR and TAOM_GHIDRA_PYTHON), docs/reviews/adopt-ghidra-hindsight-2026-09-26.md, the native_crash_triage.py decompile_hint, and the native-crash-triage SKILL.md decompile lines minus the TrollActionTrace and animation-system-map pointers. The tool keys each Ghidra project on the DLL's SHA-256 and reads Version.xml, so it should work on a v1.4.8 DLL, but no run on one is recorded (UNVERIFIED). Leave TrollBruteForce, docs/community/bannerlordmodding-lt* (including the zip), the hill-troll RCA, tools and tests, gen_troll_anim_clips.ps1, set_clip_balance_name.py, the armory-audit skill (absent on 1.4.x) and the validate_moduledata.py borrowed-body hunk (that check does not exist on 1.4.x).
  - conflicts: Main/Features/TrollBruteForce/BehaviorTreeElements/BruteForceTask.cs, Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs, Main/Features/TrollBruteForce/ITrollBruteForceService.cs, Main/Features/TrollBruteForce/TrollBruteForceConfig.cs, Main/Features/TrollBruteForce/TrollBruteForceService.cs, Main/Features/TrollBruteForce/TrollClipLog.cs, Main/Features/TrollBruteForce/TrollClipTrace.cs, TAOM.Tests/Features/BannerBearers/BannerBearerServiceTests.cs, TAOM.Tests/Features/TrollBruteForce/TrollBruteForceConfigTests.cs, TAOM.Tests/Features/TrollBruteForce/TrollBruteForceServiceTests.cs, TAOM.Tests/Features/TrollBruteForce/TrollClipLogTests.cs, tools/bind_hill_troll_action_set.py, tools/blender/export_rig_for_kit.py, tools/gen_troll_anim_clips.ps1, tools/lint_docs.py, tools/oneoff/add_hill_troll_hammer_items.py, tools/set_clip_balance_name.py, tools/tests/test_add_hill_troll_hammer_items.py, tools/tests/test_bind_hill_troll_action_set.py, tools/tests/test_check_race_morph_channels.py
- `d51e43028` 2026-09-26 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.30 - read native engine calls and strings as C
  - why: Extends the native decompiler to start from a managed [EngineMethod] call or a string, and adds a hang-dump procedure to /native-crash-triage, which the 1.4.x skill lacks and which fits the E3/E4 freezes.
  - port notes: Take native_decompile.py --engine-method/--string and its registered-address seeding, tools/native_engine_methods.py, both test files, the /native-crash-triage Phase 2b hang procedure (procdump -ma, a WinDbgX stack script, then --rva on each TaleWorlds_Native frame), and the /research, /investigate, /engine-bump, agent and research-guide wiring. Re-express for 1.4.x: the 2-engine-compat lens sentence (lens file absent on 1.4.x) into the 1.4.x /deep-review engine-compat brief, and the CLAUDE.md and AGENTS.md lines into their differently worded 1.4.x rows. Verify on v1.4.8 first: native_engine_methods.py was proven on v1.5.3 only (2,282 methods; the 'from Engine id 817 on' instruction form is a v1.5.3 observation), though it refuses an assembly with a one-sided id rather than answer. Needs capstone.
  - prereqs: 719074072
  - conflicts: tools/native_decompile.py, tools/tests/test_native_decompile.py
  - issues: 688

### group `harness-hang-triage`  P2  track=harness  commits=1  sim: clean=0 conflict=1
symptoms: battle-freeze, loading-hang
- `9c6d4962b` 2026-09-28 PORT P2 pick=conflict :: docs(skills): v2.0.30 - hang triage confirms the process is the game
  - why: Adds a first step to the /native-crash-triage hang procedure: confirm from the image path (bin\Win64_Shipping_Client) and the rgl_log PID that the hung process is the game and not the Modding Kit; a triage aid for the 1.4.8 freeze and hang reports, not a fix.
  - port notes: The native-crash-triage SKILL.md on origin/bannerlord-1.4.5 has no hang procedure at all; d51e43028 added it on 1.5.x (Spot the spin, procdump, WinDbgX, tools/native_decompile.py). Port it together with d51e43028, or if d51e43028 is not ported, add this step as a standalone hang preface. The lesson appended to docs/reviews/lessons/build-tooling-workflow.md applies as written (bookkeeping conflict expected).
  - prereqs: d51e43028

### group `engine-bump-v1.5.5-tools`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Dependencies/Foundation/CoopPresence.cs, Dependencies/Foundation/SubModuleConstructionGuard.cs, GameReferences.targets, Main/_Module/SubModule.xml, TAOM.Tests/Core/KingdomPolicyIdsTests.cs, TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/check_engine_member_refs.ps1, tools/check_handbook_attributes.py, tools/engine_body_diff.py, tools/handbook_attribute_manifest.json, tools/tests/test_check_engine_member_refs.py, tools/tests/test_check_handbook_attributes.py, tools/tests/test_engine_body_diff.py, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py, tools/validate_moduledata.py
- `4f9985852` 2026-10-09 PORT-PARTIAL P2 pick=conflict :: chore(engine): v2.0.34 - Bannerlord v1.5.5
  - why: A Bannerlord v1.5.5 engine bump (version pins, native address pins, impact doc) that also introduces two engine-generic bump tools, tools/check_engine_member_refs.ps1 and tools/engine_body_diff.py.
  - port notes: Take only the two tools and their tests at their final shape from 4e408e0e0 (unchanged since on 1.5.x), plus the /engine-bump skill steps that call them, reworded for 1.4.x. engine_body_diff.py reads docs/reference/taleworlds-api-snapshot/{patch-targets,gamemodel-bases,reflection-sites}.md, all present on 1.4.x. check_engine_member_refs.ps1 can verify a shipped TAOM.dll or TAOM.Dependencies.dll against the installed v1.4.8 engine, relevant to E3's BuildStamp MISMATCH. Leave: pinned-game-version, SubModule.xml Native constraint, v1.5.5-impact.md, README/AGENTS version text, KingdomPolicyIds/ClipBudget/native-decompile test pins, the dump path in check_handbook_attributes.py, and the SubModuleConstructionGuard trim (superseded by its deletion in 4e408e0e0).
  - conflicts: Main/_Module/SubModule.xml, TAOM.Tests/Core/KingdomPolicyIdsTests.cs, TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, tools/check_handbook_attributes.py, tools/tests/test_check_handbook_attributes.py, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py, tools/validate_moduledata.py
  - issues: 736
- `4e408e0e0` 2026-10-14 PORT-PARTIAL P2 pick=conflict :: fix(engine): v2.0.34 - v1.5.5 review fixes, dead mod guard removed
  - why: Review follow-up that hardens the two bump tools, adds two TAOM.Dependencies reflection-gate rows, makes the handbook gate report a stale manifest, and deletes SubModuleConstructionGuard, which is dead on v1.4.8 too because Module.LoadSubModules constructs every SubModule before InitializeSubModuleBases calls any OnSubModuleLoad (Module.cs:1026-1105, 141-153, 195-202), and 1.4.x installs the guard from OnSubModuleLoad (Dependencies/SubModule.cs:231).
  - port notes: Take: tool hardening (check_engine_member_refs.ps1, engine_body_diff.py, tools/tests/fixtures/build_assemblies.ps1, both test suites), check_handbook_attributes.py stale-manifest report plus manifest, the ModuleHelper.GetActiveModules and ApplicationVersion.FromParametersFile rows in ReflectionSiteBindingTests and reflection-sites.md, the build-tooling lesson, and the guard deletion (SubModuleConstructionGuard.cs, its Install call, comments in AliasStubSubModule.cs, ReflectionUtils.cs and CoopPresence.cs; reword CoopPresence's note without the v1.5.5 claim, since v1.4.8 does not catch a SubModule constructor throw). On v1.4.8 the guard is reachable only via the native LoadSingleModule callback (Module.cs:236-243), never normal play. The deletion changes TAOM.Dependencies, so ship it only with a paired Dependencies rebuild (#371). Leave: GameReferences.targets and GameReferencesTargetsTests (BUTR reference assemblies, absent on 1.4.x), SubModule.xml, v1.5.5-impact.md, native decompile test pins.
  - prereqs: 4f9985852
  - conflicts: Dependencies/Foundation/CoopPresence.cs, Dependencies/Foundation/SubModuleConstructionGuard.cs, GameReferences.targets, Main/_Module/SubModule.xml, TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs, TAOM.Tests/Migration/ReflectionSiteBindingTests.cs, tools/check_engine_member_refs.ps1, tools/check_handbook_attributes.py, tools/engine_body_diff.py, tools/handbook_attribute_manifest.json, tools/tests/test_check_engine_member_refs.py, tools/tests/test_check_handbook_attributes.py, tools/tests/test_engine_body_diff.py, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py

### group `dev-tools-tpactool-v051`  P3  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/ModuleData/armour_acquisition/armour_classes.xml, Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml, Main/_Module/ModuleData/equipmentsets/taom_player_start_vanilla_override.xml, TAOM.Tests/Features/CharacterCreation/StarterKitCoverageTests.cs, tools/armory_catalogue_overrides.tsv, tools/blender/measure_animalia_clips.py, tools/export_face_morphs.ps1, tools/gen_animalia_anim_clips.ps1, tools/gen_troll_anim_clips.ps1, tools/generate_armour_classes.py, tools/generate_career_kits.py, tools/tests/test_gen_troll_anim_clips.py, tools/tests/test_generate_armour_classes.py, tools/tests/test_wire_starter_kit_rosters.py, tools/wire_anim_master_clip.ps1, tools/wire_anim_master_skeletons.ps1, tools/wire_starter_kit_rosters.py
- `776054c0c` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: Add clip measurement JSON files for various animals and implement tests for orc and goblin locomotion binding
  - why: Mostly 1.5.x content plus a new orc/goblin locomotion tool, but it also moves the tpac PowerShell tools off the vanished TpacTool 0.4.0 folder to v0.5.1, and three of those tools on origin/bannerlord-1.4.5 still default to the 0.4.0 path.
  - port notes: Take only: the -TpacToolBin default change to E:\Release_v0.5.1\bin in tools/dump_engine_skeleton.ps1, tools/extract_human_anims_tpac.ps1 and tools/read_anim_keyframes_tpac.ps1 (all three still point at E:\Bannerlord_Art\TpacTool_0.4.0 on origin/bannerlord-1.4.5; none uses UnknownClipName or .Save(, checked by grep); the development-machines.md TpacTool bullet; the tools/README paragraph 'Which TpacTool the PowerShell tools load'; the one-line native-crash-triage SKILL.md naming note. 1.4.x already defaults tools/Audit-MeshRefs.ps1 to v0.5.1, so this makes the tools consistent. Leave everything else: the Harad starter rosters (follow 1.5.x content 08b30bfc4, not an ancestor of origin/bannerlord-1.4.5), armour_classes.xml (ArmourAcquisition, 1.5.x-only), the orc/goblin locomotion tool, doc and tests, the Animalia clip measurements, the gen_*/wire_* tools absent on 1.4.x, and the moduledata-validation.md row naming bind_orc_goblin_locomotion.py. v0.5.1 changed skeleton body positions to Vector4 (per the README paragraph), so re-run dump_engine_skeleton.ps1 once on the desktop after the switch. Desktop-only tools; low value.
  - conflicts: Main/_Module/ModuleData/armour_acquisition/armour_classes.xml, Main/_Module/ModuleData/equipmentsets/taom_char_creation_equipment.xml, Main/_Module/ModuleData/equipmentsets/taom_player_start_vanilla_override.xml, TAOM.Tests/Features/CharacterCreation/StarterKitCoverageTests.cs, tools/armory_catalogue_overrides.tsv, tools/blender/measure_animalia_clips.py, tools/export_face_morphs.ps1, tools/gen_animalia_anim_clips.ps1, tools/gen_troll_anim_clips.ps1, tools/generate_armour_classes.py, tools/generate_career_kits.py, tools/tests/test_gen_troll_anim_clips.py, tools/tests/test_generate_armour_classes.py, tools/tests/test_wire_starter_kit_rosters.py, tools/wire_anim_master_clip.ps1, tools/wire_anim_master_skeletons.ps1, tools/wire_starter_kit_rosters.py

### group `tools-api-snapshot-check`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
- `4ab005dd1` 2026-10-22 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - API snapshot check passes on the committed docs
  - why: snapshot_api_surface.ps1 -Check can never exit 0 because the committed patch-targets.md ends with the build_backlinks.py footer, and the 1.4.x script (origin/bannerlord-1.4.5 tools/snapshot_api_surface.ps1:250-258) and doc have the same defect.
  - port notes: Take the tools/snapshot_api_surface.ps1 change (strip the backlinks footer before comparing, print up to 20 differing lines on drift). Do not copy the 1.5.x patch-targets.md: its added CoopRecruitAttributionPatch row comes from 7f3636b2e, which is not on 1.4.x (git grep finds no CoopRecruitAttributionPatch on origin/bannerlord-1.4.5). Regenerate patch-targets.md on 1.4.x with the script, then confirm -Check exits 0.

### group `build-solution-dependencies`  P3  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.sln
- `1118bc2e8` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: build: v2.0.35 - solution lists the TAOM.Dependencies project
  - why: 1.4.x TAOM.sln lists only TAOM and TAOM.Tests, so Visual Studio builds TAOM.Dependencies only through TAOM.csproj's ProjectReference (origin/bannerlord-1.4.5 Main/TAOM.csproj:89).
  - port notes: Add only the Dependencies solution folder, the TAOM.Dependencies project block and its configuration rows (optionally the x86 rows for TAOM and TAOM.Tests). Leave every {59E03C61-...} TAOM.Client row. Hygiene only: the existing ProjectReference already builds Dependencies, so this does not explain E3's BuildStamp MISMATCH.
  - conflicts: TAOM.sln

### group `issue-997-native-build-match`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/native_sig_author.py
- `fc5408508` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.35 - carry native addresses across engine builds
  - why: tools/native_build_match.py carries a TaleWorlds.Native.dll RVA from an archived build to a newer one, scored against engine-method registrations; useful for native crash and hang triage on any engine line.
  - port notes: The tool imports native_crash_triage, native_decompile and native_engine_methods; only native_crash_triage.py exists on 1.4.x. Port 719074072 (native_decompile.py and the ghidra-native-decompile doc this commit edits) and d51e43028 (native_engine_methods.py) first, and check that 1.4.x's native_crash_triage exposes what the tool calls. Needs capstone. Adapt the engine-bump and native-crash-triage SKILL text: "client builds from v1.5.2 on" is 1.5.x-specific; on 1.4.x archive the v1.4.7/v1.4.8 native DLLs. The quoted oracle scores are for 1.5.x pairs; re-score on a 1.4.x pair before trusting a carry. Take native-build-match.md, the adopt-rea record, the provenance row, the testing-qa lesson and native_sig_author.py's 2-line pointer.
  - prereqs: 719074072, d51e43028
  - conflicts: tools/native_sig_author.py
  - issues: 997
- `09030511a` 2026-10-26 PORT P3 pick=conflict :: docs: v2.0.35 - native build match doc shows its issue closed
  - why: One-line issue-status fix in native-build-match.md (#997 closed).
  - port notes: Squash into the fc5408508 port by taking the doc's final state.
  - prereqs: fc5408508
  - issues: 997

### group `issue-1004-native-build-archive`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/native_archive.py, tools/test_hooks.sh, tools/tests/test_native_archive.py
- `e1dffb138` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.35 - each native engine build is archived on its own
  - why: Session-start archiving of each TaleWorlds.Native.dll build for crash triage (#1004, prompted by the missed 1.4.7 to 1.4.8 archive); the full form needs the 1.5.x native toolchain and the session-start probe budget.
  - port notes: tools/native_archive.py imports native_build_match (first added in fc5408508), native_engine_methods (d51e43028) and native_decompile (first added in 719074072). The session-start.sh hunk uses ss_probe_budget and GAME_DIR_RESOLVED, which the 1.4.x hook lacks. DEFAULT_ARCHIVE_DIR is E:/Decompiled_Bannerlord/_native, a desktop path; the laptop needs TAOM_NATIVE_ARCHIVE set. If the toolchain stays 1.5.x-only, a minimal variant still pays: a SHA-256 copy of the installed client and wEditor native DLLs, so the v1.4.8 build 119303 image survives a later Steam update for player crash triage. The line is pinned to one engine build, so the value is mostly a one-time archive.
  - prereqs: fc5408508, d51e43028, 719074072
  - conflicts: tools/test_hooks.sh
  - issues: 1004
- `2838b25c2` 2026-10-26 PORT P3 pick=conflict :: feat(tools): v2.0.35 - a new native build arrives with its matcher scored
  - why: Scores native_build_match --oracle for each newly archived native build; it only applies with e1dffb138's full form and the native matcher.
  - port notes: New-file changes to tools/native_archive.py and its test, plus engine-bump, hooks-catalog and README lines. On a line pinned to v1.4.8 there is rarely a pair of builds of one kind to score, so skip it if e1dffb138 is ported in its minimal copy-only form.
  - prereqs: e1dffb138, fc5408508
  - conflicts: tools/native_archive.py, tools/tests/test_native_archive.py
  - issues: 1004, 997

### group `issue-1038-taom-src-short-names`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
- `857734c64` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.36 - taom-src takes short names, reads the game first
  - why: It lets taom-src accept short type names and stops it reading stale engine copies in mod folders such as Modules\Coop; tools/taom-src.ps1 on 1.4.x is identical to this commit's parent, and the 1.4.x rules also write `taom-src path <Type>`.
  - port notes: tools/taom-src.ps1 applies as is; its blob equals the parent's on origin/bannerlord-1.4.5. NavalDLC and FastMode in its game-module list do no harm where those modules are absent. In tools/tests/test_taom_src_short_names.py, drop or adapt test_the_game_module_list_matches_the_body_diff_gate: it reads tools/engine_body_diff.py, which 1.4.x lacks (it came with 4f9985852, the v1.5.5 bump). Merge the engine-bump SKILL.md and research-guide lines by hand. Take the later follow-up 97219836c (cache keyed on the DLL build) with it; f39b399b4 is v1.5.6 pins only.
  - issues: 1038
- `f07d70a7c` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.36 - RCA of the taom-src and banner review
  - why: It adds an RCA addendum and one lesson from the review of taom-src short names, useful only alongside 857734c64.
  - port notes: Take the lesson 'A fast path goes in front of the path it replaces' together with 857734c64. The banner-tool findings and the RCA addendum describe 1.5.x work.
  - prereqs: 857734c64

### group `tools-taom-src`  P2  track=dev-tools  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs, tools/taom-src.ps1, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py, tools/tests/test_taom_src_short_names.py
- `97219836c` 2026-11-04 PORT P2 pick=conflict :: fix(tools): v2.0.36 - taom-src's cache follows the game build
  - why: taom-src keyed its decompile cache on Version.xml alone, so a same-version Steam hotfix keeps serving the old build's sources; 1.4.x has the identical line (tools/taom-src.ps1:132 on origin/bannerlord-1.4.5).
  - port notes: The Resolve-Version hunk applies to 1.4.x as-is. Without 857734c64 it does no harm: the 1.4.x DLL index stays shared at the cache root, and only the .cs cache moves to the stamped folder. The test hunk extends tools/tests/test_taom_src_short_names.py (absent on 1.4.x), and the engine-bump step-3 text assumes 857734c64's short-name wording. Without 857734c64, write a standalone test and reword 1.4.x .claude/skills/engine-bump/SKILL.md line 31. The first run after the port re-decompiles into a new v1.4.8-<stamp> folder.
  - prereqs: 857734c64
  - conflicts: tools/taom-src.ps1, tools/tests/test_taom_src_short_names.py
- `f39b399b4` 2026-11-04 PORT-PARTIAL P2 pick=conflict :: chore(engine): v2.0.36 - pins for the v1.5.6 hotfix, changeset 125270
  - why: Mostly v1.5.6 hotfix pins, but two engine-agnostic tool fixes apply to 1.4.x: taom-src passes the game bin as ilspycmd's reference path, so module-bin types such as OrderTroopPlacer decompile without //IL_ gaps, and decompile_bannerlord.ps1 forces UTF-8 console output.
  - port notes: Take the tools/taom-src.ps1 '-r $script:SearchDirs[0]' hunk. 1.4.x line 210 is identical, Get-SearchDirs puts $BinDir first, and all three Try-Decompile calls (lines 280, 296, 311) run after Get-SearchDirs at line 253. Take the decompile_bannerlord.ps1 UTF-8 hunk too (its insertion point after the param block is identical on 1.4.x). The OrderTroopPlacer test sits in 857734c64's AStaleIndex class. Leave the SkeletonBuffer and MissionPerf native address rows (1.5.x-only features) and the changeset pins in test_native_decompile.py and test_native_engine_methods.py (absent on 1.4.x).
  - prereqs: 857734c64
  - conflicts: TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py, tools/tests/test_taom_src_short_names.py

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 7, batch 10, batch 11, batch 15, batch 16, batch 23, batch 24, batch 26, batch 27, batch 34, batch 35
