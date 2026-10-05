using System;
using System.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CreatureSiegeRole.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.CreatureSiegeRole.Hooks;

/// <summary>
/// What the shared game models ask of the creature siege role, one call per seam so each model body stays a one-line delegate
/// (gamemodels.md rule 4). Four models carry it: <c>TaomAgentStatCalculateModel</c> and <c>TaomCustomBattleAgentStatCalculateModel</c>
/// call <see cref="DetachmentCost(Agent, float)"/>, <c>TaomCombatMechanicsModel</c> and <c>TaomCustomBattleCreatureDamageModel</c>
/// call <see cref="ScaleGateDamage"/> after their base. Both read the one published <see cref="CreatureSiegeSnapshot"/>, which is
/// null outside an active wall battle, so every call in any other mission hands its input back after one volatile read.
///
/// <b>Threads.</b> The engine calls the cost from its asynchronous AI thread (the detachment tick) and from the main thread during
/// deployment, and the damage from whichever thread native picks for a hit. Neither hook makes a native call. The cost never
/// locks or allocates: it is a volatile read, a reference compare and an array index (the IL is pinned allocation-free by
/// <c>CreatureSiegeHooksTests</c>). The damage reads managed fields only, but a blow it actually scales takes the log throttle's
/// lock and builds a string; both are thread-safe, and every other hit on any other object skips them. An <c>Agent</c> cannot
/// be built outside the game, so each public overload reduces its engine arguments to primitives and an internal overload decides.
/// </summary>
public static class CreatureSiegeHooks
{
    private const int BlowLogBurst = 4;
    private const double BlowLogPerSecond = 1.0;

    private static volatile IModLogger? _logger;
    private static volatile SiegeLogThrottle _blowLogThrottle = new(BlowLogBurst, BlowLogPerSecond);

    /// <summary>The log the gate-blow lines go to. Set once by <c>CreatureSiegeRoleModule.InitializeStatics</c>; null writes nothing.</summary>
    internal static IModLogger? Logger
    {
        get => _logger;
        set => _logger = value;
    }

    /// <summary>The throttle on the gate-blow lines: a burst up front, then a trickle. Replaceable so a test starts each case fresh.</summary>
    internal static SiegeLogThrottle BlowLogThrottle
    {
        get => _blowLogThrottle;
        set => _blowLogThrottle = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// The detachment cost of <paramref name="agent"/>: +Infinity for a creature of the active mission, so a creature is never the
    /// lowest-cost candidate for a ram, ladder, tower, lever, stone pile or siege engine; <paramref name="baseCost"/> for every
    /// other agent, in every other mission, and with no snapshot published.
    /// </summary>
    public static float DetachmentCost(Agent agent, float baseCost)
    {
        var snapshot = CreatureSiegeSnapshot.Current;
        if (snapshot == null || agent == null)
            return baseCost;

        // A mount has no Character, so no race: it is never a creature.
        var character = agent.Character;
        return DetachmentCost(snapshot, agent.Mission, character == null ? -1 : character.Race, baseCost);
    }

    internal static float DetachmentCost(CreatureSiegeSnapshot? snapshot, object? agentMission, int race, float baseCost) =>
        snapshot != null && ReferenceEquals(snapshot.MissionToken, agentMission)
            ? CreatureSiegeRules.DetachmentCost(snapshot.IsCreatureRace(race), baseCost)
            : baseCost;

    /// <summary>
    /// A creature's melee blow on one of the mission's castle gates, multiplied; every other blow comes back unchanged. The gate
    /// test comes first and is one field read, because this runs for every hit of the battle.
    /// </summary>
    public static float ScaleGateDamage(in AttackInformation attack, in AttackCollisionData collision, float damage)
    {
        var snapshot = CreatureSiegeSnapshot.Current;
        if (snapshot == null)
            return damage;

        var gate = attack.HitObjectDestructibleComponent;
        if (gate == null || !snapshot.IsGateComponent(gate))
            return damage;

        var character = attack.AttackerAgentCharacter;
        return ScaleGateBlow(snapshot, gate.HitPoint, attack.IsFriendlyFire, attack.IsAttackerAgentMount, collision.IsMissile,
            character == null ? -1 : character.Race, damage);
    }

    /// <summary>
    /// The decision for a blow already known to land on a gate of the snapshot's mission. Logs one throttled line when the damage
    /// was multiplied. The gate test is a positive requirement: a NaN damage comes back as itself and <c>NaN &gt; NaN</c> is
    /// false, so a poisoned blow is never reported as scaled.
    /// </summary>
    internal static float ScaleGateBlow(CreatureSiegeSnapshot snapshot, float hitPointBefore, bool isFriendlyFire,
        bool isAttackerMount, bool isMissile, int attackerRace, float damage)
    {
        var multiplier = snapshot.GateDamageMultiplier;
        var scaled = CreatureSiegeRules.ScaleGateDamage(multiplier, damage, isFriendlyFire, isAttackerMount, isMissile,
            snapshot.IsCreatureRace(attackerRace));

        if (scaled > damage)
            LogBlow(damage, scaled, multiplier, hitPointBefore);

        return scaled;
    }

    // The hit may arrive on any thread and the blow is already resolved, so a line that cannot be written is dropped, never thrown.
    private static void LogBlow(float damage, float scaled, float multiplier, float hitPointBefore)
    {
        var logger = _logger;
        if (logger == null)
            return;

        try
        {
            if (!_blowLogThrottle.TryAcquire(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, out var suppressed))
                return;

            logger.LogDebug(CreatureSiegeReport.GateBlow(damage, scaled, multiplier, hitPointBefore, suppressed));
        }
        catch (Exception)
        {
            // A diagnostic line must never break a blow the engine is resolving.
        }
    }
}
