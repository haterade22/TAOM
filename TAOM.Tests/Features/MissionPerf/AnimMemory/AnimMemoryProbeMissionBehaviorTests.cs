using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Features.MissionPerf.AnimMemory.Hooks;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// The teardown of the clip memory probe's mission behavior, driven through the engine callbacks that
/// end it (<c>OnEndMissionInternal</c>, which calls <c>OnEndMission</c>, and <c>OnRemoveBehavior</c>)
/// against a real <see cref="AnimMemorySession"/>, a faked probe and a recording logger: the summary is
/// written once whichever callback comes first, a teardown that never calls EndMission still writes it,
/// and nothing samples once the session has ended. The source-text guard in
/// <see cref="AnimMemoryProbeWiringTests"/> pins only the shape of the two overrides and runs on hosted
/// CI; this class proves their effect, and needs the game assemblies because the engine's
/// <c>MissionBehavior</c> base runs in the constructor.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class AnimMemoryProbeMissionBehaviorTests
{
    private const string SummaryPrefix = "[AnimMem] summary:";

    private IAnimClipMemoryProbe _probe = null!;
    private IModLogger _logger = null!;
    private List<string> _info = null!;
    private AnimMemoryProbeMissionBehavior _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _probe = Substitute.For<IAnimClipMemoryProbe>();
        _probe.EnsureArmed().Returns(true);
        _probe.BudgetBytes.Returns(12582912);
        _probe.ReadLoadedBytes().Returns(10485760);
        _logger = Substitute.For<IModLogger>();
        _info = new List<string>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(call => _info.Add(call.Arg<string>()));
        _sut = new AnimMemoryProbeMissionBehavior(_probe, _logger);
    }

    [TestMethod]
    public void OnEndMission_AfterStart_WritesTheSummaryOnceAsTheLastLine()
    {
        _sut.AfterStart();

        _sut.OnEndMissionInternal();

        Assert.AreEqual(1, Summaries(), "OnEndMission must write the mission summary.");
        StringAssert.StartsWith(_info.Last(), SummaryPrefix, "the summary closes the mission's lines.");
    }

    [TestMethod]
    public void OnRemoveBehavior_WithoutOnEndMission_WritesTheSummaryOnce()
    {
        _sut.AfterStart();

        _sut.OnRemoveBehavior();

        Assert.AreEqual(1, Summaries(), "a teardown that never calls EndMission must still write the summary.");
    }

    [TestMethod]
    public void OnRemoveBehavior_AfterOnEndMission_WritesNothingMore()
    {
        _sut.AfterStart();
        _sut.OnEndMissionInternal();
        var linesAtEnd = _info.Count;

        _sut.OnRemoveBehavior();

        Assert.AreEqual(linesAtEnd, _info.Count, "the second teardown callback must not repeat the tail line or the summary.");
        Assert.AreEqual(1, Summaries());
    }

    [TestMethod]
    public void OnEndMission_ThenOnRemoveBehavior_TakesNoFurtherSample()
    {
        _sut.AfterStart();

        _sut.OnEndMissionInternal();
        _sut.OnRemoveBehavior();

        _probe.Received(1).ReadLoadedBytes();
        _probe.Received(1).IsAnyClipLoading();
    }

    [TestMethod]
    public void OnMissionTick_AfterTheSessionEnded_TakesNoSample()
    {
        _sut.AfterStart();
        _sut.OnEndMissionInternal();
        AgeTheClock(2.0);

        _sut.OnMissionTick(0.016f);

        _probe.Received(1).ReadLoadedBytes();
    }

    // The control for the test above: with the same aged clock, a live session does sample, so the
    // aged clock is what the ended session ignores and not a clock that never reaches the next sample.
    [TestMethod]
    public void OnMissionTick_WhileTheSessionRuns_SamplesOnceTheSecondHasPassed()
    {
        _sut.AfterStart();
        AgeTheClock(2.0);

        _sut.OnMissionTick(0.016f);

        _probe.Received(2).ReadLoadedBytes();
    }

    [TestMethod]
    public void TeardownCallbacks_WhenTheProbeNeverArmed_WriteNothing()
    {
        _probe.EnsureArmed().Returns(false);
        _sut.AfterStart();

        _sut.OnEndMissionInternal();
        _sut.OnRemoveBehavior();

        Assert.AreEqual(0, _info.Count, "no session started, so there is nothing to summarise.");
        _probe.DidNotReceive().ReadLoadedBytes();
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    private int Summaries() => _info.Count(line => line.StartsWith(SummaryPrefix, StringComparison.Ordinal));

    // The behavior times itself with Stopwatch.GetTimestamp, so a test cannot let a second pass without
    // sleeping; moving the start stamp back makes the next OnMissionTick read an older clock.
    private void AgeTheClock(double seconds)
    {
        var start = typeof(AnimMemoryProbeMissionBehavior).GetField("_start", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(start, "AnimMemoryProbeMissionBehavior._start was renamed or removed: update AgeTheClock.");
        start!.SetValue(_sut, (long)start.GetValue(_sut)! - (long)(seconds * Stopwatch.Frequency));
    }
}
