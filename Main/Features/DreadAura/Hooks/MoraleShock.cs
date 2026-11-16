using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Hooks;
using TAOM.Features.DreadAura.Domain;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ComponentInterfaces;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// One-shot morale drain on one victim, shared by the signature strike ring and the hero
/// abilities. It reuses DreadAura's policy-free pieces (<see cref="DreadAgentGate.CanAffect"/>,
/// <see cref="IDreadRegistry.ResolveResist"/> and the registered model's tier and hero
/// resistance) but not the drain service, which is gated on the Dread Aura toggle. Main thread only.
/// </summary>
public static class MoraleShock
{
    /// <summary>Returns the morale taken, 0 when nothing was drained.</summary>
    public static float Apply(
        Agent victim,
        float fearMorale,
        float falloff,
        BattleMoraleModel? moraleModel,
        ICareerAgentStatService careerStats,
        IDreadRegistry dreadRegistry)
    {
        if (!(fearMorale > 0f) || moraleModel == null || !DreadAgentGate.CanAffect(victim))
            return 0f;

        var scaled = CareerTroopHooks.ScaleFearDrain(careerStats, victim, moraleModel.CalculateMoraleChangeToCharacter(victim, fearMorale));
        var drain = FearDrainMath.Compute(
            scaled, falloff, dreadRegistry.ResolveResist(victim.Character?.Race), victim.GetMorale());
        if (!(drain > 0f))
            return 0f;

        victim.ChangeMorale(-drain);
        return drain;
    }
}
