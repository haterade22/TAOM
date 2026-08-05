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
}
