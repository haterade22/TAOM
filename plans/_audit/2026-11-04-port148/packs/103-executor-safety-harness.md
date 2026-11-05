# Input pack: WP 103 executor-safety-harness

Title (draft): Make the 1.4.x gates real and executor-safe: hook output contract, anchored paths, non-deploying commands, worktree-aware readers
Wave: 0   Lane: harness   Draft priority: P1   Groups: 7   Port commits: 9
Depends on (WP ids): 102

## Orchestrator notes for this WP (binding)
- Core defect (D2): nine 1.4.x PreToolUse gates print a top-level permissionDecision that Claude Code ignores, so the subject, files-tracked, ModuleData, doc-drift, confirm and /freeze gates are inert today. Port the HARNESS hunk of c79a58521 (group crash-report-rethrow-stack; the crash half belongs to WP107): nested hookSpecificOutput, PYTHONIOENCODING=utf-8 in _pybin.sh, test_hooks 5c, harness-facts row.
- Preconditions that the real gates would otherwise deny on, in the same WP: trim CLAUDE.md under its byte cap (lint_docs --fail-on-drift must exit 0), the laptop Armory refresh (WP101), and worktree-aware gate readers (12d96adc1, the reader half of 7e26d77fe/407570ece) because parallel executors commit from worktrees.
- Take the 7j frontmatter check of d01bddf10 (its push-judge half goes to WP116) and only the -p:ModuleId= hunk of 1d1abd694 (group improve-run-pipeline, WP138), plus a sweep that adds -p:DisableModuleCopy=true -p:ModuleId= to every dotnet build/test line in skills, rules and docs.
- Decision D3: deny Serena's edit tools (list in 7d13adc36) in .claude/settings.json; keep its read tools. Decision D19: widen the 10 git gates to matcher Bash|PowerShell. Add bannerlord-1.5.x to validate-push.sh protection. All settings.json edits: the maintainer applies an exact diff from the plan.
- 0382d5d2a .githooks/commit-msg: port; the maintainer runs git config core.hooksPath .githooks once.
- Decision D18: do NOT add 4b7e36250's merge=union line here (deferred to WP142).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D2-harness-and-changelog.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `issue-681-hook-herestring-hang`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh
- `143f0fa85` 2026-09-26 PORT-PARTIAL P2 pick=conflict :: fix(hooks): v2.0.30 - split hook text without here-strings
  - why: Git Bash 5.3 hangs on a here-string of 65,537 to 65,664 bytes and a killed gate fails open; 1.4.x hooks use here-strings at the same sites and this laptop runs GNU bash 5.3.9.
  - port notes: Independent of plan 027. 1.4.x here-strings: block-broad-git-add.sh:118, block-dangerous-git.sh:103, check-doc-config-drift.sh:78, check-moduledata-validation.sh:73, check-native-dll-crt.sh:68, check-verification-evidence.sh:42, detect-docs-gaps.sh:43, mark-verification-run.sh:74 (a here-string of a command substitution), validate-push.sh:48 (read -a), plus check-changelog-changed.sh:104,115, a 1.4.x-only hook (retired on 1.5.x) this commit never touched. Apply the set -f array split at every site; re-express validate-push, which on 1.4.x is the pre-plan-011 single-line judge. Port test_hooks.sh 4e (no here-string or expanding here-document in a registered script) and 7h (payloads sized into the window) against 1.4.x's own gate list. The byte-window comment correction (65,536 to 65,663 bytes of text) is in fff6d66e9.
  - conflicts: tools/test_hooks.sh
  - issues: 681

### group `issue-690-hook-registration-anchor`  P1  track=harness  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh, tools/tests/test_shellwords.py
- `7b342ed59` 2026-09-27 PORT P1 pick=conflict :: fix(hooks): v2.0.30 - anchor hook registrations on the project dir
  - why: Hook registrations named their scripts by relative path, so after a Bash cd into a subdirectory every gate failed to start and every call ran ungated; all 28 hook registrations in 1.4.x settings.json are relative and none uses CLAUDE_PROJECT_DIR.
  - port notes: Independent of plan 027: prefix each 1.4.x registration with "$CLAUDE_PROJECT_DIR"/ (the 1.4.x /freeze and /investigate frontmatter already uses ${CLAUDE_PROJECT_DIR}). Port test_hooks.sh 7j with its payload trunk changed from bannerlord-1.5.x to bannerlord-1.4.5, since 1.4.x validate-push protects only master, main and bannerlord-1.4.5. Port before other port work: the v2.0.28 subject-label and CHANGELOG gates this programme relies on go silent after any persistent cd in a main session. The statusline stays relative on both lines and is not a gate.
  - conflicts: tools/test_hooks.sh
  - issues: 680, 690
- `d01bddf10` 2026-09-27 PORT P2 pick=conflict :: fix(hooks): v2.0.30 - convergence fixes for the push guard work
  - why: Extends the 7j check to skill and agent frontmatter hook commands and corrects the claim that hooks always run in the main tree (#690), plus two push-judge doc and test-name fixes.
  - port notes: Take the 7j frontmatter check and the block-no-verify.sh comment with 7b342ed59; the _pushjudge.py comment, the tool_input offset note and the test-class rename go with the push-judge transplant.
  - prereqs: 7b342ed59, fff6d66e9
  - conflicts: tools/test_hooks.sh, tools/tests/test_shellwords.py
  - issues: 680, 690

### group `harness-non-deploying-verify`  P1  track=harness  commits=1  sim: clean=1 conflict=0
- `785b2703e` 2026-09-27 PORT P1 pick=clean :: docs(skills): v2.0.30 - non-deploying builds in /verify and Codex
  - why: /verify and /review-codex built and tested without -p:DisableModuleCopy=true -p:ModuleId=, so every run copied into the game install and failed while Bannerlord was open; the 1.4.x skills carry the same commands.
  - port notes: 1.4.x /verify SKILL.md:19 runs 'dotnet build Main --no-restore' and :27 'dotnet test TAOM.Tests --no-build'; /review-codex SKILL.md:239-240 runs 'dotnet build/test TAOM.Tests'. Also take references/prompt-fixed.md (needed by 218488f4e) and the Phase 3h fix: 1.4.x review-codex SKILL.md:246 still points at an AGENTS.md lessons section that lives in .ai/review-reference.md:42 and docs/reviews/codex-track-record.md, both present on 1.4.x. The cherry-pick simulated clean. Port before any executor runs /verify on 1.4.x (its CLAUDE.md asks for /verify before every commit).

### group `harness-commit-subject-gate`  P2  track=harness  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh
- `12d96adc1` 2026-10-05 PORT-PARTIAL P2 pick=conflict :: fix(hooks): v2.0.34 - commit-subject gate reads the target repo's version
  - why: Makes the commit-subject version gate read SubModule.xml from the repository a git -C or cd commit runs in; the 1.4.5 hook (still its original 8dabf4a64 version) always cds to the project directory (line 53), so cross-worktree commits during this port are judged against the wrong line's version.
  - port notes: Re-implement rather than cherry-pick: the 1.5.x diff sits on a hook rewritten after the fork (plan 013 fixes, PowerShell-tool gates e046bdf09, 90141357d, piped here-string parsing). Add the five tools/test_hooks.sh section-6 cases (stub worktree at v1.2.3). 7e26d77fe later moves this reader into .claude/hooks/_shellwords.py (absent on 1.4.5, created by b23dc8999) and applies it to every git gate, bundled with the upstream-review feature; a 1.4.x session may prefer to port that generalized gate part once.
  - conflicts: tools/test_hooks.sh

### group `git-multisession-safety`  P3  track=docs-process  commits=1  sim: clean=0 conflict=1
- `385b7adf6` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: docs(git): v2.0.34 - commit with a pathspec, never a bare commit
  - why: Adds a 'commit with a pathspec, never a bare git commit' rule after a commit swept up another session's staged files; the 1.4.x line also runs concurrent sessions and its git-safety table has no such row.
  - port notes: docs/ai-includes/git-and-commits.md does not exist on 1.4.x: add the row to the CLAUDE.md 'Multi-session git safety' table (mind the CLAUDE.md size budget) or to .claude/rules/working-discipline.md. The 1.4.x check-changelog-changed.sh already parses `git commit -- <paths>`, so the recommended form passes the existing hooks.

### group `harness-upstream-review`  P2  track=harness  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/test_hooks.sh, tools/tests/test_graphify_taom.py, tools/tests/test_shellwords.py
- `7e26d77fe` 2026-10-16 PORT-PARTIAL P2 pick=conflict :: feat(harness): v2.0.34 - upstream review; gates judge the right repo
  - why: The 1.4.x git gates still cd to CLAUDE_PROJECT_DIR (block-broad-git-add.sh:125, check-claude-files-tracked.sh:50, check-doc-config-drift.sh:55, check-moduledata-validation.sh:55, check-commit-subject-version.sh:53) and so judge the main checkout for a worktree commit; this fixes that and also adds the yotthani upstream-watch tooling.
  - port notes: Take the worktree-aware gate half: taom_git_repo_dir and taom_is_project_repo in _pybin.sh, the git-repo reader in _shellwords.py, the four gate edits, the version gate's parser move, tools/test_hooks.sh and tools/tests/test_shellwords.py. It builds on _shellwords.py, which 1.4.x lacks (created by plan 027, b23dc8999, plus its follow-ups), so port plan 027 first or adapt the reader standalone; the 1.4.x check-commit-subject-version.sh has diverged and its v2.0.28 enforcement must stay. Leave the upstream-watch half (tools/upstream_watch.py and its tests, the /upstream-review skill, docs/features/upstream-review.md, the session-start UPSTREAM block) unless the maintainer wants adoption reviews run from 1.4.x as well; one line should own docs/reviews/upstream-watch.json. Leave tools/tests/test_graphify_taom.py (graphify is 1.5.x-only). Later 1.5.x commits caa7ac356 (wrapper words before git) and 9cfcadd07 edit the same hooks.
  - prereqs: b23dc8999
  - conflicts: tools/test_hooks.sh, tools/tests/test_graphify_taom.py, tools/tests/test_shellwords.py
- `407570ece` 2026-10-16 PORT-PARTIAL P2 pick=conflict :: fix(hooks): v2.0.34 - upstream check in its own hook; Codex fixes
  - why: Codex follow-up to 7e26d77fe that closes three holes in the shared repository reader the git gates use (a command committing in two repositories, a cd leaking out of parentheses, pushd/popd) and moves the upstream check into its own SessionStart hook.
  - port notes: Take the _shellwords.py fixes, the deny-on-two-repositories rule in the commit gates, the broad-add incomplete-list note and the matching tools/test_hooks.sh and test_shellwords.py cases, together with 7e26d77fe's gate half. Leave .claude/hooks/upstream-watch.sh and its settings.json registration (only with the upstream-watch tooling) and the session-start.sh shared deadline: the 1.4.x session-start.sh has neither the Armory tpac probe nor the graphify probe that deadline budgets.
  - prereqs: 7e26d77fe, b23dc8999
  - conflicts: tools/test_hooks.sh, tools/tests/test_shellwords.py

### group `harness-git-commit-msg-hook`  P2  track=harness  commits=1  sim: clean=0 conflict=1
- `0382d5d2a` 2026-10-28 PORT P2 pick=conflict :: feat(git): v2.0.36 - a commit-msg hook checks every committer
  - why: Adds a tracked git commit-msg hook (.githooks/commit-msg running tools/check_commit_msg.py) so terminal and IDE commits meet the version-label, 72-character and no-AI-attribution rules that the 1.4.x CLAUDE.md already requires and only the Claude-side hook enforces.
  - port notes: Take .githooks/commit-msg, tools/check_commit_msg.py, tools/tests/test_check_commit_msg.py, the .gitattributes LF rule, and the hooks-catalog and tools/README rows. Adapt: the checker cites AGENTS.md 'Git and commits', which the 1.4.x AGENTS.md (working tree) lacks, and docs/ai-includes/git-and-commits.md is new on 1.5.x; on 1.4.x point both at CLAUDE.md 'Commits'. Align AI_TRAILER with the 1.4.x .claude/hooks/check-commit-subject-version.sh. Skip the three 1.5.x review records it edits (adopt-ecc-2026-09-29, rca-harad-repair-2026-10-15, rca-hill-troll-2026-09-26). Each clone then runs git config core.hooksPath .githooks. Porting it early makes every later port commit carry v2.0.28; whether git cherry-pick runs commit-msg is unverified, so re-subject each pick regardless.

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 10, batch 14, batch 17, batch 19, batch 29
