using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.CoopInterop.Missions;
using TAOM.Features.DeadTargetReset;
using TAOM.Features.DeadTargetReset.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.DeadTargetReset;

/// <summary>
/// Wiring guard for the dead-target reset (#1096). The module list adds the behavior to every mission, and the behavior must use the
/// engine's own reset (InvalidateTargetAgent reaches the same native reset a rout does, which clears the target and its flag
/// together). SetTargetAgent(null) is the unsafe path: natively it leaves the flag set, the state that crashes.
/// </summary>
[TestClass]
public class DeadTargetResetWiringTests
{
    private const string Behavior = "Main/Features/DeadTargetReset/Hooks/DeadTargetResetMissionBehavior.cs";

    private static string Squashed() => Regex.Replace(RepoPaths.ReadSource(Behavior, stripComments: true), @"\s+", " ");

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<DeadTargetResetModule>().Count());
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior()
    {
        var decls = new DeadTargetResetModule().MissionBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(DeadTargetResetMissionBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void TheBehavior_ResetsThroughTheEngineReset_NeverThroughSetTargetAgent()
    {
        var src = Squashed();

        StringAssert.Contains(src, ".InvalidateTargetAgent()");
        Assert.IsFalse(src.Contains("SetTargetAgent("), "SetTargetAgent(null) leaves the native flag set");
    }

    [TestMethod]
    public void TheBehavior_TouchesOnlyActiveAiAgents_OnTheMissionTick()
    {
        var src = Squashed();

        StringAssert.Contains(src, "public override void OnMissionTick(float dt)");
        StringAssert.Contains(src, "!agent.IsActive() || !agent.IsAIControlled");
        StringAssert.Contains(src, "DeadTargetSweep.IsStale(target.State)");
    }

    [TestMethod]
    public void CoopRole_IsAgentOwner_ByTheAiControllerInvariant()
    {
        // A puppet is Controller None, so the AI-controller gate keeps it out (docs/features/coop-interop.md).
        Assert.AreEqual(CoopMissionRole.AgentOwner, CoopMissionRoles.All[nameof(DeadTargetResetMissionBehavior)]);
    }
}
