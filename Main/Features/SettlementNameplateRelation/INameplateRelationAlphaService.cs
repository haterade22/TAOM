namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// Sets the target alpha of an untracked settlement plate inside the window by the player's
/// relation to its owner, from the two MCM opacity settings (#596): the neutral opacity for a
/// neutral plate, the coloured opacity for own-faction, enemy and allied plates. The defaults
/// reproduce vanilla's own-faction 0.5 for every coloured plate (vanilla gave enemy and allied
/// the neutral 0.35, #591) and vanilla's 0.35 for neutral; a player may raise or lower either.
/// Applied by the Patch38 postfix before the distance fade multiplier.
/// </summary>
public interface INameplateRelationAlphaService
{
    /// <summary>
    /// Returns the target to use. Unchanged when tracked (vanilla's 0.8 in-window and 1 at the
    /// screen edge must not move), when the relation is not one of the four, when the vanilla
    /// target is not positive (0 keeps an off-window plate hidden; NaN passes through untouched
    /// rather than becoming an owned value), or when the configured opacity is not positive.
    /// </summary>
    float Adjust(float vanillaTarget, int relationType, bool isTracked);
}
