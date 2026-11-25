namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// The host's answer to one <see cref="GuestTradeRequest"/> (#1329). When <see cref="Refusal"/> is
/// <see cref="GuestTradeRefusal.None"/>, <see cref="Status"/> is an <c>EmissaryPurchaseStatus</c> for a purchase or an
/// <c>UpgradeBlock</c> for an upgrade. <see cref="Balance"/> is the hero's balance of <see cref="ResourceId"/> on the
/// host after the request, or null when the settlement's faction has no special resource. <see cref="ArmouryLevel"/> is
/// the town's armoury level on the host, for an armoury request: the guest builds its list at that level, as a visiting
/// armourer's bonus lives only in the host's state. For a ladder action <see cref="Status"/> is a
/// <see cref="GuestLadderOutcome"/>. <see cref="Ladder"/> is the hero's rung of the lord's gear ladder as the server
/// holds it, after the request, for an armoury request; null when the server's switch is off, every rung is claimed,
/// or the server sent none (a v2.0.40 server).
/// </summary>
public sealed record GuestTradeReply(
    string RequestId,
    GuestTradeAction Action,
    GuestTradeRefusal Refusal,
    int Status = 0,
    int Quantity = 0,
    int TotalCost = 0,
    string? ResourceName = null,
    string? ResourceId = null,
    float? Balance = null,
    int? ArmouryLevel = null,
    GuestLadderView? Ladder = null)
{
    public bool Handled => Refusal == GuestTradeRefusal.None;
}
