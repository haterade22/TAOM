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
    /// True while another picker owns the current character creation (#704: the themed faction and
    /// hero picker), so the face generator gets no picker panel. The feature itself stays on: a hero
    /// picked on the faction screen is taken over by this feature's own handover.
    /// </summary>
    bool IsPickerHidden { get; }

    /// <summary>
    /// Hides the picker panel until this is set back to false (see <see cref="IsPickerHidden"/>). Not a
    /// failure: no warning is logged, and it never overrides <see cref="DisableForSession"/>.
    /// </summary>
    void SetPickerHidden(bool hidden);
}
