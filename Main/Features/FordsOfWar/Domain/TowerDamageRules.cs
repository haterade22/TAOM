using System;
using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The tower damage table. A blow from the tower's own side takes nothing whatever the tower's state (the engine does not
/// filter those blows in single player), then locked, destroyed and unknown towers take nothing. Otherwise the factors for ranged hits,
/// hero attackers and siege units multiply (a hero archer gets both), rounded to the nearest point, never below 1.
/// Vanilla multiplies a melee blow's base magnitude on an object (built from speed and weight, not the weapon's damage stat) by the
/// entity factor (Pierce 0.1) and, for a thrust that hits with the shaft, by 0.15, then truncates to an int. The floor raises any
/// non-ranged blow that arrives as 0 from a weapon with thrust damage, because OnHit carries no strike type (in practice thrusts, and
/// rarely a zero swing from a weapon that has thrust damage) to <c>ThrustFloorFactor</c> of that thrust damage, then it runs the same table.
/// </summary>
internal sealed class TowerDamageRules
{
    private readonly FordsTowerConfig _config;
    private readonly TowerChain _chain;
    private readonly IReadOnlyDictionary<MatchSide, FactionFeats> _feats;
    private readonly UpgradeBook? _upgrades;

    public TowerDamageRules(FordsTowerConfig config, TowerChain chain, IReadOnlyDictionary<MatchSide, FactionFeats>? feats = null, UpgradeBook? upgrades = null)
    {
        _config = config;
        _chain = chain;
        _feats = feats ?? new Dictionary<MatchSide, FactionFeats>();
        _upgrades = upgrades;
    }

    public int Effective(TowerHit hit) => Decide(hit, out _);

    /// <summary>The damage the tower takes, and which branch of the table decided it.</summary>
    public int Decide(TowerHit hit, out TowerDamageReason reason)
    {
        // The engine's destructible never reads a side, so friendly fire (gunners shooting across their own tower) arrives here.
        // It is labelled before the tower's state so a blow on a locked or fallen tower is never counted as Locked or Destroyed.
        if (IsOwnSide(hit))
        {
            reason = TowerDamageReason.OwnSide;
            return 0;
        }

        var raw = FlooredRaw(hit);
        if (raw <= 0)
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

        var feat = FeatFactor(hit);
        if (feat <= 0.0)
        {
            reason = TowerDamageReason.FactionFeat;
            return 0;
        }

        double factor = feat * UpgradeFactor(hit);
        if (hit.Ranged)
            factor *= _config.RangedDamageFactor;
        if (hit.AttackerRole == UnitRole.Hero)
            factor *= _config.HeroDamageFactor;
        if (hit.AttackerRole == UnitRole.Siege)
            factor *= _config.SiegeDamageFactor;

        var scaled = (int)Math.Round(raw * factor, MidpointRounding.AwayFromZero);
        reason = TowerDamageReason.Applied;
        return Math.Max(1, scaled);
    }

    // The attacker's siegecraft and the tower owner's fortify.
    private double UpgradeFactor(TowerHit hit)
    {
        if (_upgrades == null)
            return 1.0;

        var attack = hit.AttackerSide is { } side ? _upgrades.Factor(side, UpgradeEffect.SiegeDamage) : 1f;
        var defence = _chain.Slot(hit.TowerId) is { } slot ? _upgrades.Factor(slot.Owner, UpgradeEffect.Fortify) : 1f;
        return attack * defence;
    }

    /// <summary>The attacker's tower damage feat alone: 1 for a blow with no known side.</summary>
    public float DamageFeat(TowerHit hit) =>
        hit.AttackerSide is { } side && _feats.TryGetValue(side, out var feats) ? feats.DamageToTowers : 1f;

    private double FeatFactor(TowerHit hit)
    {
        if (!(hit.AttackerSide is { } side) || !_feats.TryGetValue(side, out var feats))
            return 1.0;

        var mounted = hit.AttackerRole == UnitRole.Cavalry || hit.AttackerRole == UnitRole.HorseArcher;
        return feats.DamageToTowers * (mounted ? feats.MountedDamageToTowers : 1f) * (hit.Ranged ? feats.RangedDamageToTowers : 1f);
    }

    // The engine's raw damage, or for a zero melee blow from a weapon with thrust damage the floor (0 when the factor is 0).
    private int FlooredRaw(TowerHit hit) =>
        hit.RawDamage > 0 || hit.Ranged || hit.ThrustDamage <= 0
            ? hit.RawDamage
            : (int)Math.Round(hit.ThrustDamage * _config.ThrustFloorFactor, MidpointRounding.AwayFromZero);

    private bool IsOwnSide(TowerHit hit) => hit.AttackerSide is { } side && _chain.Slot(hit.TowerId)?.Owner == side;
}
