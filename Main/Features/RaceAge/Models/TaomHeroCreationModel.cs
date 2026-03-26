using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Core;

namespace TAOM.Features.RaceAge.Models;

public class TaomHeroCreationModel : DefaultHeroCreationModel
{
    public override CharacterObject GetCharacterTemplateForOffspring(
        Hero mother, Hero father, bool isOffspringFemale)
    {
        // LOTR: Children inherit the same-sex parent's race and appearance
        // Male children take after the father, female children take after the mother
        if (isOffspringFemale)
            return mother.CharacterObject;
        return father.CharacterObject;
    }
}
