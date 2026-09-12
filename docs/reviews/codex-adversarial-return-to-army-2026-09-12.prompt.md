ADVERSARIAL REVIEW: Patch87_ReturnToArmy (#566), uncommitted working tree in E:\repos\TAOM

You are the reviewer. Read AGENTS.md and .ai/roles/reviewer.md first. Report defects with source locations, impact and a reproduction or proving code; try to refute each claim before reporting it. Do not fix anything. Do not stage, stash, reset or commit. The tree also holds another person's uncommitted ModuleData edits (troops_gondor.xml and equipment sets); ignore them, they are not in scope.

FEATURE
One Harmony prefix on vanilla PlayerTownVisitCampaignBehavior.game_menu_return_to_army_on_consequence(MenuCallbackArgs). Vanilla's "Return to Army" switches to the army_wait_at_settlement wait menu and leaves the settlement only for a village; "Leave" is hidden for every army member who is not the leader. For a member who is in an army but NOT merged into it (MobileParty.MainParty.Army != null, AttachedTo == null) that is a dead end in a town or castle: the wait menu's first tick re-routes through DefaultEncounterGameMenuModel.GetGenericStateMenu() to town_wait_menus (foreign faction) or stays on army_wait_at_settlement forever (own faction). The prefix reads Army, AttachedTo and CurrentSettlement.IsVillage, calls the pure ReturnToArmyRules.Decide, and for the unattached town/castle row replicates vanilla's own game_menu_settlement_leave_on_consequence (Position = GatePosition; PlayerEncounter.LeaveSettlement(); PlayerEncounter.Finish(); SetMoveModeHold(); Campaign.Current.SaveHandler.SignalAutoSave()) and returns false. Every other row returns true. Nothing writes Army. The reporting user declined auto-marching to the leader.

TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur". No config IDs are involved in this change; the cheatsheet is here in case you trace into culture data.

READ FIRST
- docs/features/return-to-army.md (the feature doc, including the decision table and the error-path reasoning)
- docs/reviews/rca-return-to-army-2026-09-12.md (the in-house review's findings; two LOW items already fixed)
- docs/reference/harmony-patch-registry.md section "Patch87_ReturnToArmy"
- docs/features/enlistment.md, the parts about MobileParty.Army being null outside a battle (#443) and the menu redirect list
- docs/features/player-switcher.md section "the lord's army membership survives the takeover"

FILES IN SCOPE
C#:
- Main/Features/ReturnToArmy/ReturnToArmyRules.cs (pure decision)
- Main/Features/ReturnToArmy/Hooks/Patch87_ReturnToArmy.cs (the prefix; Initialize and ResetForUnload)
- Main/SubModule.cs, the Patch87 lines near the Patch84 registration and the ResetForUnload block
Tests:
- TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs (12 tests: decision matrix + binding drift guards + IL call presence)
- TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs, the Patch87_ReturnToArmy Registry entry
Docs: docs/features/return-to-army.md, CHANGELOG.md 2026-09-12 fix(army) entry, docs/reviews/lessons/campaign-mechanics.md last lesson, docs/reviews/lessons/harmony-il.md last lesson

VANILLA CODE (REQUIRED: decompile from the INSTALLED v1.4.8 DLLs and paste the relevant bodies as code blocks; the dump under E:\Decompiled_Bannerlord\_categories_v1.4.8 matches the installed version but the installed DLL is authoritative)
- TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior: game_menu_return_to_army_on_condition, game_menu_return_to_army_on_consequence, game_menu_town_town_leave_on_condition, game_menu_settlement_leave_on_consequence, and the three AddGameMenuOption registrations for town_return_to_army, castle_return_to_army, village_return_to_army
- TaleWorlds.CampaignSystem.Encounters.PlayerEncounter: Finish(bool), LeaveSettlement(), EnterSettlement(), and the Init branch that enters a settlement immediately
- TaleWorlds.CampaignSystem.Actions.LeaveSettlementAction.ApplyForParty and EnterSettlementAction.ApplyForParty
- TaleWorlds.CampaignSystem.GameComponents.DefaultEncounterGameMenuModel.GetGenericStateMenu
- TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerArmyWaitBehavior: wait_menu_army_wait_at_settlement_on_init, wait_menu_army_wait_at_settlement_on_tick, wait_menu_army_enter_settlement_on_condition and _on_consequence
- TaleWorlds.CampaignSystem.Army: Tick (the merge loop), AddPartyToMergedParties, OnRemovePartyInternal
- TaleWorlds.CampaignSystem.GameMenus.GameMenu: ExitToLast, SwitchToMenu, ActivateGameMenu (what each does to Campaign.TimeControlMode)
- SandBox.dll is NOT in the dump. Decompile the installed Modules/SandBox/bin/Win64_Shipping_Client/SandBox.dll for the MapScreen tick that activates GetGenericStateMenu's result when no menu is open, and for any reflection or by-name reach into game_menu_return_to_army_on_consequence.

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with evidence)
S1. Post-leave re-entry. After the prefix's Finish(), the player is on the map with Army != null, AttachedTo == null, CurrentSettlement == null. The in-house review traced GetGenericStateMenu to a null return, so no menu re-opens. Suspect: some other engine path (MapScreen tick, Army.Tick merge because ShortTermTargetParty could still equal the leader after SetMoveModeHold, army overlay refresh, or a save-load with MapStateData.GameMenuId still set) re-opens army_wait or army_wait_at_settlement immediately or on the next load. Trace it.
S2. The attachedToArmy row. Decide returns RunVanilla whenever AttachedTo != null, on the premise that the army is physically in this settlement. Suspect: a merged member whose leader has already left while the player was inside a town submenu (LeaveSettlementAction.ApplyForParty calls PlayerEncounter.Finish() for the main party, and Finish's own LeaveSettlement is gated on AttachedTo == null). Is that state reachable, what does vanilla show, and should the prefix act on it instead of deferring?
S3. Error-path boundary. The prefix defers to vanilla on a throw up to and including main.Position = settlement.GatePosition, and skips vanilla on a throw from PlayerEncounter.LeaveSettlement() onward, on the claim that LeaveSettlementAction nulls CurrentSettlement which vanilla's own body dereferences. Confirm the null, confirm that Finish() with the menu still up cannot itself throw on this path in a way the second catch mishandles, and confirm no double-leave (Finish's internal LeaveSettlement guard).
S4. Time control. PlayerEncounter.Finish forces TimeControlMode = Stop only when Army == null or LeaderParty == EncounteredMobileParty, which is false here. The doc claims GameMenu.ExitToLast writes Stop unconditionally so the map is paused anyway. Confirm, and check whether anything after the consequence returns (MapState.ExitMenuMode, the MapScreen handler, TAOM's TimeAccelerationService) restores a running mode.
S5. Castle path. castle_return_to_army shares the consequence. Suspect a castle-only precondition the leave sequence violates: LocationEncounter type, GatePosition semantics, castle wait mesh, or the "leave" option id differing. Trace the castle menu.
S6. Other TAOM writers of the same state. Enlistment (ArmyMembershipAdapter.JoinCommanderArmy/LeaveArmy, MobilePartyAttachmentAdapter.ClearArmyAttachment), FieldCamp and Refuge menu controllers (committed today as 4d2ea82b), and Player Switcher. Can any of them leave Army != null with AttachedTo == null while the player is inside a settlement menu, and does the prefix then do the right thing or the wrong thing for that feature? Enlistment claims Army is null outside a battle (#443); verify that claim in code, not in the doc.
S7. Co-op. The CoopVetoClassificationTests entry says the only skipped vanilla statement is GameMenu.SwitchToMenu and the condition reads replicated engine state. Dispute if MobileParty.AttachedTo or Army can differ between a co-op host and client under BannerlordCoop or BannerlordTogether (read docs/features/coop-interop.md for how TAOM reasons about replicated state).
S8. Binding tests. The prefix declares no parameters. The tests pin the target by name with one MenuCallbackArgs parameter and one overload, plus IL call presence. Suspect a drift the tests cannot see: a vanilla refactor that keeps the method and its calls but changes WHICH branch leaves (for example leaving for castles too), which would make the prefix double-leave. Say whether a test could pin that and how.

REQUIRED SECTIONS IN YOUR OUTPUT
1. VANILLA CODE: the decompiled bodies listed above as code blocks, with the installed DLL path you read.
2. KNOWN SUSPECTS: S1 to S8, each CONFIRMED or DISPUTED with the evidence.
3. SCENARIO WALKTHROUGH: the reporting scenario (Player Switcher takeover of a Gondor lord marching to join an army, walks into Orthanc = town_isengard, foreign faction, clicks Return to Army) statement by statement through the prefix and through vanilla; then the same-faction town; then a castle; then a village; then an attached member. State the final menu, CurrentSettlement, AttachedTo, Army and TimeControlMode for each.
4. TESTS: does the 16-combination matrix plus the binding guards cover the decision, and what is NOT pinned that should be.
5. FINDINGS OR OBSERVATIONS: numbered, each with severity P1/P2/P3, file:line, impact, and the reproduction or proving code. Mark UNVERIFIED anything you could not prove from the installed DLLs. Mark explicitly if you find nothing.

QUALITY GATES
- Every claim about engine behaviour cites the installed decompile with a line number.
- Do not flag code that matches vanilla's own Leave as a bug.
- Do not report style preferences as defects; the project accepts the Patch84 shape (static pure decision class, prefix with Initialize/ResetForUnload, logger passed in).
- Refute your own findings before reporting them.

PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Review 95 on this model found three real MEDIUMs in a data-flow feature by reading engine consumers the in-house agents had not opened.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex reported P1s on the settlement-encounter review (2026-08-24) that collapsed under verification because it did not read the guarding call site.

OUTPUT: write your full report to stdout; it is captured to docs/reviews/raw/codex-adversarial-return-to-army-2026-09-12.md.
