namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingService
{
    float GetTargetMultiplier(string candidateId, string committedTargetId, string factionId);
    float GetStrengthMultiplier(string factionId);
    bool IsInPriorityList(string factionId, string settlementId);

    /// <summary>
    /// Returns the modified ourStrength to feed into vanilla base.GetTargetScoreForFaction.
    /// Besieger armies receive a faction-specific multiplier; other army types pass through.
    /// </summary>
    float GetEffectiveStrength(string factionId, bool isBesieger, float ourStrength);

    /// <summary>
    /// True when a target is close enough that Patch22 may overturn vanilla's "unreachable"
    /// verdict for a priority-list entry. A non-finite distance returns FALSE: vanilla already
    /// rejected the target, and an unmeasurable reading is not grounds to overrule it.
    ///
    /// <para>This is the ONLY place TAOM consults metric distance. There is no distance term in the
    /// score itself, because vanilla already supplies one (see ApplyTargetScoreModifiers).</para>
    /// </summary>
    /// <param name="normalizedDistance">Map distance to the attacker's nearest owned fortification, in town gaps.</param>
    bool IsWithinBorderRescueRange(float normalizedDistance);

    /// <summary>
    /// Soft theater weighting. Returns the primary, secondary or foreign weight depending on
    /// whether the target's owner sits in the attacker's primary theater, any shared theater, or
    /// none. Never returns zero, and returns 1.0 for any faction absent from the table so
    /// player-founded and rebel kingdoms are unaffected.
    /// </summary>
    float GetTheaterWeight(string attackerFactionId, string targetFactionId);

    /// <summary>
    /// Returns the final target score after vanilla base has computed BaseScore. Besieger armies
    /// receive the priority and theater terms; Defender armies receive the home-defence multiplier;
    /// everything else passes through. A non-finite or non-positive BaseScore is returned unchanged.
    /// </summary>
    float ApplyTargetScoreModifiers(TargetScoreContext context);

    /// <summary>
    /// Clears the one-shot diagnostic latches so a second campaign in the same process still
    /// produces provenance breadcrumbs.
    /// </summary>
    void ResetDiagnostics();
}
