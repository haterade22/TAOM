namespace TAOM.Features.NavalTravel;

/// <summary>What the load-time repair does with one party (docs/features/naval-travel.md "At-sea repair").</summary>
public enum AtSeaRepairAction
{
    /// <summary>Nothing to repair, or vanilla moves it: on land, attached to an army, able to sail, or mid-transition.</summary>
    None,

    /// <summary>In a MapEvent or a siege: the engine owns its position, so it stays at sea this session.</summary>
    SkipEngineOwned,

    /// <summary>Inside a settlement: back to that settlement's gate.</summary>
    MoveToOwnSettlementGate,

    /// <summary>Free on the map: to the gate of the nearest town, castle or village.</summary>
    MoveToNearestGate,
}

/// <summary>
/// Pure decision for <see cref="AtSeaRepairBehavior"/>. No engine types, so it is unit-testable.
/// </summary>
public static class AtSeaRepair
{
    public static AtSeaRepairAction Decide(bool isAtSea, bool isAttached, bool hasNavalCapability, bool inTransition,
        bool inMapEvent, bool inSiege, bool inSettlement)
    {
        // Attached: DisembarkToPosition on the army leader moves the attached parties with it.
        // Naval capability: a naval model is registered again and the party can sail, so it is not stranded.
        // Transition: vanilla finishes it on the first tick by flipping the flag, which would undo a repair.
        if (!isAtSea || isAttached || hasNavalCapability || inTransition)
            return AtSeaRepairAction.None;
        if (inMapEvent || inSiege)
            return AtSeaRepairAction.SkipEngineOwned;
        return inSettlement ? AtSeaRepairAction.MoveToOwnSettlementGate : AtSeaRepairAction.MoveToNearestGate;
    }
}
