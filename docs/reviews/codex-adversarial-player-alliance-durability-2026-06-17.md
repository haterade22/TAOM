# Codex Adversarial Review - PlayerAllianceDurability - 2026-06-17

Scope: TAOM PlayerAllianceDurability follow-up for Bannerlord v1.4.6. Vanilla evidence below was decompiled from the installed v1.4.6 DLLs via `tools/taom-src.ps1` / `ilspycmd`, not from `E:\Decompiled_Bannerlord`.

## VANILLA CODE

### `DeclareWarAction.ApplyInternal` and public funnel

Source: `C:\Users\mikew\.taom-src\v1.4.6\TaleWorlds.CampaignSystem.Actions.DeclareWarAction.cs`

```csharp
private static void ApplyInternal(IFaction faction1, IFaction faction2, DeclareWarDetail declareWarDetail)
{
    FactionManager.DeclareWar(faction1, faction2);
    if (faction1.IsKingdomFaction && (float)faction2.Fiefs.Count > 1f + (float)faction1.Fiefs.Count * 0.2f)
    {
        Kingdom kingdom = (Kingdom)faction1;
        kingdom.PoliticalStagnation = (int)((float)kingdom.PoliticalStagnation * 0.85f - 3f);
        if (kingdom.PoliticalStagnation < 0)
        {
            kingdom.PoliticalStagnation = 0;
        }
    }
    if (faction2.IsKingdomFaction && (float)faction1.Fiefs.Count > 1f + (float)faction2.Fiefs.Count * 0.2f)
    {
        Kingdom kingdom2 = (Kingdom)faction2;
        kingdom2.PoliticalStagnation = (int)((float)kingdom2.PoliticalStagnation * 0.85f - 3f);
        if (kingdom2.PoliticalStagnation < 0)
        {
            kingdom2.PoliticalStagnation = 0;
        }
    }
    if (faction1 == Hero.MainHero.MapFaction || faction2 == Hero.MainHero.MapFaction)
    {
        IFaction dirtySide = ((faction1 == Hero.MainHero.MapFaction) ? faction2 : faction1);
        foreach (Settlement item in Settlement.All.Where((Settlement party) => party.IsVisible && party.MapFaction == dirtySide))
        {
            item.Party.SetVisualAsDirty();
        }
        foreach (MobileParty item2 in MobileParty.All.Where((MobileParty party) => party.IsVisible && party.MapFaction == dirtySide))
        {
            item2.Party.SetVisualAsDirty();
        }
    }
    CampaignEventDispatcher.Instance.OnWarDeclared(faction1, faction2, declareWarDetail);
}

public static void ApplyByKingdomDecision(IFaction faction1, IFaction faction2)
{
    ApplyInternal(faction1, faction2, DeclareWarDetail.CausedByKingdomDecision);
}

public static void ApplyByCallToWarAgreement(IFaction faction1, IFaction faction2)
{
    ApplyInternal(faction1, faction2, DeclareWarDetail.CausedByCallToWarAgreement);
}
```

All eight public methods in the type call `ApplyInternal`: `ApplyByKingdomDecision`, `ApplyByDefault`, `ApplyByPlayerHostility`, `ApplyByRebellion`, `ApplyByCrimeRatingChange`, `ApplyByKingdomCreation`, `ApplyByClaimOnThrone`, `ApplyByCallToWarAgreement`.

### `AllianceCampaignBehavior.RegisterEvents`, `StartAlliance`, `EndAlliance`, `OnWarDeclared`

Source: `C:\Users\mikew\.taom-src\v1.4.6\TaleWorlds.CampaignSystem.CampaignBehaviors.AllianceCampaignBehavior.cs`

```csharp
public override void RegisterEvents()
{
    CampaignEvents.DailyTickClanEvent.AddNonSerializedListener(this, DailyTickClan);
    CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
    CampaignEvents.MakePeace.AddNonSerializedListener(this, OnMakePeace);
    CampaignEvents.KingdomDestroyedEvent.AddNonSerializedListener(this, OnKingdomDestroyed);
    CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
    CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
}

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
    ...
}

public void EndAlliance(Kingdom kingdom1, Kingdom kingdom2)
{
    foreach (CallToWarAgreement callToWarAgreement in GetCallToWarAgreements(kingdom1, kingdom2))
    {
        EndCallToWarAgreement(callToWarAgreement.CallingKingdom, callToWarAgreement.CalledKingdom, callToWarAgreement.KingdomToCallToWarAgainst);
    }
    RemoveAlliance(kingdom1, kingdom2);
    CampaignEventDispatcher.Instance.OnAllianceEnded(kingdom1, kingdom2);
}

private void OnWarDeclared(IFaction faction1, IFaction faction2, DeclareWarAction.DeclareWarDetail detail)
{
    if (!faction1.IsKingdomFaction || !faction2.IsKingdomFaction)
    {
        return;
    }
    Kingdom kingdom = (Kingdom)faction1;
    Kingdom kingdom2 = (Kingdom)faction2;
    if (kingdom.IsAllyWith(kingdom2))
    {
        ApplyBrokenAlliancePenalty(kingdom, kingdom2, detail);
        EndAlliance(kingdom, kingdom2);
    }
    foreach (Kingdom item in kingdom.AlliedKingdoms.ToList())
    {
        if (!item.IsAtWarWith(kingdom2))
        {
            if (kingdom == Clan.PlayerClan.Kingdom && !Hero.MainHero.Clan.IsUnderMercenaryService)
            {
                OnCallToWarAgreementProposedByPlayerKingdom(item, kingdom2);
                continue;
            }
            ProposeCallToWarAgreementDecision kingdomDecision = new ProposeCallToWarAgreementDecision(kingdom.RulingClan, item, kingdom2);
            kingdom.AddDecision(kingdomDecision, ignoreInfluenceCost: true);
        }
    }
    ...
}
```

### `AllianceCampaignBehavior.StartCallToWarAgreement`

Source: same v1.4.6 decompile.

```csharp
public void StartCallToWarAgreement(Kingdom callingKingdom, Kingdom calledKingdom, Kingdom kingdomToCallToWarAgainst, int callToWarCost, bool isPlayerPaying = false)
{
    if (IsAllyWithKingdom(callingKingdom, calledKingdom) && !calledKingdom.IsAtWarWith(kingdomToCallToWarAgainst))
    {
        UpdateAllianceEndTime(callingKingdom, calledKingdom, AddCallToWarAgreement(callingKingdom, calledKingdom, kingdomToCallToWarAgainst).EndTime);
        if (isPlayerPaying)
        {
            Hero.MainHero.ChangeHeroGold(-callToWarCost);
            calledKingdom.CallToWarWallet += callToWarCost;
        }
        else
        {
            callingKingdom.CallToWarWallet -= callToWarCost;
            calledKingdom.CallToWarWallet += callToWarCost;
        }
        CampaignEventDispatcher.Instance.OnCallToWarAgreementStarted(callingKingdom, calledKingdom, kingdomToCallToWarAgainst);
        ApplyAcceptingCallToWarOfferBonus(callingKingdom, calledKingdom);
        DeclareWarAction.ApplyByCallToWarAgreement(calledKingdom, kingdomToCallToWarAgainst);
    }
}
```

## KNOWN SUSPECTS

1. **CHOKEPOINT COMPLETENESS - CONFIRMED with load-time caveat.**

   `DeclareWarAction` is a complete runtime `WarDeclared` chokepoint: every public `ApplyBy*` method funnels through `ApplyInternal`, and `CampaignEventDispatcher.Instance.OnWarDeclared(...)` is only called there. A repository-wide decompile search found `CampaignEventDispatcher.Instance.OnWarDeclared` only in `DeclareWarAction.cs:54`.

   Caveat: `FactionManager.DeclareWar` is called directly from XML deserialization in `Clan.Deserialize` and `Kingdom.Deserialize` and `FactionManager.AfterLoad` can set old-save constant-war stances directly. Those paths set stance without firing `WarDeclared`; they are not runtime declaration flows and do not call `AllianceCampaignBehavior.OnWarDeclared`, so they do not bypass this durability fix to auto-end the alliance. `FactionManager.DeclareWar` itself only calls `SetStance(..., StanceType.War)` and does not dispatch the event.

2. **PLAYER WAR-ON-ALLY TRAP - DISPUTED; real soft-lock risk.**

   The claim that the player can still manually break a Neutral player alliance is not supported by the installed v1.4.6 decompile. A decompiled-cache search found no `EndAlliance(...)` call outside `AllianceCampaignBehavior` itself. The stock Kingdom diplomacy VM exposes alliance proposal and war proposal actions, but no break-alliance action that calls `IAllianceCampaignBehavior.EndAlliance` directly. `DeclareWarDecision.ApplyChosenOutcome` calls `DeclareWarAction.ApplyByKingdomDecision(...)`; TAOM now blocks that path through `DiplomacyService.IsWarAllowed` / `DeclareWarAction_ApplyInternal_Patch`.

   Result: after forming a Neutral player alliance, the normal player-facing war button is blocked by `TaomKingdomDecisionPermissionModel.IsWarDecisionAllowedBetweenKingdoms` and the lower-level Harmony prefix also blocks the decision if it reaches `ApplyInternal`. Without a custom break-alliance UI/action, the player appears unable to exit the alliance except by waiting for vanilla expiry (TAOM sets `MaxDurationOfAlliance` to 100 years), kingdom destruction, or another non-UI path.

   Finding #1.

3. **CALL-TO-WAR INTERPLAY - CONFIRMED as inconsistent-state bug.**

   Vanilla `StartCallToWarAgreement` commits side effects before the war declaration: it adds/extends the call-to-war agreement, transfers wallet gold, dispatches `OnCallToWarAgreementStarted`, applies relation/acceptance bonuses, and only then calls `DeclareWarAction.ApplyByCallToWarAgreement(calledKingdom, kingdomToCallToWarAgainst)`. TAOM's prefix blocks at `DeclareWarAction.ApplyInternal`, after those side effects.

   Scenario: player rules kingdom A; A is allied with B and also allied with C; B is at war with C and calls A to war against C. The call-to-war accept path reaches `StartCallToWarAgreement(B, A, C, ...)`. `DiplomacyService.IsWarAllowed(A, C)` returns false because A is the player-ruled kingdom and A/C are allied. The war is blocked, but the call-to-war agreement, gold movement, event, and bonuses have already happened. That is not a clean "prevent being dragged to war"; it is a partial success state with no war.

   Finding #2.

4. **POSTFIX PARAM-NAME BINDING - CONFIRMED clean.**

   Decompile confirms `AllianceCampaignBehavior.StartAlliance(Kingdom proposerKingdom, Kingdom receiverKingdom)` and `EndAlliance(Kingdom kingdom1, Kingdom kingdom2)`. TAOM's diagnostic postfix/prefix names match exactly at `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_StartAlliance_Patch.cs:28` and `Main/Features/Diplomacy/Hooks/AllianceCampaignBehavior_EndAlliance_Patch.cs:26`.

5. **DIAGNOSTIC COST + GATING - CONFIRMED acceptable.**

   The expensive `AreAllied(...)` scan is short-circuited behind `playerKingdomId != null && (kingdomAId == playerKingdomId || kingdomBId == playerKingdomId)` at `Main/Features/Diplomacy/DiplomacyService.cs:126-129`, so it does not run for AI-vs-AI pairs. The diagnostic interpolation is inside the blocking branch at `DiplomacyService.cs:130-132`, so it allocates only when the protective block actually fires. `AllianceAdapter.GetPlayerRuledKingdomId` is property reads only (`Clan.PlayerClan`, `clan?.Kingdom`, `kingdom.RulingClan`, `kingdom.StringId`) at `Main/Adapters/AllianceAdapter.cs:22-26`; decompile confirms `Clan.Kingdom` returns `_kingdom`, `Kingdom.RulingClan` returns `_rulingClan`, and `MBObjectBase.StringId` is an auto-property.

6. **REGRESSION - CONFIRMED for direct `IsWarAllowed` behavior, but tests miss the two integration failures above.**

   The code still returns false for Permanent tier before the new branch (`DiplomacyService.cs:115-116`) and false for same-alignment pairs before the new branch (`DiplomacyService.cs:117-118`). For AI-vs-AI pairs, vassal/mercenary players (`GetPlayerRuledKingdomId() == null`), and player-ruled but not allied pairs, the new branch does not block. The four new service tests at `TAOM.Tests/Features/Diplomacy/DiplomacyServiceTests.cs:208-252` pin those direct return values.

   However, those tests do not cover the vanilla integration surfaces that matter for findings #1 and #2: there is no test proving a player can break a Neutral alliance through a direct `EndAlliance` path, and no test for call-to-war side effects when the later war declaration is blocked.

7. **GetPlayerRuledKingdomId CORRECTNESS - CONFIRMED current behavior; LOW drift risk.**

   `AllianceAdapter.GetPlayerRuledKingdomId` returns the kingdom id only when `Clan.PlayerClan?.Kingdom` exists and `kingdom.RulingClan == Clan.PlayerClan` (`Main/Adapters/AllianceAdapter.cs:22-26`). That is correct for a player-founded/player-ruled kingdom and returns null for a vassal/mercenary player's AI-ruled liege. Decompile confirms the getter chain is field-backed except `Clan.PlayerClan => Campaign.Current.PlayerDefaultFaction`; in the campaign-only call sites under review, that is acceptable. If a future campaign-death/inheritance path changes the player clan leader, the clan-level check still tracks the player-controlled clan rather than requiring `Hero.MainHero` to be leader, which is the right predicate for this feature.

   The current implementation duplicates the same predicate in `PlayerKingdomHelper.GetPlayerRuledKingdom` (`Main/Features/Diplomacy/PlayerKingdomHelper.cs:18-22`), creating a maintainability risk after review 54's exact helper-drift bug. Finding #3.

## ADDITIONAL FINDINGS

[HIGH] Main/Features/Diplomacy/DiplomacyService.cs:126 — PlayerAllianceDurability — Blocking war on a current player ally removes the stock v1.4.6 player-facing exit from a Neutral alliance; the installed decompile shows no direct break-alliance action outside `AllianceCampaignBehavior` internals, while the normal war decision goes through `DeclareWarAction.ApplyByKingdomDecision` and is now blocked — Fix: add an explicit player break-alliance action/UI path that calls `EndAlliance` for player-ruled Neutral alliances, or distinguish player-intended break/war from involuntary war and call `EndAlliance` before allowing the war.

[HIGH] Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs:43 — Call-to-war atomicity — Blocking `ApplyByCallToWarAgreement` at `ApplyInternal` happens after vanilla `StartCallToWarAgreement` has already added the agreement, moved gold, fired `OnCallToWarAgreementStarted`, and applied bonuses; accepting a call to war against another current player ally can leave a paid call-to-war agreement with no war — Fix: add a `Patch11_Diplomacy` prefix/preflight on `AllianceCampaignBehavior.StartCallToWarAgreement` that rejects the call before side effects when `IsWarAllowed(calledKingdom, kingdomToCallToWarAgainst)` would be false, and show/log a clear denial.

[LOW] Main/Adapters/AllianceAdapter.cs:22 — Predicate single-source — The player-ruled-kingdom predicate is now implemented both in `AllianceAdapter.GetPlayerRuledKingdomId` and `PlayerKingdomHelper.GetPlayerRuledKingdom`; they are identical today, but this is the same drift shape that caused the previous vassal/mercenary bypass bug — Fix: route both call sites through one shared helper/resolver, or add a parity test that pins vassal vs ruler behavior for both paths.

## SEVERITY TABLE

| # | Severity | Finding | File:line | Fix |
|---|----------|---------|-----------|-----|
| 1 | HIGH | Player cannot normally break a Neutral player alliance once war-on-ally is blocked | `Main/Features/Diplomacy/DiplomacyService.cs:126` | Add direct break-alliance path or distinguish voluntary break/war from involuntary war and call `EndAlliance` before allowing war |
| 2 | HIGH | Call-to-war side effects commit before the now-blocked war declaration | `Main/Features/Diplomacy/Hooks/DeclareWarAction_ApplyInternal_Patch.cs:43` | Preflight/block `StartCallToWarAgreement` before `AddCallToWarAgreement`, wallet changes, events, and bonuses |
| 3 | LOW | Player-ruled predicate duplicated across adapter and helper | `Main/Adapters/AllianceAdapter.cs:22` | Use one shared helper/resolver or add parity coverage |

## TESTS

Attempted: `dotnet test TAOM.Tests\TAOM.Tests.csproj --filter DiplomacyServiceTests --no-restore`.

Result: not run. The sandboxed `dotnet` CLI failed during first-run setup with unauthorized writes to the toolpath/sentinel directories, even with `DOTNET_CLI_HOME=C:\tmp\dotnet-cli-home`.

## SUMMARY

CRITICAL: 0 | HIGH: 2 | MEDIUM: 0 | LOW: 1

VERDICT: ISSUES FOUND
