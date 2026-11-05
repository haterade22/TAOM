# Input pack: WP 140 gui-prefab-gates

Title (draft): Gauntlet prefab gates: copy drift against v1.4.8 vanilla, insert positions, stretch-in-cover, bindings
Wave: 5   Lane: E   Draft priority: P2   Groups: 7   Port commits: 14
Depends on (WP ids): 131

## Orchestrator notes for this WP (binding)
- K2: port PartyTupleVanillaParityTests.EachTupleCopy_MatchesVanilla_ApartFromTaomsTwoEdits (540848795); STOP if red on a clean tip.
- 62fb6bb1d needs PrefabExtensionBrushTests from 2c7f7fae9 (WP131); gui-prefab-copy-drift needs 450332b74; 8d5cfc623's test part only (prefab clone widget references).
- K1: port 42148af4c's PrefabHintParentGateTests.cs (fails any in-code PrefabExtension HintWidget whose parent sets DoNotAcceptEvents=true; shipped three times incl. Messengers #898). Scan the TAOM assembly (not TAOM.Client) and lower the floor from 9 to 5 (CareerButtonPrefab, CareerEnergyBarPrefab, MessengerEncyclopediaPrefabExtension, SpecialResourcePrefab, TimeAccelerationPrefab). STOP if fewer than 5 are found.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/K2-skip-other-critic.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- XML-DATA-08 [P3/high] The Gauntlet prefab insert-position gate (69b47d00b) ports with a one-assembly list; current 1.4.x inserts already pass | port: 69b47d00b,53a4e4e52  (report: plans/_audit/2026-11-04-port148/deepdive/xml-data.md)

## Port groups owned by this WP (from the commit classification)

### group `prefab-binding-gates`  P2  track=tests-ci  commits=3  sim: clean=0 conflict=3
symptoms: crash
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/GUI/PreFabs/Nameplate/PartyNameplateItem.xml, Main/_Module/SubModule.xml, tools/check_handbook_attributes.py, tools/handbook_attribute_manifest.json, tools/tests/test_check_handbook_attributes.py
- `720e421fa` 2026-09-14 PORT P3 pick=conflict :: fix(career): v2.0.28 - drop the two HUD leftovers the import brought back
  - why: Deletes two dead CareerSystem prefab leftovers that also ship on 1.4.x: AgentStatus.xml is v1.4.8 vanilla plus a hidden block naming a CareerEnergyBarWidget class that exists nowhere, and AbilityHUD.xml is an unreferenced empty override.
  - port notes: Verified: origin/bannerlord-1.4.5 Main/_Module/GUI/PreFabs/Mission/AgentStatus.xml differs from the installed v1.4.8 Native/GUI/Prefabs/Mission/AgentStatus.xml only by its lines 109-189 (the dead block); no C# CareerEnergyBarWidget exists on 1.4.x; CareerEnergyBarPrefab's XPath descendant::Widget[@VisualDefinition='AgentStatus']/Children resolves in vanilla. Conflicts only in CHANGELOG. Prerequisite for the element-name gate taken from 71484b8e4.
  - issues: 382
- `71484b8e4` 2026-09-14 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.28 - prefab clones re-based on v1.5.2, element-name gate
  - why: The prefab re-base follows v1.5.2 widget renames, but PrefabElementTypeBindingTests and PrefabExtensionBindingTests are engine-agnostic gates that check TAOM prefabs against whichever engine is installed.
  - port notes: Take TAOM.Tests/Migration/PrefabElementTypeBindingTests.cs and PrefabExtensionBindingTests.cs (GameAssemblies helper exists on 1.4.x). Leave the PartyNameplateItem/PartyPlayerNameplateItem/PartyScreen edits: v1.4.8 still names BoolStateChangerWidget (BoolStateChangerWidget.cs in _categories_v1.4.8) and NameBloodFeudContainer has no hit in the v1.4.8 dump. Needs 720e421fa first, or the element gate is red on the <CareerEnergyBarWidget> leftover.
  - prereqs: 720e421fa
- `8d5cfc623` 2026-09-15 PORT-PARTIAL P2 pick=conflict :: fix(engine): v2.0.29 - Bannerlord v1.5.3 and the nameplate clone break
  - why: The v1.5.3 bump and nameplate re-base are 1.5-only, but PrefabCloneWidgetReferenceTests (a clone must keep every Widget-typed reference its vanilla prefab declares) is engine-agnostic, and its troop-manager popup splice fixes the 1.4.x clone too.
  - port notes: Take TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs; the PartyTroopManagerPopUp.xml splice (IsTertiaryActionAvailable, the TertiaryInputKeyVisualParent attribute and widget): installed v1.4.8 SandBox/GUI/Prefabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml declares it (lines 14, 209), the origin/bannerlord-1.4.5 clone does not, and v1.4.8 PartyManageTroopPopupWidget returns early when it is null (dump line 81), so the popup's key hints never show; the engine-bump skill line naming the clone gate; the lessons/localization-ui.md entry. Leave the nameplate re-base (ShipBannerContainerWidget has no hit in the v1.4.8 dump), check_handbook_attributes.py v1.5.3 call shapes and manifest, pins and v1.5.3 docs. The gate may surface other 1.4.x clones drifted against v1.4.8.
  - conflicts: Main/_Module/GUI/PreFabs/Nameplate/PartyNameplateItem.xml, Main/_Module/SubModule.xml, tools/check_handbook_attributes.py, tools/handbook_attribute_manifest.json, tools/tests/test_check_handbook_attributes.py

### group `prefab-binding-gate`  P3  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Migration/PrefabExtensionBindingTests.cs
- `f48ef74dc` 2026-10-19 PORT-PARTIAL P3 pick=conflict :: feat(ui): v2.0.35 - Shadowfax Screens: screens open without slides
  - why: ShadowfaxScreens is a new UI feature, but the commit also makes the prefab binding gate check the movie name of whole-document patches with exact case, since an unknown movie is a silent no-op in UIExtenderEx.
  - port notes: Take only the TAOM.Tests/Migration/PrefabExtensionBindingTests.cs hunk, and only once the gate itself (created on 1.5.x by 71484b8e4) is ported; 1.4.x has no PrefabExtensionBindingTests. Leave the feature, its prefab edits and docs.
  - prereqs: 71484b8e4
  - conflicts: TAOM.Tests/Migration/PrefabExtensionBindingTests.cs
  - issues: 850

### group `issue-858-prefab-insert-gate`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Features/Namegiving/UI/FiefNamingMixin.cs, Client/Features/ReevesLedger/UI/ReserveStepMixin.cs, Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs, Main/Features/Namegiving/UI/NamegivingPrompts.cs, TAOM.Tests/Core/Logging/ScreenFaultLogTests.cs, TAOM.Tests/Features/Namegiving/NamegivingWiringTests.cs, TAOM.Tests/Features/ReevesLedger/ReevesLedgerWiringTests.cs, TAOM.Tests/Infrastructure/ServerJitBaseline.txt, TAOM.Tests/Infrastructure/ServerJitGateTests.cs
- `69b47d00b` 2026-10-24 PORT-PARTIAL P2 pick=conflict :: test(ui): v2.0.35 - gate every prefab insert on where it lands
  - why: New PrefabInsertPositionTests gate catches UIExtenderEx insert patches that land somewhere other than intended; of the prefab patches in five 1.4.x Main files only CareerEnergyBarPrefab (Child insert, no Index) would trip it, harmlessly since Index defaults to 0.
  - port notes: Take PrefabInsertPositionTests adapted to scan only the TAOM (Main) assembly (1.5.x scans TaomAssemblies.All including TAOM.Client), the 'Index => 0' line in Main/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs, the gui-ui.md rule lines, the .ai/review-reference.md line and the localization-ui.md lesson edit. Leave ScreenFaultLog/LedgerFaultLog and the Namegiving/ReevesLedger edits (1.5.x-only features), ServerJitBaseline/ServerJitGateTests (ADR-013, 1.5.x-only) and the deep-review lenses/1-standards.md line (1.4.x deep-review has only SKILL.md, no lenses dir).
  - conflicts: Client/Features/Namegiving/UI/FiefNamingMixin.cs, Client/Features/ReevesLedger/UI/ReserveStepMixin.cs, Client/Features/ReevesLedger/UI/TownManagementLedgerMixin.cs, Main/Features/Namegiving/UI/NamegivingPrompts.cs, TAOM.Tests/Core/Logging/ScreenFaultLogTests.cs, TAOM.Tests/Features/Namegiving/NamegivingWiringTests.cs, TAOM.Tests/Features/ReevesLedger/ReevesLedgerWiringTests.cs, TAOM.Tests/Infrastructure/ServerJitBaseline.txt, TAOM.Tests/Infrastructure/ServerJitGateTests.cs
  - issues: 858

### group `ui-prefab-validation-gates`  P2  track=data-integrity  commits=4  sim: clean=0 conflict=4
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/GUI/PreFabs/BattleActionBar.xml, Main/_Module/GUI/PreFabs/CustomBattle/CustomBattleScreen.xml, Main/_Module/GUI/PreFabs/FacGen/PreBuildCharacterSelection.xml, TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs, TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs, tools/package_release.py, tools/tests/test_package_release.py
- `62fb6bb1d` 2026-10-26 PORT P2 pick=conflict :: fix(ui): v2.0.35 - nine screen widgets draw with their real brushes
  - why: The undefined brush names in shared TAOM screens (BattleActionBar, CareerScreen, ArmyComposition, PresetsOverlay, EventXml) are present on 1.4.x and undefined in the v1.4.8 Native/SandBox/SandBoxCore brushes, while every replacement exists for v1.4.8 (vanilla Tooltip.xml, Popup.xml, SandBoxCore CustomBattle.xml, or 1.4.x's own CareerSystem.xml).
  - port notes: Take the prefab edits, the EventXml.xml deletion and the brush-gate test changes. The test file was created on 1.5.x by 2c7f7fae9 (Messengers, a shared feature), so port that test first or create it here. The gate reads brushes from loaded modules: run it on both the desktop (loose Armory/TAOM_Map) and the laptop (cooked packages) before trusting a result.
  - prereqs: 2c7f7fae9
  - conflicts: Main/_Module/GUI/PreFabs/BattleActionBar.xml, TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs
  - issues: 1005
- `103636439` 2026-10-26 PORT P2 pick=conflict :: fix(ui): v2.0.35 - unused weapon preview prefab removed
  - why: PreviewPanel.xml is present and unused on 1.4.x too, and the new check that every Brush.<Property> attribute names a real brush property guards against typos the game skips silently.
  - port notes: Take the deletion and the test cases added to PrefabExtensionBrushTests, after 62fb6bb1d. The commit's removal of installed copies from the module folder is a deploy step, not a repo change.
  - prereqs: 62fb6bb1d
  - conflicts: TAOM.Tests/Infrastructure/PrefabExtensionBrushTests.cs
  - issues: 1022
- `450332b74` 2026-10-26 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.35 - screens drop settings the game never applied
  - why: The misspelled and inapplicable prefab attributes (VertialAlignment appears twice in 1.4.x TroopTypeSelectionPopUp.xml) and the four unloaded screens are all on 1.4.x, and the attribute-name gate reflects over the installed engine, so it adapts to v1.4.8.
  - port notes: Take the prefab edits, the deletions of mainmenuHUD, PoppyHUD, ROTsprite and MomentumView/KingdomIcon, and PrefabAttributeNameBindingTests, recomputing its VanillaParityAllowlist from the v1.4.8 vanilla prefabs. Its EaseIn rule holds on v1.4.8 (WidgetPrefab.cs:42-50 reads only EaseType and EaseFunction). The package_release.py refusal of untracked module files must be reimplemented, because the 1.4.x packager and its tests are about 2,660 lines smaller. Skip docs/migration/v1.5.2-impact.md.
  - conflicts: tools/package_release.py, tools/tests/test_package_release.py
  - issues: 1024
- `abb718c2f` 2026-10-26 PORT-PARTIAL P3 pick=conflict :: feat(ui): v2.0.35 - custom battle panels ease in like vanilla's
  - why: The ease checks added to the prefab gate hold on v1.4.8 (WidgetPrefab.cs:42-50 parses EaseType and EaseFunction with Enum.TryParse), but easing 'like vanilla' has no v1.4.8 referent, because v1.4.8 SandBoxCore CustomBattleScreen.xml carries the same dead EaseIn="true" as TAOM's copy.
  - port notes: Take the PrefabAttributeNameBindingTests ease-value cases, after 450332b74. Drop PanelEaseTests: it compares TAOM's screen to the installed vanilla one and on 1.4.8 would demand EaseIn. Adding EaseOut/Quint to TAOM's three panel definitions is optional polish beyond v1.4.8 vanilla. The removal of an unused PreBuildCharacterSelection.xml animation can go with it.
  - prereqs: 450332b74
  - conflicts: Main/_Module/GUI/PreFabs/CustomBattle/CustomBattleScreen.xml, Main/_Module/GUI/PreFabs/FacGen/PreBuildCharacterSelection.xml, TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs
  - issues: 1027

### group `gui-prefab-copy-drift`  P2  track=data-integrity  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/_Module/GUI/PreFabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml, TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs, TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs
- `fc932d4ca` 2026-10-28 PORT-PARTIAL P3 pick=conflict :: fix(gui): v2.0.36 - drop eight screen files copied from the base game
  - why: Deletes eight TAOM prefab copies that only shadow identical vanilla files and adds a test against such copies; on 1.4.x all eight are whitespace-identical to the v1.4.8 vanilla files, so deleting them keeps behaviour the same.
  - port notes: Delete the eight files. On 1.4.x each is whitespace-identical (diff -w, CR stripped) to the installed v1.4.8 file it shadows: CompositionSlider.xml and SimpleDropdown.xml vs SandBoxCore, the other six vs SandBox. Nothing in Main, TAOM.Tests, tools or .claude references them by path. On 1.4.x, PartyTroopTuple.xml and PartyTroopTupleLeft.xml are identical to v1.4.8 SandBox as well (on 1.5.x they carry TAOM edits), so the new identical-copy test would flag them: delete them with the eight. The test extends TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs, which 1.4.x lacks. 1.5.x created that file in 8d5cfc623 (the v1.5.3 bump), so port its harness (ShadowPairs over VanillaPrefabsByBasename plus the widget-reference check) together with this test. Take the wording changes in .claude/rules/vanilla-data-comparison.md and the engine-bump SKILL.md; skip docs/migration/v1.5.2-impact.md. Release note: players must remove the old Modules\TAOM folder before installing, or the old copies stay. Low urgency: the shadowing hazard can only fire if TaleWorlds ships another 1.4.x engine patch.
  - prereqs: 8d5cfc623
  - conflicts: TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs
  - issues: 1051
- `7012783b3` 2026-10-29 PORT-PARTIAL P2 pick=conflict :: fix(ui): v2.0.36 - the troop upgrade pop-up shows used horses again
  - why: TAOM's 1.4.x PartyTroopManagerPopUp.xml is stale too: v1.4.8 vanilla's HorseLabelContainer binds UsedHorsesHint with hover commands and also has a Cancel button and a TertiaryInputKeyVisualParent, all missing from the 1.4.x copy.
  - port notes: Re-base TAOM's copy on the installed v1.4.8 SandBox/GUI/Prefabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml, not on the v1.5.6 file the commit used. Keep TAOM's one deliberate edit: a single centred Done button (no PopupBackButton) plus the top-right close ButtonWidget that calls ExecuteCancel. The missing TertiaryInputKeyVisualParent currently makes v1.4.8 PartyManageTroopPopupWidget.OnLateUpdate return at line 81, so gamepad key hints never show; there is no crash. The prereqs apply to the test part only: the test extensions need TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs (created in 8d5cfc623) and PrefabAttributeNameBindingTests.cs (created in 450332b74), both absent on 1.4.x, so port their harnesses or re-author the binding-drop check. Take the .claude/rules/moduledata-validation.md line; the RCA records 1.5.x findings only.
  - prereqs: 8d5cfc623, 450332b74
  - conflicts: Main/_Module/GUI/PreFabs/Party/PartyTroopManagerPopUp/PartyTroopManagerPopUp.xml, TAOM.Tests/Migration/PrefabAttributeNameBindingTests.cs, TAOM.Tests/Migration/PrefabCloneWidgetReferenceTests.cs
  - issues: 1051
skipped in this group: `540848795` SKIP-1.5-ENGINE fix(party-screen): v2.0.36 - the Execute button shows the feud mark

### group `tests-gauntlet-stretch-cover-ratchet`  P2  track=tests-ci  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Client/Features/FordsOfWar/FordsCameraProbe.cs, Client/Features/FordsOfWar/FordsCommanderCameraView.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.Client.cs, Main/Features/FordsOfWar/FordsMissionDirector.cs, Main/Features/FordsOfWar/FordsMissionExit.cs, Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs, Main/_Module/GUI/PreFabs/FordsOfWar/FordsCommand.xml, TAOM.Tests/Features/FordsOfWar/Client/FordsCameraProbeTests.cs, TAOM.Tests/Features/FordsOfWar/Diagnostics/FordsRunReportTests.cs, TAOM.Tests/Features/FordsOfWar/FordsOfWarMissionLogicTests.cs, TAOM.Tests/Features/FordsOfWar/Scenarios/FordsScenario.cs
- `e6dbea3e0` 2026-10-31 PORT-PARTIAL P2 pick=conflict :: fix(fords-of-war): v2.0.36 - start on the first tick, unhide the scene
  - why: Mostly a Fords of War start and layout fix, but it also adds GauntletStretchInCoverRatchetTests, a gate over every TAOM prefab for a layout bug that shipped three times, and an engine note against spawning agents inside AfterStart.
  - port notes: Take only: (1) TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs plus its later path-case fix 53a4e4e52, regenerating Known on 1.4.x (StagedSiegeHud.xml is absent there; MomentumView, CareerScreen, BattleActionBar and PreBuildCharacterSelection prefabs exist) and replacing RepoPaths (absent on 1.4.x) with a local helper; (2) the 'Never spawn an agent inside AfterStart' paragraph of docs/reference/engine/mission-and-missionbehavior-lifecycle.md, re-cited to the v1.4.8 dump (Mission.cs:3817-3826 walks MissionBehaviors with foreach for EarlyStart and AfterStart; AddMissionBehavior appends at Mission.cs:4615-4617); the 1.4.x doc lacks the preceding guard paragraph, so place it by hand; (3) the localization-ui.md lesson line, with the 1.5.x-only RCA link removed. Leave every Fords of War file.
  - conflicts: Client/Features/FordsOfWar/FordsCameraProbe.cs, Client/Features/FordsOfWar/FordsCommanderCameraView.cs, Main/Features/FordsOfWar/Diagnostics/FordsRunReport.Client.cs, Main/Features/FordsOfWar/FordsMissionDirector.cs, Main/Features/FordsOfWar/FordsMissionExit.cs, Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs, Main/_Module/GUI/PreFabs/FordsOfWar/FordsCommand.xml, TAOM.Tests/Features/FordsOfWar/Client/FordsCameraProbeTests.cs, TAOM.Tests/Features/FordsOfWar/Diagnostics/FordsRunReportTests.cs, TAOM.Tests/Features/FordsOfWar/FordsOfWarMissionLogicTests.cs, TAOM.Tests/Features/FordsOfWar/Scenarios/FordsScenario.cs
- `d4fb51a50` 2026-10-31 PORT-PARTIAL P3 pick=conflict :: chore(rules): v2.0.36 - name the Gauntlet stretch-in-cover gate
  - why: Names GauntletStretchInCoverRatchetTests in the moduledata-validation rule's GUI prefabs gate row.
  - port notes: The 1.4.x moduledata-validation.md has no GUI prefabs row (PrefabExtensionBrushTests and PrefabElementTypeBindingTests do not exist on 1.4.x), so add a short line naming the gate rather than editing a row. Only together with the test part of e6dbea3e0.
  - prereqs: e6dbea3e0
  - issues: 388, 866

### group `tests-gauntlet-stretch-gate`  P2  track=tests-ci  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs
- `53a4e4e52` 2026-11-03 PORT-PARTIAL P2 pick=conflict :: fix(tests): v2.0.36 - the prefab gate ignores path case
  - why: Fixes path-case lookups in the Gauntlet stretch-in-CoverChildren ratchet, a generic gate over every XML under Main/_Module/GUI that 1.4.x lacks because it was born inside the Fords fix e6dbea3e0.
  - port notes: Port TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs in its fixed form (StringComparer.OrdinalIgnoreCase on Known and seen) and take nothing else from e6dbea3e0. Drop the StagedSiegeHud.xml entry: a Python replica of Offenders, which reproduces 1.5.x's five-entry baseline exactly at 53a4e4e52, finds 4 offending files on origin/bannerlord-1.4.5 (MomentumView 5, CareerScreen 3, BattleActionBar 1, FacGen/PreBuildCharacterSelection 1), with all 65 GUI XML parsing, so the scanned > 50 assert holds. RepoPaths is 1.5.x-only (22 lines from 35007dbb8): take it or use a local FindRepoRoot() as 1.4.x tests do (TAOM.Tests/Core/CharacterFaceCoverageTests.cs:35). Also take the build-tooling-workflow.md lesson and docs/reviews/rca-gauntlet-ratchet-case-2026-11-03.md. The tests.md hunk edits a gate-state paragraph that 1.4.x's tests.md lacks, so add the path-case sentence fresh.
  - prereqs: e6dbea3e0
  - conflicts: TAOM.Tests/Infrastructure/GauntletStretchInCoverRatchetTests.cs
  - issues: 1078

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 1, batch 19, batch 25, batch 28, batch 30, batch 32, batch 33
