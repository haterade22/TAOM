using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

/// <summary>
/// Overrides vanilla notable target counts per (settlement, occupation). Used to give
/// specific TAOM cultures more notables for the AI's recruitment pool — most importantly
/// Isengard and Dol Guldur, which have few settlements and need asymmetric Gang-Leader
/// concentration to keep their recruitment competitive with Rohan's distributed map.
///
/// Keyed on <c>settlement.Culture</c> (settlement identity), NOT <c>OwnerClan.Culture</c>:
/// an Isengard town keeps its Isengard notable density even when conquered by another clan.
///
/// Maps the sealed TaleWorlds <see cref="Occupation"/> enum to TAOM-owned
/// <see cref="NotableOccupationKind"/> at the boundary so the service stays free of engine
/// types (ADR-007).
/// </summary>
public class TaomNotableSpawnModel : DefaultNotableSpawnModel
{
    private readonly ICulturalFeatsService _feats;

    public TaomNotableSpawnModel(ICulturalFeatsService feats)
    {
        _feats = feats;
    }

    public override int GetTargetNotableCountForSettlement(Settlement settlement, Occupation occupation)
    {
        int baseCount = base.GetTargetNotableCountForSettlement(settlement, occupation);
        if (baseCount <= 0 || settlement == null)
            return baseCount;
        var culture = CultureFeatAdapter.FromOrNull(settlement.Culture);
        return _feats.ApplyNotableCountFeat(culture, MapOccupation(occupation), baseCount);
    }

    /// <summary>
    /// Boundary helper — maps the sealed TaleWorlds <see cref="Occupation"/> enum to
    /// the TAOM-owned <see cref="NotableOccupationKind"/> so the service is free of
    /// engine types (ADR-007). Any occupation that vanilla's notable spawn pool does
    /// not target (e.g. Preacher, Soldier, Lord) maps to
    /// <see cref="NotableOccupationKind.Other"/> and the service returns the base
    /// count unchanged.
    /// </summary>
    private static NotableOccupationKind MapOccupation(Occupation occupation) => occupation switch
    {
        Occupation.Merchant => NotableOccupationKind.Merchant,
        Occupation.Artisan => NotableOccupationKind.Artisan,
        Occupation.GangLeader => NotableOccupationKind.GangLeader,
        Occupation.RuralNotable => NotableOccupationKind.RuralNotable,
        Occupation.Headman => NotableOccupationKind.Headman,
        _ => NotableOccupationKind.Other,
    };
}
