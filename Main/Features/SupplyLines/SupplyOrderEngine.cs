using TAOM.Core.Validation;

namespace TAOM.Features.SupplyLines;

/// <summary>
/// Hourly decision logic for an in-transit order, ported from the SupplyLines module's
/// <c>Tick()</c> (loss + timeout) and its route tick (proximity delivery), with two source
/// defects fixed rather than carried:
///
/// <para>1. The source's 2x-timeout delivery ran unconditionally, so a stuck caravan handed
/// recruits to a player mid-siege. Both delivery branches are now gated on
/// <c>playerInEncounter</c> being false; a blocked delivery just waits.</para>
///
/// <para>2. The source compared raw campaign-time arithmetic with no finiteness guards. Here a
/// NaN or infinite elapsed fraction or distance fails the positive-requirement gates and takes
/// the safe branch (Continue): a corrupt number must never trigger a delivery or a loss.</para>
/// </summary>
public sealed class SupplyOrderEngine : ISupplyOrderEngine
{
    // Source: route tick delivered at Distance < 1.2f. Kept as the meeting range.
    public float DeliveryRange => 1.2f;

    // Source: Tick() force-delivered at elapsed/planned >= 2.0 (the route tick used 1.5; the
    // hourly 2.0 is the one that governed orders with no route, so it is the one ported).
    public float ForceDeliverFraction => 2f;

    public SupplyOrderVerdict Advance(
        float elapsedFraction,
        bool caravanExists,
        bool caravanInRaidEvent,
        float distanceToPlayer,
        bool playerInEncounter)
    {
        // Loss outranks everything, matching the source: a missing or raided caravan is gone
        // regardless of what the player is doing.
        if (!caravanExists)
            return SupplyOrderVerdict.Lose;
        if (caravanInRaidEvent)
            return SupplyOrderVerdict.Lose;

        // Deliveries never land mid-encounter (defect fix 1). The order keeps waiting; the
        // caravan still exists, so nothing is lost by holding it.
        if (playerInEncounter)
            return SupplyOrderVerdict.Continue;

        // Positive-form comparisons only: NaN fails both and falls through to Continue.
        // The explicit finiteness gates also reject the infinities, since -Infinity would pass
        // the range comparison and +Infinity would pass the fraction comparison (defect fix 2).
        // float.MaxValue is the documented "distance unknown" sentinel and correctly Continues.
        if (FiniteFloatValidator.IsFinite(distanceToPlayer) && distanceToPlayer <= DeliveryRange)
            return SupplyOrderVerdict.Deliver;

        if (FiniteFloatValidator.IsFinite(elapsedFraction) && elapsedFraction >= ForceDeliverFraction)
            return SupplyOrderVerdict.Deliver;

        return SupplyOrderVerdict.Continue;
    }
}
