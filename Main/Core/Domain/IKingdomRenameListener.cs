namespace TAOM.Core.Domain;

/// <summary>
/// Told when a feature renames a kingdom in play (Realms names Erebor after Durin's Folk when its king forms that title,
/// and gives it its own name back when the title is lost). The game raises no event for <c>Kingdom.ChangeKingdomName</c>,
/// so the feature that renames says so through this seam, and a feature that shows the name (the realm names on the map)
/// listens without the two knowing each other. Called on the game thread, once per renamed kingdom.
/// </summary>
public interface IKingdomRenameListener
{
    void OnKingdomRenamed(string kingdomId);
}
