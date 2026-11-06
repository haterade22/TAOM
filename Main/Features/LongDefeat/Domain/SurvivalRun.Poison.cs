using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// Morgul Blades in the run (phase2-plan.md section 2). A blades wave holds one <see cref="LdPoisonBook"/>: an attacker's hit on a defender doses him (the owner of the dose is the hitter), the
/// doses tick on the wave's fighting seconds, and the wave's end takes them all away. Fords' pure <c>PoisonRules</c> is underneath; it holds plain dictionaries, so this is called on the main
/// thread only (a hit or a removal reaches it through the director's <c>RunOrDefer</c>).
/// </summary>
internal sealed partial class SurvivalRun
{
    private LdPoisonBook? _poison;

    /// <summary>True while a Morgul Blades wave is running: its hits are wanted and its doses tick.</summary>
    public bool PoisonWave => _poison != null;

    /// <summary>
    /// An attacker (<paramref name="attacker"/>) struck a defender (<paramref name="victim"/>). Takes a dose in a blades wave only, and drops the hit when either key was forgotten. Doses are counted apart
    /// from hits: a victim already poisoned is refreshed, not dosed again.
    /// </summary>
    public void DefenderHit(AgentKey victim, AgentKey attacker)
    {
        if (_poison is not { } book || Result != null)
            return;

        var doses = book.Doses;
        if (!book.Hit(victim, attacker))
            return;

        _stats.Poison.Hits++;
        _stats.Poison.Doses += book.Doses - doses;
    }

    /// <summary>The engine removed this agent, an attacker or a defender: its dose goes and a later hit naming it is dropped.</summary>
    public void Forget(AgentKey agent) => _poison?.Forget(agent);

    /// <summary>What the adapter did with one poison tick: the health it took, and whether the owner was not active at the call or the killing tick was withheld.</summary>
    public void PoisonApplied(int damage, bool ownerMissing, bool withheld)
    {
        var poison = _stats.Poison;
        poison.Damage += Math.Max(0, damage);
        if (ownerMissing)
            poison.OwnerMissing++;
        if (withheld)
            poison.Withheld++;
    }

    private void InitPoison() => _poison = RunModifiers.Blades(_plan.Affixes, _waveAffixes);

    private void EndPoison() => _poison = null;

    /// <summary>
    /// The doses' tick, on the wave's fighting seconds, while an attacker stands to own a kill (the tick that clears the wave has none, and a poisoned defender then keeps what health he has). The ticks go
    /// out in <see cref="RunEffects.PoisonTicks"/>; the adapter applies them and reports back through <see cref="PoisonApplied"/>.
    /// </summary>
    private void TickPoison(float dt, int attackersStanding, RunEffects fx)
    {
        if (_poison is not { } book || attackersStanding <= 0)
            return;

        fx.PoisonTicks.AddRange(book.Tick(dt));
    }
}
