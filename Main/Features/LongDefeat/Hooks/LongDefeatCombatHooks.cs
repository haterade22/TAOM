using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The damage factor of an endless wave, applied to each attacker's blows from the Custom Battle damage model. Inert outside the armed
/// Long Defeat mission: nothing armed, another mission, a defender's blow, a fall and a factor of 1 all pass straight through. The model asks from
/// the engine's agent threads, so the factor is one volatile number and the count an <see cref="Interlocked"/> long; nothing allocates.
/// Armed from the mission logic's <c>AfterStart</c> and disarmed on both exits, like <see cref="LongDefeatMorale"/>. The engine's own line is
/// <c>float baseDamage2 = agentApplyDamageModel.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage);</c>
/// (<c>AgentApplyDamageModel.cs:15</c>), which the Custom Battle twin of the damage model answers.
/// </summary>
internal static class LongDefeatCombatHooks
{
    private static volatile Mission? _armed;
    private static volatile float _attackerDamage = 1f;
    private static long _raised;

    /// <summary>Blows the factor raised since it was last published, so a log line can show the seam ran in this wave.</summary>
    internal static long Raised => Interlocked.Read(ref _raised);

    internal static void Arm(Mission? mission)
    {
        _attackerDamage = 1f;
        Interlocked.Exchange(ref _raised, 0);
        _armed = mission;
    }

    internal static void Disarm(Mission? mission)
    {
        if (mission == null || !ReferenceEquals(_armed, mission))
            return;

        _armed = null;
        _attackerDamage = 1f;
    }

    /// <summary>Takes the attackers' damage factor for the wave that is starting; ignored from any mission but the armed one.</summary>
    internal static void Publish(Mission? mission, float attackerDamage)
    {
        if (mission == null || !ReferenceEquals(_armed, mission))
            return;

        Interlocked.Exchange(ref _raised, 0);
        _attackerDamage = attackerDamage;
    }

    /// <summary>
    /// The blow's damage times the wave's factor when its attacker is an attacker of the armed mission (a rider's mount counts as its rider).
    /// A fall is no blow: the engine names the faller as its own attacker (<c>FallDamageCallback(ref collisionData, b, riderAgent, riderAgent)</c>,
    /// <c>Mission.cs:6151</c>), so the wave factor would raise the damage a scaled attacker takes from a ladder or a tower.
    /// </summary>
    internal static float Amplify(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var armed = _armed;
        var factor = _attackerDamage;
        if (armed == null || factor == 1f || collision.IsFallDamage)
            return damage;

        var attacker = attack.IsAttackerAgentMount ? attack.AttackerAgent?.RiderAgent : attack.AttackerAgent;
        if (attacker == null || !ReferenceEquals(attacker.Mission, armed) || attacker.Team?.Side != BattleSideEnum.Attacker)
            return damage;

        Interlocked.Increment(ref _raised);
        return damage * factor;
    }
}
