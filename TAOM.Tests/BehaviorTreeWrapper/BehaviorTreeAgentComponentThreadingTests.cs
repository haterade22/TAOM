using System.Linq;
using System.Reflection;
using BehaviorTreeWrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.BehaviorTreeWrapper;

/// <summary>
/// <c>Mission.OnAgentRemoved</c> reaches <c>AgentComponent.OnAgentRemoved</c> through
/// <c>Agent.OnRemove</c> on whichever thread native raised the removal, and a player's log caught
/// it off the main thread (#634). The component used to remove itself from the logic's schedule list and tree
/// map right there, racing the main thread. Its removal now goes through the logic's deferral queue.
/// Neither the component nor the logic can be built in a test (native agent, BehaviorTrees.dll), so
/// this pins the routing in the IL: the body hands off, it never touches the collections itself.
/// </summary>
[TestClass]
public class BehaviorTreeAgentComponentThreadingTests
{
    private static MethodBase[] CallsIn(MethodBase method) =>
        IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToArray();

    private static readonly MethodInfo OnAgentRemoved =
        typeof(BehaviorTreeAgentComponent).GetMethod(nameof(BehaviorTreeAgentComponent.OnAgentRemoved),
            BindingFlags.Public | BindingFlags.Instance, null, System.Type.EmptyTypes, null);

    [TestMethod]
    public void OnAgentRemoved_HandsTheRemovalToTheMissionThread()
    {
        var calls = CallsIn(OnAgentRemoved);

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(BehaviorTreeMissionLogic) && m.Name == "RunOnMissionThread"),
            "OnAgentRemoved must route its cleanup through BehaviorTreeMissionLogic.RunOnMissionThread");
    }

    // The handed-off cleanup must still do the work: an emptied Retire would pass both tests above.
    [TestMethod]
    public void Retire_UnschedulesTheComponentAndDisposesItsTree()
    {
        var retire = typeof(BehaviorTreeAgentComponent).GetMethod("Retire", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(retire, "BehaviorTreeAgentComponent.Retire was renamed; this pin no longer sees it.");

        var calls = CallsIn(retire);

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(BehaviorTreeMissionLogic) && m.Name == "Unschedule"));
        Assert.IsTrue(calls.Any(m => m.Name == nameof(BehaviorTreeBannerlordWrapper.DisposeTree)));
    }

    [TestMethod]
    public void OnAgentRemoved_NeverTouchesTheScheduleOrTreeMapDirectly()
    {
        var calls = CallsIn(OnAgentRemoved);

        Assert.IsFalse(calls.Any(m => m.Name == "Unschedule"), "Unschedule mutates a List the mission tick iterates");
        Assert.IsFalse(calls.Any(m => m.Name == nameof(BehaviorTreeBannerlordWrapper.DisposeTree)),
            "DisposeTree mutates the Dictionary every listener lookup reads");
    }
}
