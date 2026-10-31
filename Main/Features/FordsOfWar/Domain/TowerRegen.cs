using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Towers regain RegenPerSecond hit points once RegenDelaySeconds have passed with no damage. Fractions carry over
/// between ticks. Heals go into the <see cref="TowerChain"/> and come back as a list so the engine layer can mirror them.
/// </summary>
internal sealed class TowerRegen
{
    private sealed class Quiet
    {
        public float SinceDamage;
        public double Carry;
    }

    private readonly FordsTowerConfig _config;
    private readonly TowerChain _chain;
    private readonly Dictionary<string, Quiet> _quiet = new();

    public TowerRegen(FordsTowerConfig config, TowerChain chain)
    {
        _config = config;
        _chain = chain;
        foreach (var id in chain.Ids)
            _quiet[id] = new Quiet { SinceDamage = config.RegenDelaySeconds };
    }

    /// <summary>Call whenever a tower takes damage: restarts its quiet timer.</summary>
    public void NotifyDamaged(string towerId)
    {
        if (!_quiet.TryGetValue(towerId, out var q))
            return;

        q.SinceDamage = 0f;
        q.Carry = 0;
    }

    public IReadOnlyList<TowerHeal> Tick(float dt)
    {
        var heals = new List<TowerHeal>();
        if (dt <= 0f)
            return heals;

        foreach (var pair in _quiet)
        {
            var q = pair.Value;
            var before = q.SinceDamage;
            q.SinceDamage += dt;

            if (_chain.IsDestroyed(pair.Key) || _chain.HitPoints(pair.Key) >= _chain.MaxHitPoints(pair.Key))
            {
                q.Carry = 0;
                continue;
            }

            var regenSeconds = q.SinceDamage - Math.Max(before, _config.RegenDelaySeconds);
            if (regenSeconds <= 0f)
                continue;

            q.Carry += regenSeconds * (double)_config.RegenPerSecond;
            var whole = (int)Math.Floor(q.Carry);
            if (whole <= 0)
                continue;

            q.Carry -= whole;
            var healed = _chain.Heal(pair.Key, whole);
            if (healed > 0)
                heals.Add(new TowerHeal(pair.Key, healed));
        }

        return heals;
    }
}
