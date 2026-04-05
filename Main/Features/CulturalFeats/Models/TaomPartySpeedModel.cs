using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
{
    /// <summary>
    /// Vanilla forest movement penalty magnitude (DefaultPartySpeedCalculatingModel.MovingAtForestEffect).
    /// </summary>
    private const float ForestPenaltyMagnitude = 0.3f;

    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
    {
        var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);

        var culture = mobileParty.Party?.Owner?.Culture;
        if (culture == null)
            return result;

        var terrain = Campaign.Current.MapSceneWrapper.GetFaceTerrainType(mobileParty.CurrentNavigationFace);
        if (terrain == TerrainType.Forest)
        {
            if (culture.HasFeat(TaomCulturalFeats.MirkwoodForestSpeedFeat))
            {
                float bonus = TaomCulturalFeats.MirkwoodForestSpeedFeat.EffectBonus * ForestPenaltyMagnitude;
                result.AddFactor(bonus, CultureText);
            }

            if (culture.HasFeat(TaomCulturalFeats.LothlorienForestSpeedFeat))
            {
                float bonus = TaomCulturalFeats.LothlorienForestSpeedFeat.EffectBonus * ForestPenaltyMagnitude;
                result.AddFactor(bonus, CultureText);
            }
        }

        // Rohan infantry speed penalty — applies when >50% of party is infantry
        if (culture.HasFeat(TaomCulturalFeats.RohanInfantrySpeedFeat))
        {
            var roster = mobileParty.MemberRoster;
            int totalCount = roster.TotalManCount;
            if (totalCount > 0)
            {
                int mountedCount = 0;
                foreach (var element in roster.GetTroopRoster())
                {
                    if (element.Character?.IsMounted == true)
                        mountedCount += element.Number;
                }
                if (mountedCount * 2 < totalCount)
                    result.AddFactor(TaomCulturalFeats.RohanInfantrySpeedFeat.EffectBonus, CultureText);
            }
        }

        return result;
    }
}
