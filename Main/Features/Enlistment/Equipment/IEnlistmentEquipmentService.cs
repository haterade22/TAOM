namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Issues the (culture, rank) service-equipment roster to the player's party
/// INVENTORY, once per rank. Inventory, never equipped: Equipment.FillFrom
/// replaces all 12 equipped slots, which would destroy the player's own kit —
/// the soldier receives gear and chooses what to wear.
/// </summary>
public interface IEnlistmentEquipmentService
{
    /// <param name="cultureId">RUNTIME culture StringId of the commander's faction
    /// (vlandia/empire/aserai/khuzait/sturgia/battania for the XSLT cultures — never lore names).</param>
    EquipmentIssueResult IssueForRank(string cultureId, EnlistmentRank rank);
}
