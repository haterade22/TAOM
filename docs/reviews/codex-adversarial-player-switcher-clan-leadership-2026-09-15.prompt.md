# Codex Adversarial Review: Player Switcher clan leadership (#550) + Patch80 seam D

You are an adversarial reviewer for TAOM, a Lord of the Rings total conversion for Mount & Blade II: Bannerlord v1.5.3 (installed). Your job is to find real defects in the changeset below, prove each one against the actual code (TAOM source and the installed engine's decompiled bodies), and refute your own claims before reporting them. Read AGENTS.md and .ai/roles/reviewer.md first. This is a review-only assignment: do not edit files, do not run git write commands, do not write the report to any path. Return the full report as your FINAL MESSAGE; the dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-player-switcher-clan-leadership-2026-09-15.md. Do NOT write that path yourself.

Engine sources: decompiled v1.5.3 is at E:\Decompiled_Bannerlord\_categories_v1.5.3\ (category tree: Campaign/, MountAndBlade/, UI/, Core/, Modules/). Installed DLLs are authoritative for signatures; if a body matters, read it there or via `pwsh tools/taom-src.ps1 path <FullTypeName>` from the repo root.

## Feature description

Players who take over a king's spouse or child (Boromir, Faramir) through the Player Switcher were left with `Clan.PlayerClan.Leader` still pointing at the AI king. Vanilla keys the player's part in a kingdom election off `Supporter.IsPlayer => Clan.Leader.IsHumanPlayerCharacter`, so every election resolved through `KingdomElection.ReadyToAiChoose()` inside `DecisionItemBaseVM`'s own constructor and the popup rendered already decided and unclosable from the second decision of a screen visit on (the widget latch `KingdomDecisionPopupWidget.IsKingsDecisionDone` is edge-triggered and never reset). Three changes:

(A) Root fix: `IPlayerIdentityAdapter.PromoteToClanLeader(heroId)` wraps vanilla `ChangeClanLeaderAction.ApplyWithSelectedNewLeader(hero.Clan, hero)`; `HeroSwitchService.Run` calls it on the takeover path right after `ReassignPlayerClan`, after `ChangePlayerCharacterAction.Apply`, before gold transfer, career re-key, `MarkClanAndKingdomKnown` and `KillCharacterAction.ApplyByRemove` of the created hero. A taken-over non-leader of the ruling clan therefore becomes the kingdom's ruler; that consequence was chosen deliberately.

(B) Session-launch repair for existing saves: `PlayerClanLeadershipService.RepairIfNeeded()` (from `PlayerClanLeadershipRepairBehavior` on `CampaignEvents.OnSessionLaunchedEvent`) promotes `Hero.MainHero` when `Clan.PlayerClan.Leader != Hero.MainHero`, gated by: co-op authority only; snapshot valid; hero in play (alive, not disabled, not NotSpawned); leader != hero; `Clan.PlayerClan.StringId != "player_faction"` (the takeover proxy `AiPartySizeService.IsTakenOverPlayerClan`); `Hero.MainHero.Clan == Clan.PlayerClan`; clan has a leader. Prisoners are repaired. On repair it shows one `InformationManager.DisplayMessage` line via `IInquiryAdapter.ShowMessage`.

(C) Patch80 seam D: `KingdomDecisionsVM_RefreshWith_AutoResolved_Patch`, a `Priority.Last` postfix on `KingdomDecisionsVM.RefreshWith(KingdomDecision decision)`. When `__instance.CurrentDecision` is the item built for THIS `decision` (matched through the private `_decision` field) and `IsActive && IsKingsDecisionOver` already hold, it invokes vanilla's protected `DecisionItemBaseVM.ExecuteDone()` through a `MethodInfo` cached in `KingdomVoteDeadlockBinding.Initialize`. Seams A to C (#547) are unchanged; `ExecuteDone` is kept out of the binding's `IsReady` so losing it disables only seam D.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur". Denethor is Hero `lord_1_7`, owner of `clan_empire_west_1` (the Gondor ruling clan); Boromir is `lord_1_75`, Faramir `lord_1_34`, both children of `lord_1_7`.

## READ FIRST

- docs/features/player-switcher.md, sections "The handover, and why its order is load-bearing" and "Clan leadership follows the player (#550)"
- docs/features/diplomacy.md, section "Kingdom vote deadlock guard (Patch80)"
- docs/reference/harmony-patch-registry.md, section "Patch80_KingdomVoteDeadlock"
- docs/reviews/rca-kingdom-vote-deadlock-2026-09-06.md (the #547 review; its lessons about substituting for vanilla methods apply directly to seam D)
- docs/reviews/rca-player-switcher-clan-leadership-2026-09-15.md (the Claude deep-review RCA of this changeset; one MED finding already fixed)
- CHANGELOG.md, entry "fix(player-switcher): a taken-over non-leader now leads their clan, and Patch80 seam D closes a pre-concluded vote (#550)"

## KNOWN SUSPECTS (CONFIRM or DISPUTE each, with the decompiled lines)

S1. Ordering of the promotion. `ChangeClanLeaderAction.ApplyInternal` runs after `ChangePlayerCharacterAction.Apply` and the `Campaign.PlayerDefaultFaction` reflection write, before `KillCharacterAction.ApplyByRemove(createdHero, showNotification: false, isForced: true)`. Hypothesis to test: some side effect of ApplyInternal (GiveGoldAction old->new leader; ChangeGovernorAction.RemoveGovernorOf; MobilePartyHelper.CreateNewClanMobileParty or MobileParty.ChangePartyLeader; the relation loop over Hero.AllAliveHeroes; clan.SetLeader; OnClanLeaderChanged listeners) interacts with a step before or after it. In particular: when the taken-over hero has NO party and rides in the old leader's party, Campaign.OnPlayerCharacterChanged has already made that party MobileParty.MainParty; ChangePartyLeader then re-heads it to the player with the old king as a member. Is that state coherent for vanilla (AI lord as a non-leader member of the main party: does any daily tick try to give him his own party, does `LordPartyComponent.Owner` agree with `_leader`, does the old king's `PartyBelongedTo` behave)? And: does anything in KillCharacterAction.ApplyByRemove for the created hero read the ruling clan's leader?

S2. Seam D invokes `ExecuteDone` synchronously inside the `RefreshWith` postfix. One call site is the affirmative callback of `HandleDecision`'s inquiry (KingdomDecisionsVM.cs:220-228), so `ExecuteDone`'s own `InformationManager.ShowInquiry` runs while the previous inquiry's callback is still on the stack. Vanilla's single-clan branch of `RefreshWith` does the same. Hypothesis: is there any InformationManager / GauntletInquiry state where a nested ShowInquiry is dropped or double-queued on v1.5.3 (read InformationManager.ShowInquiry and the Gauntlet inquiry view's handling of an inquiry raised from an inquiry callback)? Also trace `KingdomDecisionsVM.OnFrameTick` between ExecuteDone and the player's OK: IsActive is false, `_shouldCheckForDecision` is false (set by RefreshWith's else branch), CurrentDecision is non-null with IsActive false; confirm OnFrameTick cannot offer the next decision before OnDecisionOver runs, and that GauntletKingdomScreen unlocks map navigation once IsActive is false.

S3. `ExecuteDone` is invoked on an item whose `_finalSelectionDone` is false and whose `_currentSelectedOption` is null. Hypothesis: something in ExecuteDone, in the inquiry, or in `OnDecisionOver` -> `_refreshKingdomManagement` assumes a selection was made (reads `_currentSelectedOption`, or the DecisionOptionVM list's IsSelected) and NREs or asserts. Read DecisionItemBaseVM.ExecuteDone, KingdomDecisionsVM.OnDecisionOver, KingdomManagementVM.OnRefresh and whatever `_refreshKingdomManagement` points at.

S4. The session-launch repair's gate. Enumerate vanilla and TAOM paths that leave `Clan.PlayerClan.StringId != "player_faction"` while `Clan.PlayerClan.Leader != Hero.MainHero` LEGITIMATELY, so the repair would wrongly promote: vanilla heir selection (ApplyHeirSelectionAction), retirement, StoryMode, the player joining a kingdom, TAOM's PlayerPossession (co-op possession of another hero: Main/Features/PlayerPossession), TAOM's Enlistment (Main/Features/Enlistment), anything that calls ChangePlayerCharacterAction.Apply (grep both the engine and Main/). For each, state whether the gate declines correctly. Also: OnSessionLaunched fires before character creation on a NEW campaign; confirm the created character already leads player_faction at that moment (read Campaign / CampaignObjectManager / SandBoxGameManager's new-game path) so the table declines by the proxy alone.

S5. `InformationManager.DisplayMessage` from OnSessionLaunched. Hypothesis: on a loaded save the message is raised before the map screen and its message log exist, so it is silently dropped and the player never sees the "You now lead ..." line. Read InformationManager.DisplayMessage and how the map's message log subscribes; say whether a message raised in OnSessionLaunched survives to the map. If it does not, propose the earliest event that does (e.g. OnGameLoadFinished / the first tick).

S6. Kingdom.Leader consumers. The promotion changes `Kingdom.Leader` (computed from RulingClan.Leader) without any ruling-clan change event. Vanilla's own non-leader king path (KingSelectionKingdomDecision.ApplyChosenOutcome) does the same ChangeClanLeaderAction call, so this should be fine. Hypothesis to test anyway: grep the engine for fields that cache the kingdom leader or `Kingdom.Leader == Hero.MainHero` at load or construction and are not refreshed (KingdomManagementVM, KingdomDiplomacyVM, encyclopedia pages, map notifications, `Hero.IsKingdomLeader`-style flags, the Kingdom's own `_leader`-like fields if any). Report any that would hold the old ruler after a session-launch repair.

S7. Seam D's match `ReferenceEquals(GetDecisionOf(item), decision)`. Hypothesis: `KingdomManagementVM.ForceDecideDecision` can be called while an earlier window for a DIFFERENT decision is still open (CurrentDecision non-null, IsActive true, not over). RefreshWith's else branch overwrites CurrentDecision with the new item. Is the old item finalized/its listener cleared anywhere (leak, as seam B's history), and does seam D's guard behave correctly in that sequence?

## FILES

TAOM changed/new (under E:\repos\TAOM):
- Main/Adapters/IPlayerIdentityAdapter.cs
- Main/Adapters/PlayerIdentityAdapter.cs (new methods PromoteToClanLeader, GetPlayerClanLeadership at the end)
- Main/Features/PlayerSwitcher/Domain/PlayerClanLeadership.cs (new)
- Main/Features/PlayerSwitcher/HeroSwitchService.cs
- Main/Features/PlayerSwitcher/IPlayerClanLeadershipService.cs (new)
- Main/Features/PlayerSwitcher/PlayerClanLeadershipService.cs (new)
- Main/Features/PlayerSwitcher/PlayerClanLeadershipRepairBehavior.cs (new)
- Main/Features/PlayerSwitcher/PlayerSwitcherIoC.cs
- Main/Features/Diplomacy/Hooks/KingdomVoteDeadlockBinding.cs
- Main/Features/Diplomacy/Hooks/KingdomDecisionsVM_RefreshWith_AutoResolved_Patch.cs (new)
- Main/SubModule.cs (the PlayerClanLeadershipRepairBehavior AddBehavior near line 956 and the seam D Initialize near line 1495)
- Main/_Module/ModuleData/taom_player_switcher_strings.xml and Main/_Module/ModuleData/Languages/*/std_taom_player_switcher_strings_*.xml (new key taom_ps_clan_leader_repaired, English-seeded)

Context, unchanged but load-bearing:
- Main/Features/PlayerSwitcher/HeroPickerService.cs, SwitchPlanner.cs, PlayerSwitchContentHandler.cs, PlayerSwitchRegistrationBehavior.cs
- Main/Features/Diplomacy/Hooks/KingdomDecisionsVM_RefreshWith_Patch.cs (seam A), KingdomDecisionsVM_HandleDecision_Patch.cs (seam C), DecisionItemBaseVM_ExecuteFinalSelection_Patch.cs (seam B)
- Main/Features/Diplomacy/KingdomVoteDeadlockService.cs, Main/Adapters/KingdomBallotAdapter.cs
- Main/Features/AiPartySize/AiPartySizeService.cs (IsTakenOverPlayerClan, VanillaPlayerClanId)
- Main/Adapters/InquiryAdapter.cs (ShowMessage)
- Main/Features/CoopInterop/ICoopSessionProvider.cs

Tests:
- TAOM.Tests/Features/PlayerSwitcher/HeroSwitchServiceTests.cs
- TAOM.Tests/Features/PlayerSwitcher/PlayerClanLeadershipServiceTests.cs (new)
- TAOM.Tests/Features/PlayerSwitcher/PlayerSwitcherBindingTests.cs
- TAOM.Tests/Features/PlayerSwitcher/PlayerSwitcherWiringTests.cs
- TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs
- TAOM.Tests/Migration/ReflectionSiteBindingTests.cs

Other sessions' uncommitted edits are also present in the working tree (Armory audit tooling #599, special-resources costs #600, hooks and docs for those). They are OUT OF SCOPE; do not report on them.

## REQUIRED SECTIONS

### 1. VANILLA CODE
Paste the relevant bodies as code blocks, from the v1.5.3 decompile: KingdomElection.StartElection, ReadyToAiChoose, ApplyChosenOutcome, ApplySelection; DecisionItemBaseVM constructor, InitValues, OnKingdomDecisionConcluded, ExecuteFinalSelection, ExecuteDone; KingdomDecisionsVM.OnFrameTick, HandleDecision, RefreshWith, OnDecisionOver; KingdomManagementVM.ForceDecideDecision and OnRefresh; KingdomDecisionPopupWidget (whole class); Supporter.IsPlayer; ChangeClanLeaderAction (whole class); ChangePlayerCharacterAction.Apply; Campaign.OnPlayerCharacterChanged; the relevant part of KillCharacterAction.ApplyInternal; ApplyHeirSelectionAction.ApplyInternal; KingSelectionKingdomDecision.ApplyChosenOutcome; Hero.IsAlive/IsDisabled/IsNotSpawned; InformationManager.DisplayMessage and ShowInquiry.

### 2. HANDOVER SEQUENCE ANALYSIS
Walk HeroSwitchService.Run step by step for a Boromir takeover in two shapes: (a) Boromir leads his own party; (b) Boromir has no party and sits in Minas Tirith or rides in Denethor's party. State the value of Hero.MainHero, Clan.PlayerClan, Clan.PlayerClan.Leader, MobileParty.MainParty, MainParty.LeaderHero, LordPartyComponent.Owner, Denethor.PartyBelongedTo, Boromir.GovernorOf after each step. Name any step where two of those disagree.

### 3. REPAIR STATE MATRIX
Table: every combination the OnSessionLaunched mutation can meet (states from CharacterStates x clan pointer relationships x co-op role x vanilla/takeover proxy), with the gate's verdict and whether that verdict is right. Include a vanilla heir-selection save, a retirement save, a StoryMode save, a co-op host and client, a save from before #514 existed.

### 4. SEAM D TRACE
For each RefreshWith call site: the exact state of KingdomDecisionsVM and DecisionItemBaseVM after the postfix, through the inquiry OK, to the next OnFrameTick. Then the seam A/seam D interplay when seam A's prefix returns false. Then the widget: confirm the popup is hidden by IsActive=false regardless of the IsKingsDecisionDone latch.

### 5. CONFIG CROSS-REFERENCE
The new key in taom_player_switcher_strings.xml and the 12 language files; the {CLAN} variable name against InquiryAdapter.ShowMessage; the reflection-site catalogue rows in docs/reference/taleworlds-api-snapshot/reflection-sites.md against KingdomVoteDeadlockBinding; the Patch80 category literal against SubModule.cs.

### 6. FINDINGS OR OBSERVATIONS
Each finding: severity (P1 blocks ship, P2 should fix, P3 nice to have), file:line, the defect, the proof (code you read, both sides), the concrete failing scenario, and the minimal fix. If you have no P1/P2 findings, say so plainly; do not invent one. Observations are things worth knowing that are not defects.

## QUALITY GATES
- Every claim about the engine cites a decompiled line you read this session.
- Try to refute each of your findings before reporting it; report the refutation attempt.
- Do not flag code that matches vanilla's own behaviour as a bug.
- Do not report the other sessions' files.
- No em or en dashes in your prose; use commas, colons or parentheses.

## PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Review #547 (Patch80) caught a leaked KingdomDecisionConcluded listener and an error path that turned a hang into a crash by enumerating every branch of the vanilla method being replaced.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections. Codex once reported a mismatch from the OLD decompile dump instead of the installed engine.

Return the full report as your final message.
