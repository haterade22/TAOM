using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Features.CareerSystem;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.EyeOfSauron;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomPartySpeedModel : DefaultPartySpeedCalculatingModel
{
    private const float PlayerCaravanSpeedFactor = 0.15f;
    private static TextObject? _packTrainText;

    private readonly ICulturalFeatsService _feats;
    private readonly ICareerPassiveService _careerPassives;
    private readonly IEyeOfSauronHook _eyeOfSauron;

    public TaomPartySpeedModel(ICulturalFeatsService feats, ICareerPassiveService careerPassives, IEyeOfSauronHook eyeOfSauron)
    {
        _feats = feats;
        _careerPassives = careerPassives;
        _eyeOfSauron = eyeOfSauron;
    }

    public override ExplainedNumber CalculateFinalSpeed(MobileParty mobileParty, ExplainedNumber finalSpeed)
    {
        var result = base.CalculateFinalSpeed(mobileParty, finalSpeed);

        // Boundary: convert sealed TaleWorlds types to primitives + adapter, then delegate.
        // Phase 9b #135 P1 — `Campaign.Current` and `MapSceneWrapper` can both be null during
        // scene transitions; `?.` short-circuit yields a null TerrainType which MapTerrain maps
        // to TerrainKind.None so no terrain feat is applied.
        var culture = CultureFeatAdapter.FromOrNull(mobileParty.Party);
        var terrain = MapTerrain(
            Campaign.Current?.MapSceneWrapper?.GetFaceTerrainType(mobileParty.CurrentNavigationFace));
        // Match vanilla: the night movement penalty (which the Mordor night feat offsets) is
        // applied only when not at sea, so the offsetting bonus must be land-only too.
        var isNight = (Campaign.Current?.IsNight ?? false) && !mobileParty.IsCurrentlyAtSea;
        var (mountedCount, totalCount) = CountMountedAndTotal(mobileParty.MemberRoster, _feats.NeedsMountedCount(culture));

        _feats.ApplyTerrainSpeedFeats(culture, terrain, isNight, ref result);
        _feats.ApplyRohanInfantryPenalty(culture, mountedCount, totalCount, ref result);
        _careerPassives.ApplyFactor(mobileParty.LeaderHero?.StringId, ref result, PassiveEffectType.PartyMovementSpeed);
        ApplyPlayerCaravanSpeed(mobileParty, ref result);
        // #765 M8: the Eye of Sauron hunter outpaces the player within 1.5 of the player's seeing ranges. LAST, so
        // the floor is compared against every other line; it reads only the main-thread snapshot, because this runs
        // on the parallel party tick.
        _eyeOfSauron.ApplyHunterSpeedFloor(mobileParty.StringId, ref result);

        return result;
    }

    private static void ApplyPlayerCaravanSpeed(MobileParty mobileParty, ref ExplainedNumber result)
    {
        if (mobileParty.IsCaravan && mobileParty.Owner?.Clan == Clan.PlayerClan)
            result.AddFactor(PlayerCaravanSpeedFactor, _packTrainText ??= new TextObject("{=taom_caravan_pack_train_speed}Caravan pack train"));
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
    /// Boundary helper: collapses a sealed <see cref="TroopRoster"/> down to the two primitives
    /// <see cref="ICulturalFeatsService.ApplyRohanInfantryPenalty"/> needs, keeping the service free of
    /// TaleWorlds types per ADR-007. When the service says the penalty cannot apply, it returns (0, 0)
    /// without touching the roster; the penalty then returns at its own <c>totalCount &lt;= 0</c> gate,
    /// exactly as it returned at its feat gate before.
    /// </summary>
    private static (int mounted, int total) CountMountedAndTotal(TroopRoster roster, bool needed)
    {
        if (!needed)
            return (0, 0);
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
