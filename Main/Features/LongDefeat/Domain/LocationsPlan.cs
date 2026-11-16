using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

// The places of Phase 4 (phase4-plan.md sections 4 and 5), as shop.json holds them: the armoury's gear sets, the engineer's yard's engine kinds. Plain data:
// LocationsReader builds it, LoadoutMerge folds the owned sets into the lord's kit, and a later slice sells from it.

/// <summary>The slot names a gear set may use (D34): the five armour slots for an armour set, the four weapon slots for a weapons set. No set touches a horse slot.</summary>
internal static class GearSlots
{
    public static IReadOnlyList<string> Armour { get; } = new[] { "Head", "Body", "Gloves", "Leg", "Cape" };

    public static IReadOnlyList<string> Weapons { get; } = new[] { "Item0", "Item1", "Item2", "Item3" };

    public static IReadOnlyList<string> Of(GearKind kind) => kind == GearKind.Weapons ? Weapons : Armour;
}

internal enum GearKind
{
    Armour,
    Weapons,
}

/// <summary>
/// One set the armoury sells: one donor troop's own items for the slots it names. <paramref name="Name"/> and <paramref name="Text"/> are raw <c>{=KEY}English</c> text (the id and empty when shop.json gives none).
/// <paramref name="Classes"/> is null for every class; a weapons set always lists its classes, so an archer is never sold away from his bow.
/// </summary>
internal sealed record GearSet(string Id, string Name, string Text, GearKind Kind, IReadOnlyList<string>? Classes, IReadOnlyList<LdKitSlot> Slots, int Price)
{
    /// <summary>Whether the set is sold to a class: an armour set to every class unless it lists some, a weapons set only to the classes it lists. No class (the lord in his own gear) is offered nothing.</summary>
    public bool OfferedTo(string? classId) =>
        !string.IsNullOrEmpty(classId) && (Classes == null || Classes.Contains(classId, StringComparer.Ordinal));
}

/// <summary>The armoury: how close the lord must stand to its recorded place, in metres, and its sets in shop.json order. At least one set.</summary>
internal sealed record ArmouryPlan(float Reach, IReadOnlyList<GearSet> Sets);

/// <summary>One engine kind the yard sells (<c>mangonel</c> or <c>trebuchet</c>) and its price.</summary>
internal sealed record EngineOffer(string Kind, int Price);

/// <summary>The engineer's yard: how close the lord must stand to its recorded place, in metres, and the engine kinds it lists. At least one.</summary>
internal sealed record YardPlan(float Reach, IReadOnlyList<EngineOffer> Engines);

/// <summary>A shop's two Phase 4 blocks; a null one is absent from shop.json (or failed its rules) and is hidden.</summary>
internal sealed record LocationsPlan(ArmouryPlan? Armoury, YardPlan? Yard);

/// <summary>
/// What this build can deliver (D46), in the <see cref="FortOffer"/> shape: the armoury and each engine kind. Each slice raises its kind when its effect lands, and the Python judge
/// mirrors it as <c>LOCATION_OFFERED</c> (a test reads this source, so the two cannot drift). Slice 1 delivers nothing, so every row of shop.json stays hidden and shipped play is unchanged.
/// </summary>
internal sealed record LocationOffer(bool Armoury, bool Mangonel, bool Trebuchet)
{
    /// <summary>What this build can deliver.</summary>
    public static LocationOffer Offered { get; } = new(false, false, false);

    /// <summary>Nothing for sale: a run that plays as it did before any location.</summary>
    public static LocationOffer None { get; } = new(false, false, false);
}
