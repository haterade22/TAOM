namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// What a co-op guest asks the host to do (#1329). The values travel on the wire as ints: never renumber.
/// A balance action changes nothing and only returns the guest's balance on the host. The two ladder actions hand
/// in the lord's materials for, or claim the piece of, the rung the guest saw (<see cref="GuestLadderView"/>); a
/// v2.0.40 server answers them BadRequest, and a guest sends them only after a server's reply carried a ladder.
/// </summary>
public enum GuestTradeAction
{
    None = 0,
    EmissaryBalance = 1,
    EmissaryBuy = 2,
    ArmouryBalance = 3,
    ArmouryUpgrade = 4,
    LadderHandIn = 5,
    LadderClaim = 6,
}
