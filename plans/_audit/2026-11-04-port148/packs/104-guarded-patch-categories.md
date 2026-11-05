# Input pack: WP 104 guarded-patch-categories

Title (draft): Apply every Harmony patch category through one guard (plan 009), add RepoPaths and the honest wiring reader
Wave: 0   Lane: code   Draft priority: P1   Groups: 2   Port commits: 9
Depends on (WP ids): 101

## Orchestrator notes for this WP (binding)
- Port plan 009 from its post-merge end state (998ae7e95; commits 45bcf80bd, 9da9b5b92, bdf7d515c, 4c728dac0, 7912fdd8f, b6cb6ff5d, 7eae47044, 27fd23bbb). Do not take 346f3e093 (WP118).
- RepoPaths: take TAOM.Tests/Infrastructure/RepoPaths.cs with ReadSource/StripComments/MentionsOutsideComments and CodeRoots = { Main } (Client/ does not exist on 1.4.x); V03 says 35007dbb8's 22-line version lacks ReadSource: use the 4f27b3bde / 06252ae2c shape.
- Take a0db0558b (plan 018 step 0, owned by group plan-018-composition-root): the test-only reader that closes a false pass on 1.4.x (GameModelOverrideBindingTests counts the commented TaomPartyNavigationModel AddModel); add ParkedModels.
- D4 (Patch88): BHA0001 is a false positive (the analyzer cannot resolve nested types; LordPartyComponent.InitializationArgs.InitializeLordPartyProperties exists on v1.4.8); silence it with a scoped pragma on that one attribute.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D1-plan-018-composition-root.md
- plans/_audit/2026-11-04-port148/verify/D4-patch88-and-binding-health.md
- plans/_audit/2026-11-04-port148/verify/V03-skeleton-buffer-v148.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Deep-dive findings and non-port items this WP owns
- LOAD-TIME-04 [P2/high] One throwing patch category in the once-only game-init batch skips every later category and escapes an unguarded engine loop | port: 45bcf80bd,plan 009  (report: plans/_audit/2026-11-04-port148/deepdive/load-time.md)

## Port groups owned by this WP (from the commit classification)

### group `plan-009-guarded-patch-category-apply`  P1  track=crash-robustness  commits=8  sim: clean=1 conflict=7
symptoms: crash, loading-hang
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/PatchCategoryApplier.cs, Main/PatchCategoryIndex.cs, Main/SubModule.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs, TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs, tools/translation_cache/de.json, tools/translation_cache/fr.json, tools/translation_cache/jp.json
- `45bcf80bd` 2026-09-23 PORT-PARTIAL P1 pick=conflict :: fix(harmony): v2.0.30 - apply every patch category through one guard
  - why: One unresolvable Harmony target throws out of SubModule; on 1.4.x the game-init batch sets _gameInitPatchesApplied first (SubModule.cs:1324-1325) and then runs bare calls, so one failure silently drops every later category, including the Patch65, Patch80, Patch82 and Patch84 crash guards.
  - port notes: Take PatchCategoryApplier, TryPatchCategory and PatchCategoryApplierTests (its only dependency, IModLogger, exists at Main/Core/Logging/IModLogger.cs on 1.4.x). Redo the SubModule conversion by hand: 1.4.x SubModule.cs has 82 lines matching PatchCategory( and its own guarded sites (Patch37, 43, 55, 60, 61, 62, 63_BannerBearerSpawnGuard, 68, 77, 78, 79, 83 and the preview batch), whose side effects must be kept as the 1.5.x commit kept its own. Update the nine text tests that exist on 1.4.x. Both lines use Lib.Harmony 2.4.2. Port the series' final shape through 27fd23bbb (outside this batch) and leave 346f3e093 (load-stamps timing) and d021426b0/6d7e67f3f (co-op client split; Client/ is absent on 1.4.x). DECISIONS #28 (2026-09-24) already lists 009 as a planned 1.4.5 port.
  - conflicts: Main/SubModule.cs
- `9da9b5b92` 2026-09-23 PORT P3 pick=clean :: docs(harmony): v2.0.30 - correct Patch37 coverage, name the guard
  - why: Corrects the claim that Patch37's finalizers cover TAOM's own OnSubModuleLoad; the same wrong claim sits on 1.4.x at SubModule.cs:155-156 and crash-report.md:280 ('to maximise coverage of our own init').
  - port notes: Both docs exist on 1.4.x; also correct the matching SubModule.cs comment. The TryPatchCategory references need 45bcf80bd.
  - prereqs: 45bcf80bd
- `bdf7d515c` 2026-09-24 PORT-PARTIAL P2 pick=conflict :: fix(harmony): v2.0.30 - review follow-ups for plan 009
  - why: Moves the module-load failure notice into a queued inquiry at the first main menu (InformationManager has no subscriber during OnSubModuleLoad) and corrects what a failed category leaves applied.
  - port notes: Take the PatchCategoryApplier, SubModule and test changes, the battle-load-diagnostics, crash-report and lifecycle doc text, and the tools/triage_battle_load.py hint: 1.4.x's tool still names the 'Patch43 diagnostics failed to apply' warning at :530 and :1096, which the plan 009 conversion removes. Skip the 5-data-flow lens hunk unless the lens split is ported (lenses/ is absent on 1.4.x). The Harmony and localization lessons can ride along; review records are bookkeeping.
  - prereqs: 45bcf80bd
  - conflicts: Main/PatchCategoryApplier.cs, Main/SubModule.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs
- `4c728dac0` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(harmony): v2.0.30 - convergence fixes for plan 009
  - why: Plan 009 convergence: comments and docs now name the assembly-wide category-index failure case; no runtime change.
  - port notes: Take the SubModule comment and the crash-report.md and lifecycle-doc text. Skip the plan 018 references (Main/Composition is 1.5.x-only) and the review-record hunks.
  - prereqs: bdf7d515c
  - conflicts: Main/SubModule.cs
- `7912fdd8f` 2026-09-24 PORT-PARTIAL P1 pick=conflict :: fix(harmony): v2.0.30 - apply maintainer decisions for plan 009
  - why: PatchCategoryIndex builds Harmony's category index class by class, so one [HarmonyPatch] naming a type the engine no longer has costs that class instead of failing every category.
  - port notes: Take PatchCategoryIndex, PatchCategoryIndexTests (emitted probe assembly) and the applier and SubModule wiring; this is the engine-bump safety net for 1.4.x too. Later 1.5.x edits to PatchCategoryIndex (d021426b0, 6d7e67f3f) are co-op client-only; leave them.
  - prereqs: 45bcf80bd, bdf7d515c
  - conflicts: Main/PatchCategoryApplier.cs, Main/SubModule.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs
  - issues: 653
- `b6cb6ff5d` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(loc): v2.0.30 - localize the patch failure notice for plan 009
  - why: Localizes the startup patch-failure notice with five taom_patch_apply_* keys and AI first drafts in all 12 languages.
  - port notes: Take the applier and SubModule TextObject changes, the five rows in taom_module_strings.xml, and the language and translation-cache rows. Place the language rows inside <strings> by hand: the 1.4.x language files end their string list with the same `</strings>\r\r\n` line (checked in DE on origin/bannerlord-1.4.5) that made translate_with_claude.py's sync_missing_ids strand rows after </strings>, the defect 7eae47044 fixed. Expect conflicts in the 12 language files and caches (bookkeeping hot spots).
  - prereqs: 7912fdd8f
  - conflicts: Main/PatchCategoryApplier.cs, Main/SubModule.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs, TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs
  - issues: 653
- `7eae47044` 2026-09-24 PORT P3 pick=conflict :: fix(loc): v2.0.30 - move the notice translations inside <strings>
  - why: Moves the 60 seeded translations inside <strings>, the only place LocalizedTextManager reads them; b6cb6ff5d had put them after </strings>.
  - port notes: Fold into the b6cb6ff5d port by placing the rows correctly once rather than replaying the misplacement. The seeding bug in tools/translate_with_claude.py is not fixed by this commit; whether 1.4.x's copy has the same sync_missing_ids anchor is unchecked.
  - prereqs: b6cb6ff5d
- `27fd23bbb` 2026-09-24 PORT P2 pick=conflict :: fix(harmony): v2.0.30 - review follow-ups for plan 009
  - why: Review follow-up for plan 009 (absent on 1.4.x, whose Main/SubModule.cs still has 82 bare _harmony.PatchCategory calls): pins the null-category guard, fixes the DE/FR/JP notice text, and adds a LanguageDataXmlTests gate for string rows outside root <strings>.
  - port notes: Apply after the plan 009 implementation series (45bcf80bd, 9da9b5b92, bdf7d515c, 4c728dac0, 7912fdd8f, b6cb6ff5d, 7eae47044); PatchCategoryApplier.cs and PatchCategoryIndex.cs are new on 1.4.x. The new gate can land on its own: v1.4.8 LocalizedTextManager.LoadLanguage reads only <string> under root-level <strings> (C:\Decompiled_Bannerlord\_categories_v1.4.8\Core\TaleWorlds.Localization\TaleWorlds.Localization\LocalizedTextManager.cs:243-251), and all 168 per-language XML files on origin/bannerlord-1.4.5 have zero stray rows, so it lands green; re-cite its comment from v1.5.3 to v1.4.8. Translation rows and tools/translation_cache entries move together. Review records, REVIEW-LOG and CHANGELOG are bookkeeping (write a 1.4.x entry, v2.0.28 subject).
  - prereqs: 45bcf80bd, 9da9b5b92, bdf7d515c, 4c728dac0, 7912fdd8f, b6cb6ff5d, 7eae47044
  - conflicts: Main/PatchCategoryApplier.cs, Main/PatchCategoryIndex.cs, TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs, tools/translation_cache/de.json, tools/translation_cache/fr.json, tools/translation_cache/jp.json
skipped in this group: `aa7a55181` SKIP-BOOKKEEPING fix(docs): v2.0.30 - convergence fixes for plan 009; `4da415e21` SKIP-BOOKKEEPING docs(reviews): v2.0.30 - Codex prompt for plan 009

### group `tests-repopaths-infra`  P3  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Composition/FeatureModulesTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs, TAOM.Tests/Features/EyeOfSauron/EyeOfSauronWiringTests.cs, TAOM.Tests/Features/FiefManagement/FiefsNavButtonWiringTests.cs, TAOM.Tests/Features/WarChronicle/Events/WarChronicleEventsWiringTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleWiringTests.cs, TAOM.Tests/Infrastructure/RepoPaths.cs, TAOM.Tests/Infrastructure/RepoPathsTests.cs
- `06252ae2c` 2026-10-14 PORT-PARTIAL P3 pick=conflict :: test(infra): v2.0.34 - faster wiring scans, pin the comment scan
  - why: Adds RepoPaths.MentionsOutsideComments (raw-text prefilter before comment stripping), which the later main-party scan uses; RepoPaths and the six wiring tests edited here do not exist on 1.4.x.
  - port notes: Take only MentionsOutsideComments and its RepoPathsTests rows, as part of porting TAOM.Tests/Infrastructure/RepoPaths.cs (absent on 1.4.x; begun in 35007dbb8, which is not on 1.4.x, and extended later; simplest to take the file from the 1.5.x tip). Needed by tests in 91f369984, dc1c37031 and c74ad5aed. Skip the six wiring-test edits (files absent on 1.4.x). If those tests are adapted to local helpers instead (1.4.x tests use private StripComments/FindMainSourceDir, e.g. CoopVetoClassificationTests.cs:303-306), this becomes SKIP.
  - conflicts: TAOM.Tests/Composition/FeatureModulesTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingWiringTests.cs, TAOM.Tests/Features/EyeOfSauron/EyeOfSauronWiringTests.cs, TAOM.Tests/Features/FiefManagement/FiefsNavButtonWiringTests.cs, TAOM.Tests/Features/WarChronicle/Events/WarChronicleEventsWiringTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleWiringTests.cs, TAOM.Tests/Infrastructure/RepoPaths.cs, TAOM.Tests/Infrastructure/RepoPathsTests.cs
  - issues: 792

## SHARED group, owned by WP 105: take ONLY the hunks or commits your notes name; the owner ports the rest

### group `plan-018-composition-root`  P2  track=architecture  commits=6  sim: clean=0 conflict=6
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureDecls.cs, Main/Composition/FeatureModuleHooks.cs, Main/Composition/FeatureModules.cs, Main/Composition/ModuleRunner.cs, Main/Composition/TaomFeatureModule.cs, Main/SubModule.cs, TAOM.Tests/Composition/FeatureModulesTests.cs, TAOM.Tests/Composition/ModuleRunnerTests.cs, TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs, TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs, TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs, TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs, TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs, TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs, TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs, TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs, TAOM.Tests/Infrastructure/RepoPaths.cs, TAOM.Tests/Infrastructure/RepoPathsTests.cs
- `a0db0558b` 2026-09-24 PORT-PARTIAL P2 pick=conflict :: test(composition): v2.0.30 - read SubModule and IoC through one reader
  - why: Test-only: routes the wiring tests that read Main/SubModule.cs and Main/IoC.cs through RepoPaths.ReadSource with comment stripping, which closes a false pass that also exists on 1.4.x (GameModelOverrideBindingTests reads SubModule.cs raw at origin/bannerlord-1.4.5:TAOM.Tests/Migration/GameModelOverrideBindingTests.cs:55 and counts the commented-out TaomPartyNavigationModel AddModel at Main/SubModule.cs:938 as registered).
  - port notes: Take RepoPaths.ReadSource/StripComments, RepoPathsTests, the GameModelOverrideBindingTests ParkedModels list, and the conversions of the 25 test files that exist on 1.4.x (keep AutoResolveDiagnosticsWiringTests: that feature is still on 1.4.x). Skip TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs (SignatureStrikes is new on 1.5.x). RepoPaths.cs is absent on 1.4.x: its 22-line base came from the post-fork commit 35007dbb8, so create it from that base or take that part first. Worth taking even if plan 018 is not adopted.
  - prereqs: 35007dbb8
  - conflicts: TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs, TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs, TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs, TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs, TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs, TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs, TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs, TAOM.Tests/Infrastructure/RepoPaths.cs
- `9ed3ee308` 2026-09-24 PORT P3 pick=conflict :: refactor(composition): v2.0.30 - add the feature-module runner, empty
  - why: Plan 018 step 1 adds Main/Composition (module contract, declarations, ModuleRunner, FeatureModuleHooks) with an empty module list and one runner call per SubModule phase, an in-scope architecture refactor with no behaviour change.
  - port notes: Main/Composition is a 1.5.x-only project and adopting it is a line-wide decision: on the 1.5.x tip 82 feature modules derive TaomFeatureModule and 92 commits touch Main/Composition, so later 1.5.x fixes to migrated features port cleanly only if this lands. Hard prerequisite: plan 009 (the runner calls go through TryPatchCategory and sit before ReportPatchFailures in SubModule.cs). Port the series end state (after fa0b24e13, ecbc2199e, 0f3af9577 and the merge 1efdf2985, which carries its own resolutions in IoC.cs, SubModule.cs and tests), not this commit alone: ITaomFeatureModule.cs is deleted again in fa0b24e13. Edits the single-owner IoC.cs and SubModule.cs.
  - prereqs: 45bcf80bd, a0db0558b
  - conflicts: Main/SubModule.cs
- `44045b348` 2026-09-24 PORT P3 pick=conflict :: refactor(composition): v2.0.30 - move WandererAllegiance into a module
  - why: Plan 018 step 2 moves WandererAllegiance, a shared feature hand-wired on 1.4.x at Main/SubModule.cs:1247 and Main/IoC.cs:106, into the first feature module with behaviour-neutral rewiring.
  - port notes: Only together with 9ed3ee308. WandererAllegianceIoC changes from IContainer to IRegistrator. Take the FeatureModulesTests declaration tests and the DryIoc handshake test; the feature doc edit applies; CHANGELOG is bookkeeping.
  - prereqs: 9ed3ee308
  - conflicts: Main/Composition/FeatureModules.cs, TAOM.Tests/Composition/FeatureModulesTests.cs, TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs
- `fa0b24e13` 2026-09-24 PORT P3 pick=conflict :: fix(composition): v2.0.30 - review follow-ups for plan 018
  - why: Review follow-up for plan 018: fixes a dormant runner gap (a faulted save-owning module was skipped before the fail-closed check), deletes ITaomFeatureModule and FeatureState, and adds FeatureModuleHooks tests against real game starters.
  - port notes: Only with 9ed3ee308 and 44045b348; code and tests apply as a unit. Review record, RCA and REVIEW-LOG are bookkeeping; the state-lifecycle-save and testing-qa lessons are optional. The CHANGELOG known limitation (Patch37 finalizer swallows the campaign-start throw) describes 1.5.x Patch37; recheck it against the 1.4.x Patch37 once plan 006 is ported.
  - prereqs: 9ed3ee308, 44045b348
  - conflicts: Main/Composition/FeatureDecls.cs, Main/Composition/FeatureModuleHooks.cs, Main/Composition/FeatureModules.cs, Main/Composition/ModuleRunner.cs, Main/Composition/TaomFeatureModule.cs, Main/SubModule.cs, TAOM.Tests/Composition/FeatureModulesTests.cs, TAOM.Tests/Composition/ModuleRunnerTests.cs, TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs, TAOM.Tests/Infrastructure/RepoPathsTests.cs
- `ecbc2199e` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(composition): v2.0.30 - convergence fixes for plan 018
  - why: Convergence fix for plan 018: comment wording in FeatureModuleHooks.cs and FeatureModules.cs plus CHANGELOG, lesson and RCA prose, with no behaviour change.
  - port notes: Take the two code-comment edits (they name four hand-wired blocks after the module call; verify that count against the 1.4.x SubModule.cs) and the state-lifecycle-save lesson wording if that lesson was carried; drop the CHANGELOG and RCA hunks.
  - prereqs: fa0b24e13
  - conflicts: Main/Composition/FeatureModuleHooks.cs, Main/Composition/FeatureModules.cs
- `0f3af9577` 2026-09-25 PORT P3 pick=conflict :: fix(composition): v2.0.30 - kernel test anchors follow 009's localized reports
  - why: Merge follow-up that moves the FeatureModulesTests kernel anchors to plan 009's localized TextObject ReportPatchFailures form; test-only.
  - port notes: Needed only when both plan 009 (including b6cb6ff5d's localized reports) and plan 018 are ported; apply after both.
  - prereqs: b6cb6ff5d, 9ed3ee308
  - conflicts: TAOM.Tests/Composition/FeatureModulesTests.cs
skipped in this group: `667002d8d` SKIP-BOOKKEEPING docs(reviews): v2.0.30 - Codex prompt and re-cut plan 018; `71fd21698` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - cite #662 for plan 018

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 4, batch 5, batch 16
