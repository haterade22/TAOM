TAOM CODEX ADVERSARIAL REVIEW, PASS 2: the fixes made after pass 1 (yotthani adoption, 2026-10-08)

You review only the fixes made after your first pass on the uncommitted work in the git worktree E:\repos\taom-yotthani (branch feat/yotthani-20261008, base commit d6b537092). Your first pass is docs/reviews/raw/codex-adversarial-yotthani-adoption-2026-10-08.md (1 conditional MEDIUM, 2 LOW). Two Claude convergence lenses ran on the same tree at the same time; their findings and these fixes are recorded in docs/reviews/rca-yotthani-adoption-2026-10-08.md, section "Codex review and convergence pass", findings 34 to 37.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

THE FIXES TO REVIEW:
F1. Main/SubModule.cs: OnMissionBehaviorInitialize is now a thin wrapper. It calls the new private AddTaomMissionBehaviors(mission) inside a try, and registers the BattleLoad loading window's only closer (BattleLoadPhaseBehavior) in the finally. AddTaomMissionBehaviors holds the old body unchanged: the first mission's patch application, the BattleLoad bracket, every TAOM behaviour registration. Intent: the closer exists whatever the wiring throws (Patch103 survives such a throw and starts the mission), it is registered last so it ticks first, and the throw still reaches the guard (no catch). Pinned by TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs.
F2. Main/Features/MapViewRelease/MapViewReleaseService.cs, OnSceneLayerActivated: an activation of a layer that is not the pending one now drops the pending mark when no campaign runs OR when the activated layer is the current map's own scene layer (a loaded save's new MapScreen). Tests: MapViewReleaseServiceTests (OnSceneLayerActivated_ANewMapLayerWhileAnOldOneIsDue_DropsTheOldMark, renamed CoverAndReturn_ tests).
F3. TAOM.Tests/Features/MapViewRelease/MapViewReleaseBindingTests.cs: pins that ScreenLayer.HandleActivate sets IsActive before it raises OnLayerActiveStateChanged.
F4. Wording only: the NameplateCullService replay comment (best effort, not idempotent), the two CrashReportSettings hints (mission, not battle), the Patch103 Priority.First comment and its binding assert message, and the docs (mission-start-guard.md, map-view-release.md, nameplate-cull.md, skeleton-buffer-guard.md, harmony-patch-registry.md, the engine doc's section 10 spin ranges).

QUESTIONS (CONFIRM or DISPUTE each with code evidence):
Q1. F1: is the closer now registered on every path through OnMissionBehaviorInitialize, including a throw in the first mission's patch application, a throw in any registration, and a throw in the closer's own construction? What does a throw in the finally do to the exception already in flight, and does that matter here? Does anything that used to rely on the closer being counted and logged inside the BattleLoad bracket (LogTaomBehaviorAdded, taomBehaviorCount, LogTaomBehaviorsDone) break now that the closer is registered after the bracket's Done line? Does the closer's new position (last registered, so first to tick) change the "battle playable" stamp or phase-5 logging in a way the BattleLoad docs (docs/features/battle-load-diagnostics.md) contradict?
Q2. F1: with Survive Mission Start Failures OFF, the throw escapes, Patch37 swallows it, and the engine loads the mission again on the same Mission object. Each attempt now adds one more closer. Can several BattleLoadPhaseBehavior instances on one mission misbehave (double close, double ClearInflight, a stale marker, a duplicate playable line)?
Q3. F2: can the new drop rule drop a release that should run (a map layer that is the current map's AND not the pending one while the pending one is still alive), and can it call IsMapSceneLayer in a state where MapScreen.Instance is stale?
Q4. F3: does the pin read the right method body and fail if the order flips?
Q5. Anything else these fixes broke, judged against the v1.5.4 engine (C:\Users\mikew\.taom-src\v1.5.4\, or pwsh tools/taom-src.ps1 path <Full.Type.Name>).

QUALITY GATES:
- Every claim cites a file and line you read in this run.
- Do not re-report items the RCA records as settled or not applied, unless a fix itself is wrong.
- Prefer one proven defect to several speculative ones, but report every proven defect.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

FINDINGS FORMAT: severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the claim, evidence with line numbers, a reproduction or proving input, the fix. Mark anything you could not prove UNVERIFIED. End with a count per severity and a verdict.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write any file: the dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-yotthani-adoption-2026-10-08-pass2.md, so never write that path yourself. Do not edit, create or delete any file anywhere. Do not run git commands that change state. If you build or test, use only: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= (never ./build.ps1, which deploys into the game).
