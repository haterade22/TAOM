using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>How a queued step ends (build-spec 5.2): at once, on arrival, when its target company is gone, or when an entity order is spent.</summary>
internal enum StepEnd
{
    AtOnce,
    Arrival,
    TargetGone,
    EntityOrderEnded,
    ImplicitArrival,
}

/// <summary>
/// The per-company FIFO lists of build-spec 5.3: a step shared by several companies appears in each one's list, and it starts only
/// when it heads every member's list.
/// </summary>
internal sealed class MarshalOrderQueue
{
    private readonly Dictionary<int, List<QueueStep>> _lists = new();
    private long _nextId = 1;

    /// <summary>Bumped on every change to a list: the one definition of "something changed" for the driver and for the flags.</summary>
    internal int Version { get; private set; }

    /// <summary>No company holds a step, because a list is never kept empty.</summary>
    internal bool IsEmpty => _lists.Count == 0;

    internal IReadOnlyCollection<int> Companies => _lists.Keys.ToList();

    internal int TotalSteps => _lists.Values.SelectMany(l => l).Distinct().Count();

    internal IReadOnlyList<QueueStep> StepsOf(int company) => _lists.TryGetValue(company, out var list) ? list : Array.Empty<QueueStep>();

    internal int CountOf(int company) => _lists.TryGetValue(company, out var list) ? list.Count : 0;

    internal bool IsFull(IReadOnlyList<int> companies) => companies.Any(c => CountOf(c) >= MarshalTuning.MaxStepsPerCompany);

    internal QueueStep Enqueue(IReadOnlyList<int> companies, StepEnd end, object? payload, bool needsWaitTest)
    {
        var step = new QueueStep(_nextId++, companies.Distinct().ToList(), end, payload, needsWaitTest);
        foreach (var company in step.Companies)
            ListOf(company).Add(step);
        Version++;
        return step;
    }

    /// <summary>An implicit step that only waits for arrival, placed ahead of the company's current head (5.2, "Empty queue while a Move is under way").</summary>
    internal QueueStep EnqueueAhead(int company)
    {
        var step = new QueueStep(_nextId++, new List<int> { company }, StepEnd.ImplicitArrival, null, needsWaitTest: false);
        ListOf(company).Insert(0, step);
        Version++;
        return step;
    }

    /// <summary>Steps not yet started that head the list of every member.</summary>
    internal IReadOnlyList<QueueStep> Ready()
    {
        var ready = new List<QueueStep>();
        foreach (var list in _lists.Values)
        {
            if (list.Count == 0)
                continue;
            var head = list[0];
            if (!head.Started && !ready.Contains(head) && head.Companies.All(c => _lists[c][0] == head))
                ready.Add(head);
        }

        return ready.OrderBy(s => s.Id).ToList();
    }

    /// <summary>Started steps still holding at least one member, oldest first.</summary>
    internal IReadOnlyList<QueueStep> Running() =>
        _lists.Values.SelectMany(l => l).Where(s => s.Started).Distinct().OrderBy(s => s.Id).ToList();

    /// <summary>Steps flagged for the implicit wait test that head the list of at least one member.</summary>
    internal IReadOnlyList<QueueStep> WaitTestPending() =>
        _lists.Values.Where(l => l.Count > 0 && l[0].NeedsWaitTest && !l[0].Started).Select(l => l[0]).Distinct().OrderBy(s => s.Id).ToList();

    internal void Start(QueueStep step, double now, double patience)
    {
        step.Started = true;
        step.StartedAt = now;
        step.Patience = patience;
        Version++;
    }

    /// <summary>A member has finished the step: it leaves the step and its own list advances. The step goes when no member is left.</summary>
    internal void Finish(QueueStep step, int company)
    {
        Detach(step, company);
        Version++;
    }

    /// <summary>
    /// Takes a company out of every step it held, because it dropped out or a direct player order cleared its queue (5.3, rules 5 and 6), and
    /// returns how many steps that was.
    /// </summary>
    internal int RemoveCompany(int company)
    {
        if (!_lists.TryGetValue(company, out var list))
            return 0;
        var count = list.Count;
        foreach (var step in list)
            step.Companies.Remove(company);
        _lists.Remove(company);
        Version++;
        return count;
    }

    internal void ClearAll()
    {
        foreach (var list in _lists.Values)
            foreach (var step in list)
                step.Companies.Clear();
        _lists.Clear();
        Version++;
    }

    /// <summary>A step is dropped whole, with no order (5.3, rule 2).</summary>
    internal void Drop(QueueStep step)
    {
        foreach (var company in step.Companies.ToList())
            Detach(step, company);
        Version++;
    }

    /// <summary>
    /// The time a step may take: <c>max(MinPatience, PatiencePerEta * distance / speed)</c>, with the speed floored at 1 because
    /// <c>Formation.CachedMovementSpeed</c> has a floor of only 0.1 (Formation.cs:1450, 1469). A number that is not finite, or a negative
    /// distance, gives the minimum.
    /// </summary>
    internal static double PatienceFor(double distance, double speed)
    {
        if (double.IsNaN(distance) || double.IsInfinity(distance) || distance < 0 || double.IsNaN(speed) || double.IsInfinity(speed))
            return MarshalTuning.MinPatience;
        return Math.Max(MarshalTuning.MinPatience, MarshalTuning.PatiencePerEta * distance / Math.Max(speed, 1.0));
    }

    private List<QueueStep> ListOf(int company)
    {
        if (!_lists.TryGetValue(company, out var list))
            _lists[company] = list = new List<QueueStep>();
        return list;
    }

    private void Detach(QueueStep step, int company)
    {
        step.Companies.Remove(company);
        if (!_lists.TryGetValue(company, out var list))
            return;
        list.Remove(step);
        if (list.Count == 0)
            _lists.Remove(company);
    }
}
