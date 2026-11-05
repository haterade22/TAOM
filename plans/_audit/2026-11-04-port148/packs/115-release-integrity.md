# Input pack: WP 115 release-integrity

Title (draft): Make a 1.4.x release reproducible: build identity, a gated packager, paired Dependencies, a pinned TAOM_Map
Wave: 1   Lane: B   Draft priority: P2   Groups: 9   Port commits: 17
Depends on (WP ids): 107, 109

## Orchestrator notes for this WP (binding)
- Must merge before the first player release that contains any wave-1 work (#371 pairing: ship TAOM and TAOM.Dependencies from one build).
- Decision D8: the next 1.4.x release is v2.4.0; the packager and release docs must refuse v2.0.29..v2.0.36 (1.5.x tags).
- Take d99398f12's missing_armory_shaders gate hunk and 709649c3f's release pre-flight 5 hunk.
- New: config= and jitOpt= tokens in the [BuildStamp] line (public DLLs are Debug builds with the JIT optimizer off; keep shipping Debug, it is the recorded policy, but make it visible).
- New: an Armory and TAOM_Map pairing check: taom_body_length / 1.5.x Monster attributes in the Armory (decision D15: report as a pairing signal, never allowlist silently), live bandit dens (K2, 0799af376), the 15 VILLAGES rows (K2, 4b207f951), and the TAOM_Map package pin (desktop TAOM_Map after 2026-10-29 carries 1.5.x ids).
- V08: a TAOM-side guard against a mismatched TAOM.Dependencies.dll (E3 shows a MISMATCH) is needed by WP118; the BuildStamp check lives here.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/V08-load-time.md
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- RELEASE-INTEGRITY-01 [P2/high] Public TAOM.dll and TAOM.Dependencies.dll are unoptimized Debug builds (JIT optimizer disabled), invisible in player logs | port: e4c2c159a,6eb2e1b90  (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- RELEASE-INTEGRITY-02 [P2/high] BuildStamp MISMATCH is a hand-carried, 16-day-old TAOM.Dependencies module; benign now only because its sources are unchanged, and the time-only check cannot tell | port: 5d1b1bda1,db1a7166b,743ce2e1d  (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- RELEASE-INTEGRITY-03 [P2/high] Public build identity unresolvable from git: v2.0.29.5 in no commit, sorts after the 1.5.2-only v2.0.29 tag, and the 1.4.x stamp has no dirty flag | port: 057628a2b,9cedb92df,743ce2e1d,5d1b1bda1,db1a7166b,d5033e4e8,9ca99437f,6be50e8f7,c80006994  (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- RELEASE-INTEGRITY-05 [P2/high] Package hand-assembled with no packager or gate: 577 MB of PSD/PNG AssetSources, 22 MB of PDB/EXP/LIB, a stray ModuleData.zip, a Game Pass folder and retired binaries | port: 5d1b1bda1,db1a7166b,e4c2c159a,a4b5d6258,e6ee8c998  (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- RELEASE-INTEGRITY-08 [P3/medium] Public channel pairs the 1.4.8 TAOM build with an Armory carrying 1.5.x-era creature data; no Armory-to-TAOM gate on the 1.4.x line | port:   (report: plans/_audit/2026-11-04-port148/deepdive/release-integrity.md)
- XML-DATA-04 [P3/high] 'The taom_body_length attribute is not declared' (36x in E2) is harmless noise from the public Armory's 1.5.x Monster files | port:   (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)

## Port groups owned by this WP (from the commit classification)

### group `issue-481-log-engine-identity`  P2  track=diagnostics  commits=1  sim: clean=0 conflict=1
symptoms: crash
- `c80006994` 2026-09-14 PORT P2 pick=conflict :: fix(diagnostics): v2.0.28 - a log names its engine and its ending (#481)
  - why: Public 1.4.8 logs carry no [Engine] or [Shutdown] line (0 hits in E3/E4), so a log cannot say which engine it ran on or whether the game died; this adds both, keeps 30 logs instead of 10, and puts TAOM.Dependencies' diag.log in the crash bundle.
  - port notes: No 1.5.x dependency: ModuleHelper.GetActiveModules/GetModuleInfo and TAOM.Dependencies.Foundation.RuntimeLog.Path exist on origin/bannerlord-1.4.5 (Dependencies/Foundation/RuntimeLog.cs:27); FileLogger there still has DefaultRetainedLogs = 10 and BuildStampReport has no EngineReport. The commit itself says the 1.4.x back-port is owed; its archived source 586abf26 is not an ancestor of origin/bannerlord-1.4.5. Conflicts: Main/SubModule.cs placement (1.5.x sits beside Patch89), CrashBundleWriterTests and PlainTextCrashReportRendererTests merges, docs/reviews/lessons/build-tooling-workflow.md.
  - issues: 481

### group `module-manifest-dependency-order`  P3  track=release-packaging  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/SubModule.xml
- `de385cab9` 2026-09-14 PORT-PARTIAL P3 pick=conflict :: fix(module): v2.0.29 - TAOM.Dependencies listed first in both blocks
  - why: Lists TAOM.Dependencies first in both dependency blocks of Main/_Module/SubModule.xml and moves the #371 comments above the entries they describe; the 1.4.x manifest has the same misplaced pin comment.
  - port notes: Re-apply the reorder by hand on origin/bannerlord-1.4.5, keeping its pins (Native v1.4.8.*, TAOM.Dependencies v2.0.6); do not take the v1.5.2.* Native pin (the source of the code conflict). Launcher display only; BundledDependencyManifestTests must stay green.
  - conflicts: Main/_Module/SubModule.xml
  - issues: 371

### group `plan-017-build-identity`  P2  track=release-packaging  commits=9  sim: clean=2 conflict=7
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Directory.Build.props, Main/Core/Diagnostics/BuildStampReport.cs, TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs, tools/package_release.py, tools/tests/test_package_release.py
- `743ce2e1d` 2026-09-24 PORT P2 pick=conflict :: feat(crash): v2.0.30 - put the TAOM build stamp in crash bundles
  - why: Crash bundles gain the TAOM build stamp (build time plus commit SHA) in report.txt, manifest.txt and report.json, so triage no longer depends on the bundled log; CrashReport and BuildStampReport exist on both lines.
  - port notes: Hand-merge. BuildStampReport.cs conflicts only in the summary comment and the private-to-internal change on ReadInformationalVersion (the 1.5.x-only EngineReport that sits near it is not needed). IdentitySnapshot gains a last positional field; the four construction sites on the 1.4.x tip (IdentityCollector, the CrashReportService fallback, CrashBundleWriterTests, PlainTextCrashReportRendererTests) match 1.5.x. CrashBundleWriterTests conflicts against 1.5.x-only log-tail tests: add only the new build-line test. plans/README on the 1.5.x ref records maintainer decision D59 for plan 017: the 1.4.5 port is Mike's.
  - conflicts: Main/Core/Diagnostics/BuildStampReport.cs, TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs
- `057628a2b` 2026-09-25 PORT P2 pick=clean :: feat(build): v2.0.30 - mark dirty and git-less builds in the stamp
  - why: A Directory.Build.props target marks the stamped commit SHA .dirty, .nogit or nogit, so a DLL built from uncommitted edits no longer looks like a clean build of HEAD.
  - port notes: Clean pick. 1.4.x builds already get the SDK +sha suffix (E3 line 2: build.20260930-120651Z+8df90444...), so the target hooks the same SDK targets; re-check on the laptop's SDK (upstream verified on SDK 10.0.401). Follow with 9cedb92df.
- `5d1b1bda1` 2026-09-25 PORT P2 pick=clean :: feat(release): v2.0.30 - refuse to package a dirty or off-tag DLL
  - why: package_release.py --require-build refuses to package DLLs whose stamps are dirty, git-less or name a commit other than the release rev, which is the gate that catches a stale TAOM.Dependencies.dll.
  - port notes: Clean pick: package_release.py and its tests are identical on both lines before plan 017. It would have refused the public build's pair (E3 line 2: TAOM +8df90444, TAOM.Dependencies +0c323437, MISMATCH 16d apart). git diff 0c323437 8df90444 over Dependencies/ and Directory.Build.props is empty, so that mismatch is benign in source, but the gate forces both DLLs to be built at the release rev. Needs 057628a2b for the dirty flag to mean anything.
  - prereqs: 057628a2b
- `ff84e1b88` 2026-09-25 PORT P3 pick=conflict :: docs(release): v2.0.30 - build and package at the release tag
  - why: Release skill Phase 8 and release-process.md: rebuild at the tag, gate the DLLs with --require-build --dry-run, then package; crash-report.md names the new Identity field.
  - port notes: Take the skill and doc text. The CHANGELOG.md hunk follows the plan 020 decision (see ae85381dc).
  - prereqs: 5d1b1bda1
- `db1a7166b` 2026-09-25 PORT P2 pick=conflict :: fix(release): v2.0.30 - review follow-ups for plan 017
  - why: Review fixes for plan 017: the package gate failed open in four ways and trusted a missing .dirty flag; it now reads every bin copy, casefolds module names and refuses empty values, missing modules, unreadable DLLs and revs that predate the dirty-flag target.
  - port notes: Take the package_release.py and test_package_release.py changes, the BuildStampReport and test comment fixes, the CrashBundleWriterTests rename and the skill/doc text. The deep-review report, Codex prompt, RCA, REVIEW-LOG and lessons rows are optional records and conflict in bookkeeping files.
  - prereqs: 5d1b1bda1, 743ce2e1d
  - conflicts: TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs, tools/package_release.py, tools/tests/test_package_release.py
  - issues: 658
- `d5033e4e8` 2026-09-25 PORT P2 pick=conflict :: fix(release): v2.0.30 - convergence fixes for plan 017
  - why: Convergence fixes: the Phase 8 orphan-removal step no longer deletes TAOM.dll and its runtime DLLs from bin/, and the shallow-clone skip in the predates test can actually fire.
  - port notes: Doc step plus one test fix. Hosted CI is gone on both lines (1.4.x ccef172ad), but the shallow-clone skip still matters for local depth-1 clones.
  - prereqs: db1a7166b
  - conflicts: tools/tests/test_package_release.py
- `9cedb92df` 2026-09-25 PORT P2 pick=conflict :: fix(build): v2.0.30 - untracked files always mark the stamp dirty
  - why: The stamp's git status passes --untracked-files=normal so a user's status.showUntrackedFiles=no cannot hide a new source file, and GameReferences.targets joins the pathspec.
  - port notes: GameReferences.targets does not exist on 1.4.x (it arrived with plan 010, b8c000452); git status with a missing pathspec exits 0 (checked this session), so keep it for parity or drop it. The CHANGELOG hunk follows the plan 020 decision.
  - prereqs: 057628a2b
  - conflicts: Directory.Build.props
- `3c747f4fb` 2026-09-25 PORT P3 pick=conflict :: fix(release): v2.0.30 - prune only what the tag and its build own
  - why: The /release prune step keeps install-only TAOM.Dependencies assets and removes from bin/ only names neither the tag nor the build owns; the props comment adds assume-unchanged paths.
  - port notes: Recount for 1.4.x before porting the text: Dependencies/_Module/bin tracks 36 files on the 1.4.x tip against 44 at this commit (Main/_Module/bin tracks 2 on both); the 55 MCM asset files and the 12 leftovers are 1.5.x desktop measurements.
  - prereqs: d5033e4e8, 9cedb92df
  - conflicts: Directory.Build.props
- `9ca99437f` 2026-09-25 PORT P3 pick=conflict :: docs(release): v2.0.30 - compare the prune case-insensitively
  - why: Docs: the release prune must compare paths case-insensitively, since the tag spells GUI/PreFabs/ and a literal comparison deletes every live TAOM prefab.
  - port notes: The same trap holds on 1.4.x: its tip tracks Main/_Module/GUI/PreFabs/.
  - prereqs: 3c747f4fb
skipped in this group: `ffa80908a` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 017 merged

### group `plan-035-release-shader-cache-check`  P2  track=release-packaging  commits=1  sim: clean=0 conflict=1
symptoms: shader-stall
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/package_release.py, tools/tests/test_package_release.py
- `e4c2c159a` 2026-10-04 PORT-PARTIAL P2 pick=conflict :: feat(release): v2.0.32 - sack and JIT reports; module sacks left out
  - why: The release packager prints read-only reports on scene shader caches, module shader sacks and the JIT optimization state of the shipped DLLs, and stops copying module-level compressed_shader_cache.sack files.
  - port notes: Take the three reports, their tests, and the release skill and release-process notes. Do not take the module-sack exclusion as it stands. On 1.5.x it rests on decision D15 (prove a Kit-built sack works first). The 1.4.x packager has no sack handling and treats Shaders as a known top-level folder, so it copies module sacks the way 1.5.x did before this commit. E3 shows 2720 shaders pending at battle start, so run the same sack test on v1.4.8 before dropping sacks from 1.4.x releases. The packager has drifted between the lines (plan 017's 5d1b1bda1 and db1a7166b are 1.5.x-only), so expect conflicts.
  - conflicts: tools/package_release.py, tools/tests/test_package_release.py
  - issues: 448, 717

### group `engine-bump-v1.5.4`  P3  track=release-packaging  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .github/workflows/build.yml, GameReferences.targets, Main/Features/Execution/Hooks/ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.cs, Main/SubModule.cs, Main/_Module/SubModule.xml, Main/_Module/THIRD-PARTY-LICENSES.txt, TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs, TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Infrastructure/Localization/LanguageTextIntegrityTests.cs, TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs, tools/check_handbook_attributes.py, tools/handbook_attribute_manifest.json, tools/package_release.py, tools/tests/test_check_handbook_attributes.py, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py, tools/tests/test_package_release.py, tools/translation_cache/br.json, tools/translation_cache/cns.json, tools/translation_cache/cnt.json, tools/translation_cache/de.json, tools/translation_cache/fr.json, tools/translation_cache/it.json, tools/translation_cache/jp.json, tools/translation_cache/ko.json, tools/translation_cache/pl.json, tools/translation_cache/ru.json, tools/translation_cache/sp.json, tools/translation_cache/tr.json
- `9c310bc23` 2026-10-05 PORT-PARTIAL P3 pick=conflict :: chore(engine): v2.0.34 - Bannerlord v1.5.4, NativeSkinFixes removed
  - why: Mostly the Bannerlord v1.5.4 engine bump and the NativeSkinFixes removal, both 1.5.x-only; it also adds the missing licence notices for the NuGet runtime libraries TAOM ships.
  - port notes: Take only the THIRD-PARTY-LICENSES.txt notices for DryIoc 4.8.8, Newtonsoft.Json 13.0.3 and System.Runtime.CompilerServices.Unsafe. origin/bannerlord-1.4.5 references DryIoc 4.8.8 and Newtonsoft.Json 13.0.3 in Main/TAOM.csproj, but its licence file names only MinHook. Keep the MinHook notice on 1.4.x, which still ships MinHook.x64.dll for the parked NativeSkinFixes, and confirm that the Unsafe DLL ships there. Everything else (engine pins, the NativeSkinFixes removal, the packager's refusal of retired binaries, native address pins, docs) is 1.5.x-only.
  - conflicts: .github/workflows/build.yml, GameReferences.targets, Main/Features/Execution/Hooks/ExecutionCampaignBehavior_BloodFeudRelationPenalty_Patch.cs, Main/SubModule.cs, Main/_Module/SubModule.xml, Main/_Module/THIRD-PARTY-LICENSES.txt, TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs, TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs, TAOM.Tests/Infrastructure/Localization/LanguageTextIntegrityTests.cs, TAOM.Tests/Infrastructure/Localization/LocalizationKeyConsistencyTests.cs, tools/check_handbook_attributes.py, tools/handbook_attribute_manifest.json, tools/package_release.py, tools/tests/test_check_handbook_attributes.py, tools/tests/test_native_decompile.py, tools/tests/test_native_engine_methods.py, tools/tests/test_package_release.py, tools/translation_cache/br.json, tools/translation_cache/cns.json, tools/translation_cache/cnt.json
  - issues: 736
skipped in this group: `5b241c46d` SKIP-1.5-ENGINE refactor(hooks): remove redundant native DLL check command from settin

### group `yotthani-adoption-provenance`  P2  track=release-packaging  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/THIRD-PARTY-LICENSES.txt
- `eed20591a` 2026-10-09 PORT-PARTIAL P2 pick=conflict :: docs(provenance): v2.0.34 - yotthani's HoN code is MIT
  - why: Adds the MIT notice for yotthani's Heir of Numenor code; all seven source folders it names (SiegeDismount, MixedFormations, SmartCavalryAI, FiefManagement, QuickActions, EquipPresets, CompanionTactics) ship on origin/bannerlord-1.4.5, whose THIRD-PARTY-LICENSES.txt has no such section.
  - port notes: Take the HoN section of Main/_Module/THIRD-PARTY-LICENSES.txt (drop the VanillaTuning and ShaderCacheKeeper xinput9_1_0.dll wording unless those ports land), the HoN, TransferbuttonMenu and yotthani/bannerlord rows of docs/reference/provenance-register.md, and the licence lines in external-repo-adoption.md and companion-tactics.md. Leave shader-compile-notice.md and adopt-yotthani-2026-10-08.md (1.5.x-only).
  - conflicts: Main/_Module/THIRD-PARTY-LICENSES.txt
skipped in this group: `46fe58b7d` SKIP-BOOKKEEPING docs(reviews): v2.0.34 - record the fixes for two adoption findings

### group `release-packager-gates`  P2  track=release-packaging  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/package_release.py, tools/tests/test_package_release.py
- `a4b5d6258` 2026-10-18 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.34 - release packager refuses Custom Battle test troops
  - why: Makes the release packager refuse a package whose ModuleData defines a taom_test_ troop, since deploys never delete install files and a leftover test troop can ship from either line.
  - port notes: The 1.4.x package_release.py has diverged (about 540 lines of 1.5.x additions, no RETIRED_BINARIES block), so insert test_troop_files() and the main() refusal by hand; plan._copy_list exists on 1.4.x (package_release.py:198,220). The release SKILL and release-process.md hunks refer to a pre-flight 5 grep that 1.4.x lacks (it arrived with 709649c3f); add that pre-flight line too or reword. The 1.4.x repo ModuleData defines no taom_test_ troop today.
  - conflicts: tools/package_release.py, tools/tests/test_package_release.py

### group `build-drop-gamepass`  P2  track=release-packaging  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/TAOM.Client.csproj, Main/TAOM.csproj, TAOM.Tests/Infrastructure/ClientDeploymentTests.cs, tools/package_release.py, tools/tests/test_package_release.py
- `e6ee8c998` 2026-10-22 PORT-PARTIAL P2 pick=conflict :: fix(build): v2.0.35 - drop Game Pass, guard Visual Studio deploys
  - why: Stops staging and deploying the Gaming.Desktop.x64_Shipping_Client (Game Pass) bin and fails a deploy that would ship a stale one; the 1.4.x project files still stage it (Main/TAOM.csproj:148-153, Dependencies/TAOM.Dependencies.csproj:76-80 on origin/bannerlord-1.4.5).
  - port notes: First confirm the maintainer's 2026-10-22 'Game Pass is not supported' decision covers the public 1.4.8 build. Take: the Gaming.Desktop MakeDir/Copy removal and the FailOnGamePassStagingInModule target in Main/TAOM.csproj and Dependencies/TAOM.Dependencies.csproj, and the Game Pass lines in release-process.md, module-taom.md, modules-overview.md, module-dependencies.md and the release SKILL.md. Leave the TAOM.Client Visual Studio core-match guard (Client/TAOM.Client.csproj) and ClientDeploymentTests. The package_release.py half (refuse a channel folder holding Gaming.Desktop; read the DLL list from the release commit's SubModule.xml) builds on e0a6970dd's client-folder checks, and the 1.4.x gate has no Game Pass or manifest handling at all, so re-implement those two checks on the 1.4.x gate instead of cherry-picking.
  - conflicts: Client/TAOM.Client.csproj, Main/TAOM.csproj, TAOM.Tests/Infrastructure/ClientDeploymentTests.cs, tools/package_release.py, tools/tests/test_package_release.py
  - issues: 968, 969

### group `issue-1010-ingame-ledger`  P3  track=release-packaging  commits=1  sim: clean=0 conflict=1
- `715530875` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: feat(tools): v2.0.35 - each release lists its owed in-game checks
  - why: Adds tools/ingame_ledger.py and /release steps listing closed issues still labelled triage-needs-ingame; it selects by date in a GitHub repo both lines share, so 1.4.x needs a branch filter.
  - port notes: The tool counts an issue when its closedAt is after the previous tag's committer date, so on 1.4.x it would also list 1.5.x-only issues. Add a filter keeping only issues referenced by commits in <previous tag>..HEAD. The 1.4.x .claude/skills/release/SKILL.md and docs/reference/release-process.md differ from the 1.5.x parent by about 150 and 166 lines and still use the hand-edited CHANGELOG, so insert the pre-flight count and the testers' checklist step by hand. Bring the Agent Reach provenance row and the review record.
  - issues: 1010

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 9, batch 13, batch 15, batch 19, batch 23, batch 27
