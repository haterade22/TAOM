using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The queue's own rules (build-spec 5.3): the barrier, the lists, drop-out, clearing and patience. Nothing here names an engine type.</summary>
[TestClass]
public class MarshalOrderQueueTests
{
    private readonly MarshalOrderQueue _queue = new();

    private static List<int> Ids(params int[] ids) => ids.ToList();

    private QueueStep Add(StepEnd end, params int[] companies) => _queue.Enqueue(Ids(companies), end, new object(), needsWaitTest: false);

    [TestMethod]
    public void Enqueue_OneCompany_AppearsInThatCompanysList()
    {
        var step = Add(StepEnd.Arrival, 3);

        CollectionAssert.AreEqual(new[] { step }, _queue.StepsOf(3).ToArray());
        Assert.AreEqual(1, _queue.CountOf(3));
        Assert.AreEqual(0, _queue.CountOf(4));
    }

    [TestMethod]
    public void Enqueue_SharedStep_AppearsInEveryMembersList()
    {
        var step = Add(StepEnd.Arrival, 1, 2);

        Assert.AreSame(step, _queue.StepsOf(1).Single());
        Assert.AreSame(step, _queue.StepsOf(2).Single());
    }

    [TestMethod]
    public void Enqueue_GivesEachStepADistinctIncreasingId()
    {
        var a = Add(StepEnd.AtOnce, 1);
        var b = Add(StepEnd.AtOnce, 1);

        Assert.IsTrue(b.Id > a.Id);
    }

    [TestMethod]
    public void Ready_FirstStepOfALoneCompany_IsReady_AndTheSecondIsNot()
    {
        var first = Add(StepEnd.Arrival, 1);
        Add(StepEnd.Arrival, 1);

        CollectionAssert.AreEqual(new[] { first }, _queue.Ready().ToArray());
    }

    [TestMethod]
    public void Ready_SharedStep_WaitsUntilItIsAtTheHeadOfEveryMember()
    {
        // Companies 1 and 2 share A and C; only company 1 has B between them, so 2 waits at C until B is done.
        var a = Add(StepEnd.Arrival, 1, 2);
        var b = Add(StepEnd.Arrival, 1);
        var c = Add(StepEnd.Arrival, 1, 2);

        _queue.Start(a, 0, 30);
        _queue.Finish(a, 1);
        _queue.Finish(a, 2);

        CollectionAssert.AreEqual(new[] { b }, _queue.Ready().ToArray());
        _queue.Start(b, 1, 30);
        _queue.Finish(b, 1);
        CollectionAssert.AreEqual(new[] { c }, _queue.Ready().ToArray());
    }

    [TestMethod]
    public void Ready_ASharedStepWithOneMemberStillBehindAnEarlierStep_IsNotReady()
    {
        Add(StepEnd.Arrival, 1);
        Add(StepEnd.Arrival, 1, 2);

        Assert.AreEqual(1, _queue.Ready().Count, "only company 1's first step; the shared one waits for company 1");
    }

    [TestMethod]
    public void Ready_ReturnsNoStartedStep()
    {
        var step = Add(StepEnd.Arrival, 1);
        _queue.Start(step, 2.0, 30);

        Assert.AreEqual(0, _queue.Ready().Count);
        Assert.IsTrue(step.Started);
        Assert.AreEqual(2.0, step.StartedAt);
        Assert.AreEqual(30.0, step.Patience);
    }

    [TestMethod]
    public void Finish_OneMemberOfAStartedStep_AdvancesOnlyThatMembersList()
    {
        var shared = Add(StepEnd.Arrival, 1, 2);
        var next = Add(StepEnd.Arrival, 1);
        _queue.Start(shared, 0, 30);

        _queue.Finish(shared, 1);

        CollectionAssert.AreEqual(new[] { next }, _queue.StepsOf(1).ToArray());
        Assert.AreSame(shared, _queue.StepsOf(2).Single(), "company 2 is still walking");
        CollectionAssert.AreEqual(new[] { 2 }, shared.Companies.ToArray());
    }

    [TestMethod]
    public void Finish_TheLastMember_RemovesTheStepFromTheQueue()
    {
        var step = Add(StepEnd.AtOnce, 1);
        _queue.Start(step, 0, 0);

        _queue.Finish(step, 1);

        Assert.AreEqual(0, _queue.TotalSteps);
        Assert.AreEqual(0, _queue.StepsOf(1).Count);
    }

    [TestMethod]
    public void RemoveCompany_LeavesEveryStepItWasIn_AndDropsTheStepsItEmptied()
    {
        var alone = Add(StepEnd.Arrival, 1);
        var shared = Add(StepEnd.Arrival, 1, 2);

        _queue.RemoveCompany(1);

        Assert.AreEqual(0, _queue.StepsOf(1).Count);
        CollectionAssert.AreEqual(new[] { shared }, _queue.StepsOf(2).ToArray());
        CollectionAssert.AreEqual(new[] { 2 }, shared.Companies.ToArray());
        Assert.AreEqual(0, alone.Companies.Count, "a step with no member left is removed");
    }

    [TestMethod]
    public void RemoveCompany_TheBlockerOfASharedStep_ReleasesTheOther()
    {
        Add(StepEnd.Arrival, 1);
        var shared = Add(StepEnd.Arrival, 1, 2);

        _queue.RemoveCompany(1);

        CollectionAssert.AreEqual(new[] { shared }, _queue.Ready().ToArray());
    }

    [TestMethod]
    public void RemoveCompany_RemovesThatCompanysSteps_AndReportsTheCount()
    {
        Add(StepEnd.Arrival, 1);
        Add(StepEnd.Arrival, 1);
        var other = Add(StepEnd.Arrival, 2);

        var cleared = _queue.RemoveCompany(1);

        Assert.AreEqual(2, cleared);
        Assert.AreEqual(0, _queue.CountOf(1));
        CollectionAssert.AreEqual(new[] { other }, _queue.StepsOf(2).ToArray());
    }

    [TestMethod]
    public void RemoveCompany_NothingQueued_ReportsZero_AndChangesNothing()
    {
        var version = _queue.Version;

        Assert.AreEqual(0, _queue.RemoveCompany(9));
        Assert.AreEqual(version, _queue.Version, "the driver's rounds stop when the version holds, so an unlisted company must not move it");
    }

    [TestMethod]
    public void ClearAll_EmptiesEveryList()
    {
        Add(StepEnd.Arrival, 1, 2);
        Add(StepEnd.Arrival, 3);

        _queue.ClearAll();

        Assert.AreEqual(0, _queue.TotalSteps);
        Assert.AreEqual(0, _queue.Companies.Count);
    }

    [TestMethod]
    public void IsFull_ACompanyAtMaxSteps_IsFull_OneBelowIsNot()
    {
        for (var i = 0; i < MarshalTuning.MaxStepsPerCompany - 1; i++)
            Add(StepEnd.AtOnce, 1);
        Assert.IsFalse(_queue.IsFull(Ids(1)));

        Add(StepEnd.AtOnce, 1);

        Assert.IsTrue(_queue.IsFull(Ids(1)));
        Assert.IsTrue(_queue.IsFull(Ids(2, 1)), "any member at the limit fills the group");
        Assert.IsFalse(_queue.IsFull(Ids(2)));
    }

    [TestMethod]
    public void EnqueueAhead_PutsAnImplicitStepBeforeTheHeadOfThatCompanyOnly()
    {
        var head = Add(StepEnd.Arrival, 1, 2);

        var implicitStep = _queue.EnqueueAhead(1);

        CollectionAssert.AreEqual(new[] { implicitStep, head }, _queue.StepsOf(1).ToArray());
        CollectionAssert.AreEqual(new[] { head }, _queue.StepsOf(2).ToArray());
        CollectionAssert.AreEqual(new[] { 1 }, implicitStep.Companies.ToArray());
        Assert.AreEqual(StepEnd.ImplicitArrival, implicitStep.End);
        Assert.IsNull(implicitStep.Payload);
    }

    [TestMethod]
    public void Version_RisesOnEachMethodThatChangesAList_AndOnNoRead()
    {
        var step = Add(StepEnd.Arrival, 1, 2);
        var toDrop = Add(StepEnd.Arrival, 4);
        var changing = new (string Name, System.Action Act)[]
        {
            ("Enqueue", () => Add(StepEnd.Arrival, 3)),
            ("EnqueueAhead", () => _queue.EnqueueAhead(3)),
            ("Start", () => _queue.Start(step, 0, 30)),
            ("Finish", () => _queue.Finish(step, 1)),
            ("RemoveCompany", () => _queue.RemoveCompany(2)),
            ("Drop", () => _queue.Drop(toDrop)),
            ("ClearAll", () => _queue.ClearAll()),
        };

        foreach (var (name, act) in changing)
        {
            var before = _queue.Version;
            act();
            Assert.IsTrue(_queue.Version > before, name + " must raise the version");
        }

        Add(StepEnd.Arrival, 5);
        var version = _queue.Version;
        _ = _queue.Ready();
        _ = _queue.Running();
        _ = _queue.WaitTestPending();
        _ = _queue.StepsOf(5);
        _ = _queue.Companies;
        Assert.AreEqual(version, _queue.Version, "reading the lists changes nothing");
    }

    [TestMethod]
    public void IsEmpty_IsTrueAtTheStart_FalseWhileAnyStepIsListed_AndTrueAgainAfterEachWayOutOfTheLastOne()
    {
        Assert.IsTrue(_queue.IsEmpty);

        var step = Add(StepEnd.Arrival, 1);
        Assert.IsFalse(_queue.IsEmpty);
        _queue.Start(step, 0, 30);
        _queue.Finish(step, 1);
        Assert.IsTrue(_queue.IsEmpty, "the last member finished");

        Add(StepEnd.Arrival, 1);
        _queue.RemoveCompany(1);
        Assert.IsTrue(_queue.IsEmpty, "the last company dropped out");

        var dropped = Add(StepEnd.Arrival, 1, 2);
        _queue.Drop(dropped);
        Assert.IsTrue(_queue.IsEmpty, "the step was dropped whole");

        Add(StepEnd.Arrival, 3);
        _queue.ClearAll();
        Assert.IsTrue(_queue.IsEmpty);
    }

    [TestMethod]
    public void IsEmpty_ACompanyStillHoldingAStep_IsNotEmpty()
    {
        var shared = Add(StepEnd.Arrival, 1, 2);
        _queue.RemoveCompany(1);

        Assert.IsFalse(_queue.IsEmpty, "company 2 still holds the step");
        _queue.RemoveCompany(2);
        Assert.IsTrue(_queue.IsEmpty);
        Assert.AreEqual(0, shared.Companies.Count);
    }

    [TestMethod]
    public void PatienceFor_ALongWalk_ScalesWithDistanceOverSpeed()
    {
        // 600 m at 4 m/s is 150 s of walk, and the allowance is 2.5 times that.
        Assert.AreEqual(375.0, MarshalOrderQueue.PatienceFor(600, 4), 1e-9);
    }

    [TestMethod]
    public void PatienceFor_ShortWalk_NeverFallsBelowTheMinimum()
    {
        Assert.AreEqual(MarshalTuning.MinPatience, MarshalOrderQueue.PatienceFor(10, 4), 1e-9);
    }

    [TestMethod]
    public void PatienceFor_ASpeedBelowOne_IsReadAsOne()
    {
        // Formation.CachedMovementSpeed has a floor of only 0.1; the allowance must not become 25 times larger.
        Assert.AreEqual(MarshalOrderQueue.PatienceFor(100, 1), MarshalOrderQueue.PatienceFor(100, 0.1), 1e-9);
    }

    [DataTestMethod]
    [DataRow(double.NaN, 4.0)]
    [DataRow(100.0, double.NaN)]
    [DataRow(double.PositiveInfinity, 4.0)]
    [DataRow(-5.0, 4.0)]
    public void PatienceFor_ANonFiniteOrNegativeInput_FallsBackToTheMinimum(double distance, double speed)
    {
        Assert.AreEqual(MarshalTuning.MinPatience, MarshalOrderQueue.PatienceFor(distance, speed), 1e-9);
    }
}
