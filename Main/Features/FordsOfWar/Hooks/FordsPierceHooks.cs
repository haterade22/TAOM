using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Models;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>
/// The pierce rule against cavalry (<see cref="MountedPierceRule"/>, Mike 2026-11-07) on a modelled blow, from
/// <see cref="FordsUpgradeHooks.Reduce"/>, and on a scripted one, from <see cref="FordsScriptedBlows"/>. The factor is 1 outside
/// a match: <see cref="FordsUpgradeHooks.Set"/> and <see cref="FordsUpgradeHooks.Clear"/> set and reset it. Blows can run off the
/// main thread, so the factor is one volatile value read once per blow.
/// </summary>
internal static class FordsPierceHooks
{
    private static volatile float _factor = 1f;
    private static long _hits;

    /// <summary>Pierce blows on a horse or a rider that the rule multiplied, modelled and scripted, since the match started. Blocked blows are not counted.</summary>
    internal static long Hits => Interlocked.Read(ref _hits);

    internal static void Set(float factor) => _factor = factor;

    internal static void Clear()
    {
        _factor = 1f;
        Interlocked.Exchange(ref _hits, 0);
    }

    // DamageType first, so the blunt rule runs only for a pierce blow; the engine makes a shield bash, a kick, a hit with the arm
    // instead of the weapon, fall damage and a horse charge blunt whatever DamageType says (TaomAgentApplyDamageModel.BluntByVanillaRule).
    // The engine runs the damage model on a blocked blow too, where the damage is the shield's (Mission.GetAttackCollisionResults),
    // and the rule is for the soldier and the horse, so a block keeps its shield damage.
    internal static float Factor(in AttackInformation attack, in AttackCollisionData collision)
    {
        var pierce = collision.DamageType == (int)DamageTypes.Pierce && !collision.AttackBlockedWithShield
            && !TaomAgentApplyDamageModel.BluntByVanillaRule(in attack, in collision);
        return Counted(MountedPierceRule.Factor(_factor, pierce, attack.IsVictimAgentMount || attack.DoesVictimHaveMountAgent));
    }

    // A scripted blow's empty weapon reads as blunt to the model, so the rule takes the blow's own type here.
    internal static int ScriptedDamage(float modelled, DamageTypes damageType, Agent victim)
    {
        var pierce = damageType == DamageTypes.Pierce;
        var mounted = victim.IsMount || victim.HasMount;
        Counted(MountedPierceRule.Factor(_factor, pierce, mounted));
        return MountedPierceRule.Damage(modelled, _factor, pierce, mounted);
    }

    private static float Counted(float factor)
    {
        if (factor != 1f)
            Interlocked.Increment(ref _hits);
        return factor;
    }
}
