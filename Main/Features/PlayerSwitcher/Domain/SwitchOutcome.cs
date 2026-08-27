namespace TAOM.Features.PlayerSwitcher.Domain;

/// <summary>
/// Result of a handover attempt. Anything other than <see cref="Switched"/> leaves the player
/// as the character they created; the feature never leaves a campaign half-swapped.
/// </summary>
public enum SwitchOutcome
{
    /// <summary>No selection was made, or the feature is switched off. Nothing was touched.</summary>
    NotAttempted = 0,

    /// <summary>A precondition failed (missing target, dead target, already the player). Nothing was touched.</summary>
    Blocked = 1,

    /// <summary>The handover ran and the player now controls the target hero.</summary>
    Switched = 2,

    /// <summary>The handover threw partway. Logged, and the campaign continues.</summary>
    Failed = 3,
}
