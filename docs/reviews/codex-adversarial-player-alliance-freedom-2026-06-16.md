# Codex Adversarial Review - PlayerAllianceFreedom - 2026-06-16

Scope: uncommitted PlayerAllianceFreedom changes in `Main/Features/Diplomacy`, `AllianceAdapter`, `SubModule`, `taom_module_strings.xml`, and `DiplomacyServiceTests`.

Vanilla source was decompiled from the installed Bannerlord v1.4.6 DLLs via `tools/taom-src.ps1` / direct `ilspycmd` against `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\TaleWorlds.CampaignSystem.dll`. I did not use `E:\Decompiled_Bannerlord`.

## VANILLA CODE

### `DefaultAllianceModel.CanMakeAlliance` (v1.4.6)

```csharp
public override bool CanMakeAlliance(Kingdom kingdom, Kingdom targetKingdom, IFaction evaluatingFaction, out TextObject reason, bool includeReason = false)
{
    reason = (includeReason ? _allianceNotFormedExplanationText : null);
    if (targetKingdom.IsEliminated || kingdom.IsEliminated)
    {
        if (includeReason)
            reason.SetTextVariable("REASON", new TextObject("{=a5EAl1aW}That realm has been eliminated."));
        return false;
    }
    if (targetKingdom == kingdom)
    {
        if (includeReason)
            reason.SetTextVariable("REASON", new TextObject("{=zPoS5fIu}You are referring to your own realm."));
        return false;
    }
    if (targetKingdom.IsAtWarWith(kingdom))
    {
        if (includeReason)
        {
            TextObject textObject = new TextObject("{=lseJ70y0}Your realm is at war with the {KINGDOM_NAME}.");
            textObject.SetTextVariable("KINGDOM_NAME", targetKingdom.Name);
            reason.SetTextVariable("REASON", textObject);
        }
        return false;
    }
    if (kingdom.AlliedKingdoms.Count >= Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances)
        return false;
    if (targetKingdom.AlliedKingdoms.Count >= Campaign.Current.Models.AllianceModel.MaxNumberOfAlliances)
        return false;
    if (targetKingdom.IsAllyWith(kingdom))
        return false;
    if (kingdom == Clan.PlayerClan.Kingdom)
    {
        if (Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(targetKingdom, kingdom, out reason, includeReason).ResultNumber < 50f)
            return false;
        if (evaluatingFaction != Clan.PlayerClan && evaluatingFaction is Clan evaluatingClan && !CanMakeAllianceWithPlayerSupport(kingdom, targetKingdom, evaluatingClan))
            return false;
    }
    else
    {
        if (Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(kingdom, targetKingdom, out reason, includeReason).ResultNumber < 50f)
            return false;
        if (Clan.PlayerClan?.Kingdom != null && Clan.PlayerClan.Kingdom == targetKingdom)
        {
            if (!CanMakeAllianceWithPlayerSupport(targetKingdom, kingdom, Campaign.Current.Models.AllianceModel.GetProposerClanForAllianceDecision(targetKingdom, kingdom)))
                return false;
        }
        else if (Campaign.Current.Models.AllianceModel.GetScoreOfStartingAlliance(targetKingdom, kingdom, out reason, includeReason).ResultNumber < 50f)
            return false;
    }
    return true;
}
```

Supporting vanilla lines used for score/support analysis:

```csharp
public override int MaxNumberOfAlliances => 2;
public override CampaignTime MaxDurationOfAlliance => CampaignTime.Days(84f);

private bool CanMakeAllianceWithPlayerSupport(Kingdom proposingKingdom, Kingdom proposedKingdom, Clan evaluatingClan)
{
    if (proposingKingdom == Clan.PlayerClan.Kingdom && evaluatingClan == Clan.PlayerClan)
        return true;
    KingdomElection kingdomElection = new KingdomElection(new StartAllianceDecision(evaluatingClan, proposedKingdom));
    DecisionOutcome supportedOutcome = kingdomElection.PossibleOutcomes.FirstOrDefault((DecisionOutcome x) => x is StartAllianceDecision.StartAllianceDecisionOutcome startAllianceDecisionOutcome && startAllianceDecisionOutcome.ShouldAllianceBeStarted);
    kingdomElection.SetupResultWithoutPlayerSupport();
    return kingdomElection.GetWinChanceWithPlayerSupport(supportedOutcome, Supporter.SupportWeights.FullyPush) > 0.5f;
}
```

### `StartAllianceDecision` (v1.4.6)

```csharp
public override bool IsAllowed()
{
    TextObject reason;
    return Campaign.Current.Models.KingdomDecisionPermissionModel.IsStartAllianceDecisionAllowedBetweenKingdoms(base.Kingdom, KingdomToStartAllianceWith, out reason);
}

public override void ApplyChosenOutcome(DecisionOutcome chosenOutcome)
{
    if (((StartAllianceDecisionOutcome)chosenOutcome).ShouldAllianceBeStarted)
    {
        AllianceCampaignBehavior.StartAlliance(base.Kingdom, KingdomToStartAllianceWith);
    }
}

public override bool CanMakeDecision(out TextObject reason, bool includeReason = false)
{
    return Campaign.Current.Models.AllianceModel.CanMakeAlliance(base.Kingdom, KingdomToStartAllianceWith, base.ProposerClan, out reason, includeReason);
}
```

### `AllianceCampaignBehavior.StartAlliance` (v1.4.6)

```csharp
public void StartAlliance(Kingdom proposerKingdom, Kingdom receiverKingdom)
{
    if (IsAllyWithKingdom(proposerKingdom, receiverKingdom))
    {
        return;
    }
    StanceLink stanceWith = proposerKingdom.GetStanceWith(receiverKingdom);
    if (stanceWith.GetDailyTributeToPay(proposerKingdom) != 0)
    {
        stanceWith.SetDailyTributePaid(proposerKingdom, 0, 0);
    }
    if (stanceWith.GetDailyTributeToPay(proposerKingdom) != 0)
    {
        stanceWith.SetDailyTributePaid(proposerKingdom, 0, 0);
    }
    AddAlliance(proposerKingdom, receiverKingdom);
    CampaignEventDispatcher.Instance.OnAllianceStarted(proposerKingdom, receiverKingdom);
    foreach (IFaction item in proposerKingdom.FactionsAtWarWith.WhereQ((IFaction f) => f.IsKingdomFaction && !f.IsAtWarWith(receiverKingdom)).ToList())
    {
        if (proposerKingdom == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
        {
            OnCallToWarAgreementProposedByPlayerKingdom(receiverKingdom, (Kingdom)item);
            continue;
        }
        ProposeCallToWarAgreementDecision kingdomDecision = new ProposeCallToWarAgreementDecision(proposerKingdom.RulingClan, receiverKingdom, (Kingdom)item);
        proposerKingdom.AddDecision(kingdomDecision, ignoreInfluenceCost: true);
    }
    foreach (IFaction item2 in receiverKingdom.FactionsAtWarWith.WhereQ((IFaction f) => f.IsKingdomFaction && !f.IsAtWarWith(proposerKingdom)).ToList())
    {
        if (receiverKingdom == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
        {
            OnCallToWarAgreementProposedByPlayerKingdom(proposerKingdom, (Kingdom)item2);
            continue;
        }
        ProposeCallToWarAgreementDecision kingdomDecision2 = new ProposeCallToWarAgreementDecision(receiverKingdom.RulingClan, proposerKingdom, (Kingdom)item2);
        receiverKingdom.AddDecision(kingdomDecision2, ignoreInfluenceCost: true);
    }
}
```

Supporting invariant code:

```csharp
public bool IsAllyWithKingdom(Kingdom kingdom1, Kingdom kingdom2)
{
    if (kingdom1 == null || kingdom2 == null || kingdom1 == kingdom2 || kingdom1.IsEliminated || kingdom2.IsEliminated)
        return false;
    Alliance foundAlliance;
    return TryGetAlliance(kingdom1, kingdom2, out foundAlliance);
}

private Alliance AddAlliance(Kingdom kingdom1, Kingdom kingdom2)
{
    Alliance alliance = new Alliance(kingdom1, kingdom2, CampaignTime.Now + Campaign.Current.Models.AllianceModel.MaxDurationOfAlliance);
    _alliances.Add(alliance);
    kingdom1.UpdateAlliedKingdoms();
    kingdom2.UpdateAlliedKingdoms();
    return alliance;
}
```

## KNOWN SUSPECTS

1. SCORE COMPLETENESS - DISPUTED for a player-ruled kingdom; related scope bug confirmed below.

`TaomAllianceModel.GetScoreOfStartingAlliance` adds the service modifier after the base score at `Main/Features/Diplomacy/Models/TaomAllianceModel.cs:26-39`; `DiplomacyService.GetAllianceScoreModifier(..., involvesPlayer: true)` returns exactly `+1000f` at `Main/Features/Diplomacy/DiplomacyService.cs:59-62`. Vanilla `CanMakeAlliance` uses the `50f` threshold in both player-initiate and AI-offer directions. For AI offers to the player, the later `CanMakeAllianceWithPlayerSupport` gate is also cleared: if the proposer for the player kingdom is `Clan.PlayerClan`, vanilla returns true immediately; otherwise the election path uses the same `GetSupportScoreOfStartingAllianceForClan`, so the +1000 score drives positive support and `GetWinChanceWithPlayerSupport(..., FullyPush)` adds player support.

The +1000 does not and should not bypass eliminated/same-kingdom/at-war/already-allied structural gates. The max-alliance gate is bypassed only because TAOM already overrides `MaxNumberOfAlliances` to `int.MaxValue`.

Base score cannot plausibly fall below -950 in v1.4.6. The negative helper terms are bounded at about -132.4 before positives: alliance penalties -96 max, relation -8, at-war-with-ally -8, at-war-or-peace -0.4, same-culture-fief -16, honor -4; the threat term is positive when the detailed scoring block runs.

2. MAXNUMBEROFALLIANCES SCOPE - CONFIRMED observation, pre-existing.

`TaomAllianceModel.MaxNumberOfAlliances => int.MaxValue` at `Main/Features/Diplomacy/Models/TaomAllianceModel.cs:16` is model-global, while vanilla is `2`. That means all kingdoms, not only the player, can exceed the vanilla cap. This line is not in the current diff, so this feature does not introduce or worsen the AI-vs-AI cap behavior; it only relies on the pre-existing global override to avoid the cap gate.

3. INVOLVESPLAYER SYMMETRY - CONFIRMED issue.

The helper is null-safe and order-independent: both model copies read `Clan.PlayerClan?.Kingdom` and compare either argument at `Main/Features/Diplomacy/Models/TaomAllianceModel.cs:45-49` and `Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs:33-37`.

The problem is scope: it treats a vassal or mercenary player's liege/employer as "player-involved" even when the player does not rule that kingdom. Vanilla `Clan.Kingdom` is a normal membership property and `Clan.IsUnderMercenaryService` is separate, so this can grant the full-freedom score/permission bypass to AI-controlled diplomacy. See HIGH finding #1.

4. DIALOG vs BUTTON COST ASYMMETRY - DISPUTED as a state-invariant issue.

The direct dialog path calls `_service.FormPlayerAlliance(...)` at `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:90`, which delegates to `IAllianceAdapter.StartAlliance` at `Main/Features/Diplomacy/DiplomacyService.cs:98-104` and `Main/Adapters/AllianceAdapter.cs:31-38`. Vanilla `StartAllianceDecision.ApplyChosenOutcome` calls the same `AllianceCampaignBehavior.StartAlliance`, so the direct path does not skip alliance end-time creation, both-kingdom `UpdateAlliedKingdoms`, or `OnAllianceStarted`.

The 0-influence/no-vote asymmetry is real and documented as intentional in `docs/features/diplomacy.md:35`. A pending vanilla `StartAllianceDecision` for the same pair should not double-apply: `StartAlliance` returns if already allied, and `StartAllianceDecision.CanMakeDecision` re-enters `CanMakeAlliance`, whose already-allied gate returns false.

5. DIALOG GATING - PARTIALLY CONFIRMED.

The player side correctly excludes a vassal player because `GetPlayerLedKingdom` requires `kingdom.RulingClan == clan` at `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:100-105`. The target side is null-safe for clanless/kingdomless heroes and excludes non-ruling clans at `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:109-115`. Own kingdom is excluded at `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:77-78`.

Bug: the target helper checks only that the conversation hero's clan is the ruling clan. It does not require the conversation hero to be the kingdom leader or clan leader, so any talkable non-ruler member of the ruling clan can accept an alliance. See MEDIUM finding #2.

`hero_main_options` is an existing TAOM extension point: CareerSwitch and Messengers both register player lines under the same token with distinct line IDs. I found no line-id collision with the new `taom_alliance_*` dialog IDs.

6. FORMPLAYERALLIANCE GUARD - PARTIALLY CONFIRMED.

`FormPlayerAlliance` rechecks `CanPlayerProposeAlliance` before starting the alliance at `Main/Features/Diplomacy/DiplomacyService.cs:98-104`, and `CanPlayerProposeAlliance` preserves the intended structural at-war/already-allied gates at `Main/Features/Diplomacy/DiplomacyService.cs:85-95`. It intentionally does not check lore Hostile.

The service/adapter still have a silent-failure shape: invalid IDs make `AllianceAdapter.StartAlliance` no-op at `Main/Adapters/AllianceAdapter.cs:31-38`, and the dialog displays the success message unconditionally after the void service call at `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:90-95`. See LOW finding #3.

7. REGRESSION - DISPUTED for the service overloads.

The retained 2-arg `GetAllianceScoreModifier` forwards to `involvesPlayer:false` at `Main/Features/Diplomacy/DiplomacyService.cs:54-57`. The false branch at `Main/Features/Diplomacy/DiplomacyService.cs:64-71` is the pre-feature tier switch: Permanent +1000, Natural +500, Hostile -10000, Neutral 0. `IsAllianceDecisionAllowed(..., false)` returns `IsAllianceAllowed(...)` at `Main/Features/Diplomacy/DiplomacyService.cs:79-82`, matching the old Hostile block. Existing callers of the 2-arg form remain on the old behavior.

Separate from the overload regression question, the model-side `involvesPlayer` computation is too broad for vassal/mercenary playthroughs; that is finding #1.

8. STRING KEYS - DISPUTED; the in-session fix is complete.

The old harvested key remains at `Main/_Module/ModuleData/taom_module_strings.xml:371` as `taom_alliance_formed`. The new dialog notification uses `taom_player_alliance_formed` in C# at `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:92` and XML at `Main/_Module/ModuleData/taom_module_strings.xml:820`.

The four new source keys are present exactly once: `taom_alliance_player_freedom` at line 817, `taom_alliance_propose` at line 818, `taom_alliance_accept` at line 819, and `taom_player_alliance_formed` at line 820. A full duplicate-id scan of `taom_module_strings.xml` returned no duplicate `<string id>` entries. Every new C# `{=key}` use has a matching XML id, and the four new XML ids are all referenced by the new C#.

## ADDITIONAL FINDINGS

[HIGH] Main/Features/Diplomacy/Models/TaomAllianceModel.cs:45 and Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs:33 - Player-scope gate - The "player involved" bypass applies to any `Clan.PlayerClan.Kingdom`, not only a player-ruled kingdom. If the player is a vassal or mercenary, their liege/employer kingdom can get `+1000` and bypass the lore Hostile decision block, changing AI-controlled diplomacy despite the feature requirement that AI-vs-AI behavior remain unchanged. Fix: replace both duplicated helpers with a shared `InvolvesPlayerRuledKingdom` check that requires `var playerClan = Clan.PlayerClan; var playerKingdom = playerClan?.Kingdom; playerKingdom != null && playerKingdom.RulingClan == playerClan && (kingdom1 == playerKingdom || kingdom2 == playerKingdom)`. Add coverage for player-ruler vs vassal/mercenary cases.

[MEDIUM] Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:109 - Dialog target gate - `GetConversationRulerKingdom` accepts any member of the ruling clan, not the ruler. A non-ruler lord from the ruling clan can display the proposal line and directly forge a kingdom alliance. Fix: require `hero == kingdom.Leader` or `hero == clan.Leader` in addition to `kingdom.RulingClan == clan`; add behavior-level coverage for ruling-clan non-leader conversations.

[LOW] Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:90 - Dialog success reporting - The dialog always displays the forged-alliance message after calling a void `FormPlayerAlliance`, but the service can return early and the adapter can no-op on missing kingdoms/behavior. A state change between dialog condition and consequence can therefore show success without an alliance. Fix: make `FormPlayerAlliance` and `IAllianceAdapter.StartAlliance` return `bool`, reject null/eliminated kingdoms explicitly, verify `AreAllied` after the call, and display the message only when the alliance actually exists.

## SEVERITY TABLE

| # | Severity | Finding | File:line | Fix |
|---|----------|---------|-----------|-----|
| 1 | HIGH | Player-freedom bypass applies to vassal/mercenary liege kingdoms, changing AI-controlled diplomacy. | `Main/Features/Diplomacy/Models/TaomAllianceModel.cs:45`, `Main/Features/Diplomacy/Models/TaomKingdomDecisionPermissionModel.cs:33` | Require `Clan.PlayerClan` to be the kingdom's `RulingClan`; centralize the helper and test ruler vs non-ruler cases. |
| 2 | MEDIUM | Dialog target gate accepts any ruling-clan member, not only the ruler. | `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:109` | Require `hero == kingdom.Leader` or `hero == clan.Leader` before returning the target kingdom. |
| 3 | LOW | Dialog can show a success message after a no-op alliance attempt. | `Main/Features/Diplomacy/PlayerAllianceProposalBehavior.cs:90`, `Main/Features/Diplomacy/DiplomacyService.cs:98`, `Main/Adapters/AllianceAdapter.cs:31` | Return success/failure from service/adapter, log rejection reasons, and display success only after `AreAllied` is true. |

## TEST STATUS

Attempted: `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter FullyQualifiedName~DiplomacyServiceTests --no-restore --nologo`.

Result: not executed. The sandbox blocked MSBuild while probing `C:\Users\mikew\AppData\Local\Microsoft SDKs`. This is an environment permission failure, not a test failure.

## SUMMARY

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 1

VERDICT: ISSUES FOUND
