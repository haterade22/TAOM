namespace TAOM.Features.Stamina.Domain;

/// <summary>
/// The validated values of stamina.json, resolved once at load. Immutable, so the stat models can read it from any
/// thread. Fresh always has no effect.
/// </summary>
public sealed class StaminaTuning
{
    public const float DefaultSweepSeconds = 0.25f;
    public const int DefaultMaxRefreshesPerFrame = 8;
    public const float DefaultMinRefreshAgeSeconds = 1f;

    public static readonly StaminaBandEffects DefaultWinded = new StaminaBandEffects(0.95f, 1f, 1f, 0.8f);
    public static readonly StaminaBandEffects DefaultTired = new StaminaBandEffects(0.90f, 0.90f, 0.95f, 0.5f);
    public static readonly StaminaBandEffects DefaultExhausted = new StaminaBandEffects(0.90f, 0.90f, 0.45f, 0.5f);

    public static readonly StaminaTuning Default = new StaminaTuning(DefaultWinded, DefaultTired, DefaultExhausted,
        DefaultSweepSeconds, DefaultMaxRefreshesPerFrame, DefaultMinRefreshAgeSeconds);

    private readonly StaminaBandEffects[] _effects;

    public StaminaTuning(StaminaBandEffects winded, StaminaBandEffects tired, StaminaBandEffects exhausted,
        float sweepSeconds, int maxRefreshesPerFrame, float minRefreshAgeSeconds)
    {
        _effects = new[] { StaminaBandEffects.None, winded, tired, exhausted };
        SweepSeconds = sweepSeconds;
        MaxRefreshesPerFrame = maxRefreshesPerFrame;
        MinRefreshAgeSeconds = minRefreshAgeSeconds;
    }

    /// <summary>Seconds between two looks at the same soldier.</summary>
    public float SweepSeconds { get; }

    /// <summary>The most Agent.UpdateAgentProperties calls in one frame: each call runs the whole stat model again.</summary>
    public int MaxRefreshesPerFrame { get; }

    /// <summary>A soldier younger than this is not refreshed: his spawn may still be in progress.</summary>
    public float MinRefreshAgeSeconds { get; }

    public StaminaBandEffects EffectsOf(StaminaBand band)
    {
        var index = (int)band;
        return index > 0 && index < _effects.Length ? _effects[index] : StaminaBandEffects.None;
    }
}
