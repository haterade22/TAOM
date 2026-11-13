You are an adversarial reviewer for TAOM (a Bannerlord total conversion, .NET Framework 4.7.2, engine v1.5.7). This is a focused re-review of fixes made after your first pass today (docs/reviews/raw/codex-adversarial-harmony-guards-batch-2026-11-13.md) and after two Claude review lenses. REVIEW ONLY: do not edit, create or delete any file, and do not build or run tests. Return the full report as your FINAL MESSAGE; do NOT write it to any path yourself.

You run in the worktree E:\repos\taom-finalizer-rethrow, branch fix/finalizer-rethrow-class, HEAD = trunk eeb664f4a. Everything under review is UNCOMMITTED: see it with git status --short and git diff. Two issues share the diff and will be two commits.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- AGENTS.md, then .ai/review-reference.md.
- .claude/rules/harmony-patches.md (Finalizer bullets).
- docs/features/war-of-the-ring.md: the paragraph that starts "The phase turns to Full War first" and the one that starts "**Limits of the repair.**".
- docs/reviews/rca-wotr-phase2-wars-2026-10-08.md (why the repair exists; rows R1, R2, F22).

FIX 1 -- issue #1163 (your F1 from the first pass): the host's daily tick now runs the declared-war repair.
Files: Main/Features/Diplomacy/WarOfTheRingBehavior.cs (OnDailyTick), Main/Features/Diplomacy/WarOfTheRingService.cs (comment only; read ReconcileDeclaredWars, DeclareFullWarWars, DeclareConfiguredWars, DeclareHostileTierWars, TryDeclareWar, ShouldBlockPeace, GetWarVeto), TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingServiceTests.cs, docs/features/war-of-the-ring.md, docs/features/coop-interop.md.
What changed: OnDailyTick, after CheckPhaseTransition and before EndForbiddenWars, calls _wotrService.ReconcileDeclaredWars() when _coopSession.ShouldDeferToHost is false (it already returned earlier when IsAuthority is false). The test that pinned "the daily tick does not run the repair" was replaced.

FIX 2 -- issue #1155, changes since your first pass:
- Four finalizers that only observe are now void: Main/Features/LordPartyTemplates/Hooks/Patch88_SpawnLordPartyScope.cs, Patch88_InitializeLordPartyPropertiesScope.cs, Main/Features/CreatureBandits/Hooks/Patch93_CreatureBanditWeaponState.cs, Main/Features/PlayerSwitcher/Hooks/Patch77_BodyGeneratorView_OnFinalize.cs.
- Main/Features/LordSpawnGuard/Hooks/Patch65_LandlessCultureSpawnGuard.cs: the finalizer takes MethodBase __originalMethod and passes it to RethrowStackPreserver.PreserveForRethrow.
- TAOM.Tests/Infrastructure/FinalizerRethrowGateTests.cs: the gate now scans comment-stripped text with five alternations.
- TAOM.Tests/Infrastructure/Dependencies/RethrowStackPreserverTests.cs: a real-Harmony test that a method whose finalizers are all void keeps its trace.
- Texts: PatchShield's owner protection is described as "not protected yet" with decision D13 (plans/_audit/2026-10-02-perf/DECISIONS.md) named; the registry, three docs and the lessons were corrected.

KNOWN SUSPECTS -- answer each with CONFIRMED or DISPUTED and the code you read:
S1 (Fix 1). A daily call to ReconcileDeclaredWars in Full War: can it declare a war that should not be declared on any day after the first (a pair the player or the AI ended legitimately while the feature is ON, a pair with blockPeaceBetweenHostileTiers off, an eliminated or newly created kingdom, a kingdom that changed alignment, the WarEnded phase)? Trace ShouldBlockPeace and HasDeclaredWar for each.
S2 (Fix 1). Can the daily call repeat a warning or an error every day (TryDeclareWar "had no effect", WarnSkippedOnce, an exception from the alliance adapter), or cost enough to hitch the map on a campaign with the shipped kingdom count? Read IAllianceAdapter's implementation.
S3 (Fix 1). Order and timing: is "after CheckPhaseTransition, before EndWarsForbiddenByPhase" right for every phase, including the tick on which the phase turns to Full War and a mid-campaign install that jumps phases? At a daily tick, are the engine's WarDeclared listeners and chat notices registered (they are not at session launch, RCA row R1)? Does a war declared here pull permanent allies in through vanilla's alliance listener, and is that the same as at the phase transition?
S4 (Fix 1). Co-op: with BannerlordCoop, does the host's declaration here replicate, and can a client run this path (IsAuthority, ShouldDeferToHost; read ICoopSessionProvider and CoopSessionPolicy)? Is the stricter gate on the repair than on CheckPhaseTransition in the same method a problem or a benefit?
S5 (Fix 2). The four void finalizers: read Harmony 2.4.2's MethodCreator in 0Harmony.dll from the TAOM.Dependencies module folder. For each target, with the finalizers it carries in a real session (PatchShield's value-returning finalizer from the second game start, Patch65 on SpawnLordParty), does any exception path now lose its throw site, or lose the cleanup the finalizer did (Patch88's owner restore, Patch77's teardown, Patch93's log)? Does a void finalizer with an `Exception __exception` parameter (Patch93) bind correctly?
S6 (Fix 2). Patch65's new `MethodBase __originalMethod` parameter: is it bound by Harmony for a finalizer on a private instance method, and what does it cost per call on HeroSpawnCampaignBehavior.SpawnLordParty? Is the parameter order with `ref MobileParty __result` safe?
S7 (Fix 2). The gate regex in FinalizerRethrowGateTests: name any spelling in the CURRENT source of Main, Client, Dependencies or CoopCompat that it flags wrongly, and any real bare hand-back it misses that is not already listed in its docstring.

REQUIRED SECTIONS:
1. ENGINE AND HARMONY CODE. From the installed DLLs through your ilspy server, paste the lines you rely on: the daily tick dispatch that calls TAOM's OnDailyTick listener and when vanilla registers its WarDeclared listeners (Campaign.OnSessionStart, KingdomManager, CampaignInformationManager); DeclareWarAction.ApplyInternal; AllianceCampaignBehavior.OnWarDeclared; Harmony MethodCreator's finalizer emit for void and value-returning finalizers.
2. FIX 1 ANALYSIS. S1 to S4, each with one concrete campaign scenario.
3. FIX 2 ANALYSIS. S5 to S7.
4. FINDINGS OR OBSERVATIONS. Each finding: severity (P1 ship-blocking, P2 real defect, P3 minor), file:line, the evidence, a failing scenario, the smallest fix. Say plainly when a fix has no finding.

QUALITY GATES:
- Every claim about engine or Harmony behaviour quotes the line that produces it.
- No finding that only repeats what vanilla itself does, and none that repeats a limit the feature doc already states.
- Vary severity honestly; mark anything you could not check as UNVERIFIED.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write it to docs/reviews/raw or to any other path, and do not change any file.
