using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>One moment of poison for the engine layer to take off a unit: it ignores armour, and Attacker gets the kill.</summary>
internal sealed record PoisonTick(AgentKey Victim, AgentKey Attacker, int Damage);

/// <summary>
/// Who is poisoned and for how long. A victim carries one dose: another hit refreshes the time, and a stronger poison replaces a weaker one, so massed archers cannot stack it onto one target.
/// Fractions of a point carry over between ticks, because a tick is much shorter than a second.
/// </summary>
internal sealed class PoisonRules
{
    private sealed class Dose
    {
        public AgentKey Attacker;
        public MatchSide Side;
        public float PerSecond;
        public float SecondsLeft;
        public double Carry;
    }

    private readonly Dictionary<AgentKey, Dose> _doses = new();
    private readonly Dictionary<MatchSide, (int Doses, long Damage)> _totals = new();

    public void Apply(AgentKey victim, AgentKey attacker, MatchSide side, float perSecond, float seconds)
    {
        var (doses, damage) = Totals(side);
        _totals[side] = (doses + 1, damage);
        if (_doses.TryGetValue(victim, out var dose) && dose.PerSecond > perSecond)
            return;

        var carry = dose?.Carry ?? 0;
        _doses[victim] = new Dose { Attacker = attacker, Side = side, PerSecond = perSecond, SecondsLeft = seconds, Carry = carry };
    }

    public List<PoisonTick> Tick(float dt)
    {
        var ticks = new List<PoisonTick>();
        foreach (var pair in _doses.ToList())
        {
            var dose = pair.Value;
            var seconds = dt < dose.SecondsLeft ? dt : dose.SecondsLeft;
            dose.SecondsLeft -= seconds;
            dose.Carry += seconds * (double)dose.PerSecond;
            var whole = (int)System.Math.Floor(dose.Carry + 1e-6);
            if (whole > 0)
            {
                dose.Carry -= whole;
                ticks.Add(new PoisonTick(pair.Key, dose.Attacker, whole));
                var (doses, damage) = Totals(dose.Side);
                _totals[dose.Side] = (doses, damage + whole);
            }

            if (dose.SecondsLeft <= 0f)
                _doses.Remove(pair.Key);
        }

        return ticks;
    }

    public void Forget(AgentKey victim) => _doses.Remove(victim);

    /// <summary>The doses this side has landed and the damage they did, for the log at the end of a match.</summary>
    public (int Doses, long Damage) Totals(MatchSide side) => _totals.TryGetValue(side, out var totals) ? totals : (0, 0L);
}
