using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// D35 (phase4-plan.md): the lord's effective loadout. The class kit's slots are laid over his first battle set at the spawn, so the effective loadout is the same slot list with each owned set laid
/// over it in purchase order, a later set winning a slot. A weapons set replaces all four weapon slots as a block, clearing any it does not name, so a kit shield never stays beside a set's polearm.
/// A set the class is not offered is skipped, and no horse slot ever comes from a set. Pure: the inputs are not changed.
/// </summary>
internal static class LoadoutMerge
{
    /// <summary>The kit with the owned sets laid over it, or null when there is no kit (the lord plays in his own gear and buys nothing). Slots keep the kit's order; a slot the kit never named is appended.</summary>
    public static LdLoadout? Merge(LdLoadout? kit, IReadOnlyList<GearSet> owned)
    {
        if (kit == null)
            return null;

        var slots = new List<LdKitSlot>(kit.Slots);
        foreach (var set in owned)
        {
            if (!set.OfferedTo(kit.ClassId))
                continue;

            if (set.Kind == GearKind.Weapons)
            {
                foreach (var weapon in GearSlots.Weapons)
                    Put(slots, weapon, "");
            }

            foreach (var slot in set.Slots)
            {
                if (!KitSlots.IsMount(slot.Slot))
                    Put(slots, slot.Slot, slot.ItemId);
            }
        }

        return kit with { Slots = slots };
    }

    private static void Put(List<LdKitSlot> slots, string slot, string itemId)
    {
        var at = slots.FindIndex(x => string.Equals(x.Slot, slot, StringComparison.Ordinal));
        if (at >= 0)
            slots[at] = new LdKitSlot(slot, itemId);
        else
            slots.Add(new LdKitSlot(slot, itemId));
    }
}
