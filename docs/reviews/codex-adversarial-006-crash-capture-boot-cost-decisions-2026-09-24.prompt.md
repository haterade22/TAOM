# Codex adversarial review: plan 006 (crash-capture-boot-cost), branch improve/006-crash-capture-boot-cost

Feature: Cut the crash-capture sweep from 247 patched callbacks to a six-entry allowlist, make both capture toggles live, and delete four finalizers that can never fire. Every TAOM boot spends 29 to 33 seconds inside one block of `SubModule.OnSubModuleLoad`, measured on 30 of 30 desktop launches (the gap between the `[SaveDefiners]` log line and `[CrashReport] Native2Managed: attached 247 Finalizer(s)`). That block Harmony-patches all 247 static methods of the engine's three `*CallbacksGenerated` classes with a crash-capture finalizer, at about 120 to 190 ms per `harmony.Patch` on this machine; in 30 logged sessions it never captured a single exception. Players cannot switch it off: the MCM toggle is read at a point where MCM's settings object is always null, so the code always takes its `?? true` fallback, while the MCM hint text and the feature doc both claim the finalizers "cost zero". Separately, four of the nine named crash finalizers sit on empty base virtual methods and can never run for the overrides that actually throw. After this plan: the sweep patches six chosen callbacks (expected boot cost about 1 s instead of about 30 s), both toggles work live without a restart, the dead finalizers are gone with a test that stops them coming back, a crash that recurs every frame stops writing one log line per frame, and the docs say what the code does. The capture stays ON by default: that posture is the maintainer's decision and this plan does not change it.

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff 70727529..improve/006-crash-capture-boot-cost is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/006-crash-capture-boot-cost:docs/reviews/deep-review-006-crash-capture-boot-cost-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 70727529..improve/006-crash-capture-boot-cost
- Any file as the branch has it: git show improve/006-crash-capture-boot-cost:<path>
- The base for comparison: git show 70727529:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/006-crash-capture-boot-cost:plans/006-crash-capture-boot-cost.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? Any file you created, edited or staged resolves under `E:\repos\TAOM\` instead of W, or the Tree guard
2. From the plan's STOP conditions, did the change hit or mishandle this risk? Any excerpt in "Current state" does not match your worktree at `b2e387db`.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0's full run fails a test in `TAOM.Tests/Features/CrashReport/`, `SettingRequireRestartPostureTests`
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 1's RED lists any class other than the four named, or is `Skipped`/Inconclusive (game assemblies
5. From the plan's STOP conditions, did the change hit or mishandle this risk? `Native2ManagedTargetsTests.All_ResolvesEveryShimAgainstTheInstalledEngine` reports a missing shim.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? `RethrowStackPreserver.PreserveForRethrow` does not exist with the signature quoted above, or
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (15 files changed, 524 insertions(+), 41 deletions(-)):
C#:
- Main/Features/CrashReport/Hooks/AppDomainExceptionHook.cs
- Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs
- Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs
- Main/Features/CrashReport/Hooks/Native2ManagedTargets.cs
Tests:
- TAOM.Tests/Features/CrashReport/AppDomainExceptionHookTests.cs
- TAOM.Tests/Features/CrashReport/CrashReportPatchHelperTests.cs
- TAOM.Tests/Features/CrashReport/Native2ManagedBridgeTests.cs
- TAOM.Tests/Features/CrashReport/Native2ManagedTargetsTests.cs
- TAOM.Tests/Features/CrashReport/RethrowProbe.cs
Harness and docs:
- CHANGELOG.md
- docs/features/crash-report.md
- docs/features/hero-race.md
- docs/reference/harmony-patch-registry.md
- docs/reviews/deep-review-006-crash-capture-boot-cost-2026-09-24.md
- docs/reviews/lessons/harmony-il.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
