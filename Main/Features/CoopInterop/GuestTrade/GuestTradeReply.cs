namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// The host's answer to one <see cref="GuestTradeRequest"/> (#1329). When <see cref="Refusal"/> is
/// <see cref="GuestTradeRefusal.None"/>, <see cref="Status"/> is an <c>EmissaryPurchaseStatus</c> for a purchase or an
/// <c>UpgradeBlock</c> for an upgrade. <see cref="Balance"/> is the hero's balance of <see cref="ResourceId"/> on the
/// host after the request, or null when the settlement's faction has no special resource. <see cref="ArmouryLevel"/> is
/// the town's armoury level on the host, for an armoury request: the guest builds its list at that level, as a visiting
/// armourer's bonus lives only in the host's state.
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
    int? ArmouryLevel = null)
{
    public bool Handled => Refusal == GuestTradeRefusal.None;
}
