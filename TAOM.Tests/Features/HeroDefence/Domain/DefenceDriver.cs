using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.HeroDefence.Domain;

namespace TAOM.Tests.Features.HeroDefence.Domain;

/// <summary>Plays the director's part for the run tests: it registers each released creep and tracks the live keys.</summary>
internal sealed class DefenceDriver
{
    public const string Melee = "melee_a";
    public const string MeleeB = "melee_b";
    public const string Bow = "bow_a";
    public const string Costly = "costly";
    public const int RoadFace = 1;
    public const int PlateauFace = 2;
    public const int YardFace = 3;

    public static readonly Vec3f Entry = new(0f, 0f, 0f);
    public static readonly Vec3f Exit = new(500f, 0f, 0f);

    public static readonly DefenceMap Map = new(Entry, Exit, new Dictionary<int, GroundZone>
    {
        [RoadFace] = GroundZone.Road,
        [PlateauFace] = GroundZone.Plateau,
    });

    private static readonly HeroDef[] Heroes =
    {
        new(Melee, HeroClass.Melee, 500),
        new(MeleeB, HeroClass.Melee, 200),
        new(Bow, HeroClass.Ranged, 300),
        new(Costly, HeroClass.Melee, 5000),
    };

    private int _nextKey = 1;

    /// <summary>Each entry of <paramref name="packsPerWave"/> lists one wave's pack sizes; the last entry repeats.</summary>
    public DefenceDriver(DefenceRules? rules = null, params int[][] packsPerWave)
    {
        var waves = packsPerWave.Length > 0 ? packsPerWave : new[] { new[] { 2 } };
        Run = new HeroDefenceRun(new DefencePlan(
            rules ?? new DefenceRules(),
            Heroes,
            Map,
            wave => new WaveBrief(wave, waves[System.Math.Min(wave - 1, waves.Length - 1)]
                .Select(size => new CreepPack(CreepClass.Infantry, Enumerable.Repeat(Soldier(), size).ToList()))
                .ToList())));
    }

    public HeroDefenceRun Run { get; }

    public List<AgentKey> Live { get; } = new();

    public static CreepSpec Soldier(int level = 1, bool mounted = false, CreepRank rank = CreepRank.Soldier) =>
        new("troop", level, rank, mounted);

    public static PlacementProbe OnRoad(float x) => new(new Vec3f(x, 0f, 0f), RoadFace, true, true);

    public static PlacementProbe OnPlateau(float x) => new(new Vec3f(x, 30f, 8f), PlateauFace, true, false);

    public DefenceEffects Tick(float dt = 0.1f, FieldReading? field = null)
    {
        var fx = Run.Tick(new DefenceSnapshot(dt, field));
        foreach (var batch in fx.CreepBatches)
        {
            foreach (var creep in batch.Pack.Creeps)
            {
                var key = new AgentKey(_nextKey++);
                Live.Add(key);
                Run.CreepSpawned(key, creep, batch.Slot);
            }
        }

        foreach (var removed in fx.Removals)
            Live.Remove(removed);
        return fx;
    }

    public AgentKey AddCreep(CreepSpec creep, int slot = 7)
    {
        var key = new AgentKey(_nextKey++);
        Live.Add(key);
        Run.CreepSpawned(key, creep, slot);
        return key;
    }

    public void Own(string heroId, float x = 100f)
    {
        Run.TryBuyHero(heroId);
        Run.TryPlaceHero(heroId, heroId == Bow ? OnPlateau(x) : OnRoad(x));
    }

    /// <summary>Owns and places the melee hero, starts the wave and releases the first pack.</summary>
    public DefenceDriver InWave()
    {
        Own(Melee);
        Run.TryStartWave();
        Tick();
        return this;
    }

    public void End(CreepEnd end)
    {
        foreach (var key in Live.ToList())
        {
            Run.CreepGone(key, end);
            Live.Remove(key);
        }
    }

    public DefenceEffects KillAllAndClear()
    {
        End(CreepEnd.Killed);
        return Tick();
    }
}
