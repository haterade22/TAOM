using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The launch check for gear sets (D34, phase4-plan.md). A kit is checked only when the lord spawns (<c>BuildKit</c> logs a skipped slot) and the launcher checks only troops, so an item an art drop
/// removed would otherwise show up as a half-dressed lord mid-run. This finds every set that names an item the spawn would refuse (one the object manager lacks, or one that does not fit its slot,
/// the two tests <c>BuildKit</c> applies), logs one warning per set, and returns the set ids: the plan hides those sets for the run.
/// </summary>
internal static class LongDefeatGearItems
{
    /// <summary>The ids of the plan's gear sets with an item the spawn would refuse, in shop order, one warning each.</summary>
    public static IReadOnlyList<string> Missing(RunPlan plan, MBObjectManager? objects, IModLogger logger) =>
        Missing(plan, (slot, id) => FitsSlot(slot, objects?.GetObject<ItemObject>(id)), logger);

    /// <summary>The same check over any test of whether an item exists and fits a slot (by its XML name): the engine overload hands it the object manager's and the engine's own.</summary>
    internal static IReadOnlyList<string> Missing(RunPlan plan, Func<string, string, bool> itemFitsSlot, IModLogger logger)
    {
        var hidden = new List<string>();
        foreach (var set in plan.Armoury?.Sets ?? (IReadOnlyList<GearSet>)Array.Empty<GearSet>())
        {
            var gone = new List<string>();
            foreach (var slot in set.Slots)
            {
                var named = $"{slot.Slot}={slot.ItemId}";
                if (!itemFitsSlot(slot.Slot, slot.ItemId) && !gone.Contains(named))
                    gone.Add(named);
            }

            if (gone.Count == 0)
                continue;

            hidden.Add(set.Id);
            logger.LogWarning($"[LongDefeat] gear set '{set.Id}' is hidden for this run: the object manager has no such item, or it does not fit the slot [{string.Join(", ", gone)}]");
        }

        return hidden;
    }

    // BuildKit's own rule (LongDefeatBattlefieldAdapter.Player.cs): the slot name goes through the engine's mapping (Item0 is not an EquipmentIndex member), a null item is refused,
    // and Equipment.IsItemFitsToSlot decides the rest. A set holds no horse slot (GearSlots), so BuildKit's rideable test never applies.
    private static bool FitsSlot(string slot, ItemObject? item)
    {
        if (item == null)
            return false;

        try
        {
            var index = Equipment.GetEquipmentIndexFromOldEquipmentIndexName(slot);
            return Equipment.IsItemFitsToSlot(index, item);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
