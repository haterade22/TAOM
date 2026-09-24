# Codex adversarial review: plan 019 (nullable-ratchet), branch improve/019-nullable-ratchet

Feature: Stop discarding the nullable warnings and graduate the first folder (Siege) to errors. `Directory.Build.props` turns nullable reference types on for every project, and then `Main/TAOM.csproj` and `Dependencies/TAOM.Dependencies.csproj` throw the seven main nullable warning ids away with `<NoWarn>`. A scratch build at `b2e387db` with the suppression lifted printed **2,028** distinct nullable diagnostics (CS8618 756, CS8603 408, CS8604 331, CS8625 285, CS8600 108, CS8601 78, CS8602 62), and every new file in every folder lands with no null-flow signal at all, so the backlog only grows. Because the compiler's `/nowarn` beats any `.editorconfig` severity for the same id, no folder can opt back in today. This plan moves the suppression from `<NoWarn>` into the root `.editorconfig` (build output unchanged), then makes the first folder, `Main/Features/Siege`, null-clean and sets the seven ids to **error** there, so any new null-flow mistake in that folder fails the build. Each later folder is a small, separate change using the procedure in Maintenance notes.

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff de288136..improve/019-nullable-ratchet is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/019-nullable-ratchet:docs/reviews/deep-review-019-nullable-ratchet-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff de288136..improve/019-nullable-ratchet
- Any file as the branch has it: git show improve/019-nullable-ratchet:<path>
- The base for comparison: git show de288136:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/019-nullable-ratchet:plans/019-nullable-ratchet.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything and the live files differ from the Current state excerpts.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0's baseline build shows any CS86xx, or anything other than `2 Warning(s)`, or any test
3. From the plan's STOP conditions, did the change hit or mishandle this risk? The worktree's and the main tree's `Main/_Module/SubModule.xml` versions differ at a commit (the
4. From the plan's STOP conditions, did the change hit or mishandle this risk? After Step 1 (and its one sanctioned fallback) the Main build still shows CS86xx, or the
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 2 shows a Siege warning list that differs from the 8 in Current state, or any CS86xx outside
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 3's first test passes before the guard exists, or fails for a reason other than the
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (8 files changed, 154 insertions(+), 11 deletions(-)):
C#:
- Main/Features/Siege/Models/KingdomSiegeMessages.cs
- Main/Features/Siege/Models/SiegeDefenseConfig.cs
- Main/Features/Siege/SiegeDefenseService.cs
Tests:
- TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/siege-defense.md
- docs/features/siege.md
- docs/reviews/deep-review-019-nullable-ratchet-2026-09-24.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
