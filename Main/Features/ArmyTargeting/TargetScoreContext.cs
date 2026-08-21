namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// Primitives extracted from the sealed TaleWorlds types at the <c>TaomTargetScoreModel</c>
/// boundary, so the scoring policy can live in a service with no engine dependency.
///
/// Introduced when the signature grew past five arguments: the theater weighting needs the
/// target's OWNING faction (not just the settlement id), and the reach falloff needs a
/// normalised map distance that only the model can obtain.
/// </summary>
public sealed class TargetScoreContext
{
    /// <summary>Value returned by <c>base.GetTargetScoreForFaction</c>. May be NaN if engine state is corrupt.</summary>
    public float BaseScore { get; set; }

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

    /// <summary>
    /// Map distance from the target to the attacking faction's NEAREST OWNED FORTIFICATION,
    /// divided by the engine's average distance between the closest two towns ("town gaps").
    ///
    /// Nearest fortification rather than <c>FactionMidSettlement</c> deliberately: the medoid
    /// distorts wide empires by up to 3.4x (Rhun to Gondor is 167 path units from its nearest
    /// fort but 567 from its mid) and it recomputes on every fief transfer, so it drifts toward
    /// whatever a kingdom is currently conquering.
    ///
    /// NaN or infinity means "could not be measured" and is treated as no suppression.
    /// </summary>
    public float NormalizedDistance { get; set; }
}
