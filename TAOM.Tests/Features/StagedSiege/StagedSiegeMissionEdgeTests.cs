using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;
using TAOM.Features.StagedSiege.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The mission edge's catch (#1054): it logs the boundary fault with its true effect (this call skipped, or the staged rules off
/// for the mission after <c>EarlyStart</c>) and tells the service and hall it holds, so the run report's <c>faults</c> check sees
/// a fault that never reached a service.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StagedSiegeMissionEdgeTests
{
    private IModLogger _log = null!;
    private StagedSiegeService _service = null!;
    private object _token = null!;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        StagedSiegeSlot.ClearCampaignSettlement();
        _token = new object();
        _log = Substitute.For<IModLogger>();
        var adapter = Substitute.For<IStagedSiegeMissionAdapter>();
        adapter.MissionToken.Returns(_token);
        adapter.SceneName.Returns("sturgia_castle_siege_001");
        adapter.IsSiegeBattle.Returns(true);
        adapter.IsCampaignWallsAssault.Returns(true);
        adapter.PlayerSide.Returns((StagedSide?)StagedSide.Attacker);
        adapter.SpawnFlags(Arg.Any<IReadOnlyList<StageZone>>()).Returns(4);
        var catalog = StagedSiegeSceneCatalog.FromText(StagedSiegeSceneCatalogTests.PilotText, Substitute.For<IModLogger>());
        _service = new StagedSiegeService(adapter, catalog, _log);
        Assert.IsTrue(_service.TryArm(true));
    }

    [TestCleanup]
    public void Cleanup()
    {
        StagedSiegeSlot.ClearArmed();
        StagedSiegeSlot.ClearLastEnded();
        StagedSiegeSlot.ClearCampaignSettlement();
    }

    [TestMethod]
    public void Fault_LogsTheBoundaryWordingNotStagedRulesOff()
    {
        var edge = new StagedSiegeMissionEdge(_log);

        edge.Fault("MissionEnded", new InvalidOperationException("edge boom"), _service, null);

        _log.Received(1).LogError(Arg.Is<string>(s => s.StartsWith("[StagedSiege] boundary fault in MissionEnded: ")
            && s.Contains("edge boom") && s.EndsWith("; this call skipped, vanilla continues", StringComparison.Ordinal)));
        _log.DidNotReceive().LogError(Arg.Is<string>(s => s.Contains("staged rules off")));
    }

    [TestMethod]
    public void Fault_NotesTheBoundaryFaultInTheServiceAndLeavesItLive()
    {
        var edge = new StagedSiegeMissionEdge(_log);

        edge.Fault("OnAgentBuild", new ArgumentException("edge"), _service, null);

        Assert.IsTrue(_service.IsArmed);
        Assert.IsFalse(_service.IsFaulted);
        _service.Shutdown();
        _log.Received().LogInfo(Arg.Is<string>(s => s.Contains("CHECK faults FAIL: boundary OnAgentBuild: ArgumentException")));
    }

    [TestMethod]
    public void Fault_WithNoServiceOrHall_OnlyLogs()
    {
        var edge = new StagedSiegeMissionEdge(_log);

        edge.Fault("OnAgentBuild", new InvalidOperationException("late"));

        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("boundary fault in OnAgentBuild")
            && s.EndsWith("; this call skipped, vanilla continues")));
    }

    [TestMethod]
    public void Fault_WithAnEffect_LogsThatEffect()
    {
        var edge = new StagedSiegeMissionEdge(_log);

        edge.Fault("EarlyStart", new InvalidOperationException("early"), effect: StagedSiegeMissionEdge.RulesOffForMission);

        _log.Received(1).LogError(Arg.Is<string>(s => s.Contains("boundary fault in EarlyStart")
            && s.EndsWith("; staged rules off for this mission, vanilla continues")));
    }
}
