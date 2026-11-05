using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// The driver of build-spec 5.3: it feeds the pure queue the engine's facts and issues what the queue says is ready, with
/// <see cref="Capture"/> in the screen tick (it only reads) and <see cref="Pass"/> in <c>OnMissionTick</c>.
/// </summary>
internal sealed class MarshalQueueDriver
{
    private const int MaxRounds = 8;

    private readonly IMarshalOrderAdapter _orders;
    private readonly IOutcomeLog _outcomes;
    private readonly string _key;
    private readonly List<(IReadOnlyList<int> Companies, object Payload)> _marks = new();
    private double _nextPass = double.NegativeInfinity;
    private int _finished;
    private int _endedByPatience;
    private int _endedByArrival;
    private int _captured;

    internal MarshalQueueDriver(IMarshalOrderAdapter orders, IOutcomeLog outcomes, string missionKey)
    {
        _orders = orders;
        _outcomes = outcomes;
        _key = missionKey;
    }

    internal MarshalOrderQueue Queue { get; } = new();

    /// <summary>Steps whose last member left, for the patience share and the field trial.</summary>
    internal int Finished => _finished;

    /// <summary>Of those, the steps that patience ended.</summary>
    internal int EndedByPatience => _endedByPatience;

    /// <summary>Of those, the steps that ended with <c>reason=arrival</c>.</summary>
    internal int EndedByArrival => _endedByArrival;

    /// <summary>Steps the capture stored (a mark waiting its turn is a step too), for the field trial.</summary>
    internal int Captured => _captured;

    /// <summary>Stores the selected companies' order as a step, or says why not (5.2). <paramref name="end"/> is null for an order off the allow list.</summary>
    internal CaptureResult Capture(StepEnd? end, object payload, string kind)
    {
        var selected = _orders.SelectedCompanies();
        if (selected.Count == 0)
            return Decline("none-selected");
        if (end == null)
            return Decline("not-allowed");
        if (_orders.IsDeployment)
            return Decline("deployment");
        if (selected.Any(c => !_orders.IsControllable(c)))
            return Decline("ai-controlled");

        if (Queue.IsFull(selected))
        {
            _outcomes.Skip(MarshalChecks.CapturedId, _key + ":full", "mission=" + _key + " reason=full");
            return CaptureResult.Full;
        }

        var needsWaitTest = selected.Any(c => Queue.CountOf(c) == 0);
        Queue.Enqueue(selected, end.Value, payload, needsWaitTest);
        _captured++;
        _outcomes.Pass(MarshalChecks.CapturedId, _key + ":" + kind, "mission=" + _key + " kind=" + kind + " companies=" + Count(selected.Count));
        return CaptureResult.Queued;
    }

    /// <summary>
    /// A held mark key turned the player's charge on an enemy company into a mark (8.1): one request for every selected company under the
    /// player's control, applied at the next pass because the setter writes native state per soldier. Vanilla runs the order when no such
    /// company exists, so the click is not swallowed.
    /// </summary>
    internal CaptureResult CaptureMark(object markPayload)
    {
        if (_orders.IsDeployment)
            return Decline("deployment");
        var selected = _orders.SelectedCompanies();
        var companies = selected.Where(_orders.IsControllable).ToList();
        if (companies.Count == 0)
            return Decline(selected.Count == 0 ? "none-selected" : "ai-controlled");

        _marks.Add((companies, markPayload));
        return CaptureResult.Queued;
    }

    /// <summary>A direct player order cleared these companies' queues (5.3, rule 6).</summary>
    internal void OnDirectOrder(IReadOnlyList<int> companies)
    {
        foreach (var company in companies)
        {
            var steps = Queue.RemoveCompany(company);
            if (steps > 0)
                _outcomes.Pass(MarshalChecks.ClearedId, _key, "mission=" + _key + " company=" + Count(company) + " steps=" + Count(steps));
        }
    }

    /// <summary>Each stored company number named a formation of the old team (5.3, rule 6).</summary>
    internal void OnPlayerTeamChanged()
    {
        _marks.Clear();
        var steps = Queue.TotalSteps;
        Queue.ClearAll();
        _outcomes.Skip(MarshalChecks.ClearedId, _key + ":team", "mission=" + _key + " reason=team-changed steps=" + Count(steps));
    }

    /// <summary>One slow pass: drop the companies that left, test the walks, start what is ready, finish what is done.</summary>
    internal void Pass()
    {
        ApplyMarks();
        var now = _orders.Now;
        if (now < _nextPass)
            return;
        _nextPass = now + MarshalTuning.SlowPassSeconds;
        if (Queue.IsEmpty)
            return;

        DropOut();
        for (var round = 0; round < MaxRounds; round++)
        {
            var before = Queue.Version;
            WaitBehindWalksUnderWay();
            StartReady(now);
            FinishRunning(now);
            if (Queue.Version == before)
                break;
        }
    }

    private void ApplyMarks()
    {
        if (_marks.Count == 0)
            return;
        var requests = _marks.ToList();
        _marks.Clear();
        foreach (var (companies, payload) in requests)
        {
            if (_orders.Issue(companies, payload))
                _outcomes.Pass(MarshalChecks.MarkId, _key, "mission=" + _key + " companies=" + Count(companies.Count));
            else
                _outcomes.Skip(MarshalChecks.MarkId, _key + ":not-applied", "mission=" + _key + " reason=not-applied");
        }
    }

    private CaptureResult Decline(string reason)
    {
        _outcomes.Skip(MarshalChecks.DeclinedId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);
        return CaptureResult.Declined;
    }

    private void DropOut()
    {
        foreach (var company in Queue.Companies)
            if (!_orders.IsControllable(company))
                Lose(company);
    }

    private void Lose(int company)
    {
        Queue.RemoveCompany(company);
        _outcomes.Skip(MarshalChecks.DroppedId, _key + ":lost-control", "mission=" + _key + " reason=lost-control company=" + Count(company));
    }

    private void WaitBehindWalksUnderWay()
    {
        foreach (var step in Queue.WaitTestPending())
        {
            step.NeedsWaitTest = false;
            foreach (var company in step.Companies)
            {
                if (Queue.StepsOf(company)[0] == step && _orders.WalkUnderWay(company))
                    Queue.EnqueueAhead(company);
            }
        }
    }

    private void StartReady(double now)
    {
        foreach (var step in Queue.Ready())
        {
            foreach (var company in step.Companies.ToList())
                if (!_orders.IsControllable(company))
                    Lose(company);
            if (step.Companies.Count == 0 || (step.Payload != null && !IssueOrDrop(step)))
                continue;

            Queue.Start(step, now, PatienceOf(step));
            _outcomes.Pass(MarshalChecks.StartedId, _key + ":" + step.End, "mission=" + _key + " end=" + step.End + " companies=" + Count(step.Companies.Count));
            if (step.End == StepEnd.AtOnce)
                FinishAll(step, "at-once");
        }
    }

    private bool IssueOrDrop(QueueStep step)
    {
        if (_orders.PayloadSpent(step.Payload!))
            return Drop(step, "spent");
        return _orders.Issue(step.Companies, step.Payload!) || Drop(step, "none-selected");
    }

    private bool Drop(QueueStep step, string reason)
    {
        Queue.Drop(step);
        _outcomes.Skip(MarshalChecks.DroppedId, _key + ":" + reason, "mission=" + _key + " reason=" + reason);
        return false;
    }

    private double PatienceOf(QueueStep step)
    {
        if (CannotEndByPatience(step))
            return 0.0;
        return step.Companies.Max(c => MarshalOrderQueue.PatienceFor(_orders.RemainingDistance(c, step.Payload), _orders.Speed(c)));
    }

    private void FinishRunning(double now)
    {
        foreach (var step in Queue.Running())
        {
            switch (step.End)
            {
                case StepEnd.TargetGone when step.Payload == null || _orders.PayloadSpent(step.Payload):
                    FinishAll(step, "target-gone");
                    break;
                case StepEnd.Arrival:
                case StepEnd.ImplicitArrival:
                    foreach (var company in step.Companies.Where(_orders.Arrived).ToList())
                        FinishMember(step, company, "arrival");
                    break;
                case StepEnd.EntityOrderEnded:
                    foreach (var company in step.Companies.Where(_orders.OrderEnded).ToList())
                        FinishMember(step, company, "entity-ended");
                    break;
            }

            if (step.Companies.Count > 0 && step.End != StepEnd.TargetGone && now - step.StartedAt > step.Patience)
                FinishAll(step, "patience");
        }
    }

    private void FinishAll(QueueStep step, string reason)
    {
        foreach (var company in step.Companies.ToList())
            FinishMember(step, company, reason);
    }

    private void FinishMember(QueueStep step, int company, string reason)
    {
        Queue.Finish(step, company);
        MarkHandOver(step, company, reason);
        var detail = "mission=" + _key + " reason=" + reason + " end=" + step.End + " company=" + Count(company) + " behind=" + (step.Behind ? "1" : "0");
        var key = _key + ":" + reason + ":" + step.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (reason == "patience")
            _outcomes.Skip(MarshalChecks.FinishedId, key, detail);
        else
            _outcomes.Pass(MarshalChecks.FinishedId, key, detail);

        if (step.Companies.Count > 0 || CannotEndByPatience(step))
            return;
        _finished++;
        if (reason == "patience")
            _endedByPatience++;
        else if (reason == "arrival")
            _endedByArrival++;
        Judge();
    }

    private void MarkHandOver(QueueStep step, int company, string reason)
    {
        if (reason != "arrival" || step.Payload == null || step.Companies.Count > 0 || Queue.CountOf(company) == 0)
            return;
        var next = Queue.StepsOf(company)[0];
        if (!next.Started)
            next.Behind = true;
    }

    private void Judge()
    {
        var detail = "mission=" + _key + " finished=" + Count(_finished) + " by-patience=" + Count(_endedByPatience);
        if (MarshalChecks.PatienceTooOften(_finished, _endedByPatience))
            _outcomes.Fail(MarshalChecks.PatienceId, _key, detail);
        else if (_finished == MarshalTuning.PatienceJudgeAfter)
            _outcomes.Pass(MarshalChecks.PatienceId, _key, detail);
    }

    private static bool CannotEndByPatience(QueueStep step) => step.End == StepEnd.AtOnce || step.End == StepEnd.TargetGone;

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
