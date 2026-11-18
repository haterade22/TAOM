using System;
using System.Runtime.Serialization;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MissionDiagnostic;
using TAOM.Features.MissionDiagnostic.Hooks;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The behavior's wiring of the off-thread drop (#607): both engine callbacks park their drop when native raises them
// off the main thread, report the site once to the file log, and the next tick applies the drop before it reads any
// agent. The tick marks the main thread itself, so the deferral never leans on another behavior's mark (#634).
[TestClass]
[TestCategory("RequiresGame")]   // MissionDiagnosticBehavior derives from the engine's MissionLogic
public class MissionDiagnosticOffThreadTests
{
    private IMissionDiagnosticService _service = null!;
    private IModLogger _logger = null!;
    private MissionDiagnosticBehavior _sut = null!;

    // The drop keeps the reference and never reads it (Agent.GetHashCode is a managed field), so a bare instance is
    // enough, as in MountDespawnOffThreadTests. Reading it (CaptureActionSet) would reach native code and fail.
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    private static void OnWorker(Action action)
    {
        var worker = new Thread(() => action());
        worker.Start();
        worker.Join();
    }

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
        _service = Substitute.For<IMissionDiagnosticService>();
        _logger = Substitute.For<IModLogger>();
        _sut = new MissionDiagnosticBehavior(_service, Substitute.For<IRaceManager>(), _logger);
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void OnMissionTick_MarksTheMainThreadItself()
    {
        MissionThreadGuard.ResetForTests();
        _sut.OnMissionTick(0.016f);

        var onMain = true;
        OnWorker(() => onMain = MissionThreadGuard.IsOnMainThread);

        Assert.IsFalse(onMain, "once the behavior has ticked, a worker must read as off the main thread");
    }

    [TestMethod]
    public void OnAgentRemoved_OffTheMainThread_IsParked_AndTheAgentIsNeverRead()
    {
        var agent = BareAgent();
        _sut.OnAgentCreated(agent);

        OnWorker(() => _sut.OnAgentRemoved(agent, null!, AgentState.Killed, default));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("MissionDiagnosticBehavior.OnAgentRemoved ran off the main mission thread")));

        _sut.OnMissionTick(0.016f);

        _service.DidNotReceiveWithAnyArgs().TryMarkActionSetKey(default, default, default);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("CaptureActionSet failed")));
    }

    [TestMethod]
    public void OnAgentDeleted_OffTheMainThread_IsParked_AndTheAgentIsNeverRead()
    {
        var agent = BareAgent();
        _sut.OnAgentCreated(agent);

        OnWorker(() => _sut.OnAgentDeleted(agent));
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("MissionDiagnosticBehavior.OnAgentDeleted ran off the main mission thread")));

        _sut.OnMissionTick(0.016f);

        _service.DidNotReceiveWithAnyArgs().TryMarkActionSetKey(default, default, default);
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(s => s.Contains("CaptureActionSet failed")));
    }

    // After a failed read nothing is pending and nothing drains until mission end, so a parked drop would only pile up.
    [TestMethod]
    public void AfterAReadFault_AnOffThreadDrop_IsNotParked()
    {
        _sut.OnAgentCreated(BareAgent());
        _sut.OnMissionTick(0.016f); // reading a bare agent throws (MBAPI.IMBAgent is null here), so census reads stop
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("CaptureActionSet failed")));

        OnWorker(() => _sut.OnAgentRemoved(BareAgent(), null!, AgentState.Killed, default));
        OnWorker(() => _sut.OnAgentDeleted(BareAgent()));

        Assert.AreEqual(0L, MissionThreadGuard.OffThreadCalls, "with census reads stopped, an off-thread drop must not be parked");
    }
}
