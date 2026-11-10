using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.HeroDefence.Domain;

/// <summary>The pause has no creeps and a countdown. It is never an engine pause.</summary>
internal enum DefencePhase
{
    Pause,
    Wave,
    Ended,
}

internal enum HeroClass
{
    Melee,
    Ranged,
}

/// <summary>A fallen hero is out for the rest of the wave. The purchase stays, and the hero returns at the next pause.</summary>
internal enum DefenceHeroState
{
    NotOwned,
    Owned,
    Placed,
    Fallen,
}

internal enum CreepRank
{
    Soldier,
    Captain,
    Lieutenant,
    General,
}

/// <summary>How a creep left the field. A cull is a removal by the rules, which pays no bounty and costs no life.</summary>
internal enum CreepEnd
{
    Killed,
    Leaked,
    Culled,
    Fled,
}

internal enum GroundZone
{
    Road,
    Plateau,
}

internal enum BuyResult
{
    Bought,
    WrongPhase,
    UnknownHero,
    AlreadyOwned,
    NotEnoughGold,
}

internal enum PlaceResult
{
    Placed,
    WrongPhase,
    NotOwned,
    OffNavmesh,
    OutOfBounds,
    WrongGround,
    TooCloseToRoadEnd,
    TooCloseToPost,
    FieldFull,
}

internal enum StartResult
{
    Started,
    WrongPhase,
    NoHeroPlaced,
}

internal enum LevelResult
{
    Levelled,
    WrongPhase,
    UnknownHero,
    NotOwned,
    TopLevel,
    NotEnoughGold,
}

internal sealed record HeroDef(string Id, HeroClass Class, int Price);

/// <summary>
/// Where the director must spawn, move or reset one hero, and the level whose numbers it gets. A hero has at most one order in a
/// tick, made from its post and level at the end of the tick.
/// </summary>
internal sealed record HeroOrder(string HeroId, Vec3f Post, int Level);

internal sealed record CreepSpec(string TroopId, int Level, CreepRank Rank, bool Mounted);

/// <summary>
/// A group of one class. It fills one formation. The bonuses add to the brief's health and damage factors.
/// Only infantry that has no row in the band, and so falls to the top of its tree, carries them.
/// </summary>
internal sealed record CreepPack(CreepClass Class, IReadOnlyList<CreepSpec> Creeps)
{
    public float HealthBonus { get; init; }

    public float DamageBonus { get; init; }
}

/// <summary>
/// One pack that the director must spawn into one formation slot. A later pack can take the same slot, so <see cref="PackId"/>, unique in
/// the run, names the pack.
/// </summary>
internal sealed record CreepBatch(int PackId, int Slot, CreepPack Pack);

/// <summary>One wave, in arrival order, with what it adds to its creeps.</summary>
internal sealed record WaveBrief(int Wave, IReadOnlyList<CreepPack> Packs)
{
    public string CultureId { get; init; } = "";

    public int LevelLow { get; init; }

    public int LevelHigh { get; init; }

    public float HealthFactor { get; init; } = 1f;

    public float DamageFactor { get; init; } = 1f;

    public float ArmourPercent { get; init; }

    public float SwingPercent { get; init; }

    public IReadOnlyList<string> PowerIds { get; init; } = Array.Empty<string>();

    public float LeaderHealthFactor { get; init; } = 1f;
}

/// <summary>The road ends, and the zone of each navmesh face group a hero can stand on.</summary>
internal sealed record DefenceMap(Vec3f Entry, Vec3f Exit, IReadOnlyDictionary<int, GroundZone> Zones);

/// <summary>Everything a run needs. <see cref="Briefs"/> gives the same brief for the same wave, so a preview and its wave cannot differ.</summary>
internal sealed record DefencePlan(DefenceRules Rules, IReadOnlyList<HeroDef> Heroes, DefenceMap Map, Func<int, WaveBrief> Briefs)
{
    public HeroLevels Levels { get; init; } = new();
}

/// <summary>What the engine layer read at a clicked point. A <see cref="FaceGroup"/> of -1 means the point is off the navmesh.</summary>
internal sealed record PlacementProbe(Vec3f Point, int FaceGroup, bool InsideBoundary, bool LinksToRoad);

internal sealed record UnitSample(AgentKey Key, Vec3f Position);

/// <summary>The positions of the living creeps, read at a low rate.</summary>
internal sealed record FieldReading(IReadOnlyList<UnitSample> Creeps);

internal sealed record DefenceSnapshot(float Dt, FieldReading? Field);

/// <summary>The score of a finished run: waves cleared, then kills.</summary>
internal sealed record DefenceResult(int Waves, int Kills);

/// <summary>The engine work of one tick. The director applies the wave brief first, because creep armour is written once at spawn.</summary>
internal sealed class DefenceEffects
{
    public WaveBrief? WaveBegan { get; set; }

    public List<HeroOrder> HeroResets { get; } = new();

    public List<HeroOrder> HeroSpawns { get; } = new();

    public List<HeroOrder> HeroMoves { get; } = new();

    public List<CreepBatch> CreepBatches { get; } = new();

    public List<AgentKey> Removals { get; } = new();
}
