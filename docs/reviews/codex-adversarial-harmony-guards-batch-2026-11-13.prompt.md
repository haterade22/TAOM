You are an adversarial reviewer for TAOM (a Bannerlord total conversion, .NET Framework 4.7.2, engine v1.5.7). Review three things that had no Codex pass. This is a REVIEW ONLY: do not edit, create or delete any file, and do not build or run tests. Return the full report as your FINAL MESSAGE; the dispatcher redirects stdout into a file, so do NOT write the report to any path yourself.

You run in the worktree E:\repos\taom-finalizer-rethrow, branch fix/finalizer-rethrow-class. HEAD is trunk commit eeb664f4a. The working tree holds one UNCOMMITTED change (Part A). Parts B and C are already in HEAD.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- AGENTS.md, then .ai/review-reference.md (architecture, known exceptions, lessons from prior reviews).
- .claude/rules/harmony-patches.md (the finalizer and prefix rules).
- docs/reviews/rca-caravan-template-guard-2026-11-13.md (what two earlier review rounds already found in Part B; do not repeat those unless the fix is wrong).
- docs/reviews/lessons/harmony-il.md: only the entries "A value-returning finalizer that hands back its exception erases the throw site", "A finalizer cannot tell a vanilla NRE from a foreign one: guard the read with a prefix", "A new patch on an engine method also gets PatchShield's finalizer", "A bool prefix that returns false does not stop the postfixes", "A patch's own try/catch cannot survive a JIT-time member-resolution failure in its own body" (grep the headings; do not read the file whole).
- GitHub issue #1155 body is summarized in Part A below.

PART A -- uncommitted, issue #1155 (see it with: git status --short ; git diff)
Intent: nine finalizer sites returned `__exception` bare. When any finalizer on a method returns a value, Harmony 2.4.2 ends its wrapper with `throw`, which replaces the stack trace. Each site now returns RethrowStackPreserver.PreserveForRethrow(__exception, null). A new source gate fails on the bare statement. PatchShield: NO behaviour change; a test pins that TAOM's main Harmony owner id com.taom.mod is deliberately NOT a protected owner (so the rescue strips TAOM's own patches after a missing-API exception) and that the shields' own ids are protected; a lesson and two comments that said the opposite were corrected.
Files (production): Main/Features/LordSpawnGuard/Hooks/Patch65_LandlessCultureSpawnGuard.cs, Main/Features/LordPartyTemplates/Hooks/Patch88_SpawnLordPartyScope.cs, Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs, Main/Features/CreatureBandits/Hooks/Patch93_CreatureBanditWeaponState.cs, Main/Features/PlayerSwitcher/Hooks/Patch77_BodyGeneratorView_OnFinalize.cs, Main/Features/Arena/Hooks/GauntletMovie_Release_AvGuard_Patch.cs, Dependencies/Foundation/CollectAssemblyTypesShim.cs, Dependencies/Foundation/PatchShield.cs (comment only), Dependencies/Foundation/PatchShieldPolicy.cs (comment only).
Files (helper, unchanged, read it): Dependencies/Foundation/RethrowStackPreserver.cs.
Files (tests): TAOM.Tests/Infrastructure/FinalizerRethrowGateTests.cs (new), TAOM.Tests/Infrastructure/ContainmentFinalizerRethrowTests.cs, TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs.
Files (text): docs/reviews/lessons/harmony-il.md, .claude/rules/harmony-patches.md, docs/reviews/rca-caravan-template-guard-2026-11-13.md.

PART B -- in HEAD: Patch147 CaravanTemplateGuard (issue #482, PR #1147, rework commit 372cab396)
Intent: vanilla CaravansCampaignBehavior.SpawnCaravan dereferences a null caravan template. A bool prefix hands vanilla's own inputs to the pure rule CaravanTemplateGuard.Judge, which repeats vanilla's list choice (elite when GetEliteCaravanSpawnChance(hero) > hero.RandomFloat()) and predicate (x.ShipHulls.Count == 0 != hero.CurrentSettlement.HasPort), and skips vanilla only when the pick would be null or the predicate would throw. No finalizer.
Files: Main/Features/LordSpawnGuard/Hooks/Patch147_CaravanTemplateGuard.cs, Main/Features/LordSpawnGuard/CaravanTemplateGuard.cs, Main/SubModule.cs (the TryPatchCategory("Patch147_CaravanTemplateGuard") line), TAOM.Tests/Features/LordSpawnGuard/Patch147CaravanTemplateGuardTests.cs, TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs (the Patch147 row), TAOM.Tests/Core/CulturePartyTemplateTests.cs, docs/reference/harmony-patch-registry.md (section Patch147_CaravanTemplateGuard), docs/features/lord-spawn-guard.md.

PART C -- in HEAD: six pull requests merged on 2026-11-13 (see each with: git show --stat <merge> ; git diff <merge>^1 <merge>)
- #1144, merge 534f79e12: War of the Ring momentum counts a raid only when the village is Looted. Main/Adapters/WarEventSnapshotAdapter.cs (FromRaid), Main/Features/WarOfTheRingMomentum/MomentumEventService.cs (ProcessRaid), Snapshots/RaidOutcomeSnapshot.cs.
- #1145, merge f110b8a33: charge knockdown returns null for a NaN or infinite victim max health or inflicted damage. Main/Features/CombatMechanics/ChargeKnockdownService.cs.
- #1146, merge 208753d29: PatchShield does not log or count a finalizer-only owner; a target counts as unpatched or withheld only after a real strip or withhold. Dependencies/Foundation/PatchShield.cs (TryUnpatchOffendingPatches), PatchShieldPolicy.cs (OwnersToStrip).
- #1148, merge b48307077: the missing-career warning logs once per hero and career. Main/Features/CareerSystem/Abilities/CareerAbilityService.cs.
- #1149, merge 5f02bd511, plus the comment and doc follow-up 1c93d60df: WarOfTheRingService.ShouldBlockPeace returns false when the master toggle is off. Main/Features/Diplomacy/WarOfTheRingService.cs; consumers Main/Features/Diplomacy/Models/TaomDiplomacyModel.cs (IsAtConstantWar), Models/TaomKingdomDecisionPermissionModel.cs, Hooks/PeaceActionHook.cs, Main/Features/WarPledge/WarPledgeService.cs. Decided by the maintainer: when the toggle goes on again in Full War, a peace made meanwhile ends and the war resumes; the doc says so.
- #1150, merge 33bd5b034: removes the unused AgentCombatAdapter and 33 interface members with their implementations, fakes and tests (83 files). Build and tests pass; the question is callers the compiler cannot see.

KNOWN SUSPECTS -- answer each with CONFIRMED or DISPUTED and the code you read:
S1 (Part A). HeroSpawnCampaignBehavior.SpawnLordParty now carries two TAOM finalizers that call PreserveForRethrow (Patch88_SpawnLordPartyScope, transparent; Patch65, which swallows InvalidOperationException), and from the second game start PatchShield's finalizer as a third. In the order Harmony 2.4.2 runs them, can the preserved trace be printed twice, lost, or attributed to the wrong method, and can Patch88 calling the preserver before Patch65 swallows change what Patch65 logs? Read Harmony's finalizer emit and RethrowStackPreserver.
S2 (Part A). CollectAssemblyTypesShim finalizes Assembly.GetTypes (a BCL method called from many threads and by the launcher). Is PreserveForRethrow safe there (reflection writes into the exception object, the Data dictionary, an exception shared between threads, an exception whose Data is read-only)? Does any of the nine converted sites sit on a hot path where the extra call on the null-exception path matters?
S3 (Part A). The gate matches only the statement `return __exception;`. Find every finalizer in Main, Client, Dependencies and CoopCompat that hands an exception back by another spelling (a local alias, an expression body, a ternary, `throw`), and say for each whether it loses the throw site. Also say whether any converted finalizer should have been `void` instead, per harmony-patches.md.
S4 (Part A). The pin test says com.taom.mod is unprotected by design. Check it against the code and the four feature docs that describe the rescue (docs/features/map-perf-profiler.md, mission-perf-heartbeat.md, mission-start-guard.md, load-time-stamps.md). List every Harmony owner id TAOM creates (new Harmony(...) in Main, Client, Dependencies, CoopCompat) with whether it is protected, and name any for which the current answer is harmful.
S5 (Part B). Compare Patch147's Prefix and CaravanTemplateGuard.Judge line by line with the installed SpawnCaravan and TaleWorlds.Core Extensions.GetRandomElementWithPredicate. Name every input for which the prefix skips although vanilla would have completed, and every input for which vanilla throws before CreateCaravanParty although the prefix lets it run. Include float precision of hero.RandomFloat(), a model that another module overrides, and the order of the checks in Judge. Are the four staged-object tests sound, or do they pass for a wrong reason?
S6 (Part C). #1149: with the toggle off then on in Full War, trace IsAtConstantWar, FactionManager.IsAtWarAgainstFaction, Kingdom.FactionsAtWarWith and ReconcileDeclaredWars. Is any state left that the next session launch does not repair, and can a co-op host and client disagree? #1144: can a real loot be missed or a non-loot counted (every raiser of RaidCompleted, the state of Village.VillageState when listeners run)? #1146: can _handled hide a target that should be retried or counted? #1150: is there any caller of a removed member that the compiler cannot see (reflection, a string name, XML or JSON, a Gauntlet binding, another module listed in docs/reference/external-mod-couplings.md)?

REQUIRED SECTIONS:
1. VANILLA CODE. Read these from the INSTALLED DLLs through your ilspy server (the dump at E:\Decompiled_Bannerlord is for browsing only) and paste the relevant lines as code blocks: CaravansCampaignBehavior.SpawnCaravan and its two callers; TaleWorlds.Core Extensions.GetRandomElementWithPredicate(MBReadOnlyList); RandomOwnerExtensions.RandomFloat; HeroSpawnCampaignBehavior.SpawnLordParty; RaidEventComponent.OnBeforeMapEventFinalize; FactionManager.IsAtWarAgainstFaction and Kingdom.UpdateFactionsAtWarWith. For Harmony, read 0Harmony.dll from the TAOM.Dependencies module folder: the finalizer part of MethodCreator (when it emits throw, when rethrow, in which order finalizers run).
2. PART A ANALYSIS. For each of the nine sites: what the finalizer is for, what it returned before and now, and one concrete scenario that proves or disproves that the throw site survives. Then the gate: what it catches and what it misses.
3. PART B ANALYSIS. The line-by-line comparison of S5, then the log path (ReportOnce key, lazy logger, the catch that returns true).
4. PART C ANALYSIS. One paragraph per pull request with a concrete scenario each.
5. CONFIG CROSS-REFERENCE. For #1149 read Main/_Module/ModuleData for the War of the Ring config and say which flags ShouldBlockPeace reads; for Part B read taom_spcultures.xml and the party templates and say whether any culture can reach the hazard today.
6. FINDINGS OR OBSERVATIONS. Each finding: severity (P1 ship-blocking, P2 real defect, P3 minor), file:line, the evidence, a failing scenario, and the smallest fix. Say plainly when a part has no finding.

QUALITY GATES:
- Every claim about engine or Harmony behaviour quotes the line that produces it; a name is not evidence.
- No finding that only repeats what vanilla itself does.
- Do the hard sections (1, 2 and the S5 comparison) in full; do not skip them for the easy ones.
- Mark anything you could not check as UNVERIFIED and say what was missing.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write it to docs/reviews/raw or to any other path, and do not change any file.
