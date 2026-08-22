namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// Primitives extracted from the sealed TaleWorlds types at the <c>TaomTargetScoreModel</c>
/// boundary, so the scoring policy can live in a service with no engine dependency.
///
/// <para>It carries no distance. TAOM's score has no metric distance term: vanilla's own besieger
/// factor already ramps 10.0 to 0.9 across five town gaps and hard-zeroes non-adjacent targets, so
/// a second falloff bought 0.283 gaps of crossover movement for an adapter on the hot path and a
/// cohesion-disband hazard. Distance now enters only through Patch22's border-rescue gate.</para>
/// </summary>
public sealed class TargetScoreContext
{
    /// <summary>Value returned by <c>base.GetTargetScoreForFaction</c>. May be NaN if engine state is corrupt.</summary>
    public float BaseScore { get; set; }

    /// <summary>
    /// The ourStrength value to hand to vanilla, already carrying the faction aggression inflation
    /// that bypasses vanilla's 2x-defender siege gate. Precomputed by the context factory so the
    /// model body stays a straight-line boundary conversion.
    /// </summary>
    public float EffectiveStrength { get; set; }

    /// <summary>Mission classification, mapped from <c>Army.ArmyTypes</c>.</summary>
    public ArmyTargetingMission Mission { get; set; }

    /// <summary>
    /// <c>MapFaction.StringId</c> of the attacking party. Note empire_s is Mordor, empire_w is
    /// Gondor and empire is Dunland: all three share culture "empire", so culture cannot
    /// distinguish them and the faction id is the only safe key.
    /// </summary>
    public string FactionId { get; set; }

    /// <summary><c>MapFaction.StringId</c> of the settlement's owner. Null for unowned settlements.</summary>
    public string TargetFactionId { get; set; }

    /// <summary><c>Settlement.StringId</c> of the candidate target.</summary>
    public string TargetSettlementId { get; set; }

    /// <summary><c>Settlement.StringId</c> of the army's current commitment, or null.</summary>
    public string CommittedTargetId { get; set; }
}
