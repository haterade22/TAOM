using TAOM.Features.CaravanTrade;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.CulturalFeats.Models;

public class TaomCaravanModel : DefaultCaravanModel
{
    private readonly ICulturalFeatsService _feats;
    private readonly ICaravanTradeService _caravanTrade;

    public TaomCaravanModel(ICulturalFeatsService feats, ICaravanTradeService caravanTrade)
    {
        _feats = feats;
        _caravanTrade = caravanTrade;
    }

    public override int GetCaravanFormingCost(bool largerCaravan, bool navalCaravan)
        => _feats.ApplyCaravanCost(
            CultureFeatAdapter.FromOrNull(CharacterObject.PlayerCharacter?.Culture),
            base.GetCaravanFormingCost(largerCaravan, navalCaravan));

    // CaravanTrade basket-diversity levers (delegate to the pure service; vanilla when the feature is off).
    public override int GetInitialTradeGold(Hero owner, bool navalCaravan, bool largeCaravan)
        => _caravanTrade.ResolveInitialTradeGold(
            base.GetInitialTradeGold(owner, navalCaravan, largeCaravan),
            owner == Hero.MainHero);

    public override int GetMaxGoldToSpendOnOneItemCategory(MobileParty caravan, ItemCategory itemCategory)
        => _caravanTrade.ResolveMaxGoldPerCategory(
            base.GetMaxGoldToSpendOnOneItemCategory(caravan, itemCategory),
            caravan?.Owner?.Clan == Clan.PlayerClan);
}
