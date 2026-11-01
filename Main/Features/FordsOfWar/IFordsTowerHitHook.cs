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
    /// <paramref name="inflictedDamage"/> back unchanged; a locked or fallen tower gets 0. <paramref name="thrustDamage"/> is the
    /// thrust damage of the weapon the attacker held (0 for none or unknown): a melee blow whose raw damage is 0 takes a share of it
    /// instead (<c>towers.thrustFloorFactor</c>). Never throws.
    /// </summary>
    int AdjustTowerDamage(object component, object? attacker, bool ranged, int inflictedDamage, int thrustDamage);

    /// <summary>
    /// A blow on <paramref name="component"/> the patch will not hand to <see cref="AdjustTowerDamage"/>: a blow with no weapon
    /// (<paramref name="weaponless"/>), or a zero blow the floor cannot raise (a ranged blow, or a weapon with no thrust damage). Counts it for the run report and changes nothing. Never throws; may be called
    /// from any thread. <paramref name="ranged"/> is whether the weapon is a missile, thrown or shot weapon, as for
    /// <see cref="AdjustTowerDamage"/>.
    /// </summary>
    void NoteUnappliedBlow(object component, object? attacker, bool weaponless, bool ranged);
}
