namespace TAOM.Features.LongDefeat.Domain;

internal enum RunPhase
{
    Preparation,
    Wave,
    Intermission,
    Ended,
}

/// <summary>Inside a Wave: still queueing attackers into the field, or waiting for the fight to finish.</summary>
internal enum WaveStage
{
    Spawning,
    Fighting,
}
