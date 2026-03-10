using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.TroopProgression.Models;

public class TaomPartyWageModel : DefaultPartyWageModel
{
    private readonly ITroopCostService _costService;

    public TaomPartyWageModel(ITroopCostService costService)
    {
        _costService = costService;
    }

    public override int MaxWagePaymentLimit => 20000;

    public override int GetCharacterWage(CharacterObject character)
    {
        int tier = character.Tier;
        bool isMounted = character.IsMounted;
        bool isMercenary = IsMercenaryOccupation(character.Occupation);

        return _costService.GetCharacterWage(tier, isMounted, isMercenary);
    }

    public override ExplainedNumber GetTroopRecruitmentCost(
        CharacterObject troop, Hero buyerHero, bool withoutItemCost = false)
    {
        int level = troop.Level;
        bool isMercenary = IsMercenaryOccupation(troop.Occupation);

        int baseCost = _costService.GetTroopRecruitmentCost(level, isMercenary);

        var result = new ExplainedNumber(baseCost, includeDescriptions: false);

        if (!withoutItemCost && troop.IsMounted)
        {
            int horseCost = troop.Level >= 26 ? 500 : 150;
            result.Add(horseCost, null);
        }

        return result;
    }

    private static bool IsMercenaryOccupation(Occupation occupation)
    {
        return occupation == Occupation.Mercenary
            || occupation == Occupation.Gangster
            || occupation == Occupation.CaravanGuard;
    }
}
