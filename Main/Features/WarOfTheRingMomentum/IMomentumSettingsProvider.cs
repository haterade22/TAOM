namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// Effective tunables: MCM value when available, JSON config fallback otherwise
/// (TaomSettingsProvider precedent). Values already clamped to the same ranges the
/// config provider validates (dual-surface rule).
/// </summary>
public interface IMomentumSettingsProvider
{
    bool MomentumEnabled { get; }

    /// <summary>When false the War of the Ring never ends via momentum — endless-war mode (default).</summary>
    bool VictoryEnabled { get; }

    int VictoryThreshold { get; }
    float ParticipationMultiplier { get; }
    bool RequireParticipationForVictory { get; }
    int MinimumPlayerEventsForVictory { get; }
    bool ShowMapMeter { get; }
}
