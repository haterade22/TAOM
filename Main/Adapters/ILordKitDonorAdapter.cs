using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

namespace TAOM.Adapters;

/// <summary>
/// Picks a generated lord's equipment from the kits the XML lords of the same culture, race and sex
/// already wear (<see cref="TAOM.Features.GeneratedLordKits.LordKitSelector"/>).
/// </summary>
public interface ILordKitDonorAdapter
{
    /// <summary>A fresh copy of a shared peer kit, or null when there is none and the caller falls back.</summary>
    Equipment PickKit(Hero hero, Equipment.EquipmentType equipmentType);
}
