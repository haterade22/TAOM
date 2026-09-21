using System;

namespace TAOM.Features.CultureConversion.GarrisonSwap.Domain;

/// <summary>
/// The four militia slots the engine spawns settlement militia from. Named to match
/// <c>CultureObject</c>'s own properties: there is no single "elite militia troop" on the engine
/// type, the elite pair is split melee/ranged.
/// </summary>
public enum MilitiaSlot
{
    MeleeBasic = 0,
    MeleeElite = 1,
    RangedBasic = 2,
    RangedElite = 3,
}

/// <summary>
/// One culture's four militia troop ids, read at the boundary from <c>CultureObject</c>'s
/// <c>MeleeMilitiaTroop</c> / <c>MeleeEliteMilitiaTroop</c> / <c>RangedMilitiaTroop</c> /
/// <c>RangedEliteMilitiaTroop</c>.
///
/// Militia is the one roster that does NOT need tier-and-role matching: the engine's daily top-up
/// (<c>Settlement.AddMilitiasToParty</c>) only ever draws from these four slots, so a militia stack
/// can be mapped slot-for-slot onto the new culture's equivalent. Of the 24 cultures in
/// <c>taom_spcultures.xml</c>, 16 author the full set and 8 (the minor factions) author none, so a
/// resolved instance can still be entirely empty: see <see cref="IsEmpty"/>. Any single id is
/// nullable too, and a culture that omits one falls back to the garrison ladder for that slot rather
/// than losing the stack.
/// </summary>
public sealed class CultureMilitiaTroops
{
    public string CultureId { get; }
    public string? MeleeBasicTroopId { get; }
    public string? MeleeEliteTroopId { get; }
    public string? RangedBasicTroopId { get; }
    public string? RangedEliteTroopId { get; }

    public CultureMilitiaTroops(
        string cultureId,
        string? meleeBasicTroopId,
        string? meleeEliteTroopId,
        string? rangedBasicTroopId,
        string? rangedEliteTroopId)
    {
        CultureId = cultureId;
        MeleeBasicTroopId = meleeBasicTroopId;
        MeleeEliteTroopId = meleeEliteTroopId;
        RangedBasicTroopId = rangedBasicTroopId;
        RangedEliteTroopId = rangedEliteTroopId;
    }

    /// <summary>
    /// True when this culture authors no militia troop at all, which every minor faction does.
    /// A caller testing only for a null instance would miss these, because the culture resolves
    /// perfectly well and simply has four empty slots.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(MeleeBasicTroopId) && string.IsNullOrEmpty(MeleeEliteTroopId)
        && string.IsNullOrEmpty(RangedBasicTroopId) && string.IsNullOrEmpty(RangedEliteTroopId);

    /// <summary>Which slot this troop id occupies for this culture, or null when it is not a militia troop of it.</summary>
    public MilitiaSlot? SlotOf(string troopId)
    {
        if (string.IsNullOrEmpty(troopId))
            return null;
        if (string.Equals(troopId, MeleeBasicTroopId, StringComparison.Ordinal)) return MilitiaSlot.MeleeBasic;
        if (string.Equals(troopId, MeleeEliteTroopId, StringComparison.Ordinal)) return MilitiaSlot.MeleeElite;
        if (string.Equals(troopId, RangedBasicTroopId, StringComparison.Ordinal)) return MilitiaSlot.RangedBasic;
        if (string.Equals(troopId, RangedEliteTroopId, StringComparison.Ordinal)) return MilitiaSlot.RangedElite;
        return null;
    }

    /// <summary>This culture's troop for the given slot, or null when it authors none.</summary>
    public string? TroopFor(MilitiaSlot slot) => slot switch
    {
        MilitiaSlot.MeleeBasic => MeleeBasicTroopId,
        MilitiaSlot.MeleeElite => MeleeEliteTroopId,
        MilitiaSlot.RangedBasic => RangedBasicTroopId,
        MilitiaSlot.RangedElite => RangedEliteTroopId,
        _ => null,
    };
}
