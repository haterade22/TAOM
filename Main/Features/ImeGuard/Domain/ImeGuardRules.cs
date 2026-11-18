namespace TAOM.Features.ImeGuard.Domain;

/// <summary>
/// When the IME guard (#1447, ported from Silas He's v1.5.6 fix package) detaches the game window's input context.
/// </summary>
public static class ImeGuardRules
{
    /// <summary>
    /// Only while the switch is on, the game window is in front, and no text field has keyboard focus: a CJK IME
    /// then has nothing to type into, and would only take the player's hotkeys.
    /// </summary>
    public static bool ShouldDetach(bool enabled, bool ownForeground, bool textFocus)
        => enabled && ownForeground && !textFocus;
}
