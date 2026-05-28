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
        // scene transitions; `?.` short-circuit yields a null TerrainType which MapTerrain maps
        // to TerrainKind.None so no terrain feat is applied.
        var culture = CultureFeatAdapter.FromOrNull(ResolvePartyCulture(mobileParty.Party));
        var terrain = MapTerrain(
            Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
        // Match vanilla: the night movement penalty (which the Mordor night feat offsets) is
        // applied only when not at sea, so the offsetting bonus must be land-only too.
        var isNight = (Campaign.Current?.IsNight ?? false) && !mobileParty.IsCurrentlyAtSea;
        var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster);

        _feats.ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result);
        _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);

        return result;
    }

    /// <summary>
    /// Boundary helper — maps the sealed TaleWorlds <see cref="TerrainType"/> (nullable
    /// when the map scene is unavailable) to the TAOM-owned <see cref="TerrainKind"/> so
    /// the service stays free of engine types (ADR-007). <see cref="TerrainType.Dune"/>
    /// folds into <see cref="TerrainKind.Desert"/> to match vanilla's desert handling.
    /// Any unmapped terrain (water, mountain, etc.) and a null input map to
    /// <see cref="TerrainKind.None"/>.
    /// </summary>
    private static TerrainKind MapTerrain(TerrainType? terrain) => terrain switch
    {
        TerrainType.Plain => TerrainKind.Plain,
        TerrainType.Forest => TerrainKind.Forest,
        TerrainType.Swamp => TerrainKind.Swamp,
        TerrainType.Steppe => TerrainKind.Steppe,
        TerrainType.Desert => TerrainKind.Desert,
        TerrainType.Dune => TerrainKind.Desert,
        TerrainType.Snow => TerrainKind.Snow,
        _ => TerrainKind.None,
    };

    /// <summary>
    /// Boundary helper — resolves the culture used for cultural-feat checks with the same
    /// precedence as vanilla <c>PartyBaseHelper.HasFeat</c> (leader hero → party → owner →
    /// settlement). Resolving only <c>Owner.Culture</c> missed ownerless parties (garrison,
    /// militia, caravan) and leader-driven culture; this mirrors the engine. Returns null
    /// when no culture can be resolved.
    /// </summary>
    private static CultureObject? ResolvePartyCulture(PartyBase? party)
    {
        if (party == null)
            return null;
        if (party.LeaderHero != null)
            return party.LeaderHero.Culture;
        if (party.Culture != null)
            return party.Culture;
        if (party.Owner != null)
            return party.Owner.Culture;
        if (party.Settlement != null)
            return party.Settlement.Culture;
        return null;
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
