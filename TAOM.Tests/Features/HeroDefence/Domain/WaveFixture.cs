using System.Collections.Generic;
using System.Linq;
using TAOM.Features.HeroDefence.Domain;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.HeroDefence.Domain;

/// <summary>A small catalog with the shipped shape: its bands and growth are the plan's numbers.</summary>
internal static class WaveFixture
{
    public static readonly string[] Pool = { "lash", "drums", "eye", "discipline", "hide" };

    public static RosterRow Row(string id, int level, CreepClass cls = CreepClass.Infantry) => new(id, level, cls);

    /// <summary>One troop of the class at every fifth level from 1 to 51.</summary>
    public static RosterRow[] Ladder(string prefix, CreepClass cls = CreepClass.Infantry) =>
        Enumerable.Range(0, 11).Select(i => new RosterRow($"{prefix}_{i}", i == 0 ? 1 : i * 5 + 1, cls)).ToArray();

    public static CultureDef Culture(
        string id,
        string signature = "wall",
        IReadOnlyDictionary<CreepClass, int>? mix = null,
        float countFactor = 1f,
        string firstPower = "hide",
        IReadOnlyList<string>? leaders = null,
        IReadOnlyList<RosterRow>? roster = null) =>
        new(id, signature, mix ?? InfantryOnly, countFactor, firstPower, leaders ?? new[] { id + "_cap", id + "_lt", id + "_gen" }, roster ?? Ladder(id));

    public static readonly IReadOnlyDictionary<CreepClass, int> InfantryOnly = new Dictionary<CreepClass, int> { [CreepClass.Infantry] = 100 };

    public static IReadOnlyDictionary<CreepClass, int> Mix(int infantry, int ranged = 0, int cavalry = 0, int horseArcher = 0) =>
        new Dictionary<CreepClass, int>
        {
            [CreepClass.Infantry] = infantry,
            [CreepClass.Ranged] = ranged,
            [CreepClass.Cavalry] = cavalry,
            [CreepClass.HorseArcher] = horseArcher,
        };

    public static WaveCatalog Catalog(params CultureDef[] cultures) => new()
    {
        Cultures = cultures,
        Signatures = new Dictionary<string, SignatureDef>
        {
            ["wall"] = new("wall", new[] { CreepClass.Infantry, CreepClass.Ranged, CreepClass.Cavalry, CreepClass.HorseArcher }, false),
            ["horse"] = new("horse", new[] { CreepClass.Cavalry, CreepClass.HorseArcher, CreepClass.Infantry, CreepClass.Ranged }, false),
            ["horde"] = new("horde", new[] { CreepClass.Infantry, CreepClass.Ranged, CreepClass.Cavalry, CreepClass.HorseArcher }, true),
        },
        Opening = new string[0],
        Bands = new[]
        {
            new LevelBand(1, 1, 6),
            new LevelBand(2, 11, 11),
            new LevelBand(3, 11, 16),
            new LevelBand(5, 16, 21),
            new LevelBand(7, 21, 26),
            new LevelBand(10, 26, 31),
            new LevelBand(13, 31, 36),
            new LevelBand(16, 36, 41),
            new LevelBand(20, 41, 46),
            new LevelBand(24, 46, 51),
        },
        PowerSchedule = new[] { new PowerStep(5, 1), new PowerStep(10, 2), new PowerStep(15, 3), new PowerStep(20, 4), new PowerStep(25, 5) },
        PowerPool = Pool,
        Scale = new EndlessScale(0.05f, 0.03f, 20f, int.MaxValue, new Dictionary<string, string>()),
    };

    public static IEnumerable<CreepSpec> Creeps(this WaveBrief brief) => brief.Packs.SelectMany(pack => pack.Creeps);

    public static CreepSpec? Leader(this WaveBrief brief) => brief.Creeps().FirstOrDefault(c => c.Rank != CreepRank.Soldier);
}
