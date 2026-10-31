namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>Free wave cadence and the caps that keep a long match under the engine's agent budget.</summary>
internal sealed record FordsWaveConfig(float FreeWaveIntervalSeconds, int MaxLivePerLanePerSide,
    int MaxLivePerSide, int MaxSpawnsPerTick, int MaxQueuedPacksPerLane);
