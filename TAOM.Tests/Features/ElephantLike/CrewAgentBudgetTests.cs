using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ElephantLike;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

// The engine's agent pool holds 0x7F8 (2,040) agents (#1107). Past that, its slot search returns -1 and the native
// spawn writes through that index over the mission's own memory, so a spawn outside vanilla's budget must check first.

namespace TAOM.Tests.Features.ElephantLike;

[TestClass]
public class CrewAgentBudgetTests
{
    private const int EngineLimit = 2040;

    [TestMethod]
    public void HasRoom_OneSlotLeft_True() =>
        Assert.IsTrue(CrewAgentBudget.HasRoom(EngineLimit - 1, EngineLimit));

    [TestMethod]
    public void HasRoom_AtTheLimit_False() =>
        Assert.IsFalse(CrewAgentBudget.HasRoom(EngineLimit, EngineLimit));

    [TestMethod]
    public void HasRoom_PastTheLimit_False() =>
        Assert.IsFalse(CrewAgentBudget.HasRoom(EngineLimit + 1, EngineLimit));

    [TestMethod]
    public void EveryDirectSpawnAgentCaller_ChecksTheAgentBudget()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var spawners = TaomAssemblies.FindCallers(IsMissionSpawnAgent, out var unreadable, out int scanned);
        var checkers = TaomAssemblies.FindCallers(IsHasRoom, out _, out _);

        Assert.AreEqual(0, unreadable.Count, "Unreadable method bodies, so the gate cannot vouch: " + string.Join("; ", unreadable));
        Assert.IsTrue(scanned > 0, "No method bodies scanned: the scan failed rather than passed.");
        // Both crew spawners call it today; fewer than two callers means the match broke, not that the code is clean.
        Assert.IsTrue(spawners.Count >= 2, "The scan found too few Mission.SpawnAgent callers: " + string.Join("; ", spawners));

        // This proves that a HasRoom call exists in the same method. It does not prove that the call guards the spawn.
        var unchecked_ = spawners.Except(checkers, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, unchecked_.Count,
            "Mission.SpawnAgent has no capacity check. Call CrewAgentBudget.HasRoom in the same method: " + string.Join("; ", unchecked_));
    }

    private static bool IsMissionSpawnAgent(MethodBase called) =>
        called.Name == nameof(Mission.SpawnAgent) && called.DeclaringType == typeof(Mission);

    private static bool IsHasRoom(MethodBase called) =>
        called.Name == nameof(CrewAgentBudget.HasRoom) && called.DeclaringType == typeof(CrewAgentBudget);
}
