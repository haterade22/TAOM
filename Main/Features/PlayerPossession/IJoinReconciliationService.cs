namespace TAOM.Features.PlayerPossession;

/// <summary>
/// Re-applies the character-creation package to the hero a multiplayer join actually handed us.
///
/// Every grant it performs already exists elsewhere and already takes a hero id — this re-invokes
/// them against the right hero rather than reimplementing any of them.
/// </summary>
public interface IJoinReconciliationService
{
    /// <summary>
    /// Applies race, starting gold, career and special-resource seed to <paramref name="heroId"/>.
    /// Each grant is independently fail-open: one throwing must not cost the player the others.
    /// </summary>
    /// <param name="kingdomId">
    /// The possessed hero's LIVE kingdom, resolved by the caller at the boundary. Only the special
    /// resource depends on it, and only the host knows it — the character-creation campaign never did.
    /// </param>
    /// <returns>True when at least one grant was applied.</returns>
    bool ReapplyCharacterCreationPackage(PlayerCharacterCreationChoices choices, string heroId, string kingdomId);
}
