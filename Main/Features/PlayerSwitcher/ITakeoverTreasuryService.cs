namespace TAOM.Features.PlayerSwitcher;

/// <summary>
/// Gives a taken-over lord their own treasury back after character creation (Mike, 2026-10-02).
/// </summary>
public interface ITakeoverTreasuryService
{
    /// <summary>
    /// Puts back the treasury of the lord the player took over in this character creation, over the
    /// engine's 1,000 gold. Call at the last OnCharacterCreationIsOver phase and on the default start
    /// only: any other Advanced Starting Options start type sets the player's gold itself. True when the
    /// player took over a lord, whether or not the gold could be written, so the caller grants no culture
    /// starting gold; false for a created character or an adopted wanderer.
    /// </summary>
    bool RestoreIfTakenOver();
}
