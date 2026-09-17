namespace TAOM.Features.CultureDoctrine.Doctrines;

/// <summary>
/// The vanilla tactic lifecycle as one pure step (<c>TacticDefensiveEngagement.TickOccasionally</c>,
/// `TacticDefensiveEngagement.cs:146-171`): re-apply the phase tables when the formation set
/// changed, when the battle-joined flag flipped, or when the engine asked for a re-apply
/// (<c>IsTacticReapplyNeeded</c>, set by <c>OnApply</c>, <c>ResetTactic</c> and
/// <c>CheckAndDetermineFormation</c>); recount formations only when the set changed.
/// One instance per tactic object, touched only from that object's tick.
/// </summary>
public sealed class TacticPhaseMachine
{
    private bool _joined;

    /// <summary>The phase to apply now, or null when nothing changed.</summary>
    public TacticPhase? Step(bool formationsChanged, bool battleJoined, bool reapplyNeeded, bool alwaysEngaged, out bool recountFormations)
    {
        var joined = alwaysEngaged || battleJoined;
        recountFormations = false;
        if (!formationsChanged && joined == _joined && !reapplyNeeded)
            return null;
        _joined = joined;
        recountFormations = formationsChanged;
        return joined ? TacticPhase.Engage : TacticPhase.Defend;
    }

    public void Reset() => _joined = false;
}
