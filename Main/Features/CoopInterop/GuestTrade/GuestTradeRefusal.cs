namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// Why the host did not run a guest's request (#1329). <see cref="None"/> means it ran; the result is then in the
/// reply's status. The values travel on the wire as ints: never renumber.
/// </summary>
public enum GuestTradeRefusal
{
    None = 0,

    /// <summary>The hero is dead, a prisoner, or does not lead its own party.</summary>
    NotFreeLeader = 1,

    /// <summary>The party is not in the settlement the request names, or is in a battle or a besieged settlement.</summary>
    NotInSettlement = 2,

    /// <summary>The emissary does not trade with this hero here.</summary>
    NoEmissaryTrade = 3,

    /// <summary>The settlement has no armoury, or Armour Acquisition is off.</summary>
    NoArmoury = 4,

    /// <summary>The request is malformed: an unknown action, a missing id, or a quantity out of range.</summary>
    BadRequest = 5,

    /// <summary>The host threw while it handled the request.</summary>
    HostFailed = 6,
}
