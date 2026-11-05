using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The mark-effect proof and the outlines (build-spec 8.1 and 8.2) over fake adapters.</summary>
[TestClass]
public class MarshalMarkAndOutlineTests
{
    private FakeMarshalOrders _orders = null!;
    private RecordingOutcomeLog _log = null!;
    private ListLogger _logger = null!;
    private MarshalMarkWatch _watch = null!;

    [TestInitialize]
    public void Setup()
    {
        _orders = new FakeMarshalOrders();
        _log = new RecordingOutcomeLog();
        _logger = new ListLogger();
        _watch = new MarshalMarkWatch(_orders, _log, _logger, "7");
    }

    private void Watch(double now, params MarkSample[] samples)
    {
        _orders.Marks = samples;
        _orders.Now = now;
        _watch.Pass();
    }

    private static MarkSample Sample(int onTarget, int of, MarkContext context = MarkContext.RangedOnStop) => new(2, 6, onTarget, of, context);

    // ---- the mark-effect proof ----

    [TestMethod]
    public void Pass_AMarkedCompany_WritesTheContractsLine_OncePerSecond()
    {
        Watch(1.0, Sample(5, 8));
        Watch(1.4, Sample(5, 8));
        Watch(2.0, Sample(6, 8));

        CollectionAssert.AreEqual(new[] { "[Marshal] mark company=2 target=6 onTarget=5 of=8", "[Marshal] mark company=2 target=6 onTarget=6 of=8" },
            _logger.Lines.ToArray());
    }

    [TestMethod]
    public void Pass_BeforeTheMarkHasStoodThreeSeconds_JudgesNothing()
    {
        Watch(1.0, Sample(0, 10));
        Watch(3.9, Sample(0, 10));

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.MarkEffectId));
    }

    [TestMethod]
    public void Pass_HalfOrMoreOfTheSampledAimAtTheMarkedCompany_Passes_ForARangedCompanyOnStop()
    {
        Watch(1.0, Sample(4, 8));
        Watch(4.0, Sample(4, 8));

        var line = _log.Lines.Single(l => l.Id == MarshalChecks.MarkEffectId);
        Assert.AreEqual("pass", line.Result);
        StringAssert.Contains(line.Detail, "context=RangedOnStop onTarget=4 of=8");
    }

    [TestMethod]
    public void Pass_UnderHalfAimAtTheMarkedCompany_Fails()
    {
        Watch(1.0, Sample(3, 8));
        Watch(4.0, Sample(3, 8));

        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.MarkEffectId));
    }

    [TestMethod]
    public void Pass_AnyOtherCompanyWithATarget_IsLoggedBesideARangedOneOnStop_ButOnlyTheRangedOneIsJudged()
    {
        Watch(1.0, Sample(7, 8, MarkContext.Other), new MarkSample(3, 6, 4, 8, MarkContext.RangedOnStop));
        Watch(4.0, Sample(7, 8, MarkContext.Other), new MarkSample(3, 6, 4, 8, MarkContext.RangedOnStop));

        var keys = _log.Lines.Where(l => l.Id == MarshalChecks.MarkEffectId).Select(l => l.Key).ToArray();
        CollectionAssert.AreEqual(new[] { "7:3:RangedOnStop" }, keys);
        Assert.AreEqual(4, _logger.Lines.Count, "a mark line for each of the two companies on each of the two passes");
    }

    [TestMethod]
    public void Pass_AnyOtherContext_IsLoggedButNeverJudged()
    {
        Watch(1.0, Sample(0, 10, MarkContext.Other));
        Watch(4.0, Sample(0, 10, MarkContext.Other));

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.MarkEffectId));
        Assert.AreEqual(2, _logger.Lines.Count);
    }

    [TestMethod]
    public void Pass_FewerThanThreeSoldiersWithATarget_IsNotJudged()
    {
        Watch(1.0, Sample(0, 2));
        Watch(4.0, Sample(0, 2));

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.MarkEffectId));
    }

    [TestMethod]
    public void Pass_AMarkThatWasClearedAndSetAgain_SettlesAgain()
    {
        Watch(1.0, Sample(0, 10));
        Watch(2.0);
        Watch(4.5, Sample(0, 10));

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.MarkEffectId), "the mark began again at 4.5 s");
    }

    [TestMethod]
    public void OnPlayerTeamChanged_ForgetsWhenEachMarkBegan()
    {
        Watch(1.0, Sample(5, 8));
        _watch.OnPlayerTeamChanged();

        Watch(4.0, Sample(5, 8));

        Assert.AreEqual(0, _log.Lines.Count(l => l.Id == MarshalChecks.MarkEffectId));
    }

    // ---- the verdict the field trial reads ----

    [TestMethod]
    public void JudgedOf_BeforeTheMarkHasStoodThreeSeconds_IsNull_ThenCarriesTheSampleTheLineWrote()
    {
        Watch(1.0, Sample(5, 8));
        Assert.IsNull(_watch.JudgedOf(2));

        Watch(4.0, Sample(6, 8));

        Assert.AreEqual(new MarkVerdict(Sample(6, 8), true), _watch.JudgedOf(2));
    }

    [TestMethod]
    public void JudgedOf_UnderHalfOnTarget_IsAFailedVerdict_TheSameOneTheFailLineCarries()
    {
        Watch(1.0, Sample(1, 8));
        Watch(4.0, Sample(1, 8));

        Assert.AreEqual(new MarkVerdict(Sample(1, 8), false), _watch.JudgedOf(2));
        Assert.AreEqual(1, _log.Count("fail", MarshalChecks.MarkEffectId));
    }

    [TestMethod]
    public void JudgedOf_AnyOtherContextOrTooFewTargets_IsNull_SoTheTrialNeverReadsAVerdictThatWasNotWritten()
    {
        Watch(1.0, Sample(4, 8, MarkContext.Other));
        Watch(4.0, Sample(4, 8, MarkContext.Other));
        Assert.IsNull(_watch.JudgedOf(2));

        Watch(5.0, Sample(1, 2));
        Watch(9.0, Sample(1, 2));
        Assert.IsNull(_watch.JudgedOf(2));
    }

    [TestMethod]
    public void JudgedOf_AMarkThatWasCleared_OrTheTeamChanged_IsForgotten()
    {
        Watch(1.0, Sample(5, 8));
        Watch(4.0, Sample(5, 8));
        Assert.IsNotNull(_watch.JudgedOf(2));

        Watch(5.0);
        Assert.IsNull(_watch.JudgedOf(2));

        Watch(6.0, Sample(5, 8));
        Watch(9.0, Sample(5, 8));
        Assert.IsNotNull(_watch.JudgedOf(2));
        _watch.OnPlayerTeamChanged();
        Assert.IsNull(_watch.JudgedOf(2));
    }

    // ---- the rules ----

    [TestMethod]
    public void ContextOf_OnlyARangedCompanyOnStopIsJudged()
    {
        Assert.AreEqual(MarkContext.RangedOnStop, MarshalMarkRules.ContextOf(ranged: true, onStop: true));
        Assert.AreEqual(MarkContext.Other, MarshalMarkRules.ContextOf(true, false), "a ranged company on a Move or anything else");
        Assert.AreEqual(MarkContext.Other, MarshalMarkRules.ContextOf(false, true), "a melee company on Stop");
        Assert.AreEqual(MarkContext.Other, MarshalMarkRules.ContextOf(false, false), "a melee company on a Move");
    }

    [TestMethod]
    public void Aims_ExactlyHalf_Counts_AndOneUnderDoesNot()
    {
        Assert.IsTrue(MarshalMarkRules.Aims(Sample(4, 8)));
        Assert.IsFalse(MarshalMarkRules.Aims(Sample(3, 8)));
        Assert.IsTrue(MarshalMarkRules.Aims(Sample(2, 3)));
    }

    // ---- the outlines ----

    private sealed class FakeOutlineAdapter : IMarshalOutlineAdapter<int>
    {
        internal readonly Dictionary<int, uint> Wanted = new();
        internal readonly List<(int Unit, uint? Colour)> Painted = new();
        internal readonly HashSet<int> ThrowsOnClear = new();
        internal int Reads;

        public void ReadWanted(Dictionary<int, uint> into)
        {
            Reads++;
            foreach (var pair in Wanted)
                into[pair.Key] = pair.Value;
        }

        public void Paint(int unit, uint? colour)
        {
            Painted.Add((unit, colour));
            if (colour == null && ThrowsOnClear.Contains(unit))
                throw new System.InvalidOperationException("unit " + unit + " would not clear");
        }
    }

    [TestMethod]
    public void Outlines_ShowingWithASelection_PaintsEveryWantedUnit()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline, [2] = MarshalTuning.MarkedOutline } };
        var service = new MarshalOutlineService<int>(adapter);

        service.Pass(1.0, show: true);

        Assert.AreEqual(2, service.PaintedCount);
        CollectionAssert.AreEquivalent(new[] { (1, (uint?)MarshalTuning.SelectedOutline), (2, (uint?)MarshalTuning.MarkedOutline) }, adapter.Painted);
    }

    [TestMethod]
    public void Outlines_ThePanelClosing_ClearsEverythingItPaintedInOnePass()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);

        service.Pass(1.25, show: false);

        Assert.AreEqual(0, service.PaintedCount);
        Assert.AreEqual((1, (uint?)null), adapter.Painted.Last());
        Assert.AreEqual(1, adapter.Reads, "when it should not show, the wanted set is empty and is not read");
    }

    [TestMethod]
    public void Outlines_NothingPaintedAndNotShowing_DoesNoWorkAtAll()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);

        service.Pass(1.0, show: false);

        Assert.AreEqual(0, adapter.Reads);
        Assert.AreEqual(0, adapter.Painted.Count);
    }

    [TestMethod]
    public void Outlines_APassInsideTheQuarterSecond_IsSkipped()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        adapter.Wanted.Clear();

        service.Pass(1.2, true);

        Assert.AreEqual(1, service.PaintedCount);
        service.Pass(1.25, true);
        Assert.AreEqual(0, service.PaintedCount);
    }

    [TestMethod]
    public void Outlines_OnlyAUnitItPainted_IsEverCleared()
    {
        var adapter = new FakeOutlineAdapter();
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        adapter.Wanted[5] = MarshalTuning.SelectedOutline;
        service.Pass(1.5, true);
        adapter.Wanted.Clear();

        service.Pass(2.0, true);

        CollectionAssert.AreEqual(new[] { (5, (uint?)MarshalTuning.SelectedOutline), (5, (uint?)null) }, adapter.Painted);
    }

    [TestMethod]
    public void Outlines_ARidersMountLeavingTheWantedSet_IsClearedWhileTheRiderKeepsItsOutline()
    {
        // The adapter reads a rider's mount as a wanted unit of its own, so the dictionary remembers every visual that was painted. A rider who
        // dismounted or died leaves the mount with no rider to reach it through, and the mount is cleared because it was painted.
        const int rider = 1;
        const int mount = 101;
        var adapter = new FakeOutlineAdapter { Wanted = { [rider] = MarshalTuning.SelectedOutline, [mount] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        adapter.Painted.Clear();
        adapter.Wanted.Remove(mount);

        service.Pass(1.5, true);

        CollectionAssert.AreEqual(new[] { (mount, (uint?)null) }, adapter.Painted);
        Assert.AreEqual(1, service.PaintedCount);
    }

    [TestMethod]
    public void Outlines_ARiderWhoDiedBeforeTheClear_LeavesNeitherTheRiderNorTheMountPainted()
    {
        const int rider = 1;
        const int mount = 101;
        var adapter = new FakeOutlineAdapter { Wanted = { [rider] = MarshalTuning.SelectedOutline, [mount] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        adapter.Painted.Clear();

        service.Pass(1.5, show: false);

        CollectionAssert.AreEquivalent(new[] { (rider, (uint?)null), (mount, (uint?)null) }, adapter.Painted);
        Assert.AreEqual(0, service.PaintedCount);
    }

    [TestMethod]
    public void Outlines_AUnitWhoseColourChanged_IsPaintedAgain()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        adapter.Wanted[1] = MarshalTuning.MarkedOutline;

        service.Pass(1.5, true);

        Assert.AreEqual((1, (uint?)MarshalTuning.MarkedOutline), adapter.Painted.Last());
        Assert.AreEqual(2, adapter.Painted.Count);
    }

    [TestMethod]
    public void Outlines_ClearAll_ClearsAtOnceOutsideTheCadence()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);

        service.ClearAll();

        Assert.AreEqual(0, service.PaintedCount);
        Assert.AreEqual((1, (uint?)null), adapter.Painted.Last());
    }

    [TestMethod]
    public void Outlines_ClearAll_OneUnitThatThrows_DoesNotStopTheOthers_AndTheFailureIsStillReported()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline, [2] = MarshalTuning.SelectedOutline, [3] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        adapter.Painted.Clear();
        adapter.ThrowsOnClear.Add(1);

        Assert.ThrowsException<System.InvalidOperationException>(() => service.ClearAll());

        CollectionAssert.AreEquivalent(new[] { (1, (uint?)null), (2, (uint?)null), (3, (uint?)null) }, adapter.Painted);
        Assert.AreEqual(0, service.PaintedCount, "a failed unit is not retried: nothing is known to throw, and a retry loop would be code for a guess");
    }

    [TestMethod]
    public void Outlines_Drop_ForgetsWhatWasPainted_WithNoNativeCall()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, true);
        var calls = adapter.Painted.Count;

        service.Drop();
        service.ClearAll();

        Assert.AreEqual(0, service.PaintedCount);
        Assert.AreEqual(calls, adapter.Painted.Count);
    }

    [TestMethod]
    public void Pass_ExactlyThreeSoldiersWithATarget_IsJudged()
    {
        Watch(1.0, Sample(2, 3));
        Watch(4.0, Sample(2, 3));

        Assert.AreEqual(1, _log.Count("pass", MarshalChecks.MarkEffectId));
    }

    [TestMethod]
    public void Outlines_AHiddenPassWithNothingPainted_DoesNotDelayTheNextShow()
    {
        var adapter = new FakeOutlineAdapter { Wanted = { [1] = MarshalTuning.SelectedOutline } };
        var service = new MarshalOutlineService<int>(adapter);
        service.Pass(1.0, show: false);

        service.Pass(1.1, show: true);

        Assert.AreEqual(1, service.PaintedCount, "an idle pass leaves the cadence alone, so the panel opening paints at once");
    }
}
