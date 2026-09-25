# Codex adversarial review: plan 013 (bash-hook-prefilter), branch improve/013-bash-hook-prefilter

Feature: Let non-git Bash calls skip the Python start-up in every Bash hook. Every Bash tool call in a Claude Code session runs 13 TAOM hook scripts. Each one sources `.claude/hooks/_pybin.sh`, which starts Python once to prove the interpreter is live, and then starts Python a second time to read `tool_input.command` out of the JSON payload, and only then asks whether the command is a `git` (or `dotnet`) command at all. Measured at planning on this desktop with a non-git `ls docs` payload: 200 to 328 ms per hook (5-run means) against a 58 ms bare `bash` spawn, a serial sum of about 2.9 s across the 12 timed hooks for every Bash call (they run in parallel, so the wall cost is lower but the CPU is spent), and the checker's transcript count says about 82% of Bash calls contain no `git` anywhere in the payload. After this plan each hook first tests the raw payload for its own trigger text with a bash builtin and exits with its normal allow output when the text is absent, so those calls start no Python at all (about 60 ms per hook), while every call that could concern a gate takes exactly the path it takes today.

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff 5dcef67a..improve/013-bash-hook-prefilter is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/013-bash-hook-prefilter:docs/reviews/deep-review-013-bash-hook-prefilter-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 5dcef67a..improve/013-bash-hook-prefilter
- Any file as the branch has it: git show improve/013-bash-hook-prefilter:<path>
- The base for comparison: git show 5dcef67a:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/013-bash-hook-prefilter:plans/013-bash-hook-prefilter.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything, or any excerpt in "Current state" does not match the file (for example the six-line preamble is not at the listed lines, or a hook no longer sources `_pybin.sh` before its parse).
2. From the plan's STOP conditions, did the change hit or mishandle this risk? `git rev-parse --show-toplevel` in Step 0 does not print `E:/repos/wt-plan-013`, or you find you edited or ran a hook under `E:/repos/TAOM/`. Report exactly what was touched; do not try to revert main-tree files yourself.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 1's RED run does not show exactly 13 `FAIL.*started an interpreter` lines and 14 "still parses" lines, or shows a `4c premise` or `4c discovery` failure. The premise failing means the counting interpreter is not accepted on this platform; do not weaken the test to get past it.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Any "skipped its parse on a trigger payload" line appears at any point: a prefilter is narrower than its hook's trigger. Do not widen the test; fix the prefilter to the plain substring form given here, and if the form given here is what fails, STOP.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The parity script reports any difference. Report the `DIFF` lines verbatim; do not edit a hook's decision logic to make them match.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Some Bash-matched hook in `settings.json` is not one of the 13 listed here (a registration was added since planning), or a hook's trigger turns out to need text other than `git`, `dotnet` or `build.ps1` in the command.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (16 files changed, 267 insertions(+), 65 deletions(-)):
Scripts and hooks:
- .claude/hooks/block-broad-git-add.sh
- .claude/hooks/block-dangerous-git.sh
- .claude/hooks/block-no-verify.sh
- .claude/hooks/check-changelog-changed.sh
- .claude/hooks/check-claude-files-tracked.sh
- .claude/hooks/check-commit-subject-version.sh
- .claude/hooks/check-doc-config-drift.sh
- .claude/hooks/check-moduledata-validation.sh
- .claude/hooks/check-native-dll-crt.sh
- .claude/hooks/mark-verification-run.sh
- .claude/hooks/notify-test-results.sh
- .claude/hooks/validate-push.sh
- tools/test_hooks.sh
Harness and docs:
- CHANGELOG.md
- docs/reference/hooks-catalog.md
- docs/reviews/deep-review-013-bash-hook-prefilter-2026-09-24.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
