using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// The refuge defender reduction (#507) as the campaign damage model's <c>ApplyDamageReductions</c> applies it, moved out
/// of <c>TaomCombatMechanicsModel</c> by #737: the victim's mobile party from its agent origin, the service's reduction for
/// that party, then <see cref="RefugeDamageReduction"/>'s (1 - r) on the final damage, the contract the auto-resolve path
/// shares. A null service is the feature absent. The origin switch knows <c>PartyAgentOrigin</c> and
/// <c>PartyGroupAgentOrigin</c> only, so any other origin (vanilla's <c>SimpleAgentOrigin</c> in settlement and arena scenes,
/// or the elephant crew's, elephant.md) gets no reduction.
/// </summary>
public static class RefugeDamageHooks
{
    public static float Reduce(IRefugeDefenseService? refugeDefense, IAgentOriginBase victimOrigin, float damage)
    {
        var reduction = refugeDefense?.DefenderDamageReduction(VictimPartyId(victimOrigin)) ?? 0f;
        // Shared composition contract (RefugeDamageReduction): (1 - r) on the final damage; the
        // auto-resolve site applies the identical contract. NaN/out-of-range applies nothing.
        return RefugeDamageReduction.Apply(damage, reduction);
    }

    private static string VictimPartyId(TaleWorlds.Core.IAgentOriginBase origin)
    {
        var party = origin switch
        {
            PartyAgentOrigin p => p.Party,
            PartyGroupAgentOrigin g => g.Party,
            _ => null,
        };
        return party?.MobileParty?.StringId;
    }
}
