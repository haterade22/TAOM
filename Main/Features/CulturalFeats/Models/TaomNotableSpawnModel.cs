using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

/// <summary>
/// Overrides vanilla notable target counts per (settlement, occupation). Used to give
/// specific TAOM cultures more notables for the AI's recruitment pool — most importantly
/// Isengard, which has only a single town and needs the extra notable density to feed
/// AI recruitment of Isengard troops.
///
/// Keyed on <c>settlement.Culture</c> (settlement identity), NOT <c>OwnerClan.Culture</c>:
/// an Isengard town keeps its Isengard notable density even when conquered by another clan.
/// This matches how vanilla treats settlement identity (`Settlement.Culture` is XML-defined
/// and survives ownership change).
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
        return _feats.ApplyNotableCountFeat(culture, settlement.IsTown, baseCount);
    }
}
