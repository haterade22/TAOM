using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.HeroDefence.Domain;

internal enum CreepClass
{
    Infantry,
    Ranged,
    Cavalry,
    HorseArcher,
}

internal sealed record RosterRow(string TroopId, int Level, CreepClass Class);

/// <summary>One culture of the rotation. <see cref="Leaders"/> lists the captain, the lieutenant and the general, in that order.</summary>
internal sealed record CultureDef(
    string Id,
    string Signature,
    IReadOnlyDictionary<CreepClass, int> Mix,
    float CountFactor,
    string FirstPower,
    IReadOnlyList<string> Leaders,
    IReadOnlyList<RosterRow> Roster);

/// <summary>How the classes of a culture arrive. A shuffled signature draws its order per wave from the seed.</summary>
internal sealed record SignatureDef(string Id, IReadOnlyList<CreepClass> Order, bool Shuffled);

internal sealed record LevelBand(int FromWave, int Low, int High);

internal sealed record PowerStep(int FromWave, int Count);

/// <summary>The data of the wave generator. The defaults are first guesses, and the config files replace them.</summary>
internal sealed record WaveCatalog
{
    public IReadOnlyList<CultureDef> Cultures { get; init; } = Array.Empty<CultureDef>();
    public IReadOnlyDictionary<string, SignatureDef> Signatures { get; init; } = new Dictionary<string, SignatureDef>();
    public IReadOnlyList<string> Opening { get; init; } = Array.Empty<string>();
    public IReadOnlyList<LevelBand> Bands { get; init; } = Array.Empty<LevelBand>();
    public IReadOnlyList<PowerStep> PowerSchedule { get; init; } = Array.Empty<PowerStep>();
    public IReadOnlyList<string> PowerPool { get; init; } = Array.Empty<string>();
    public EndlessScale Scale { get; init; } = EndlessScale.None;

    public int CountStart { get; init; } = 30;
    public int CountPerWave { get; init; } = 6;
    public int CountMax { get; init; } = 180;
    public int PackSize { get; init; } = 20;
    public int TroopsPerLevel { get; init; } = 2;
    public int NearestLowerLevels { get; init; } = 15;
    public double HighLevelShare { get; init; } = 0.6d;

    public float ArmourPercentPerWave { get; init; } = 1f;
    public float ArmourPercentMax { get; init; } = 60f;
    public float SwingPercentPerWave { get; init; } = 0.5f;
    public float SwingPercentMax { get; init; } = 25f;
    public float GapHealthPerLevel { get; init; } = 0.015f;
    public float GapDamagePerLevel { get; init; } = 0.01f;

    public int LeaderEveryWaves { get; init; } = 4;
    public IReadOnlyList<float> LeaderHealth { get; init; } = new[] { 2f, 3f, 4f };

    /// <summary>The band of the last step that has started. A wave before the first step uses the first band.</summary>
    public LevelBand BandOf(int wave) => Bands.LastOrDefault(band => band.FromWave <= wave) ?? Bands[0];

    /// <summary>A culture enters when the band reaches its lowest infantry, so elves arrive late with no list of entry waves.</summary>
    public bool HasEntered(CultureDef culture, int wave)
    {
        var high = BandOf(wave).High;
        return culture.Roster.Any(row => row.Class == CreepClass.Infantry && row.Level <= high);
    }
}
