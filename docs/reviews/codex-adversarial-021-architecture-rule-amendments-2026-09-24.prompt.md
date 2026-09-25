# Codex adversarial review: plan 021 (architecture-rule-amendments), branch improve/021-architecture-rule-amendments

Feature: Amend three architecture rules to match the code: virtual boundary seams, when a service gets an interface, when a patch gets a hook. Three written architecture rules describe a codebase TAOM does not have, so reviewers flag correct code and demand files that hide nothing. (A) ADR-007 and the review reference grade any TaleWorlds use in a service CRITICAL, yet four reviewed, well-tested campaign services keep their engine access in `protected virtual` "seam" members overridden by a test subclass, and nothing records that pattern as allowed. (B) The `/deep-review` Standards lens and ADR-002 demand an interface for every service while `.claude/rules/think-before-coding.md` forbids single-implementation interfaces; the two rules contradict each other and the feature scaffolds keep producing interfaces nothing fakes. (C) AGENTS.md states "patch → hook interface → service → adapter" as mandatory, but only 24 of the 217 Harmony patch files use a hook interface and none of the 19 hook interfaces is faked in a test. When this lands, every rule source says the same thing and matches the code: seams are allowed under four conditions, an interface is written when a test fakes it or a second class implements it (adapters always), and a hook interface is written only when a patch needs a narrow seam or a test fake.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff bec0389d..improve/021-architecture-rule-amendments
- Any file as the branch has it: git show improve/021-architecture-rule-amendments:<path>
- The base for comparison: git show bec0389d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/021-architecture-rule-amendments:plans/021-architecture-rule-amendments.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The worktree `E:/repos/wt-plan-021` or the branch is missing, or `plan021_check.py adr` fails:
2. From the plan's STOP conditions, did the change hit or mishandle this risk? An Edit or Write call is denied by the config-protection hook (it means you touched a protected
3. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check shows a quoted line changed on `bannerlord-1.5.x`, or the Step 1 RED run does not
4. From the plan's STOP conditions, did the change hit or mishandle this risk? `lint_docs.py --fail-on-drift` fails with a finding your edit caused (for example a `table-row`
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 8.6's grep prints a path beyond its five expected lines (another tracked file states the
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 8.2's normalized lint diff prints a `>` line naming one of your edited files.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (16 files changed, 102 insertions(+), 32 deletions(-)):
Harness and docs:
- .ai/review-reference.md
- .claude/agents/feature-builder.md
- .claude/rules/csharp-architecture.md
- .claude/rules/csharp-patterns.md
- .claude/rules/harmony-patches.md
- .claude/rules/think-before-coding.md
- .claude/skills/deep-review/lenses/1-standards.md
- .claude/skills/new-feature/SKILL.md
- AGENTS.md
- CHANGELOG.md
- docs/adrs/002-thin-entry-points.md
- docs/adrs/007-adapter-pattern.md
- docs/adrs/008-testability-requirements.md
- docs/ai-includes/architecture.md
- docs/ai-includes/decompiled-code-analysis.md
- docs/ai-includes/patterns.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
