namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// One request from the AI. The match applies it through the same Try* method a human click would use. Id is the offer id
/// for BuyPack and the hero id for BuyHero and Buyback (Lane is unused for Buyback); Order is read only for SetOrder.
/// Note is a log line the match reports once the order was accepted; empty for most actions.
/// </summary>
internal sealed record AiAction(AiActionKind Kind, int Lane, string Id, LaneOrder Order = LaneOrder.Push, string Note = "")
{
    public static AiAction BuyPack(int lane, string offerId) => new(AiActionKind.BuyPack, lane, offerId);

    public static AiAction BuyHero(int lane, string heroId) => new(AiActionKind.BuyHero, lane, heroId);

    public static AiAction Buyback(string heroId) => new(AiActionKind.Buyback, -1, heroId);

    public static AiAction SetOrder(int lane, LaneOrder order, string note = "") => new(AiActionKind.SetOrder, lane, "", order, note);

    public static AiAction BuyUpgrade(string trackId) => new(AiActionKind.BuyUpgrade, -1, trackId);

    public static AiAction CallCreature(int lane, string offerId) => new(AiActionKind.CallCreature, lane, offerId);
}
