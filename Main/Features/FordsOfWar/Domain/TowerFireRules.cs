using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The scripted volleys. A ready tower with an enemy inside its own fire range fires at the nearest hero if one is in range,
/// otherwise at the nearest enemy, then waits VolleyIntervalSeconds. Destroyed and unknown towers never fire;
/// a locked tower still does.
/// </summary>
internal sealed class TowerFireRules
{
    private readonly FordsTowerConfig _config;
    private readonly TowerChain _chain;
    private readonly Dictionary<string, float> _cooldowns = new();
    private readonly IReadOnlyDictionary<MatchSide, FactionFeats> _feats;

    public TowerFireRules(FordsTowerConfig config, TowerChain chain, IReadOnlyDictionary<MatchSide, FactionFeats>? feats = null)
    {
        _config = config;
        _chain = chain;
        _feats = feats ?? new Dictionary<MatchSide, FactionFeats>();
    }

    public IReadOnlyList<TowerVolley> Tick(float dt, IReadOnlyList<TowerTargets> targets)
    {
        if (dt > 0f)
        {
            foreach (var id in _cooldowns.Keys.ToList())
                _cooldowns[id] = Math.Max(0f, _cooldowns[id] - dt);
        }

        var volleys = new List<TowerVolley>();
        foreach (var entry in targets)
        {
            if (!_chain.IsTower(entry.TowerId) || _chain.IsDestroyed(entry.TowerId))
                continue;
            if (_cooldowns.TryGetValue(entry.TowerId, out var left) && left > 0f)
                continue;

            var rate = RateOf(entry.TowerId);
            if (rate <= 0f)
                continue;

            var target = PickTarget(entry.EnemiesInRange, _chain.FireRange(entry.TowerId));
            if (target == null)
                continue;

            volleys.Add(new TowerVolley(entry.TowerId, target.Key, _config.VolleyMissiles));
            _cooldowns[entry.TowerId] = _config.VolleyIntervalSeconds / rate;
        }

        return volleys;
    }

    private float RateOf(string towerId) =>
        _chain.Slot(towerId) is { } slot && _feats.TryGetValue(slot.Owner, out var feats) ? feats.TowerFireRate : 1f;

    private static TargetCandidate? PickTarget(IReadOnlyList<TargetCandidate> enemies, float range)
    {
        TargetCandidate? bestHero = null;
        TargetCandidate? bestAny = null;
        foreach (var e in enemies)
        {
            if (e.Distance > range)
                continue;
            if (bestAny == null || e.Distance < bestAny.Distance)
                bestAny = e;
            if (e.IsHero && (bestHero == null || e.Distance < bestHero.Distance))
                bestHero = e;
        }

        return bestHero ?? bestAny;
    }
}
