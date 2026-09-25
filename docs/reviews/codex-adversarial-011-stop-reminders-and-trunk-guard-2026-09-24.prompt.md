# Codex adversarial review: plan 011 (stop-reminders-and-trunk-guard), branch improve/011-stop-reminders-and-trunk-guard

Feature: Make the four Stop reminders reach Claude and guard the live trunk against force pushes. Claude Code sends a Stop hook's stderr (on exit 0) and its plain stdout to the debug log only; Claude never sees either. TAOM's four Stop hooks (build before "done", `/deep-review` before commit, tag the release, update CHANGELOG) print their reminder with `echo ... >&2` and `exit 0`, so not one of them has ever reached Claude, while `docs/reference/hooks-catalog.md`, `block-no-verify.sh` and `docs/reference/release-process.md` all present them as the working backstops. Three of them also write a mute marker when they fire, so the single reminder per streak is spent on the debug log. Separately, `validate-push.sh` hard-blocks a force push only to `master`, `main` and `bannerlord-1.4.5`, while the live trunk is `bannerlord-1.5.x` (the `v2.0.29` and `v2.0.30` release tags are on it and on neither other branch), the hook is registered only for the Bash tool (a push through the PowerShell tool is never checked), and GitHub protects no branch at all. After this plan, each reminder arrives once per streak as a Stop block that Claude answers, a force push to any `bannerlord-*` branch is refused from either shell tool, and Mike has the exact steps to add the server-side ruleset.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff bec0389d..improve/011-stop-reminders-and-trunk-guard
- Any file as the branch has it: git show improve/011-stop-reminders-and-trunk-guard:<path>
- The base for comparison: git show bec0389d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/011-stop-reminders-and-trunk-guard:plans/011-stop-reminders-and-trunk-guard.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything, or an excerpt in "Current state" does not match the file (line
2. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 1 finds Step 0 not done (no worktree, wrong branch, the PowerShell count is not `2`, or
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Any Edit or Write would land under `E:/repos/TAOM` (the main tree) instead of
4. From the plan's STOP conditions, did the change hit or mishandle this risk? A commit gate refuses the commit for a reason about state you do not own (for example, it names
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The RED run in Step 2 does not produce the listed failures (for example a Stop hook already
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Section 4 or 5 of `tools/test_hooks.sh` reports a Stop hook hanging, exiting other than 0, or
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (16 files changed, 496 insertions(+), 371 deletions(-)):
Scripts and hooks:
- .claude/hooks/_stop_reminder.sh
- .claude/hooks/check-changelog-updated.sh
- .claude/hooks/check-deep-review.sh
- .claude/hooks/check-verification-evidence.sh
- .claude/hooks/check-version-tagged.sh
- .claude/hooks/mark-verification-run.sh
- .claude/hooks/notify-test-results.sh
- .claude/hooks/suggest-compact.sh
- .claude/hooks/validate-push.sh
- tools/test_hooks.sh
Harness and docs:
- .claude/rules/harness-facts.md
- .claude/rules/hook-authoring.md
- .claude/settings.json
- CHANGELOG.md
- CLAUDE.md
- docs/reference/hooks-catalog.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
