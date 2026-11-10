using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;
using TAOM.Features.CoopInterop;
using TAOM.Features.CulturalFeats;
using TAOM.Features.Diplomacy.Hooks;
using TAOM.Features.KingdomAllegiance;
using TAOM.Features.KingdomAllegiance.Models;
using TAOM.Features.Relations;

namespace TAOM.Features.Diplomacy.Models;

public class TaomDiplomacyModel : DefaultDiplomacyModel
{
    private readonly IWarOfTheRingService _wotrService;
    private readonly ICoopSessionProvider _coop;
    private readonly IOnAllianceAction _allianceHook;
    private readonly IPersonalRelationService _personalRelations;
    private readonly KingdomAllegianceService _allegiance;
    private readonly AllegianceFactsReader _allegianceFacts;

    public TaomDiplomacyModel(
        IWarOfTheRingService wotrService, ICoopSessionProvider coop, IOnAllianceAction allianceHook,
        IPersonalRelationService personalRelations, KingdomAllegianceService allegiance,
        AllegianceFactsReader allegianceFacts)
    {
        _wotrService = wotrService;
        _coop = coop;
        _allianceHook = allianceHook;
        _personalRelations = personalRelations;
        _allegiance = allegiance;
        _allegianceFacts = allegianceFacts;
    }

    /// <summary>
    /// Kingdom Allegiance (#1111): the clan's side of every AI join and defection barter (<c>JoinKingdomAsClanBarterable</c>, from
    /// <c>DiplomaticBartersBehavior</c>). An AI clan of the opposed side gets vanilla's own never-join value; anything else is vanilla.
    /// </summary>
    public override float GetScoreOfClanToJoinKingdom(Clan clan, Kingdom kingdom) =>
        _allegiance.JoinVeto(_allegianceFacts.Read(clan, kingdom)) ?? base.GetScoreOfClanToJoinKingdom(clan, kingdom);

    /// <summary>Kingdom Allegiance (#1111): the mercenary clan's side of <c>MercenaryJoinKingdomBarterable</c>, same rule.</summary>
    public override float GetScoreOfMercenaryToJoinKingdom(Clan mercenaryClan, Kingdom kingdom) =>
        _allegiance.JoinVeto(_allegianceFacts.Read(mercenaryClan, kingdom))
        ?? base.GetScoreOfMercenaryToJoinKingdom(mercenaryClan, kingdom);

    /// <summary>
    /// #370 — the THIRD site reading <c>ShouldBlockPeace</c>, and the one the review agent itself
    /// missed while reporting the other two. "Constantly at war" makes peace permanently
    /// unreachable between two factions, so a peer whose WotR phase had drifted would hold a
    /// different view of which wars can ever end. Same gate as the prefixes and the permission
    /// model: under co-op the host's diplomacy governs.
    /// </summary>
    public override bool IsAtConstantWar(IFaction faction1, IFaction faction2)
    {
        if (!_coop.ShouldDeferToHost
            && _wotrService.IsWarOfTheRingActive
            && _wotrService.ShouldBlockPeace(faction1.StringId, faction2.StringId))
        {
            return true;
        }

        return base.IsAtConstantWar(faction1, faction2);
    }

    /// <summary>
    /// The AI pays 200 influence per war proposal, and a proposal the permission model refuses is then
    /// cancelled, so a forbidden war must never score well enough to be proposed. The penalty is a
    /// constant per veto class (see <see cref="IWarOfTheRingService.GetDeclareWarScorePenalty"/>), so
    /// the order among equally vetoed neighbours survives, which the Mercenary start uses to pick its
    /// enemy. Kingdom against kingdom only, and off under co-op; the hook owns both conditions.
    /// </summary>
    public override float GetScoreOfDeclaringWar(
        IFaction factionDeclaresWar, IFaction factionDeclaredWar, Clan evaluatingClan,
        out TextObject reason, bool includeReason = false)
    {
        return base.GetScoreOfDeclaringWar(
                factionDeclaresWar, factionDeclaredWar, evaluatingClan, out reason, includeReason)
            - _allianceHook.GetDeclareWarScorePenalty(
                factionDeclaresWar.StringId, factionDeclaresWar.IsKingdomFaction,
                factionDeclaredWar.StringId, factionDeclaredWar.IsKingdomFaction);
    }

    /// <summary>
    /// Personal relations (#862). Vanilla maps each hero with a clan to his clan leader, for every pair, and every relation read
    /// (<c>GetEffectiveRelation</c>, <c>Hero.GetRelation</c>, <c>ChangeRelationAction</c>) goes through here, on the main
    /// thread. Base runs first, so every AI-to-AI pair stays vanilla. Then, when the service is active (one bool, read before
    /// anything else) and an effective hero is the player, the OTHER effective hero goes back to the hero it was asked about:
    /// (player, Denethor) from (companion, Faramir) becomes (player, Faramir). The service holds the decision as pure facts
    /// (<c>Resolve</c>); the two reference compares and the three ternaries here are the boundary conversion rule 4 allows.
    /// The player is <c>Hero.MainHero</c>, never <c>IsHumanPlayerCharacter</c>, which a co-op module turns into a party scan.
    /// </summary>
    public override void GetHeroesForEffectiveRelation(
        Hero hero1, Hero hero2, out Hero effectiveHero1, out Hero effectiveHero2)
    {
        base.GetHeroesForEffectiveRelation(hero1, hero2, out effectiveHero1, out effectiveHero2);
        var side = _personalRelations.IsActive
            ? _personalRelations.Resolve(effectiveHero1 == Hero.MainHero, effectiveHero2 == Hero.MainHero)
            : PersonalPairSide.None;
        effectiveHero1 = side == PersonalPairSide.UnmapFirst ? hero1 : effectiveHero1;
        effectiveHero2 = side == PersonalPairSide.UnmapSecond ? hero2 : effectiveHero2;
    }

    /// <summary>
    /// Personal relations (#862): when a clan's leader changes (death, a leader changing clan, a member elected king), vanilla's <c>ChangeClanLeaderAction</c> gives the heir his raw
    /// relation plus this value (0.7 of the dead leader's) for every living hero. While the service is active the player's share
    /// is 0, so an heir keeps the relation he earned himself.
    /// </summary>
    public override int GetRelationChangeAfterClanLeaderIsDead(Hero deadLeader, Hero relationHero) =>
        (_personalRelations.IsActive ? _personalRelations.InheritedRelationChange(relationHero == Hero.MainHero) : null)
        ?? base.GetRelationChangeAfterClanLeaderIsDead(deadLeader, relationHero);

    /// <summary>
    /// Personal relations (#862): the relation cost of disbanding an army by paying influence. The only vanilla caller is
    /// <c>DisbandArmyAction.ApplyInternal</c> (<c>DismissalRequestedWithInfluence</c>), on the main thread. Only the army leader
    /// is repriced; null from the service (inactive, cost off, or a member) leaves vanilla's -4 and -1.
    /// </summary>
    public override int GetRelationCostOfDisbandingArmy(bool isLeaderParty) =>
        _personalRelations.DisbandCost(isLeaderParty) ?? base.GetRelationCostOfDisbandingArmy(isLeaderParty);

    public override int GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(
        Hero supporter, bool hasHeroVotedAgainstOwner)
    {
        int result = base.GetRelationChangeAfterVotingInSettlementOwnerPreliminaryDecision(
            supporter, hasHeroVotedAgainstOwner);

        if (hasHeroVotedAgainstOwner
            && supporter.Culture?.HasFeat(TaomCulturalFeats.IsengardDecisionPenaltyFeat) == true)
        {
            result += (int)(result * TaomCulturalFeats.IsengardDecisionPenaltyFeat.EffectBonus);
        }

        return result;
    }
}
