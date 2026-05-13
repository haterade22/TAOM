using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat.Services;

namespace TAOM.Tests.Features.AdvancedCombat;

// ADR-008 minimum-coverage for SpatialGridDebugService.
//
// The audit issue (#185) flagged RenderDebugVisualization as untested AND claimed the "consumption
// path unknown". Consumption is actually clear: AdvancedCombatBehavior.OnMissionTick calls
// _debugService.RenderDebugVisualization() every 2 seconds (throttled by GridUpdateInterval).
//
// The METHOD body, however, is 100% engine-coupled — it reads Agent.Main, Input.IsKeyDown,
// SpatialGrid.Instance, and calls MBDebug.RenderDebugSphere, all sealed engine statics with no
// adapter wrapping today. Full behavior tests would need an ADR-007 refactor introducing
// IAgentSourceAdapter, IInputAdapter, ISpatialGridAdapter, and IDebugRendererAdapter. That's
// outside the scope #185 specified.
//
// What we CAN test without engine state is below; deferral noted in the issue close comment.
[TestClass]
public class SpatialGridDebugServiceTests
{
    [TestMethod]
    public void Constructs_NoDependenciesRequired()
    {
        // The service is parameterless — verifies the IoC Singleton registration succeeds without
        // throwing. (DryIoc lazy-init in the AdvancedCombatBehavior ctor relies on this.)
        var sut = new SpatialGridDebugService();
        Assert.IsNotNull(sut);
    }

    [TestMethod]
    public void ImplementsInterface()
    {
        // Protects the IoC.ResolveAll<>/Resolve<>() consumer in AdvancedCombatBehavior. A future
        // rename or interface drift breaks the resolve at startup; this test catches it at build
        // time.
        var sut = new SpatialGridDebugService();
        Assert.IsInstanceOfType(sut, typeof(ISpatialGridDebugService));
    }
}
