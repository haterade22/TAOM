using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Features.Arena.Models;

public class TaomTournamentModel : DefaultTournamentModel
{
    internal const float RegularMinTier = 2f;
    internal const float RegularMaxTier = 4f;
    internal const float EliteMinTier = 4f;

    public override MBList<ItemObject> GetRegularRewardItems(
        Town town, int regularRewardMinValue, int regularRewardMaxValue)
    {
        var items = BuildPrizePool(town.Culture?.StringId, RegularMinTier, RegularMaxTier);
        return items.Count > 0
            ? items
            : base.GetRegularRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
    }

    public override MBList<ItemObject> GetEliteRewardItems(
        Town town, int regularRewardMinValue, int regularRewardMaxValue)
    {
        var items = BuildPrizePool(town.Culture?.StringId, EliteMinTier, float.MaxValue);
        return items.Count > 0
            ? items
            : base.GetEliteRewardItems(town, regularRewardMinValue, regularRewardMaxValue);
    }

    private static MBList<ItemObject> BuildPrizePool(string cultureId, float minTier, float maxTier)
    {
        var result = new MBList<ItemObject>();
        if (string.IsNullOrEmpty(cultureId))
            return result;
        foreach (var item in Items.All)
        {
            if (item.Culture?.StringId != cultureId) continue;
            if (item.NotMerchandise) continue;
            if (item.Tierf < minTier || item.Tierf >= maxTier) continue;
            if (!item.HasWeaponComponent && !item.HasArmorComponent) continue;
            if (item.ItemType == ItemObject.ItemTypeEnum.Horse) continue;
            result.Add(item);
        }
        return result;
    }

    public override Equipment GetParticipantArmor(CharacterObject participant)
    {
        if (participant.Culture != null)
        {
            var dummy = Game.Current.ObjectManager.GetObject<CharacterObject>(
                $"gear_practice_dummy_{participant.Culture.StringId}");
            if (dummy?.RandomBattleEquipment != null)
                return dummy.RandomBattleEquipment;
        }

        return base.GetParticipantArmor(participant);
    }

    internal static string ResolveDummyId(string participantCultureId, string settlementCultureId)
    {
        if (!string.IsNullOrEmpty(participantCultureId))
            return $"gear_practice_dummy_{participantCultureId}";
        if (!string.IsNullOrEmpty(settlementCultureId))
            return $"gear_practice_dummy_{settlementCultureId}";
        return "gear_practice_dummy_empire";
    }
}
