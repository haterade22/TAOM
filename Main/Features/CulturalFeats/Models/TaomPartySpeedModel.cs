using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
{
    /// <summary>
    /// Vanilla forest movement penalty magnitude
    /// (<see cref="DefaultPartySpeedCalculatingModel.MovingAtForestEffect"/>).
    /// </summary>
    private const float ForestPenaltyMagnitude = 0.3f;

    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartySpeedModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives)
    {
        _feats = feats;
        _careerPassives = careerPassives;
    }

    public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
    {
        var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);

        // Boundary: convert sealed TaleWorlds types to primitives + adapter, then delegate.
        // Phase 9b #135 P1 — `Campaign.Current` and `MapSceneWrapper` can both be null during
        // scene transitions; `?.` short-circuit returns Plain so the forest branch is skipped.
        var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party?.Owner?.Culture);
        var terrain = Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace)
                      ?? TerrainType.Plain;
        var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster);

        _feats.ApplyForestSpeedFeats(culture, terrain == TerrainType.Forest, ForestPenaltyMagnitude, ref result);
        _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);

        return result;
    }

    /// <summary>
    /// Boundary helper — collapses a sealed <see cref="TroopRoster"/> down to the
    /// two primitives <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/>
    /// needs, keeping the service free of TaleWorlds types per ADR-007.
    /// </summary>
    private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster)
    {
        int total = roster.TotalManCount;
        int mounted = 0;
        foreach (var element in roster.GetTroopRoster())
        {
            if (element.Character?.IsMounted == true)
                mounted += element.Number;
        }
        return (mounted, total);
    }
}
