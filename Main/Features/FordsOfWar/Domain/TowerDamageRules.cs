using System;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The tower damage table. A locked, destroyed or unknown tower takes nothing, and neither does a tower hit by its own side
/// (the engine does not filter those blows in single player). Otherwise the factors for ranged hits,
/// hero attackers and siege units multiply (a hero archer gets both), rounded to the nearest point, never below 1.
/// </summary>
internal sealed class TowerDamageRules
{
    private readonly FordsTowerConfig _config;
    private readonly TowerChain _chain;

    public TowerDamageRules(FordsTowerConfig config, TowerChain chain)
    {
        _config = config;
        _chain = chain;
    }

    public int Effective(TowerHit hit) => Decide(hit, out _);

    /// <summary>The damage the tower takes, and which branch of the table decided it.</summary>
    public int Decide(TowerHit hit, out TowerDamageReason reason)
    {
        if (hit.RawDamage <= 0)
        {
            reason = TowerDamageReason.RawZero;
            return 0;
        }

        if (!_chain.IsTower(hit.TowerId))
        {
            reason = TowerDamageReason.UnknownTower;
            return 0;
        }

        if (_chain.IsDestroyed(hit.TowerId))
        {
            reason = TowerDamageReason.Destroyed;
            return 0;
        }

        if (_chain.IsLocked(hit.TowerId))
        {
            reason = TowerDamageReason.Locked;
            return 0;
        }

        // The engine's destructible never reads a side, so friendly fire (gunners shooting across their own tower) arrives here.
        if (hit.AttackerSide is { } side && _chain.Slot(hit.TowerId)?.Owner == side)
        {
            reason = TowerDamageReason.OwnSide;
            return 0;
        }

        double factor = 1.0;
        if (hit.Ranged)
            factor *= _config.RangedDamageFactor;
        if (hit.AttackerRole == UnitRole.Hero)
            factor *= _config.HeroDamageFactor;
        if (hit.AttackerRole == UnitRole.Siege)
            factor *= _config.SiegeDamageFactor;

        var scaled = (int)Math.Round(hit.RawDamage * factor, MidpointRounding.AwayFromZero);
        reason = TowerDamageReason.Applied;
        return Math.Max(1, scaled);
    }
}
