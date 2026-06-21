# Codex Adversarial Review — LotrIssues (full feature, Waves 1-7)

You are an independent adversarial reviewer for TAOM, a Bannerlord v1.4.6 total-conversion mod. Find real bugs. Be specific, cite file:line, and CONFIRM or DISPUTE each Known Suspect. Do not invent issues; verify against the source and the decompiled engine.

## What this feature does

LotrIssues replaces all 43 vanilla procedural "issues" (the problems-at-a-notable the player solves for reward) with 43 TAOM-authored Middle-earth issues, and suppresses the vanilla ones. Generic-template + XML-config architecture: one config row per issue, 3 mechanic templates. Wave 0 (framework + DeliverGoods template + suppression + dispatch + adapters + SaveableTypeDefiner) was already reviewed by you (review 60) and by a 5-agent deep-review; its 8 deep-review findings + 2 Codex MEDIUMs were all fixed. THIS pass focuses on what shipped AFTER Wave 0: the Combat template (+ its 3 variants), the DeliverPersonnel template, the 43 configs, and the localization wiring.

Engine is v1.4.6 (NOT 1.4.5 despite the branch name). Verify signatures against the INSTALLED DLLs at:
E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/
using ilspycmd. Decompiled browse-only dump is at E:/Decompiled_Bannerlord/ (do NOT trust it for signatures).

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar.
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.
Culture IDs (vanilla-engine): vlandia=Rohan, empire=Dunland, aserai=Harad, khuzait=Easterlings, sturgia=Dale, battania=Khand. NOTE "rohan"/"dunland"/"dol_guldur" are NOT valid ids.

## READ FIRST

- docs/features/lotr-issues.md (the "Implementation (as built)" section + the known per-type-saturation limitation)
- docs/reviews/rca-lotr-issues-wave0-2026-06-17.md (Wave 0 findings already fixed; the M1 parsed-but-unresolvable + M2 trimmed-behavioral-port lessons)
- Main/Features/LotrIssues/LotrIssueConfigProvider.cs (the validation rules every config must pass)

## FILES IN SCOPE (new since Wave 0)

Templates:
- Main/Features/LotrIssues/Templates/CombatLotrIssue.cs  (CombatLotrIssue : IssueBase + CombatLotrIssueQuest : QuestBase; variant DefeatRaids/CaptureLords/WinTournaments)
- Main/Features/LotrIssues/Templates/DeliverPersonnelLotrIssue.cs  (bandit-prisoner delivery)
Dispatch + save + config:
- Main/Features/LotrIssues/LotrIssuesCampaignBehavior.cs  (TemplateType + CreateIssue switches; RelatedObject dispatch)
- Main/Features/LotrIssues/LotrIssueSaveableTypeDefiner.cs  (base 726900801, localIds 101-106)
- Main/Features/LotrIssues/Domain/LotrIssueDefinition.cs  (Variant field)
- Main/_Module/ModuleData/lotr_issues/taom_lotr_issues.xml  (43 configs)
Localization:
- Main/_Module/ModuleData/taom_lotr_issue_strings.xml  (308 keys)
- Main/_Module/SubModule.xml  (the new taom_lotr_issue_strings GameText node)
Context (Wave 0, already reviewed — read only if a finding requires it):
- Main/Features/LotrIssues/Templates/DeliverGoodsLotrIssue.cs, LotrIssueService.cs, LotrIssueSuppression.cs, Main/Adapters/LotrIssue*Adapter.cs
Tests:
- TAOM.Tests/Features/LotrIssues/*

## KNOWN SUSPECTS — CONFIRM or DISPUTE each

1. CombatLotrIssueQuest auto-completes by calling Success() -> CompleteQuestWithSuccess() directly inside the CampaignEvent handlers (OnPlayerBattleEnd / OnHeroPrisonerTaken / OnTournamentFinished). Is this re-entrancy-safe in v1.4.6? Can completing a quest from inside an MbEvent dispatch corrupt the listener list or double-fire? Is the `if (!IsOngoing) return;` guard in Bump() sufficient?

2. The 3-way event routing in RegisterEvents: CaptureLords -> HeroPrisonerTaken, WinTournaments -> TournamentFinished, else (DefeatRaids) -> OnPlayerBattleEnd. Confirm exactly ONE count-source is subscribed per variant (no double-count), and that WarDeclared + OnClanChangedKingdom cancellation hooks fire for all variants without interfering.

3. OnPlayerBattleEnd counts a won battle via mapEvent.WinningSide == mapEvent.PlayerSide. Does this over-count (e.g., fire for sieges, hideouts, or simulated battles the player didn't actually fight) or under-count? Compare to how a vanilla "win N battles" quest filters MapEvents in 1.4.6.

4. OnHeroPrisonerTaken (CaptureLords) requires capturer == PartyBase.MainParty && prisoner.IsLord && prisoner.MapFaction.IsAtWarWith(playerFaction). Could a lord captured by an allied/army party, or a non-combatant lord, mis-count or NRE? Is prisoner.MapFaction ever null mid-capture?

5. OnTournamentFinished (WinTournaments) bumps when winner == CharacterObject.PlayerCharacter. Verify the TournamentFinished delegate signature (CharacterObject, MBReadOnlyList<CharacterObject>, Town, ItemObject) and that the player winning is correctly detected.

6. DeliverPersonnelLotrIssue counts/removes bandit prisoners from PartyBase.MainParty.PrisonRoster keyed on CharacterObject.Occupation == Occupation.Bandit. Is that the correct way to identify bandit prisoners in 1.4.6? Does removal use the modifier-preserving roster API? Can the turn-in remove prisoners that were freed/escaped/recruited between accept and turn-in (stale-count bug like Wave-0 M2)?

7. SaveableTypeDefiner: base 726900801 + localIds 101-106 -> derived ids. Confirm no collision with CareerQuest (726900701/726900802) or any other TAOM definer, and that all 6 localIds are unique.

8. CombatLotrIssueQuest [SaveableField] set (_defId, _neededCount, _rewardGold, _difficulty, _variant, _progress, _log) — is any mutable runtime field used after load but NOT saved (would reset to default / NRE on load)? Is _def correctly NON-saveable + re-resolved via EnsureDef? Same audit for DeliverPersonnelLotrIssueQuest.

9. PER-TYPE SATURATION (we believe this is real + accepted): all 27 Combat configs share typeof(CombatLotrIssue) and all 14 Deliver share typeof(DeliverGoodsLotrIssue). Does the v1.4.6 IssueManager / IssuesCampaignBehavior over-representation + cooldown logic key on the issue TYPE such that the 27 Combat variants compete for a single per-type slot? CONFIRM the mechanism and state the practical spawn-rate consequence. (We have documented this as an accepted v1 limitation; confirm we have it right, and flag if it is WORSE than "fewer simultaneous issues + rare variants surface infrequently" — e.g. if it can fully starve a variant or soft-lock the panel.)

10. Config integrity (taom_lotr_issues.xml, 43 rows): every DeliverGoods item_source is item:<id> with <id> a real engine item; every Combat variant is one of the 3 implemented; every giver_occupation/frequency/template parses; no duplicate id; no reward is zero-everything or sign-flipped. Flag any row that LotrIssueConfigProvider would silently skip at load.

11. Localization: every {=KEY} referenced in taom_lotr_issues.xml AND in the 3 template .cs files has a matching <string id> in taom_lotr_issue_strings.xml. Any missing key renders the inline default (acceptable) — but flag any key referenced with NO inline default anywhere (would render the raw {=KEY}).

## REQUIRED OUTPUT SECTIONS

- VANILLA CODE: paste the v1.4.6 decompiled signatures/bodies you relied on (IssueManager over-representation + cooldown, the MapEvent win-detection a vanilla quest uses, TournamentFinished, the QuestBase completion path) as evidence for suspects 1/3/5/9.
- KNOWN SUSPECTS VERDICTS: CONFIRMED / DISPUTED + evidence for each of the 11.
- FINDINGS: any NEW bugs not in the suspect list, with severity (HIGH/MED/LOW), file:line, and the fix.
- CONFIG CROSS-REFERENCE: result of suspect 10.

## QUALITY GATES

- Decompile the actual v1.4.6 installed DLLs for every engine claim; do not assert from memory.
- A finding is a hypothesis — show the code that proves it.
- TAOM custom cultures use LOTR-name StringIds; vanilla-engine cultures use Calradic ids (see cheatsheet). Do not flag culture="" (means "all cultures") as a bug.
- These quests are issue-attached (created via IssueBase.GenerateIssueQuest), so leaving SpecialQuestType empty is CORRECT (not a bug) — they survive QuestManager.OnGameLoaded via the issue-link branch.
- The IssueBase/QuestBase templates are TaleWorlds-constructed entry points; IoC.Resolve + direct TaleWorlds types in them are allowed (ADR boundary). Only flag sealed types leaking into the pure LotrIssueService/ConfigProvider.

Output your full review below.
