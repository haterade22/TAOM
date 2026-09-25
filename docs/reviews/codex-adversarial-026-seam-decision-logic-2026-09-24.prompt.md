# Codex adversarial review: plan 026 (seam-decision-logic), branch improve/026-seam-decision-logic

Feature: Move TAOM decision logic out of nine protected-virtual seams into their services, so unit tests run it. `SupplyOrderService`, `RefugeService`, `WardenService` and `CampService` keep their engine access in `protected virtual` members ("seams") that the unit tests override on a test subclass. Nine of those seams also hold TAOM decisions: who gets paid for a supply order and what happens when the payee is gone; where a failed order's goods, volunteers and troops go back to and which counts move; which hostile party raids a refuge; which refuge-held prisoners a peace frees; which heroes count as warden candidates; how a promoted soldier's companion template and age are chosen; and which settlements count as the "nearest town" that keeps camps and refuges away. Because every test subclass overrides the seam, **no unit test runs any of that logic today**; a regression there ships silently. After this plan each decision lives in the service where tests run it, each seam is one engine operation, the two identical fortification searches share one pure rule, the behaviour in game is unchanged, and 76 new tests pin the moved logic.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff e452b0c7..improve/026-seam-decision-logic
- Any file as the branch has it: git show improve/026-seam-decision-logic:<path>
- The base for comparison: git show e452b0c7:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/026-seam-decision-logic:plans/026-seam-decision-logic.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything and the live code differs from a "Current state" excerpt, Step 0
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The baseline suite (Step 0) has a failure.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? A RED step fails with an error in a file other than its own test file, or with an error code
4. From the plan's STOP conditions, did the change hit or mishandle this risk? An **existing** test fails after a GREEN step and a re-read of the excerpt does not show a
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Making the change seems to need a new or changed member on `ISupplyOrderService`, `IRefugeService`,
6. From the plan's STOP conditions, did the change hit or mishandle this risk? The compiler reports that a seam is overridden somewhere else (a `CS0115` or `CS0506` in a file
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (12 files changed, 5783 insertions(+), 225 deletions(-)):
C#:
- Main/Features/FieldCamp/CampService.cs
- Main/Features/Refuge/RefugeService.cs
- Main/Features/Refuge/WardenService.cs
- Main/Features/SupplyLines/SupplyOrderService.cs
Tests:
- TAOM.Tests/Features/FieldCamp/CampServiceTests.cs
- TAOM.Tests/Features/Refuge/RefugeServiceTests.cs
- TAOM.Tests/Features/Refuge/WardenServiceTests.cs
- TAOM.Tests/Features/SupplyLines/SupplyOrderServiceTests.cs
Harness and docs:
- CHANGELOG.md
- plans/026-seam-decision-logic.md
- plans/_audit/2026-09-23-opus/plan-review-026-ext.md
- plans/_audit/2026-09-23-opus/plan-review-026.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
