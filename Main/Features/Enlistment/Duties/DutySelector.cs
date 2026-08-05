using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.Enlistment.Duties;

public class DutySelector : IDutySelector
{
    private const int IncidentRollPrecision = 1000;

    private readonly IRandomProvider _random;

    public DutySelector(IRandomProvider random)
    {
        _random = random;
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

    private static List<(T Item, int Weight)> Eligible<T>(
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
            result.Add((item, DutyGateEvaluator.AffinityWeight(gates, progress.Assignment)));
        }
        return result;
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
