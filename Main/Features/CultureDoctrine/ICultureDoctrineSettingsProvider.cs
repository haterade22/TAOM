namespace TAOM.Features.CultureDoctrine;

/// <summary>
/// MCM live values over the validated JSON. <c>TaomSettings.Instance</c> can be null very early
/// in startup or when MCM fails to load; the feature then stays off, its shipped default.
/// </summary>
public interface ICultureDoctrineSettingsProvider
{
    /// <summary>MCM toggle AND the JSON's own <c>enabled</c>. Read once per mission at
    /// <c>EarlyStart</c>: the tactic list cannot change once the AI thread reads it, so a flip
    /// applies from the next battle.</summary>
    bool IsEnabled { get; }

    /// <summary>Emit the per-team status line every 5 s of mission time.</summary>
    bool IsDebug { get; }

    /// <summary><see cref="IsEnabled"/> and the morale sub-toggle. Read per call from the
    /// morale model, on the engine's worker threads: a bool read, nothing else.</summary>
    bool IsMoraleEnabled { get; }

    /// <summary><see cref="IsEnabled"/> and the aggression sub-toggle. Read per agent property
    /// refresh from the agent-stat model, on the main thread.</summary>
    bool IsAggressionEnabled { get; }
}
