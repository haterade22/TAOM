# Input pack: WP 138 harness-review-pipeline

Title (draft): Harness: deep-review lenses, the /improve run pipeline, retro and usage tools, model and effort routing
Wave: 5   Lane: C   Draft priority: P2   Groups: 28   Port commits: 71
Depends on (WP ids): 116, 136, 137

## Orchestrator notes for this WP (binding)
- One serial harness executor. End-state adaptation, not commit replay. Keep the 1.4.x skills the 1.5.x tip deleted unless each deletion is justified; graphify is not installed on the laptop.
- Take fa8c06f82's lens half here (its tool half is WP136). The /improve pipeline assumed a generated CHANGELOG: fine after WP102.
- Decision D10: plans are executed by ordinary sessions, not /improve execute; this WP is not on the critical path.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `harness-engine-bump-skill`  P3  track=harness  commits=1  sim: clean=0 conflict=1
- `43fe5ebe8` 2026-09-14 PORT-PARTIAL P3 pick=conflict :: docs(engine): v2.0.28 - v1.5.2 impact, ranked body diff, target line
  - why: Mostly the v1.5.2 target line and impact docs, plus one engine-agnostic /engine-bump step: diff the bodies of every bound member, full-replacement GameModel overrides first.
  - port notes: Take only the .claude/skills/engine-bump/SKILL.md member-level body-diff paragraph (absent on 1.4.x), restating its v1.5.2 calibration as an example. Leave the CLAUDE.md/AGENTS.md/.ai/agents/skills target-line edits, the docs/migration v1.5.2 docs, and the PartyIconScale comment edits, which describe v1.5.0 relocations.

### group `improve-run-pipeline`  P2  track=dev-tools  commits=15  sim: clean=0 conflict=15
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .gitattributes, tools/check_public_text.py, tools/improve_ctl.py, tools/integrate_branch.py, tools/test_hooks.sh, tools/tests/test_check_public_text.py, tools/tests/test_improve_ctl.py, tools/tests/test_improve_workflows.py, tools/tests/test_integrate_branch.py
- `5e5877bdf` 2026-09-27 PORT P2 pick=conflict :: feat(tools): v2.0.30 - check public text before it is posted
  - why: Adds check_public_text.py, a gate for long dashes, local absolute paths and leftover placeholders in issue and commit text before it goes public.
  - port notes: Standalone; its dash check calls lint_docs.scan_text_for_dashes, present on 1.4.x (tools/lint_docs.py:955). Take 086abc57b and the 0cc91b320 URL-scheme test with it. Conflicts only in tools/README.md.
- `218488f4e` 2026-09-27 PORT P2 pick=conflict :: feat(tools): v2.0.30 - improve_ctl runs the /improve pipeline plumbing
  - why: Adds improve_ctl.py (args, status, codex-prompt, file-issue, watch) to replace the /improve run's per-batch scratch scripts.
  - port notes: args embeds dispatch-rules.md (from 1d1abd694) and codex-prompt reads review-codex/references/prompt-fixed.md (from 785b2703e); file-issue gates on check_public_text.py. No line-specific literal found in the tool (the version comes from SubModule.xml). Take d6742dd91 and 0cc91b320 with it.
  - prereqs: 5e5877bdf, 1d1abd694, 785b2703e
- `d648e00c0` 2026-09-27 PORT P2 pick=conflict :: feat(tools): v2.0.30 - integrate_branch merges with append-only union
  - why: Adds integrate_branch.py, which merges a branch into an integration worktree and unions conflicts in append-only files (lessons, REVIEW-LOG).
  - port notes: Line-agnostic. On 1.4.x CHANGELOG.md is still hand-edited and the top conflict hot spot; it is not in the default append-only list, so it goes to hand resolution (exit 2). Ask the maintainer whether 1.4.x should union CHANGELOG blocks. Take 459b63ad7 with it.
- `e4c43e59e` 2026-09-27 PORT-PARTIAL P2 pick=conflict :: fix(hooks): v2.0.30 - deny commits over an untracked .js harness file
  - why: check-claude-files-tracked.sh let an untracked .js harness file through; the 1.4.x copy also misses .py.
  - port notes: 1.4.x's gate builds its list with find -name (md, sh, json, yaml, yml) at check-claude-files-tracked.sh:70, not 1.5.x's post-plan-013 grep, so re-express it: add -name '*.py' and -name '*.js'. Port the 7b row (an untracked .js under .claude/skills must be denied). Matters once any .js workflow or .py hook helper (_shellwords.py, _pushjudge.py) is ported.
  - conflicts: tools/test_hooks.sh
- `1198881f0` 2026-09-27 PORT-PARTIAL P2 pick=conflict :: feat(improve): v2.0.30 - durable Workflow scripts for /improve runs
  - why: Moves the /improve Workflow scripts (fanout, plans, execute, review, draft-issues) into the skill with args-driven values, an LF pin and a static plus stub-runtime test.
  - port notes: Take the five scripts, the .gitattributes LF pin for .claude/skills/improve/workflows (1.4.x .gitattributes exists with sh and bash rules) and tools/tests/test_improve_workflows.py. Adapt plans.js:179 and :212 ('No CHANGELOG step: CHANGELOG.md is generated at /release'; line numbers at 37ddf739d), which contradict 1.4.x's hand-edited CHANGELOG and its hard-blocking check-changelog-changed.sh. Take bc160ca85 with it.
  - prereqs: 218488f4e, 1d1abd694, e4c43e59e
  - conflicts: .gitattributes
- `1d1abd694` 2026-09-27 PORT-PARTIAL P2 pick=conflict :: docs(improve): v2.0.30 - rebuild the skill around the run pipeline
  - why: Rebuilds /improve around the full run pipeline and corrects its build and test commands to the non-deploying form, which the 1.4.x skill still gets wrong.
  - port notes: Safety hunk first: 1.4.x Main/TAOM.csproj:133-136 says DisableModuleCopy does not gate CopyModule (ModuleId must be emptied), so the 1.4.x skill's commands (SKILL.md:22,38; plan-template.md:79-80,131,137-138, all -p:DisableModuleCopy=true without -p:ModuleId=) deploy into the game install; add -p:ModuleId= even if the rest waits. For the rebuild (SKILL.md, dispatch-rules.md, run-protocol.md, execute-and-review.md replacing closing-the-loop.md, audit-playbook.md, plan-template.md), keep 1.4.x's CHANGELOG step: dispatch-rules.md:95 and plan-template.md:127-128 (at 37ddf739d) forbid editing CHANGELOG.md, which would leave 1.4.x executors blocked by check-changelog-changed.sh. Labels are v2.0.28 on 1.4.x. Fold in 84c606fa3 and 37ddf739d.
- `43db0b47a` 2026-09-27 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.30 - drop lesson counts, retire sprint scripts
  - why: Drops the hand-maintained lesson counts from the lessons index and deletes the 1.5.x sprint's machinery scripts.
  - port notes: Take only the docs/reviews/LESSONS-LEARNED.md change; the 1.4.x index still prints per-category and total counts (lines 11-23), and nothing under 1.4.x tools/, .claude/ or .github/ reads them (git grep: only a docstring in patch_dwarf_action_parity.py names the file). The plans/_audit/2026-09-23-opus/ deletions and PROGRESS.md touch a folder 1.4.x does not have.
- `989c87497` 2026-09-27 PORT-PARTIAL P3 pick=conflict :: docs(claude): v2.0.30 - /improve index row names the run variants
  - why: Updates the /improve row of the 1.5.x CLAUDE.md skills index to name the execute, review, merge and resume variants and their maintainer gate.
  - port notes: 1.4.x CLAUDE.md has no skills-index table; carry the wording into its Skill Routing row for /improve, within the CLAUDE.md size budget, after the 1d1abd694 port.
  - prereqs: 1d1abd694
- `84c606fa3` 2026-09-27 PORT P3 pick=conflict :: docs(improve): v2.0.30 - keep the playbook section names others cite
  - why: Restores the three audit-playbook section names (Tech Debt & Architecture, Dependencies & Migrations, DX & Tooling) that rule-provenance.md cites.
  - port notes: Fold into the 1d1abd694 port; 1.4.x docs/reference/rule-provenance.md:36 cites 'audit-playbook § Tech Debt & Architecture'.
  - prereqs: 1d1abd694
- `086abc57b` 2026-09-27 PORT P2 pick=conflict :: fix(tools): v2.0.30 - public-text gate sees every local path form
  - why: Makes the public-text gate catch forward-slash drive paths and every Git Bash drive path, and skip code spans when looking for placeholders.
  - port notes: Follow-up to 5e5877bdf; take together.
  - prereqs: 5e5877bdf
  - conflicts: tools/check_public_text.py, tools/tests/test_check_public_text.py
- `459b63ad7` 2026-09-27 PORT P2 pick=conflict :: fix(tools): v2.0.30 - integrate_branch stages the merge, never commits
  - why: integrate_branch.py now stages the merge instead of committing from Python, which bypassed every PreToolUse commit gate, and its union reads the diff3 base.
  - port notes: Follow-up to d648e00c0. On 1.4.x the gates the printed 'git commit -F' line passes through include the CHANGELOG and v2.0.28 label hooks, so the message file must satisfy them.
  - prereqs: d648e00c0
  - conflicts: tools/integrate_branch.py, tools/tests/test_integrate_branch.py
- `d6742dd91` 2026-09-27 PORT P2 pick=conflict :: fix(tools): v2.0.30 - improve_ctl watch catches hook-ask stalls
  - why: improve_ctl watch flags an agent stalled on an unanswered hook ask, and args and codex-prompt validate revisions with --end-of-options.
  - port notes: Follow-up to 218488f4e; take together.
  - prereqs: 218488f4e
  - conflicts: tools/improve_ctl.py, tools/tests/test_improve_ctl.py
- `bc160ca85` 2026-09-27 PORT P2 pick=conflict :: fix(improve): v2.0.30 - workflows report stops and never guess CLEAN
  - why: The /improve workflows report BLOCKED or FAILED stops and never read a missing or empty review as CLEAN.
  - port notes: Follow-up to 1198881f0; carry the same CHANGELOG adaptation wherever its prompt text repeats the 1.5.x rule.
  - prereqs: 1198881f0
  - conflicts: tools/tests/test_improve_workflows.py
- `0cc91b320` 2026-09-27 PORT P3 pick=conflict :: test(tools): v2.0.30 - cover a never-retried failure and a URL scheme
  - why: Adds two test cases the mutation check found missing: a failed agent that was never retried and a one-letter URL scheme.
  - port notes: Test-only; take with the two tools it covers.
  - prereqs: d6742dd91, 086abc57b
  - conflicts: tools/tests/test_check_public_text.py, tools/tests/test_improve_ctl.py
- `37ddf739d` 2026-09-27 PORT-PARTIAL P3 pick=conflict :: docs(improve): v2.0.30 - dispatch rules: every ask form, retries
  - why: Aligns the /improve dispatch rules' HOOK-ASK list, hook-suite timeout recipe and python3 warning with the 1.5.x hooks.
  - port notes: Re-check the HOOK-ASK list against 1.4.x's own block-dangerous-git.sh and block-broad-git-add.sh (1.4.x asks on reset --hard, clean -f, branch -D, stash drop/clear, checkout and restore forms; confirm each shape). The TIMEOUT recipe's CLAUDE_PROJECT_DIR reaches the hooks only once 7b342ed59 is ported. The retry and resume line is line-agnostic.
  - prereqs: 1d1abd694

### group `harness-improve-run-pipeline`  P2  track=harness  commits=6  sim: clean=0 conflict=6
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/improve_ctl.py, tools/integrate_branch.py, tools/tests/test_improve_ctl.py, tools/tests/test_integrate_branch.py
- `dc260635b` 2026-09-27 PORT P2 pick=conflict :: docs(improve): v2.0.30 - skill text matches the fixed run tools
  - why: Brings the /improve run-pipeline skill text (SKILL.md, run-protocol, execute-and-review) in line with the fixed improve_ctl and integrate_branch tools; harness is in scope, but the pipeline it documents exists only on 1.5.x.
  - port notes: Only meaningful once the /improve run-pipeline series is ported: run-protocol.md, execute-and-review.md, dispatch-rules.md, workflows/*.js, tools/improve_ctl.py and tools/integrate_branch.py are all absent on origin/bannerlord-1.4.5, which still carries the pre-rebuild skill (with closing-the-loop.md). Port the series as one unit, ideally at its end state. Adapt for 1.4.x: dispatch rule 11 says CHANGELOG.md is generated at /release (plan 020), but 1.4.x hand-edits CHANGELOG.md under hook enforcement; subjects need the v2.0.28 label; execute-and-review's blast radius calls tools/graphify_taom.py and the merge step uses tools/check_public_text.py, both absent on 1.4.x.
  - prereqs: 5e5877bdf, 218488f4e, d648e00c0, 1198881f0, 1d1abd694, 459b63ad7, d6742dd91, 37ddf739d
- `014e23022` 2026-09-27 PORT P2 pick=conflict :: docs(improve): v2.0.30 - name integrate_branch's exit 3 in the merge steps
  - why: One-line run-protocol fix: on integrate_branch exit 3, read git status first because a merge may be in progress; it travels with the run-pipeline series.
  - port notes: run-protocol.md is new on 1.5.x (1d1abd694) and exit 3 arrived with 459b63ad7; apply only as part of the /improve run-pipeline unit.
  - prereqs: 1d1abd694, 459b63ad7
- `d24aff340` 2026-09-27 PORT P2 pick=conflict :: fix(tools): v2.0.30 - integrate_branch dry run applies the union rule
  - why: integrate_branch --dry-run now applies the real diff3 union rule (it used to report unions where the merge stops with exit 2), and a git failure after the merge starts now exits 3.
  - port notes: tools/integrate_branch.py and tools/tests/test_integrate_branch.py do not exist on origin/bannerlord-1.4.5; port with the run-pipeline unit (the tools/README.md row is a bookkeeping conflict). The default append-only set is lessons/*.md and REVIEW-LOG.md; 1.4.x still hand-edits CHANGELOG.md, so decide how the merge step treats it there.
  - prereqs: d648e00c0, 459b63ad7
  - conflicts: tools/integrate_branch.py, tools/tests/test_integrate_branch.py
- `ac1aa578b` 2026-09-27 PORT P2 pick=conflict :: fix(tools): v2.0.30 - improve_ctl watch and args path fixes
  - why: improve_ctl watch stops reading killed workflows under a session folder (one killed run made it exit 1 forever), and args now refuses non-absolute paths, which Git Bash mangles.
  - port notes: tools/improve_ctl.py and tools/tests/test_improve_ctl.py are absent on origin/bannerlord-1.4.5; port with the run-pipeline unit.
  - prereqs: 218488f4e, d6742dd91
  - conflicts: tools/improve_ctl.py, tools/tests/test_improve_ctl.py
- `56e9970fd` 2026-09-27 PORT P2 pick=conflict :: docs(improve): v2.0.30 - merge, cleanup and watch text match the tools
  - why: Run-protocol, dispatch-rules and execute-and-review text catch up with the fixed tools: branch cleanup order, merge exit codes, diff --cached --check and quoted paths for Git Bash.
  - port notes: Text only. SKILL.md and plan-template.md exist on 1.4.x but in their pre-rebuild form, so these hunks will not apply before 1d1abd694; apply within the run-pipeline unit.
  - prereqs: 1d1abd694, 37ddf739d, dc260635b, d24aff340, ac1aa578b
- `6c4b0981b` 2026-09-27 PORT P2 pick=conflict :: docs(improve): v2.0.30 - quote the last path templates, exit 1 wording
  - why: Final convergence text fixes: quoted TEMP and commit -F templates, the exit 1 wording in integrate_branch, and an absolute run folder.
  - port notes: Part of the run-pipeline unit only; its integrate_branch.py hunk needs that file.
  - prereqs: 56e9970fd, d24aff340
  - conflicts: tools/integrate_branch.py
skipped in this group: `4f1a7ed9d` SKIP-BOOKKEEPING docs(improve): v2.0.30 - close the 2026-09-23 run's PROGRESS

### group `harness-codex-effort`  P2  track=harness  commits=1  sim: clean=0 conflict=1
- `ef9559b14` 2026-09-28 PORT-PARTIAL P2 pick=conflict :: docs(skills): v2.0.30 - Codex effort is sized to each review
  - why: Codex dispatch templates pass a per-review -c model_reasoning_effort instead of always running at the config's ultra; both lines pin gpt-6-astra at ultra, so 1.4.x gets the same cut in Codex review time and cost.
  - port notes: Take the nine files that exist on origin/bannerlord-1.4.5: the codex-verify, deep-review, review-codex and ship skills, the .codex/config.toml comment, completion-workflow.md, new-culture-authoring.md, codex-integration.md and REVIEW-GUIDE.md. The 1.4.x config.toml has the same model and effort lines as the 1.5.x parent. Leave the .claude/skills/improve/references/execute-and-review.md hunk unless the /improve run-pipeline unit (1d1abd694) is ported first. Expect three docs conflicts.

### group `harness-model-effort-routing`  P2  track=harness  commits=2  sim: clean=0 conflict=2
- `fdec324f8` 2026-10-08 PORT P2 pick=conflict :: docs(ai): v2.0.34 - route subagents by model and effort tier
  - why: Adds the model and effort routing rule and three tier agents so subagent work goes to the cheapest model that does it well; the harness is shared by sessions on both lines.
  - port notes: New files (.claude/rules/model-effort-selection.md, agents architect, fast-reader, implementer) are absent on 1.4.5 and apply as is; replace the 1.4.x CLAUDE.md 'Model Routing' table with the pointer and add the rules-catalog and rule-provenance rows, checking the 1.4.x CLAUDE.md lint budget.
- `d0e4bc947` 2026-10-08 PORT P2 pick=conflict :: docs(ai): v2.0.34 - give each specialist agent its own tier
  - why: Gives the five existing specialist agents, all present on 1.4.5, an explicit model and effort tier.
  - port notes: Applies after fdec324f8, which creates model-effort-selection.md on 1.4.x.
  - prereqs: fdec324f8

### group `harness-skill-usage-audit`  P2  track=harness  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/generate_new_faction_kingdoms.py, tools/lint_docs.py, tools/rebalance_lords.py, tools/test_hooks.sh, tools/validate_mesh_refs.py
- `8b1083127` 2026-10-08 PORT P2 pick=conflict :: chore(skills): v2.0.34 - remove 14 skills the models never used
  - why: Removes 14 Claude Code skills that seven weeks of transcripts on this machine showed were never invoked, cutting their eagerly loaded descriptions; all 14 still exist on origin/bannerlord-1.4.5.
  - port notes: Rewrite the 1.4.x CLAUDE.md routing rows that name removed skills (/scope-check, /deslop, /context-save, /context-restore, /humanizer, /migration-status, /codex-verify, /skill-stocktake and others) and move their unique lines where the commit moved them; docs/ai-includes/git-and-commits.md, one destination, is absent on 1.4.5, as is docs/reviews/skill-usage-audit-2026-10-08.md. Heavy doc conflicts expected (27 bookkeeping files).
- `d6b537092` 2026-10-08 PORT P2 pick=conflict :: chore(skills): v2.0.34 - remove the last 14 unused skills
  - why: Second half of the skill usage audit: removes 14 more wrapper skills (13 exist on 1.4.5; new-map-prop does not) and moves their unique rules into the docs that own them.
  - port notes: Moves context-budget's scan.sh to tools/context_budget_scan.sh and lands retry-budget, binding-gate, XSLT vanilla-file-map and commit-grouping text in other docs; the API snapshot README exists on 1.4.5 but docs/ai-includes/git-and-commits.md does not, so check each destination. Rewrite the 1.4.x CLAUDE.md routing rows for /build-fix, /research, /taom-src, /verify-bindings, /xslt-check, /commit-split, /context-budget and the authoring skills. Heavy conflicts expected (41 bookkeeping files plus tools/lint_docs.py and tools/test_hooks.sh).
  - prereqs: 8b1083127
  - conflicts: tools/generate_new_faction_kingdoms.py, tools/lint_docs.py, tools/rebalance_lords.py, tools/test_hooks.sh, tools/validate_mesh_refs.py

### group `harness-claude-code-alignment`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .vscode/mcp.json.example
- `b84dbaeb2` 2026-10-08 PORT P2 pick=conflict :: chore(harness): v2.0.34 - align the harness with Claude Code 2.1.292
  - why: Aligns the harness with the installed Claude Code (subagent spawn depth 1, unused plugins off, updated harness facts) and stops the VS Code 'Run Tests' task deploying into the game, which the 1.4.5 tasks.json still does with plain dotnet test TAOM.Tests.
  - port notes: settings.json env and enabledPlugins, harness-facts.md, powershell-tool.md, mcp-servers.md and the two skills' Codex timeouts are machine facts, so they hold for 1.4.x; .vscode/mcp.json.example exists on 1.4.5 and goes. The model-effort-selection.md hunk needs fdec324f8. Expect CLAUDE.md conflicts.
  - prereqs: fdec324f8
  - conflicts: .vscode/mcp.json.example

### group `harness-prompt-audit`  P3  track=harness  commits=1  sim: clean=0 conflict=1
- `9e9f04df4` 2026-10-08 PORT-PARTIAL P3 pick=conflict :: docs(ai): v2.0.34 - fix stale facts in skills, agents and rules
  - why: Prompt-audit fixes to skills, agents and rules that the repo contradicts; most apply to 1.4.x, a few are 1.5.x-specific.
  - port notes: Take the generic fixes: taleworlds-researcher's ILSpy MCP calls its tool list never granted, /freeze, /unfreeze and /localize matching their scripts, gamemodels, harmony-patches and provenance claims the repo disproves, removal of 'think harder' prompts, and CI mentions (1.4.x removed CI in ccef172ad). Leave the v1.5.4 engine pin (1.4.x pins v1.4.8) and the /issue rewrite, which needs tools/check_public_text.py (5e5877bdf, 1.5.x-only). architect.md comes from fdec324f8; deep-review lenses/tooling.md and the improve references dispatch-rules.md and run-protocol.md are absent on 1.4.5.
  - prereqs: fdec324f8

### group `harness-builder-brief-template`  P3  track=harness  commits=1  sim: clean=1 conflict=0
- `8c1305e20` 2026-10-14 PORT P3 pick=clean :: docs(ai): v2.0.34 - builder template asks for docs, bounds, true text
  - why: Adds four rules to the Feature Developer spawn template in docs/ai-includes/agent-teams.md (docs move with code, bound config-derived values, true result messages, one writer per saved marker): provider-neutral harness guidance that applies on 1.4.x.
  - port notes: Clean pick, five added lines. Re-subject to v2.0.28.
  - issues: 769

### group `feature-hard-questions`  P3  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs
- `f382ff6fd` 2026-10-19 PORT-PARTIAL P3 pick=conflict :: feat(prisoners): v2.0.35 - Hard Questions for captive lords
  - why: Hard Questions is a new feature, but it adds a MANDATORY section to .claude/rules/adapters.md with two engine facts that bind shared code: relation changes land on the pair from DiplomacyModel.GetHeroesForEffectiveRelation, and a write made from a party-screen conversation is reverted by Escape unless the screen is committed.
  - port notes: Take only the .claude/rules/adapters.md 'Relations and party-screen writes' section and the matching engine entries in docs/reviews/lessons/adapters-taleworlds-api.md, after re-checking on v1.4.8 (ChangeRelationAction.cs:22 calls GetHeroesForEffectiveRelation; PartyScreenData.ResetUsing is at PartyScreenData.cs:231). Leave the feature, adapters, strings, MCM settings and RCA.
  - conflicts: Main/Composition/FeatureModules.cs
  - issues: 896
skipped in this group: `328ef9fc1` SKIP-FEATURE feat(prisoners): v2.0.35 - Hard Questions counts the clans it angers; `79528cc81` SKIP-1.5-ONLY-FIX fix(hard-questions): v2.0.35 - no hard questions on the ransom screen

### group `issue-962-harness-retro`  P2  track=harness  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Dependencies/Foundation/ShieldCoverage.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs, tools/test_hooks.sh
- `c8a3ac9d9` 2026-10-21 PORT P2 pick=conflict :: feat(harness): v2.0.35 - harness retro measures harness changes
  - why: Harness self-measurement (the tools/harness_retro.py transcript miner, the /harness-retro skill, a session-start cadence probe and tests) is in scope with no 1.5.x code dependency; its first retro found detect-docs-gaps.sh and the git gates being killed by their timeouts.
  - port notes: Expect conflicts in tools/test_hooks.sh, .claude/hooks/session-start.sh (the 1.4.x version differs) and the bookkeeping docs. The 1.5.x retro and adoption records describe 1.5.x sessions: port them as history or write a fresh 1.4.x record, otherwise session-start prints HARNESS RETRO DUE. harness_retro.py parses hook commands written as "$CLAUDE_PROJECT_DIR"/.claude/hooks/x.sh, while 1.4.x settings.json uses .claude/hooks/x.sh: check its hook-name parser. Register the new skill in the CLAUDE.md routing table and skill count, and add a hand-edited CHANGELOG entry on 1.4.x.
  - conflicts: tools/test_hooks.sh
  - issues: 962
- `6ce20f6ed` 2026-10-21 PORT-PARTIAL P2 pick=conflict :: feat: Add TAOM-Coop compatibility and improve Bash command length handling
  - why: A mixed commit. Its harness half (Bash length gate, faster detect-docs-gaps.sh, tests) applies to 1.4.x, whose detect-docs-gaps.sh still spawns a pipeline per feature folder. Its co-op shield changes were reverted by bd1ac8292, and its 33 banner tpacs duplicate 8cdb11408 and ab338de2d.
  - port notes: Take: .claude/hooks/check-bash-length.sh (the 1.4.x _pybin.sh already defines taom_pybin_degraded), the single-sed rewrite of detect-docs-gaps.sh, the session-start.sh gate list, the CLAUDE.md Bash-length line, the tools/test_hooks.sh cases, the hooks-catalog row and the build-tooling lesson. Leave: the co-op changes in Dependencies/Foundation, CoopSessionProvider and Patch69 (bd1ac8292 reverts them; see it for the net residue); .claude/agents/deep-reviewer.md (absent on 1.4.x); the co-op gap ledger and provenance rows; and the BannerIcons tpacs (identical blobs; take them from 8cdb11408 and ab338de2d). The harness-retro record hunk needs c8a3ac9d9. The hook is registered in 1c622732e. superseded_by applies only to the co-op part.
  - prereqs: c8a3ac9d9
  - superseded_by: ['bd1ac8292']
  - conflicts: Dependencies/Foundation/ShieldCoverage.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs, TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs, tools/test_hooks.sh
- `1c622732e` 2026-10-21 PORT-PARTIAL P2 pick=conflict :: fix(settings): increase timeout values for hooks and add PYTHONIOENCODING
  - why: Raises the PreToolUse git-gate timeouts to 30 s (the retro found them killed, and so failing open, about 400 times in a month), adds PYTHONIOENCODING=utf-8, and registers the Bash length gate; 1.4.x has the same 5 s and 10 s gates in a diverged settings.json.
  - port notes: Re-apply by hand; do not cherry-pick. The 1.4.x settings.json has no "$CLAUDE_PROJECT_DIR" prefix, matches "Bash" rather than "Bash|PowerShell", still registers check-changelog-changed.sh, and has no check-graphify-usage.sh or upstream-watch.sh. Raise each gate 1.4.x registers at 5 s or 10 s to 30 s (block-no-verify, validate-push, check-changelog-changed, check-claude-files-tracked, check-commit-subject-version, block-dangerous-git, block-broad-git-add, and config-protection if present). Add the env var, register check-bash-length.sh once it is ported, and update the hooks-catalog timeouts.
  - prereqs: 6ce20f6ed
- `ae7c4edbf` 2026-10-21 PORT P2 pick=conflict :: test(hooks): v2.0.35 - hook suite covers the Bash length gate
  - why: Hook-suite coverage for the Bash length gate, plus timeout notes in hook-authoring.md and the hooks catalog; it should follow the gate wherever the gate is ported.
  - port notes: Port with 6ce20f6ed and 1c622732e; the retro-record hunk needs c8a3ac9d9. The '1,035 passed' count comes from a 1.5.x run: re-run bash tools/test_hooks.sh on 1.4.x.
  - prereqs: 6ce20f6ed, 1c622732e, c8a3ac9d9
  - conflicts: tools/test_hooks.sh
  - issues: 962

### group `harness-deep-review-risk-tier`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Adapters/FrontEndRuntimeSprite.cs, Client/Features/FactionUI/UI/Widgets/FilteredPropertyRow.cs, Client/Features/FactionUI/UI/Widgets/FrontEndRichTextWidget.cs, Client/Features/FactionUI/UI/Widgets/FrontEndTextWidget.cs, Client/Features/FactionUI/UI/Widgets/NarrativeRandomButton.cs, Client/Features/FactionUI/UI/Widgets/ScrollShortcutButton.cs, Client/Features/FactionUI/UI/Widgets/StageJumpButton.cs, Client/Features/FactionUI/UI/Widgets/ThemeIconWidget.cs
- `0a8d61a4e` 2026-10-22 PORT-PARTIAL P2 pick=conflict :: docs(harness): v2.0.35 - size /deep-review to the risk of the change
  - why: Sizes /deep-review by a risk tier that picks the Codex effort and which lenses run, a harness efficiency change that fits either line; the commit also swept up 30 staged ADR-013 file moves by mistake.
  - port notes: Take only .claude/skills/deep-review/SKILL.md. Leave every Client/ and Main/ rename (ADR-013 B4a moves, completed by 10f1f0b80). The 1.4.x skill is still the monolithic version (origin/bannerlord-1.4.5 SKILL.md:10 '5 core agents', :16 'Step 0 (Optional)', no lenses/ folder), so the tier table needs fa8c06f82 (2026-09-18 lens split) first or must be rewritten against the 1.4.x agent list. The rule is still live at the 1.5.x tip with later refinements (2c737d97c and the 2026-11-03 harness corrections): port the tip state of the skill rather than replaying each commit. Check that the 1.4.x Codex dispatch accepts the ultra/xhigh effort names and that the paid-dispatch wording matches .ai/policy.md.
  - prereqs: fa8c06f82
  - conflicts: Client/Adapters/FrontEndRuntimeSprite.cs, Client/Features/FactionUI/UI/Widgets/FilteredPropertyRow.cs, Client/Features/FactionUI/UI/Widgets/FrontEndRichTextWidget.cs, Client/Features/FactionUI/UI/Widgets/FrontEndTextWidget.cs, Client/Features/FactionUI/UI/Widgets/NarrativeRandomButton.cs, Client/Features/FactionUI/UI/Widgets/ScrollShortcutButton.cs, Client/Features/FactionUI/UI/Widgets/StageJumpButton.cs, Client/Features/FactionUI/UI/Widgets/ThemeIconWidget.cs

### group `issue-985-rtk-adoption`  P2  track=harness  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/harness_retro.py, tools/test_hooks.sh, tools/tests/test_harness_retro.py, tools/tests/test_shellwords.py
- `caa7ac356` 2026-10-23 PORT-PARTIAL P2 pick=conflict :: fix(hooks): v2.0.35 - git gates read past wrapper words and grammar
  - why: The 1.4.x git gates still find git only as a segment's first word (block-dangerous-git.sh needs ^git after env assignments), so timeout/env/nice wrappers, subshells and brace groups slip past the destructive-git, broad-add and commit-label gates and the verification marker.
  - port notes: _shellwords.py and tools/tests/test_shellwords.py are absent on 1.4.x: created on 1.5.x by b23dc8999 (plan 027, read PowerShell commands as Bash text) and reworked by d2fa6d081, a4ba6b0b3, 60d85ed7d, fff6d66e9, 7e26d77fe and 407570ece before this commit (and by 09f3126a7, 9cfcadd07, 434d1d795 after). The 1.4.x _pybin.sh has only taom_pybin_is_safe/probe/resolve_python/degraded; 1.5.x adds taom_hook_command, taom_hook_segments, taom_git_repo_dir and others, which the commit-label gate uses. Easiest port: lift the final _shellwords.py, its test and the _pybin.sh helpers from the 1.5.x tip, then rewire block-dangerous-git.sh, block-broad-git-add.sh, mark-verification-run.sh and check-commit-subject-version.sh plus tools/test_hooks.sh cases, instead of replaying each commit. Keep 1.4.x-only gates (CHANGELOG, v2.0.28 label). Take the hook-authoring.md line and hooks-catalog rows. Run bash tools/test_hooks.sh after.
  - prereqs: b23dc8999
  - conflicts: tools/test_hooks.sh, tools/tests/test_shellwords.py
  - issues: 985
- `c64c78e72` 2026-10-23 PORT P3 pick=conflict :: feat(tools): v2.0.35 - harness_retro reports tool output size
  - why: Adds a tool-output-size section to harness_retro.py, a harness measurement tool that does not exist on 1.4.x at all.
  - port notes: Not cherry-pickable alone: tools/harness_retro.py, its test, the harness-retro skill and docs/ai-includes/harness-retro.md were created on 1.5.x by c8a3ac9d9 and grew through 78fe34c16, 0985c9515, d94bddecb, ad3967222, 4358d1622, 3f53e4623 and 69d2e8c0a; its command families reuse _shellwords from caa7ac356. If the maintainer wants the retro on this branch, port the whole tool from the 1.5.x tip in one go.
  - prereqs: c8a3ac9d9, caa7ac356
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py
  - issues: 985
- `549bf3902` 2026-10-23 PORT P3 pick=conflict :: docs(reviews): v2.0.35 - rtk adoption review and its records
  - why: Review record, provenance-register row, adoption precedent and a hook lesson on first-word anchors for the in-scope #985 harness work; every target file except the new review record exists on 1.4.x.
  - port notes: Port alongside caa7ac356 and c64c78e72; if harness_retro is not ported, trim the record's references to it.
  - prereqs: caa7ac356
  - issues: 985
- `6e59b4d6a` 2026-10-23 PORT P3 pick=conflict :: docs: v2.0.35 - index and tools catalog name the rtk review
  - why: One-line docs/INDEX.md entry for the rtk review and a tools/README.md row update for harness_retro.py, index bookkeeping for in-scope harness work.
  - port notes: Fold into the 781/783 port; the tools/README.md row applies only once harness_retro.py exists on 1.4.x.
  - prereqs: 549bf3902
  - issues: 985

### group `harness-retro-tool`  P2  track=harness  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/harness_retro.py, tools/tests/test_harness_retro.py
- `78fe34c16` 2026-10-24 PORT P2 pick=conflict :: feat(tools): v2.0.35 - harness retro flags output growth by itself
  - why: Harness measurement tool gains an automatic output-growth check on section 9; harness is in scope, but every file it touches except tools/README.md is absent on origin/bannerlord-1.4.5.
  - port notes: Port after c8a3ac9d9 (introduces tools/harness_retro.py and the harness-retro skill) and c64c78e72 (adds section 9, tool output size). The 3 lines in docs/reviews/adopt-rtk-2026-10-23.md need that record (549bf3902); drop them if it is not ported. tools/README.md conflicts as bookkeeping. The tool reads machine-wide Claude transcripts, so it is line-agnostic: low marginal value on 1.4.x.
  - prereqs: c8a3ac9d9, c64c78e72
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py
  - issues: 985
- `0985c9515` 2026-10-26 PORT P2 pick=conflict :: feat(tools): v2.0.35 - harness retro shows which files Read spends on
  - why: harness_retro.py adds a Top files read table and a file-growth check for Read output, the largest share of tool output.
  - port notes: Port after 78fe34c16 and its own prerequisites (c8a3ac9d9, c64c78e72). The series continues outside this batch with c237c84e4, d94bddecb, ad3967222, 4358d1622, 3f53e4623 and 69d2e8c0a.
  - prereqs: 78fe34c16
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py
  - issues: 1002
- `d94bddecb` 2026-10-26 PORT P3 pick=conflict :: feat(tools): v2.0.35 - harness retro shows tokens by agent and model
  - why: Extends harness_retro.py section 10 with tokens by agent type and model; the whole harness retro tool and skill are absent on 1.4.x.
  - port notes: Applies as is once the harness retro itself is ported: tools/harness_retro.py first appears in c8a3ac9d9, and section 10 comes from c237c84e4. The retro mines this project's Claude Code session transcripts, which both branches share in one checkout. Running it from a 1.5.x checkout already covers 1.4.x sessions, so port only if 1.4.x work happens without a 1.5.x checkout.
  - prereqs: c8a3ac9d9, c237c84e4
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py
  - issues: 1008
- `b6f0ec794` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.35 - Paperclip adoption review and its records
  - why: Paperclip adoption record; only its provenance-register row matters, and only if d94bddecb's section 10 code is ported.
  - port notes: Take the Paperclip row in docs/reference/provenance-register.md, which credits harness_retro.py section 10. The review record docs/reviews/adopt-paperclip-2026-10-26.md is optional. Skip the #1009 changelog-dedupe narrative (1.5.x-only process) and the docs/INDEX.md churn. Take nothing if d94bddecb is not ported.
  - prereqs: d94bddecb
  - issues: 1008, 1009
- `3f53e4623` 2026-11-03 PORT P3 pick=conflict :: feat(tools): v2.0.36 - harness_retro can leave a session out
  - why: It adds --exclude-session to tools/harness_retro.py, a harness measurement tool that the 1.4.x line does not have yet.
  - port notes: This only means something once harness_retro.py itself is ported. c8a3ac9d9 added it, and c64c78e72, 78fe34c16, 0985c9515, c237c84e4, d94bddecb, ad3967222 and 4358d1622 changed it before this commit. Take that chain in order and this commit then applies to the same file. The tool reads the machine's Claude transcripts, which both lines share when they run from the same checkout folder, so the maintainer may decide the 1.5.x copy is enough. The tools/README.md row conflict is bookkeeping.
  - prereqs: c8a3ac9d9, c64c78e72, 78fe34c16, 0985c9515, c237c84e4, d94bddecb, ad3967222, 4358d1622
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py
  - issues: 1078

### group `harness-ralph-adoption`  P2  track=harness  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/lint_docs.py, tools/perf_runs.py, tools/test_hooks.sh, tools/tests/test_improve_workflows.py, tools/tests/test_lint_docs.py, tools/tests/test_perf_runs.py
- `4407fbc05` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: Enhance documentation and tooling with various improvements
  - why: Mixed harness commit (ralph adoption rules, a lint_docs gate requiring a verify section in new feature docs, a session-start worktree count, perf_runs inconclusive groups); in scope, but several parts sit on post-fork 1.5.x harness code.
  - port notes: Take: hook-authoring.md digits-only arithmetic rule, simplicity-criterion.md speculative-guard row, working-discipline.md loop breaker and root-cause line, evidence-over-claims.md edit, issue SKILL template, external-repo-adoption row plus the adopt-ralph record, hooks-catalog row, test_hooks.sh cases, and lint_docs.py check_feature_doc_verify_section with tests (1.4.x lint_docs.py has _git_out at :991 but no --staged/--drift-only mode, which 1.5.x added in c79a58521; run the gate in working-tree mode or port that mode). Session-start worktree block: needs ss_probe_budget (407570ece) and names tools/worktree_audit.py (arrives in 2fb9d914a); take it only with 5eacdedc1's WT_MAX=15, never the 1000 shipped here. Leave unless their bases are ported: execute-and-review.md (/improve rebuild 1d1abd694) and perf_runs.py inconclusive handling (tool from 67eff55cf, which parses MissionPerf logs). The CLAUDE.md item 8 hunk has no target: 1.4.x's CLAUDE.md has no resume-card item. The verify-section gate pairs with 2fb9d914a's TEMPLATE.md section and is fixed by 88c0b6985 and bc7a7f0d9.
  - prereqs: 407570ece, 2fb9d914a
  - conflicts: tools/lint_docs.py, tools/perf_runs.py, tools/test_hooks.sh, tools/tests/test_lint_docs.py, tools/tests/test_perf_runs.py
- `4d17aeab8` 2026-10-24 PORT-PARTIAL P3 pick=conflict :: docs(harness): v2.0.35 - ralph port gets dead ends and breaker fixes
  - why: Finishes the ralph adoption (#996): tighter loop and run breakers, a Dead ends section for resumed runs, the bash arithmetic idiom.
  - port notes: Take the hook-authoring.md and working-discipline.md edits and the adopt-ralph record after 4407fbc05. execute-and-review.md and run-protocol.md need the /improve rebuild (1d1abd694). Drop the CLAUDE.md item 8 edit: 1.4.x's CLAUDE.md has no resume-card item.
  - prereqs: 4407fbc05
  - issues: 996
- `5eacdedc1` 2026-10-24 PORT P2 pick=conflict :: fix(session-start): reduce maximum worktree count to 15 for session budget management
  - why: One-line fix to the session-start worktree check from 4407fbc05, which shipped WT_MAX=1000 although its own comment says it warns above 15.
  - port notes: Squash into the 4407fbc05 port of the worktree block; 1.4.x's session-start.sh has no WT_MAX today.
  - prereqs: 4407fbc05
- `a7bbf3fe7` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.35 - ported ralph rules now enforce themselves
  - why: Makes the ralph rules self-enforcing: tools/loop_progress.py loop breaker, a test_hooks.sh arithmetic probe across every hook, compact-time Dead ends from session-start.sh, and an /improve run breaker in execute.js.
  - port notes: Take tools/loop_progress.py and its tests (new, standalone), test_hooks.sh section 4f (1.5.x probes 26 hooks; enumerate 1.4.x's own set), the compact-time Dead ends block in session-start.sh (1.4.x reads SOURCE at :9-11 and exits on non-startup at :15, so insert it before that exit), the working-discipline.md, hook-authoring.md and codex-operating-guide.md edits, and the hooks-catalog and tools/README rows. Leave execute.js, tools/tests/test_improve_workflows.py, execute-and-review.md and run-protocol.md unless the /improve pipeline (1d1abd694, 1198881f0) is ported.
  - prereqs: 4407fbc05, 4d17aeab8
  - conflicts: tools/test_hooks.sh, tools/tests/test_improve_workflows.py
  - issues: 996, 998

### group `harness-pstack-adoption`  P2  track=harness  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/lint_docs.py, tools/test_hooks.sh, tools/tests/test_lint_docs.py, tools/tests/test_worktree_audit.py, tools/worktree_audit.py
- `2fb9d914a` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: docs(harness): v2.0.35 - pstack review adds checks the workflow runs
  - why: The pstack adoption puts seven checks into existing steps: a two-edit test judgment and ADR-009 comment review in /deep-review, a "How to verify in game" feature-doc section, /investigate premise testing, tools/worktree_audit.py, and docs/ai-includes/perf-method.md.
  - port notes: Take output-style.md, tests.md, the investigate and ship SKILL edits, code-quality, completion-workflow, external-repo-adoption, INDEX, TEMPLATE and dev-console docs, perf-method.md (directly useful to the 1.4.8 perf deep-dive: median of five alternating runs, stop rule, attempt log), the build-tooling-workflow lesson, the adopt-pstack record and tools/worktree_audit.py with tests. 1.4.x's /deep-review is a single SKILL.md (no lenses/ folder; 1.5.x added it in fa8c06f82), so write the two lens rules there or port fa8c06f82 first. The run-protocol.md line needs 1d1abd694; the mission-perf-heartbeat.md line needs that 1.5.x MissionPerf doc. Pairs with 4407fbc05's verify-section gate and 88c0b6985's fixes.
- `88c0b6985` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - pstack gates catch one-command commits
  - why: Fixes the pstack gates: the new-feature-doc check missed a one-command `git add && git commit`, the verify section needed a body, and worktree_audit.py misjudged nested worktrees and rewrote other worktrees' index files through a plain git status.
  - port notes: Take the lint_docs.py --also-new and fenced-heading fixes with tests, the worktree_audit.py fixes with tests (the --no-optional-locks status matters), the test_hooks.sh cases and the session-start wording (1.4.x's older line at session-start.sh:107 still tells agents to prune abandoned worktrees). The check-doc-config-drift.sh hunk targets 1.5.x's `lint_docs.py --drift-only --staged` call (from c79a58521); 1.4.x's hook calls `--fail-on-drift` at :91, so adapt the --also-new plumbing to that call. Adapt the probe-count comment to 1.4.x's session-start probes.
  - prereqs: 4407fbc05, 2fb9d914a
  - conflicts: tools/lint_docs.py, tools/test_hooks.sh, tools/tests/test_lint_docs.py, tools/tests/test_worktree_audit.py, tools/worktree_audit.py
- `28de0dc7d` 2026-10-26 PORT P3 pick=conflict :: docs: v2.0.35 - backlink footers for the pstack docs
  - why: Backlink footers for the pstack docs (perf-method.md, adopt-pstack record, mission-perf-heartbeat.md).
  - port notes: Squash into the 2fb9d914a port; the mission-perf-heartbeat.md footer needs that 1.5.x doc.
  - prereqs: 2fb9d914a
- `bc7a7f0d9` 2026-10-26 PORT P2 pick=conflict :: fix(tools): v2.0.35 - doc lint stops flagging merged-in feature docs
  - why: The new-feature-doc gate counted docs a merge parent already had as new, and the secret-scanner test's deliberate password literal tripped /security-scan as HIGH.
  - port notes: The lint_docs.py merge-parent fix and its two tests need 4407fbc05's gate. The test_audit_repo_secrets.py audit-allow marker stands on its own: 1.4.x carries the same unmarked literal at :178, and 1.4.x's audit_claude_config.py already honours `audit-allow:` (:66, :123-125).
  - prereqs: 4407fbc05
  - conflicts: tools/lint_docs.py, tools/tests/test_lint_docs.py
- `21f2ba79d` 2026-10-26 PORT P3 pick=conflict :: docs: v2.0.35 - perf method checks a faster result independently
  - why: Perf method section 4: check a faster path against something the change did not touch, never a second copy of the changed code.
  - port notes: perf-method.md arrives with 2fb9d914a and the adopt-rea record with fc5408508; provenance-register.md exists on 1.4.x.
  - prereqs: 2fb9d914a, fc5408508

### group `harness-context-mode-adoption`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `9d327f43e` 2026-10-26 PORT P3 pick=conflict :: docs(reviews): v2.0.35 - context-mode adoption review and its records
  - why: Adoption review of context-mode (vetted, not installed) that adds its "think in code" principle as item 6 of the agent operating manual.
  - port notes: Every edited file except the new review record exists on 1.4.x (agent-operating-manual.md, external-repo-adoption.md, provenance-register.md, INDEX.md); expect bookkeeping conflicts only. Check that 1.4.x's operating manual numbering leaves item 6 free. The 0.3% re-read figure was measured on 1.5.x sessions.

### group `issue-1003-reviewer-rule-rereads`  P2  track=harness  commits=1  sim: clean=0 conflict=1
- `9a2a8dae9` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: docs(review): v2.0.35 - reviewers stop re-reading rules they loaded
  - why: Harness token saving (#1003): a reviewer reads a paths: rule only if it has not already loaded, but the lens, agent and manual files it edits are 1.5.x-only, so the principle needs re-homing on 1.4.x.
  - port notes: The edited files are absent on 1.4.x. .claude/agents/deep-reviewer.md and .claude/skills/deep-review/lenses/* come from fa8c06f82. Agent-operating-manual item 6 comes from 9d327f43e; the 1.4.x manual has only five items. If those bases are not ported, carry the one principle (Read a paths: rule only if it has not arrived, then only the needed section) into the 1.4.x deep-review SKILL.md agent prompts and the manual. Also take the docs/reviews/lessons/build-tooling-workflow.md entry. Drop the adopt-context-mode doc edit and the harness_retro references unless c8a3ac9d9 is ported.
  - prereqs: fa8c06f82, 9d327f43e
  - issues: 1003

### group `harness-plugin-marketplace-checks`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/harness_retro.py, tools/tests/test_harness_retro.py
- `c237c84e4` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.35 - plugin marketplace review adds five checks
  - why: Adds a frontmatter-key allowlist to the hook suite, a /verify canary test and review-lens checklists; the allowlist and canary stand alone on 1.4.x, while the lens and harness_retro parts need 1.5.x-only bases.
  - port notes: Take:
- tools/test_hooks.sh section 3b: SKILL_KEYS/AGENT_KEYS allowlist and the name-equals-folder check. 1.4.x has the same 3b frontmatter loop at origin/bannerlord-1.4.5:tools/test_hooks.sh:182-252.
- The two allowlist rows in .claude/rules/harness-facts.md. The 1.4.x row stops at the 2026-07-18 field list and lacks background, metadata, license and compatibility.
- TAOM.Tests/Infrastructure/VerifyCanaryTests.cs and /verify Step 2c.

Adapt:
- The 'total under 20,000 tests' floor must be re-measured on 1.4.x.
- Run the new 3b check first: any 1.4.x skill or agent key off the allowlist will fail it.

Leave:
- The deep-review lens edits (need fa8c06f82).
- harness_retro.py section 10 and its tests (need c8a3ac9d9).
- The review record is optional.
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py
skipped in this group: `54ec60973` SKIP-BOOKKEEPING docs: v2.0.35 - backlink footer for the plugin marketplace review

### group `issue-987-tool-followups`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/audit_mbproj_registration.py, tools/lint_docs.py, tools/test_hooks.sh, tools/tests/test_audit_mbproj_registration.py, tools/tests/test_lint_docs.py, tools/tests/test_shellwords.py, tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py
- `09f3126a7` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - exact mbproj ids, hook judges what commits record
  - why: Precision fixes to the project.mbproj audit, the doc linter's dash check and the doc-config drift hook; the audit's id table is v1.5.5-sourced and the hook needs 1.5.x-only helpers.
  - port notes: Re-implement rather than cherry-pick. By git diff --stat, 1.4.x differs from the 1.5.x parent by about 762 changed lines in tools/lint_docs.py, 3,013 in tools/test_hooks.sh, 180 in tools/audit_mbproj_registration.py and 123 in .claude/hooks/check-doc-config-drift.sh.

Portable alone:
- lint_docs dash check: a closing fence must be at least as long as its opener (1.4.x has the dash check and --dash-base).
- The gating git-diff-failed finding for an unresolvable --dash-base.
- --only-paths.

Audit:
- REQUESTED_IDS is a table sourced from the v1.5.5 engine; re-derive it from v1.4.8 before porting.
- The BOM-aware decode and 'exit 2 when nothing was audited' are engine-neutral.
- Builds on bc30b76da.

Hook: the drift-hook rewrite needs .claude/hooks/_shellwords.py (b23dc8999).

XSD: the validate_xml_schemas.py edit needs fa8c06f82.
  - prereqs: bc30b76da, b23dc8999, fa8c06f82
  - conflicts: tools/audit_mbproj_registration.py, tools/lint_docs.py, tools/test_hooks.sh, tools/tests/test_audit_mbproj_registration.py, tools/tests/test_lint_docs.py, tools/tests/test_shellwords.py, tools/tests/test_validate_xml_schemas.py, tools/validate_xml_schemas.py
  - issues: 983, 984, 987

### group `issue-1012-statusline-usage`  P2  track=harness  commits=2  sim: clean=1 conflict=1
- `2473fa80e` 2026-10-26 PORT P2 pick=clean :: feat(harness): v2.0.35 - status line shows the usage windows
  - why: The status line gains a usage-window line and stops ctx: from reading a rate-limit window's used_percentage; the 1.4.x statusline.sh is byte-identical to the pre-image (blob 4f1952c18).
  - port notes: Clean pick, including tools/tests/test_statusline.py. The new comment cites docs/reference/provenance-register.md for jarrodwatts/claude-hud, so take 49bf3b115's register row with it.
  - issues: 1012
- `49bf3b115` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: docs(reviews): v2.0.35 - claude-hud adoption review and its records
  - why: claude-hud adoption record; its provenance-register row backs the source named in 2473fa80e's status line comment.
  - port notes: Take the claude-hud row in docs/reference/provenance-register.md. The review record docs/reviews/adopt-claude-hud-2026-10-26.md and the external-repo-adoption.md entry are optional. Skip the docs/INDEX.md churn.
  - prereqs: 2473fa80e
  - issues: 1012

### group `harness-pstack-marketplace-review`  P2  track=harness  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/harness_retro.py, tools/lint_docs.py, tools/perf_runs.py, tools/test_hooks.sh, tools/tests/test_harness_retro.py, tools/tests/test_lint_docs.py, tools/tests/test_perf_runs.py
- `ad3967222` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: fix(tools): v2.0.35 - review follow-ups for retro, lint and perf runs
  - why: Review follow-ups in which lint_docs.py now exits 2 on an unresolvable --dash-base and test_hooks.sh section 3b is proven able to fail; both tools exist on 1.4.x, while harness_retro.py and perf_runs.py are 1.5.x-only.
  - port notes: Take the tools/lint_docs.py _dash_base_resolves check and exit 2 with its tests, after 09f3126a7 (it added the git-diff-failed finding this commit rewrites; 1.4.x lint_docs.py has --dash-base at line 1209 but no git-diff-failed). Take the test_hooks.sh 3b planted-tree, zero-files and check-doc-config-drift amend tests as a hand port: the 1.4.x test_hooks.sh is about 3,100 lines shorter than the 1.5.x parent. Take the harness_retro.py, perf_runs.py, harness-retro.md and perf-method.md hunks only if those tools are ported (all absent on 1.4.x). Doc and README hunks are bookkeeping.
  - prereqs: 09f3126a7
  - conflicts: tools/harness_retro.py, tools/lint_docs.py, tools/perf_runs.py, tools/test_hooks.sh, tools/tests/test_harness_retro.py, tools/tests/test_lint_docs.py, tools/tests/test_perf_runs.py
- `a87bdc3a9` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: docs(harness): v2.0.35 - three review offers built, seven declined
  - why: Adds the rule that a fact a quick run can show is not a question for the user; 1.4.x has think-before-coding.md in its older long form, while deep-reviewer.md and harness-retro.md are 1.5.x-only.
  - port notes: Hand-edit 'or a quick run' into the 1.4.x think-before-coding.md 'When NOT to ask' conventions bullet (line 28 on origin/bannerlord-1.4.5). The 1.5.x file is a 2,244-byte rewrite and the 1.4.x one is 5,539 bytes, so a cherry-pick will not apply. The deep-reviewer.md and harness-retro.md lines apply only if that agent and skill are ported. The review-record hunks are bookkeeping.
skipped in this group: `c4ff1db51` SKIP-BOOKKEEPING docs: v2.0.35 - pstack review backlink from the marketplace review

### group `harness-statusline-panel`  P3  track=harness  commits=1  sim: clean=0 conflict=1
- `dfd4cf9e0` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: docs(harness): v2.0.35 - status line is terminal-only; panel uses /usage
  - why: Records the doc-backed fact that statusLine renders only in the terminal CLI, not in the VS Code graphical panel, which holds for the 1.4.x harness too, although its statusline.sh has no #1012 usage line.
  - port notes: Take the harness-facts.md row without the pointer to the #1012 usage line. Skip the statusline.sh header comment (origin/bannerlord-1.4.5 statusline.sh has no usage line) and the claude-hud review record. Mind any always-load size budget the 1.4.x hooks enforce.
  - issues: 1012

### group `harness-deep-review-lenses`  P3  track=harness  commits=2  sim: clean=0 conflict=2
- `ce7f8d6ba` 2026-10-31 PORT-PARTIAL P3 pick=conflict :: chore(review): v2.0.36 - check new mission types and engine effects
  - why: Adds a /deep-review expansion for new mission types or spawn paths, and a rule that the engine-compat lens must quote the engine line that produces each relied-on effect.
  - port notes: Put the Step 2c line into the 1.4.x .claude/skills/deep-review/SKILL.md expansion list (around lines 463-465). Put the Lens 2 effect-quote bullet into the 1.4.x inline engine-compat checklist (near line 130), because lenses/2-engine-compat.md only exists after fa8c06f82. Reword the rca-fords-of-war citations, which have no file on 1.4.x.
- `d3fc3c4d0` 2026-10-31 PORT P3 pick=conflict :: chore(review): v2.0.36 - judge self-check rules on real evidence
  - why: Adds one generic clause to /deep-review lens 4: a self-check or verdict rule needs a test on evidence its real producers made plus an injected fault, since hand-built records prove only arithmetic.
  - port notes: The lens files do not exist on 1.4.x (only .claude/skills/deep-review/SKILL.md, checked with git ls-tree); apply after fa8c06f82 (#621 lens split) is ported, otherwise fold the clause into 1.4.x SKILL.md's test-check item. The citation rca-fords-of-war-2026-10-31.md is a 1.5.x-only file: drop or reword it.
  - prereqs: fa8c06f82

### group `issue-1078-harness-usage`  P2  track=harness  commits=3  sim: clean=0 conflict=3
- `2ca35d6e2` 2026-11-02 PORT-PARTIAL P2 pick=conflict :: docs(harness): v2.0.36 - cut rule load, reviewers batch their reads
  - why: Cuts rule load by splitting moduledata-validation.md, which on 1.4.x is the second-largest rule (25,306 bytes, after csharp-architecture.md at 25,828) and loads on every tools/**/*.py and tools/**/*.ps1 read, so the same split saves context for 1.4.x sessions too.
  - port notes: Redo the split on 1.4.x's own rule instead of cherry-picking: the 1.5.x pre-commit file was 44,508 bytes (post-commit 9,455), so 1.5.x's text and its new docs/reference/moduledata-validator-codes.md carry post-fork content. Take: narrow the rule's paths to the ModuleData globs plus tools/schemas/*.json, tools/validate_moduledata.py and tools/taom_schema.py; move the 'XML I/O convention' section verbatim into a new .claude/rules/data-scripts.md scoped to tools/**/*.py and tools/**/*.ps1; move the long reference sections to a new docs/reference/moduledata-validator-codes.md; add the rules-catalog and INDEX rows. Optional: the observation that MEMORY.md reaches subagents as a row in 1.4.x harness-facts.md 'Memory file (MEMORY.md) semantics' (1.4.x CLAUDE.md has no line claiming the opposite), and the auto-compaction threshold row. Leave: the .claude/agents/deep-reviewer.md hunk (agent absent on 1.4.x, added by fa8c06f82); 1.4.x /deep-review spawns Explore and taleworlds-researcher lenses, whose prompt templates could take the 'batch your reads' sentence instead. Expect hand merges in CLAUDE.md, harness-facts.md, INDEX.md and rules-catalog.md, plus the 1.4.x CHANGELOG gate for .claude/ commits.
  - issues: 1078
- `2c737d97c` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: docs(harness): v2.0.36 - deep review effort follows the risk tier
  - why: Sets deep-review lens effort by risk tier (max for tier 1, high for tier 2 and second-wave lenses), which only exists in 1.5.x's redesigned /deep-review.
  - port notes: Meaningful only after the 1.5.x /deep-review redesign is ported: the deep-reviewer agent (fa8c06f82), the risk tiers (0a8d61a4e) and .claude/rules/model-effort-selection.md (fdec324f8). On 1.4.x /deep-review has no tiers (only 'effort: high' in its frontmatter) and spawns Explore (haiku) and taleworlds-researcher (sonnet) lenses. Without the redesign, take only the harness-facts.md fact that the Agent tool's effort parameter overrides a definition's effort: and CLAUDE_CODE_EFFORT_LEVEL beats both (DOC-backed 2026-11-02), adapted into 1.4.x's table.
  - prereqs: fa8c06f82, 0a8d61a4e, fdec324f8
  - issues: 1078
- `6a54436c6` 2026-11-03 PORT P3 pick=conflict :: docs(reviews): v2.0.36 - measure Headroom, do not install it
  - why: Records the measured rejection of the Headroom tool-output proxy (about 1% of spend saved, and it alters code a reviewer reads), a branch-independent harness decision worth keeping so 1.4.x sessions do not re-evaluate it.
  - port notes: Optional and cheap: add docs/reviews/adopt-headroom-2026-11-03.md, the provenance-register.md table row and detail section, and the external-repo-adoption.md bullet. Hand-merge docs/INDEX.md: 1.4.x's 'External adoption reviews' line (INDEX.md:240) lacks the post-fork adopt-rtk, adopt-context-mode and adopt-claude-hud entries the 1.5.x hunk sits beside, and 1.4.x's register (end marker at line 88) has none of the Gitleaks, Ruff or ShellCheck rows. The record cites tools/harness_retro.py, absent on 1.4.x unless that series is ported.
  - issues: 1078

### group `harness-retro`  P3  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .gitleaksignore, tools/harness_retro.py, tools/tests/test_harness_retro.py
- `4358d1622` 2026-11-02 PORT-PARTIAL P3 pick=conflict :: feat(tools): v2.0.36 - the retro prices usage, scans workflow agents
  - why: Adds usage pricing (report section 11) and workflow-transcript discovery to tools/harness_retro.py, a harness tool that exists only on 1.5.x.
  - port notes: Port only with the harness-retro series: c8a3ac9d9 adds tools/harness_retro.py, its test and .claude/skills/harness-retro/SKILL.md (all absent on 1.4.x); c64c78e72, 78fe34c16, 0985c9515, c237c84e4, d94bddecb and ad3967222 extend them; 3f53e4623 and 69d2e8c0a follow in batch 34. The tool imports .claude/hooks/_shellwords.py (absent on 1.4.x, added by b23dc8999) and audit_claude_config._GENERIC_SECRET and _SECRET_COMPILED (present on 1.4.x at tools/audit_claude_config.py:163-165). Leave the .gitleaksignore hunk: the file is absent on 1.4.x and the new line names a 1.5.x-only commit and Fords test. Low marginal value: the tool reads transcripts outside the repo via --transcripts DIR, so a 1.5.x checkout can already analyse 1.4.x sessions.
  - prereqs: c8a3ac9d9, c64c78e72, 78fe34c16, 0985c9515, c237c84e4, d94bddecb, ad3967222, b23dc8999
  - conflicts: .gitleaksignore, tools/harness_retro.py, tools/tests/test_harness_retro.py
  - issues: 1078

### group `tools-review-yield-provenance-loc`  P2  track=dev-tools  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/harness_retro.py, tools/tests/test_harness_retro.py
- `69d2e8c0a` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.36 - yield by lens, provenance and stale loc checks
  - why: It adds review-yield reporting, a provenance-register checker and a stale-translation checker; on 1.4.x, analyze_reviews.py, check_doc_graph_ratchet.py and doc_graph_baseline.json are identical to the 1.5.x parent, so those parts apply.
  - port notes: Take:
- analyze_reviews.py and its tests
- check_doc_graph_ratchet.py, re-measuring doc_graph_baseline.json on 1.4.x rather than taking 27 orphans and 32 components
- check_provenance.py and check_stale_loc_rows.py, both new; run them against the 1.4.x register and language files
- their /verify and /localize wiring; verify SKILL.md changed on 1.5.x, so merge it by hand
The harness_retro.py half needs the harness-retro chain (prereqs, for that half only). Removing the REVIEW-LOG.md Metrics block is bookkeeping.
  - prereqs: c8a3ac9d9, 4358d1622, 3f53e4623
  - conflicts: tools/harness_retro.py, tools/tests/test_harness_retro.py

### group `issue-621-deep-review-lenses`  P3  track=harness  commits=1  sim: clean=0 conflict=1
- `099840f33` 2026-11-03 PORT P3 pick=conflict :: docs(harness): v2.0.36 - /deep-review cites the budget rule by its home
  - why: Text-only follow-up that repoints /deep-review's wave-limit citation from a dead memory file to agent-teams.md 'Budget'.
  - port notes: Only meaningful once fa8c06f82 (#621, adds 'Waves of four' and the agent-budget-discipline citation) and 4fbfb7adc (adds the agent-teams.md Budget section) are on 1.4.x. The 1.4.x /deep-review SKILL.md and agent-teams.md have neither. Drop it otherwise.
  - prereqs: fa8c06f82, 4fbfb7adc

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 10, batch 11, batch 14, batch 16, batch 19, batch 22, batch 23, batch 25, batch 26, batch 27, batch 28, batch 31, batch 32, batch 33, batch 34, batch 35
