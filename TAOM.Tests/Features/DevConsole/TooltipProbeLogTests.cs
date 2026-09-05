using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// Pins the rate limiting behind the tooltip probes.
///
/// Both probes fire on every hover, so they cannot log unconditionally. They also cannot use a
/// single global latch: that is the mistake that sat in SpecialResourceMapBarMixin, where the first
/// failure was logged and every later DIFFERENT failure was invisible forever. The limiter is
/// therefore per key, and a type that fails and later succeeds must report both, because that
/// transition is the most informative thing the probe can tell us.
///
/// The build probe distinguishes three outcomes, because GauntletInformationView.OnShowTooltip has
/// three silent exits with different field state left behind, and collapsing them would hide which
/// stage failed.
/// </summary>
[TestClass]
public class TooltipProbeLogTests
{
    private TooltipProbeLog _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new TooltipProbeLog();

    // ---------- hover probe ----------

    [TestMethod]
    public void TryRecordHover_FirstTimeForAnItem_ReportsAndNamesTheItem()
    {
        var recorded = _sut.TryRecordHover("gold", out var message);

        Assert.IsTrue(recorded);
        StringAssert.Contains(message, "gold");
    }

    [TestMethod]
    public void TryRecordHover_SameItemTwice_ReportsOnlyOnce()
    {
        _sut.TryRecordHover("gold", out _);

        Assert.IsFalse(_sut.TryRecordHover("gold", out _));
    }

    [TestMethod]
    public void TryRecordHover_DifferentItems_EachReportOnce()
    {
        // The whole point of per-key limiting: hovering gold must not silence food.
        Assert.IsTrue(_sut.TryRecordHover("gold", out _));
        Assert.IsTrue(_sut.TryRecordHover("food", out _));
        Assert.IsTrue(_sut.TryRecordHover("special_resource", out _));
    }

    [TestMethod]
    public void TryRecordHover_NullItemId_StillReportsUnderAPlaceholder()
    {
        var recorded = _sut.TryRecordHover(null, out var message);

        Assert.IsTrue(recorded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(message));
    }

    [TestMethod]
    public void TryRecordHover_NullThenEmpty_CollapseToTheSameKey()
    {
        _sut.TryRecordHover(null, out _);

        Assert.IsFalse(_sut.TryRecordHover("", out _));
    }

    // ---------- build probe ----------

    [TestMethod]
    public void TryRecordBuild_FirstConstructionFailureForAType_ReportsAndSaysItFailed()
    {
        var recorded = _sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out var message);

        Assert.IsTrue(recorded);
        StringAssert.Contains(message, "System.String");
        StringAssert.Contains(message, "FAILED");
        StringAssert.Contains(message, "CONSTRUCTION");
    }

    [TestMethod]
    public void TryRecordBuild_MovieFailure_ReportsAndNamesTheMovieStage()
    {
        // The third silent exit: the view model exists, LoadMovie threw. Naming the stage is the
        // point, because it tells the reader to look at the prefab rather than the tooltip data.
        var recorded = _sut.TryRecordBuild("System.String", TooltipBuildOutcome.ConstructedButMovieFailed, out var message);

        Assert.IsTrue(recorded);
        StringAssert.Contains(message, "FAILED");
        StringAssert.Contains(message, "MOVIE");
        StringAssert.Contains(message, "LoadMovie");
    }

    [TestMethod]
    public void TryRecordBuild_MovieFailureAndConstructionFailure_AreDistinctKeys()
    {
        Assert.IsTrue(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _));
        Assert.IsTrue(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.ConstructedButMovieFailed, out _));
    }

    [TestMethod]
    public void TryRecordBuild_FailureMessages_ExplainWhyNothingElseShowsThem()
    {
        // The reader needs to know this is the engine's swallowed FailedAssert, and that rgl_log is
        // the only other place it would ever appear.
        _sut.TryRecordBuild("A", TooltipBuildOutcome.NotConstructed, out var construction);
        _sut.TryRecordBuild("B", TooltipBuildOutcome.ConstructedButMovieFailed, out var movie);

        StringAssert.Contains(construction, "rgl_log");
        StringAssert.Contains(movie, "rgl_log");
    }

    [TestMethod]
    public void TryRecordBuild_SameTypeFailsRepeatedly_ReportsOnlyOnce()
    {
        _sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _);

        Assert.IsFalse(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _));
    }

    [TestMethod]
    public void TryRecordBuild_DifferentTypes_EachReportOnce()
    {
        Assert.IsTrue(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _));
        Assert.IsTrue(_sut.TryRecordBuild("TaleWorlds.CampaignSystem.Party.MobileParty", TooltipBuildOutcome.NotConstructed, out _));
    }

    /// <summary>
    /// The anti-one-shot-latch guarantee. A type that failed and then works is the single most
    /// useful observation the probe can make, and a naive per-type latch would hide it.
    /// </summary>
    [TestMethod]
    public void TryRecordBuild_TypeThatFailedThenSucceeds_ReportsBothOutcomes()
    {
        Assert.IsTrue(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _));
        Assert.IsTrue(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.Built, out var okMessage));

        StringAssert.Contains(okMessage, "built");
    }

    [TestMethod]
    public void TryRecordBuild_SameTypeSucceedsRepeatedly_ReportsOnlyOnce()
    {
        _sut.TryRecordBuild("System.String", TooltipBuildOutcome.Built, out _);

        Assert.IsFalse(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.Built, out _));
    }

    [TestMethod]
    public void TryRecordBuild_NullTypeName_StillReportsUnderAPlaceholder()
    {
        var recorded = _sut.TryRecordBuild(null, TooltipBuildOutcome.NotConstructed, out var message);

        Assert.IsTrue(recorded);
        Assert.IsFalse(string.IsNullOrWhiteSpace(message));
    }

    // ---------- lifecycle ----------

    [TestMethod]
    public void Reset_AllowsEverythingToReportAgain()
    {
        // A new campaign in the same process is a new question; the probe should speak again.
        _sut.TryRecordHover("gold", out _);
        _sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _);

        _sut.Reset();

        Assert.IsTrue(_sut.TryRecordHover("gold", out _));
        Assert.IsTrue(_sut.TryRecordBuild("System.String", TooltipBuildOutcome.NotConstructed, out _));
    }
}
