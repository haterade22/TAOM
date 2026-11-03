using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// The levels each side has bought on each upgrade track, and what they come to. A level is shared by the whole side and lasts for the match. A faction's
/// <see cref="FactionCatalog.UpgradeCost"/> scales its prices. Damage factors rise with the level; armour and fortify factors fall, but never below <see cref="LeastTaken"/>.
/// </summary>
internal sealed class UpgradeBook
{
    private const float LeastTaken = 0.1f;
    private const int PriceStep = 5;

    private readonly IReadOnlyDictionary<MatchSide, FactionCatalog> _factions;
    private readonly Dictionary<(MatchSide, string), int> _levels = new();
    private readonly Dictionary<MatchSide, CombatUpgradeFactors> _combat = new();

    public UpgradeBook(IReadOnlyList<UpgradeTrack> tracks, IReadOnlyDictionary<MatchSide, FactionCatalog> factions)
    {
        Tracks = tracks;
        _factions = factions;
    }

    public IReadOnlyList<UpgradeTrack> Tracks { get; }

    public bool Knows(string trackId) => Find(trackId) != null;

    public int Level(MatchSide side, string trackId) => _levels.TryGetValue((side, trackId), out var level) ? level : 0;

    /// <summary>What this side pays for the track's next level; null at the top level or for an unknown track.</summary>
    public int? NextPrice(MatchSide side, string trackId)
    {
        var track = Find(trackId);
        var level = Level(side, trackId);
        if (track == null || level >= track.Prices.Count)
            return null;

        var cost = _factions.TryGetValue(side, out var faction) && faction.UpgradeCost != null && faction.UpgradeCost.TryGetValue(trackId, out var factor) ? factor : 1f;
        return Math.Max(PriceStep, (int)Math.Round(track.Prices[level] * cost / PriceStep, MidpointRounding.AwayFromZero) * PriceStep);
    }

    public bool TryRaise(MatchSide side, string trackId)
    {
        if (NextPrice(side, trackId) == null)
            return false;

        _levels[(side, trackId)] = Level(side, trackId) + 1;
        _combat.Remove(side);
        return true;
    }

    /// <summary>The factor on this side of everything the effect changes; 1 with no level bought.</summary>
    public float Factor(MatchSide side, UpgradeEffect effect)
    {
        var step = Tracks.Where(t => t.Effect == effect).Sum(t => t.StepPerLevel * Level(side, t.Id));
        return effect is UpgradeEffect.MeleeDamage or UpgradeEffect.MissileDamage or UpgradeEffect.SiegeDamage ? 1f + step : Math.Max(LeastTaken, 1f - step);
    }

    /// <summary>The same value until the side buys another level, so reading it each tick allocates nothing.</summary>
    public CombatUpgradeFactors CombatFactors(MatchSide side)
    {
        if (_combat.TryGetValue(side, out var factors))
            return factors;

        factors = new CombatUpgradeFactors(Factor(side, UpgradeEffect.MeleeDamage), Factor(side, UpgradeEffect.MissileDamage),
            Factor(side, UpgradeEffect.MeleeArmour), Factor(side, UpgradeEffect.MissileArmour));
        _combat[side] = factors;
        return factors;
    }

    private UpgradeTrack? Find(string trackId) => Tracks.FirstOrDefault(t => t.Id == trackId);
}
