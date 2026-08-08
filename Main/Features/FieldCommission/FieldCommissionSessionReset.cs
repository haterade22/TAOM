namespace TAOM.Features.FieldCommission;

/// <summary>
/// What Battlefield Promotions throws away when the campaign underneath it changes. Split out of
/// <c>FieldCommissionBehavior</c> so the behaviour stays a thin event router (ADR-002).
///
/// The distinction the two methods encode is the whole point: banked merit, decline marks and
/// promoted-hero ids belong to a SAVE and must survive a load; the offer queue and the "showing an
/// offer" latch belong to a PROCESS and must not survive anything. Both of the latter live on
/// <c>Reuse.Singleton</c> services and are never persisted, so left alone they cross campaign
/// boundaries — an offer earned in one save pops in the next save loaded without restarting, and a
/// latch raised when a prompt failed to close silently kills every later offer for the session.
/// </summary>
internal static class FieldCommissionSessionReset
{
    /// <summary>Transient, process-lifetime state. Safe to call on EVERY session boundary, including
    /// a load-from-save where the merit bank must be preserved.</summary>
    internal static void ClearCarriedOverOffers(
        IFieldCommissionMeritService merit,
        IFieldCommissionOfferFlowService offerFlow)
    {
        merit.ClearPendingOffers();
        offerFlow.Reset();
    }

    /// <summary>Everything, including the persisted bank. Only for a genuinely fresh campaign.</summary>
    internal static void ClearAll(
        IFieldCommissionMeritService merit,
        IFieldCommissionOfferFlowService offerFlow)
    {
        merit.ImportMerits(null);
        merit.ImportPromotedHeroIds(null);
        merit.ImportDeclinedMarks(null);
        ClearCarriedOverOffers(merit, offerFlow);
    }
}
