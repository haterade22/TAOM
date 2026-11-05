# Input pack: WP 143 harness-rules-sync

Title (draft): Sync the 2026-11-03 rules review, the lint_docs chain and the .githooks pre-commit gate, adapted for 1.4.x
Wave: 5   Lane: H   Draft priority: P2   Groups: 11   Port commits: 24
Depends on (WP ids): 138, 142

## Orchestrator notes for this WP (binding)
- Takes every remaining harness group (see the pack). Keep 1.4.x specifics: v2.0.28 subject rule, v1.4.8 engine facts, Serena read tools.
- K2: rebuild the settlement-prefix line at .claude/rules/xml-data.md:37 from docs/modding/id-cheatsheet.md:94-114 (c625ada0f's shape).
- Take 175b149fd's /issue duplicate-search step (co-op WP dissolved, D7) and 709649c3f's harness hunks except release pre-flight 5 (WP115).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Port groups owned by this WP (from the commit classification)

### group `issue-646-animalia-monstersize`  P3  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/AdvancedCombat/CustomAttacksUtils.cs, Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs, Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs, Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs, Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeCombatProfile.cs, Main/Features/Elk/ElkCombat.cs, Main/Features/Elk/ElkConfig.cs, Main/Features/Elk/ElkMissionBehavior.cs, Main/IoC.cs, Main/SubModule.cs, Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml, Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml, Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_mirkwood.xml, Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml, Main/_Module/ModuleData/troops/troops_animalia_test.xml, Main/_Module/ModuleData/troops/troops_mirkwood.xml, TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs, TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs, TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs, TAOM.Tests/Features/Elephant/HowdahPrefabTests.cs, TAOM.Tests/Features/Elk/ElkConfigTests.cs, TAOM.Tests/Features/Elk/ElkMountWiringTests.cs, TAOM.Tests/Features/Mumakil/MumakilPlatformTests.cs, tools/apply_animalia_armory.py, tools/blender/measure_animalia_clips.py, tools/gen_animalia_anim_clips.ps1, tools/skeleton_hit_capsules.py, tools/tests/test_skeleton_hit_capsules.py, tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py ...
- `709649c3f` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: Add unit tests for Animalia armory writer functionality
  - why: A squash that introduces the 1.5.x-only Animalia, reworked Elk and MonsterSize creature features (#646); only three small harness hunks are generic.
  - port notes: Hand-apply only: (1) .claude/skills/release/SKILL.md Phase 1 item 5, the `git grep -l taom_test_ -- Main/_Module` pre-release gate, minus the Animalia sentence (1.4.x has no taom_test_ rows today, git grep on origin/bannerlord-1.4.5); (2) the 2-engine-compat lens rule that every by-name reflection site needs a ReflectionSiteBindingTests [DataRow] and a reflection-sites.md row (both files exist on 1.4.x), folded into the 1.4.x monolithic deep-review SKILL.md because lenses/ and the deep-reviewer agent are absent on 1.4.x; (3) tooling lens items 14 and 15, folded in the same way. Leave everything else: Animalia, Elk, MonsterSize, the ElephantLike reach scaling (a feature extension, no shared bug fix), the comment-only edits in CustomAttacksUtils and CareerAgentStatService, ModuleData, tools, docs, the harness-facts/CLAUDE.md Fable text (deep-reviewer is 1.5.x-only), and the validate_xml_schemas.py taom_body_length allowlist (that validator is absent on 1.4.x, and on 1.4.x the attribute is dead data, so suppressing it would hide a real signal).
  - conflicts: Main/Features/AdvancedCombat/CustomAttacksUtils.cs, Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs, Main/Features/CareerSystem/Abilities/ICareerAgentStatService.cs, Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs, Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeCombatProfile.cs, Main/Features/Elk/ElkCombat.cs, Main/Features/Elk/ElkConfig.cs, Main/Features/Elk/ElkMissionBehavior.cs, Main/IoC.cs, Main/SubModule.cs, Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml, Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml, Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_mirkwood.xml, Main/_Module/ModuleData/equipmentsets/taom_lord_template_equipment.xml, Main/_Module/ModuleData/troops/troops_animalia_test.xml, Main/_Module/ModuleData/troops/troops_mirkwood.xml, TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs, TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs, TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs, TAOM.Tests/Features/Elephant/HowdahPrefabTests.cs

### group `plan-023-no-resetforunload`  P3  track=harness  commits=1  sim: clean=0 conflict=1
- `636b50abf` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: docs(rules): v2.0.30 - new patches need no ResetForUnload
  - why: Adds a harmony-patches.md rule that new patches need no ResetForUnload because nothing reloads TAOM in-process; the claim holds on v1.4.8.
  - port notes: Take the rule line, but replace its v1.5.3 citation with the v1.4.8 one. Module.FinalizeSubModulesBases (Module.cs:227-233) is called only from FinalizeModule (Module.cs:1268-1287), through FinalizeCurrentModule (1289-1293), whose only caller is CoreManaged.Finalize (CoreManaged.cs:113-118) at shutdown; all in C:\Decompiled_Bannerlord\_categories_v1.4.8\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade. ResetForUnloadSweepTests and the three SubModule.cs flags the rule names (SubModule.cs:106-108) exist on 1.4.x. The rule file is unchanged there since the fork. Rewrite the CHANGELOG hunk.

### group `issue-921-coop-row-test`  P3  track=tests-ci  commits=2  sim: clean=1 conflict=1
- `5db856e05` 2026-10-20 PORT-PARTIAL P3 pick=clean :: test(coop): v2.0.35 - every module campaign behavior has a co-op row
  - why: A test that every feature-module campaign behavior has a co-op classification row; useful on 1.4.x, but it enumerates TAOM.Composition.FeatureModules.All, which exists only with Main/Composition (plan 018).
  - port notes: Signals say pick=clean, but it would not compile on 1.4.x (no TAOM.Composition namespace there). Port it only if plan 018 lands on 1.4.x, or retarget it to the behaviors 1.4.x registers in SubModule.cs, which this version deliberately skips. Recompute the baseline from 1.4.x behaviors (the 1.4.x coop-interop.md has both headings it parses, at lines 204 and 250). The TEMPLATE.md and coop-interop.md hunks go with the test.
  - issues: 921
- `175b149fd` 2026-10-20 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.35 - RCA for the co-op row test and issue search
  - why: Adds a step to the /issue skill to search open issues for duplicates (the 1.4.x skill files without searching), plus a lesson; the RCA mostly records the co-op row test.
  - port notes: Take the new first step in .claude/skills/issue/SKILL.md, adapted to the 1.4.x step list (its step 3 runs gh issue create directly, with no check_public_text.py), and the build-tooling-workflow lesson. Take the RCA only if 5db856e05 ports.
  - issues: 781, 919, 921, 922
skipped in this group: `c2988ac3d` SKIP-1.5-ONLY-FIX test(coop): v2.0.35 - baseline five behaviors that landed in review

### group `harness-rule-writers-before-gates`  P3  track=harness  commits=1  sim: clean=1 conflict=0
- `7c6d48628` 2026-10-21 PORT P3 pick=clean :: docs(rules): v2.0.35 - list every writer before gating on an engine flag
  - why: Adds one C# architecture rule row (list every writer of an engine flag, vanilla first, before gating on it); the rule fits either line and the pick is clean.
  - port notes: The row cites the Hammerhand lesson, which will not exist on 1.4.x: keep the rule and cite lessons/adapters-taleworlds-api.md generically. Re-subject to v2.0.28.
  - issues: 895

### group `issue-877-loremasters-line`  P3  track=harness  commits=1  sim: clean=1 conflict=0
- `267834a2e` 2026-10-23 PORT P3 pick=clean :: docs(gui): v2.0.35 - a hint's parent must lie inside the bar it sits on
  - why: Generic Gauntlet rule for .claude/rules/gui-ui.md (a hint's widget must be enabled and inside every ancestor's rectangle), verified on v1.4.8 where EventManager.CollectEnableWidgetsAt (EventManager.cs:880) enters a child only if it is not hidden, not disabled and IsPointInsideMeasuredArea (AreaRect.IsPointInside, :742).
  - port notes: Clean pick. The rule cites the docs/reviews/lessons/localization-ui.md entry 'A hover target lies inside every ancestor's rectangle', written in the OUT commit fcd140907: lift that lesson hunk too (its v1.5.5 EventManager.cs:880-901 cite matches v1.4.8 line 880) or drop the citation. Re-subject with v2.0.28.
  - issues: 877
skipped in this group: `fcd140907` SKIP-FEATURE feat(character): v2.0.35 - Loremaster's Line marks a skill's limit

### group `harness-writers-before-gates`  P3  track=harness  commits=1  sim: clean=0 conflict=1
- `ed3bb65cc` 2026-10-24 PORT-PARTIAL P3 pick=conflict :: docs(harness): v2.0.35 - plans cite the line that builds an engine total
  - why: Extends the 'Writers before gates' rule in csharp-architecture.md to cite the line that builds an engine total; the rule row is absent on 1.4.x (born on 1.5.x in 7c6d48628) and both cited engine facts hold on v1.4.8.
  - port notes: Port after 7c6d48628. Take the one rule row: ItemRoster.TotalFood counts livestock meat (v1.4.8 ItemRoster.cs:449-452) and SellItemsAction prices each unit in a loop (v1.4.8 SellItemsAction.cs:47-49), so keep both facts inline but drop the docs/reviews/rca-provender-2026-10-23.md citation and the RCA edit (1.5.x-only feature).
  - prereqs: 7c6d48628

### group `harness-provenance-rule`  P3  track=harness  commits=1  sim: clean=1 conflict=0
- `1d88b8633` 2026-10-26 PORT P3 pick=clean :: docs(harness): v2.0.35 - a source the maintainer leaves unnamed
  - why: The provenance rule gains the maintainer's unnamed-source exception; a harness rule that picks cleanly onto 1.4.x.
  - port notes: Clean pick. It adds the 'Sources the maintainer chooses not to name' section to .claude/rules/provenance.md (absent on the 1.4.x tip) and adjusts the register and docs/INDEX.md wording to 'every named source'.

### group `harness-rules-review-2026-11-03`  P2  track=harness  commits=13  sim: clean=1 conflict=12
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .mcp.json, .serena/memories/task_completion_checklist.md, .serena/project.yml, tools/graphify_taom.py, tools/loop_progress.py, tools/test_hooks.sh
- `2992763f6` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: docs(rules): v2.0.36 - correct the rules that caused harm
  - why: It corrects harness rule text that misleads agents, and several of the same harmful lines are on the 1.4.x tip, including the spear-registration advice behind the shield-polearm trap.
  - port notes: Verified on origin/bannerlord-1.4.5:
- docs/ai-includes/weapon-creation-workflow.md:228-230 still says short spears go in TwoHandedPolearm only, although tools/register_one_handed_polearms.py exists there.
- .ai/review-reference.md:464 says 'NEVER use SandBox/ModuleData/'.
- lord-skills-authoring.md:208 calls --all-cultures --apply 'the safe default'.
- .editorconfig has no [*.{sh,bash}] end_of_line = lf section.
Take those fixes, plus the paid-call guards (completion-workflow, deep-review, localize, new-culture-authoring, adopt-external), the no-deploy flags on every dotnet test line, the false-sample removals in gamemodels.md and csharp-patterns.md, the thread-local finalizer advice and the feature-builder single-owner line. Merge each by hand: 33 of the 40 files changed on 1.5.x.
Leave orientation.md (where the act_horse_kick trap line lives), lens 7, review-outside-pr, model-effort-selection.md and artist-ladder-brief.md, all absent on 1.4.x. Also leave the six plan rows, and the [.githooks/*] editorconfig line unless .githooks are ported. The Serena line needs the maintainer's decision, because Serena's tools are offered in this laptop session.
  - issues: 763
- `e653257a8` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: docs(rules): v2.0.36 - trim the entry docs and always-on rules
  - why: It trims AGENTS.md, CLAUDE.md and the always-on rules that load into every session; the 1.4.x copies carry the same duplicated sections, but all 8 shared files changed on 1.5.x.
  - port notes: Merge by hand; do not cherry-pick. AGENTS.md on 1.4.x still holds the Code Review Rules, the Codex skill list and the provider paragraph that this commit drops. Take:
- the 'quote the line that produces an effect' research rule
- the issue-rule waiver, and 'no git action' meaning git that changes state
- the TDD thin-entry-point note
- the removal of description frontmatter the harness ignores, and the one-sentence form of the cadence table
Leave references to pieces only 1.5.x has: the risk tiers from /deep-review's tier table, orientation.md, the deleted agents (unless 452796db4 is ported) and 1.5.x paths. Keep the 1.4.x CHANGELOG and v2.0.28 subject rules.
- `625e8fc53` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: docs(rules): v2.0.36 - cut the path rules back toward their cap
  - why: It cuts the path-scoped rules back toward their 12 KB cap, and on the 1.4.x tip csharp-architecture.md (25.8 KB), vanilla-data-comparison.md (21.8 KB), gui-ui.md (19.9 KB), troops.md (14.7 KB) and external-skill-ports.md (12.1 KB) are over that cap too.
  - port notes: All 14 shared rule files changed on 1.5.x. Port by re-trimming the 1.4.x rules with this commit as the model rather than applying the diff. Before deleting moved text, confirm its destination exists on 1.4.x. docs/reference/gui-prefab-clones.md is absent there; docs/modding/party-templates.md exists. The 'Which thread runs your target' table in harmony-patches.md says it was verified on v1.4.8 and is the reference for E4's off-thread callback errors, so it must stay reachable. config-providers.md (created here) and data-scripts.md (absent on 1.4.x) come along only with their content.
- `452796db4` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs(skills): v2.0.36 - correct the skills, drop three unused agents
  - why: It deletes three custom agents and corrects skill text; the agents and several of the skills exist on 1.4.x, but 16 of the 34 files are absent there and the rest changed.
  - port notes: The debugger, error-detective and refactoring-specialist agents exist on 1.4.x, and its CLAUDE.md routing table and 'Custom Agents' section name all three. If the maintainer agrees, delete them together with those rows; the 1, 0, 0 spawn counts come from 1.5.x transcripts. Take:
- the /freeze fixes: memory and scratchpad writes allowed, relative paths resolved from the git root (check-freeze.sh exists on 1.4.x)
- the 'read every line first' rule for crashes reported only by logs, in /investigate and /native-crash-triage
Do NOT take the /native-crash-triage DLL-copy change: 1.4.x session-start.sh does not archive native DLLs, so its manual-copy advice is still right there. Re-verify the /engine-bump wEditor-offset correction against the 1.4.8 triage tooling before taking it. Leave deep-reviewer.md, implementer.md, the lenses, the improve references and workflows, and the armory-audit and harness-retro skills, all absent on 1.4.x.
- `4fbfb7adc` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - delete guides with no reader, correct the rest
  - why: It deletes six AI guides that nobody reads and corrects stale docs; five of the six guides are byte-identical on the 1.4.x tip, and the same false claims are there, such as tools/README.md:8 'All Python scripts support --dry-run'.
  - port notes: Deleting tdd-enforcement, security, research-workflow, iterative-problem-solving and multi-approach-validation applies as is, since their blobs equal the 1.5.x parent. decompiled-code-analysis.md differs. On 1.4.x, links into these guides need fixing in .claude/skills/new-feature/SKILL.md, docs/INDEX.md, agent-operating-manual.md, agent-teams.md, architecture.md, code-quality.md, patterns.md, taleworlds-research-guide.md, testing-guide.md and doc-lookup.md. Merge the README and tools README corrections and the moved maintainer decisions by hand. Leave the plan table, git-and-commits.md and harness-retro.md (absent on 1.4.x), and any text about tooling only 1.5.x has.
- `250e1b321` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs(adrs): v2.0.36 - amend the ADRs, drop false samples and gates
  - why: It amends the ADRs to drop samples, tests and gates that never existed; the same ADRs exist on 1.4.x, which has also had no CI since ccef172ad.
  - port notes: 3 of the 11 shared files are identical to the 1.5.x parent and apply; merge the dated amendments into the rest by hand. Leave ADR-011, ADR-012 and lens 1, all absent on 1.4.x. If 8caac7da5's gate tests are ported, ADR-002 and ADR-007 should name them; otherwise the amendment must say that no test holds the rule. Check each 'does not exist' claim (IAdapterFactory, the four provider interfaces) against the 1.4.x tree before writing it.
- `9cfcadd07` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: chore(harness): v2.0.36 - remove dead hooks and Serena, fix the gates
  - why: It removes seven dead hooks and fixes several gate hooks; all seven are registered in the 1.4.x .claude/settings.json, and the gates it fixes exist there too.
  - port notes: Take:
- the deletion of post-compact, pre-compact, check-deep-review, check-version-tagged, session-stop, mcp-health-check and mcp-health-mark, with their settings.json entries
- the matching update to the CLAUDE.md Hook Response Contracts rows on 1.4.x, which name post-compact.sh and check-version-tagged.sh
- the gate fixes in check-claude-files-tracked, config-protection, check-moduledata-validation, check-polearm-shield-parity and check-verification-evidence
The block-no-verify rewrite calls the taom_hook_command and taom_hook_segments helpers in _pybin.sh. Those helpers and _shellwords.py came from b23dc8999 and caa7ac356, and the 1.4.x _pybin.sh has neither helper. Leave the hunks for _pushjudge.py, _stop_reminder.sh, check-graphify-usage.sh and upstream-watch.sh, which only 1.5.x has. The session-start core.hooksPath warning only makes sense once .githooks exist (434d1d795). Removing Serena is the maintainer's call. Run bash tools/test_hooks.sh after the merge; the 1.4.x copy is 509 lines against 3,681 on the 1.5.x parent.
  - prereqs: b23dc8999, caa7ac356
  - conflicts: .mcp.json, .serena/memories/task_completion_checklist.md, .serena/project.yml, tools/graphify_taom.py, tools/loop_progress.py, tools/test_hooks.sh
- `683fd1331` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - decisions move from private memory to the repo
  - why: It moves maintainer decisions from private memory into repo docs; the decisions about the shared Armory, the localization pipeline and child townsfolk apply to both lines, but the engine file layout it records was read from the v1.5.x NavigationCache.
  - port notes: After checking each against 1.4.x, take the notes in TRANSLATOR_GUIDE.md, module-armory.md, npcs-notables-and-townsfolk.md, recipe-add-a-race-or-creature.md and agent-teams.md, and the new docs/research/harmony-fork-feasibility.md. The distance-cache layout in module-map.md (IsPortUsed, NavalDLC) must be re-read from the v1.4.8 NavigationCache.Serialize before it lands. Leave ADR-012, the upstream-review skill and adoption-history.md, all absent on 1.4.x.
- `b6b54ce84` 2026-11-03 PORT-PARTIAL P3 pick=clean :: docs(reviews): v2.0.36 - RCA of the rules review gates
  - why: It adds the RCA record and five process lessons from the rules-review gates, and every lesson describes tooling that 1.4.x does not have yet.
  - port notes: Take each lesson in docs/reviews/lessons/build-tooling-workflow.md only together with the tool it describes:
- POSIX sh git hooks: with 434d1d795
- shlex comment handling: with the _shellwords base
- CommitView: with 35ff005dc
- the ratchet merge base: with the lint_docs ratchet
- analyze_reviews rows: with 69d2e8c0a
The RCA file serves as provenance for those ports; otherwise skip it.
- `3ea14a730` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: feat(hooks): v2.0.36 - warn when MEMORY.md passes its limit
  - why: It makes session-start warn when MEMORY.md passes a size limit; the check itself is generic, but the 40-line/4 KB limit comes from ADR-011, which 1.4.x lacks, and the 1.4.x session-start.sh (183 lines against 357) has no compact-time block for the commit's other edit to change.
  - port notes: Take the main_checkout_name helper and the MEMORY.md size note, and pick the limit: ADR-011 is absent, and the 1.4.x harness-facts.md gives the load cap as 200 lines or 25 KB. Leave the Dead-ends find change, since 1.4.x has no such block. Port the tools/test_hooks.sh 7n rows, adapted.
  - conflicts: tools/test_hooks.sh
- `b6012355b` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: chore(harness): v2.0.36 - remove the agent teams flag, add the retro
  - why: It removes CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS after headless tests showed the flag does not change how subagents load memory; the 1.4.x .claude/settings.json still sets it to 1 at line 3, and agent-teams.md still describes it.
  - port notes: Take the settings.json line removal and the agent-teams.md section change; the 2026-09-05 budget-incident text applies to both lines. Take docs/features/commit-gates.md only with 434d1d795. docs/reviews/harness-retro-2026-11-03.md is a 1.5.x measurement record, so leave it. The harness-facts.md hunk edits a row the 1.4.x file may not have; check before merging.
- `f4b036e95` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs(harness): v2.0.36 - correct false lines in skills and rules
  - why: It makes small corrections to skills, rules and CLAUDE.md; a few hold on 1.4.x, but most edit text that has changed on 1.5.x.
  - port notes: Take the removal of coverage percentages from feature-builder, the /localize line about searching issue bodies, and the /ship issue wording if the 1.4.x skill makes the same demand. Take the lens 1 and ServiceLocatorGateTests references only with 8caac7da5. The /ship pointer to the tier table needs 1.5.x's /deep-review tiers. Verify the CLAUDE.md CRLF-probe claim under the 1.4.x shell before taking it.
  - issues: 1078
- `3ead3765e` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: docs: v2.0.36 - correct stale docs, fill the catalogs
  - why: It corrects stale docs and fills catalogs; some corrections hold on 1.4.x, while most new catalog rows describe scripts and features only 1.5.x has.
  - port notes: Verified on 1.4.x:
- Main/Core/Services does not exist, so the ADR-008 checklist fix applies, together with its no-deploy dotnet test line.
- docs/reference/gamemodel-registry.md and Dependencies/Foundation/PatchShield.cs exist, so the engine lifecycle doc fix applies.
The lord-skills guide's --check line needs apply_culture_skills_traits --check from f1675c058. Add tools/README.md rows only for scripts present on 1.4.x, and provenance rows only for adoptions present there. Leave the 18 feature-map folders, the README kingdom table and the roadmap count, which are 1.5.x content.

### group `tools-lint-docs-commit-view`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/check_commit_msg.py, tools/lint_docs.py, tools/tests/test_check_commit_msg.py, tools/tests/test_lint_docs.py, tools/tests/test_model_registry.py
- `35ff005dc` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - the doc linter judges what a commit records
  - why: It makes lint_docs judge the tree a commit records and adds a rule-drift check that also refuses dotnet test lines without -p:ModuleId=, but the 1.4.x lint_docs.py (1,280 lines against 1,872 on the 1.5.x parent) has neither --staged nor --drift-only.
  - port notes: The 1.5.x lint_docs.py history must come first: 12 commits since the fork, among them 09f3126a7, ad3967222, bc7a7f0d9, 88c0b6985, bc30b76da and ac1d8edbc. The commit-msg half also needs tools/check_commit_msg.py from 0382d5d2a. Check 11's no-deploy and dead-memory-citation rules are worth taking even without .githooks. Drop the gamemodels.md table only once the 1.4.x gamemodel-registry.md holds every row. Leave the lens 7 and ADR-011 edits (absent on 1.4.x).
  - prereqs: 0382d5d2a, 09f3126a7, ad3967222
  - conflicts: tools/check_commit_msg.py, tools/lint_docs.py, tools/tests/test_check_commit_msg.py, tools/tests/test_lint_docs.py, tools/tests/test_model_registry.py

### group `harness-githooks-commit-gates`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .githooks/commit-msg, .gitleaksignore, CONTRIBUTING.md, tools/test_hooks.sh, tools/tests/test_shell_lint_gate.py, tools/tests/test_shellwords.py
- `434d1d795` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: feat(hooks): v2.0.36 - commit gates for every committer
  - why: It adds .githooks/pre-commit and pre-merge-commit so the gitignored-file, doc and ModuleData gates bind every committer, while the 1.4.x line has no .githooks directory and gates only commits Claude makes.
  - port notes: It depends on:
- .githooks/commit-msg and tools/check_commit_msg.py (0382d5d2a)
- lint_docs --drift-only --staged (35ff005dc and its lint_docs chain)
- the block-no-verify and _shellwords base (9cfcadd07, b23dc8999, caa7ac356)
The pre-commit runs validate_moduledata.py with codes 1.4.x lacks (TROOP_ID_REMOVED, SKILL_TEMPLATE_MISMATCH, ARMOUR_ACQUISITION_REF, CREATURE_GEAR_OBTAINABLE, BROKEN_FACTION_REF, SCHEMA_INVALID, MISSING_COLLISION_BODY, ITEM_SLOT_REJECTED), plus generate_career_kits.py --verify, which only 1.5.x has. Trim both to what 1.4.x has. The commit-msg check must enforce the v2.0.28 subject and keep the hand-edited CHANGELOG rule. The POSIX sh, fail-open design and setup-dev-env.ps1's core.hooksPath line port as is. 1.5.x plans to retire its Claude commit gates after a comparison around 2026-11-17; do not retire the 1.4.x gates.
  - prereqs: 0382d5d2a, 35ff005dc, 9cfcadd07, b23dc8999, caa7ac356
  - conflicts: .githooks/commit-msg, .gitleaksignore, CONTRIBUTING.md, tools/test_hooks.sh, tools/tests/test_shell_lint_gate.py, tools/tests/test_shellwords.py

### group `harness-dead-memory-lint`  P3  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/lint_docs.py, tools/tests/test_lint_rule_drift.py
- `2129f753c` 2026-11-03 PORT-PARTIAL P3 pick=conflict :: feat(tools): v2.0.36 - lint dead memory citations in the engine study
  - why: Extends 1.5.x's dead-memory-citation lint to docs/reference/engine; the rule fits 1.4.x, but its base check (35ff005dc) is not on 1.4.x and the 1.4.x engine study still holds 71 such citations.
  - port notes: Take the DEAD_MEMORY_DIRS extension in tools/lint_docs.py, its test and the doc-health-linter.md row only after two things land: 35ff005dc (adds check 11 and tools/tests/test_lint_rule_drift.py, both absent on 1.4.x) and a 1.4.x citation cleanup (50e8dc7ae's engine hunks). 1.4.x also has 82 citations in .claude/rules (18), .claude/agents (1), .claude/skills (38) and docs/ai-includes (25) that 35ff005dc's own scope would flag; 1.5.x has 0. Leave docs/reviews/_clan_registry.json: the 16 clans it adds are 1.5.x content (anduinvale, angaladh, dorwinion and southrhovanion houses; feral_orcs not found in 1.4.x ModuleData by git grep).
  - prereqs: 35ff005dc, 50e8dc7ae
  - conflicts: tools/lint_docs.py, tools/tests/test_lint_rule_drift.py

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 4, batch 7, batch 21, batch 22, batch 25, batch 27, batch 34, batch 35
