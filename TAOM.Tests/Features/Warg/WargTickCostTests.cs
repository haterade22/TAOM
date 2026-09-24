using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Warg;
using TAOM.Features.Warg.BehaviorTreeElements;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// A warg tree's root runs on every mission tick (the tree is built with a 10 ms delay and
/// BehaviorTreeAgentComponent compares it in whole seconds), so whatever a node's Evaluate or
/// Execute calls, it calls per frame per engaged warg. Two rules, pinned in the IL (plan 015): a
/// per-tick method never reaches IoC.Resolve, directly or through a getter of its own type; and a
/// grid scan fills a reused buffer through the three-argument SpatialGrid overload instead of
/// allocating a list per call.
/// </summary>
[TestClass]
public class WargTickCostTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static IEnumerable<MethodBase> CallsIn(MethodBase method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray();
        return il == null ? Enumerable.Empty<MethodBase>() : IlCallScanner.ExtractCalledMethods(method, il);
    }

    /// <summary>The method's own calls plus the calls of each same-type method it calls: a static
    /// getter such as the old AdapterFactory property hides its Resolve one level down.</summary>
    private static List<MethodBase> CallsReachedFrom(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, Declared);
        Assert.IsNotNull(method, $"{type.Name}.{methodName} not found");
        List<MethodBase> direct = CallsIn(method).ToList();
        IEnumerable<MethodBase> oneDown = direct.Where(m => m.DeclaringType == type).SelectMany(CallsInIfLoadable);
        return direct.Concat(oneDown).ToList();
    }

    /// <summary>A same-type helper whose locals name a type from TaleWorlds.MountAndBlade.View (for
    /// example WargRiderHandManager.UpdateWargRiderHandle and its AutonomousMovementPlayerController)
    /// cannot have its body read here: that assembly lives in the game's Modules folder, not the bin
    /// folder the tests reference. Such a helper is skipped; the method's own IL is always scanned.</summary>
    private static IEnumerable<MethodBase> CallsInIfLoadable(MethodBase method)
    {
        try
        {
            return CallsIn(method).ToList();
        }
        catch (System.IO.FileNotFoundException)
        {
            return Enumerable.Empty<MethodBase>();
        }
    }

    private static void AssertNeverResolves(Type type, string methodName)
    {
        List<string> resolves = CallsReachedFrom(type, methodName)
            .Where(m => m.DeclaringType == typeof(global::TAOM.IoC) && m.Name == "Resolve")
            .Select(m => m.ToString())
            .ToList();
        Assert.AreEqual(0, resolves.Count,
            $"{type.Name}.{methodName} runs every tick; use a service resolved once, not IoC.Resolve: {string.Join(", ", resolves)}");
    }

    private static void AssertScansIntoABuffer(Type type)
    {
        List<MethodBase> scans = CallsReachedFrom(type, "Evaluate")
            .Where(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.GetNearAliveAgentsInRange))
            .ToList();
        Assert.IsTrue(scans.Count > 0, $"{type.Name}.Evaluate no longer scans the grid; this test needs re-planning");
        Assert.IsTrue(scans.All(m => m.GetParameters().Length == 3),
            $"{type.Name}.Evaluate calls an allocating GetNearAliveAgentsInRange overload; pass a reused List<Agent> buffer");
    }

    [TestMethod]
    public void PeriodicallyCheckIfCanAttackAnyone_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(PeriodicallyCheckIfCanAttackAnyone), "Evaluate");

    [TestMethod]
    public void CheckOnceIfCanAttackEnemy_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(CheckOnceIfCanAttackEnemy), "Evaluate");

    [TestMethod]
    public void WargAiControlledIsNotFacingEnemy_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(WargAiControlledIsNotFacingEnemy), "Evaluate");

    [TestMethod]
    public void WargAttackTask_Execute_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(WargAttackTask), "Execute");

    [TestMethod]
    public void WargRiderHandManager_Tick_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(WargRiderHandManager), "Tick");

    [TestMethod]
    public void NoEnemyCloseDecorator_Evaluate_NeverResolvesFromIoC()
        => AssertNeverResolves(typeof(NoEnemyCloseDecorator), "Evaluate");

    [TestMethod]
    public void NoEnemyCloseDecorator_Evaluate_ScansIntoAReusedBuffer()
        => AssertScansIntoABuffer(typeof(NoEnemyCloseDecorator));

    [TestMethod]
    public void PeriodicallyCheckIfCanAttackAnyone_Evaluate_ScansIntoAReusedBuffer()
        => AssertScansIntoABuffer(typeof(PeriodicallyCheckIfCanAttackAnyone));

    [TestMethod]
    public void CheckOnceIfCanAttackEnemy_Evaluate_ScansIntoAReusedBuffer()
        => AssertScansIntoABuffer(typeof(CheckOnceIfCanAttackEnemy));
}
