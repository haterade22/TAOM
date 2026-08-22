namespace TAOM.Features.Refuge;

/// <summary>
/// Defender damage reduction for refuge garrisons, consumed by TAOM's combat model chain.
///
/// <para>The source module patched <c>SandboxAgentApplyDamageModel.ApplyDamageReductions</c> and
/// <c>DefaultCombatSimulationModel.SimulateHit</c> directly. Both slots are TAOM-owned
/// (<c>TaomCombatMechanicsModel</c> chain, <c>TaomCombatSimulationModel</c>), so the port deletes
/// the patches and the models consult this service instead: one decision, two consumers, no
/// Harmony ordering accident.</para>
/// </summary>
public interface IRefugeDefenseService
{
    /// <summary>
    /// Damage-reduction factor (0..1) for a defender belonging to the party with this StringId:
    /// 0.20 for a refuge, 0.35 for a stronghold, 0 when the party is not a ready refuge.
    /// Hot path (per hit / per sim tick): dictionary probe only, no allocation, null-tolerant.
    /// </summary>
    float DefenderDamageReduction(string partyStringId);
}
