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
    /// Reach falloff for a besieging army, replacing vanilla's distance factor which never drops
    /// below 0.9x at ANY distance. Flat 1.0 out to the inner radius so genuine borders are
    /// untouched, then linear decay to <c>ReachFloor</c> at the outer radius.
    ///
    /// A non-finite distance means the adapter could not measure one, and returns 1.0: suppressing
    /// every target on garbage would break AI targeting outright.
    /// </summary>
    /// <param name="normalizedDistance">Map distance to the attacker's nearest owned fortification, in town gaps.</param>
    float GetReachMultiplier(float normalizedDistance);

    /// <summary>
    /// True when a target is close enough that Patch22 may override vanilla's "unreachable"
    /// verdict. A non-finite distance returns FALSE: vanilla already rejected the target, and we
    /// do not overturn that on an unmeasurable reading.
    /// </summary>
    bool IsWithinReach(float normalizedDistance);

    /// <summary>
    /// Soft theater weighting. Returns the primary, secondary or foreign weight depending on
    /// whether the target's owner sits in the attacker's primary theater, any shared theater, or
    /// none. Never returns zero, and returns 1.0 for any faction absent from the table so
    /// player-founded and rebel kingdoms are unaffected.
    /// </summary>
    float GetTheaterWeight(string attackerFactionId, string targetFactionId);

    /// <summary>
    /// Returns the final target score after vanilla base has computed BaseScore. Besieger armies
    /// receive priority, theater and reach terms; Defender armies receive the home-defence
    /// multiplier; everything else passes through. A non-finite or non-positive BaseScore is
    /// returned unchanged.
    /// </summary>
    float ApplyTargetScoreModifiers(TargetScoreContext context);
}
