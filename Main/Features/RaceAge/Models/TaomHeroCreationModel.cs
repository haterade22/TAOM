using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.RaceAge.Models;

public class TaomHeroCreationModel : DefaultHeroCreationModel
{
    private readonly ILordKitDonorAdapter _lordKits;

    public TaomHeroCreationModel(ILordKitDonorAdapter lordKits) => _lordKits = lordKits;

    public override CharacterObject GetCharacterTemplateForOffspring(
        Hero mother, Hero father, bool isOffspringFemale)
    {
        // LOTR: Children inherit the same-sex parent's race and appearance
        // Male children take after the father, female children take after the mother
        if (isOffspringFemale)
            return mother.CharacterObject;
        return father.CharacterObject;
    }

    // An adult lord created at runtime (a new companion clan's lords, rebel leaders) otherwise keeps a clone of
    // its template's gear. For the six renamed vanilla cultures those templates are still vanilla's minor-faction
    // leaders in Calradic kit. Children, non-lords and minor-faction clans keep vanilla's handling (LordKitSelector.Wants).
    public override Equipment GetCivilianEquipment(Hero hero) =>
        _lordKits.PickKit(hero, Equipment.EquipmentType.Civilian) ?? base.GetCivilianEquipment(hero);

    public override Equipment GetBattleEquipment(Hero hero) =>
        _lordKits.PickKit(hero, Equipment.EquipmentType.Battle) ?? base.GetBattleEquipment(hero);
}
