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

    public int Effective(TowerHit hit)
    {
        if (hit.RawDamage <= 0 || !_chain.IsTower(hit.TowerId) || _chain.IsDestroyed(hit.TowerId) || _chain.IsLocked(hit.TowerId))
            return 0;

        // The engine's destructible never reads a side, so friendly fire (gunners shooting across their own tower) arrives here.
        if (hit.AttackerSide is { } side && _chain.Slot(hit.TowerId)?.Owner == side)
            return 0;

        double factor = 1.0;
        if (hit.Ranged)
            factor *= _config.RangedDamageFactor;
        if (hit.AttackerRole == UnitRole.Hero)
            factor *= _config.HeroDamageFactor;
        if (hit.AttackerRole == UnitRole.Siege)
            factor *= _config.SiegeDamageFactor;

        var scaled = (int)Math.Round(hit.RawDamage * factor, MidpointRounding.AwayFromZero);
        return Math.Max(1, scaled);
    }
}
