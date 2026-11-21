Adversarial review: TAOM fix #1516, special-resource upgrade charge on a successful PartyScreenLogic.DoneLogic.

You are a REVIEWER. Do not edit any file. Do not write the output file; return the full report as your FINAL MESSAGE (the dispatcher redirects stdout into docs/reviews/raw/). Read AGENTS.md and .ai/review-reference.md first.

FEATURE: Special resources. A troop upgrade into a troop with an upgrade_cost (troop_resource_costs.xml) queues a cost while the party screen is open (PartyScreenLogic_UpgradeTroop_Patch -> ISpecialResourceService.QueueUpgradeSpend). Before this change the cost was debited only at screen close (SpecialResourcesBehavior.OnPartyScreenClosed -> CommitSession), and a Cancel (CancelSession) or Reset (OnPartyScreenReset -> RestartPartyScreenSession from PartyScreenHeldUpgrades.Read) dropped or rebuilt the queue. PartyScreenLogic.DoneLogic can return true while the screen stays open (PartyVM.ExecuteTalk; GauntletPartyScreen.OnCompanionRemoved via IChangeableScreen.ApplyChanges; TAOM's Main/Adapters/PartyScreenCommit.cs forced DoneLogic(true)). Vanilla then applies the upgrades and empties UpgradedTroopsHistory, so a later Escape made the upgrades free. The fix: a Harmony postfix on DoneLogic charges the queued cost when __result is true, through IOnPartyUpgradeResourceCheck.ChargeAppliedUpgrades (0 in a live co-op session) -> SpecialResourceService.CommitAppliedUpgrades (commits and keeps the session open).

THE CHANGE: run `git diff` and `git status` in this worktree. New file: Main/Features/SpecialResources/Hooks/PartyScreenLogic_DoneLogic_ChargePatch.cs. Changed: SpecialResourceService.cs, ISpecialResourceService.cs, Hooks/IOnPartyUpgradeResourceCheck.cs, Hooks/PartyUpgradeResourceCheckHook.cs, Main/SubModule.cs (Initialize line), BondsOfBattleModule.cs (comment), two test files under TAOM.Tests/Features/SpecialResources/, docs/features/special-resources.md, docs/reference/harmony-patch-registry.md, docs/reference/taleworlds-api-snapshot/patch-targets.md.

READ FIRST: docs/features/special-resources.md; docs/reference/harmony-patch-registry.md sections Patch26_SpecialResources, Patch106 (same DoneLogic target, BondsOfBattle), Patch144_CoopUpgradeCharge; Main/Features/SpecialResources/SpecialResourcesBehavior.cs (OnScreenPushed, AttachToPartyScreen, OnPartyScreenClosed, OnPartyScreenReset).

VANILLA TARGETS (read from the installed v1.5.7 DLLs with the ilspy server or `pwsh tools/taom-src.ps1 path <Type>`): TaleWorlds.CampaignSystem.Party.PartyScreenLogic (DoneLogic, OnPartyScreenClosed, Reset, ResetLogic, SavePartyScreenData, ResetToLastSavedPartyScreenData, FireCampaignRelatedEvents); Helpers.PartyScreenHelper (ClosePartyPresentation); PartyVM (ExecuteTalk, ExecuteDone, ExecuteCancel); PartyTroopManagerVM (OpenPopUp, ConfirmCancel); SandBox.GauntletUI.GauntletPartyScreen (ApplyChanges, OnCompanionRemoved). Coop: Modules/Coop GameInterface.dll PartyScreenLogicPatches.DoneLogicPrefix and PartyDoneLogicHandler (source also at E:/repos/BannerlordCoop/source).

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with quoted lines):
S1. Double charge on the normal Done: the postfix commits, then OnPartyScreenClosed commits again. Claimed safe because pending is 0 after CommitAppliedUpgrades. Verify the order in ClosePartyPresentation and every other close path (isForced closes, Loot mode, any path that fires PartyScreenClosedEvent without DoneLogic).
S2. Wrong payer or stray charge: the postfix runs for every PartyScreenLogic.DoneLogic in the process and charges Hero.MainHero whenever the service is in session. Is there any DoneLogic call on a logic other than the open TAOM-session screen while _inSession is true and _pendingSpend > 0 (for example a screen pushed without a GauntletPartyScreen push, so BeginPartyScreenSession never ran and an old session leaks)?
S3. Co-op: Coop's DoneLogicPrefix skips the original, calls Reset(true), sets __result. With the hook's IsSessionActive gate, is any upgrade charged twice (client plus Patch144 on server) or never? Consider host, guest, dedicated server, and Coop patches still installed after a session ends in the same process.
S4. Re-queue after an already charged Done: the troop-manager popup saves screen data (SavePartyScreenData) and ConfirmCancel restores it (ResetToLastSavedPartyScreenData fires AfterReset -> RestartPartyScreenSession). If a successful DoneLogic happens between those, the restore could re-queue upgrades already charged. Reachable?
S5. Prisoner recruit charges (OnUnitRecruited via FireCampaignRelatedEvents) run inside DoneLogic before the postfix. Does CommitSession's measured debit still give the right toast and floor at zero?
S6. Exceptions: the postfix swallows every exception. Could a swallowed fault leave _pendingSpend partly committed or the session closed?

REQUIRED SECTIONS:
1. VANILLA CODE: paste the decompiled lines you rely on as code blocks.
2. DEEP ANALYSIS: walk these scenarios step by step with the session state (_inSession, _pendingSpend) after each step: (a) upgrade, Done; (b) upgrade, Talk confirm, Escape; (c) upgrade, Talk confirm, Reset, Done; (d) upgrade, Provender Buy (PartyScreenCommit) in conversation, back to the screen, upgrade again, Escape; (e) upgrade, Done fails for gold; (f) co-op guest upgrade, Done.
3. CONFIG CROSS-REFERENCE: none changed; say so if you find no config impact.
4. FINDINGS: each with severity (P1/P2/P3), file:line, quoted evidence, and a concrete failing scenario. Mark anything you could not prove UNVERIFIED.

QUALITY GATES: Quote code for every claim. Do not report vanilla-matching behaviour as a TAOM bug. Do not skip a section; say "no finding" when there is none.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. Do not write docs/reviews/raw/codex-adversarial-party-done-charge-2026-11-21.md yourself.
