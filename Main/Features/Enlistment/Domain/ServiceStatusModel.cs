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
/// field whose changes never reach the screen. Add a field, add it to Equals, add a DataRow to
/// <c>Equals_IsFalse_WhenAnySingleFieldDiffers</c> — all three, or none.
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

    public ServiceStatusModel(
        string commanderName = null,
        CommanderActivity activity = CommanderActivity.Marching,
        string settlementName = null,
        ServiceRank rank = ServiceRank.Recruit,
        ServiceAssignment assignment = ServiceAssignment.Infantry,
        int daysServed = 0,
        int trust = 0,
        int deferredWages = 0,
        string activeDutyId = null)
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
            && string.Equals(ActiveDutyId, other.ActiveDutyId, StringComparison.Ordinal);
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
            return hash;
        }
    }
}
