using System;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Master armourers who visit a town for a few days and raise its armoury level (KEYforce's "event"
/// route for heavy and elite stock). The towns and their Barracks come from the town adapter; the visits
/// live in <see cref="ArmourAcquisitionState"/>, so they survive a save.
/// </summary>
public sealed class VisitingArmourerService
{
    private readonly ArmourAcquisitionState _state;
    private readonly IArmourAcquisitionConfigProvider _config;
    private readonly IArmouryTownAdapter _towns;

    public VisitingArmourerService(ArmourAcquisitionState state, IArmourAcquisitionConfigProvider config, IArmouryTownAdapter towns)
    {
        _state = state;
        _config = config;
        _towns = towns;
    }

    /// <summary>
    /// Ends every visit whose last day has come, then (when enabled) rolls for a new one at a town not
    /// already visited whose armoury can still rise. Returns the settlement id the new visit went to, or null.
    /// </summary>
    public string? OnDailyTick(int today, Random rng, bool enabled)
    {
        foreach (var id in _state.VisitUntilDay.Where(v => v.Value <= today).Select(v => v.Key).ToList())
            _state.VisitUntilDay.Remove(id);

        if (!enabled)
            return null;
        var config = _config.GetConfig();
        // Compared in float, the chance's own precision (0.1f widened to double is 0.1000000015).
        if (!((float)rng.NextDouble() < config.VisitChancePerDay))
            return null;

        // A town whose Barracks is already at the top gains nothing from a visit, so it is never chosen.
        var candidates = _towns.AllTownIds()
            .Where(id => !string.IsNullOrEmpty(id) && !_state.VisitUntilDay.ContainsKey(id)
                         && _towns.GetBarracksLevel(id) < ArmourAcquisitionConfig.MaxArmouryLevel)
            .ToList();
        if (candidates.Count == 0)
            return null;
        var town = candidates[rng.Next(candidates.Count)];
        _state.VisitUntilDay[town] = today + config.VisitDurationDays;
        return town;
    }

    /// <summary>The armoury levels a visit adds to a town today: the configured bonus, or 0.</summary>
    public int GetBonus(string settlementId, int today) =>
        settlementId != null && _state.VisitUntilDay.TryGetValue(settlementId, out var until) && today < until
            ? _config.GetConfig().VisitLevelBonus
            : 0;
}
