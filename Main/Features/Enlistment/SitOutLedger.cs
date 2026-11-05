namespace TAOM.Features.Enlistment;

/// <summary>
/// The last sit-out verdict the encounter menu produced (#830), for the battle-end payout to read.
/// A singleton, so it is per-process, not per-campaign and never saved: the payout consumes it on every
/// battle and <c>ServiceMaintenanceService.ResetSessionCaches</c> consumes it between campaigns.
/// Same shape as <c>IBattleMeritAccumulator</c>. <see cref="SitOutVerdict.Unchanged"/> means empty.
/// </summary>
public sealed class SitOutLedger
{
    private SitOutVerdict _pending = SitOutVerdict.Unchanged;

    public void Record(SitOutVerdict verdict) => _pending = verdict;

    /// <summary>Returns the pending verdict and resets the ledger to <see cref="SitOutVerdict.Unchanged"/>.</summary>
    public SitOutVerdict Consume()
    {
        var verdict = _pending;
        _pending = SitOutVerdict.Unchanged;
        return verdict;
    }
}
