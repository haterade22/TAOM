using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.RaceAge.Models;

public class TaomHeroCreationModel : DefaultHeroCreationModel
{
    public override CharacterObject GetCharacterTemplateForOffspring(
        Hero mother, Hero father, bool isOffspringFemale)
    {
        // LOTR: Father's race always determines the child's race
        // Eldarion (Aragorn + Arwen) is a Man, not an Elf
        return father.CharacterObject;
    }
}
