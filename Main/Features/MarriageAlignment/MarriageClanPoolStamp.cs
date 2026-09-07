namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Decides when the per-culture candidate-clan pool cache in
/// <c>Patch81_MarriageClanDraw</c> has gone stale and must be discarded.
/// </summary>
/// <remarks>
/// This lives outside the Harmony patch on purpose. It is the only part of the cache that carries
/// real decision logic (the rest is a dictionary and a loop over <c>Clan</c>), it is the part most
/// likely to be wrong, and inside a patch class it could not be reached by a test. It deals in a
/// campaign id string rather than the <c>Campaign</c> object so that a finished campaign's whole
/// object graph is not held alive by a static field until the next campaign's first daily tick.
/// <para>
/// The three components and what each one catches:
/// <list type="bullet">
/// <item><b>campaignId</b> - a second campaign started in the same process. Pools built for
/// campaign A must never be served to campaign B (plans/001-cross-campaign-singleton-resets.md).</item>
/// <item><b>clanCount</b> - clans created or eliminated. A clan's culture is only ever assigned at
/// CREATION (vanilla sets it in <c>CreateSettlementRebelClan</c> and <c>CreateCompanionToLordClan</c>,
/// and nothing in vanilla or TAOM reassigns an existing clan's culture), and every creation path
/// changes this count, so the count is what actually covers culture churn in the clan population.
/// Note this is NOT the <c>CultureConversion</c> feature, which converts SETTLEMENT cultures and
/// leaves clan cultures alone.</item>
/// <item><b>day</b> - a cheap backstop that bounds any staleness this stamp fails to model to a
/// single campaign day, and costs one rebuild per culture per day.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class MarriageClanPoolStamp
{
    private string? _campaignId;
    private int _clanCount = -1;
    private int _day = -1;

    /// <summary>
    /// True when the cache must be cleared for this observation, in which case the observation is
    /// recorded as the new stamp. Calling again with the same triple returns false, so the caller
    /// clears once and then serves from cache until something actually moves.
    /// </summary>
    /// <remarks>
    /// The initial state (<c>null</c> / -1 / -1) cannot collide with a real observation: a live
    /// campaign always has a non-negative clan count and day. So the very first call after a reset
    /// always invalidates, which is the intended behaviour and not a sentinel collision
    /// (<c>.claude/rules/harmony-patches.md</c> "Static State Machines").
    /// </remarks>
    public bool ShouldInvalidate(string? campaignId, int clanCount, int day)
    {
        if (string.Equals(_campaignId, campaignId, System.StringComparison.Ordinal)
            && _clanCount == clanCount
            && _day == day)
        {
            return false;
        }

        _campaignId = campaignId;
        _clanCount = clanCount;
        _day = day;
        return true;
    }
}
