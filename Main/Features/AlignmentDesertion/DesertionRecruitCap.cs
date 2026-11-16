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
    /// <summary>Returned by <see cref="FirstDeserterSlot"/> when no slot would desert (or there are no slots): nothing is capped.</summary>
    public const int NoDeserter = int.MaxValue;

    /// <summary>
    /// The index of the first filled slot whose troop would desert, or <see cref="NoDeserter"/> when none would.
    /// </summary>
    public static int FirstDeserterSlot(IReadOnlyList<string?>? slotCultureIds, Func<string?, bool> wouldDesert)
    {
        if (slotCultureIds == null)
            return NoDeserter;
        for (var i = 0; i < slotCultureIds.Count; i++)
        {
            var culture = slotCultureIds[i];
            if (culture != null && wouldDesert(culture))
                return i;
        }

        return NoDeserter;
    }

    /// <summary>
    /// <c>MaximumIndexHeroCanRecruitFromHero</c> has two readers with two conventions. The player paths are inclusive:
    /// <c>HeroHelper.HeroCanRecruitFromHero</c> returns <c>index &lt;= max</c> (HeroHelper.cs:517) and
    /// <c>RecruitVolunteerVM</c> (line 173) reaches it through HeroHelper; <c>RecruitVolunteerTroopVM</c> line 290
    /// uses <c>index &lt; max</c> only to find the tooltip's required relation. The AI recruit loop is an exclusive bound:
    /// <c>RecruitmentCampaignBehavior.RecruitVolunteersFromNotable</c> breaks on <c>if (num5 &gt;= num3)</c> (line 544)
    /// with <c>num5 = num4 % 6</c>. A deserter at slot k therefore caps a player buyer at k - 1 and an AI buyer at k.
    /// No deserter returns vanilla's value unchanged; a cap never raises it, and -1 recruits nothing.
    /// </summary>
    public static int CapVolunteerIndex(int vanillaIndex, int firstDeserterSlot, bool buyerIsPlayer)
    {
        if (firstDeserterSlot == NoDeserter)
            return vanillaIndex;
        return Math.Min(vanillaIndex, buyerIsPlayer ? firstDeserterSlot - 1 : firstDeserterSlot);
    }

    /// <summary>
    /// <c>MaximumIndexGarrisonCanRecruitFromHero</c> is a slot count (the garrison loop runs <c>i &lt; max</c>).
    /// </summary>
    public static int CapSlotCount(int vanillaCount, int firstDeserterSlot)
        => firstDeserterSlot == NoDeserter ? vanillaCount : Math.Min(vanillaCount, firstDeserterSlot);

    /// <summary>
    /// The end (exclusive) of the slots the AI trip estimate may count for one notable: vanilla's own window, stopped
    /// at the first slot that would desert, as the recruit itself stops there.
    /// </summary>
    public static int EstimateSlotEnd(int slotsConsidered, int slotCount, int firstDeserterSlot)
        => Math.Max(0, Math.Min(Math.Min(slotsConsidered, slotCount), firstDeserterSlot));

    /// <summary>
    /// What the player's cap does to slot <paramref name="index"/> (#1189). The cap stops before the first deserter, so
    /// a deserter and every slot after it stay locked at any relation. The slot just before the first deserter still
    /// opens with relation, but the recruit VM's relation search (<c>index &lt; max</c>) cannot reach it under the cap.
    /// </summary>
    public static SlotLockReason LockReason(int index, int firstDeserterSlot, bool slotDeserts)
    {
        if (slotDeserts)
            return SlotLockReason.WouldDesert;
        if (firstDeserterSlot == NoDeserter || index < firstDeserterSlot - 1)
            return SlotLockReason.None;
        return index == firstDeserterSlot - 1 ? SlotLockReason.BeforeDeserter : SlotLockReason.BehindDeserter;
    }

    /// <summary>
    /// The lowest relation, from -100 to 99, at which the player may recruit slot <paramref name="index"/>: the first
    /// one where <c>index &lt;= maxIndexAtRelation(relation)</c>, as <c>HeroHelper.HeroCanRecruitFromHero</c> tests it.
    /// Null when no relation in that range opens the slot.
    /// </summary>
    public static int? RequiredRelation(int index, Func<int, int> maxIndexAtRelation)
    {
        for (var relation = -100; relation < 100; relation++)
        {
            if (index <= maxIndexAtRelation(relation))
                return relation;
        }
        return null;
    }
}

/// <summary>Why a volunteer slot is locked for the player by the desertion cap.</summary>
public enum SlotLockReason
{
    /// <summary>The cap does not lock it; any lock is vanilla's relation rule.</summary>
    None,

    /// <summary>This volunteer would desert the player's party.</summary>
    WouldDesert,

    /// <summary>An earlier volunteer would desert, and the player cannot recruit past them.</summary>
    BehindDeserter,

    /// <summary>The next volunteer would desert; relation opens this slot, but vanilla's hint cannot compute it.</summary>
    BeforeDeserter,
}
