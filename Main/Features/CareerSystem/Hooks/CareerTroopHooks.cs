using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.Hooks;

/// <summary>
/// What the morale, stat and combat seams ask of the player's career counters (#769 E10): the fear a foe's
/// drain lays on the leader's non-hero troops, their knockdown resistance, and a chance to hold against a
/// crush-through. One call per seam, so each model body stays a nested delegate (gamemodels.md rule 4) and
/// the four fear sites each add one wrapper. The caller hands in the <see cref="ICareerAgentStatService"/> it
/// already holds, as <c>RefugeDamageHooks.Reduce</c> and <c>SignatureStrikeVerdicts.Decide</c> are given theirs:
/// no lookup happens here, per blow or per drain.
///
/// <b>Why not the morale model.</b> BattleMoraleModel.CalculateMoraleChangeToCharacter also scales the morale
/// a soldier GAINS from kills, so the counter wraps its result at the four DRAIN sites instead.
///
/// <b>Where each runs.</b> <see cref="ScaleFearDrain(ICareerAgentStatService?, Agent?, float)"/> runs at the
/// four drain sites (the Dread Aura pulse, the signature strike ring, the race ability deaths and auras) in every
/// mission, Custom Battle included, where it falls through because a Custom Battle origin is not a party.
/// <see cref="KnockDownResistance(ICareerAgentStatService?, Agent?, float)"/> runs under the campaign stat model and in
/// Hammerhand's bash and kick decision in every mission, Custom Battle included, where it falls through for the same
/// reason; <see cref="ApplyCrushHold(ICareerAgentStatService?, Agent?, bool)"/> runs only under the campaign models (the
/// Custom Battle twins do not carry it). The knockdown read is reached from the engine's hit callbacks (not
/// declared multi-thread; which thread native uses is unproven); it takes only locks and snapshots, so it is safe on
/// either. The crush read comes from <c>DecideCrushedThrough</c>, which the engine reaches from
/// <c>Mission.GetDefendCollisionResults</c>, a callback declared multi-thread callable, so there it may run off the
/// main thread: on a crush-through verdict it reads the defender's map-event party (<c>Origin.BattleCombatant</c>,
/// a lookup into a table the main thread fills) and takes the passive-cache lock. A counter that takes effect
/// writes its tally, and a first fault its entry in the fault set, under the service's own lock. The random draw
/// happens only when the leader holds a pip for that class, and it uses the hold's own generator under its own lock,
/// never <c>MBRandom</c>: the engine's generator is <c>Game.Current.RandomGenerator</c>, whose XorShift state two
/// concurrent draws corrupt (Codex, 2026-10-15, reproduced against the installed v1.5.5 DLL). Nothing is deferred:
/// the callback needs the verdict now.
///
/// <b>Fail open.</b> Whatever is missing (no service, no agent, a hero, no party, no leader) hands the input
/// back. So does a thrown exception: the engine's answer stands, and the first fault of each hook is reported
/// once through the service (<see cref="ICareerAgentStatService.ReportHookFault"/>), so a counter that has gone
/// inert leaves a line in the log. An <c>Agent</c> cannot be built outside the game, so each public overload
/// reduces it to a leader id and a class and an internal overload decides.
/// </summary>
public static class CareerTroopHooks
{
    private static readonly object RollGate = new object();
    private static readonly Random HoldRandom = new Random();
    private static readonly Func<float> RandomRoll = () =>
    {
        lock (RollGate) return (float)HoldRandom.NextDouble();
    };

    /// <summary>The enemy's fear drain on <paramref name="victim"/>, shrunk by its leader's TroopFearResistance.</summary>
    public static float ScaleFearDrain(ICareerAgentStatService? stats, Agent? victim, float drain)
        => TryResolve(stats, nameof(ScaleFearDrain), victim, out var leader, out var troopClass)
            ? ScaleFearDrain(stats, leader, troopClass, drain)
            : drain;

    /// <summary>
    /// The knockdown resistance of <paramref name="agent"/>, raised by its leader's TroopKnockdownResistance. The
    /// engine asks for a mount too (a charge does), and a mount's resistance is float.MaxValue, which the service
    /// hands back unchanged.
    /// </summary>
    public static float KnockDownResistance(ICareerAgentStatService? stats, Agent? agent, float resistance)
        => TryResolve(stats, nameof(KnockDownResistance), agent, out var leader, out var troopClass)
            ? KnockDownResistance(stats, leader, troopClass, resistance)
            : resistance;

    /// <summary>
    /// False when the verdict was a crush-through and <paramref name="defender"/> holds against it, using the
    /// engine's random float as the roll; otherwise the verdict as given. Never turns a hold into a crush.
    /// </summary>
    public static bool ApplyCrushHold(ICareerAgentStatService? stats, Agent? defender, bool crushedThrough)
        => crushedThrough
            && (!TryResolve(stats, nameof(ApplyCrushHold), defender, out var leader, out var troopClass)
                || ApplyCrushHold(stats, leader, troopClass, true, RandomRoll));

    internal static float ScaleFearDrain(ICareerAgentStatService? stats, string? leaderHeroId, TroopClassMask troopClass, float drain)
    {
        if (stats == null || leaderHeroId == null) return drain;
        try
        {
            return stats.ScaleTroopFear(leaderHeroId, troopClass, drain);
        }
        catch (Exception ex)
        {
            stats.ReportHookFault(nameof(ScaleFearDrain), ex);
            return drain;
        }
    }

    internal static float KnockDownResistance(ICareerAgentStatService? stats, string? leaderHeroId, TroopClassMask troopClass, float resistance)
    {
        if (stats == null || leaderHeroId == null) return resistance;
        try
        {
            return stats.ScaleTroopKnockdownResistance(leaderHeroId, troopClass, resistance);
        }
        catch (Exception ex)
        {
            stats.ReportHookFault(nameof(KnockDownResistance), ex);
            return resistance;
        }
    }

    internal static bool ApplyCrushHold(ICareerAgentStatService? stats, string? leaderHeroId, TroopClassMask troopClass, bool crushedThrough, Func<float> roll)
    {
        if (!crushedThrough) return false;
        if (stats == null || leaderHeroId == null) return true;
        try
        {
            return !stats.HoldsAgainstCrush(leaderHeroId, troopClass, roll);
        }
        catch (Exception ex)
        {
            stats.ReportHookFault(nameof(ApplyCrushHold), ex);
            return true;
        }
    }

    // The agent reduction for a public overload: false (the input stands) for no service, no agent, a hero or no
    // leader, and for a fault in the engine reads, which is reported once for <hook>.
    private static bool TryResolve(ICareerAgentStatService? stats, string hook, Agent? agent, out string? leaderHeroId, out TroopClassMask troopClass)
    {
        leaderHeroId = null;
        troopClass = TroopClassMask.None;
        if (stats == null) return false;
        try
        {
            return TryResolveTroop(agent, out leaderHeroId, out troopClass) && leaderHeroId != null;
        }
        catch (Exception ex)
        {
            leaderHeroId = null;
            troopClass = TroopClassMask.None;
            stats.ReportHookFault(hook, ex);
            return false;
        }
    }

    /// <summary>
    /// The party leader and the class of a non-hero troop, from the same facts the damage boundary reads
    /// (TaomAgentApplyDamageModel): the rider for a mount; false for no agent or a hero (excluded, as for
    /// TroopDamage and TroopResistance: no hero-level counter exists, so a hero companion takes the full effect);
    /// the leader off the origin's party; the class off the character's default formation
    /// (<see cref="TroopClassMapper.FromCharacter"/>), None with no character.
    /// </summary>
    internal static bool TryResolveTroop(Agent? agent, out string? leaderHeroId, out TroopClassMask troopClass)
    {
        leaderHeroId = null;
        troopClass = TroopClassMask.None;
        var troop = agent != null && agent.IsMount ? agent.RiderAgent : agent;
        if (troop == null || troop.IsHero)
            return false;

        leaderHeroId = (troop.Origin?.BattleCombatant as PartyBase)?.LeaderHero?.StringId;
        troopClass = TroopClassMapper.FromCharacter(troop.Character);
        return true;
    }
}
