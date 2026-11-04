using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The queue driver over a fake adapter (build-spec 5.2 and 5.3): what it captures, declines, starts, finishes and logs.</summary>
[TestClass]
public class MarshalQueueDriverTests
{
    private FakeMarshalOrders _orders = null!;
    private RecordingOutcomeLog _log = null!;
    private MarshalQueueDriver _driver = null!;

    [TestInitialize]
    public void Setup()
    {
        _orders = new FakeMarshalOrders();
        _log = new RecordingOutcomeLog();
        _driver = new MarshalQueueDriver(_orders, _log, "7");
    }

    private FakePayload Queue(StepEnd end, params int[] companies)
    {
        _orders.Selected.Clear();
        _orders.Selected.AddRange(companies);
        var payload = new FakePayload("step" + _log.Lines.Count + end);
        Assert.AreEqual(CaptureResult.Queued, _driver.Capture(end, payload, "move"));
        return payload;
    }

    private void Pass(double now)
    {
        _orders.Now = now;
        _driver.Pass();
    }

    private string[] Reasons(string id, string result) =>
        _log.Lines.Where(l => l.Id == id && l.Result == result).Select(l => l.Detail).ToArray();

    // ---- capture ----

    [TestMethod]
    public void Capture_AnAllowedOrderOfASelectedCompany_IsQueued_AndLogged()
    {
        _orders.Selected.Add(2);

        var result = _driver.Capture(StepEnd.Arrival, new FakePayload("a"), "move");

        Assert.AreEqual(CaptureResult.Queued, result);
        Assert.AreEqual(1, _driver.Queue.CountOf(2));
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.CapturedId));
        StringAssert.Contains(_log.Lines.Single().Detail, "kind=move");
    }

    [TestMethod]
    public void Capture_NothingSelected_Declines_SoVanillaRuns()
    {
        var result = _driver.Capture(StepEnd.Arrival, new FakePayload("a"), "move");

        Assert.AreEqual(CaptureResult.Declined, result);
        Assert.AreEqual(0, _driver.Queue.TotalSteps);
        StringAssert.Contains(Reasons(MarshalChecks.DeclinedId, "skip").Single(), "reason=none-selected");
    }

    [TestMethod]
    public void Capture_AnOrderOffTheAllowList_Declines()
    {
        _orders.Selected.Add(1);

        var result = _driver.Capture(null, new FakePayload("followme"), "FollowMe");

        Assert.AreEqual(CaptureResult.Declined, result);
        StringAssert.Contains(Reasons(MarshalChecks.DeclinedId, "skip").Single(), "reason=not-allowed");
    }

    [TestMethod]
    public void Capture_InDeployment_Declines()
    {
        _orders.Selected.Add(1);
        _orders.IsDeployment = true;

        var result = _driver.Capture(StepEnd.Arrival, new FakePayload("a"), "move");

        Assert.AreEqual(CaptureResult.Declined, result);
        StringAssert.Contains(Reasons(MarshalChecks.DeclinedId, "skip").Single(), "reason=deployment");
    }

    [TestMethod]
    public void Capture_ASelectedCompanyUnderAIControl_Declines_SoItTakesTheDirectOrder()
    {
        _orders.Selected.AddRange(new[] { 1, 2 });
        _orders.Lost.Add(2);

        var result = _driver.Capture(StepEnd.Arrival, new FakePayload("a"), "move");

        Assert.AreEqual(CaptureResult.Declined, result);
        Assert.AreEqual(0, _driver.Queue.TotalSteps);
        StringAssert.Contains(Reasons(MarshalChecks.DeclinedId, "skip").Single(), "reason=ai-controlled");
    }

    [TestMethod]
    public void Capture_AFullQueue_SwallowsTheOrder_AndQueuesNothing()
    {
        _orders.Selected.Add(1);
        for (var i = 0; i < MarshalTuning.MaxStepsPerCompany; i++)
            _driver.Capture(StepEnd.AtOnce, new FakePayload("s" + i), "charge");

        var result = _driver.Capture(StepEnd.Arrival, new FakePayload("late"), "move");

        Assert.AreEqual(CaptureResult.Full, result);
        Assert.AreEqual(MarshalTuning.MaxStepsPerCompany, _driver.Queue.CountOf(1));
        StringAssert.Contains(Reasons(MarshalChecks.CapturedId, "skip").Single(), "reason=full");
    }

    [TestMethod]
    public void Capture_AGroupWhereOneCompanyIsFull_SwallowsForTheWholeGroup()
    {
        _orders.Selected.Add(1);
        for (var i = 0; i < MarshalTuning.MaxStepsPerCompany; i++)
            _driver.Capture(StepEnd.AtOnce, new FakePayload("s" + i), "charge");
        _orders.Selected.Add(2);

        Assert.AreEqual(CaptureResult.Full, _driver.Capture(StepEnd.Arrival, new FakePayload("late"), "move"));
        Assert.AreEqual(0, _driver.Queue.CountOf(2));
    }

    [TestMethod]
    public void Capture_ACompanyWithNothingQueued_MarksTheStepForTheWaitTest()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1);

        var steps = _driver.Queue.StepsOf(1);

        Assert.IsTrue(steps[0].NeedsWaitTest);
        Assert.IsFalse(steps[1].NeedsWaitTest, "a company with a queued step is not walking on a direct order");
    }

    // ---- starting and finishing ----

    [TestMethod]
    public void Pass_AReadyStep_IssuesItsOrderToItsCompanies_AndStartsIt()
    {
        var payload = Queue(StepEnd.Arrival, 1, 2);

        Pass(0);

        Assert.AreEqual(1, _orders.Issued.Count);
        CollectionAssert.AreEqual(new[] { 1, 2 }, _orders.Issued[0].Companies);
        Assert.AreSame(payload, _orders.Issued[0].Payload);
        Assert.IsTrue(_driver.Queue.StepsOf(1)[0].Started);
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.StartedId));
    }

    [TestMethod]
    public void Pass_AnArrivalStep_HoldsTheNextUntilTheCompanyHasArrived_ThenStartsItInTheSamePass()
    {
        Queue(StepEnd.Arrival, 1);
        var second = Queue(StepEnd.Arrival, 1);
        Pass(0);
        Assert.AreEqual(1, _orders.Issued.Count, "the second waits for the first");

        _orders.Arrivals.Add(1);
        Pass(1);

        Assert.AreEqual(2, _orders.Issued.Count);
        Assert.AreSame(second, _orders.Issued[1].Payload);
        // The fake keeps company 1 arrived, so the second step also ends in this pass; a real Move resets the order point.
        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length, "two steps, two lines: the log keeps one line per key, and the key names the step");
        Assert.IsTrue(finished.All(d => d.Contains("reason=arrival")));
    }

    [TestMethod]
    public void Pass_AStepQueuedBehindAnother_WritesBehindOne_AndTheFirstOneWritesBehindZero()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1);
        Pass(0);
        _orders.Arrivals.Add(1);

        Pass(1);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length);
        StringAssert.EndsWith(finished[0], "company=1 behind=0");
        StringAssert.EndsWith(finished[1], "company=1 behind=1");
    }

    [TestMethod]
    public void Pass_TwoMovesForTwoCompanies_EachHeadsItsOwnList_SoNeitherWritesBehindOne()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 2);
        Pass(0);
        _orders.Arrivals.Add(1);
        _orders.Arrivals.Add(2);

        Pass(1);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length);
        Assert.IsTrue(finished.All(d => d.EndsWith(" behind=0")), string.Join("; ", finished));
    }

    [TestMethod]
    public void Pass_AStepSharedByACompanyThatWaitedAndOneThatDidNot_WritesBehindOne_BecauseOneMemberWaitedForAWalk()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1, 2);
        Pass(0);
        _orders.Arrivals.Add(1);
        _orders.Arrivals.Add(2);

        Pass(1);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length, "one line per step: the key names the step, not the company");
        StringAssert.EndsWith(finished[0], " behind=0");
        StringAssert.EndsWith(finished[1], " behind=1");
    }

    [TestMethod]
    public void Pass_AMoveSharedByTwoCompaniesWhereOnlyOneArrivedAtTheMoveAhead_WritesBehindZero_BecauseTheOtherWalkWasNotWaitedFor()
    {
        Queue(StepEnd.Arrival, 1, 2);
        Queue(StepEnd.Arrival, 1, 2);
        Pass(0);
        _orders.Arrivals.Add(1);
        Pass(1);
        _driver.OnDirectOrder(new[] { 1 });
        _orders.Arrivals.Remove(1);
        Pass(MarshalTuning.MinPatience + 2);
        _orders.Arrivals.Add(2);

        Pass(MarshalTuning.MinPatience + 3);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length, "company 1 at the Move ahead, company 2 at the shared Move");
        StringAssert.EndsWith(finished[1], " behind=0");
    }

    [TestMethod]
    public void Pass_AMoveSharedByTwoCompaniesThatBothArrivedAtTheMoveAhead_WritesBehindOne()
    {
        Queue(StepEnd.Arrival, 1, 2);
        Queue(StepEnd.Arrival, 1, 2);
        Pass(0);
        _orders.Arrivals.Add(1);
        _orders.Arrivals.Add(2);

        Pass(1);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length);
        StringAssert.EndsWith(finished[0], " behind=0");
        StringAssert.EndsWith(finished[1], " behind=1");
    }

    [TestMethod]
    public void Pass_TheImplicitWaitAheadOfAStep_WritesBehindZero_AndSoDoesTheMoveAfterIt()
    {
        _orders.Walking.Add(1);
        Queue(StepEnd.Arrival, 1);
        Pass(0);
        _orders.Walking.Clear();
        _orders.Arrivals.Add(1);

        Pass(1);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        StringAssert.EndsWith(finished.First(d => d.Contains("end=ImplicitArrival")), " behind=0");
        StringAssert.EndsWith(finished.Single(d => d.Contains("end=Arrival")), " behind=0");
    }

    [TestMethod]
    public void Pass_AMoveBehindAnAtOnceStep_WritesBehindZero_BecauseNoWalkWasWaitedFor()
    {
        Queue(StepEnd.AtOnce, 1);
        Queue(StepEnd.Arrival, 1);
        _orders.Arrivals.Add(1);

        Pass(0);

        StringAssert.EndsWith(Reasons(MarshalChecks.FinishedId, "pass").Single(d => d.Contains("reason=arrival")), " behind=0");
    }

    [TestMethod]
    public void Pass_AMoveBehindAStepDroppedAtIssue_WritesBehindZero()
    {
        var first = Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1);
        _orders.OnIssue = payload => _orders.IssueResult = payload != first;
        _orders.Arrivals.Add(1);

        Pass(0);

        StringAssert.Contains(Reasons(MarshalChecks.DroppedId, "skip").Single(), "reason=none-selected");
        StringAssert.EndsWith(Reasons(MarshalChecks.FinishedId, "pass").Single(), " behind=0");
    }

    [TestMethod]
    public void Pass_AMoveBehindAStepThatRanOutOfPatience_WritesBehindZero_AndSoDoesTheStepThatTimedOut()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1);
        Pass(0);
        Pass(MarshalTuning.MinPatience + 1);
        _orders.Arrivals.Add(1);

        Pass(MarshalTuning.MinPatience + 2);

        StringAssert.EndsWith(Reasons(MarshalChecks.FinishedId, "skip").Single(), " behind=0");
        StringAssert.EndsWith(Reasons(MarshalChecks.FinishedId, "pass").Single(), " behind=0");
    }

    [TestMethod]
    public void Pass_AStepTheTestStartedByHandAheadOfTheStepBeforeIt_WritesBehindZero()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1);
        Pass(0);
        _driver.Queue.Start(_driver.Queue.StepsOf(1)[1], 0.5, MarshalTuning.MinPatience);
        _orders.Arrivals.Add(1);

        Pass(1);

        var finished = Reasons(MarshalChecks.FinishedId, "pass");
        Assert.AreEqual(2, finished.Length);
        Assert.IsTrue(finished.All(d => d.EndsWith(" behind=0")), string.Join("; ", finished));
    }

    [TestMethod]
    public void Pass_AtOnceSteps_AllRunInOnePass_InOrder()
    {
        var a = Queue(StepEnd.AtOnce, 1);
        var b = Queue(StepEnd.AtOnce, 1);
        var c = Queue(StepEnd.AtOnce, 1);

        Pass(0);

        CollectionAssert.AreEqual(new object[] { a, b, c }, _orders.Issued.Select(i => i.Payload).ToArray());
        Assert.AreEqual(0, _driver.Queue.TotalSteps);
    }

    [TestMethod]
    public void Pass_BeforeASlowPassHasElapsed_DoesNothing()
    {
        Queue(StepEnd.Arrival, 1);
        Pass(0);
        _orders.Arrivals.Add(1);
        Queue(StepEnd.Arrival, 1);

        Pass(0.1);

        Assert.AreEqual(1, _orders.Issued.Count);
        Pass(0.26);
        Assert.AreEqual(2, _orders.Issued.Count);
    }

    [TestMethod]
    public void Pass_AnEmptyQueue_StillSpendsTheSlowPass_SoAStepCapturedJustAfterWaitsForTheNextOne()
    {
        Pass(0);
        Queue(StepEnd.Arrival, 1);

        Pass(0.1);
        Assert.AreEqual(0, _orders.Issued.Count, "the pass at 0 ran, so the next is a quarter second later");
        Pass(0.26);

        Assert.AreEqual(1, _orders.Issued.Count);
    }

    [TestMethod]
    public void Pass_ReturnsAtOnceOnAnEmptyQueue_AfterItHasWrittenItsTimer()
    {
        // An empty queue has nothing to drop, test, start or finish, and the early return must not move the timing.
        var pass = System.Text.RegularExpressions.Regex.Replace(
            TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/MarshalsWord/MarshalQueueDriver.cs", stripComments: true), @"\s+", " ");
        var start = pass.IndexOf("internal void Pass()", System.StringComparison.Ordinal);

        StringAssert.Contains(pass.Substring(start), "_nextPass = now + MarshalTuning.SlowPassSeconds; if (Queue.IsEmpty) return; DropOut();");
    }

    [TestMethod]
    public void Pass_ASharedStep_StartsOnlyWhenEveryMemberIsFreeOfEarlierSteps()
    {
        Queue(StepEnd.Arrival, 1);
        var shared = Queue(StepEnd.Arrival, 1, 2);
        Pass(0);
        Assert.AreEqual(1, _orders.Issued.Count, "company 2 is free but company 1 still walks its first step");

        _orders.Arrivals.Add(1);
        Pass(1);

        Assert.AreSame(shared, _orders.Issued[1].Payload);
    }

    [TestMethod]
    public void Pass_ASharedStep_OneMemberArrivingDoesNotFinishTheOther()
    {
        Queue(StepEnd.Arrival, 1, 2);
        Pass(0);
        _orders.Arrivals.Add(1);

        Pass(1);

        CollectionAssert.AreEqual(new[] { 2 }, _driver.Queue.StepsOf(2)[0].Companies.ToArray());
        Assert.AreEqual(0, _driver.Queue.CountOf(1));
    }

    [TestMethod]
    public void Pass_ACompanyThatLostControl_IsDroppedFromEveryStep()
    {
        Queue(StepEnd.Arrival, 1, 2);
        Queue(StepEnd.Arrival, 1);
        _orders.Lost.Add(1);

        Pass(0);

        Assert.AreEqual(0, _driver.Queue.CountOf(1));
        Assert.AreEqual(1, _orders.Issued.Count, "the shared step still starts for company 2");
        CollectionAssert.AreEqual(new[] { 2 }, _orders.Issued[0].Companies);
        StringAssert.Contains(Reasons(MarshalChecks.DroppedId, "skip").Single(), "reason=lost-control");
    }

    [TestMethod]
    public void Pass_AMemberLostBetweenTwoStepsOfOnePass_IsLeftOutOfTheLaterStep()
    {
        // The hero can fall after the pass began. A step that started now would take company 2 back off AI control for nobody (rule 2).
        var first = Queue(StepEnd.AtOnce, 1);
        var second = Queue(StepEnd.Arrival, 1, 2);
        _orders.OnIssue = payload =>
        {
            if (payload == first)
                _orders.Lost.Add(2);
        };

        Pass(0);

        Assert.AreEqual(2, _orders.Issued.Count);
        Assert.AreSame(second, _orders.Issued[1].Payload);
        CollectionAssert.AreEqual(new[] { 1 }, _orders.Issued[1].Companies);
        Assert.AreEqual(0, _driver.Queue.CountOf(2));
    }

    [TestMethod]
    public void Pass_AStepWhoseEveryMemberIsLostBetweenTwoStepsOfOnePass_IssuesNothing()
    {
        // Both steps are ready when the pass starts. The first order takes company 2's control away, so the second step has no member left
        // when its turn comes, and a step with nobody to order must not be issued (rule 2).
        var first = Queue(StepEnd.AtOnce, 1);
        var second = Queue(StepEnd.Arrival, 2);
        _orders.OnIssue = payload =>
        {
            if (payload == first)
                _orders.Lost.Add(2);
        };

        Pass(0);

        Assert.AreEqual(1, _orders.Issued.Count);
        Assert.AreSame(first, _orders.Issued[0].Payload);
        Assert.AreEqual(0, _driver.Queue.CountOf(2));
        Assert.IsFalse(_orders.Issued.Any(i => i.Payload == second));
        StringAssert.Contains(Reasons(MarshalChecks.DroppedId, "skip").Single(), "company=2");
    }

    [TestMethod]
    public void Pass_AStepWhoseOnlyMemberLostControl_IssuesNothing()
    {
        Queue(StepEnd.Arrival, 1);
        _orders.Lost.Add(1);

        Pass(0);

        Assert.AreEqual(0, _orders.Issued.Count);
        Assert.AreEqual(0, _driver.Queue.TotalSteps);
    }

    [TestMethod]
    public void Pass_AStepWhoseTargetIsSpent_IsDroppedWithNoOrder_AndTheNextStarts()
    {
        var spent = Queue(StepEnd.TargetGone, 1);
        var next = Queue(StepEnd.Arrival, 1);
        _orders.Spent.Add(spent);

        Pass(0);

        Assert.AreEqual(1, _orders.Issued.Count);
        Assert.AreSame(next, _orders.Issued[0].Payload);
        StringAssert.Contains(Reasons(MarshalChecks.DroppedId, "skip").Single(), "reason=spent");
    }

    [TestMethod]
    public void Pass_WhenNoMemberCanBeSelected_TheStepIsDroppedWithNoOrder()
    {
        Queue(StepEnd.Arrival, 1);
        _orders.IssueResult = false;

        Pass(0);

        Assert.AreEqual(0, _driver.Queue.TotalSteps);
        StringAssert.Contains(Reasons(MarshalChecks.DroppedId, "skip").Single(), "reason=none-selected");
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.StartedId));
    }

    [TestMethod]
    public void Pass_ATargetGoneStep_EndsWhenItsTargetHasNoUnits_AndNeverByPatience()
    {
        var charge = Queue(StepEnd.TargetGone, 1);
        Queue(StepEnd.Arrival, 1);
        Pass(0);
        Pass(1000);
        Assert.AreEqual(1, _orders.Issued.Count, "a charge has no patience: the player asked for it until the target is gone");

        _orders.Spent.Add(charge);
        Pass(1001);

        Assert.AreEqual(2, _orders.Issued.Count);
        StringAssert.Contains(Reasons(MarshalChecks.FinishedId, "pass").Single(), "reason=target-gone");
    }

    [TestMethod]
    public void Pass_AnEntityStep_EndsWhenTheCompanysOrderIsNoLongerAnAttack()
    {
        Queue(StepEnd.EntityOrderEnded, 1);
        var next = Queue(StepEnd.Arrival, 1);
        Pass(0);
        Assert.AreEqual(1, _orders.Issued.Count);

        _orders.Ended.Add(1);
        Pass(1);

        Assert.AreSame(next, _orders.Issued[1].Payload);
        StringAssert.Contains(Reasons(MarshalChecks.FinishedId, "pass").Single(), "reason=entity-ended");
    }

    // ---- the implicit wait ----

    [TestMethod]
    public void Pass_ANewStepBehindAWalkUnderWay_WaitsForTheArrivalFirst()
    {
        var queued = Queue(StepEnd.Arrival, 1);
        _orders.Walking.Add(1);

        Pass(0);

        Assert.AreEqual(0, _orders.Issued.Count, "the queued move would cancel the walk the player just ordered");
        Assert.AreEqual(StepEnd.ImplicitArrival, _driver.Queue.StepsOf(1)[0].End);

        _orders.Arrivals.Add(1);
        _orders.Walking.Clear();
        Pass(1);

        Assert.AreSame(queued, _orders.Issued.Single().Payload);
    }

    [TestMethod]
    public void Pass_AGroupStepWithOneMemberWalkingAndOneIdle_WaitsOnlyBehindTheWalkingOne()
    {
        var shared = Queue(StepEnd.Arrival, 1, 2);
        _orders.Walking.Add(1);

        Pass(0);

        Assert.AreEqual(StepEnd.ImplicitArrival, _driver.Queue.StepsOf(1)[0].End);
        Assert.AreEqual(2, _driver.Queue.CountOf(1), "the implicit wait and the shared step");
        Assert.AreEqual(1, _driver.Queue.CountOf(2), "the idle company gets no wait of its own");
        Assert.AreEqual(0, _orders.Issued.Count, "the shared step waits for the company that is still walking");

        _orders.Arrivals.Add(1);
        _orders.Walking.Clear();
        Pass(1);

        Assert.AreSame(shared, _orders.Issued.Single().Payload);
        CollectionAssert.AreEqual(new[] { 1, 2 }, _orders.Issued.Single().Companies);
    }

    [TestMethod]
    public void Pass_AGroupStepThatHeadsOnlyOneMembersList_GivesTheOtherMemberNoWait()
    {
        // Company 2 already has a step of its own ahead of the shared one, so the shared step does not head its list.
        Queue(StepEnd.Arrival, 2);
        _driver.Queue.StepsOf(2)[0].NeedsWaitTest = false;
        Queue(StepEnd.Arrival, 1, 2);
        _orders.Walking.Add(1);
        _orders.Walking.Add(2);

        Pass(0);

        Assert.AreEqual(2, _driver.Queue.CountOf(1), "company 1 waits behind its walk");
        Assert.AreEqual(2, _driver.Queue.CountOf(2), "company 2 keeps its own step and the shared one: its walk is not the shared step's business");
    }

    [TestMethod]
    public void Pass_ANewStepWhenTheCompanyIsNotWalking_StartsAtOnce()
    {
        Queue(StepEnd.Arrival, 1);

        Pass(0);

        Assert.AreEqual(1, _orders.Issued.Count);
    }

    [TestMethod]
    public void Pass_TheWaitTest_InsertsOneImplicitStep_NotOnePerPass()
    {
        Queue(StepEnd.Arrival, 1);
        _orders.Walking.Add(1);

        Pass(0);
        Pass(1);
        Pass(2);

        Assert.AreEqual(2, _driver.Queue.CountOf(1), "the implicit step and the queued one");
    }

    // ---- patience ----

    [TestMethod]
    public void Pass_AStepThatNeverArrives_EndsByPatience_AndTheNextStarts()
    {
        Queue(StepEnd.Arrival, 1);
        var next = Queue(StepEnd.Arrival, 1);
        Pass(0);

        Pass(MarshalTuning.MinPatience - 1);
        Assert.AreEqual(1, _orders.Issued.Count);
        Pass(MarshalTuning.MinPatience + 1);

        Assert.AreSame(next, _orders.Issued[1].Payload);
        StringAssert.Contains(Reasons(MarshalChecks.FinishedId, "skip").Single(), "reason=patience");
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.FinishedId), "patience is not an arrival");
    }

    [TestMethod]
    public void Pass_AnEntityStepThatNeverEnds_EndsByPatience_AndTheNextStarts()
    {
        Queue(StepEnd.EntityOrderEnded, 1);
        var next = Queue(StepEnd.Arrival, 1);
        Pass(0);

        Pass(MarshalTuning.MinPatience + 1);

        Assert.AreSame(next, _orders.Issued[1].Payload);
        var line = Reasons(MarshalChecks.FinishedId, "skip").Single();
        StringAssert.Contains(line, "reason=patience");
        StringAssert.Contains(line, "end=EntityOrderEnded");
    }

    [TestMethod]
    public void Pass_AnImplicitWaitThatNeverArrives_EndsByPatience_AndTheQueuedStepStarts()
    {
        var queued = Queue(StepEnd.Arrival, 1);
        _orders.Walking.Add(1);
        Pass(0);
        Assert.AreEqual(0, _orders.Issued.Count, "the queued step waits behind the walk");

        Pass(MarshalTuning.MinPatience + 1);

        var line = Reasons(MarshalChecks.FinishedId, "skip").Single();
        StringAssert.Contains(line, "reason=patience");
        StringAssert.Contains(line, "end=ImplicitArrival");
        Assert.AreSame(queued, _orders.Issued.Single().Payload, "a company stuck on an implicit wait must not hold its queue for the battle");
    }

    [TestMethod]
    public void Pass_APatienceTakenFromTheLongestWalkOfTheGroup()
    {
        // 800 m at 4 m/s is 200 s, times 2.5 = 500 s; the nearer company would give only the minimum.
        _orders.Distances[1] = 20;
        _orders.Distances[2] = 800;
        Queue(StepEnd.Arrival, 1, 2);
        Pass(0);

        Pass(400);

        Assert.AreEqual(2, _driver.Queue.StepsOf(1).Concat(_driver.Queue.StepsOf(2)).Distinct().Sum(s => s.Companies.Count),
            "both members still hold the step at 400 s");
        Pass(501);
        Assert.AreEqual(0, _driver.Queue.TotalSteps);
    }

    [TestMethod]
    public void Pass_PatienceEndsOnMoreThanOneStepInFour_FailsTheCheckFromEightFinishedSteps()
    {
        for (var i = 0; i < 8; i++)
        {
            Queue(StepEnd.Arrival, 1);
            Pass(i * 100.0);
            if (i < 3)
                Pass(i * 100.0 + 50.0);
            else
                _orders.Arrivals.Add(1);
            Pass(i * 100.0 + 90.0);
            _orders.Arrivals.Clear();
        }

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.PatienceId));
        StringAssert.Contains(Reasons(MarshalChecks.PatienceId, "fail").Single(), "by-patience=3");
    }

    // An at-once step finishes the moment it starts and a target-gone step waits for a kill: neither can end by patience, so neither counts toward the share.
    private double FinishInstantStep(StepEnd end, double at)
    {
        var payload = Queue(end, 1);
        Pass(at);
        if (end == StepEnd.TargetGone)
        {
            _orders.Spent.Add(payload);
            at += 1;
            Pass(at);
        }

        return at;
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Pass_InstantSteps_DoNotDiluteThePatienceShare(bool targetGone)
    {
        var instant = targetGone ? StepEnd.TargetGone : StepEnd.AtOnce;
        // The report's log: one arrival, two steps that ended by patience and five instant steps. Counting the instant ones reached eight
        // finished steps and wrote a patience pass line, although two of the three walks had timed out.
        Queue(StepEnd.Arrival, 1);
        _orders.Arrivals.Add(1);
        Pass(0);
        _orders.Arrivals.Clear();
        for (var i = 1; i <= 2; i++)
        {
            Queue(StepEnd.Arrival, 1);
            Pass(i * 100.0);
            Pass(i * 100.0 + 50.0);
        }

        var at = 300.0;
        for (var i = 0; i < 5; i++)
            at = FinishInstantStep(instant, at + 10);

        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.PatienceId), "three walks have finished; the judge waits for eight");
        Assert.AreEqual(0, _log.Count("fail", MarshalChecks.PatienceId));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Pass_InstantStepsBeforeAFailingShare_DoNotHideTheFailure(bool targetGone)
    {
        var instant = targetGone ? StepEnd.TargetGone : StepEnd.AtOnce;
        var at = 0.0;
        for (var i = 0; i < 10; i++)
            at = FinishInstantStep(instant, at + 10);
        for (var i = 0; i < 8; i++)
        {
            at += 100;
            Queue(StepEnd.Arrival, 1);
            Pass(at);
            if (i < 3)
            {
                at += 50;
                Pass(at);
            }
            else
            {
                _orders.Arrivals.Add(1);
                at += 1;
                Pass(at);
            }

            _orders.Arrivals.Clear();
        }

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.PatienceId));
        StringAssert.Contains(Reasons(MarshalChecks.PatienceId, "fail").Single(), "finished=8 by-patience=3");
    }

    [TestMethod]
    public void Pass_EightArrivals_PassTheCheck_AndNothingFails()
    {
        for (var i = 0; i < 8; i++)
        {
            Queue(StepEnd.Arrival, 1);
            _orders.Arrivals.Add(1);
            Pass(i * 10.0);
            _orders.Arrivals.Clear();
        }

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.PatienceId));
        Assert.AreEqual(0, _log.Count("fail", MarshalChecks.PatienceId));
    }

    [TestMethod]
    public void Pass_PatienceBecomingTooOftenAfterTheEighthStep_StillFails_WhenTheEighthWroteAPass()
    {
        for (var i = 0; i < 8; i++)
        {
            Queue(StepEnd.Arrival, 1);
            _orders.Arrivals.Add(1);
            Pass(i * 10.0);
            _orders.Arrivals.Clear();
        }

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.PatienceId));
        var at = 1000.0;
        for (var i = 0; i < 3; i++)
        {
            Queue(StepEnd.Arrival, 1);
            Pass(at);
            Pass(at + 100.0);
            at += 200.0;
        }

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.PatienceId), "3 of 11 finished steps ended by patience, over one in four");
        StringAssert.Contains(Reasons(MarshalChecks.PatienceId, "fail").Single(), "finished=11 by-patience=3");
    }

    [TestMethod]
    public void Pass_FewerThanEightFinishedSteps_JudgeNothing()
    {
        for (var i = 0; i < 7; i++)
        {
            Queue(StepEnd.Arrival, 1);
            Pass(i * 100.0);
            Pass(i * 100.0 + 90.0);
        }

        Assert.AreEqual(0, _log.Count("fail", MarshalChecks.PatienceId));
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.PatienceId));
    }

    // ---- clearing ----

    [TestMethod]
    public void OnDirectOrder_ClearsThatCompanysQueue_AndLogsIt()
    {
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 1);
        Queue(StepEnd.Arrival, 2);

        _driver.OnDirectOrder(new[] { 1 });

        Assert.AreEqual(0, _driver.Queue.CountOf(1));
        Assert.AreEqual(1, _driver.Queue.CountOf(2));
        StringAssert.Contains(Reasons(MarshalChecks.ClearedId, "pass").Single(), "steps=2");
    }

    [TestMethod]
    public void OnDirectOrder_ACompanyWithNothingQueued_WritesNoLine()
    {
        _driver.OnDirectOrder(new[] { 5 });

        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_ClearsEveryQueue()
    {
        Queue(StepEnd.Arrival, 1, 2);
        Queue(StepEnd.Arrival, 3);

        _driver.OnPlayerTeamChanged();

        Assert.AreEqual(0, _driver.Queue.TotalSteps);
        StringAssert.Contains(Reasons(MarshalChecks.ClearedId, "skip").Single(), "reason=team-changed");
    }
}
