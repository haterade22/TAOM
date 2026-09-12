using System.Collections.Generic;

namespace TAOM.Features.FiefGranting;

/// <summary>
/// Who fought for a settlement the last time it was stormed, and how much (#565).
///
/// Vanilla remembers one clan: <c>Town.LastCapturedBy</c>, the assault leader's, written by
/// <c>ChangeOwnerOfSettlementAction.ApplyBySiege</c> and never cleared. The election needs every
/// clan that fielded a party in the winning assault, weighted by the engine's own
/// <c>MapEventParty.ContributionToBattle</c>, the number the loot split uses. The campaign behavior
/// records that at <c>MapEventEnded</c> and forgets it on any ownership change that is not a siege,
/// so a record cannot outlive the capture it describes.
///
/// Primitives only (ADR-007): settlement and clan <c>StringId</c>s in, a share out.
/// </summary>
public interface IFiefSiegeParticipationService
{
    /// <summary>
    /// Replace the record for <paramref name="settlementId"/> with the winning side's parties.
    /// Contributions are summed per clan; entries with no clan id or a non-positive contribution
    /// are dropped. When nothing positive remains the settlement has no record at all.
    /// </summary>
    void RecordAssault(string settlementId, IReadOnlyList<KeyValuePair<string, int>> partyContributions);

    /// <summary>Drop the record, if any: the settlement changed hands by something other than a siege.</summary>
    void Forget(string settlementId);

    /// <summary>True when at least one clan is on record for the settlement.</summary>
    bool HasRecord(string settlementId);

    /// <summary>
    /// The clan's contribution over the top clan's contribution, in (0, 1]; 0 when the clan was
    /// absent or the settlement has no record.
    /// </summary>
    float GetContributionShare(string settlementId, string clanId);

    /// <summary>Settlement id to <c>clanId=contribution;clanId=contribution</c>, clans sorted by id.</summary>
    Dictionary<string, string> SnapshotForSave();

    /// <summary>Replace everything held with the snapshot. Null or empty leaves the service empty.</summary>
    void RestoreFromSave(Dictionary<string, string> snapshot);

    /// <summary>Clear everything: a fresh campaign, or a save from before this feature, starts empty.</summary>
    void ResetForNewSession();
}
