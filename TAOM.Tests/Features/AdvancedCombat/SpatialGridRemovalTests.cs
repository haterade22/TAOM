using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using BehaviorTreeWrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.AdvancedCombat;

/// <summary>
/// <c>AdvancedCombatBehavior.OnAgentDeleted</c> drops a deleted agent from the grid (#595), and
/// <c>OnAgentDeleted</c> is native's to place (#634). The grid's cell lists belong to the mission tick,
/// so a removal raised elsewhere waits for the next <c>ApplyPendingRemovals</c>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SpatialGridRemovalTests
{
    private static Agent BareAgent() => (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));

    [TestInitialize]
    public void Setup()
    {
        MissionThreadGuard.ResetForTests();
        MissionThreadGuard.MarkMainThread();
    }

    [TestCleanup]
    public void Cleanup() => MissionThreadGuard.ResetForTests();

    [TestMethod]
    public void Remove_OnTheMainThread_AppliesAtOnce()
    {
        var grid = new SpatialGrid();

        grid.Remove(BareAgent());

        Assert.AreEqual(0, grid.PendingRemovalCount);
    }

    [TestMethod]
    public void Remove_OffTheMainThread_WaitsForApplyPendingRemovals()
    {
        var grid = new SpatialGrid();
        var worker = new Thread(() => grid.Remove(BareAgent()));
        worker.Start();
        worker.Join();

        Assert.AreEqual(1, grid.PendingRemovalCount, "the worker must not touch the cell lists");

        grid.ApplyPendingRemovals();

        Assert.AreEqual(0, grid.PendingRemovalCount);
    }

    [TestMethod]
    public void AdvancedCombatBehavior_OnMissionTick_AppliesPendingRemovals()
    {
        var tick = typeof(AdvancedCombatBehavior).GetMethod(nameof(AdvancedCombatBehavior.OnMissionTick));
        var calls = IlCallScanner.ExtractCalledMethods(tick, tick.GetMethodBody().GetILAsByteArray()).ToArray();

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.ApplyPendingRemovals)),
            "a removal parked off-thread is only ever applied from the mission tick");
    }
}
