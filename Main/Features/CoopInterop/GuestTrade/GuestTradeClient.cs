using System;
using System.Diagnostics;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// The guest's side of guest trade (#1329): sends one request at a time to the host and runs its callback when the
/// matching reply arrives, on the game thread (the relay marshals it). It copies the host's balance into this machine's
/// store, so TAOM's own emissary and armoury screens show the host's number. Design credit: minimalcash63
/// (TAOM.CoopEmissaryArmory).
/// </summary>
public sealed class GuestTradeClient
{
    /// <summary>After this long without a reply, a new request may go; the old one's reply still runs if it comes.</summary>
    internal const double ReplyTimeoutSeconds = 15;

    private readonly ICoopSessionProvider _coop;
    private readonly IPlayerPartyAdapter _player;
    private readonly ISpecialResourceStorageService _storage;
    private readonly IModLogger _logger;
    private readonly Func<double> _clock;
    private IGuestTradeLink? _link;
    private Waiting? _pending;
    private Waiting? _late;

    private sealed record Waiting(string RequestId, Action<GuestTradeReply> OnReply, double SentAt);

    public GuestTradeClient(ICoopSessionProvider coop, IPlayerPartyAdapter player, ISpecialResourceStorageService storage, IModLogger logger)
        : this(coop, player, storage, logger, () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency)
    {
    }

    internal GuestTradeClient(ICoopSessionProvider coop, IPlayerPartyAdapter player, ISpecialResourceStorageService storage,
        IModLogger logger, Func<double> clock)
    {
        _coop = coop;
        _player = player;
        _storage = storage;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>True on a co-op guest whose TAOM.CoopCompat relay is attached: the screens then ask the host.</summary>
    public bool IsGuest => _link != null && _coop.ShouldDeferToHost;

    /// <summary>
    /// True on a co-op guest with no relay (TAOM.CoopCompat not loaded, or Coop not usable): the screens refuse
    /// before anything is charged, as the host's next resync would erase what this machine changed.
    /// </summary>
    public bool IsGuestWithoutLink => _link == null && _coop.ShouldDeferToHost;

    /// <summary>The relay attaches itself, or null to detach. Whatever waited is dropped.</summary>
    public void Attach(IGuestTradeLink? link)
    {
        _link = link;
        _pending = null;
        _late = null;
    }

    public GuestTradeSend Request(GuestTradeRequest request, Action<GuestTradeReply> onReply)
    {
        var link = _link;
        if (link == null)
            return GuestTradeSend.Failed;
        var now = _clock();
        if (_pending != null)
        {
            if (now - _pending.SentAt < ReplyTimeoutSeconds)
                return GuestTradeSend.Busy;
            _late = _pending;
            _pending = null;
            return GuestTradeSend.NoAnswer;
        }

        var waiting = new Waiting(Guid.NewGuid().ToString("N"), onReply, now);
        _pending = waiting;
        bool sent;
        try
        {
            sent = link.SendToHost(request with { RequestId = waiting.RequestId });
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GuestTrade] could not send {request.Action}: {ex.Message}");
            sent = false;
        }
        if (sent)
            return GuestTradeSend.Sent;
        _pending = null;
        return GuestTradeSend.Failed;
    }

    public void Receive(GuestTradeReply reply)
    {
        if (string.IsNullOrEmpty(reply.RequestId))
            return;
        Waiting? match = null;
        if (_pending?.RequestId == reply.RequestId)
        {
            match = _pending;
            _pending = null;
        }
        else if (_late?.RequestId == reply.RequestId)
        {
            match = _late;
            _late = null;
        }
        if (match == null)
            return;

        CopyBalance(reply);
        try
        {
            match.OnReply(reply);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[GuestTrade] showing the host's answer to {reply.Action} failed: {ex.Message}");
        }
    }

    private void CopyBalance(GuestTradeReply reply)
    {
        if (reply.Balance is not { } balance || string.IsNullOrEmpty(reply.ResourceId))
            return;
        if (float.IsNaN(balance) || float.IsInfinity(balance) || balance < 0f)
            return;
        var heroId = _player.GetMainHeroId();
        if (string.IsNullOrEmpty(heroId))
            return;
        _storage.Set(heroId, reply.ResourceId!, balance);
    }
}
