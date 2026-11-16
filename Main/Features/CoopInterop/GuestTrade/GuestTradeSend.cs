namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>What <see cref="GuestTradeClient.Request"/> did with a request.</summary>
public enum GuestTradeSend
{
    /// <summary>Sent; the reply callback runs when the host answers.</summary>
    Sent,

    /// <summary>An earlier request still waits for the host; nothing was sent.</summary>
    Busy,

    /// <summary>No link, or the link could not send; nothing was sent.</summary>
    Failed,

    /// <summary>
    /// The last request got no answer within <see cref="GuestTradeClient.ReplyTimeoutSeconds"/>; nothing was sent. The
    /// next request is sent, and a late answer to the old one still runs.
    /// </summary>
    NoAnswer,
}
