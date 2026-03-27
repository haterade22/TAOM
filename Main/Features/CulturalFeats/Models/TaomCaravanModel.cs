using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Library;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomCaravanModel : DefaultCaravanModel
{
    public override int GetCaravanFormingCost(bool largerCaravan, bool navalCaravan)
    {
        int baseCost = base.GetCaravanFormingCost(largerCaravan, navalCaravan);

        if (CharacterObject.PlayerCharacter?.Culture?.HasFeat(TaomCulturalFeats.UmbarCheaperCaravansFeat) == true)
            return MathF.Round(baseCost * TaomCulturalFeats.UmbarCheaperCaravansFeat.EffectBonus);

        return baseCost;
    }
}
