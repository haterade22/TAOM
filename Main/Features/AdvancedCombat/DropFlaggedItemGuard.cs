namespace TAOM.Features.AdvancedCombat;

/// <summary>
/// The decision behind <c>Patch50_DropFlaggedItemGuard</c>'s Prefix, kept out of the Harmony
/// patch so it can be unit-tested (ADR-002 thin entry points, ADR-008 — a patch body has no
/// unit harness).
///
/// Vanilla <c>Agent.CheckToDropFlaggedItem</c> (Agent.cs:3595, v1.4.8) reads
/// <c>Equipment[equipmentIndex].Item.ItemFlags</c> behind a guard that tests only the INDEX:
///
/// <code>
/// if (equipmentIndex != EquipmentIndex.None &amp;&amp; Equipment[equipmentIndex].Item.ItemFlags.HasAnyFlag(ItemFlags.DropOnAnyAction))
/// </code>
///
/// Neither <c>Equipment</c> nor the resolved <c>Item</c> is checked, so two shapes throw:
/// a null <c>Equipment</c> (the indexer itself), and a wielded slot whose <c>Item</c> is null.
/// </summary>
public static class DropFlaggedItemGuard
{
    /// <summary>
    /// True when vanilla's body would dereference null for this agent, i.e. when the Prefix
    /// should skip the original.
    /// </summary>
    /// <remarks>
    /// Parameters are plain bools rather than engine types so this carries no TaleWorlds
    /// dependency. <paramref name="primaryItemPresent"/> / <paramref name="offhandItemPresent"/>
    /// are only meaningful when the matching <c>*Wielded</c> flag is set — vanilla's index guard
    /// short-circuits before the item is ever read, so an empty non-wielded slot is not a throw.
    ///
    /// Skipping is exact parity in the null-<c>Equipment</c> case (vanilla drops nothing, then
    /// throws) and in the single-bad-slot case where nothing droppable precedes the fault. It
    /// diverges only for an agent carrying a genuine <c>DropOnAnyAction</c> item in one slot AND
    /// a phantom wielded index in the other: vanilla drops the good item before throwing, this
    /// skips both. That state requires a half-built agent holding a flagged item, and vanilla is
    /// already broken there — accepted deliberately in exchange for not reimplementing the
    /// engine's loop and inheriting its drift.
    /// </remarks>
    public static bool WouldVanillaDereferenceNull(
        bool hasEquipment,
        bool primaryWielded,
        bool primaryItemPresent,
        bool offhandWielded,
        bool offhandItemPresent)
    {
        if (!hasEquipment) return true;
        if (primaryWielded && !primaryItemPresent) return true;
        if (offhandWielded && !offhandItemPresent) return true;
        return false;
    }
}
