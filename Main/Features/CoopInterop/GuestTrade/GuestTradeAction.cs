namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// What a co-op guest asks the host to do (#1329). The values travel on the wire as ints: never renumber.
/// A balance action changes nothing and only returns the guest's balance on the host.
/// </summary>
public enum GuestTradeAction
{
    None = 0,
    EmissaryBalance = 1,
    EmissaryBuy = 2,
    ArmouryBalance = 3,
    ArmouryUpgrade = 4,
}
