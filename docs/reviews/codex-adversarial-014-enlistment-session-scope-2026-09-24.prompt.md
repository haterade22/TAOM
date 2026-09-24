# Codex adversarial review: plan 014 (enlistment-session-scope), branch improve/014-enlistment-session-scope

Feature: Reset Enlistment's per-session clocks and caches on load and on a new campaign. Every Enlistment service is a DryIoc `Reuse.Singleton`, so it lives for the whole Bannerlord process, not for one campaign. Three of them hold values keyed on the absolute campaign clock that nothing clears when the player loads an earlier save or starts a new campaign in the same process: the settlement-dwell anchor, the shore-leave offer's settlement id and 24-hour cooldown stamp, and the per-hour army-rhythm snapshot. After a load of an earlier save, the old stamp sits in the future, and the code reads a negative elapsed time as "a moment ago": the exit sweep keeps the player inside a town the commander has already left (until the new clock passes the old stamp plus 6 hours), and the leave-on-arrival popup stays silent until the new clock passes the old stamp plus a day, which can be the rest of the playthrough. Worse, the feature's one reset method, `ServiceMaintenanceService.ResetSessionCaches`, runs only on game load: a brand-new campaign never reaches it, so the cached commander party handle, the stale-battle-latch anchor and the army handle it already clears also leak into campaign two. After this plan, both lifecycle edges (load and new campaign) run one reset that clears every one of these, and tests pin both paths.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 7f02fc8d..improve/014-enlistment-session-scope
- Any file as the branch has it: git show improve/014-enlistment-session-scope:<path>
- The base for comparison: git show 7f02fc8d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/014-enlistment-session-scope:plans/014-enlistment-session-scope.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? Any Step 0 item 3 check prints something other than the stated value (the code drifted from the
2. From the plan's STOP conditions, did the change hit or mishandle this risk? A RED step fails with anything other than the named compile errors (for example Step 1 already
3. From the plan's STOP conditions, did the change hit or mishandle this risk? `EnlistmentContainerWiringTests` fails after Step 4 (with its two substitutes in place) or Step 6,
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Constructing `EnlistmentBehavior` in the Step 5 test throws (for example a TypeLoad or
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 6 cannot be done within two added lines (the ADR-002 ceiling of under 150 lines).
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Any other existing test newly fails after a GREEN step, in particular any test that expects
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (14 files changed, 300 insertions(+), 23 deletions(-)):
C#:
- Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs
- Main/Features/Enlistment/EnlistmentReconciler.cs
- Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs
- Main/Features/Enlistment/IServiceAttachmentService.cs
- Main/Features/Enlistment/IServiceMaintenanceService.cs
- Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs
- Main/Features/Enlistment/ServiceAttachmentService.cs
- Main/Features/Enlistment/ServiceMaintenanceService.cs
Tests:
- TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs
- TAOM.Tests/Features/Enlistment/EnlistmentPumpAuthorityTests.cs
- TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs
- TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/enlistment.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
