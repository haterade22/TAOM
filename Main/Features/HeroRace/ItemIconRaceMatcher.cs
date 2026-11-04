using System;

namespace TAOM.Features.HeroRace;

/// <summary>
/// Decides which race an armour item was modelled for, and which icon camera slot it renders in.
/// Pure and engine-free so the matching rules are unit-tested without a running game.
///
/// <para><b>Why the icon needs a race at all.</b> Vanilla renders every armour icon by placing the
/// item's own mesh at a fixed spot in the tableau scene (<c>helmet_frame</c>, <c>armor_frame</c>, ...)
/// and shooting it with a fixed camera (<c>helmet_cam</c>, <c>armor_cam</c>, ...). Both are set up for
/// human-proportioned meshes. A dwarf helmet is modelled in dwarf body space, so it sits at dwarf head
/// height, below the point the camera aims at, and the icon shows mostly empty background.</para>
///
/// <para><b>Why names, not a race field.</b> An <c>ItemObject</c> carries no race. The dwarven armour in
/// LOTRLOME_Armory is recognisable by its ids and mesh names (<c>sk_dwarf_erebor_helmet_...</c>,
/// <c>sk_dwarf_iron_chest_...</c>) and by its culture (<c>erebor</c>, <c>iron_hills</c>). The mesh is
/// the geometry that is actually rendered, so it decides whenever the item has one: a human mesh on
/// an item whose id names a dwarf (<c>hon_ring_dwarf_1</c> wears <c>leather_gloves</c>) stays human.
/// Only an item without a mesh name falls back to its id and culture. A realm token matches as well
/// as the race token, so a mesh named after Erebor rather than after dwarves is still dwarven.</para>
/// </summary>
public static class ItemIconRaceMatcher
{
    /// <summary>Race whose armour is reframed. It is also the first half of every icon row key.</summary>
    public const string DwarfRace = "dwarf";

    /// <summary>
    /// Substrings that mark an item as dwarven: the race itself and the dwarven realms. Matched
    /// case-insensitively against the item id, the mesh name and the culture id.
    /// </summary>
    internal static readonly string[] DwarfTokens =
    {
        "dwarf",
        "erebor",
        "iron_hills",
        "ironhills",
        "ered_luin",
        "blue_mountains",
        "khazad",
    };

    /// <summary>
    /// Every race <see cref="MatchRace"/> can return. An icon row for any other race is never read,
    /// so the tuning console refuses one.
    /// </summary>
    public static readonly string[] Races = { DwarfRace };

    /// <summary>
    /// Icon camera slots, named exactly as vanilla names the scene tags (<c>&lt;slot&gt;_cam</c>,
    /// <c>&lt;slot&gt;_frame</c>). Only armour slots: weapons, shields and goods are not race-shaped.
    /// </summary>
    public static readonly string[] Slots = { "helmet", "armor", "boot", "glove", "cape" };

    /// <summary>
    /// The race an item was modelled for, or null when nothing marks it as non-human. The mesh name
    /// decides when there is one; id and culture count only for an item without a mesh name.
    /// </summary>
    public static string MatchRace(string itemId, string meshName, string cultureId)
    {
        var dwarven = string.IsNullOrEmpty(meshName)
            ? ContainsAny(itemId, DwarfTokens) || ContainsAny(cultureId, DwarfTokens)
            : ContainsAny(meshName, DwarfTokens);

        return dwarven ? DwarfRace : null;
    }

    /// <summary>True when <paramref name="race"/> is one of <see cref="Races"/>.</summary>
    public static bool IsRace(string race)
        => Array.IndexOf(Races, race) >= 0;

    /// <summary>The config row key for a race and slot, for example <c>dwarf_helmet</c>.</summary>
    public static string RowKey(string race, string slot)
        => string.IsNullOrEmpty(race) || string.IsNullOrEmpty(slot) ? null : race + "_" + slot;

    /// <summary>True when <paramref name="slot"/> is one of <see cref="Slots"/>.</summary>
    public static bool IsSlot(string slot)
        => Array.IndexOf(Slots, slot) >= 0;

    /// <summary>
    /// Splits a row key into race and slot. The slot is the part after the LAST underscore, so a race
    /// name with an underscore (<c>cave_troll_helmet</c>) still splits correctly.
    /// </summary>
    public static bool TrySplitRowKey(string rowKey, out string race, out string slot)
    {
        race = null;
        slot = null;

        if (string.IsNullOrEmpty(rowKey))
            return false;

        var cut = rowKey.LastIndexOf('_');
        if (cut <= 0 || cut == rowKey.Length - 1)
            return false;

        race = rowKey.Substring(0, cut);
        slot = rowKey.Substring(cut + 1);
        return IsSlot(slot);
    }

    private static bool ContainsAny(string value, string[] tokens)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        foreach (var token in tokens)
        {
            if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
