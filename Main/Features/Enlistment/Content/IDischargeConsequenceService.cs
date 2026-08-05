using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>
/// End-of-service consequence policy, keyed by <see cref="DischargeReason"/>. Runs AFTER
/// the discharge pipeline completed (record cleared, party presence restored) and must
/// never re-enter service state. Honorable exits settle the column's outstanding arrears;
/// desertion forfeits them.
/// </summary>
public interface IDischargeConsequenceService
{
    void ApplyConsequences(DischargeReason reason);
}
