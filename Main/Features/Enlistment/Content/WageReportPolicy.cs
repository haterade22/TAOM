namespace TAOM.Features.Enlistment.Content;

/// <summary>What the player should be told about today's pay. Empty when there is nothing to say.</summary>
public sealed class WageReport
{
    /// <summary>Gold that actually reached the player's purse today (wage + released arrears).</summary>
    public int Received { get; set; }

    /// <summary>Gold the column owes but could not pay today.</summary>
    public int Deferred { get; set; }

    /// <summary>Gold destroyed by the arrears cap — owed, unpayable, and now gone.</summary>
    public int Forfeited { get; set; }

    public bool ShouldAnnouncePayment => Received > 0;
    public bool ShouldWarn => Deferred > 0 || Forfeited > 0;
}

/// <summary>
/// Turns the daily <see cref="WageDecision"/> into something the player can actually see (field
/// report 2: "you are either not paid or it doesn't show up as a positive").
///
/// BOTH READINGS OF THE REPORT WERE TRUE, which is why this exists.
///
/// *Not paid*: <c>WagePolicy.ComputeDaily</c> pays only from commander gold above a 500 reserve, and
/// plenty of AI lords sit under it — so the wage silently becomes arrears. That is the designed
/// behaviour and it stays, but it was completely unsignalled: a paid day and an unpaid day were
/// indistinguishable from the player's chair.
///
/// *Doesn't show up*: the gold transfer passes <c>disableNotification: true</c>, and the computed
/// <c>DailySummary.Wage</c> was read by nobody — <c>OnDailyTick</c> used the summary for promotion
/// and contract expiry and dropped the wage on the floor. Gold arrived with no message attached to
/// it, in a game where every other income line announces itself.
///
/// Pure, so the arrears/forfeit arithmetic is testable without a campaign; the wording and the
/// wallet tooltip live at the boundary.
/// </summary>
public static class WageReportPolicy
{
    public static WageReport Describe(WageDecision decision)
    {
        if (decision == null)
            return new WageReport();

        return new WageReport
        {
            // TotalPaidToPlayer already sums wage + minted + released arrears — the three ways gold
            // reaches the purse. Reporting the parts separately would make a day that cleared back
            // pay read as two payments when the player sees one number change.
            Received = decision.TotalPaidToPlayer,
            Deferred = decision.NewlyDeferred,
            Forfeited = decision.Forfeited,
        };
    }
}
