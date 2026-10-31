using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.UI;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>What the HUD view model hands the run report (#1054): each letter:kind it showed and the marker letters, once each.</summary>
[TestClass]
public class StagedSiegeHudRecorderTests
{
    private readonly object _token = new();

    [TestInitialize]
    public void Setup() => StagedSiegeSlot.ClearArmed();

    [TestCleanup]
    public void Cleanup() => StagedSiegeSlot.ClearArmed();

    [TestMethod]
    public void Shown_RecordsLetterAndKind()
    {
        var recorder = new StagedSiegeHudRecorder();

        recorder.Shown(_token, "B", HudObjectiveKind.Take);

        CollectionAssert.AreEqual(new[] { "B:Take" }, StagedSiegeSlot.HudFor(_token)!.Pairs.ToList());
    }

    [TestMethod]
    public void Shown_TheHallKinds_AreNamedAsTheReportExpects()
    {
        var recorder = new StagedSiegeHudRecorder();

        recorder.Shown(_token, "E", HudObjectiveKind.TakeHall);
        recorder.Shown(_token, "E", HudObjectiveKind.HallTaken);

        CollectionAssert.AreEqual(new[] { "E:HallTaken", "E:TakeHall" }, StagedSiegeSlot.HudFor(_token)!.Pairs.ToList());
    }

    [TestMethod]
    public void Shown_SamePairAgain_IsNotWrittenToTheSlotAgain()
    {
        var recorder = new StagedSiegeHudRecorder();
        recorder.Shown(_token, "A", HudObjectiveKind.Take);
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.RecordHud(new object(), "other:Take");

        recorder.Shown(_token, "A", HudObjectiveKind.Take);

        Assert.IsNull(StagedSiegeSlot.HudFor(_token), "an unchanged pair is skipped, so a refresh costs a string compare, not a lock");
    }

    [TestMethod]
    public void Marker_Visible_RecordsTheLetterOnce()
    {
        var recorder = new StagedSiegeHudRecorder();
        recorder.Shown(_token, "C", HudObjectiveKind.Hold);

        recorder.Marker(true, "C");
        recorder.Marker(true, "C");

        CollectionAssert.AreEqual(new[] { "C" }, StagedSiegeSlot.HudFor(_token)!.Markers.ToList());
    }

    [TestMethod]
    public void Marker_NotVisible_RecordsNothing()
    {
        var recorder = new StagedSiegeHudRecorder();
        recorder.Shown(_token, "C", HudObjectiveKind.Hold);

        recorder.Marker(false, "C");

        Assert.AreEqual(0, StagedSiegeSlot.HudFor(_token)!.Markers.Count);
    }

    [TestMethod]
    public void Marker_BeforeAnyRefresh_RecordsNothing()
    {
        var recorder = new StagedSiegeHudRecorder();

        recorder.Marker(true, "A");

        Assert.IsNull(StagedSiegeSlot.HudFor(_token));
    }

    [TestMethod]
    public void Describe_NamesStripAndObjective() =>
        Assert.AreEqual("HUD: strip \"A+ [B] C D\", objective \"Take point B\"", StagedSiegeHudRecorder.Describe("A+ [B] C D", "Take point B"));
}
