# Codex adversarial review: plan 027 (powershell-gate-coverage), branch improve/027-powershell-gate-coverage

Feature: Make the eight Bash-only PreToolUse gates cover the PowerShell tool, read PowerShell syntax, and close validate-push's remaining bypass shapes. Claude Code on this machine has two shell tools, Bash and PowerShell (pwsh 7). Eight PreToolUse gates are registered for the Bash tool only, so the same git command run through the PowerShell tool skips them: an unlabelled commit, an AI co-author trailer, `--no-verify`, `git reset --hard` and `git add -A` all run unchecked. Registering them is not enough on its own: every gate parses its command as Bash text, and measured at `96afb6fb` a correctly labelled PowerShell here-string commit (`git commit -m @'` ... `'@`) is **denied** by `check-commit-subject-version.sh` (it reads the subject as `@`), while `& 'C:\Program Files\Git\cmd\git.exe' reset --hard`, `GIT add -A` and `if ($x) { git stash drop }` pass the confirm gates. Maintainer decision 61 (2026-09-25) chose "a new plan to cover PowerShell: register the gates for PowerShell too, each gate's parsing checked for PowerShell syntax, test rows per shell". The plan 011 final convergence review also found force-push shapes `validate-push.sh` still lets through in either shell (glob and `heads/` refspecs, braces glued to the command, git by path or in capitals, a push option's value taken as the remote, a Bash backtick substitution) and one false refusal (a trunk named only in a trailing `#` comment); they are routed here. When this lands, one `Bash|PowerShell` group registers all nine git gates, each gate reads a PowerShell command as the Bash text of the same command, and every shape above has a test row under both tool names.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 96afb6fb..improve/027-powershell-gate-coverage
- Any file as the branch has it: git show improve/027-powershell-gate-coverage:<path>
- The base for comparison: git show 96afb6fb:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/027-powershell-gate-coverage:plans/027-powershell-gate-coverage.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything, `git status` shows a path other than `.claude/settings.json` and
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The baseline hook suite has any failure after one rerun (the check-8 `scan.sh` flake excepted).
3. From the plan's STOP conditions, did the change hit or mishandle this risk? A Step 2 expectation fails and the only fix would be to change the expectation: those values are
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Any pre-existing hook-suite row (one this plan did not add) fails at any step, or a fix seems to
5. From the plan's STOP conditions, did the change hit or mishandle this risk? A RED count differs from 40 (41 with the one timing row Step 4 names), 19 or 2 because a row
6. From the plan's STOP conditions, did the change hit or mishandle this risk? A timing row (4b or 7e) exceeds 80% of its registration twice in a row at Step 5 or later: the
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (22 files changed, 3722 insertions(+), 227 deletions(-)):
Tests:
- tools/tests/test_shellwords.py
Scripts and hooks:
- .claude/hooks/_pybin.sh
- .claude/hooks/_shellwords.py
- .claude/hooks/block-broad-git-add.sh
- .claude/hooks/block-dangerous-git.sh
- .claude/hooks/block-no-verify.sh
- .claude/hooks/check-claude-files-tracked.sh
- .claude/hooks/check-commit-subject-version.sh
- .claude/hooks/check-doc-config-drift.sh
- .claude/hooks/check-moduledata-validation.sh
- .claude/hooks/check-native-dll-crt.sh
- .claude/hooks/mark-verification-run.sh
- .claude/hooks/validate-push.sh
- tools/test_hooks.sh
Harness and docs:
- .claude/rules/harness-facts.md
- .claude/rules/hook-authoring.md
- .claude/settings.json
- docs/reference/hooks-catalog.md
- docs/reference/mcp-servers.md
- plans/027-powershell-gate-coverage.md
- plans/_audit/2026-09-23-opus/plan-review-027-second.md
- plans/_audit/2026-09-23-opus/plan-review-027.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
