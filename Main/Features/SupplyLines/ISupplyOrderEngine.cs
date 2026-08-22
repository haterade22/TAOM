using TAOM.Features.SupplyLines.Domain;

namespace TAOM.Features.SupplyLines;

/// <summary>What the hourly advance decided for one order. Pure data.</summary>
public enum SupplyOrderVerdict
{
    /// <summary>Nothing to do this tick.</summary>
    Continue = 0,

    /// <summary>Hand the cargo to the player and retire the order.</summary>
    Deliver = 1,

    /// <summary>The caravan is gone (destroyed, raided): cargo and money are lost.</summary>
    Lose = 2,
}

/// <summary>
/// The order state machine's decisions, separated from campaign state so every branch is testable.
/// The campaign behavior gathers the inputs and applies the verdicts; nothing in here touches
/// TaleWorlds types.
/// </summary>
public interface ISupplyOrderEngine
{
    /// <summary>
    /// Hourly decision for an in-transit order.
    /// </summary>
    /// <param name="elapsedFraction">Elapsed over planned travel time (1.0 = nominal arrival).</param>
    /// <param name="caravanExists">The caravan party still exists on the map.</param>
    /// <param name="caravanInRaidEvent">The caravan is currently being raided.</param>
    /// <param name="distanceToPlayer">Map distance caravan → player, or float.MaxValue if unknown.</param>
    /// <param name="playerInEncounter">The player is in a battle, siege or encounter. Deliveries
    /// never land mid-encounter (the source module's 2x timeout delivered recruits into sieges).</param>
    SupplyOrderVerdict Advance(
        float elapsedFraction,
        bool caravanExists,
        bool caravanInRaidEvent,
        float distanceToPlayer,
        bool playerInEncounter);

    /// <summary>Distance at which a caravan meeting the player counts as delivered.</summary>
    float DeliveryRange { get; }

    /// <summary>Elapsed-over-planned fraction past which delivery is forced (stuck-caravan failsafe).</summary>
    float ForceDeliverFraction { get; }
}
