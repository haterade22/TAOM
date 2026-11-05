# Input pack: WP 122 test-infrastructure-gates

Title (draft): Binding gates that fail instead of skipping, patch-number and transpiler-site gates, engine data fakes
Wave: 2   Lane: E   Draft priority: P2   Groups: 12   Port commits: 28
Depends on (WP ids): 104, 115

## Orchestrator notes for this WP (binding)
- Decision D9: do not re-add CI (ccef172ad removed it on both lines: 57 of 186 runs passed). Plan 010's CI commits and its RefAsm no-game gate (8589a3384) are recorded as skipped. Plan 019 (nullable ratchet) is an optional final part.
- Plan 008's binding-gate runsettings (MapInconclusiveToFailed) makes an unlisted engine build fail the native pin gate instead of reading Skipped (V03).
- D4: the binding gate's lookup rules differ from Harmony 2.4.2's and some manual patches are unguarded: include the stricter-gate work (D4 WP-C).
- issue-1020 edits Directory.Build.props after WP115's plan 017 edits (maintainer applies Directory.Build.props diffs).

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/D4-patch88-and-binding-health.md
- plans/_audit/2026-11-04-port148/verify/V03-skeleton-buffer-v148.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md

## Port groups owned by this WP (from the commit classification)

### group `transpiler-site-binding-gate`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
- `8b9f0a233` 2026-09-14 PORT-PARTIAL P2 pick=conflict :: chore(engine): v2.0.28 - compile TAOM against Bannerlord v1.5.2
  - why: The v1.5.2 compile adaptation is 1.5-only, but it introduces TranspilerSiteBindingTests (feed the real installed IL through each production transpiler and fail on its fail-safe warning) and one engine-agnostic harmony-patches.md rule bullet, neither present on 1.4.x.
  - port notes: Take only: (1) TAOM.Tests/Migration/TranspilerSiteBindingTests.cs as a pattern, REWRITTEN for the 1.4.x transpilers on v1.4.8. As committed it binds 1.5.x-only types (MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch, Patch53_PartyIconScaleHumanVisual, PartyIconScaleTranspiler.RewriteIconSites/RewriteHumanVisualSite); on origin/bannerlord-1.4.5 PartyIconScaleTranspiler exposes only Rewrite(...), and the line has nine transpiler files (Patch42 x2, Patch53, Patch81, Enlistment BehaviorComponent, BannerColorPersistence x2, CharacterSelection RefreshCharacterEntityAux, RaceAge DeliverOffSpring). The 1.4.5 to 1.4.8 move makes a silently non-matching transpiler plausible, which is exactly what this gate catches. (2) The .claude/rules/harmony-patches.md bullet requiring every caller of a target with the actor it supplies (absent on 1.4.x). Leave everything else: pins, Execution re-home onto the blood-feud seam, BannerColor/PartyIconScale v1.5.0 transpilers, ten-phase OnCharacterCreationIsOver guards, ClanPartyItemWithPartyVM refuge rows, test re-derivations, registries and docs.

### group `harmony-binding-hasbody-gate`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
- `197454e68` 2026-09-14 PORT-PARTIAL P2 pick=conflict :: fix(troopweight): v2.0.28 - patch the two concrete clan rows, not the abstract base
  - why: ClanPartyItemVM.UpdateProperties still has a body on v1.4.8, so the TroopWeight retarget is 1.5-only, but the HasBody check in HarmonyPatchBindingTests (an abstract or extern target resolves by name yet aborts its category at apply time) is a portable gate.
  - port notes: Take the three HasBody checks and the HasBody helper in TAOM.Tests/Migration/HarmonyPatchBindingTests.cs. Drop or re-pin the negative test AnAbstractEngineMethod_ResolvesButHasNoBody_SoTheGateRefusesIt: on v1.4.8 ClanPartyItemVM.UpdateProperties is concrete (ClanPartyItemVM.cs:1125), so it would fail. Leave the ClanPartyItemVM_UpdateProperties_Patch TargetMethods rewrite (ClanPartyItemWithPartyVM/WithHeroVM do not exist on v1.4.8).

### group `plan-008-binding-gate-no-silent-skips`  P2  track=tests-ci  commits=6  sim: clean=0 conflict=6
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .github/workflows/build.yml, TAOM.Tests/Migration/BindingGateRunSettingsTests.cs, TAOM.Tests/Migration/GameAssemblies.cs, TAOM.Tests/Migration/GameAssembliesResolutionTests.cs, TAOM.Tests/Migration/GameModelOverrideBindingTests.cs, TAOM.Tests/Migration/HarmonyPatchBindingTests.cs, TAOM.Tests/binding-gate.runsettings, tools/test_hooks.sh
- `8c89e042f` 2026-09-23 PORT-PARTIAL P2 pick=conflict :: test(bindings): v2.0.30 - make the binding gate fail loudly on skips
  - why: The binding gate passes by skipping when the test process lacks the game variables, and 1.4.x's GameAssemblies.cs resolves only BANNERLORD_OVERRIDE_DIR or BANNERLORD_GAME_DIR, so the same silent pass exists there.
  - port notes: Take the TaomGameFolder assembly metadata in TAOM.Tests.csproj and the GameAssemblies fallback (the variables keep precedence), GameAssembliesResolutionTests, TAOM.Tests/binding-gate.runsettings (MapInconclusiveToFailed), the discovery floors that now fail in GameModelOverrideBindingTests and HarmonyPatchBindingTests, and the verify-bindings skill and doc command lines. Leave the build.yml CI step (1.4.x deleted CI in ccef172ad on 2026-10-05) and the notify-test-results.sh and test_hooks.sh 7c banner (reverted by 37306bca3). Flag for the orchestrator: a #652 comment recorded 'no port to bannerlord-1.4.5' for plan 008 (rca-binding-gate-no-silent-skips-2026-09-24.md:112-115 on origin/bannerlord-1.5.x), and DECISIONS #28 lists only 006, 007, 009, 012, 014 and 015 as 1.4.5 ports; confirm with Mike before porting. Port the net state through 1f5047572 as one change.
  - conflicts: .github/workflows/build.yml, tools/test_hooks.sh
- `549afffde` 2026-09-24 PORT-PARTIAL P2 pick=conflict :: fix(bindings): v2.0.30 - review follow-ups for plan 008
  - why: Plan 008 review follow-ups: two tests pin the resolver's override and game-dir guards, plus corrected comments and skill text.
  - port notes: Take the GameAssemblies.cs, GameAssembliesResolutionTests, binding-test floor-message, verify-bindings SKILL.md and reflection-sites.md hunks. Skip notify-test-results.sh, test_hooks.sh and the hooks-catalog banner text (reverted by 37306bca3), and the review records.
  - prereqs: 8c89e042f
  - conflicts: TAOM.Tests/Migration/GameAssemblies.cs, TAOM.Tests/Migration/GameAssembliesResolutionTests.cs, TAOM.Tests/Migration/GameModelOverrideBindingTests.cs, TAOM.Tests/Migration/HarmonyPatchBindingTests.cs, tools/test_hooks.sh
- `2ca0805bc` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(bindings): v2.0.30 - convergence fixes for plan 008
  - why: Plan 008 convergence fixes; the notify hook part was reverted two commits later, leaving a verify-bindings precondition note and two test comments.
  - port notes: Take only the verify-bindings Step 2 sentence ("Main/SubModule.cs not found" is a precondition to report) and the two test comments. Skip notify-test-results.sh and test_hooks.sh 7c, which 37306bca3 restored to their 7f02fc8d content.
  - prereqs: 549afffde
  - conflicts: TAOM.Tests/Migration/GameAssembliesResolutionTests.cs, tools/test_hooks.sh
- `37306bca3` 2026-09-24 PORT-PARTIAL P2 pick=conflict :: fix(bindings): v2.0.30 - apply maintainer decisions for plan 008
  - why: Maintainer decisions for plan 008: TreatNoTestsAsError in the strict runsettings, a BindingGateRunSettingsTests pin, and the skip banner dropped.
  - port notes: Take binding-gate.runsettings (TreatNoTestsAsError), BindingGateRunSettingsTests and the verify-bindings and hooks-catalog text. The hook revert is a no-op if the banner hunks were never ported.
  - prereqs: 8c89e042f
  - conflicts: TAOM.Tests/binding-gate.runsettings, tools/test_hooks.sh
  - issues: 652
- `aa59f68ed` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: fix(binding-gate): v2.0.30 - review follow-ups for plan 008
  - why: Plan 008 round-two follow-ups: accurate wording of what the strict settings fail, zero-match triage in verify-bindings, and a shared repo-path helper in the runsettings test.
  - port notes: Take the verify-bindings Step 2 and hooks-catalog wording. The switch to RepoPaths.RepoPath needs a RepoPaths class that 1.4.x lacks (git grep 'class RepoPaths' on the tip finds none): keep the local walker or port RepoPaths first. Review records and CHANGELOG headings are bookkeeping.
  - prereqs: 37306bca3
  - conflicts: TAOM.Tests/Migration/BindingGateRunSettingsTests.cs
  - issues: 652
- `1f5047572` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: fix(binding-gate): v2.0.30 - convergence fixes for plan 008
  - why: Plan 008 convergence: the verify-bindings zero-match triage now names MSTest's two discovery warnings correctly.
  - port notes: Take the one verify-bindings SKILL.md line; the rest is review records and CHANGELOG.
  - prereqs: aa59f68ed

### group `plan-010-ci-on-hosted-windows`  P3  track=tests-ci  commits=5  sim: clean=0 conflict=5
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .github/workflows/build.yml, .github/workflows/csharp.yml, GameReferences.targets, Main/TAOM.csproj, TAOM.Tests/Core/LordInlineSkillParityTests.cs, TAOM.Tests/Features/AdvancedCombat/CreatureImpactSoundTests.cs, TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs, TAOM.Tests/Features/AdvancedCombat/SyntheticBlowScopeTests.cs, TAOM.Tests/Features/AdvancedStartOptions/TaomStartOptionsProviderTests.cs, TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs, TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs, TAOM.Tests/Features/CultureDoctrine/ArcherFlankGeometryTests.cs, TAOM.Tests/Features/CultureDoctrine/CultureDoctrineBindingTests.cs, TAOM.Tests/Features/CultureDoctrine/CultureDoctrinePhaseCBindingTests.cs, TAOM.Tests/Features/CultureDoctrine/CultureMoraleAndAggressionTests.cs, TAOM.Tests/Features/CultureDoctrine/DoctrineSwitchInvariantTests.cs, TAOM.Tests/Features/CultureDoctrine/EnvelopAndVolleyTests.cs, TAOM.Tests/Features/Elephant/HowdahCrewLoadoutTests.cs, TAOM.Tests/Features/Elephant/HowdahHarnessItemTests.cs, TAOM.Tests/Features/Elephant/HowdahPrefabTests.cs, TAOM.Tests/Features/Elephant/LegacyHowdahPrefabTests.cs, TAOM.Tests/Features/Elk/ElkConfigTests.cs, TAOM.Tests/Features/LocalizationOverride/GlobalStringsOverridesTests.cs, TAOM.Tests/Features/MapLoadDiagnostics/MapLoadDiagnosticsBehaviorTests.cs, TAOM.Tests/Features/MountDespawn/MountDespawnOffThreadTests.cs, TAOM.Tests/Features/Mumakil/MumakilPlatformTests.cs, TAOM.Tests/Features/PlayerPossession/PlayerPossessionBehaviorPhaseGuardTests.cs, TAOM.Tests/Features/PlayerSwitcher/KingdomJoinOfferBehaviorTests.cs, TAOM.Tests/Features/SignatureStrikes/StrikeRequestBufferTests.cs ...
- `b8c000452` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: ci(tests): v2.0.30 - build and test C# on hosted Windows runners
  - why: Plan 010 centralises game references in GameReferences.targets with a BUTR reference-assembly build mode and tags game-dependent tests; its hosted CI workflow was deleted on both lines on 2026-10-05.
  - port notes: Take GameReferences.targets (Install default; RefAsm needs BannerlordRefAsmVersion set to BUTR's v1.4.8 build, changeset 119303 per the brief, and whether nuget.org publishes it is UNVERIFIED), the Reference rewiring in Main, Dependencies and TAOM.Tests csproj files, GameReferencesTargetsTests, the RequiresGame/RequiresGameIL/LiveInstall tags for test classes that exist on 1.4.x (the tagger manifest was a scratch file and was never committed, so re-derive it), the tests.md tag rules and the .ai/verification.md no-game recipe. Leave csharp.yml and the build.yml deletion: 1.4.x removed CI in ccef172ad and 1.5.x removed csharp.yml in c8d69a0dc. RefAsm survives on the 1.5.x tip (pinned 1.5.6.125053-beta). Low value on 1.4.x because every 1.4.x build machine has the game; port only if agents need game-free builds.
  - conflicts: .github/workflows/build.yml, TAOM.Tests/Core/LordInlineSkillParityTests.cs, TAOM.Tests/Features/AdvancedCombat/CreatureImpactSoundTests.cs, TAOM.Tests/Features/AdvancedCombat/CustomAttacksUtilsBlowFlagsTests.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridRemovalTests.cs, TAOM.Tests/Features/AdvancedCombat/SyntheticBlowScopeTests.cs, TAOM.Tests/Features/AdvancedStartOptions/TaomStartOptionsProviderTests.cs, TAOM.Tests/Features/Animalia/AnimaliaMountWiringTests.cs, TAOM.Tests/Features/CareerSystem/CareerPerkOffThreadTests.cs, TAOM.Tests/Features/CultureDoctrine/ArcherFlankGeometryTests.cs, TAOM.Tests/Features/CultureDoctrine/CultureDoctrineBindingTests.cs, TAOM.Tests/Features/CultureDoctrine/CultureDoctrinePhaseCBindingTests.cs, TAOM.Tests/Features/CultureDoctrine/CultureMoraleAndAggressionTests.cs, TAOM.Tests/Features/CultureDoctrine/DoctrineSwitchInvariantTests.cs, TAOM.Tests/Features/CultureDoctrine/EnvelopAndVolleyTests.cs, TAOM.Tests/Features/Elephant/HowdahCrewLoadoutTests.cs, TAOM.Tests/Features/Elephant/HowdahHarnessItemTests.cs, TAOM.Tests/Features/Elephant/HowdahPrefabTests.cs, TAOM.Tests/Features/Elephant/LegacyHowdahPrefabTests.cs, TAOM.Tests/Features/Elk/ElkConfigTests.cs
- `a4b90e4d2` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(ci): v2.0.30 - review follow-ups for plan 010
  - why: Plan 010 review follow-ups: the BUTR version test compares the changeset, the reference guard reads the whole element, and one _TaomNuGetRoot property replaces repeats.
  - port notes: Take GameReferences.targets, GameReferencesTargetsTests and the tests.md and .ai/verification.md text; skip the csharp.yml hunk (CI removed on both lines) and the review records.
  - prereqs: b8c000452
  - conflicts: .github/workflows/csharp.yml, GameReferences.targets, TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs
  - issues: 421
- `2897fcca1` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(ci): v2.0.30 - convergence fixes for plan 010
  - why: Plan 010 convergence: one fixture row per rejected spelling in GameReferencesTargetsTests, and a no-game recipe that runs as written.
  - port notes: Take GameReferencesTargetsTests and the .ai/verification.md and tests.md recipe text, dropping lines that refer to csharp.yml. Review records and CHANGELOG are bookkeeping.
  - prereqs: a4b90e4d2
  - conflicts: TAOM.Tests/Infrastructure/GameReferencesTargetsTests.cs
- `c139bc50d` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(ci): v2.0.30 - apply maintainer decisions for plan 010
  - why: D44 deletes the SandBoxCore module reference because its bin holds no DLL, and D45 narrows the Patch86 test's RequiresGame tag to one method.
  - port notes: D44 holds on 1.4.8: this laptop's v1.4.8 Modules/SandBoxCore/bin/Win64_Shipping_Client is empty, so the 1.4.x wildcard Reference at Main/TAOM.csproj:52 matches nothing and can be deleted even without GameReferences.targets. D45 matters only with the TestCategory tags. D46 was measured and reverted, a no-op.
  - prereqs: b8c000452
  - conflicts: GameReferences.targets, Main/TAOM.csproj
  - issues: 421
- `2628c66c5` 2026-09-24 PORT-PARTIAL P3 pick=conflict :: fix(tests): v2.0.30 - review follow-ups for plan 010
  - why: Second plan 010 review: a tests.md sentence on class versus method tags, and the GameReferences.targets comment carrying the pending 1.4.8 SandBoxCore re-check.
  - port notes: Take the tests.md sentence; write the GameReferences.targets comment as 'the SandBoxCore bin holds no DLL on v1.4.8 either' (re-checked this run on the laptop install). Review records, lessons and CHANGELOG are bookkeeping.
  - prereqs: c139bc50d
  - conflicts: GameReferences.targets
skipped in this group: `1e9899443` SKIP-BOOKKEEPING fix(reviews): v2.0.30 - convergence fixes for plan 010; `2bc6695eb` SKIP-BOOKKEEPING docs(reviews): v2.0.30 - Codex prompt for plan 010

### group `plan-019-nullable-ratchet`  P3  track=tests-ci  commits=8  sim: clean=2 conflict=6
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): .editorconfig, Main/Features/Siege/.editorconfig, Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs, Main/Features/Siege/Models/KingdomSiegeMessages.cs, Main/Features/Siege/SiegeDefenseService.cs, Main/TAOM.csproj, TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs, TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs, TAOM.Tests/Infrastructure/NullableRatchetGateTests.cs
- `4d2a6c9ac` 2026-09-24 PORT P3 pick=clean :: chore(build): v2.0.30 - move nullable suppression into .editorconfig
  - why: Moves the seven nullable warning ids out of the NoWarn lists of Main/TAOM.csproj and TAOM.Dependencies.csproj into the root .editorconfig so a folder can raise them; build output unchanged.
  - port notes: Picks clean: both NoWarn lines (Main/TAOM.csproj:9, Dependencies/TAOM.Dependencies.csproj:9) and .editorconfig are unchanged on 1.4.x since the fork. The csproj files are single-owner during parallel work. Confirm with a 1.4.x build that the warning count is unchanged.
- `7ad7a3074` 2026-09-24 PORT P3 pick=clean :: fix(siege): v2.0.30 - null-clean the Siege folder, nullable as errors
  - why: Makes Main/Features/Siege null-clean and raises the nullable ids to errors there; Patch8_SiegeCampGuard gets an explicit no-settlement branch instead of an NRE swallowed by its catch-all, with the same defer-to-vanilla outcome.
  - port notes: Picks clean; the Siege folder is unchanged on 1.4.x since the fork (the dereference is at origin/bannerlord-1.4.5 BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs:50). Plan 019 follow-ups continue in the next batch.
  - prereqs: 4d2a6c9ac
- `19ca72d3d` 2026-09-24 PORT P3 pick=conflict :: docs(nullable): v2.0.30 - per-folder nullable ratchet procedure
  - why: Documents the per-folder nullable ratchet in docs/ai-includes/code-quality.md and the Siege prefix's new branch in siege.md.
  - port notes: Both docs are identical at fork and on 1.4.x; drop the CHANGELOG hunk.
  - prereqs: 4d2a6c9ac, 7ad7a3074
- `155e3ea57` 2026-09-24 PORT P3 pick=conflict :: fix(siege): v2.0.30 - review follow-ups for plan 019
  - why: Plan 019 review fixes: /build-fix stops advising `!` for CS8602, NullableRatchetGateTests fails if a ratchet id returns to a production NoWarn, SiegeCampGuardPatchTests covers all five prefix paths, and the no-op NoWarn line leaves Main/TAOM.csproj.
  - port notes: Conflicts are series prerequisites except docs/reference/harmony-patch-registry.md, which diverged on 1.4.x (55d779012, f8c9c8ecb, c5b84fb41, ed3f42792) and needs a hand merge. .claude/skills/build-fix/SKILL.md is identical on 1.4.x. Plan 019 continues outside this batch: de2881366, 503b933ee, bdc80a5a8, 272ac1f06 (merge c16051e7c, #660).
  - prereqs: 7ad7a3074, 19ca72d3d
  - conflicts: .editorconfig, Main/Features/Siege/.editorconfig, Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs, Main/Features/Siege/Models/KingdomSiegeMessages.cs, Main/TAOM.csproj, TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs, TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs
- `de2881366` 2026-09-24 PORT P3 pick=conflict :: fix(nullable): v2.0.30 - convergence fixes for plan 019
  - why: Makes the nullable ratchet gate parse <NoWarn> the way the compiler does (comma, space and the 'nullable' alias) and adds the no-deploy build flags to the graduation procedure; this is code-quality gate work, which is in scope.
  - port notes: Only meaningful after plan 019's base lands on 1.4.x. 155e3ea57 creates NullableRatchetGateTests.cs. 4d2a6c9ac removes the NoWarn ids still present at origin/bannerlord-1.4.5:Main/TAOM.csproj:9; without it the gate fails. The touched code and docs have no 1.4.x drift since the fork, so expect conflicts only in CHANGELOG.md and the review record. Rewrite the CHANGELOG hunk in 1.4.x style and re-subject to v2.0.28.
  - prereqs: 4d2a6c9ac, 7ad7a3074, 19ca72d3d, 155e3ea57
  - conflicts: TAOM.Tests/Infrastructure/NullableRatchetGateTests.cs
- `503b933ee` 2026-09-24 PORT P3 pick=conflict :: fix(siege): v2.0.30 - apply maintainer decisions for plan 019
  - why: The siege defense popup now fills each null or empty KingdomMessages field, or a JSON-null entry, from the defaults; 1.4.x still returns the configured entry unchanged (origin/bannerlord-1.4.5 SiegeDefenseService.cs:76-82).
  - port notes: Latent on 1.4.x: the shipped Main/_Module/ModuleData/siege/siege_defense_config.json fills all five fields in all 18 entries (checked). The popup path is wrapped in try/catch, but GrantReward (SiegeDefenseService.cs:309-340) dereferences msgs with no local guard. The textual prereq is 7ad7a3074, which makes the fields nullable and adds the doc comment this edits. If plan 019 is not ported, apply GetMessages/OrDefault and the four JsonConvert tests standalone (string? compiles under Directory.Build.props Nullable=enable). Leave the CHANGELOG hunk; the review record is optional.
  - prereqs: 7ad7a3074, 155e3ea57
  - conflicts: Main/Features/Siege/Models/KingdomSiegeMessages.cs, Main/Features/Siege/SiegeDefenseService.cs
  - issues: 660
- `bdc80a5a8` 2026-09-24 PORT P3 pick=conflict :: fix(siege): v2.0.30 - review follow-ups for plan 019 decisions
  - why: Review follow-ups to the siege fallback: a comment-only change in SiegeDefenseService.cs, two mutant-proof SiegeDefenseServiceTests, and doc and registry corrections.
  - port notes: Take the test additions, the GetMessages comment, siege-defense.md, siege.md and the harmony-patch-registry decision line (that line describes the camp-guard path from 7ad7a3074). The REVIEW-LOG, lessons, RCA and review record are optional and will conflict in shared files. Do not carry the '2,256 nullable warnings' figure as a 1.4.x fact.
  - prereqs: 503b933ee
  - conflicts: Main/Features/Siege/SiegeDefenseService.cs, TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs
  - issues: 660
- `272ac1f06` 2026-09-24 PORT P3 pick=conflict :: fix(siege): v2.0.30 - convergence fixes for plan 019
  - why: Convergence fix: the fresh-copy isolation test now catches a mutant that returns DefaultMessages for an empty id, plus wording fixes in the records; no production code.
  - port notes: Take the SiegeDefenseServiceTests hunk. The record and lesson edits only apply if plan 019's review records are ported.
  - prereqs: bdc80a5a8
  - conflicts: TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs

### group `plan-010-ci-hosted-windows`  P3  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Composition/FeatureModuleHooksTests.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs, TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBButtonsVMTests.cs, TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssignerTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentStopEndTests.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs, TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs, TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs, TAOM.Tests/Features/Warg/WargTreeNodeInjectionTests.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs, TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs
- `e9b28e2ad` 2026-09-25 PORT-PARTIAL P3 pick=conflict :: test(ci): v2.0.30 - tag the sprint's engine-bound tests RequiresGame
  - why: Tags engine-bound tests with [TestCategory("RequiresGame")] so a no-game unit run skips them; 1.4.x uses that category in 0 files, against 624 on the 1.5.x tip.
  - port notes: Only meaningful if plan 010's RequiresGame category and no-game run are ported first (base b8c000452). After that, tag per file only where the test exists on 1.4.x. Today that is only WandererAllegianceWiringTests.cs. SpecialResourcesBehaviorSessionResetTests.cs arrives with 4263535a1 and SiegeCampGuardPatchTests.cs with 7ad7a3074. The other files come with other series and should be tagged only if those land: plan 009 (45bcf80bd, 7912fdd8f), plan 014 (6fb37c9f7, fd61757bf), plan 015 (23f6f85bd, 5d9d4cc97), plan 018 (fa0b24e13), the combat commits 7577894d4 and b8b144635, MapLoadDiagnostics (e6c03411d, a 1.5.x-only feature folder) and plan 022 (SKIP).
  - prereqs: b8c000452, 4263535a1, 7ad7a3074
  - conflicts: TAOM.Tests/Composition/FeatureModuleHooksTests.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs, TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBButtonsVMTests.cs, TAOM.Tests/Features/CompanionTactics/FormationPresets/OOBCaptainAutoAssignerTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs, TAOM.Tests/Features/Enlistment/EnlistmentStopEndTests.cs, TAOM.Tests/Features/MapLoadDiagnostics/LoadingWindowDisablePatchTests.cs, TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs, TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs, TAOM.Tests/Features/Warg/WargTreeNodeInjectionTests.cs, TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs, TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs

### group `tests-reflection-site-labels`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Migration/ReflectionSiteBindingTests.cs
- `ee19f1052` 2026-10-09 PORT-PARTIAL P2 pick=conflict :: test(migration): v2.0.34 - pin reflection-site labels to their lines
  - why: Adds two game-free tests that keep ReflectionSiteBindingTests' File.cs:N labels and the reflection-sites.md catalogue in step; both files exist on 1.4.x and the drift it catches applies there.
  - port notes: Take the two tests (EveryLineLabel_PointsAtALineThatNamesItsMember, EveryDataRow_HasAMatchingRowInTheCatalogue), the exact-token matcher and the convention text. Do not copy the 1.5.x label values: run the tests against the 1.4.x DataRows and repoint what fails. They call RepoPaths.StripComments (a0db0558b) or need an inline equivalent. Rows for 1.5.x-only sites (nameplate cull, mission start guard, OOB applier) come only if those features are ported.
  - prereqs: a0db0558b
  - conflicts: TAOM.Tests/Migration/ReflectionSiteBindingTests.cs

### group `tests-engine-data-store-fake`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Features/ArmourAcquisition/ArmourAcquisitionBehaviorSessionResetTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs, TAOM.Tests/Features/EyeOfSauron/EyeOfSauronBehaviorSessionResetTests.cs, TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleBehaviorTests.cs
- `ea8f2ad52` 2026-10-14 PORT-PARTIAL P2 pick=conflict :: test(infra): v2.0.34 - one engine-faithful data store fake
  - why: Replaces drifting private IDataStore fakes with one engine-faithful EngineDataStore; v1.4.8 BehaviorSaveData.SyncData has the same Add-on-save and TryGetValue-on-load semantics (CampaignBehaviorDataStore.cs:26-38), and 1.4.x CareerPersistenceTests carries one of the drifting fakes.
  - port notes: Take TAOM.Tests/Infrastructure/EngineDataStore.cs (re-cite its v1.5.5 decompile reference to v1.4.8 CampaignBehaviorDataStore.cs:26-38) and the CareerPersistenceTests migration (the 1.4.x file differs from the parent by one line). Leave the five session-reset and WarChronicle test files (absent on 1.4.x; plan 001 is TODO there). RacePersistenceServiceTests' RoundTripDataStore stays, as EngineDataStore's summary says.
  - conflicts: TAOM.Tests/Features/ArmourAcquisition/ArmourAcquisitionBehaviorSessionResetTests.cs, TAOM.Tests/Features/Diplomacy/WarOfTheRingBehaviorSessionResetTests.cs, TAOM.Tests/Features/EyeOfSauron/EyeOfSauronBehaviorSessionResetTests.cs, TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorSessionResetTests.cs, TAOM.Tests/Features/WarChronicle/WarChronicleBehaviorTests.cs
  - issues: 792

### group `issue-938-patch-number-gate`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
- `6ddcc8f9b` 2026-10-19 PORT-PARTIAL P2 pick=conflict :: test(patches): v2.0.35 - one patch number names one feature
  - why: Test gate that each Harmony patch number names one feature and has a registry section, a check 1.4.x lacks.
  - port notes: Adapt TAOM.Tests/Composition/PatchCategoryNumberTests.cs: scan typeof(SubModule).Assembly instead of TAOM.Composition.FeatureModules (1.5.x-only), replace the 1.5.x-only RepoPaths helper (GameAssemblies exists on 1.4.x), lower the 100-category and 100-heading floors (the 1.4.x registry has 95 '## ' headings), and rebuild the exception tables against 1.4.x (keep Patch63 BannerBearers/BlowDiagnostics; the Patch89/98/101 entries are 1.5.x-only). Take the .claude/rules/harmony-patches.md line and the lessons.
  - issues: 938

### group `tests-viewmodel-mixin-binding-gate`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/TaomSettings.cs, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
- `e7cdb2e96` 2026-10-20 PORT-PARTIAL P2 pick=conflict :: feat(settlements): v2.0.35 - Reeve's Ledger in the town window
  - why: Reeve's Ledger is a new feature, but the commit also adds TAOM.Tests/Migration/ViewModelMixinBindingTests, a generic gate for UIExtenderEx [ViewModelMixin] refresh names, which otherwise throw at UIExtender.Register on startup (1.4.x pins UIExtenderEx 2.13.2 at Main/TAOM.csproj:111).
  - port notes: Take only: TAOM.Tests/Migration/ViewModelMixinBindingTests.cs with the checkedCount floor lowered from 6 to 5 (1.4.x has five named-refresh mixins: CharacterDeveloperCareerMixin, MissionAgentStatusCareerMixin, MessengerEncyclopediaMixin, SpecialResourceMapBarMixin, TimeAccelerationMixin; GameAssemblies exists on 1.4.x); the one-line .claude/skills/engine-bump/SKILL.md change; the two docs/reference/taleworlds-api-snapshot/README.md rows; optionally the harmony-il.md lesson on OnFinalize hook stacking, after re-checking it on UIExtenderEx 2.13.2 (the lesson cites 2.13.3) and the 1.4.x targets (TownManagementReserveControlVM is ledger-only). Leave all of ReevesLedger, its adapters, strings, settings, the Main/Composition line, co-op rows and feature docs.
  - conflicts: Main/Composition/FeatureModules.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/TaomSettings.cs, TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs
  - issues: 857, 931

### group `issue-1020-quality-gates`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): tools/blender/spider_cfg.py, tools/test_hooks.sh
- `affcee2ea` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: feat(tools): v2.0.35 - five quality gates for Python, C#, shell, NuGet
  - why: Five repo gates (Ruff bug-class lint, a banned-constructs C# gate, a Claude config lint, NuGet audit, ShellCheck) apply to 1.4.x, and the C# gate would pass as-is because origin/bannerlord-1.4.5 has no #region, [Obsolete] or #if outside Main/IoC.cs.
  - port notes: Take ruff.toml with test_python_lint_gate.py (and the missing import json fix in tools/oneoff/generate_rivendell_materials.py, which exists on 1.4.x); BannedConstructsGateTests; test_claude_config_lint.py, with its skill-key allowlist derived from the 1.4.x harness-facts row; the Directory.Build.props NuGet audit, after checking that the 1.4.x restore passes; test_shell_lint_gate.py, after rerunning ShellCheck over the 1.4.x scripts; and the /verify and /security-scan steps. Regenerate .gitleaksignore from a scan of 1.4.x history instead of copying fingerprints. Skip deep-review lenses/4-completeness.md and tools/context_budget_scan.sh (absent on 1.4.x). Tool installs are per machine.
  - conflicts: tools/blender/spider_cfg.py, tools/test_hooks.sh
  - issues: 1020

### group `plan-010-refasm-nogame-gate`  P3  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): GameReferences.targets, TAOM.Tests/Adapters/HeroCombatAdapterTests.cs, TAOM.Tests/Features/CombatMechanics/CustomBattleDamageModelTests.cs, TAOM.Tests/Features/CultureDoctrine/TeamCombatantSelectorTests.cs, TAOM.Tests/Features/Encyclopedia/EncyclopediaHeroFilterAdapterTests.cs, TAOM.Tests/Features/Encyclopedia/EncyclopediaHeroFilterPatchTests.cs, TAOM.Tests/Features/ManyHammers/ManyHammersWiringTests.cs, TAOM.Tests/Migration/GameAssemblies.cs, TAOM.Tests/Migration/GameAssembliesResolutionTests.cs, TAOM.Tests/TAOM.Tests.csproj
- `8589a3384` 2026-10-28 UNSURE P3 pick=conflict :: fix(tests): v2.0.36 - no-game binding gate runs with no variable
  - why: Fixes the RefAsm no-game binding gate from plan 010, infrastructure absent on 1.4.x (no GameReferences.targets and no TaomGameRefs in any 1.4.x project file).
  - port notes: Decided by whether plan 010's RefAsm build mode is ported (b8c000452 adds GameReferences.targets, follow-ups a4b90e4d2, c139bc50d and 2628c66c5; 8c89e042f adds GameAssembliesResolutionTests). Its CI workflows were removed on 1.5.x in c8d69a0dc, so on 1.4.x it would only serve a machine without the game. If ported: take the GameReferences.targets stamp, TAOM.Tests/Migration/GameAssemblies.cs, GameAssembliesResolutionTests.cs, TAOM.Tests.csproj, the RequiresGame and RequiresGameIL tags for tests that exist on 1.4.x, .ai/verification.md and .claude/rules/tests.md; leave the CultureDoctrine and ManyHammers tags (1.5.x-only features), the v1.5.6 docs and REVIEW-LOG; BannerlordRefAsmVersion would need a BUTR v1.4.8 build (availability UNVERIFIED).
  - prereqs: b8c000452, 8c89e042f
  - conflicts: GameReferences.targets, TAOM.Tests/Adapters/HeroCombatAdapterTests.cs, TAOM.Tests/Features/CombatMechanics/CustomBattleDamageModelTests.cs, TAOM.Tests/Features/CultureDoctrine/TeamCombatantSelectorTests.cs, TAOM.Tests/Features/Encyclopedia/EncyclopediaHeroFilterAdapterTests.cs, TAOM.Tests/Features/Encyclopedia/EncyclopediaHeroFilterPatchTests.cs, TAOM.Tests/Features/ManyHammers/ManyHammersWiringTests.cs, TAOM.Tests/Migration/GameAssemblies.cs, TAOM.Tests/Migration/GameAssembliesResolutionTests.cs, TAOM.Tests/TAOM.Tests.csproj
  - issues: 1038
skipped in this group: `7bac0c9d4` SKIP-SUPERSEDED docs: v2.0.36 - no-game gate step and v1.5.6 gate record; `0740c632e` SKIP-1.5-ONLY-FIX docs(plans): v2.0.36 - plan 044, the last 7 no-game test failures

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 4, batch 6, batch 7, batch 15, batch 16, batch 20, batch 21, batch 28, batch 29
