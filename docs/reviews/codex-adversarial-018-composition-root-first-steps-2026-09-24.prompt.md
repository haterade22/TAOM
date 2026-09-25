# Codex adversarial review: plan 018 (composition-root-first-steps), branch improve/018-composition-root-first-steps

Feature: Start the feature-module composition root with one shared source reader, the module contract, an empty module list and one pilot feature. `Main/SubModule.cs` is 2,148 lines at `b2e387db` (758 in June) and every feature is wired into it and into `Main/IoC.cs` by hand: services, patch categories, hook handshakes, campaign behaviors, game models and mission behaviors. Since the June audit, 131 of the 349 commits that touched `Main/Features/` (38%) also had to edit one of those two single-owner files, which is why parallel sessions keep colliding there (both files carry another session's uncommitted edits right now). The 26 test files that guard this wiring read the two files as raw text, so they pin its spelling and cannot tell code from a comment: `GameModelOverrideBindingTests` counts `TaomPartyNavigationModel` as registered only because a commented-out line contains `new TaomPartyNavigationModel(`. This plan lays the first three stones of the approved design: one shared, comment-aware source reader for those tests (with the parked model made explicit), the feature-module contract with an empty ordered list and a runner called once at the end of each lifecycle phase (so behaviour is identical and, from then on, the single-owner files only lose lines), and one pilot feature, WandererAllegiance, moved into its own module with its text asserts replaced.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 4c728dac..improve/018-composition-root-first-steps
- Any file as the branch has it: git show improve/018-composition-root-first-steps:<path>
- The base for comparison: git show 4c728dac:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/018-composition-root-first-steps:plans/018-composition-root-first-steps.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The precondition fails: `Main/PatchCategoryApplier.cs` is missing, `ReportPatchFailures(` does not count 5, or `TryPatchCategory` is not a `private bool TryPatchCategory(string category)` in `SubModule.cs` (plan 009 has not landed, or landed differently; every SubModule anchor in this plan depends on it).
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check shows any change beyond plan 009's (listed at the top), or a "Current state" excerpt does not match the live code.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? `git worktree add` fails because `plan/018-composition-root` or `E:/repos/wt-plan-018` already exists.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? The checker's first run (start of Step 0.3, after Step 0.2) prints a count other than 41 old-style reads (a test was added, removed or rewritten since planning).
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The Step 0.2 RED run is Inconclusive (game assemblies not loaded on this machine) or lists any model other than `TAOM.Features.NavalTravel.Models.TaomPartyNavigationModel`.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Any test in the 26 files fails after it moves to the comment-stripped view: that test asserts on text that lives only in a comment. Report the test and the asserted string; do not switch the file back to raw text.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (42 files changed, 1278 insertions(+), 264 deletions(-)):
C#:
- Main/Composition/FeatureDecls.cs
- Main/Composition/FeatureModuleHooks.cs
- Main/Composition/FeatureModules.cs
- Main/Composition/ITaomFeatureModule.cs
- Main/Composition/ModuleRunner.cs
- Main/Composition/TaomFeatureModule.cs
- Main/Features/WandererAllegiance/WandererAllegianceIoC.cs
- Main/Features/WandererAllegiance/WandererAllegianceModule.cs
- Main/IoC.cs
- Main/SubModule.cs
Tests:
- TAOM.Tests/Composition/FeatureModulesTests.cs
- TAOM.Tests/Composition/ModuleRunnerTests.cs
- TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs
- TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs
- TAOM.Tests/Features/BannerColorPersistence/BannerTripletOrderingTests.cs
- TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallDisarmTests.cs
- TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs
- TAOM.Tests/Features/CoopInterop/ResetForUnloadSweepTests.cs
- TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs
- TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs
- TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs
- TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs
- TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs
- TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs
- TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs
- TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs
- TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs
- TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs
- TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs
- TAOM.Tests/Features/Refuge/RefugeWiringTests.cs
- TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs
- TAOM.Tests/Features/SettlementGuards/SettlementGuardsWiringTests.cs
- TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs
- TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs
- TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs
- TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesWiringTests.cs
- TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs
- TAOM.Tests/Infrastructure/RepoPaths.cs
- TAOM.Tests/Infrastructure/RepoPathsTests.cs
- TAOM.Tests/Migration/GameModelOverrideBindingTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/wanderer-allegiance.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW
