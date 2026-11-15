using System;
using System.Collections.Generic;

namespace TAOM.Features.CreatureSaddles;

/// <summary>
/// The harness family of each creature mount that is not a horse rig (docs/features/creature-saddles.md). The engine lets
/// a harness onto a mount only when <c>ArmorComponent.FamilyType</c> equals <c>Monster.FamilyType</c> (v1.5.7
/// <c>SPInventoryVM</c>). The Armory gives the warg, fell warg, dire wolf and spider family 1, the horse family, so every
/// horse saddle fits them; a horse saddle with reins on a dire wolf crashed a player's battle spawn (2026-11-15). Each
/// rig gets its own family here, and its own saddle the same one. The horse-rig reskins (elk, moose, ram, WildMesh) keep
/// family 1 because horse saddles are made for their skeleton. Taken: 0 humans, 1 horse, 2 camel, 4 chariot, 10 elephant.
/// </summary>
public static class CreatureSaddleFamilies
{
    public const int WargFamily = 11;
    public const int DireWolfFamily = 12;
    public const int SpiderFamily = 13;

    private static readonly Dictionary<string, int> MonsterFamilies = new(StringComparer.Ordinal)
    {
        ["warg"] = WargFamily,
        ["fell_warg"] = WargFamily,
        ["dire_wolf"] = DireWolfFamily,
        ["spider"] = SpiderFamily,
    };

    private static readonly Dictionary<string, int> HarnessFamilies = new(StringComparer.Ordinal)
    {
        ["warg_saddle"] = WargFamily,
        ["dire_wolf_saddle"] = DireWolfFamily,
    };

    public static IReadOnlyDictionary<string, int> Monsters => MonsterFamilies;

    public static IReadOnlyDictionary<string, int> Harnesses => HarnessFamilies;

    /// <summary>The family TAOM gives this Monster, or null to keep the data's own.</summary>
    public static int? MonsterFamily(string? monsterId) =>
        monsterId != null && MonsterFamilies.TryGetValue(monsterId, out var f) ? f : null;

    /// <summary>The family TAOM gives this harness item, or null to keep the data's own.</summary>
    public static int? HarnessFamily(string? itemId) =>
        itemId != null && HarnessFamilies.TryGetValue(itemId, out var f) ? f : null;

    /// <summary>True when an equipped harness no longer fits its mount and must come off (old saves).</summary>
    public static bool MustUnequipHarness(int mountFamily, int harnessFamily) => mountFamily != harnessFamily;
}
