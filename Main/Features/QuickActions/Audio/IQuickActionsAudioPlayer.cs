namespace TAOM.Features.QuickActions.Audio;

/// <summary>
/// Plays a short 2D UI sound after a quick-action completes. Wrapped behind an
/// interface so unit tests can verify the request without playing audio.
/// </summary>
public interface IQuickActionsAudioPlayer
{
    /// <summary>Play the "bulk sell completed" sound. No-op if PlaySounds is off.</summary>
    void PlaySellCompleted();

    /// <summary>Play the "items unequipped" sound. No-op if PlaySounds is off.</summary>
    void PlayUnequipCompleted();
}
