using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CombatMechanics.Hooks;

/// <summary>
/// The post-pass that scales a mount's <c>MountChargeDamage</c> by its rider's culture (#610),
/// after the base model has derived it from the horse and harness. One boundary helper shared by
/// both <c>AgentStatCalculateModel</c> slots (campaign and Custom Battle) so the rider hop lives
/// in exactly one place. The engine reads the property off the MOUNT per charge hit
/// (<c>AttackInformation</c> takes <c>attackerAgent.GetAgentDrivenPropertyValue(MountChargeDamage)</c>
/// and <c>Mission.ChargeDamageCallback</c>'s attacker is the horse, v1.5.3 <c>Mission.cs:6103</c>),
/// so the multiply belongs on the mount's properties, not the rider's. It is a plain <c>*=</c>,
/// so call it only from the lifecycle method in which the BASE model rewrites the property:
/// <c>UpdateAgentStats</c> for the Sandbox model (rewritten every call, :1280),
/// <c>InitializeAgentStats</c> for the Custom Battle model (written once, :48). From anywhere
/// else it compounds. <c>MountChargeDamageBindingTests</c> pins both placements.
/// </summary>
public static class MountChargeDamageApplier
{
    public static void Apply(Agent? agent, AgentDrivenProperties p, IChargeDamageService service)
    {
        if (agent == null || !agent.IsMount)
            return;
        p.MountChargeDamage *= service.Multiplier(RiderCultureOf(agent));
    }

    /// <summary>The culture a mount charges for: its RIDER's character's culture. A mount agent is
    /// built with a null <c>Character</c> (<c>Mission.CreateHorseAgentFromRosterElements</c> passes
    /// <c>null</c> to <c>CreateAgent</c>, v1.5.3 <c>Mission.cs:4611</c>), so the hop through
    /// <c>RiderAgent</c> is the only route, the same one vanilla's <c>UpdateHorseStats</c> takes
    /// for the Riding skill. Null for a riderless horse or a rider without a culture.</summary>
    public static string? RiderCultureOf(Agent? mount) =>
        mount != null && mount.IsMount ? mount.RiderAgent?.Character?.Culture?.StringId : null;
}
