namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// Raises the target alpha of enemy and allied settlement plates. Vanilla's
/// <c>SettlementNameplateWidget.DetermineTargetAlphaValue</c> gives them the neutral 0.35 while an
/// own-faction plate gets 0.5; this lifts them to the own-faction level so a hostile or allied
/// fief is as prominent as the player's own. Applied by the Patch38 postfix before the distance
/// fade multiplier.
/// </summary>
public interface INameplateRelationAlphaService
{
    /// <summary>
    /// Returns the adjusted target. Unchanged when tracked (0.8 must not drop), when the relation
    /// is not enemy or allied, or when the vanilla target is not positive (0 keeps an off-window
    /// plate hidden; NaN passes through untouched rather than becoming an owned value).
    /// </summary>
    float Adjust(float vanillaTarget, int relationType, bool isTracked);
}
