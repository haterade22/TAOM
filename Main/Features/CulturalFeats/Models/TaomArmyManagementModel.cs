using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomArmyManagementModel : DefaultArmyManagementCalculationModel
{
    public override float DailyBeingAtArmyInfluenceAward(MobileParty armyMemberParty)
    {
        float result = base.DailyBeingAtArmyInfluenceAward(armyMemberParty);

        var culture = armyMemberParty.Party?.Owner?.Culture;
        if (culture == null)
            return result;

        if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceFeat))
            result += result * TaomCulturalFeats.RivendellArmyInfluenceFeat.EffectBonus;

        if (culture.HasFeat(TaomCulturalFeats.GondorArmyInfluenceFeat))
            result += result * TaomCulturalFeats.GondorArmyInfluenceFeat.EffectBonus;

        return result;
    }

    public override int CalculatePartyInfluenceCost(MobileParty armyLeaderParty, MobileParty party)
    {
        int result = base.CalculatePartyInfluenceCost(armyLeaderParty, party);

        var culture = armyLeaderParty.Party?.Owner?.Culture;
        if (culture == null)
            return result;

        float multiplier = 0f;

        if (culture.HasFeat(TaomCulturalFeats.RivendellArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.RivendellArmyInfluenceCostFeat.EffectBonus;

        if (culture.HasFeat(TaomCulturalFeats.GundabadArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.GundabadArmyInfluenceCostFeat.EffectBonus;

        if (culture.HasFeat(TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.DolGuldurArmyInfluenceCostFeat.EffectBonus;

        if (culture.HasFeat(TaomCulturalFeats.MordorArmyInfluenceCostFeat))
            multiplier += TaomCulturalFeats.MordorArmyInfluenceCostFeat.EffectBonus;

        if (multiplier != 0f)
            result = (int)(result * (1f + multiplier));

        return result;
    }
}
