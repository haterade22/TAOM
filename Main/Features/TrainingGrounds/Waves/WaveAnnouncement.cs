namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>The four quick notices of a wave fight (docs/features/training-waves.md); the master's dialogue carries the full numbers.</summary>
internal enum WaveAnnouncement
{
    /// <summary>A wave begins: the wave number and how many fighters it holds.</summary>
    WaveStart,

    /// <summary>A wave is cleared: the wave number and what it paid.</summary>
    WaveCleared,

    /// <summary>The player went down: the wave number and the stake the house keeps.</summary>
    WaveLost,

    /// <summary>A wave could not start (or a fault ended it) and the stake went back: the wave number and the stake returned.</summary>
    Refunded,
}
