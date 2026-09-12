# Adversarial review: commit 4d2ea82b, "refuge founding no longer strands the player in a dead wait menu" (#567)

You are the independent adversarial reviewer for TAOM (Bannerlord 1.4.8 total conversion, .NET Framework 4.7.2). Review ONLY commit 4d2ea82b on branch bannerlord-1.4.5. Run `git show 4d2ea82b` first. Ignore every uncommitted file in the working tree; another session owns those. Do not edit anything. Try to REFUTE every claim below before you accept it.

## What the commit claims

Players who founded a refuge and deposited troops into its garrison came back to the camp sub-menu (`taom_fc_camp`, a WAIT menu since commit 811a3429) reading "Choose how to make camp here." with zero options; time controls alive, party unable to move, and a save written there loaded straight back into it. Claimed root cause: `TaleWorlds.CampaignSystem.GameMenus.GameMenu.GetMenuOptionConditionsHold` ANDs a wait menu's condition delegate with every option's own condition, Leave included, and nothing in the engine exits the menu or ends the wait when that delegate returns false. The sub-menu's delegate was `args => _camps.PlayerCamp != null`. Refuge founding (`taom_rf_found`, inserted on that menu at index 4, isLeave false) runs `RefugeService.Found`, which calls `CampService.BreakPlayerCamp`, then `RefugeMenuController.OnWardenChosen` opened `PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners` over the still-open menu; on close `MapState.OnActivate` refreshed a panel whose every option failed the gate.

Fix, two edits: (A) the wait condition is `args => true`; (B) `OnWardenChosen` calls `_menus.ExitToLast()` (GameMenu.ExitToLast) BEFORE opening the deposit screen. Two source-pinning wiring tests were added.

## TAOM ID CHEATSHEET (not central here, but do not misread ids)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa. "rohan" and "dol_guldur" are NOT valid ids.

## READ FIRST

- docs/features/field-camp.md, section "Menus and time"
- docs/features/refuge.md, sections "Architecture" and "Traps"
- docs/reviews/rca-yotthani-camps-2026-08-23.md, Classes 9 and 12
- .claude/rules/csharp-architecture.md (thin entry points, adapters)

## Files in scope

TAOM source (read whole files, not only the diff):
- Main/Features/FieldCamp/Hooks/FieldCampMenuController.cs
- Main/Features/Refuge/Hooks/RefugeMenuController.cs
- Main/Features/FieldCamp/CampService.cs (every BreakPlayerCamp call site: HourlyTick, FrameTick, MoveGuardTick, TrySpringAmbush)
- Main/Features/Refuge/RefugeService.cs (CanFound, Found, FrameTick hold-nearby, FinishBuild)
- Main/Features/FieldCamp/Hooks/FieldCampCampaignBehavior.cs
- Main/Adapters/IGameMenuAdapter.cs, Main/Adapters/GameMenuAdapter.cs
- Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs, Main/Features/Enlistment/Hooks/EnlistmentWaitMenuOptions.cs, Main/Features/Enlistment/DischargeService.cs (the only other AddWaitGameMenu in the repo; follow-up question below)
- TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs, TAOM.Tests/Features/Refuge/RefugeWiringTests.cs

Vanilla, installed 1.4.8. Decompile with ilspycmd against "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.CampaignSystem.dll" (or the ilspy MCP server). The dump at E:/Decompiled_Bannerlord/_categories_v1.4.8/ is for browsing only.
- TaleWorlds.CampaignSystem.GameMenus.GameMenu (GetMenuOptionConditionsHold, RunWaitMenuCondition, RunOnTick, RunMenuOptionConsequence, StartWait, EndWait, ExitToLast, SwitchToMenu, ActivateGameMenu)
- TaleWorlds.CampaignSystem.GameMenus.GameMenuManager (ExitToLast, GetVirtualMenuOptionConditionsHold)
- TaleWorlds.CampaignSystem.GameState.MenuContext (Refresh, HandleStates, Destroy)
- TaleWorlds.CampaignSystem.GameState.MapState (OnLoadingFinished, OnActivate, OnMenuModeTick, EnterMenuMode, ExitMenuMode)
- TaleWorlds.CampaignSystem.ViewModelCollection.GameMenu.GameMenuVM (Refresh) in TaleWorlds.CampaignSystem.ViewModelCollection.dll
- Helpers.PartyScreenHelper.OpenScreenAsManageTroopsAndPrisoners, TaleWorlds.CampaignSystem.GameState.PartyState
- TaleWorlds.Core.MBInformationManager.ShowMultiSelectionInquiry and the affirmative callback dispatch (does the callback run while the wait menu is the current context, and is campaign time paused while the picker is open?)
- TaleWorlds.CampaignSystem.CampaignGameStarter.AddWaitGameMenu

## KNOWN SUSPECTS: CONFIRM or DISPUTE each with decompiled evidence

S1. The root cause. Confirm from GameMenu.GetMenuOptionConditionsHold that a false wait condition hides every option including isLeave options, and that no engine path exits the menu or calls EndWait when it turns false. If you find one, the whole fix is misframed; say so.

S2. GameMenu.ExitToLast from inside an inquiry affirmative callback (not a menu option consequence) while the wait is active. Claimed precedent: EncounterGameMenuBehavior.game_menu_encounter_leave_on_consequence shows an InquiryData whose affirmative delegate reaches PlayerEncounter.Finish, which calls GameMenu.ExitToLast. Verify that chain exists in 1.4.8. Then look for any difference between InformationManager.ShowInquiry and MBInformationManager.ShowMultiSelectionInquiry in how and when the affirmative action runs (same frame, deferred, inside the Gauntlet layer tick) that could make ExitToLast unsafe from the multi-selection path.

S3. Exiting a wait menu through ExitToLast never calls EndWait, so GameMenu.IsWaitActive stays true on the registered GameMenu object and _previousTickTime is stale. Claim: harmless, because SwitchToMenu, ActivateGameMenu and MapState.OnLoadingFinished all call StartWait on entry. Find any entry path into a wait menu that does NOT call StartWait (MenuContext.Refresh from MapState.OnActivate on the SAME wait menu after a conversation or a pushed screen, GameMenu.SwitchToMenu when TimeControlMode is not Stop, anything else). If one exists, what does the first RunOnTick do with a stale _previousTickTime, and does OnSubMenuTick (status re-render only) care?

S4. Ordering inside OnWardenChosen: Found (spawns the refuge party, attaches the warden, charges gold, breaks the camp) runs BEFORE ExitToLast. BreakPlayerCamp shows a message, cancels camp-placed supply orders, untracks the party. Is anything between Found and ExitToLast able to re-enter the menu system (MenuContext.Refresh, a SwitchToMenu, an inquiry) so that the exit lands on the wrong context? Also: is the multi-selection picker shown with campaign time PAUSED? If not, the camp's FrameTick keeps running while the picker is open; trace what happens if MoveGuardTick or TrySpringAmbush breaks the camp while the picker is open (the precheck in OnWardenChosen should turn it into a Warn; confirm nothing worse).

S5. After the fix, the ONLY paths that make PlayerCamp null while taom_fc_camp is open are background ticks (captivity fold, settlement fold, ambush spring). With the true condition the panel stays open, status flips to "Choose how to make camp here.", four options are disabled and Leave works. Is that acceptable, or is there a path where the player is still stuck (for example the ambush spring starting a battle: does the encounter menu replace the wait menu cleanly, and what happens to the wait menu's IsWaitActive then)?

S6. Stuck-save rescue. MapState.OnLoadingFinished re-enters the persisted GameMenuId and calls StartWait when IsWaitMenu. Confirm that a save written with GameMenuId = taom_fc_camp and no player camp now renders Leave and that Leave's consequence (RunMenuOptionConsequence: EndWait, then GameMenu.ExitToLast) lands the player on the map with TimeControlMode Stop. Look for anything in FieldCampMenuController.OnMenuInit or RefreshStatusVariables that can throw with PlayerCamp null (that would be a load-time crash on exactly the saves this fix means to rescue).

S7. The same shape in Enlistment. EnlistmentMenuBehavior registers its service wait menu with `args => _store.Record.IsEnlisted` and none of its options is isLeave. The deep review traced every path that sets EnlistmentState.NotEnlisted to DischargeService.Execute, whose RestoreCampaignContext ends by calling ExitServiceMenuIfOpen. Refute or confirm: is there any path (load-time record repair, session reset for a pre-feature save with the menu id persisted, MCM toggle, co-op client, exception thrown before ExitServiceMenuIfOpen) where IsEnlisted becomes false while the service wait menu is the current or persisted menu, leaving zero options? Report as FOLLOW-UP with evidence; the commit deliberately did not touch Enlistment.

S8. The tests. Both new tests are source-text pins (string slicing of the controller files). Can either pass vacuously: an empty slice, a terminator that matches too early (the `);` search inside the AddWaitGameMenu call; the `private ` search after OnWardenChosen), a future refactor that moves the exit into a helper method so the literal `_menus.ExitToLast()` no longer appears in OnWardenChosen while the behaviour is still correct (false RED), or the reverse (true GREEN with broken behaviour)?

## REQUIRED SECTIONS in your output

1. VANILLA CODE: paste the decompiled bodies of GameMenu.GetMenuOptionConditionsHold, RunWaitMenuCondition, RunMenuOptionConsequence, StartWait, EndWait, ExitToLast, SwitchToMenu and MapState.OnLoadingFinished, OnActivate, ExitMenuMode as code blocks, with the assembly and version you read them from.
2. KNOWN SUSPECTS: S1 to S8, each CONFIRMED or DISPUTED with file and line evidence from both codebases.
3. SCENARIO WALKS: (a) fresh founding on the fix build, from the picker to the map; (b) loading a save written in the stuck state on the fix build; (c) founding while the player's party is inside manage range of an existing refuge (CanFound should refuse); (d) the picker cancelled (negativeAction) after the camp was broken by a background tick.
4. CONFIG CROSS-REFERENCE: the commit adds no ModuleData or localization; confirm by grepping the diff for `{=` and for XML paths.
5. FINDINGS OR OBSERVATIONS: each with severity P1/P2/P3, file:line, the failing scenario, and proving code or a decompiled line. If you have none, say so and state what you checked.

## QUALITY GATES

- Every claim about engine behaviour cites a decompiled line from the installed 1.4.8 DLL, not the dump.
- Every "missing" claim is backed by a grep you ran.
- Do not flag vanilla-matching behaviour as a bug.
- Do not restate the commit message as findings.
- Severity requires a reproducible scenario.

## Prior review lessons

SUCCESSES: config id cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; review 95 (shader precompile) found seven real ordering issues by walking the engine's own call sequence.
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped the hard decompile sections and reasoned from names.

Output the review as markdown. It will be saved to docs/reviews/raw/codex-adversarial-camp-wait-menu-2026-09-12.md.
