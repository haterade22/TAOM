using System.Linq;
using System.Reflection;
using BehaviorTreeWrapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Warg;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// A v1.4.8 player log caught <c>OnAgentDismount</c> off the main thread (#634). The warg rider's look
/// direction it resets is written by this behavior's own mission tick, so the reset is parked for that
/// tick. The behavior resolves services from IoC in its constructor, so this pins the routing in the IL.
/// </summary>
[TestClass]
public class WargOffThreadTests
{
    private static MethodBase[] CallsIn(string name) =>
        IlCallScanner.ExtractCalledMethods(
            typeof(WargMissionBehavior).GetMethod(name, BindingFlags.Public | BindingFlags.Instance),
            typeof(WargMissionBehavior).GetMethod(name, BindingFlags.Public | BindingFlags.Instance).GetMethodBody().GetILAsByteArray())
        .ToArray();

    [TestMethod]
    public void OnAgentDismount_ParksTheResetThroughTheDeferralQueue()
    {
        var calls = CallsIn(nameof(WargMissionBehavior.OnAgentDismount));

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(DeferredCallbackQueue) && m.Name == nameof(DeferredCallbackQueue.RunOrDefer)));
        Assert.IsFalse(calls.Any(m => m.Name == nameof(WargRiderHandManager.OnMainAgentDismount)),
            "the reset must not run on the callback's own thread");
    }

    [TestMethod]
    public void OnMissionTick_ReplaysParkedCallbacks()
    {
        var calls = CallsIn(nameof(WargMissionBehavior.OnMissionTick));

        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(DeferredCallbackQueue) && m.Name == nameof(DeferredCallbackQueue.Drain)));
    }
}
