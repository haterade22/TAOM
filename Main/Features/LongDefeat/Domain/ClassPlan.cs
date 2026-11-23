using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The slot names a class kit may use: the old XML spelling the engine's own roster files use, so <c>Item0</c> to <c>Item3</c> are the four weapon slots. The adapter maps
/// each through <c>Equipment.GetEquipmentIndexFromOldEquipmentIndexName</c>; this list only says which names a kit may write.
/// </summary>
internal static class KitSlots
{
    public const string Horse = "Horse";
    public const string HorseHarness = "HorseHarness";

    public static IReadOnlyList<string> Names { get; } = new[]
    {
        "Item0", "Item1", "Item2", "Item3", "Head", "Body", "Leg", "Gloves", "Cape", Horse, HorseHarness,
    };

    public static bool IsKnown(string name) => Names.Contains(name, StringComparer.Ordinal);

    public static bool IsMount(string name) => string.Equals(name, Horse, StringComparison.Ordinal) || string.Equals(name, HorseHarness, StringComparison.Ordinal);
}

/// <summary>One slot of a kit and the item put in it; an empty <paramref name="ItemId"/> clears the slot.</summary>
internal sealed record LdKitSlot(string Slot, string ItemId);

/// <summary>What the field gives the lord at a spawn: the class, whether it rides, and the slots to overwrite on a copy of his first battle set. A foot class always ends with both horse slots cleared.</summary>
internal sealed record LdLoadout(string ClassId, bool Mounted, IReadOnlyList<LdKitSlot> Slots);

/// <summary>
/// What the field read back from the spawned lord, for the <c>class</c> check: whether he exists, what each wanted slot held when he spawned, the slots the engine refused
/// (an unknown item, a misfit), and whether he sat a horse.
/// </summary>
internal sealed record LdKitFacts(bool Spawned, IReadOnlyList<LdKitSlot> Found, IReadOnlyList<string> Skipped, bool HasMount);

/// <summary>A class's slots for one defender culture.</summary>
internal sealed record ClassKit(IReadOnlyList<LdKitSlot> Slots);

/// <summary>One class of classes.json: TAOM's own name and one-line kit text as <c>{=KEY}English</c>, whether it rides, and a kit per defender culture.</summary>
internal sealed record RunClass(string Id, string Name, string Text, bool Mounted, IReadOnlyDictionary<string, ClassKit> Kits)
{
    private static readonly IReadOnlyDictionary<string, string> NoTexts = new Dictionary<string, string>();

    /// <summary>A text for one defender culture, where the class's own text would name another people (the Cavalry of Minas Tirith is no rider of the Mark); empty for most classes.</summary>
    public IReadOnlyDictionary<string, string> Texts { get; init; } = NoTexts;

    /// <summary>This class as a fortress of <paramref name="culture"/> shows it: the same class under that culture's text, or the class itself when it has none. The culture is matched without case.</summary>
    public RunClass For(string culture)
    {
        var text = Texts.FirstOrDefault(t => string.Equals(t.Key, culture, StringComparison.OrdinalIgnoreCase)).Value;
        return text == null ? this : this with { Text = text };
    }

    public bool HasKit(string culture) => Find(culture) != null;

    /// <summary>The loadout for a culture, or null when the class has no kit for it. A foot class gets both horse slots cleared after its kit.</summary>
    public LdLoadout? LoadoutFor(string culture)
    {
        var kit = Find(culture);
        if (kit == null)
            return null;

        var slots = kit.Slots.ToList();
        if (!Mounted)
        {
            slots.Add(new LdKitSlot(KitSlots.Horse, ""));
            slots.Add(new LdKitSlot(KitSlots.HorseHarness, ""));
        }

        return new LdLoadout(Id, Mounted, slots);
    }

    private ClassKit? Find(string culture) =>
        Kits.FirstOrDefault(k => string.Equals(k.Key, culture, StringComparison.OrdinalIgnoreCase)).Value;
}

/// <summary>The classes of classes.json in file order (phase2-plan.md section 2, "Classes"). <see cref="None"/>: the lord plays in his own gear.</summary>
internal sealed class ClassPlan
{
    public static ClassPlan None { get; } = new(Array.Empty<RunClass>());

    public ClassPlan(IReadOnlyList<RunClass> classes) => Classes = classes;

    public IReadOnlyList<RunClass> Classes { get; }

    /// <summary>The classes with a kit for this defender culture, in file order and under that culture's text: what a fortress of that culture offers.</summary>
    public IReadOnlyList<RunClass> OnOffer(string culture) => Classes.Where(c => c.HasKit(culture)).Select(c => c.For(culture)).ToList();

    /// <summary>
    /// The class a run plays: the one asked for when it is on offer, else the bot's draw <c>RunRandom.For(seed, 3, 0, 0).Below(count)</c> or, for a person, the first class on
    /// offer. Null when nothing is on offer.
    /// </summary>
    public static RunClass? Choose(IReadOnlyList<RunClass> offer, string? requested, ulong seed, bool bot)
    {
        if (offer.Count == 0)
            return null;

        var asked = requested == null ? null : offer.FirstOrDefault(c => string.Equals(c.Id, requested, StringComparison.OrdinalIgnoreCase));
        if (asked != null)
            return asked;

        return bot ? offer[RunRandom.For(seed, RunRandom.ClassStream, 0, 0).Below(offer.Count)] : offer[0];
    }
}
