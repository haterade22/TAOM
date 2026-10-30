using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Models;

/// <summary>
/// Career TradePrices (#769 E11): the raw trade penalty, scaled by the leader's passive (a negative magnitude is a
/// smaller penalty, so better prices both ways). This is the narrowest seam for it:
/// <c>DefaultTradeItemPriceFactorModel.GetTradePenalty</c> multiplies its raw penalty by this factor for the client
/// party (v1.5.5 :77-78), and <c>GetPriceFactor</c> then turns that penalty into the buy price
/// (<c>base * (1 + penalty)</c>) and the sell price (<c>base / (1 + penalty)</c>), so one hook covers both directions of
/// every quote, the player's, a caravan's and the supply prices TAOM reads through <c>Town.GetItemPrice</c>. It is the
/// only caller of <c>GetTradePenaltyFactor</c> in the game assemblies and passes a party only when it is not null.
///
/// Keyed on the party's own LeaderHero, not owner-first: an owner-first key would discount every caravan the player
/// owns, and a caravan's clientParty has its own leader. The service floors the scale at 0: a magnitude of -1 or
/// less is no penalty at all (no cliff at -1), and a cut can never turn a penalty into a discount below the base price.
/// </summary>
public class TaomPartyTradeModel : DefaultPartyTradeModel
{
    private readonly ICareerPassiveService _careerPassives;

    public TaomPartyTradeModel(ICareerPassiveService careerPassives)
    {
        _careerPassives = careerPassives;
    }

    public override float GetTradePenaltyFactor(MobileParty party)
        => _careerPassives.ScaleByPassive(party?.LeaderHero?.StringId, base.GetTradePenaltyFactor(party), PassiveEffectType.TradePrices);
}
