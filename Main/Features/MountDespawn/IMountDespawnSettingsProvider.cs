namespace TAOM.Features.MountDespawn;

/// <summary>
/// Isolates the MCM statics behind an interface so the scheduling service is unit-testable.
/// Values are passed through RAW: the clamp lives in <see cref="DeadMountDespawnService"/> so
/// there is exactly one place a bad slider value can be caught.
/// </summary>
public interface IMountDespawnSettingsProvider
{
    bool IsEnabled { get; }

    /// <summary>Seconds between a mount's death and its fade. Unvalidated.</summary>
    float DespawnDelaySeconds { get; }
}
