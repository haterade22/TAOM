namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// The way to the host, which TAOM.CoopCompat supplies (#1329). TAOM.dll names no Coop type, so the relay attaches
/// itself to <see cref="GuestTradeClient.Attach"/> when Coop is usable.
/// </summary>
public interface IGuestTradeLink
{
    /// <summary>False when this process is not a client in a session or has no network.</summary>
    bool SendToHost(GuestTradeRequest request);
}
