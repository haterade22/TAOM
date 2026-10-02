using TAOM.Features.PlayerSwitcher.Domain;

namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// The only place TaomSettings.Instance is read for this feature.
/// </summary>
public interface IPlayerSwitchPolicyProvider
{
    PlayerSwitchPolicy Current { get; }

    /// <summary>
    /// Latches the feature off for the rest of the session. Called when a reflection probe
    /// fails, so the picker never appears rather than leaving a campaign half-swapped.
    /// </summary>
    void DisableForSession(string reason);

    /// <summary>
    /// Another picker owns the current character creation (#704: the themed faction and hero
    /// picker), so the feature is off until this is set back to false. Not a failure: no warning is
    /// logged, and it never overrides <see cref="DisableForSession"/>.
    /// </summary>
    void SetSuppressedForCharacterCreation(bool suppressed);
}
