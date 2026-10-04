using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.AdvancedCombat.Services;
using TAOM.Tests.Migration;

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

    [TestMethod]
    public void RenderDebugVisualization_ScansIntoAReusedBuffer()
    {
        MethodInfo method = typeof(SpatialGridDebugService).GetMethod(nameof(SpatialGridDebugService.RenderDebugVisualization));
        List<MethodBase> calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();
        List<MethodBase> scans = calls
            .Where(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.GetNearAliveAgentsInRange))
            .ToList();
        // ExtractCalledMethods skips a token it cannot resolve, so an empty list must fail, never pass.
        Assert.IsTrue(scans.Count > 0, "RenderDebugVisualization no longer scans the grid; this test needs re-planning");
        Assert.IsTrue(scans.All(m => m.GetParameters().Length == 3),
            "RenderDebugVisualization calls an allocating GetNearAliveAgentsInRange overload; pass a reused List<Agent> buffer");
        Assert.IsFalse(calls.Any(m => m is ConstructorInfo && m.DeclaringType == typeof(List<Agent>)),
            "RenderDebugVisualization constructs a List<Agent> on every call; scan into a readonly instance buffer");
    }

    [TestMethod]
    public void RenderDebugVisualization_EmptiesTheBufferAfterTheScan()
    {
        // The service is a process-lifetime IoC singleton: a buffer left full keeps the last overlay frame's agents,
        // and through them their finished mission, reachable into later battles.
        MethodInfo method = typeof(SpatialGridDebugService).GetMethod(nameof(SpatialGridDebugService.RenderDebugVisualization));
        List<MethodBase> calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody().GetILAsByteArray()).ToList();

        int scan = calls.FindIndex(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.GetNearAliveAgentsInRange));
        int clear = calls.FindLastIndex(m => m.DeclaringType == typeof(List<Agent>) && m.Name == nameof(List<Agent>.Clear));

        Assert.IsTrue(scan >= 0, "RenderDebugVisualization no longer scans the grid; this test needs re-planning");
        Assert.IsTrue(clear > scan, "RenderDebugVisualization leaves its singleton buffer full; clear it after the loop");
    }
}
