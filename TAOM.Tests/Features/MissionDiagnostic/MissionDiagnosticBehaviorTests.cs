using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.MissionDiagnostic;
using TAOM.Features.MissionDiagnostic.Hooks;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The action-set census writes its closing summary exactly once per mission: when the 5 s window runs out, or at
// mission end when the mission is shorter. The window is an engine float (dt), so the close is a positive
// requirement (csharp-architecture.md, "Engine-Float Decision Gates"): a NaN dt must close it, never leave the
// "census open" header without its "closed" line. With no Mission the capture finds no agents, so only the
// window bookkeeping runs.
[TestClass]
[TestCategory("RequiresGame")]   // MissionDiagnosticBehavior derives from the engine's MissionLogic
public class MissionDiagnosticBehaviorTests
{
    private IMissionDiagnosticService _service = null!;
    private MissionDiagnosticBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IMissionDiagnosticService>();
        _sut = new MissionDiagnosticBehavior(_service, Substitute.For<IRaceManager>(), Substitute.For<IModLogger>());
    }

    [TestMethod]
    public void OnMissionTick_WindowRunsOut_ClosesTheCensusOnce()
    {
        _sut.OnMissionTick(2.5f);
        _service.DidNotReceive().LogActionSetCensusClosed();

        _sut.OnMissionTick(2.5f);
        _sut.OnMissionTick(2.5f);
        _sut.OnEndMissionInternal();

        _service.Received(1).LogActionSetCensusOpened(5f);
        _service.Received(1).LogActionSetCensusClosed();
    }

    [TestMethod]
    public void OnEndMissionInternal_MissionShorterThanTheWindow_ClosesTheCensusOnce()
    {
        _sut.OnMissionTick(1f);

        _sut.OnEndMissionInternal();

        _service.Received(1).LogActionSetCensusClosed();
    }

    [TestMethod]
    public void OnMissionTick_NaNFrameTime_ClosesTheCensusOnce()
    {
        _sut.OnMissionTick(float.NaN);
        _sut.OnMissionTick(0.016f);
        _sut.OnEndMissionInternal();

        _service.Received(1).LogActionSetCensusClosed();
    }
}
