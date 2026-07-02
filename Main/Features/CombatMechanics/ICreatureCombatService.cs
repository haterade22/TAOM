namespace TAOM.Features.CombatMechanics;

public interface ICreatureCombatService
{
    // Cleave half 1: remaining momentum for a listed creature's agent hit (originalMomentum ×
    // factor); null = fall through to base (which zeroes momentum for ordinary weapons).
    float? CalculateCleaveMomentum(string attackerMonsterId, float originalMomentum, bool isColliderAgent);

    // Cleave half 2: force SlicedThrough for a listed creature when momentum remains and the hit
    // did damage — prevents vanilla's chain-terminating Bounced/Stuck branches (shield block,
    // axe below 50% victim health, shrug-off, wrong-bone hit).
    bool ShouldForceSliceThrough(string attackerMonsterId, float momentumRemaining, bool isColliderAgent, int inflictedDamage);

    // Unstoppable: damage at or below the creature's configured threshold is shrugged off.
    bool IsUnstoppable(string victimMonsterId, int inflictedDamage);

    // Per-race stagger threshold multiplier (dwarf 1.5). Identity for invalid/unknown races.
    float ApplyStaggerThresholdMultiplier(int? victimRaceId, float baseThreshold);
}
