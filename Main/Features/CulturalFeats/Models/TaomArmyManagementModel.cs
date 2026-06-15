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

    // Resolve party culture via the shared CultureFeatAdapter.FromOrNull(PartyBase) chokepoint —
    // vanilla PartyBaseHelper.HasFeat precedence (LeaderHero-first, MapFaction-aware), null-safe.
    // Replaces the prior Owner-only inline that skipped LeaderHero.Culture (the Codex-review-43
    // systemic gap the other party-culture models were already migrated for).
    public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
        => _feats.ApplyArmyInfluenceAward(
            CultureFeatAdapter.FromOrNull(armyMemberParty.Party),
            base.DailyBeingAtArmyInfluenceAward(armyMemberParty));

    public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
        => _feats.ApplyArmyInfluenceCost(
            CultureFeatAdapter.FromOrNull(armyLeaderParty.Party),
            base.CalculatePartyInfluenceCost(armyLeaderParty, party));
}
