using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopWeight;

/// <summary>
/// Which parties pay the troop-weight size penalty. The penalty (<see cref="TroopWeightService.ApplyPartySizeWeightPenalty"/>)
/// and every capacity display (<c>TroopWeightDisplayHook</c>) read this one predicate, so what is enforced and what is
/// shown cannot disagree.
///
/// A garrison's and a patrol's limits come from their settlement: v1.4.8 <c>DefaultPartySizeLimitModel.GetPartyMemberSizeLimit</c> (DefaultPartySizeLimitModel.cs:81-96)
/// hands them to <c>CalculateGarrisonPartySizeLimit</c> and <c>CalculatePatrolPartySizeLimit</c>, and nothing TAOM runs trims
/// either (the shed needs a <c>LeaderHero</c>, which neither has). Deflated by weight, a garrison deserted every day, since
/// vanilla desertion removes a quarter of the excess over <c>PartySizeLimit</c> and its morale falls with the excess while
/// lords refill it to the unweighted garrison limit (2026-10-16, yotthani's campaign runs, recorded on the 1.5.x line). A patrol, which
/// spawns and refills at full size, sat over its limit for good, under vanilla's over-limit speed factor.
/// </summary>
public static class TroopWeightPenaltyScope
{
    public static bool PaysSizePenalty(PartyBase party)
    {
        var mobile = party.MobileParty;
        return mobile == null || !(mobile.IsGarrison || mobile.IsPatrolParty);
    }
}
