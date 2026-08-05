namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Outcome of an equipment issuance attempt. Only <see cref="Issued"/> advances
/// the ledger — every other outcome leaves it untouched so a later retry (next
/// rank-up tick, save-load) can still succeed.
/// </summary>
public enum EquipmentIssueResult
{
    Issued,
    AlreadyIssuedForRank,
    NoRosterFound,
    NoValidItems,
    PartyUnavailable,
}
