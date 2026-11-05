namespace TAOM.Features.BattleCorpses;

/// <summary>
/// The MCM values behind battle corpse cleanup and the settings advisor (#701). Passed through
/// RAW: <see cref="BattleCorpsePolicy"/> is the one place a bad slider value is caught.
/// </summary>
public interface IBattleCorpseSettingsProvider
{
    bool IsCleanupEnabled { get; }

    /// <summary>Seconds a body stays before native fades it. Unvalidated.</summary>
    float FadeSeconds { get; }

    /// <summary>TAOM's per-battle corpse cap. Unvalidated.</summary>
    int CorpseCap { get; }

    /// <summary>Whether the main menu may offer the recommended ragdoll and corpse options.</summary>
    bool IsAdviceEnabled { get; }
}
