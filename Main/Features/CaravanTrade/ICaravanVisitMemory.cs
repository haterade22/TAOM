namespace TAOM.Features.CaravanTrade;

/// <summary>
/// Per-caravan short-term memory of the last few towns each caravan visited, used by the
/// <c>GetTradeScoreForTown</c> reweight to penalize just-visited towns so caravans stop
/// shuttling between the nearest two and circulate instead.
///
/// Pure + TaleWorlds-free (ADR-007): the thin <see cref="CaravanVisitMemoryBehavior"/> and the
/// score hook convert sealed engine types (<c>MobileParty</c>/<c>Settlement</c>) to their
/// <c>StringId</c> at the boundary. State is ephemeral (no <c>SyncData</c>) — it rebuilds as
/// caravans move, matching the feature's save-clean design.
///
/// Replaces the previous <c>isJustLeft = town == LastVisitedSettlement</c> logic, which was inert:
/// <c>LastVisitedSettlement</c> equals the caravan's CURRENT (parked) town at decision time, and
/// vanilla already excludes the current town from candidates — so the old anti-shuttle penalty
/// never fired on a selectable town.
/// </summary>
public interface ICaravanVisitMemory
{
    /// <summary>Record that <paramref name="caravanId"/> just entered town <paramref name="townId"/>.</summary>
    void RecordVisit(string caravanId, string townId);

    /// <summary>
    /// Multiplicative recency penalty for <paramref name="townId"/> as a destination for
    /// <paramref name="caravanId"/>. Returns <c>1.0</c> for a never/long-ago-visited town (no
    /// penalty) and a smaller value (down to a strictly-positive floor) for a recently-visited one.
    /// Range is <c>(0, 1]</c> — never zero, so a penalized town in a sparse region still scores
    /// positive and can be picked (no stranding).
    /// </summary>
    float GetRecencyPenaltyFactor(string caravanId, string townId);

    /// <summary>Drop all memory for a caravan (called when the caravan is destroyed).</summary>
    void Clear(string caravanId);

    /// <summary>
    /// Drop ALL caravans' memory. Called at session launch (new game or load) because this service is a
    /// process-level singleton whose rings would otherwise survive an in-process campaign switch and,
    /// since <c>MobileParty.StringId</c> is reused across campaigns, penalize towns visited in a prior
    /// or abandoned campaign.
    /// </summary>
    void ClearAll();
}
