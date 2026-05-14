using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CulturalFeats.Models;

/// <summary>
/// Thin GameModel boundary — converts <see cref="MobileParty"/> into the
/// adapter the <see cref="ICulturalFeatsService"/> needs, then delegates. Per
/// <c>gamemodels.md</c> rule 4: no inline if/foreach/switch/yield, no business
/// logic. Issues #144 / #176.
/// </summary>
public class TaomArmyManagementModel : DefaultArmyManagementCalculationModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomArmyManagementModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
        => _feats.ApplyArmyInfluenceAward(
            CultureFeatAdapter.FromOrNull(armyMemberParty.Party?.Owner?.Culture),
            base.DailyBeingAtArmyInfluenceAward(armyMemberParty));

    public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
        => _feats.ApplyArmyInfluenceCost(
            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party?.Owner?.Culture),
            base.CalculatePartyInfluenceCost(armyLeaderParty, party));
}
