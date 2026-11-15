namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// A guest's intent, as the host receives it (#1329). It names no hero, price or balance: the host finds the hero
/// from the connection and reads the rest from its own campaign. <see cref="GuestTradeClient"/> sets
/// <see cref="RequestId"/>.
/// </summary>
/// <param name="Subject">The troop to buy, or the armour piece to upgrade.</param>
/// <param name="Modifier">The armour piece's modifier, or null.</param>
/// <param name="Target">The armour piece the upgrade makes.</param>
public sealed record GuestTradeRequest(
    GuestTradeAction Action,
    string SettlementId,
    string? Subject = null,
    string? Modifier = null,
    string? Target = null,
    int Quantity = 0,
    string RequestId = "");
