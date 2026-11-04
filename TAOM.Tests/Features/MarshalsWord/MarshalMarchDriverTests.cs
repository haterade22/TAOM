using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The march driver (build-spec 6.4 and 6.5): adopting a request, publishing paces, and the one verdict line when it ends.</summary>
[TestClass]
public class MarshalMarchDriverTests
{
    private FakeMarshalOrders _orders = null!;
    private RecordingOutcomeLog _log = null!;
    private MarshalMarchDriver _driver = null!;

    private static readonly MarchTarget[] Pair = { new(1, 10f, 10f), new(2, 30f, 10f) };

    [TestInitialize]
    public void Setup()
    {
        _orders = new FakeMarshalOrders();
        _log = new RecordingOutcomeLog();
        _driver = new MarshalMarchDriver(_orders, _log, "7");
    }

    private static MarchLeg Leg(int company, float remaining, float shift = 0f) => new(company, remaining, 4f, shift);

    private void Pass(double now, bool allowed = true)
    {
        _orders.Now = now;
        _driver.Pass(allowed);
    }

    [TestMethod]
    public void Pass_WithNoRequest_DoesNothing()
    {
        Pass(0);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Count);
        Assert.AreEqual(0, _log.Lines.Count);
    }

    [TestMethod]
    public void Request_OfOneCompany_StartsNoMarch()
    {
        _driver.Request(new[] { new MarchTarget(1, 0f, 0f) });
        _orders.Legs = new[] { Leg(1, 100) };

        Pass(0);

        Assert.IsFalse(_driver.IsMarching);
    }

    [TestMethod]
    public void Pass_ARequestOfTwoCompanies_StartsAMarch_AndPublishesTheirPaces()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 50) };

        Pass(0);

        Assert.IsTrue(_driver.IsMarching);
        var paces = _orders.Published.Last();
        Assert.AreEqual(1f, paces.Single(p => p.Company == 1).Pace, 1e-4f);
        Assert.AreEqual(0.5f, paces.Single(p => p.Company == 2).Pace, 1e-4f);
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.MarchId));
        CollectionAssert.AreEqual(Pair, _orders.TargetsRead.Last().ToArray(), "the adapter reads the legs of the targets the commit gave");
    }

    [TestMethod]
    public void Pass_ARequestMadeWithTheSettingOff_IsDropped_AndNeverStartsLaterWhenTheSettingComesBack()
    {
        // The array commit asks for a march whatever the setting says; the driver's pass is the one place that decides (build-spec 6.4).
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 50) };

        Pass(0, allowed: false);
        Pass(1, allowed: true);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.MarchId));
        Assert.AreEqual(0, _orders.TargetsRead.Count, "no leg is read for a request that was dropped");
    }

    private static readonly MarchTarget[] Trio = { new(1, 0f, 0f), new(2, 0f, 0f), new(3, 0f, 0f) };

    private void SetLegs(params (int Company, float Remaining)[] legs) =>
        _orders.Legs = legs.Select(l => new MarchLeg(l.Company, l.Remaining, 3f, 0f)).ToArray();

    private void AssertTheMarchEndedWithOnlyACompanyLeftSkip()
    {
        Assert.AreEqual(0, _log.Count("fail", MarshalChecks.SpreadId), "a march that lost a member proves nothing");
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.SpreadId));
        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpreadId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=company-left");
        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count);
    }

    [TestMethod]
    public void Pass_ACompanyWipedOutMidMarch_WritesACompanyLeftSkip_WhenTheSurvivorsArriveTogether()
    {
        _driver.Request(Trio);
        SetLegs((1, 90f), (2, 30f), (3, 27f));
        Pass(0);
        SetLegs((1, 75f), (2, 25f), (3, 22.5f));
        Pass(5);
        SetLegs((2, 20f), (3, 18f));
        Pass(10);
        SetLegs((2, 3f), (3, 2.4f));

        Pass(16.5);

        AssertTheMarchEndedWithOnlyACompanyLeftSkip();
    }

    [TestMethod]
    public void Pass_ACompanyWipedOutMidMarch_WritesACompanyLeftSkip_WhenThePaceWasDead()
    {
        // 90, 30 and 27 m at 3 m/s with no pace: the survivors arrive a second apart, and the far company that is gone made the walks look provable.
        _driver.Request(Trio);
        SetLegs((1, 90f), (2, 30f), (3, 27f));
        Pass(0);
        SetLegs((2, 27f), (3, 24f));
        Pass(1);
        SetLegs((2, 1f), (3, 0f));

        Pass(10);

        AssertTheMarchEndedWithOnlyACompanyLeftSkip();
    }

    private void WalkTheMarch(float[] metres, float[] readSpeeds, float[] realSpeeds, bool withPace)
    {
        _driver.Request(metres.Select((_, i) => new MarchTarget(i + 1, 0f, 0f)).ToArray());
        var remaining = (float[])metres.Clone();
        var pace = Enumerable.Repeat(1f, metres.Length).ToArray();
        for (var step = 0; step < 4000; step++)
        {
            _orders.Legs = remaining.Select((r, i) => new MarchLeg(i + 1, r, readSpeeds[i], 0f)).ToArray();
            Pass(step * 0.25);
            if (!_driver.IsMarching)
                return;
            if (withPace)
                foreach (var p in _orders.Published.Last())
                    pace[p.Company - 1] = p.Pace;
            for (var i = 0; i < remaining.Length; i++)
                remaining[i] = System.Math.Max(0f, remaining[i] - realSpeeds[i] * pace[i] * 0.25f);
        }

        Assert.Fail("the simulated march never ended");
    }

    private void AssertTheMarchWroteOnlyANoTestSkip()
    {
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.SpreadId));
        Assert.AreEqual(0, _log.Count("fail", MarshalChecks.SpreadId));
        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpreadId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=no-test");
    }

    [TestMethod]
    public void Pass_ACompanyAboveItsReadSpeedWithNoPace_WritesNoTest_NotAPass()
    {
        // 123 m and 81 m read at 3 m/s while the far company really runs at 3.3 m/s: the spread rule passes with no pace acting.
        WalkTheMarch(new[] { 123f, 81f }, new[] { 3f, 3f }, new[] { 3.3f, 3f }, withPace: false);

        AssertTheMarchWroteOnlyANoTestSkip();
    }

    [TestMethod]
    public void Pass_AVeryShortSlowWalkWithItsPaceApplied_WritesNoTest_NotAFail()
    {
        // 73 m at 7 m/s and 6 m at 1 m/s: the pace equalizes the time to the order point, and the check measures the time to the slack.
        WalkTheMarch(new[] { 73f, 6f }, new[] { 7f, 1f }, new[] { 7f, 1f }, withPace: true);

        AssertTheMarchWroteOnlyANoTestSkip();
    }

    [TestMethod]
    public void Pass_ACompanyThatLeftAndCameBack_WritesACompanyLeftSkip_NotTheVerdictOfItsFinalCount()
    {
        _driver.Request(Trio);
        SetLegs((1, 90f), (2, 30f), (3, 27f));
        Pass(0);
        SetLegs((2, 27f), (3, 24f));
        Pass(1);
        SetLegs((1, 87f), (2, 24f), (3, 21f));
        Pass(2);
        SetLegs((1, 2f), (2, 2f), (3, 2f));

        Pass(10);

        AssertTheMarchEndedWithOnlyACompanyLeftSkip();
    }

    [TestMethod]
    public void Pass_OneCompanyTakingAnothersPlaceAtTheSameCount_WritesACompanyLeftSkip_NotAPass()
    {
        _driver.Request(Trio);
        SetLegs((1, 90f), (2, 30f));
        Pass(0);
        SetLegs((2, 30f), (3, 27f));
        Pass(1);
        SetLegs((2, 6f), (3, 3f));
        Pass(9);
        SetLegs((2, 3f), (3, 0f));

        Pass(10);

        AssertTheMarchEndedWithOnlyACompanyLeftSkip();
    }

    [TestMethod]
    public void Pass_WhenTheCompaniesArriveTogether_ClearsThePaces_AndPassesTheSpreadCheck()
    {
        // 103 m and 53 m at 4 m/s: 25 s and 12.5 s to the slack on the first pass, about two to one, so the check can prove the pace.
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 103), Leg(2, 53) };
        Pass(0);
        _orders.Legs = new[] { Leg(1, 2f), Leg(2, 2.5f) };

        Pass(25);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count, "an ended march publishes no pace");
        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.SpreadId));
        var detail = _log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail;
        StringAssert.Contains(detail, "longest=25.0");
        StringAssert.Contains(detail, "shortest=12.5");
    }

    [TestMethod]
    public void Pass_WhenOneCompanyArrivesFarAheadOfTheOther_FailsTheSpreadCheck()
    {
        // Company 2 is at its point at t=1 and company 1 is still ~40 s out when the march ends: the paces did not work.
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 103), Leg(2, 53) };
        Pass(0);
        _orders.Legs = new[] { Leg(1, 12f), Leg(2, 0f) };
        Pass(1);
        _orders.Legs = new[] { Leg(1, 5f), Leg(2, 0f) };
        Pass(40);

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.SpreadId));
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.SpreadId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "shortest=12.5");
    }

    [TestMethod]
    public void Pass_EqualWalks_WriteANoTestSkip_NotAPass_BecauseNoPaceActed()
    {
        // Two companies given one click walk the same distance, so a group with no pace arrives together too: a pass would prove nothing.
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 103), Leg(2, 103) };
        Pass(0);
        _orders.Legs = new[] { Leg(1, 2f), Leg(2, 2.5f) };

        Pass(25);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.SpreadId) + _log.Count("fail", MarshalChecks.SpreadId));
        var skip = _log.Lines.Single(l => l.Id == MarshalChecks.SpreadId);
        Assert.AreEqual("skip", skip.Result);
        StringAssert.Contains(skip.Detail, "reason=no-test");
        StringAssert.Contains(skip.Detail, "longest=25.0");
        StringAssert.Contains(skip.Detail, "shortest=25.0");
        Assert.AreEqual(0, _orders.Published.Last().Count, "the paces still clear");
    }

    [TestMethod]
    public void Pass_WalksAboutTwoToOneAtOneSpeed_AreProvable_SixtyAndThirtyMetresAtThreeMetresASecond()
    {
        var request = new[] { new MarchTarget(1, 0f, 0f), new MarchTarget(2, 0f, 0f) };
        _driver.Request(request);
        _orders.Legs = new[] { new MarchLeg(1, 60f, 3f, 0f), new MarchLeg(2, 30f, 3f, 0f) };
        Pass(0);
        _orders.Legs = new[] { new MarchLeg(1, 1f, 3f, 0f), new MarchLeg(2, 1f, 3f, 0f) };

        Pass(20);

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.SpreadId));
    }

    [TestMethod]
    public void Pass_WhenAMemberIsEmptied_EndsTheMarchWithAnEmptiedSkip_AndClearsThePaces()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 50) };
        Pass(0);
        Assert.IsTrue(_orders.Published.Last().Count == 2);
        _orders.Legs = new[] { Leg(1, 96) };

        Pass(1);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count);
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.SpreadId) + _log.Count("fail", MarshalChecks.SpreadId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=emptied");
    }

    [TestMethod]
    public void Pass_AStuckMarch_KeepsItsPacesUntilItsPatienceEnds_ThenSkipsWithTheReason()
    {
        // Nobody moves: 100 m and 50 m at 4 m/s is 25 s on the first pass, so the march gives up after 62.5 s.
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 50) };
        Pass(0);
        Pass(60);
        Assert.IsTrue(_driver.IsMarching, "still inside the patience");
        Assert.AreEqual(2, _orders.Published.Last().Count);

        Pass(65);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count, "the slowed company is released");
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=patience");
        Assert.AreEqual(0, _log.Count("fail", MarshalChecks.SpreadId), "a stuck march is not a verdict on the pace");
    }

    [TestMethod]
    public void Pass_WhenACompanyLeavesItsMove_EndsAsAReorderedSkip_ForTheHugeShiftTheAdapterReports()
    {
        // A company given Charge, Stop or Retreat moves no order point, and the adapter reports float.MaxValue for it (MarshalOrderAdapter.ReadMarch).
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 50) };
        Pass(0);
        _orders.Legs = new[] { Leg(1, 96), Leg(2, 46, shift: float.MaxValue) };

        Pass(1);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count);
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=reordered");
    }

    [TestMethod]
    public void Pass_WhenALaterOrderMovesACompany_EndsAsASkip_NotAVerdict()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };
        Pass(0);
        _orders.Legs = new[] { Leg(1, 90), Leg(2, 40, shift: 12f) };

        Pass(1);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _log.Count("pass", MarshalChecks.SpreadId) + _log.Count("fail", MarshalChecks.SpreadId));
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=reordered");
    }

    [TestMethod]
    public void Pass_WithTheSettingOff_StopsAMarchInProgress()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };
        Pass(0);

        Pass(1, allowed: false);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count);
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=setting-off");
    }

    [TestMethod]
    public void Pass_ANewRequestDuringAMarch_ReplacesIt_AndSkipsTheOld()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };
        Pass(0);

        _driver.Request(new[] { new MarchTarget(3, 0f, 0f), new MarchTarget(4, 0f, 0f) });
        _orders.Legs = new[] { Leg(3, 100), Leg(4, 60) };
        Pass(1);

        Assert.IsTrue(_driver.IsMarching);
        StringAssert.Contains(_log.Lines.Single(l => l.Id == MarshalChecks.SpreadId).Detail, "reason=replaced");
        CollectionAssert.AreEqual(new[] { 3, 4 }, _orders.TargetsRead.Last().Select(t => t.Company).ToArray(), "the new march reads its own targets, not the old ones");
    }

    [TestMethod]
    public void Pass_BeforeASlowPassHasElapsed_DoesNotReadTheLegsAgain()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };
        Pass(0);
        var published = _orders.Published.Count;

        Pass(0.1);

        Assert.AreEqual(published, _orders.Published.Count);
    }

    [TestMethod]
    public void OnPlayerTeamChanged_DuringAMarch_EndsItWithATeamChangedSkip_AndClearsThePaces()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };
        Pass(0);

        _driver.OnPlayerTeamChanged();

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count);
        var line = _log.Lines.Single(l => l.Id == MarshalChecks.SpreadId);
        Assert.AreEqual("skip", line.Result);
        StringAssert.Contains(line.Detail, "reason=team-changed");
    }

    [TestMethod]
    public void OnPlayerTeamChanged_WithNoMarch_DropsAPendingRequest_AndWritesNoLine()
    {
        _driver.Request(Pair);
        _orders.Legs = new[] { Leg(1, 100), Leg(2, 100) };

        _driver.OnPlayerTeamChanged();
        Pass(0);

        Assert.IsFalse(_driver.IsMarching);
        Assert.AreEqual(0, _orders.Published.Last().Count);
        Assert.AreEqual(0, _log.Lines.Count);
    }
}
