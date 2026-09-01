using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Issues the (culture, assignment, rank) service-equipment roster to the player's party
/// INVENTORY, once per rank. Inventory, never equipped: Equipment.FillFrom replaces all 12
/// equipped slots, which would destroy the player's own kit — the soldier receives gear and
/// chooses what to wear.
///
/// The kit carries weapons as well as armour since #525, and the assignment is part of the
/// lookup because a weapon is only right if it matches the role the player chose. The LEDGER is
/// still keyed on rank alone: a draw is spent per rank, at whatever assignment was held at the
/// time, so swapping role does not re-open one.
/// </summary>
public interface IEnlistmentEquipmentService
{
    /// <param name="cultureId">RUNTIME culture StringId of the commander's faction
    /// (vlandia/empire/aserai/khuzait/sturgia/battania for the XSLT cultures — never lore names).</param>
    EquipmentIssueResult IssueForRank(
        string cultureId, ServiceAssignment assignment, EnlistmentRank rank);
}
