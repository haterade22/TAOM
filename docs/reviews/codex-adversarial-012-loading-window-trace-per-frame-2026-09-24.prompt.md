# Codex adversarial review: plan 012 (loading-window-trace-per-frame), branch improve/012-loading-window-trace-per-frame

Feature: Trace a loading-window lower only when the window actually drops, not on every frame. A Harmony postfix written to trace "a handful" of loading-window transitions per session runs on every rendered frame of the main menu, the party screen and character creation, because the engine calls `LoadingWindow.DisableGlobalLoadingWindow()` unconditionally from those screens' frame ticks. Each call walks 12 managed stack frames with reflection and writes a line through `FileLogger.LogInfo`, which flushes to disk synchronously on the game thread. Measured on the desktop: one 35-minute session wrote an 84 MB log of which 262,763 of 265,061 lines are `LOADING-WINDOW lowered` (one per rendered frame: about 125 a second averaged over the session, peaking near 360), and one session with the main menu left open for three hours wrote a 1.16 GB log with 4,095,062 of them. FileLogger keeps 30 logs, and crash bundles zip the whole log. After this plan a lower is traced only when the window was actually up before the call and is down after it: the same 35-minute session would log 7 lowered lines instead of 262,763, and the diagnostic question the trace exists for ("who raised the window, and did a matching lower ever come back") is answered exactly as before.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 7f02fc8d..improve/012-loading-window-trace-per-frame
- Any file as the branch has it: git show improve/012-loading-window-trace-per-frame:<path>
- The base for comparison: git show 7f02fc8d:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/012-loading-window-trace-per-frame:plans/012-loading-window-trace-per-frame.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The Disable class or the engine's `DisableGlobalLoadingWindow` body does not match the excerpts
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The Step 1 or Step 3 RED fails with anything other than the named compile errors (for example the
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Reading `LoadingWindow.IsLoadingWindowActive` in the test host still throws after adding the
4. From the plan's STOP conditions, did the change hit or mishandle this risk? `Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers` fails its precondition
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The fix appears to need `MapLoadTracer.cs`, `FileLogger.cs`, `Main/SubModule.cs`, `Main/IoC.cs`,
6. From the plan's STOP conditions, did the change hit or mishandle this risk? `HarmonyFieldInjectionNamingTests`, `HarmonyPatchBindingTests` or any other test under
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (7 files changed, 160 insertions(+), 9 deletions(-)):
C#:
- Main/Features/MapLoadDiagnostics/Hooks/LoadingWindow_Transitions_Patch.cs
- Main/Features/MapLoadDiagnostics/LoadingWindowTraceGate.cs
Tests:
- TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs
- TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowTraceGateTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/map-load-diagnostics.md
- docs/reference/harmony-patch-registry.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
