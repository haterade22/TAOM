namespace TAOM.Features.LongDefeat.Domain;

/// <summary>Every number of <c>waves.json</c> "rules". The defaults are the shipped values, so a missing key falls back to them.</summary>
internal sealed record RunRules
{
    public static RunRules Default { get; } = new();

    public float PreparationSeconds { get; init; } = 90f;
    public float IntermissionSeconds { get; init; } = 60f;
    public float EarlyGoldPerSecond { get; init; } = 2f;
    public int EarlyGoldCap { get; init; } = 120;
    public int WaveBonusBase { get; init; } = 150;
    public int WaveBonusPerWave { get; init; } = 50;
    public int SpawnsPerTick { get; init; } = 8;
    public int LiveCap { get; init; } = 150;
    public int GarrisonCap { get; init; } = 220;
    public int MusterBatch { get; init; } = 20;
    public float MusterBatchSeconds { get; init; } = 4f;
    public float QuietSeconds { get; init; } = 60f;
    public float QuietAnySeconds { get; init; } = 180f;
    public float StrandedSeconds { get; init; } = 90f;
    public float StragglerShare { get; init; } = 0.10f;
    public float KeepRadius { get; init; } = 12f;
    public float KeepHeightBand { get; init; } = 4f;
    public float KeepHoldSeconds { get; init; } = 45f;
    public float KeepDecaySeconds { get; init; } = 90f;
    public int KeepMinAttackers { get; init; } = 12;
    public float KeepDominance { get; init; } = 2f;
}
