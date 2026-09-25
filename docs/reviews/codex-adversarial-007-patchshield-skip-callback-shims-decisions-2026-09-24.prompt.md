# Codex adversarial review: plan 007 (patchshield-skip-callback-shims), branch improve/007-patchshield-skip-callback-shims

Feature: Stop PatchShield re-shielding the 247 callback shims at the first game start. TAOM.Dependencies' PatchShield attaches a Harmony finalizer to every Harmony-patched method in the process. Its second pass runs synchronously on the main thread inside the loading screen of the first game start of every process (new campaign, loaded campaign or custom battle). On the maintainer's machine that pass attaches 372 finalizers in about 69 s (about 64% of a custom battle's 107 s loading screen), because every `Harmony.Patch` call currently costs about 186 ms there (an environmental tax whose cause is unknown; it was 5 to 10 ms per call before 2026-06-12). About 247 of those 372 attaches land on the engine's native-to-managed callback shims (`ManagedCallbacks.*CallbacksGenerated`), which TAOM's own crash reporter already wraps with a finalizer that swallows exceptions in normal play; PatchShield adds nothing there and never swallowed a single exception in 466 logged sessions. Excluding that namespace removes about two thirds of pass 2's attaches (about 46 s per first game start on this machine, about 1.5 s in the fast mode), and a per-call `__originalMethod` wrapper from engine callback hot paths. The plan also makes the per-attach cost visible in `diag.log` (which ships in every crash bundle) and fixes log and doc text that claims pass 2 runs at the main menu.

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff 31a31f16..improve/007-patchshield-skip-callback-shims is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/007-patchshield-skip-callback-shims:docs/reviews/deep-review-007-patchshield-skip-callback-shims-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 31a31f16..improve/007-patchshield-skip-callback-shims
- Any file as the branch has it: git show improve/007-patchshield-skip-callback-shims:<path>
- The base for comparison: git show 31a31f16:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/007-patchshield-skip-callback-shims:plans/007-patchshield-skip-callback-shims.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything and the excerpts in "Current state" no longer match the files, or
2. From the plan's STOP conditions, did the change hit or mishandle this risk? `pwsh tools/taom-src.ps1 path ManagedCallbacks.CoreCallbacksGenerated` fails, or any of the three
3. From the plan's STOP conditions, did the change hit or mishandle this risk? `Native2ManagedPatcher.cs` no longer patches `*CallbacksGenerated` types, or
4. From the plan's STOP conditions, did the change hit or mishandle this risk? `git grep -n "ExcludedTargetNamespacePrefixes" -- '*.cs'` shows a hit in any file other than
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Plan 006 or anything else has already changed `Dependencies/Foundation/PatchShield.cs` or
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 1 shows a failing test in `PatchShieldPolicyTests` or under `TAOM.Tests/Infrastructure/`, a
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (9 files changed, 199 insertions(+), 30 deletions(-)):
C#:
- Dependencies/Foundation/PatchShield.cs
- Dependencies/Foundation/PatchShieldPolicy.cs
- Dependencies/Foundation/ShieldCoverage.cs
- Dependencies/SubModule.cs
Tests:
- TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs
- TAOM.Tests/Infrastructure/Dependencies/ShieldCoverageTests.cs
Harness and docs:
- CHANGELOG.md
- docs/migration/dr3-maintenance.md
- docs/reviews/deep-review-007-patchshield-skip-callback-shims-2026-09-24.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
