namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The outcome of a commander's request (buy, field, set an order).</summary>
internal enum CommandResult
{
    Ok,
    NotEnoughGold,
    UnknownOffer,
    UnknownLane,
    LaneNotOwned,
    QueueFull,
    HeroLimit,
    HeroAlreadyFielded,
    HeroNotDead,
    OnCooldown,
    NotLive,
}
