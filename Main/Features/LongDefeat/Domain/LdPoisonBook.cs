using System;
using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The doses of one Morgul Blades wave (phase2-plan.md section 2). Fords' pure <see cref="PoisonRules"/> does the arithmetic (one dose per victim, a stronger dose replaces a weaker, fractions of a point
/// carry between ticks), and this book adds what the Long Defeat needs on top: the run's own count of <see cref="Doses"/> (a victim newly poisoned), because <c>PoisonRules.Totals</c> counts every <c>Apply</c>; and the keys the registry has forgotten, whose hits are
/// dropped. A hit the book accepts is its <see cref="Hit"/> return value, which the run counts as the report's hits. Holds plain dictionaries: main thread only.
/// The attackers are <see cref="MatchSide.East"/>, as in the plan.
/// </summary>
internal sealed class LdPoisonBook
{
    private readonly PoisonRules _rules = new();
    private readonly Dictionary<AgentKey, float> _until = new();
    private readonly HashSet<AgentKey> _forgotten = new();
    private readonly float _perSecond;
    private readonly float _seconds;
    private float _clock;

    /// <param name="perSecond">Damage a dose does each second, ignoring armour.</param>
    /// <param name="seconds">How long one dose lasts.</param>
    public LdPoisonBook(float perSecond, float seconds)
    {
        _perSecond = perSecond;
        _seconds = seconds;
    }

    /// <summary>Victims newly poisoned: a hit on a victim carrying no dose.</summary>
    public int Doses { get; private set; }

    /// <summary>
    /// A blades hit: <paramref name="attacker"/> poisons <paramref name="victim"/>, who then owes damage over time to that attacker. False, with nothing counted, when either key has been
    /// forgotten (the registry no longer holds the agent), or when the rate or the seconds cannot poison anyone.
    /// </summary>
    public bool Hit(AgentKey victim, AgentKey attacker)
    {
        if (_forgotten.Contains(victim) || _forgotten.Contains(attacker) || !(_perSecond > 0f) || !(_seconds > 0f) || float.IsInfinity(_perSecond) || float.IsInfinity(_seconds))
            return false;

        if (!_until.TryGetValue(victim, out var until) || until <= _clock)
            Doses++;

        _until[victim] = _clock + _seconds;
        _rules.Apply(victim, attacker, MatchSide.East, _perSecond, _seconds);
        return true;
    }

    /// <summary>The poison due over <paramref name="dt"/> seconds: whole points for each victim, to the attacker who last dosed him. A bad <paramref name="dt"/> does nothing.</summary>
    public IReadOnlyList<PoisonTick> Tick(float dt)
    {
        if (!(dt > 0f) || float.IsInfinity(dt))
            return Array.Empty<PoisonTick>();

        _clock += dt;
        var ticks = _rules.Tick(dt);
        foreach (var victim in new List<AgentKey>(_until.Keys))
        {
            if (_until[victim] <= _clock)
                _until.Remove(victim);
        }

        return ticks;
    }

    /// <summary>The engine removed this agent (a victim or a poisoner): its dose goes, and a later hit naming it is dropped. A dose it dealt keeps ticking, and the adapter picks another owner.</summary>
    public void Forget(AgentKey agent)
    {
        _forgotten.Add(agent);
        _until.Remove(agent);
        _rules.Forget(agent);
    }
}
