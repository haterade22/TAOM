namespace TAOM.Features.FordsOfWar;

/// <summary>
/// The seam between the tower hit patch and the match: the damage a destructible component should really take. The component
/// and the attacker come in as <c>object</c> so the seam names no engine type; the director resolves them through its adapters.
/// May be called from any thread the engine hits an entity on.
/// </summary>
internal interface IFordsTowerHitHook
{
    /// <summary>
    /// The damage to apply to <paramref name="component"/>. A component that is not a Fords tower gets
    /// <paramref name="inflictedDamage"/> back unchanged; a locked or fallen tower gets 0. Never throws.
    /// </summary>
    int AdjustTowerDamage(object component, object? attacker, bool ranged, int inflictedDamage);
}
