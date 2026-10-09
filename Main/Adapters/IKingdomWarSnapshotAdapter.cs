using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Reads the campaign's kingdoms and clock for the War Chronicle (ledger, baselines, rally) so the
/// services never touch <c>Kingdom</c>, <c>Clan</c> or <c>CampaignTime</c> (ADR-007).
/// </summary>
public interface IKingdomWarSnapshotAdapter
{
    /// <summary>One snapshot per non-eliminated kingdom; empty with no campaign.</summary>
    IReadOnlyList<KingdomWarSnapshot> GetKingdoms();

    /// <summary>The campaign's unique id (<c>Campaign.UniqueGameId</c>); empty when there is none.</summary>
    string GetCampaignId();

    /// <summary>Whole days since the campaign start, floored; 0 when unknown.</summary>
    int GetElapsedDay();

    /// <summary>Campaign time in hours (<c>CampaignTime.Now.ToHours</c>); 0 with no campaign.</summary>
    double GetNowHours();
}
