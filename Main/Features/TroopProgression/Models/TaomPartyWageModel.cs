using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CulturalFeats;

namespace TAOM.Features.TroopProgression.Models;

public class TaomPartyWageModel : DefaultPartyWageModel
{
    private static readonly TextObject CultureText = GameTexts.FindText("str_culture");

    private readonly ITroopCostService _costService;

    public TaomPartyWageModel(ITroopCostService costService)
    {
        _costService = costService;
    }

    public override int MaxWagePaymentLimit => 20000;

    public override int GetCharacterWage(CharacterObject character)
    {
        int tier = character.Tier;
        bool isMounted = character.IsMounted;
        bool isMercenary = IsMercenaryOccupation(character.Occupation);

        return _costService.GetCharacterWage(tier, isMounted, isMercenary);
    }

    public override ExplainedNumber GetTotalWage(MobileParty mobileParty, TroopRoster troopRoster, bool includeDescriptions = false)
    {
        var result = base.GetTotalWage(mobileParty, troopRoster, includeDescriptions);

        // Garrison wage feats — apply when party is garrisoned
        if (mobileParty.CurrentSettlement?.Owner?.Culture is { } garrisonCulture)
        {
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.EreborGarrisonWageFeat);
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.LothlorienGarrisonWageFeat);
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.IsengardGarrisonWageFeat);
            ApplyGarrisonWageFeat(ref result, garrisonCulture, TaomCulturalFeats.GondorGarrisonWageFeat);
        }

        // Party wage feats — apply to party owner's culture
        var partyCulture = mobileParty.Party?.Owner?.Culture;
        if (partyCulture != null)
        {
            if (partyCulture.HasFeat(TaomCulturalFeats.GundabadWageFeat))
                result.AddFactor(TaomCulturalFeats.GundabadWageFeat.EffectBonus, CultureText);

            if (partyCulture.HasFeat(TaomCulturalFeats.UmbarWageFeat))
                result.AddFactor(TaomCulturalFeats.UmbarWageFeat.EffectBonus, CultureText);

            if (partyCulture.HasFeat(TaomCulturalFeats.MordorWageFeat))
                result.AddFactor(TaomCulturalFeats.MordorWageFeat.EffectBonus, CultureText);

            // Rohan mounted wage reduction — scale by mounted troop fraction
            if (partyCulture.HasFeat(TaomCulturalFeats.RohanMountedWageFeat) && troopRoster != null)
            {
                int totalCount = troopRoster.TotalManCount;
                if (totalCount > 0)
                {
                    int mountedCount = 0;
                    foreach (var element in troopRoster.GetTroopRoster())
                    {
                        if (element.Character?.IsMounted == true)
                            mountedCount += element.Number;
                    }
                    float mountedFraction = (float)mountedCount / totalCount;
                    result.AddFactor(TaomCulturalFeats.RohanMountedWageFeat.EffectBonus * mountedFraction, CultureText);
                }
            }
        }

        return result;
    }

    public override ExplainedNumber GetTroopRecruitmentCost(
        CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
    {
        int level = troop.Level;
        bool isMercenary = IsMercenaryOccupation(troop.Occupation);

        int baseCost = _costService.GetTroopRecruitmentCost(level, isMercenary);

        var result = new ExplainedNumber(baseCost, includeDescriptions: false);

        if (!withoutItemCost && troop.IsMounted)
        {
            int horseCost = troop.Level >= 26 ? 500 : 150;
            result.Add(horseCost, null);
        }

        // Mounted recruit cost feats
        if (troop.IsMounted && buyerHero?.Culture?.HasFeat(TaomCulturalFeats.IsengardCheaperRecruitsFeat) == true)
            result.AddFactor(TaomCulturalFeats.IsengardCheaperRecruitsFeat.EffectBonus, CultureText);

        if (troop.IsMounted && buyerHero?.Culture?.HasFeat(TaomCulturalFeats.RohanMountedCostFeat) == true)
            result.AddFactor(TaomCulturalFeats.RohanMountedCostFeat.EffectBonus, CultureText);

        return result;
    }

    private static void ApplyGarrisonWageFeat(
        ref ExplainedNumber result, CultureObject culture, FeatObject feat)
    {
        if (culture.HasFeat(feat))
            result.AddFactor(feat.EffectBonus, CultureText);
    }

    private static bool IsMercenaryOccupation(Occupation occupation)
    {
        return occupation == Occupation.Mercenary
            || occupation == Occupation.Gangster
            || occupation == Occupation.CaravanGuard;
    }
}
