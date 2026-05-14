namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingService
{
    float GetTargetMultiplier(string candidateId, string committedTargetId, string factionId);
    float GetStrengthMultiplier(string factionId);
    float GetDistanceCompensation(string factionId, string targetId);
    bool IsInPriorityList(string factionId, string settlementId);

    // Phase 9b #138 — TaomTargetScoreModel orchestration moved out of override body per
    // gamemodels.md rule 4. The model becomes a pure boundary that extracts primitives and
    // delegates to these two methods.

    /// <summary>
    /// Returns the modified ourStrength to feed into vanilla base.GetTargetScoreForFaction.
    /// Besieger armies receive a faction-specific multiplier; other army types pass through.
    /// </summary>
    /// <param name="factionId">MapFaction.StringId of the army (empire_s/empire_w/empire — culture cannot distinguish Mordor/Gondor/Dunland).</param>
    /// <param name="isBesieger">true iff missionType == Army.ArmyTypes.Besieger.</param>
    /// <param name="ourStrength">unmodified ourStrength from vanilla.</param>
    float GetEffectiveStrength(string factionId, bool isBesieger, float ourStrength);

    /// <summary>
    /// Returns the final target score after vanilla base has computed baseScore. For Besieger
    /// armies with positive baseScore, multiplies by target priority + distance compensation;
    /// otherwise returns baseScore unchanged (vanilla rejection or non-Besieger army type).
    /// </summary>
    /// <param name="baseScore">value returned by base.GetTargetScoreForFaction.</param>
    /// <param name="isBesieger">true iff missionType == Army.ArmyTypes.Besieger.</param>
    /// <param name="factionId">MapFaction.StringId of the army.</param>
    /// <param name="targetSettlementId">Settlement.StringId of the candidate target.</param>
    /// <param name="committedTargetId">Settlement.StringId of the army's current commitment, or null.</param>
    float ApplyTargetScoreModifiers(float baseScore, bool isBesieger, string factionId, string targetSettlementId, string committedTargetId);
}
