# Input pack: WP 105 composition-root

Title (draft): Add the plan 018 feature-module runner (empty) and move WandererAllegiance into the first module
Wave: 0   Lane: code   Draft priority: P2   Groups: 1   Port commits: 6
Depends on (WP ids): 104

## Orchestrator notes for this WP (binding)
- Decision D2: ADOPT the infrastructure now. Port END STATES: Main/Composition from merge 1efdf2985, tests from 0f3af9577. Exclude RegisterCampaignObjects (8b015667f), ClientComposition/IClientComposition and every tip-only contract. a0db0558b is WP104's.
- Follow D1's commit B and commit C exactly (anchors, six FeatureModuleHooks calls, two IoC lines, grep gates, T0 + 42 tests).
- Add a reusable 'how to port a 1.5.x feature module onto 1.4.x' section to docs (the downstream WPs cite it).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D1-plan-018-composition-root.md

## Port groups owned by this WP (from the commit classification)

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
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 5
