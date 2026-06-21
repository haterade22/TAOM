Adversarial code review of the TAOM "LotrIssues" feature (Wave 0). TAOM is a Lord of the Rings total-conversion mod for Mount and Blade II Bannerlord, engine version v1.4.6 (installed). You are an independent reviewer. Be skeptical and concrete. Confirm or DISPUTE each Known Suspect by reading the actual code. Do not flag code that correctly mirrors vanilla as a bug.

WHAT THIS FEATURE DOES
LotrIssues replaces Bannerlord's procedural "issue" (quest) system with LOTR-flavored custom issues. Wave 0 ships: a generic config-driven framework, suppression of ALL 43 vanilla issue behaviors, and ONE working template (T1 "DeliverGoods" -- deliver N of an item to a notable). The other 7 templates are deferred to later waves by design. This just passed an internal multi-agent review (Standards PASS, API 64 verified / 0 incompatible, 8 findings all fixed). Find what that review missed.

ENGINE VERSION
Installed game is v1.4.6. The authoritative decompiled source is cached at C:\Users\mikew\.taom-src\v1.4.6\ (per-type .cs files; e.g. TaleWorlds.CampaignSystem.Issues.IssueBase.cs, TaleWorlds.CampaignSystem.QuestBase.cs, TaleWorlds.CampaignSystem.Issues.PotentialIssueData.cs, TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.cs, TaleWorlds.CampaignSystem.CampaignGameStarter.cs, TaleWorlds.CampaignSystem.Hero.cs). You can also run: pwsh tools/taom-src.ps1 path <FullTypeName> to decompile any other installed type. Do NOT trust E:\Decompiled_Bannerlord (it is a 1.4.5 dump). Verify every engine-API claim against the v1.4.6 cache.

READ FIRST
- docs/reviews/rca-lotr-issues-wave0-2026-06-17.md (the internal review + the 8 fixed findings -- do not re-report these unless a fix is wrong)
- docs/features/lotr-issues.md (the conversion plan + per-issue disposition matrix)
- docs/reference/engine/issue-and-quest-system.md (TAOM's own engine reference for this subsystem)
- Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml (the one shipped config issue)

REFERENCE IMPLEMENTATION
DeliverGoodsLotrIssue.cs is a generic, config-driven re-implementation of the vanilla HeadmanNeedsGrainIssueBehavior (cache: C:\Users\mikew\.taom-src\v1.4.6\TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.cs). Diff TAOM's version against vanilla to spot behavioral drift, missing cancellation paths, or incorrect API use.

KNOWN SUSPECTS -- confirm or DISPUTE each by reading source
1. DISPATCH / RelatedObject. LotrIssuesCampaignBehavior.OnCheckForIssue builds PotentialIssueData(OnSelected, type, frequency, def) where def is passed as the relatedObject arg, and OnSelected reads pid.RelatedObject as LotrIssueDefinition to construct the issue. Verify against v1.4.6: does IssueManager actually invoke the SAME PotentialIssueData (with RelatedObject intact) as the OnStartIssue/OnSelected callback when the issue is selected? If the engine reconstructs or copies the pid without RelatedObject, def is null and NO issue ever spawns. Trace IssueManager.CheckForIssues -> selection -> OnStartIssue invocation.
2. SUPPRESSION LOAD-ORDER. LotrIssueSuppression.SuppressAll is called from Main/SubModule.cs OnGameStart and calls CampaignGameStarter.RemoveBehaviors<T> (via reflection) for 43 vanilla issue behavior types. This only works if TAOM's OnGameStart runs AFTER the behaviors are registered. Vanilla registers 36 in SandBoxManager.Initialize and 7 in SandBoxSubModule.InitializeGameStarter. Confirm the actual call ordering at game start: does a dependent module's OnGameStart run after SandBoxManager.Initialize AND after SandBoxSubModule.InitializeGameStarter? If RemoveBehaviors runs before registration, it is a silent no-op and vanilla Calradic issues still spawn alongside the LOTR ones. Also confirm RemoveBehaviors<T> actually removes by exact type (not assignable-from) and that calling it for a type that was never added is a safe no-op.
3. HOST SPAWNER. The suppression intentionally does NOT remove IssuesCampaignBehavior (the host that raises OnCheckForIssueEvent). Confirm OnCheckForIssueEvent is raised by IssuesCampaignBehavior (or another non-removed host), independent of the 43 removed issue behaviors -- otherwise our listener never fires and zero issues spawn.
4. DIALOG TURN-IN. DeliverGoodsLotrIssueQuest.SetDialogs builds OfferDialogFlow = DialogFlow.CreateDialogFlow("issue_classic_quest_start")... and DiscussDialogFlow = DialogFlow.CreateDialogFlow("quest_discuss")... with a player option gated by ClickableCondition + a Consequence that hooks Campaign.Current.ConversationManager.ConversationEndOneShot += Success. Verify these dialog-token strings and the fluent chain against v1.4.6 (compare to vanilla HeadmanNeedsGrainIssueQuest.SetDialogs). Will the turn-in option actually appear and complete the quest, and is hooking ConversationEndOneShot the correct completion trigger?
5. SAVEABLE ID COLLISION. LotrIssueSaveableTypeDefiner uses base 726900801 with AddClassDefinition(DeliverGoodsLotrIssue, 101) and (DeliverGoodsLotrIssueQuest, 102). Engine global id = base + localId, so 726900902 and 726900903. Confirm these do not collide with any other TAOM SaveableTypeDefiner -- known in use: EquipPresets 726900501 (101,102), FormationPreset 726900601 (101), CareerQuest 726900701 (101 -> 726900802). Grep the repo for ": SaveableTypeDefiner" and verify no derived-id overlap.
6. ISSUE STAY-ALIVE vs ACCEPTED QUEST. DeliverGoodsLotrIssue.IssueStayAliveConditions() returns ResolveItem(DeliverItemId) != null (a fail-closed guard added in the internal review). Verify: once the player has ACCEPTED the quest, does the engine still call IssueStayAliveConditions on the issue, and would a transient false (e.g., item lookup hiccup) wrongly cancel an in-progress quest? Compare to how vanilla uses IssueStayAliveConditions.
7. SAVE/LOAD of the quest. DeliverGoodsLotrIssueQuest has [SaveableField] _defId, _itemId, _neededCount, _rewardGold, _difficulty, _acceptedLog, _readyLog. InitializeQuestOnGameLoad re-resolves _def from the service by _defId and calls SetDialogs. Confirm: (a) the two JournalLog fields round-trip without a hand-written AutoGeneratedInstanceCollectObjects (the internal review REMOVED those as cargo-cult, citing CareerQuest which saves a List<JournalLog> via [SaveableField] alone -- verify CareerQuest actually does this and that single JournalLog [SaveableField]s are collected by the engine's reflection fallback); (b) nothing reads _def before EnsureDef on the load path; (c) if the config row for _defId was deleted between saves, GetIssueById returns null and the quest degrades without crashing.
8. SUPPRESSION COMPLETENESS / SAVE-COMPAT. Confirm the 43-type list in LotrIssueSuppression matches the actual vanilla registration sites (36 in SandBoxManager.Initialize, 7 in SandBoxSubModule). Flag any vanilla issue behavior that is registered but NOT in the suppression list (would leak a Calradic issue) or any name in the list that is not actually registered. Separately: assess the save-compat risk for a pre-suppression campaign that has an in-flight vanilla issue whose behavior is now removed -- does load soft-lock, and is "new-campaign feature" an adequate mitigation?

ALSO CHECK (beyond the suspects)
- Per-typeof cooldown/saturation: IssueManager keys cooldown + over-representation by issue Type. All DeliverGoods configs share typeof(DeliverGoodsLotrIssue). For Wave 0 (one config) confirm this is at worst a balance concern, not a correctness bug; note if it becomes one in later waves.
- GetEligibleIssues vs CanPlayerTakeQuestConditions: the service filters offers by occupation + culture + relation_min; the issue's CanPlayerTakeQuestConditions independently re-checks relation (now reads def.RelationMin) + at-war. Confirm these two gates cannot disagree in a way that offers an un-takeable issue.
- LotrIssueGiverAdapter.Occupation: maps Hero notable roles + Lord. Verify against v1.4.6 Hero (IsNotable is false for lords; the fix checks IsLord independently). Confirm IsValid requiring CurrentSettlement != null does not wrongly exclude valid givers for any occupation the shipped config uses (Headman).
- Config validation (LotrIssueConfigProvider): confirm the skip-invalid-and-warn + FiniteFloat handling has no gap that lets a malformed issue through to runtime.

TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
NOTE: "rohan" is NOT a valid id (use vlandia). "dol_guldur" is NOT valid (use dolguldur). The shipped config uses cultures="" (all), so no id cross-ref applies in Wave 0, but flag any hardcoded id assumptions in the code.

FILES TO REVIEW
Feature: Main/Features/LotrIssues/Domain/LotrIssueTemplate.cs, IssueGiverOccupation.cs, IssueFrequencyTier.cs, LotrIssueDefinition.cs ; Main/Features/LotrIssues/ILotrIssueConfigProvider.cs, LotrIssueConfigProvider.cs, ILotrIssueService.cs, LotrIssueService.cs, LotrIssueSaveableTypeDefiner.cs, LotrIssueSuppression.cs, LotrIssuesCampaignBehavior.cs, LotrIssuesIoC.cs ; Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs
Adapters: Main/Adapters/ILotrIssueGiverAdapter.cs, ILotrIssueRewardAdapter.cs, LotrIssueGiverAdapter.cs, LotrIssueRewardAdapter.cs
Config: Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml
Wiring: Main/IoC.cs (the LotrIssuesIoC.RegisterLotrIssuesFeature line), Main/SubModule.cs (the LotrIssueSuppression.SuppressAll + AddBehavior block in OnGameStart)
Tests: TAOM.Tests/Features/LotrIssues/LotrIssueConfigProviderTests.cs, LotrIssueServiceTests.cs, LotrIssueSuppressionTests.cs

REQUIRED OUTPUT SECTIONS
1. KNOWN SUSPECTS -- for each of the 8: CONFIRMED / DISPUTED / PARTIAL, with the exact file:line and the v1.4.6 source evidence (paste the relevant vanilla code block).
2. ADDITIONAL FINDINGS -- anything else, each with severity (HIGH/MED/LOW), file:line, why it is a bug, and the fix. Paste both the TAOM code and the vanilla code you compared against.
3. SAVE/LOAD + DISPATCH correctness verdict -- can an issue actually spawn, be accepted, tracked, turned in, and survive save/load in v1.4.6? State your confidence and what you could not verify statically.
4. FALSE-POSITIVE SELF-CHECK -- list anything you considered flagging but confirmed is correct (esp. anything that matches vanilla).

QUALITY GATES
- Every engine-API claim must cite the v1.4.6 cache (paste the code). Claims without evidence will be discounted.
- Do not flag the 8 already-fixed findings in the RCA unless a fix is actually wrong -- if so, say which and why.
- Do not flag deferred Wave 1-7 scope (7 other templates, category: item sourcing, the strings file / 12-language registration) as missing -- it is intentional.
- "I could not verify X statically" is an acceptable and valuable answer -- say so rather than guessing.

PRIOR REVIEW LESSONS
Successes: cross-referencing config IDs caught rohan/dol_guldur mismatches; decompiling the vanilla target caught missing gates; lifecycle tracing caught stale caches.
Failures to avoid: do not assume empire=Rohan (empire=Dunland); do not flag vanilla-matching code as a bug; do not skip the hard sections (dispatch + save/load are the hard sections here).
