using System;
using System.Collections.Generic;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Pure slot arithmetic for recruiting from a notable whose volunteers would desert the buyer. The engine lets a
/// buyer take the volunteer slots up to an index, so the only way to keep a troop that would desert out of reach is
/// to cap that index before the first such slot. Slots are given as their troops' culture ids (null = empty slot).
/// </summary>
public static class DesertionRecruitCap
{
    /// <summary>
    /// The index of the first filled slot whose troop would desert, or <c>slotCultureIds.Count</c> when none would.
    /// </summary>
    public static int FirstDeserterSlot(IReadOnlyList<string?>? slotCultureIds, Func<string?, bool> wouldDesert)
    {
        if (slotCultureIds == null)
            return 0;
        for (var i = 0; i < slotCultureIds.Count; i++)
        {
            var culture = slotCultureIds[i];
            if (culture != null && wouldDesert(culture))
                return i;
        }

        return slotCultureIds.Count;
    }

    /// <summary>
    /// <c>MaximumIndexHeroCanRecruitFromHero</c> is an inclusive index (the engine checks <c>index &lt;= max</c>;
    /// one VM checks <c>index &lt; max</c>, which is stricter, so both stay safe). -1 recruits nothing.
    /// </summary>
    public static int CapInclusiveIndex(int vanillaIndex, int firstDeserterSlot)
        => Math.Min(vanillaIndex, firstDeserterSlot - 1);

    /// <summary>
    /// <c>MaximumIndexGarrisonCanRecruitFromHero</c> is a slot count (the garrison loop runs <c>i &lt; max</c>).
    /// </summary>
    public static int CapSlotCount(int vanillaCount, int firstDeserterSlot)
        => Math.Min(vanillaCount, firstDeserterSlot);

    /// <summary>
    /// The end (exclusive) of the slots the AI trip estimate may count for one notable: vanilla's own window, stopped
    /// at the first slot that would desert, as the recruit itself stops there.
    /// </summary>
    public static int EstimateSlotEnd(int slotsConsidered, int slotCount, int firstDeserterSlot)
        => Math.Max(0, Math.Min(Math.Min(slotsConsidered, slotCount), firstDeserterSlot));
}
