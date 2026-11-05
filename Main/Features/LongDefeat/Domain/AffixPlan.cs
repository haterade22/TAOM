using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>One row of the affix schedule: from <see cref="FromWave"/> on, a wave carries <see cref="Count"/> affixes (until a later row).</summary>
internal sealed record AffixStage(int FromWave, int Count);

/// <summary>An affix as the HUD shows it: its power id and its raw <c>{=KEY}English</c> name (the id itself when the culture's table has none).</summary>
internal sealed record AffixLabel(string Id, string Name);

/// <summary>The affixes of a run (phase2-plan.md section 2): the schedule, each power's numbers and the attacker culture's names; <see cref="Roll"/> is a pure function of the seed and the wave.</summary>
internal sealed class AffixPlan
{
    public const string Lash = "lash";
    public const string Drums = "drums";
    public const string Eye = "eye";
    public const string Discipline = "discipline";
    public const string Hide = "hide";
    public const string Hammer = "hammer";
    public const string Terror = "terror";
    public const string Blades = "blades";

    /// <summary>The eight powers, in the order <see cref="Pool"/> keeps. Never reordered: a replay depends on it.</summary>
    public static IReadOnlyList<string> Ids { get; } = new[] { Lash, Drums, Eye, Discipline, Hide, Hammer, Terror, Blades };

    /// <summary>
    /// The powers a wave can roll, in the order of <see cref="Ids"/>: only those with an effect, so a player never meets a named power that does nothing.
    /// Slice 8 adds <c>Hammer</c> and <c>Terror</c> and Slice 9 adds <c>Blades</c>, each when its effect lands; the Python replay (<c>AFFIX_POOL</c>) and the pinned rolls change with it.
    /// </summary>
    public static IReadOnlyList<string> Pool { get; } = new[] { Lash, Drums, Eye, Discipline, Hide };

    /// <summary>The numbers each power reads, by key: the whole vocabulary of <c>affixes.json</c> "powers", which its reader accepts and no more.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Keys { get; } = new Dictionary<string, IReadOnlyList<string>>
    {
        [Lash] = new[] { "swingSpeedPercent", "attackEagernessPercent" },
        [Drums] = new[] { "moveSpeedPercent", "accelerationPercent" },
        [Eye] = new[] { "aimErrorPercent", "drawSpeedPercent", "reloadSpeedPercent", "missileSpeedPercent" },
        [Discipline] = new[] { "blockAbilityPercent", "parryAbilityPercent" },
        [Hide] = new[] { "armourPercent" },
        [Hammer] = new[] { "engineHitPointsPercent" },
        [Terror] = new[] { "radius", "pulseSeconds", "amount", "floor" },
        [Blades] = new[] { "poisonPerSecond", "poisonSeconds" },
    };

    /// <summary>No schedule, so no wave ever carries an affix: what a plan built with no <c>affixes.json</c> gets.</summary>
    public static AffixPlan None { get; } = new(
        Array.Empty<AffixStage>(),
        new Dictionary<string, IReadOnlyDictionary<string, float>>(),
        new Dictionary<string, string>());

    private readonly IReadOnlyList<AffixStage> _schedule;
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, float>> _powers;
    private readonly IReadOnlyDictionary<string, string> _names;

    /// <param name="schedule">Rows with rising <c>FromWave</c>.</param>
    /// <param name="powers">Power id to its numbers by key.</param>
    /// <param name="names">Power id to the raw <c>{=KEY}English</c> name in the attacker culture's table.</param>
    public AffixPlan(IReadOnlyList<AffixStage> schedule, IReadOnlyDictionary<string, IReadOnlyDictionary<string, float>> powers,
        IReadOnlyDictionary<string, string> names)
    {
        _schedule = schedule;
        _powers = powers;
        _names = names;
    }

    public IReadOnlyList<AffixStage> Schedule => _schedule;

    /// <summary>How many affixes wave <paramref name="wave"/> rolls: the count of the last schedule row at or below it (0 before the first), held to what <see cref="Pool"/> can fill.</summary>
    public int CountFor(int wave)
    {
        var count = 0;
        foreach (var stage in _schedule)
        {
            if (stage.FromWave <= wave)
                count = stage.Count;
        }

        return Math.Min(count, Pool.Count);
    }

    /// <summary>
    /// The wave's affixes, drawn from <see cref="RunRandom"/> stream <see cref="RunRandom.AffixStream"/> at <c>(wave, 0)</c>: a partial Fisher-Yates over
    /// <see cref="Pool"/> (for i from 0, <c>j = i + Below(Pool.Count - i)</c>, swap), so the powers are distinct and a shorter roll is a prefix of a longer one.
    /// </summary>
    public IReadOnlyList<string> Roll(ulong seed, int wave)
    {
        var count = CountFor(wave);
        if (count <= 0)
            return Array.Empty<string>();

        var random = RunRandom.For(seed, RunRandom.AffixStream, wave, 0);
        var ids = Pool.ToArray();
        for (var i = 0; i < count; i++)
        {
            var j = i + random.Below(ids.Length - i);
            (ids[i], ids[j]) = (ids[j], ids[i]);
        }

        return ids.Take(count).ToArray();
    }

    /// <summary>One number of one power, 0 when the file gave none.</summary>
    public float Value(string power, string key) =>
        _powers.TryGetValue(power, out var numbers) && numbers.TryGetValue(key, out var value) ? value : 0f;

    /// <summary>The raw <c>{=KEY}English</c> name of a power in the attacker culture's table, or null when the table has none.</summary>
    public string? NameOf(string power) => _names.TryGetValue(power, out var name) ? name : null;

    /// <summary>The HUD's view of a roll: each id with its name, or the id itself where the table has none.</summary>
    public IReadOnlyList<AffixLabel> Labels(IReadOnlyList<string> ids) =>
        ids.Select(id => new AffixLabel(id, NameOf(id) ?? id)).ToArray();
}
