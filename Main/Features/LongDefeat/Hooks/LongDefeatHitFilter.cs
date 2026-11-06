using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Hooks;

/// <summary>
/// The filter <c>OnScoreHit</c> applies before a hit reaches the registry and the run (phase2-plan.md section 4, "The hit path"). A hit is dropped when it has no affector (<c>Agent.cs:5465</c> reads a
/// null owner), when affector and victim are one body (<c>KillAgentCheat</c> raises a hit whose affector is the victim, <c>Agent.cs:5527</c>), when the engine says it was blocked, when it took no health, or when it killed the victim.
/// A mount's blow is its rider's. Generic over the agent so the rules are tested without an engine; the logic passes <c>Agent</c>.
/// </summary>
internal static class LongDefeatHitFilter
{
    /// <summary>The body whose blow it was: a mount's rider (null for a riderless mount), anyone else themselves, null for no affector.</summary>
    public static T? Attacker<T>(T? affector, Func<T, bool> isMount, Func<T, T?> rider) where T : class =>
        affector == null ? null : isMount(affector) ? rider(affector) : affector;

    /// <summary>
    /// Whether a hit goes on to the run: both bodies known, not the same body, not blocked, some health taken, and the victim left standing. <c>HandleBlow</c> writes the health, raises the hit and only then
    /// calls <c>Die</c> when "Health &lt; 1f" (<c>Agent.cs:5513</c>, <c>:5527</c>, <c>:5529-5532</c>), so <paramref name="victimHealth"/> read in the hit is the health after the blow: under 1 is a killing blow, a man
    /// no dose can reach. A damage or a health that is not a number fails its positive test and the hit is dropped.
    /// </summary>
    public static bool Admits<T>(T? affected, T? attacker, bool isBlocked, float damagedHp, float victimHealth) where T : class =>
        affected != null && attacker != null && !ReferenceEquals(affected, attacker) && !isBlocked && damagedHp > 0f && victimHealth >= 1f;
}
