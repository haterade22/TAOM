# Input pack: WP 142 docs-process-sync

Title (draft): Sync process docs, repo hygiene and MCP pins; then union-merge the lessons files
Wave: 5   Lane: G   Draft priority: P2   Groups: 19   Port commits: 37
Depends on (WP ids): 141

## Orchestrator notes for this WP (binding)
- Takes every remaining docs-process group (see the pack). Rewrite 1.5.x facts for 1.4.x; never import 1.5.x engine claims.
- Decision D18: 4b7e36250's merge=union line for docs/reviews/lessons/*.md is the LAST step, after the lessons files are synced; never on CHANGELOG.md.
- K1: 50e8dc7ae: skip its settlement-economy-food-prosperity.md hunk; the 1.4.x text at :149-153 is correct (TaomPartyHealingModel overrides only GetSurvivalChance and GetDailyHealingHpForHeroes).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `readme-install-requirements`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `c83352298` 2026-09-14 PORT-PARTIAL P3 pick=conflict :: docs(readme): v2.0.29 - players need no separate Harmony or MCM
  - why: The 1.4.x README still lists Harmony and MCM as separate requirements although TAOM.Dependencies bundles Harmony, ButterLib, UIExtenderEx and MCM on both lines.
  - port notes: Take the README 'Installing to Play' rewrite, but state Bannerlord v1.4.8 instead of v1.5.2 (beta) and drop 'all at the same version': on origin/bannerlord-1.4.5 TAOM.Dependencies is v2.0.6 while TAOM is v2.0.28. The TAOM_Map/Armory manifest pin edits the body mentions happened outside the repo.

### group `plans-006-019-opus-audit`  P3  track=docs-process  commits=1  sim: clean=1 conflict=0
- `7f02fc8d3` 2026-09-23 PORT-PARTIAL P3 pick=clean :: docs(plans): v2.0.30 - Opus review sprint report and plans 006-019
  - why: Adds /improve plans 006-019 and the 2026-09-23 Opus audit record; no source change, but several plans target defects the 1.4.x line shares.
  - port notes: Do not cherry-pick as is: each plan is an executor spec for the desktop (E:\repos worktrees, drift checks against b2e387db7 on bannerlord-1.5.x). Use the plans as specs when porting their implementation commits and rewrite the README rows for 1.4.x status (001-003 and 005 TODO, 004 DONE there). Direct leads: 006/007 (the 247 patched callback shims behind E3's 'attached 247 Finalizer(s)'), 009 (64 unguarded PatchCategory calls that can fail boot or loop a failed load), 012 (loading-window trace walks a stack every frame), 015 (warg tick costs), 017 (build identity, E3's BuildStamp MISMATCH). Plan 010 (hosted Windows CI) runs against the 2026-10-05 CI removal on both lines (ccef172ad). The _audit record is reference only.

### group `plan-022-oob-auto-assign`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `66e3fd593` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: docs(tactics): v2.0.30 - document OOB Auto-Assign
  - why: Mostly Auto-Assign feature docs, but it also adds a CompanionTactics row that is missing from docs/reference/feature-map.md on 1.4.x too.
  - port notes: Take only the feature-map.md CompanionTactics row, rewritten without the 'Assign Heroes (role-matched captains ...)' clause, because 1.4.x still ships the stub. Optionally correct companion-tactics.md:157 on 1.4.x, which says TroopStanceManagerTests has 8 tests while the file has 9 test attributes. Leave out the Auto-Assign subsection and the CHANGELOG entry.
skipped in this group: `d1b2c44c1` SKIP-FEATURE feat(tactics): v2.0.30 - wire OOB Auto-Assign to HeroAutoAssigner; `daeb127e5` SKIP-FEATURE fix(tactics): v2.0.30 - review follow-ups for plan 022; `1b21a2145` SKIP-FEATURE fix(tactics): v2.0.30 - convergence fixes for plan 022; `0192a858a` SKIP-BOOKKEEPING docs(reviews): v2.0.30 - Codex prompt for plan 022

### group `plan-016-repo-hygiene-mcp-pins`  P2  track=docs-process  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .mcp.json
- `470ee64e9` 2026-09-25 PORT P2 pick=conflict :: chore(security): v2.0.30 - untrack local files and pin MCP servers
  - why: 1.4.5 still tracks .claude/settings.local.json (which holds the only MCP write-tool denies, machine paths and the MCP trust list), _taom_loc.pkl and crashz/, and runs serena, server-filesystem, mcp-server-git and elevenlabs-mcp unpinned; this untracks those files, moves the nine denies into the tracked settings.json and pins the servers.
  - port notes: On 1.4.x, repoint the crashz/report.json citations (engine-bump SKILL.md, native-crash-triage SKILL.md, docs/migration/v1.4.8-impact.md and the .gitignore comment) to a 1.4.x commit that still tracks the file, for example 1b00d1acd. Do not use b2e387db: it is a 1.5.x commit and not an ancestor of origin/bannerlord-1.4.5.

The merge deletes settings.local.json from each checkout's disk, so copy it aside first.

7d13adc36 replaces the Serena pin 949a27ef with 7a296833. The native-crash-triage SKILL.md text differs on 1.4.x (the 1.5.x text names v1.5.2), so expect hand-merges there and in .gitignore.
- `8a6388311` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: docs(readme): v2.0.30 - drop stale counts, fix version and test command
  - why: The README drops stale hand-kept counts and uses the non-deploying test command, and the Build and Test rows of agent-operating-manual.md gain -p:ModuleId=, which is still missing on 1.4.5 (lines 41-42) even though .ai/verification.md and CLAUDE.md there already carry it.
  - port notes: Take the agent-operating-manual.md rows, the README count removals and the 'dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=' line. Leave every v1.5.3 and bannerlord-1.5.x line: the 1.4.x README must keep v1.4.8 and bannerlord-1.4.5.
- `1ce8c370e` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: fix(docs): v2.0.30 - review follow-ups for plan 016
  - why: Plan 016 review follow-ups: pins in the .vscode MCP example and the kingdom-voices snippet, the pin and deny-list rule in mcp-servers.md, per-checkout settings.local.json guidance, ModuleData MCP activation wording, the agent-manual Build row and three tooling lessons.
  - port notes: Leave the README line that clones onto bannerlord-1.5.x, the CHANGELOG moves, and the development-machines.md sentence saying bannerlord-1.4.5 still tracks settings.local.json (false once 470ee64e9 lands there). The restore note must name a 1.4.x commit, not b2e387db. The deep-review-016 report, RCA and Codex prompt are optional records; the lessons are worth keeping.
  - prereqs: 470ee64e9, 8a6388311
  - issues: 657
- `35f212c60` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: fix(docs): v2.0.30 - convergence fixes for plan 016
  - why: Convergence wording fixes for plan 016: the deny-list derivation is scoped to the pinned git and filesystem servers, user-level servers are noted as unpinned, and INDEX.md names .codex/config.toml.
  - port notes: Recompute the LESSONS-LEARNED counts on 1.4.x instead of copying 202 and 924. 7d13adc36 supersedes the sentence that Serena's editing tools are not denied yet.
  - prereqs: 1ce8c370e
- `7d13adc36` 2026-09-25 PORT P2 pick=conflict :: chore(mcp): v2.0.30 - newest Serena pin, deny its edit tools
  - why: Serena's 16 editing tools (including execute_shell_command) bypass every Edit, Write and Bash hook, and on 1.4.x Serena is enabled in the tracked settings.local.json with nothing denying them, so this adds them to the deny list and pins Serena to 7a296833.
  - port notes: 9cfcadd07 (2026-11-03) later removes Serena and these deny rules from 1.5.x. If that removal is ported to 1.4.x, skip this commit. The pin needs a live check on the 1.4.x machine (start-mcp-server --help with --context and --project) after a restart.
  - prereqs: 470ee64e9
  - conflicts: .mcp.json
skipped in this group: `4d912ee5f` SKIP-BOOKKEEPING docs(plans): v2.0.30 - plan 016 merged, decisions 57 to 61

### group `issue-696-keyforce-art`  P3  track=docs-process  commits=2  sim: clean=1 conflict=1
- `0aad0d941` 2026-09-30 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.32 - RCA and lessons for the KEYforce art wiring
  - why: The RCA describes out-of-scope content wiring, but its lesson entries are general: an unexplained crash is not a standing rule, a Visual Studio launch deploys the working tree, a subagent's issue number is a claim to check, backups belong in the quarantine, and role-based consumers all read a troop that joins a shared tree.
  - port notes: Take the entries added to docs/reviews/lessons/animation-skeleton.md, build-tooling-workflow.md and data-content-cultures.md (all three exist on 1.4.x), and reword or drop their links into the RCA. Leave docs/reviews/rca-keyforce-art-wiring-2026-09-29.md.
  - issues: 696
- `df273ea24` 2026-09-30 PORT P3 pick=clean :: docs(rules): v2.0.32 - model the deserializer when reading troop kit
  - why: Adds a moduledata-validation.md section telling tools to model BasicCharacterObject.Deserialize when they read troop kit. The claim holds on v1.4.8, where MBEquipmentRoster.AddOverriddenEquipments lays each troop-level <equipment> over every set.
  - port notes: The commit applies cleanly (pick=clean). Its link targets the lesson entry 'Troop-level <equipment> overrides beat roster slots', which is not in docs/reviews/lessons/xslt-moduledata.md on 1.4.x: port that entry or repoint the link. Verified in the v1.4.8 dump: BasicCharacterObject.cs:360-412 and MBEquipmentRoster.AddOverriddenEquipments.
  - issues: 641, 696
skipped in this group: `98ca32e0c` SKIP-CONTENT feat(creatures): v2.0.32 - KEYforce's elephant and spider art (#696); `1d8fc1d8e` SKIP-1.5-ONLY-FIX feat(culture-conversion): v2.0.32 - no creature rider replacements

### group `docs-release-process`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `62157c7e7` 2026-10-05 PORT-PARTIAL P3 pick=conflict :: docs(release): v2.0.34 - channel numbers, version bumps, hook gap
  - why: Release process docs: read every channel's version before picking a number, move the Dependencies version with each release, resolve crash reports that quote versions git never saw, and record the commit-subject hook's worktree gap.
  - port notes: Take the release SKILL.md Phase 1 channel check, the release-process.md additions that are not specific to 1.5.x, the hooks-catalog note and the lessons. The hooks-catalog note matters for the port programme: origin/bannerlord-1.4.5's check-commit-subject-version.sh also cds to CLAUDE_PROJECT_DIR and reads that checkout's SubModule.xml, so a commit run in a worktree on another line is judged against the main checkout's version. Skip .claude/agents/deep-reviewer.md and docs/ai-includes/git-and-commits.md (both absent on 1.4.x) and the v2.0.34 INDEX row.

### group `issue-765-review-records`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `0b08eeeab` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - RCAs for the chronicle events and the Eye of Sauron
  - why: Review records for two 1.5.x-only features, but its four lessons are generic and one carries an engine fact true on v1.4.8 (PartyBase.PartySizeLimit caches against MemberRoster.VersionNo, PartyBase.cs:343-355).
  - port notes: Take only the four lesson entries in docs/reviews/lessons/{adapters-taleworlds-api,campaign-mechanics,state-lifecycle-save}.md, re-sourced, because the cited RCAs and commits do not exist on 1.4.x. Leave both RCAs and the REVIEW-LOG rows. Optional; fold into any lessons sweep.
  - issues: 765
skipped in this group: `b8988815a` SKIP-BOOKKEEPING docs(reviews): v2.0.34 - account for every finding in the two RCAs

### group `issue-792-docs-audit`  P3  track=docs-process  commits=3  sim: clean=0 conflict=3
- `2e2a422dc` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(war): v2.0.34 - War Chronicle and Eye of Sauron docs current
  - why: Mostly War Chronicle and Eye of Sauron doc updates, but its dev-console.md pass adds rows and naming exceptions for commands that also ship on 1.4.x and have no doc row there.
  - port notes: Take only dev-console.md rows for commands present and undocumented on 1.4.x: taom.print_ai_food_relief (Main/Features/AiPartySize/Cheats/AiPartySizeCheats.cs), taom.fc_status and taom.fc_grant_merit (Main/Features/FieldCommission/Cheats/FieldCommissionCheats.cs), plus the naming-table 'shipped exceptions' wording restricted to commands that exist on 1.4.x (time_status, service_status, fc_status, fc_grant_merit, nudge_race_offset and the save_/reload_/rescue_ verbs; verify each against 1.4.x code). Leave taom.tactic_status (CultureDoctrine #608, 1.5.x-only), chronicle_*, war_effect_add, realm_*, the eye-hunt rows, war-chronicle.md, eye-of-sauron.md, and the feature-map and gamemodel-registry rows.
  - issues: 765, 792, 793
- `86d3a774e` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(coop): v2.0.34 - chronicle gates, hunt under Together, counts
  - why: Mostly co-op docs for 1.5.x-only features (Eye of Sauron, War Chronicle) and the v1.5.5 pin, but it corrects co-op doc facts that are also wrong on 1.4.x.
  - port notes: Take: coop-interop.md:248 on 1.4.x says the siege-defence split is 'Pinned by three tests', but 1.4.x CoopAuthorityGateTests.cs has exactly the two named SiegeDefense tests (:137, :148), so use the two-test wording. Replace the hard-coded '7 transpilers' (1.4.x coop-interop.md:449 and bannerlord-together-compat.md:124) with the grep command and a fresh 1.4.x count; recount the veto classes (1.4.x bannerlord-together-compat.md:253 says 32) rather than copying 61. Take the WarOfTheRingBehavior row's reconcile wording only with a port of d1b9fd91a. Leave the Eye of Sauron and War Chronicle rows, the v1.5.5 pin row, and the refuge.md #773 note (it cites the 9ab3952a7 rule, absent on 1.4.x).
  - issues: 765, 773, 792
- `1a2da1e3e` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(features): v2.0.34 - volunteers, Arthedain, catalogues current
  - why: Mostly War Chronicle and Arthedain doc rows, but it fixes four shared docs that are stale on 1.4.x as well.
  - port notes: Take: module-dependencies.md's rewrite saying the #371 pairing rows are back (95085de30 is on 1.4.x, yet 1.4.x module-dependencies.md:156-157 still says the field is missing), re-measuring every SubModule.xml line cite against the 1.4.x manifest (#371 comment at :15-22, the DependedModule at :23). troop-progression.md's four TaomVolunteerModel overrides (1.4.x TaomVolunteerModel.cs:33, 45, 55, 79), without the VolunteerProductionService rows (absent on 1.4.x). castle-recruitment.md's CastleRecruitmentConfigProviderTests count of 9 (1.4.x has 9 test methods), without the War Chronicle line. The tools/README.md row for check_external_loc_coverage.py (the tool exists on 1.4.x, the README has no row). Leave arthedain.md, the war_chronicle.json and rally.json catalogue rows and the wotr string count.
  - issues: 371, 754, 765, 792
skipped in this group: `f479d96f3` SKIP-BOOKKEEPING docs(reviews): v2.0.34 - #792 RCA, review log and track record

### group `harad-keyforce-port`  P3  track=docs-process  commits=2  sim: clean=0 conflict=2
- `dde30a5b7` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - KEYforce Harad port RCA and lesson
  - why: The RCA describes a 1.5.x Harad content port, but its lesson (diff the validator's whole warning set against HEAD before a troop commit, because the hook surfaces only errors) applies to 1.4.x troop edits such as the #697 public-Armory troop tree.
  - port notes: Take only the new entry in docs/reviews/lessons/data-content-cultures.md (bookkeeping hot spot, hand-append); its commands exist on 1.4.x (generate_enlistment_rosters.py --culture, origin/bannerlord-1.4.5 line 800). Leave docs/reviews/rca-keyforce-harad-port-2026-10-15.md (content port record).
  - issues: 805, 813, 815
- `42693d9bd` 2026-10-16 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - KEYforce javelins port RCA and lessons
  - why: RCA of a 1.5.x content port, but its lessons on proving a baseline run read the baseline and on who owns the region inside a TAOM tool's marker block are general and pair with the 573 starter-kit guard.
  - port notes: Take the data-content-cultures.md and xslt-moduledata.md lessons except the Erebor dwarf-dress one; skip the RCA file.
  - prereqs: a9f2d7eda
  - issues: 812
skipped in this group: `d94ee1631` SKIP-CONTENT feat(harad): v2.0.34 - KEYforce's javelins, maces, bardings, Custom Ba; `f6b5bc7fe` SKIP-CONTENT feat(harad): v2.0.34 - KEYforce's javelins, maces and bardings install

### group `yotthani-review-806-809`  P3  track=docs-process  commits=2  sim: clean=0 conflict=2
- `33253ea9d` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - yotthani review code RCA and lessons
  - why: Review record of #806 to #809 whose two lessons (a patch category fails as one unit; a native pin table must fail the gate on an unlisted build) matter on 1.4.x if those ports land.
  - port notes: Take the harmony-il.md and native-cpp-port.md lessons, rewording PatchCategoryIndex and TryPatchCategory (absent on 1.4.x, which calls Harmony.PatchCategory directly); take the RCA only alongside the ported code; skip the rca-engine-bump-v1.5.5 F10 edit (a 1.5.x engine-bump record).
  - prereqs: 15e52d7af, 85c608963, 0273e2ea4
  - issues: 806, 809
- `b3fac3021` 2026-10-15 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.34 - second review of the yotthani code items
  - why: Second review of #806 to #809; adds two general lessons (an empty patch category still returns true; read a reference doc's tag legend before adding a figure or address).
  - port notes: Take the lessons edits; skip the Codex prompt record; the RCA section only if the RCA is ported.
  - prereqs: 33253ea9d
  - issues: 806, 809

### group `docs-lessons-union-merge`  P2  track=docs-process  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .gitattributes
- `4b7e36250` 2026-10-20 PORT P2 pick=conflict :: docs(clans): v2.0.35 - correct renown bands; union-merge lessons
  - why: Adds merge=union for the append-only docs/reviews/lessons/*.md (absent on 1.4.x, and a listed conflict hot spot for this port) and corrects the clan renown band, which v1.4.8 DefaultClanTierModel.cs:43-49 confirms (max = next - (next - floor) * 0.4, so 60 percent of the band).
  - port notes: The .gitattributes conflict is context only: the preceding Shaders/Sources line is 1.5.x-only (ShaderSourceInstall), so append the union block alone. In docs/modding/clans.md keep the v1.4.8 line refs (DefaultClanTierModel.cs:11, 43-49 in the v1.4.8 dump) instead of the 1.5.x text's 44-50. Land this on 1.4.x before the bulk cherry-picks: cherry-pick reads the target branch's .gitattributes, so lessons-file conflicts then resolve automatically (reread entries where both sides edited the same existing line).
  - conflicts: .gitattributes

### group `docs-completion-workflow-issue-body`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, tools/promote_borrowed_cultures.py
- `4e7efe5a8` 2026-10-20 PORT-PARTIAL P3 pick=conflict :: fix(conversations): v2.0.35 - TAOM towns get a conversation backdrop
  - why: HallOfFire is a new feature, but the commit also adds a generic completion-workflow step (reread the issue body against git diff --stat and the untracked files before committing) that the 1.4.x completion-workflow.md lacks.
  - port notes: Take only the paragraph added to docs/ai-includes/completion-workflow.md. Leave the HallOfFire feature, its tests, the SandBox.View reference in TAOM.Tests.csproj, the Main/Composition line and the lessons.
  - conflicts: Main/Composition/FeatureModules.cs, tools/promote_borrowed_cultures.py
  - issues: 826

### group `docs-lessons-git-index-hygiene`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `fd2072a0a` 2026-10-22 PORT-PARTIAL P3 pick=conflict :: docs(lessons): v2.0.35 - commit one path when the index holds more
  - why: Adds a line-agnostic lesson (commit a single path with git commit -- <path> when the index holds other staged changes) that fits the 1.4.x multi-session git rules.
  - port notes: Take only the new section 'Commit a single path with git commit -- <path> when anything else is staged' in docs/reviews/lessons/build-tooling-workflow.md, citing 0a8d61a4e and 10f1f0b80 as 1.5.x commits (its RCA link, rca-taom-client-b4a-2026-10-22.md, is not on 1.4.x). Leave the repoint of the FactionMap client-composition lesson, which is ADR-013-only.

### group `docs-adr-001-json-configs`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `dc275dc4e` 2026-10-23 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.35 - ADR-001 allows JSON feature configs; B04 statuses
  - why: ADR-001 still says no JSON on 1.4.x while origin/bannerlord-1.4.5 tracks 66 .json files beside 286 .xml under Main/_Module/ModuleData, so the amendment that records actual practice applies there too.
  - port notes: Take the ADR-001 amendment and the two docs/adrs/README.md lines, recounted for 1.4.x (66 json / 286 xml, not 93/420). The cited 'Config Providers MUST Validate' rule exists on 1.4.x (.claude/rules/csharp-architecture.md:71), but the ObjectCreationHandling.Replace lesson in docs/reviews/lessons/state-lifecycle-save.md does not (added on 1.5.x by bc01a545c, the Hold the Walls RCA): state the rule inline or port that paragraph. README conflicts on the ADR-003 row (1.5.x amended it 2026-10-21). Leave the four B04 status edits (fire-and-tribute, hold-the-walls, unlooked-for-aid and watchful-holds docs are 1.5.x-only).
  - issues: 763

### group `engine-bump-v1.5.6`  P3  track=docs-process  commits=2  sim: clean=0 conflict=2
- `04c3c4540` 2026-10-27 PORT-PARTIAL P3 pick=conflict :: docs(engine): v2.0.36 - v1.5.6 record after the release, tool rows
  - why: Post-release v1.5.6 documentation that also carries a few facts true on 1.4.x: a GitHub closing keyword closes only the number after it, taom-src needs a fully qualified type name, and the API snapshot README should not hard-code counts (1.4.x still states 220 and 46).
  - port notes: Take the closing-keyword note in docs/ai-includes/completion-workflow.md (absent on 1.4.x), the fully-qualified-name note in .claude/skills/engine-bump/SKILL.md and the taom-src.ps1 row in tools/README.md, the count fixes in docs/reference/taleworlds-api-snapshot/README.md (1.4.x lines 19, 41, 42), and the hooks-catalog note about the ModuleData commit hook reading live module data once the 1.4.x validator's live reads are confirmed. Optional: the 'packages and release folders are Mike's' rule, re-homed because the 1.4.x /release skill has seven phases and no package items 6/6a or Phase 8, and without TAOM.Client.dll. Leave v1.5.6-impact.md, TRACKING, the v2.0.36 changelog lines in feature docs, the coop-interop v1.5.6 block and the tools/README rows for engine_body_diff.py, check_engine_member_refs.ps1 and native_archive.py (none exist on 1.4.x).
  - issues: 1026, 1038
- `370b4bf2b` 2026-10-28 PORT-PARTIAL P3 pick=conflict :: docs(skills): v2.0.36 - engine bump writes its Gates after the last run
  - why: Adds an evidence rule to /engine-bump: write the impact record's Gates section from the final /verify output, after the commit, in the past tense, naming the commit behind each number.
  - port notes: Take the Gates rule only and re-home it in the 1.4.x skill's Phase 4 or 5 (that skill is shorter and has no item list ending at 9 to extend). Leave the sentence about running the no-game commands, which needs plan 010's RefAsm mode.
  - issues: 1038
skipped in this group: `12b010626` SKIP-1.5-ENGINE fix(engine): v2.0.35 - TAOM builds and runs on Bannerlord v1.5.6; `3cdffd8a2` SKIP-1.5-ENGINE docs(engine): v2.0.36 - yotthani and MithrilForge code holds on v1.5.6; `c03ed426d` SKIP-1.5-ENGINE chore(build): v2.0.36 - BUTR v1.5.6 reference assemblies

### group `process-outside-pr-review`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `d08637c73` 2026-10-29 PORT-PARTIAL P3 pick=conflict :: docs(contributing): v2.0.36 - open the repo to outside pull requests
  - why: Opens the repo to outside PRs with CONTRIBUTING.md, a PR template and a security-first review skill; the skill is harness, and outside PRs already reach bannerlord-1.4.5 (PR #1064).
  - port notes: Port CONTRIBUTING.md and .claude/skills/review-outside-pr/SKILL.md, adapted to a 1.4.x checkout. On a 1.4.x checkout a bannerlord-1.4.5 PR can be built and tested (dotnet build/test with -p:DisableModuleCopy=true) and taom-src reads the installed 1.4.8 DLLs, whereas the skill's Phase 0 skips /verify because the desktop runs 1.5.x. Drop the line 'we still port nothing there ourselves', which this port programme contradicts, and replace the E:\ desktop paths. The PR template and README line only take effect on the GitHub default branch. A new skill on 1.4.x needs its CLAUDE.md routing row and a hand-edited CHANGELOG entry (hook-enforced), and the subject must carry v2.0.28.

### group `docs-stale-path-sweep-2026-11-03`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `bb7da8ff4` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct stale paths in the living docs
  - why: The doc paths it corrects are also wrong on the 1.4.x tip, but 18 of its 19 target docs changed on 1.5.x, so the edits must be redone by hand rather than cherry-picked.
  - port notes: Verified on origin/bannerlord-1.4.5: the folder is Main/_Module/GUI/PreFabs, yet 46 doc citations spell it Prefabs. The file is taom_spkingdoms.xml, and other casings appear in the docs. tools/oneoff/ exists (69 entries), as does ModuleData/equipmentsets/. docs/features/race-age-system.md:100 lists Main/Features/RaceAge/Hooks/GetCivilianEquipment_Patch.cs, which is in no 1.4.x tree. Redo the same substitutions across the 1.4.x docs, but skip docs/archive and changelog-archive, which are history. Leave faction-ui.md (absent on 1.4.x) and any path that is already right on 1.4.x. No code is involved.

### group `docs-feature-doc-truth-pass-2026-11-03`  P3  track=docs-process  commits=4  sim: clean=0 conflict=4
- `2e34cade1` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct feature docs that fell behind their code
  - why: It corrects feature docs against 1.5.x code; two of its hunks are also true on 1.4.x, and the rest describe changes made only on 1.5.x.
  - port notes: Take the test counts in lord-party-templates.md: ShippedLordPartyTemplateTests has 14 and Patch88LordPartyTemplateTests 11, both counted on origin/bannerlord-1.4.5. Take the SubModule.cs citation in map-event-guard.md, but use the 1.4.x line: on the 1.4.x tip the 'after Sandbox registered its behaviors' comment is at SubModule.cs:714, not 1.5.x's :926. Leave:
- elephant.md: the 1.3x model (f8b3c0ea0) and harad_howdah_crew are not on 1.4.x
- mumakil.md: the Fords copy
- many-hammers.md and namegiving.md: absent on 1.4.x
- `20616aad3` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct 23 feature docs against their code
  - why: It is the second wave of feature-doc corrections; four hunks hold on 1.4.x, and most of the others describe 1.5.x code or content.
  - port notes: Verified on origin/bannerlord-1.4.5:
- return-to-army.md: the log string 'or place it at the gate' is at Patch87_ReturnToArmy.cs:100, and the doc is identical to the 1.5.x parent
- naval-travel.md: enabled defaults to false in naval_travel_config.json and NavalTravelConfig.cs:16
- time-acceleration.md: TaomTimeControlHotKeyCategoryTests.cs has 11 tests
- module-armory.md: the lower-case texture rule is a 2026-09-13 maintainer decision
Leave:
- battle-scenes.md: TryPatchCategory does not exist in the 1.4.x SubModule.cs
- shader-precompilation.md: the TAOM.Client wiring test
- the localization-override counts, the main-menu-customizer Fords entry and the new culture lists
- the 11 docs absent on 1.4.x
The remaining hunks need a fact-by-fact check.
- `8ce80c2ff` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct 22 more feature docs against their code
  - why: It is the third wave of feature-doc corrections; the settlement-food, caravan-trade and army-targeting corrections are true on 1.4.x, while the realm additions are 1.5.x content.
  - port notes: - settlement-food.md applies. The doc is identical to the 1.5.x parent on 1.4.x, and bee07b487, which removed the count-getter patch, is on 1.4.x. Keep PartyBase.cs:381, the line in the v1.4.8 dump, instead of :394.
- caravan-trade.md applies. It is identical on 1.4.x, and ResolveSide exists in the 1.4.x CaravanTradeService.cs. Recount the five IoC registrations.
- army-targeting.md: the 1.4.x doc still describes the reach falloff that was removed on 2026-08-22 (lines 42, 186 and 205), and IsWithinBorderRescueRange exists there. Redo the correction by hand, since 8 commits changed the doc on 1.5.x.
Leave settlement-nameplate-fade (TryPatchCategory), the realms added on 2026-10-29, the lotr-issues counts and the docs absent on 1.4.x.
- `28b8f6282` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct 40 more feature docs against their code
  - why: It is the fourth wave of feature-doc corrections; its War of the Ring momentum config rows are true on 1.4.x, which helps investigate the early-end report, and most other hunks apply only to 1.5.x.
  - port notes: Take the war-of-the-ring-momentum.md rows armyMomentum 25 (c3dd7f109) and killMomentumPerHundred 40 (683799804). Both commits are on 1.4.x, and the 1.4.x momentum.json carries 25 and 40. Leave:
- the 179-test count
- battle-load-diagnostics.md: the Screen Performance FastOpen settings are 1.5.x
- crash-report.md: the Fords swallow event
- blow-diagnostics.md: server-settings
- skip-campaign-intro.md: TryPatchCategory
- character-selection.md: [ClientOnly] is not on 1.4.x
- the Neutral realm and Feral Orc lists, harmony-patch-registry.md, and the 12 docs absent on 1.4.x
The remaining hunks need a fact-by-fact check.

### group `docs-truth-pass-2026-11`  P3  track=docs-process  commits=6  sim: clean=0 conflict=6
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/check_external_xslt.py
- `3b1af2777` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct 15 feature and modding docs against their code
  - why: Doc-truth corrections measured against 1.5.x code, a few of which also hold on 1.4.x: origin/bannerlord-1.4.5 docs/features/cultural-feats.md still says 30 and 77 feats at lines 23, 30 and 222 while Main/Features/CulturalFeats/TaomCulturalFeats.cs holds 130 unique taom_ ids on both lines.
  - port notes: Hand-apply, never cherry-pick: all 13 touched files that exist on 1.4.x already differ from this commit's 1.5.x parent. Take the cultural-feats.md count fixes (verified). Re-measure every other correction against origin/bannerlord-1.4.5 before taking it. Leave module-dependencies.md (1.5.x csproj line cites, 44 vendored files through ButterLib 1.5.1, v2.0.36 text), creature-siege-role.md and taom-buildings.md (absent on 1.4.x), and any count driven by 1.5.x content.
- `ff1fe1933` 2026-11-04 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct 26 modding and feature docs against their code
  - why: Modding-guide truth pass that is mostly 1.5.x counts, but some fixes hold on 1.4.x: origin/bannerlord-1.4.5 docs/features/localization.md:5 still calls the other languages community-contributed overrides, while all 12 are AI first drafts on 1.4.x too.
  - port notes: Hand-apply only: the 23 touched files on 1.4.x all differ from the 1.5.x parent. Take the localization.md 'AI first draft' sentence. Re-measure the string, key and file counts on 1.4.x: 501, 2,545 and 23 files are 1.5.x numbers, and 1.4.x line 9 still says 1,773. Leave animalia-elk-moose.md and xml-merge-fast-path.md (absent on 1.4.x), and the culture, kingdom and settlement counts in the modding guides unless they re-measure true on 1.4.x.
- `50e8dc7ae` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct reference docs that later commits made false
  - why: Reference-doc pass against later 1.5.x commits whose engine-study hunks swap dead memory citations for lesson links (the 1.4.x engine docs carry 71 feedback_ tokens) and fix an engine claim that is wrong on v1.4.8 too (Equipment.FillFrom copies all 12 slots, empties included: Equipment.cs:184-194 in the v1.4.8 dump).
  - port notes: Take the docs/reference/engine hunks. Four of those files are byte-identical on 1.4.x (campaign-object-graph, campaign-to-mission-bridge, item-equipment-model, scene-gameentity-scriptcomponent), and the 7 lesson headings the new links name all exist under docs/reviews/lessons on origin/bannerlord-1.4.5. Re-measure TAOM pointers before keeping them: SubModule.cs:1285, :2183, :2186 and :2197 are 1.5.x lines. .claude/rules/config-providers.md does not exist on 1.4.x, so that hunk keeps the csharp-architecture.md reference. Leave v1.5.6-impact.md, TRACKING.md, kitbash, cultures.md, party-template-sizing, localization-map, the Armory snapshot and the other hunks driven by 1.5.x content. This is the citation cleanup that 2129f753c's lint scope assumes.
- `66dd97536` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct tool rows, tester checklist and asset ledgers
  - why: Tool rows, tester checklist and ledgers are mostly 1.5.x state, but the engine-study fixes hold on 1.4.x: monster-model.md is byte-identical there, the spider is a Horse-slot mount on 1.4.x too (1.4.x docs/features/spider.md:8), and TaomHowdahMachine and ElephantLikeEngageDecorator exist on both lines.
  - port notes: Take the monster-model.md, mount-and-rider-runtime.md, usable-machines.md and animation-binding-and-playback.md hunks after re-checking the cited line (ElephantLikeEngageDecorator.cs:41) on 1.4.x. main-map-vista.md is also identical on 1.4.x, but its facts need a check. The tools/check_external_xslt.py docstring's CI claim holds on 1.4.x only if it cites ccef172ad (1.4.x's own workflow removal) instead of c8d69a0dc, and its counts need re-measuring (8 repo stylesheets on 1.4.x vs 11 on 1.5.x). Leave mission-frame-threads-and-native-costs.md (absent; v1.5.6 KnownBuilds row), the tools/README.md rows, the tester checklist realm list (1.5.x content) and the six docs absent on 1.4.x.
  - conflicts: tools/check_external_xslt.py
- `24e52abb9` 2026-11-04 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct 25 more feature docs against their code
  - why: Feature-doc truth pass that mostly tracks 1.5.x content (ranged-ladders' 138 items, while 1.4.x tools/ranged_ladders.json is 5,388 bytes vs 15,524), though some fixes hold on 1.4.x: troop-weight-system's 6 patch inits (TroopWeightIoC.cs has 6 Initialize calls on both lines) and multi-culture-armor-revamp's 'SubModule.xml does not declare LOTRLOME_Armory'.
  - port notes: Hand-apply only re-measured corrections: the 21 touched files on 1.4.x all differ from the 1.5.x parent. The SubModule.xml claim holds, but its line cite (21-25) must become 1.4.x's (11-14 and 23). Leave cardolan-rhudaur, armory-ref-audit, mission-perf-heartbeat and personal-relations (absent on 1.4.x), and the Neutral-realm, Anduin Vale, archetype and roster counts unless they re-measure true.
- `f4058993a` 2026-11-04 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - finish the feature doc pass, record its open questions
  - why: Last feature-doc wave plus a 1,202-line open-questions record, mostly 1.5.x content (new cultures, the v1.5.6 pin, co-op class counts), but siege-trebuchets.md is byte-identical on 1.4.x and its correction holds there: SiegeEngineAvailabilityService, its tests and the injected TaomSiegeEventModel ctor all exist on origin/bannerlord-1.4.5.
  - port notes: Take siege-trebuchets.md. Re-measure the rest: for example, auto-resolve-diagnostics' EncounterAdapter IsFinalized cite is :166 on 1.4.x and :302 on 1.5.x (the old :137 is stale on both). Leave docs/reviews/doc-pass-2026-11-03-open-questions.md (a 1.5.x pass record), the seven docs absent on 1.4.x, and culture or career lists that name 1.5.x-only cultures (anduinvale, angaladh, dorwinion, southrhovanion).
skipped in this group: `f7cf6def6` SKIP-1.5-ONLY-FIX docs(tools): v2.0.36 - correct the remaining catalog rows; `6e886fe8d` SKIP-BOOKKEEPING docs: v2.0.36 - rebuild backlink footers of the docs this work touched; `49bf62fea` SKIP-BOOKKEEPING docs(tools): v2.0.36 - leave Fords rows to their owner, lower the grap; `a2d3dc6bf` SKIP-1.5-ONLY-FIX docs: v2.0.36 - career art counts for 91 careers

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 3, batch 7, batch 8, batch 12, batch 13, batch 15, batch 17, batch 18, batch 21, batch 23, batch 25, batch 29, batch 30, batch 34, batch 35
