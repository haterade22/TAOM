using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
{
    /// <summary>
    /// Vanilla forest movement penalty magnitude (DefaultPartySpeedCalculatingModel.MovingAtForestEffect).
    /// </summary>
    private const float ForestPenaltyMagnitude = 0.3f;

    private readonly ICareerPassiveService _careerPassives;
    private static TextObject? _cultureText;
    private static TextObject CultureText => _cultureText ??= GameTexts.FindText("str_culture");

    public TaomPartySpeedModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
    {
        var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);

        var culture = mobileParty.Party?.Owner?.Culture;
        if (culture == null)
            return result;

        // Phase 9b #135 P1 — `Campaign.Current` and `MapSceneWrapper` can both be null during
        // scene transitions (between campaign-map and battle-mission for example). This method
        // fires per-party-per-tick (`CalculateFinalSpeed` is on the world-map hot path), so the
        // pre-fix NRE was non-deterministic but frequent. `?.` short-circuit returns
        // `TerrainType.Plain` from the default-int 0; we only branch on `Forest` so non-forest
        // (including the null fallback) correctly skips the forest-feat block.
        var terrain = Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace) ?? TerrainType.Plain;
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

        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
        return result;
    }
}
