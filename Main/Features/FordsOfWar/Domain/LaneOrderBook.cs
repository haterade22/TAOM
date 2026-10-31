using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The standing order per lane per side (Push until told otherwise) and the cooldown between changes. Both the human
/// panel and the AI go through <see cref="TrySet"/>. Time is the match clock in seconds.
/// </summary>
internal sealed class LaneOrderBook
{
    private readonly FordsLaneConfig _config;
    private readonly Dictionary<(MatchSide, int), LaneOrder> _orders = new();
    private readonly Dictionary<(MatchSide, int), float> _readyAt = new();

    public LaneOrderBook(FordsLaneConfig config)
    {
        _config = config;
    }

    public LaneOrder Order(MatchSide side, int lane) =>
        _orders.TryGetValue((side, lane), out var order) ? order : LaneOrder.Push;

    public float CooldownLeft(MatchSide side, int lane, float now) =>
        _readyAt.TryGetValue((side, lane), out var readyAt) ? Math.Max(0f, readyAt - now) : 0f;

    /// <summary>Re-issuing the standing order is a free no-op; a real change costs the cooldown.</summary>
    public CommandResult TrySet(MatchSide side, int lane, LaneOrder order, float now)
    {
        if (Order(side, lane) == order)
            return CommandResult.Ok;
        if (CooldownLeft(side, lane, now) > 0f)
            return CommandResult.OnCooldown;

        _orders[(side, lane)] = order;
        _readyAt[(side, lane)] = now + _config.OrderCooldownSeconds;
        return CommandResult.Ok;
    }
}
