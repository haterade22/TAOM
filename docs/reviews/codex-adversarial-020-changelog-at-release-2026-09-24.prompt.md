# Codex adversarial review: plan 020 (changelog-at-release), branch improve/020-changelog-at-release

Feature: Generate CHANGELOG release sections at /release from commit bodies instead of hand-editing a shared file. `CHANGELOG.md` is the most contended file in the repo: 547 of the 814 commits since 2026-07-01 touch it (`git rev-list --count --since=2026-07-01 a39a9c86 -- CHANGELOG.md`), it is 1,762,388 bytes at `a39a9c86`, and 7 of the 27 merges reachable from `a39a9c86` carry a resolved conflict hunk in it. It has lost work more than once (`dc2d13a5` restored entries another session's stale rewrite dropped; `a035a2d3` repaired a CRLF rewrite; `41d4afbe` recorded entries swept into an unrelated commit), and three git-safety rules in `docs/ai-includes/git-and-commits.md:56,60,61` exist because of it. Meanwhile the file mostly repeats the commit log: every subject already carries the gated label `<type>[(scope)]: vX.Y.Z - <description>`, and the bodies already carry the detail. Mike decided (decision 18, 2026-09-24) that `/release` generates the CHANGELOG from commit subjects and bodies, the "every session" duty goes, the two CHANGELOG hooks go, and today's file is archived. After this plan, no ordinary commit touches `CHANGELOG.md`; the one writer is `tools/changelog_from_commits.py`, run once per release.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff bec0389d..improve/020-changelog-at-release
- Any file as the branch has it: git show improve/020-changelog-at-release:<path>
- The base for comparison: git show bec0389d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/020-changelog-at-release:plans/020-changelog-at-release.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 1 items 2 to 4 fail: Step 0 is missing or differs. Never edit `.claude/settings.json`
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The worktree's `Main/_Module/SubModule.xml` version differs from the version the live subject hook
3. From the plan's STOP conditions, did the change hit or mishandle this risk? The Step 3 smoke numbers differ from 57, 53 and 4, or the group order differs (history or tags
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Any block to replace in Steps 5, 7 or 8 is not found exactly once in its file (the worktree is not
5. From the plan's STOP conditions, did the change hit or mishandle this risk? After Step 7 the phrase grep prints a line in a file outside the in-scope list, or a line this
6. From the plan's STOP conditions, did the change hit or mishandle this risk? `bash tools/test_hooks.sh`, `python tools/lint_docs.py --fail-on-drift` or
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (46 files changed, 24487 insertions(+), 24255 deletions(-)):
Tests:
- tools/tests/test_changelog_from_commits.py
Scripts and hooks:
- .claude/hooks/block-broad-git-add.sh
- .claude/hooks/check-changelog-changed.sh
- .claude/hooks/check-changelog-updated.sh
- .claude/hooks/check-moduledata-validation.sh
- .claude/hooks/check-version-tagged.sh
- .claude/hooks/session-start.sh
- tools/README.md
- tools/changelog_from_commits.py
- tools/test_hooks.sh
Harness and docs:
- .ai/roles/builder.md
- .claude/rules/external-skill-ports.md
- .claude/rules/gui-ui.md
- .claude/rules/harness-facts.md
- .claude/rules/hook-authoring.md
- .claude/rules/simplicity-criterion.md
- .claude/rules/troops.md
- .claude/rules/working-discipline.md
- .claude/settings.json
- .claude/skills/armory-audit/SKILL.md
- .claude/skills/author-armor/SKILL.md
- .claude/skills/deep-review/SKILL.md
- .claude/skills/deep-review/lenses/4-completeness.md
- .claude/skills/finish-branch/SKILL.md
- .claude/skills/improve/SKILL.md
- .claude/skills/lint-cleanup-loop/SKILL.md
- .claude/skills/new-adr/SKILL.md
- .claude/skills/release/SKILL.md
- .claude/skills/scope-check/SKILL.md
- .claude/skills/ship/SKILL.md
- .claude/skills/verify-bindings/SKILL.md
- .claude/skills/verify/SKILL.md
- AGENTS.md
- CHANGELOG.md
- CLAUDE.md
- README.md
- docs/ai-includes/codex-operating-guide.md
- docs/ai-includes/completion-workflow.md
- docs/ai-includes/external-repo-adoption.md
- docs/ai-includes/lord-skills-authoring.md
- docs/ai-includes/new-culture-authoring.md
- docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md
- docs/reference/hooks-catalog.md
- docs/reference/release-process.md
Other:
- .agents/skills/taom-build/SKILL.md
- .serena/memories/task_completion_checklist.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
