namespace TAOM.Adapters;

/// <summary>
/// The engine surfaces the fief hub opener touches: whether the campaign map can take a game menu
/// right now, the "no fiefs" toast, and the menu activation itself. One seam so F6 and the
/// navigation-bar button reach the same code (ADR-007).
/// </summary>
public interface IFiefHubHostAdapter
{
    /// <summary>
    /// True when <c>fief_hub</c> may be pushed this frame: the map state is active and none of the
    /// map's modal sub-states (a game menu, battle simulation, army management, the marriage or heir
    /// popup, map cheats, a map incident, the overlay context menu, the encyclopedia) is up. Polled
    /// every frame by the navigation button, so it reads fields only.
    /// </summary>
    bool IsMapClearForMenu { get; }

    /// <summary>Shows the yellow "You don't own any fiefs yet." message.</summary>
    void ShowNoFiefsMessage();

    /// <summary>Activates the <c>fief_hub</c> game menu.</summary>
    void OpenHub();
}
