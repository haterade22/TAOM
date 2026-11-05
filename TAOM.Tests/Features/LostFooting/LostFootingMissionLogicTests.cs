using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LostFooting;
using TAOM.Features.LostFooting.Domain;
using TAOM.Features.LostFooting.Hooks;

namespace TAOM.Tests.Features.LostFooting;

/// <summary>
/// The census behavior under a real <c>MissionLogic</c> base: the error latch and the one summary. Tagged RequiresGame
/// because the engine base class is constructed; the service tests cover the same ground without it.
/// </summary>
[TestClass]
public class LostFootingMissionLogicTests
{
    private FakeLostFootingAdapter _adapter = null!;
    private IModLogger _logger = null!;
    private System.Collections.Generic.List<string> _info = null!;
    private LostFootingMissionLogic _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _adapter = new FakeLostFootingAdapter();
        _logger = Substitute.For<IModLogger>();
        _info = new System.Collections.Generic.List<string>();
        _logger.When(l => l.LogInfo(Arg.Any<string>())).Do(c => _info.Add(c.Arg<string>()));
        var settings = Substitute.For<ILostFootingSettingsProvider>();
        settings.CensusEnabled.Returns(true);
        var service = new LostFootingService(_adapter, settings, _logger, new LostFootingTunables(), new SteppingStampClock(1), () => true);
        _sut = new LostFootingMissionLogic(service, _logger);
        _adapter.Agents.Add(new AgentProbe());
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Tick_AThrowingAdapter_IsReadOnce_LogsOneError_ThenTheBehaviorIsInert()
    {
        _adapter.ThrowOnRead = new InvalidOperationException("boom");

        _sut.OnMissionTick(1f);
        _sut.OnMissionTick(1f);
        _sut.OnMissionTick(1f);

        Assert.AreEqual(1, _adapter.Slices.Count);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("[LostFooting] disabled for this mission")));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Teardown_EndThenRemove_WritesExactlyOneSummary()
    {
        _sut.OnMissionTick(1f);

        _sut.OnEndMissionInternal();
        _sut.OnRemoveBehavior();

        Assert.AreEqual(1, _info.Count(l => l.Contains("summary scene=")));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Teardown_AfterTheLatch_StillWritesTheSummaryOfWhatWasSeen()
    {
        _sut.OnMissionTick(1f);
        _adapter.ThrowOnRead = new InvalidOperationException("boom");
        _sut.OnMissionTick(1f);

        _sut.OnRemoveBehavior();

        Assert.AreEqual(1, _info.Count(l => l.Contains("summary scene=")));
    }
}
