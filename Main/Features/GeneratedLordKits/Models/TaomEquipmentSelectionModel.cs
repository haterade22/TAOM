using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.GeneratedLordKits.Models;

/// <summary>
/// A lord who comes of age, a companion made a lord, or a ruler stepping down wears a kit the XML lords of the
/// same culture, race and sex share, not vanilla's race-blind template pick. With no such kit the engine's
/// pick stands (TAOM's taom_* lord templates, lord_template_rosters.xslt).
/// </summary>
public class TaomEquipmentSelectionModel : DefaultEquipmentSelectionModel
{
    private readonly ILordKitDonorAdapter _kits;

    public TaomEquipmentSelectionModel(ILordKitDonorAdapter kits) => _kits = kits;

    public override Equipment GetEquipmentForHeroComeOfAge(Hero hero, Equipment.EquipmentType equipmentType) =>
        _kits.PickKit(hero, equipmentType) ?? base.GetEquipmentForHeroComeOfAge(hero, equipmentType);

    public override Equipment GetEquipmentForCompanionWhenTurningToLord(Hero companionHero, Equipment.EquipmentType equipmentType) =>
        _kits.PickKit(companionHero, equipmentType) ?? base.GetEquipmentForCompanionWhenTurningToLord(companionHero, equipmentType);

    // The new ruler keeps TAOM's ruler template (regalia). The old ruler, a lord again, gets a peer kit; vanilla
    // leaves Item2 null when it re-kits nobody, so a null stays null.
    public override (Equipment, Equipment) GetEquipmentsForChangingRuler(Hero newRuler, Hero oldRuler, Equipment.EquipmentType equipmentType)
    {
        var (forNewRuler, forOldRuler) = base.GetEquipmentsForChangingRuler(newRuler, oldRuler, equipmentType);
        return (forNewRuler, forOldRuler == null ? null : _kits.PickKit(oldRuler, equipmentType) ?? forOldRuler);
    }
}
