namespace TAOM.Features.Elephant;

/// <summary>
/// Pure decision logic for the war-elephant — a 1-for-1 port of ADOD's elephant gates + damage formula,
/// extracted from the engine so it is unit-testable (ADR-002/007). The engine-coupled work (radial agent
/// scan, action channel, damage application) lives in <see cref="ElephantMissionBehavior"/>.
/// </summary>
public interface IElephantAttackService
{
    /// <summary>True when the given Monster StringId is the war elephant (drives mount identification + lock).</summary>
    bool IsElephantMonster(string? monsterId);

    /// <summary>
    /// ADOD's <c>OnTickAsAI</c> trample gate: fire only when not already attacking, the elephant faces its scan
    /// direction, and the per-tick probability roll passes.
    /// </summary>
    bool ShouldAiTrample(float facingDot, float randomRoll, bool alreadyAttacking);

    /// <summary>ADOD's trample damage: <c>round(base * (blocking ? 0.25 : 1)) * 2</c>.</summary>
    int ComputeInflictedDamage(bool targetBlocking);
}
