using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Refuge.Hooks;

/// <summary>
/// The refuge defender reduction (#507) as the campaign damage model's <c>ApplyDamageReductions</c> applies it, moved out
/// of <c>TaomCombatMechanicsModel</c> by #737: the victim's mobile party from its agent origin, the service's reduction for
/// that party, then <see cref="RefugeDamageReduction"/>'s (1 - r) on the final damage, the contract the auto-resolve path
/// shares. A null service is the feature absent.
///
/// The party comes from the origin's <c>BattleCombatant</c>, cast to <c>PartyBase</c> as the parent model casts it
/// (<c>TaomAgentApplyDamageModel</c>). For the two vanilla party origins it is their <c>Party</c> (v1.5.4
/// <c>PartyAgentOrigin.cs:48</c>, <c>PartyGroupAgentOrigin.cs:31</c>), and the howdah and mumak crews forward it to their
/// mahout's or rider's origin, so a crew shares its party's refuge (#741). Before that, a switch on the two vanilla types
/// left every crew outside the reduction. A <c>SimpleAgentOrigin</c> hero (his <c>PartyBelongedTo</c>) and BannerlordCoop's
/// <c>CoopAgentOrigin</c> resolve their party the same way. A mount has no origin of its own (the engine sets one
/// only on the rider, v1.5.4 <c>Mission.cs:4219,4608-4618</c>), so a mount hit is credited to its rider's origin, the
/// branch the career passives (<c>TaomAgentApplyDamageModel</c>) and vanilla's reductions take: the horse, elephant or
/// mumak under a refuge defender shares the refuge too, and a riderless mount gets nothing. The origin is null for a hit
/// with no victim agent, and the service answers 0 for a null party id.
/// </summary>
public static class RefugeDamageHooks
{
    public static float Reduce(IRefugeDefenseService? refugeDefense, in AttackInformation attackInformation, float damage)
        => Reduce(refugeDefense,
            attackInformation.IsVictimAgentMount ? attackInformation.VictimRiderAgentOrigin : attackInformation.VictimAgentOrigin,
            damage);

    // Internal: production enters through the AttackInformation overload, which owns the mount branch.
    internal static float Reduce(IRefugeDefenseService? refugeDefense, IAgentOriginBase? victimOrigin, float damage)
    {
        var reduction = refugeDefense?.DefenderDamageReduction(VictimPartyId(victimOrigin)) ?? 0f;
        // Shared composition contract (RefugeDamageReduction): (1 - r) on the final damage; the
        // auto-resolve site applies the identical contract. NaN/out-of-range applies nothing.
        return RefugeDamageReduction.Apply(damage, reduction);
    }

    private static string? VictimPartyId(IAgentOriginBase? origin)
        => (origin?.BattleCombatant as PartyBase)?.MobileParty?.StringId;
}
