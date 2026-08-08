using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.Enlistment.Duties;

public class DutySelector : IDutySelector
{
    private const int IncidentRollPrecision = 1000;

    /// <summary>
    /// Upper bound on a data row's <c>GateSpec.Weight</c>. Keeps the cumulative sum
    /// (weight x affinity multiplier, summed over every eligible row) far below int overflow —
    /// an overflowed total goes negative and throws out of <c>IRandomProvider.Next</c>.
    /// </summary>
    public const int MaxSelectionWeight = 1000;

    private readonly IRandomProvider _random;
    private readonly IModLogger _logger;

    /// <summary>Ids already warned about, so a bad weight logs once rather than every offer tick.</summary>
    private readonly HashSet<string> _warnedWeightIds = new HashSet<string>(System.StringComparer.Ordinal);

    public DutySelector(IRandomProvider random, IModLogger logger)
    {
        _random = random;
        _logger = logger;
    }

    public DutyOfferSelection SelectOffer(
        EnlistmentDutiesConfig duties,
        ServiceProgressSnapshot progress,
        ArmyRhythmSnapshot rhythm,
        IReadOnlyList<string> recentDutyIds,
        bool pressure)
    {
        if (duties == null || progress == null)
            return DutyOfferSelection.None;

        var activeContexts = DutyGateEvaluator.ActiveContexts(rhythm);
        var recent = recentDutyIds ?? System.Array.Empty<string>();

        var eligibleFields = Eligible(duties.FieldDuties, d => d.Gates, d => d.Id, progress, activeContexts, recent, pressure);
        var eligibleInteractive = Eligible(duties.InteractiveDuties, d => d.Gates, d => d.Id, progress, activeContexts, recent, pressure);

        var fieldWeight = eligibleFields.Sum(e => e.Weight);
        var interactiveWeight = eligibleInteractive.Sum(e => e.Weight);
        var totalWeight = fieldWeight + interactiveWeight;
        if (totalWeight <= 0)
            return DutyOfferSelection.None;

        var roll = _random.Next(totalWeight);
        if (roll < fieldWeight)
            return new DutyOfferSelection { FieldDuty = WeightedPick(eligibleFields, roll) };

        return new DutyOfferSelection { InteractiveDuty = WeightedPick(eligibleInteractive, roll - fieldWeight) };
    }

    public IncidentDefinition SelectIncident(
        IReadOnlyList<IncidentDefinition> incidents,
        ServiceProgressSnapshot progress,
        ArmyRhythmSnapshot rhythm)
    {
        if (incidents == null || progress == null)
            return null;

        var activeContexts = DutyGateEvaluator.ActiveContexts(rhythm);
        foreach (var incident in incidents)
        {
            if (!DutyGateEvaluator.IsEligible(incident.Gates, progress.Rank, progress.Trust, activeContexts))
                continue;

            var threshold = (int)(incident.Chance * IncidentRollPrecision);
            if (_random.Next(IncidentRollPrecision) < threshold)
                return incident;
        }
        return null;
    }

    private List<(T Item, int Weight)> Eligible<T>(
        IEnumerable<T> pool,
        System.Func<T, GateSpec> gatesOf,
        System.Func<T, string> idOf,
        ServiceProgressSnapshot progress,
        ISet<string> activeContexts,
        IReadOnlyList<string> recent,
        bool pressure)
    {
        var result = new List<(T Item, int Weight)>();
        foreach (var item in pool ?? System.Array.Empty<T>())
        {
            var gates = gatesOf(item);
            if (!DutyGateEvaluator.IsEligible(gates, progress.Rank, progress.Trust, activeContexts))
                continue;
            if (!pressure && recent.Contains(idOf(item)))
                continue;

            // Missing gates means "no gating at all", so it also means the default rarity.
            var rarity = gates == null ? 1 : gates.Weight;

            // Positive requirement: anything not inside [1,Max] — including a 0 someone meant as
            // "disabled" — is skipped outright rather than folded into the cumulative sum.
            if (!(rarity > 0 && rarity <= MaxSelectionWeight))
            {
                WarnOnceOnUnusableWeight(idOf(item), rarity);
                continue;
            }

            result.Add((item, DutyGateEvaluator.AffinityWeight(gates, progress.Assignment) * rarity));
        }
        return result;
    }

    private void WarnOnceOnUnusableWeight(string id, int weight)
    {
        var key = id ?? "";
        if (!_warnedWeightIds.Add(key))
            return;

        _logger?.LogWarning(
            $"DutySelector: duty '{key}' skipped — gates.weight={weight} must be in [1,{MaxSelectionWeight}]. "
            + "Zero or negative is a selection bug, not a disable switch; delete the row or raise its gates instead.");
    }

    private static T WeightedPick<T>(List<(T Item, int Weight)> pool, int roll)
    {
        var cumulative = 0;
        foreach (var entry in pool)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
                return entry.Item;
        }
        return pool.Count > 0 ? pool[pool.Count - 1].Item : default;
    }
}
