namespace TAOM.Features.FieldCommission;

/// <summary>
/// Orchestrates one promotion offer's inquiry chain: promote? -> companion-room check ->
/// rename -> hero creation -> merit completion. One offer at a time — the entry point's tick
/// calls <see cref="PumpNextOffer"/> only while <see cref="IsShowingOffer"/> is false and no
/// encounter/map event is active (see <c>FieldCommissionBehavior</c>).
/// </summary>
public interface IFieldCommissionOfferFlowService
{
    bool IsShowingOffer { get; }

    /// <summary>Dequeues and shows the next pending offer. No-ops when one is already showing or
    /// none are pending.</summary>
    void PumpNextOffer();

    /// <summary>
    /// Forces the "an offer is on screen" latch back down.
    ///
    /// The latch is otherwise lowered only from inside an inquiry callback, and it lives on a
    /// process-lifetime singleton — so any path that ends a session without every callback running
    /// to completion (an engine throw inside the chain, a mod or debug tool calling
    /// <c>InformationManager.HideInquiry</c>) leaves it raised and silently suppresses every
    /// promotion offer for the rest of the process, across later loads and new campaigns.
    /// Clearing it at each session boundary bounds that to a single session.
    /// </summary>
    void Reset();
}
