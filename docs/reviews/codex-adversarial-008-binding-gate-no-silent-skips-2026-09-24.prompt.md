# Codex adversarial review: plan 008 (binding-gate-no-silent-skips), branch improve/008-binding-gate-no-silent-skips

Feature: Make the binding gate fail loudly instead of passing by skipping. `TestCategory=BindingVerification` (368 tests at `b2e387db`) is the gate every Bannerlord engine bump relies on: it proves each Harmony patch target, GameModel registration and reflection site still binds against the installed engine. When the test process cannot find the game it calls `Assert.Inconclusive`, and MSTest reports Inconclusive as **Skipped with exit code 0**. The audit measured it on 2026-09-23: the same prebuilt test DLL, run without `BANNERLORD_GAME_DIR`, printed `Passed! - Failed: 0, Passed: 33, Skipped: 335, Total: 368` and exited green; the Claude test-results hook then prints `PASSED (33 tests)`, and the verify-bindings skill tells every agent the gate "does not falsely pass". The trigger is any test process that lacks the environment variable while the build had the game: an IDE runner, `dotnet test --no-build` from a fresh shell, `dotnet test -p:BANNERLORD_GAME_DIR=...`, or a CI step that sets it only on the build. The same mapping hides the gate's own "discovery floor" guards (fewer than 30 patch types or 20 GameModels means TAOM types failed to load, the exact failure an engine bump produces). After this plan: the gate finds the game the build compiled against, the gate's documented command turns any skip into a failure, the floors fail, and the hook names skipped tests. This closes the binding-gate half of the open item "`Assert.Inconclusive` is a pass" in `docs/reviews/rca-lord-identity-2026-08-29.md:77-80` (the other half, `LordFamilyTransformTests` and the default suite, stays open). **Decided, keep (do NOT overturn):** in the *default* suite a test skips, never fails, when the game or the Armory is absent. This is recorded in `TAOM.Tests/Migration/GameAssemblies.cs:19-21` and in `docs/reviews/lessons/data-content-cultures.md:1588-1591` ("it reads the live file and skips, never fails, when the file is absent", 2026-09-23). Therefore `MapInconclusiveToFailed` must apply **only** to runs that pass the new settings file explicitly. Never set it globally (no `RunSettingsFilePath` in any project or props file, no file named `.runsettings` at the repo or solution root).

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 7f02fc8d..improve/008-binding-gate-no-silent-skips
- Any file as the branch has it: git show improve/008-binding-gate-no-silent-skips:<path>
- The base for comparison: git show 7f02fc8d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/008-binding-gate-no-silent-skips:plans/008-binding-gate-no-silent-skips.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check shows a change to an in-scope file and its "Current state" excerpt no longer matches.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? Any command you run shows `E:/repos/TAOM` as its toplevel or `bannerlord-1.5.x` as its branch when
3. From the plan's STOP conditions, did the change hit or mishandle this risk? A commit gate denies, or its reason names any file outside the 15 in-scope paths. Report the reason
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0.4 (default gate, game present) shows any failure or skip: something in the gate is already
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 6 (a) shows any failure or skip: a `BindingVerification` test goes Inconclusive with the game
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 6 (b) still skips or fails after Step 4: the metadata fallback is not taking effect (check
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (15 files changed, 255 insertions(+), 23 deletions(-)):
Tests:
- TAOM.Tests/Migration/GameAssemblies.cs
- TAOM.Tests/Migration/GameAssembliesResolutionTests.cs
- TAOM.Tests/Migration/GameModelOverrideBindingTests.cs
- TAOM.Tests/Migration/HarmonyPatchBindingTests.cs
- TAOM.Tests/TAOM.Tests.csproj
- TAOM.Tests/binding-gate.runsettings
Scripts and hooks:
- .claude/hooks/notify-test-results.sh
- .github/workflows/build.yml
- tools/test_hooks.sh
Harness and docs:
- .claude/skills/verify-bindings/SKILL.md
- CHANGELOG.md
- docs/ai-includes/agent-operating-manual.md
- docs/migration/s6-runtime-punchlist.md
- docs/reference/hooks-catalog.md
- docs/reference/taleworlds-api-snapshot/README.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
