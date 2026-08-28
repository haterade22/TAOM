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

    /// <summary>
    /// The handover threw BEFORE the first mutation. Nothing was touched and the player really is
    /// still the character they created.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// The handover threw AFTER ChangePlayerCharacterAction had already run. The player IS the
    /// chosen lord, but some later step (clan pointer, career re-key, cleanup) did not complete.
    ///
    /// This exists because the engine offers no transaction and no rollback: once
    /// ChangePlayerCharacterAction.Apply has fired, Game.Current.PlayerTroop has changed and the
    /// player-character-changed events have been dispatched to every listener. Reporting that as
    /// <see cref="Failed"/> would tell the player they are continuing as their own character while
    /// they are, in fact, someone else.
    /// </summary>
    SwitchedWithErrors = 4,
}
