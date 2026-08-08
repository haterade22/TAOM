using System;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Domain;

/// <summary>What the commander's column is doing right now.</summary>
public enum CommanderActivity
{
    Marching = 0,
    InSettlement = 1,
    InBattle = 2,
    Besieging = 3,
    Unavailable = 4,
}

/// <summary>
/// Everything the service wait menu displays, as one comparable value.
///
/// THE VALUE EQUALITY IS THE THROTTLE. The board is rebuilt on a slow cadence and the text is only
/// re-pushed when this differs from the last one, so a field left out of <see cref="Equals"/> is a
/// field whose changes never reach the screen. Add a field, add it to Equals, add it to
/// GetHashCode, add a DataRow to <c>Equals_IsFalse_WhenAnySingleFieldDiffers</c> — all four, or
/// none.
///
/// COROLLARY, and the reason it is worth spelling out: every field here must compare by VALUE. A
/// collection property would compare by reference, so a freshly built model would differ from an
/// identical one and the board would re-push its text on every pass — the throttle would look
/// present and do nothing. That is why the promotion ladder contributes a single
/// <see cref="NextRequirementKey"/> string rather than the evaluator's list of unmet keys.
/// </summary>
public sealed class ServiceStatusModel : IEquatable<ServiceStatusModel>
{
    public string CommanderName { get; }
    public CommanderActivity Activity { get; }
    public string SettlementName { get; }
    public ServiceRank Rank { get; }
    public ServiceAssignment Assignment { get; }
    public int DaysServed { get; }
    public int Trust { get; }
    public int DeferredWages { get; }

    /// <summary>Data-row id of the active duty, or null. Rendered via its own localized name.</summary>
    public string ActiveDutyId { get; }

    /// <summary>Service XP earned so far — the promotion ladder's main currency, and until now the
    /// one the player had no way at all to see.</summary>
    public int ServiceXp { get; }

    /// <summary>What the commander owes for today, from the rank wage table.</summary>
    public int DailyWage { get; }

    /// <summary>
    /// The rank the ladder is currently climbing toward, or null at the top rank / when every
    /// requirement is already met (promotion lands on the next daily tick — there is nothing to ask
    /// the player for).
    /// </summary>
    public ServiceRank? NextRank { get; }

    /// <summary>
    /// The ONE unmet promotion requirement worth showing, as <c>PromotionEvaluation</c>'s key
    /// ("days" / "xp" / "leadership" / "dutySuccesses" / "trust"), or null.
    ///
    /// A SINGLE string, never the evaluator's <c>List&lt;string&gt;</c>: this type's value equality
    /// IS the board's refresh throttle, and a list compares by reference, so carrying one would
    /// make every rebuild differ and re-push the menu text on every pass.
    /// </summary>
    public string NextRequirementKey { get; }

    /// <summary>Threshold <see cref="NextRequirementKey"/> must reach. 0 when there is no key.</summary>
    public int NextRequirementTarget { get; }

    public ServiceStatusModel(
        string commanderName = null,
        CommanderActivity activity = CommanderActivity.Marching,
        string settlementName = null,
        ServiceRank rank = ServiceRank.Recruit,
        ServiceAssignment assignment = ServiceAssignment.Infantry,
        int daysServed = 0,
        int trust = 0,
        int deferredWages = 0,
        string activeDutyId = null,
        int serviceXp = 0,
        int dailyWage = 0,
        ServiceRank? nextRank = null,
        string nextRequirementKey = null,
        int nextRequirementTarget = 0)
    {
        CommanderName = commanderName;
        Activity = activity;
        SettlementName = settlementName;
        Rank = rank;
        Assignment = assignment;
        DaysServed = daysServed;
        Trust = trust;
        DeferredWages = deferredWages;
        ActiveDutyId = activeDutyId;
        ServiceXp = serviceXp;
        DailyWage = dailyWage;
        NextRank = nextRank;
        NextRequirementKey = nextRequirementKey;
        NextRequirementTarget = nextRequirementTarget;
    }

    public bool Equals(ServiceStatusModel other)
    {
        if (other == null)
            return false;

        return string.Equals(CommanderName, other.CommanderName, StringComparison.Ordinal)
            && Activity == other.Activity
            && string.Equals(SettlementName, other.SettlementName, StringComparison.Ordinal)
            && Rank == other.Rank
            && Assignment == other.Assignment
            && DaysServed == other.DaysServed
            && Trust == other.Trust
            && DeferredWages == other.DeferredWages
            && string.Equals(ActiveDutyId, other.ActiveDutyId, StringComparison.Ordinal)
            && ServiceXp == other.ServiceXp
            && DailyWage == other.DailyWage
            && NextRank == other.NextRank
            && string.Equals(NextRequirementKey, other.NextRequirementKey, StringComparison.Ordinal)
            && NextRequirementTarget == other.NextRequirementTarget;
    }

    public override bool Equals(object obj) => Equals(obj as ServiceStatusModel);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = CommanderName?.GetHashCode() ?? 0;
            hash = (hash * 397) ^ (int)Activity;
            hash = (hash * 397) ^ (SettlementName?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ (int)Rank;
            hash = (hash * 397) ^ (int)Assignment;
            hash = (hash * 397) ^ DaysServed;
            hash = (hash * 397) ^ Trust;
            hash = (hash * 397) ^ DeferredWages;
            hash = (hash * 397) ^ (ActiveDutyId?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ ServiceXp;
            hash = (hash * 397) ^ DailyWage;
            hash = (hash * 397) ^ (NextRank.HasValue ? (int)NextRank.Value : -1);
            hash = (hash * 397) ^ (NextRequirementKey?.GetHashCode() ?? 0);
            hash = (hash * 397) ^ NextRequirementTarget;
            return hash;
        }
    }
}
