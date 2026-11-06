using TAOM.Features.LongDefeat.Domain;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The damage factor of an endless wave, applied to each attacker's blows from the Custom Battle damage model, and the damage and damage-taken factors of the boons, applied to a
/// defender's blows and to the blows it takes (<see cref="LongDefeatTargets"/> names its group; the lord's body takes the player factors only). Inert outside the armed
/// Long Defeat mission: nothing armed, another mission and a factor of 1 pass straight through. A fall passes through <c>Amplify</c> only: <c>Reduce</c> scales it by the faller's
/// damage-taken factor (a horse's by its rider's), because the engine sends a fall through the same <c>CalculateDamage</c> (<c>FallDamageCallback</c> calls
/// <c>GetAttackCollisionResults</c>, <c>Mission.cs:6138</c>, which calls <c>CalculateDamage</c>, <c>Mission.cs:6536</c>). The model asks from
/// the engine's agent threads, so every factor is read from the one published table (<see cref="LongDefeatHookState"/>, which also arms and disarms the hooks and counts the raised blows); nothing allocates. The engine's own lines are
/// <c>float baseDamage2 = agentApplyDamageModel.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage);</c>
/// (<c>AgentApplyDamageModel.cs:15</c>) and the reductions call beside it (<c>AgentApplyDamageModel.cs:17</c>), which the Custom Battle twin of the damage model answers.
/// <see cref="LongDefeatHookState.Raised"/> counts the attackers' blows only, the proof that the wave's factor ran.
/// </summary>
internal static class LongDefeatCombatHooks
{
    /// <summary>
    /// The blow's damage times the factor of whoever struck it: the wave's factor for an attacker of the armed mission, the boons' damage factor of its group for a defender (a rider's
    /// mount counts as its rider). A fall is no blow: the engine names the faller as its own attacker (<c>FallDamageCallback(ref collisionData, b, riderAgent, riderAgent)</c>,
    /// <c>Mission.cs:6151</c>), so a factor would raise the damage a soldier takes from a ladder or a tower.
    /// </summary>
    internal static float Amplify(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var armed = LongDefeatHookState.Armed;
        if (armed == null || collision.IsFallDamage)
            return damage;

        var attacker = attack.IsAttackerAgentMount ? attack.AttackerAgent?.RiderAgent : attack.AttackerAgent;
        return attacker == null ? damage : Scale(attacker, armed, damage, dealt: true);
    }

    /// <summary>The blow's damage times the damage-taken factor of the defender it struck (a horse takes its rider's). Attackers take no such factor.</summary>
    internal static float Reduce(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var armed = LongDefeatHookState.Armed;
        if (armed == null)
            return damage;

        var victim = attack.IsVictimAgentMount ? attack.VictimAgent?.RiderAgent : attack.VictimAgent;
        return victim == null ? damage : Scale(victim, armed, damage, dealt: false);
    }

    private static float Scale(Agent body, Mission armed, float damage, bool dealt)
    {
        if (!ReferenceEquals(body.Mission, armed))
            return damage;

        var factor = 1f;
        var side = body.Team?.Side;
        var table = LongDefeatHookState.Table;
        if (side == BattleSideEnum.Attacker)
            factor = dealt && table != null ? table.AttackerDamage : 1f;
        else if (side == BattleSideEnum.Defender && table != null && LongDefeatTargets.Of(body, armed) is { } target)
            factor = dealt ? table.For(target).Damage : table.For(target).DamageTaken;

        if (factor == 1f)
            return damage;

        // Only the attackers' blows are counted: the count proves the wave's factor ran, and a defender's boon blow must not stand in for it.
        if (dealt && side == BattleSideEnum.Attacker)
            LongDefeatHookState.CountRaised();
        return damage * factor;
    }
}
