using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Inventory;

[TestClass]
public class InventoryOpenFastPathServiceTests
{
    private FakeStampClock _clock = null!;
    private RecordingLogger _logger = null!;
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private InventoryOpenFastPathService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _clock = new FakeStampClock();
        _logger = new RecordingLogger();
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.InventoryFastOpen.Returns(true);
        _sut = new InventoryOpenFastPathService(_settings, _clock, _logger);
    }

    [TestMethod]
    public void Enabled_FollowsTheToggle_PerCall()
    {
        Assert.IsTrue(_sut.Enabled);
        _settings.InventoryFastOpen.Returns(false);
        Assert.IsFalse(_sut.Enabled);
        _settings.InventoryFastOpen.Returns(true);
        Assert.IsTrue(_sut.Enabled);
    }

    [TestMethod]
    public void ShouldSkipNameSort_FollowsTheToggle()
    {
        Assert.IsTrue(_sut.ShouldSkipNameSort());
        _settings.InventoryFastOpen.Returns(false);
        Assert.IsFalse(_sut.ShouldSkipNameSort());
    }

    [TestMethod]
    public void ReportSite_LogsOneLinePerSite_NoMatterHowOftenItIsReported()
    {
        _sut.ReportSite("name sort", 2, 2, null);
        _sut.ReportSite("name sort", 2, 2, null);
        _sut.ReportSite("other", 0, 2, "found 3 OrderBy call sites");
        _sut.ReportSite("other", 0, 2, "found 3 OrderBy call sites");

        CollectionAssert.AreEqual(new[]
        {
            "INFO [ShadowfaxInventory] name sort: swapped 2 of 2 call sites",
            "WARN [ShadowfaxInventory] other: left vanilla (swapped 0 of 2 call sites): found 3 OrderBy call sites",
        }, _logger.Lines);
    }

    [TestMethod]
    public void RowsPhase_OutsideAnOpen_IsNotCounted()
    {
        Assert.IsNull(_sut.BeginRows());
        _sut.EndRows(null);
        _sut.EndRows(5);

        _sut.BeginOpen();
        Assert.AreEqual("fast open on; mode=trade, left=3, right=4, rows=0.0 ms, sorts skipped=0",
            _sut.EndOpen("trade", 3, 4));
    }

    [TestMethod]
    public void Detail_ReportsModeCountsRowsAndCounters()
    {
        _sut.BeginOpen();
        var rows = _sut.BeginRows();
        _clock.Advance(41);
        _sut.EndRows(rows);
        _sut.ShouldSkipNameSort();
        _sut.ShouldSkipNameSort();

        Assert.AreEqual("fast open on; mode=inventory, left=312, right=88, rows=41.0 ms, sorts skipped=2",
            _sut.EndOpen("inventory", 312, 88));
    }

    [TestMethod]
    public void Detail_WhenOff_SaysSoAndSkipsNothing()
    {
        _settings.InventoryFastOpen.Returns(false);
        _sut.BeginOpen();
        _sut.ShouldSkipNameSort();

        StringAssert.StartsWith(_sut.EndOpen("trade", 1, 2), "fast open off; mode=trade");
    }

    [TestMethod]
    public void Detail_ReportsTheFastPathTheOpenStartedWith_NotTheToggleAtTheEnd()
    {
        _sut.BeginOpen();
        _settings.InventoryFastOpen.Returns(false);

        StringAssert.StartsWith(_sut.EndOpen("trade", 1, 2), "fast open on;");
    }

    [TestMethod]
    public void Detail_OffOpen_CountsNoSkippedSorts()
    {
        _settings.InventoryFastOpen.Returns(false);
        _sut.BeginOpen();
        _sut.ShouldSkipNameSort();

        StringAssert.Contains(_sut.EndOpen("trade", 1, 2), "rows=0.0 ms, sorts skipped=0");
    }

    [TestMethod]
    public void Detail_UnreadableCounts_PrintNa()
    {
        _sut.BeginOpen();

        StringAssert.StartsWith(_sut.EndOpen(null, -1, -1), "fast open on; mode=n/a, left=n/a, right=n/a");
    }

    [TestMethod]
    public void EndOpen_ClosesTheWindow_SoLaterRowsAreNotCounted()
    {
        _sut.BeginOpen();
        _sut.EndOpen("trade", 1, 1);

        Assert.IsNull(_sut.BeginRows());
        Assert.IsFalse(_sut.OpenActive);
    }

    [TestMethod]
    public void BeginOpen_ResetsTheCountersOfThePreviousOpen()
    {
        _sut.BeginOpen();
        _sut.ShouldSkipNameSort();
        _sut.EndOpen("trade", 1, 1);
        _sut.BeginOpen();

        StringAssert.Contains(_sut.EndOpen("trade", 1, 1), "sorts skipped=0");
    }

}
