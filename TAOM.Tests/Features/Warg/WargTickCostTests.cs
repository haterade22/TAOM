using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.Warg;
using TAOM.Features.Warg.BehaviorTreeElements;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Warg;

/// <summary>
/// A warg tree's root runs on every mission tick (the tree is built with a 10 ms delay and
/// BehaviorTreeAgentComponent compares it in whole seconds), so whatever a node's Evaluate or
/// Execute calls, it calls per frame per engaged warg. Three rules, pinned in the IL and by
/// reflection (plan 015): a per-tick method never reaches IoC.Resolve, directly or through a
/// method of its own type; a grid scan fills a buffer through the three-argument SpatialGrid
/// overload and never constructs a List&lt;Agent&gt; per call; and a node keeps its services and
/// buffers in instance fields, never static ones.
/// </summary>
[TestClass]
public class WargTickCostTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    /// <summary>A same-type helper whose locals name a type from TaleWorlds.MountAndBlade.View (the
    /// base class of AutonomousMovementPlayerController, used by
    /// WargRiderHandManager.UpdateWargRiderHandle) can only have its body read once that assembly
    /// resolves. It lives in the game's Modules\Native\bin folder, which GameAssemblies adds.</summary>
    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static IEnumerable<MethodBase> CallsIn(MethodBase method)
    {
        byte[] il = method.GetMethodBody()?.GetILAsByteArray();
        return il == null ? Enumerable.Empty<MethodBase>() : IlCallScanner.ExtractCalledMethods(method, il);
    }

    /// <summary>The method's own calls plus the calls of each same-type method it calls: a static
    /// getter such as the old AdapterFactory property hides its Resolve one level down. A body that
    /// cannot be read fails the test (or is inconclusive with no game install), never passes it.</summary>
    private static List<MethodBase> CallsReachedFrom(Type type, string methodName)
    {
        MethodInfo method = type.GetMethod(methodName, Declared);
        Assert.IsNotNull(method, $"{type.Name}.{methodName} not found");
        List<MethodBase> direct = ReadCalls(method);
        IEnumerable<MethodBase> oneDown = direct.Where(m => m.DeclaringType == type).SelectMany(ReadCalls);
        return direct.Concat(oneDown).ToList();
    }

    private static List<MethodBase> ReadCalls(MethodBase method)
    {
        try
        {
            return CallsIn(method).ToList();
        }
        catch (System.IO.FileNotFoundException ex) when (!_gameLoaded)
        {
            Assert.Inconclusive($"{method.DeclaringType?.Name}.{method.Name} needs a game assembly to read its IL ({ex.FileName}); no game install resolved.");
            throw;
        }
    }

    private static List<string> ResolvesReachedFrom(Type type, string methodName) =>
        CallsReachedFrom(type, methodName)
            .Where(m => m.DeclaringType == typeof(global::TAOM.IoC) && m.Name == "Resolve")
            .Select(m => m.ToString())
            .ToList();

    private static void AssertNeverResolves(Type type, string methodName)
    {
        List<string> resolves = ResolvesReachedFrom(type, methodName);
        Assert.AreEqual(0, resolves.Count,
            $"{type.Name}.{methodName} runs every tick; use a service resolved once, not IoC.Resolve: {string.Join(", ", resolves)}");
    }

    private static bool ConstructsAnAgentList(IEnumerable<MethodBase> calls) =>
        calls.Any(m => m is ConstructorInfo && m.DeclaringType == typeof(List<Agent>));

    private static void AssertScansIntoABuffer(Type type)
    {
        List<MethodBase> calls = CallsReachedFrom(type, "Evaluate");
        List<MethodBase> scans = calls
            .Where(m => m.DeclaringType == typeof(SpatialGrid) && m.Name == nameof(SpatialGrid.GetNearAliveAgentsInRange))
            .ToList();
        Assert.IsTrue(scans.Count > 0, $"{type.Name}.Evaluate no longer scans the grid; this test needs re-planning");
        Assert.IsTrue(scans.All(m => m.GetParameters().Length == 3),
            $"{type.Name}.Evaluate calls an allocating GetNearAliveAgentsInRange overload; pass a reused List<Agent> buffer");
        Assert.IsFalse(ConstructsAnAgentList(calls),
            $"{type.Name}.Evaluate constructs a List<Agent> on every call; scan into a readonly instance buffer");
    }

    private static readonly Type[] TreeNodes =
    {
        typeof(PeriodicallyCheckIfCanAttackAnyone), typeof(CheckOnceIfCanAttackEnemy),
        typeof(WargAiControlledIsNotFacingEnemy), typeof(WargAttackTask), typeof(NoEnemyCloseDecorator),
    };

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

    [TestMethod]
    public void TreeNodes_StaticFields_HoldNoServiceOrScanBuffer()
    {
        // A static service would outlive IoC.Dispose on a module reload; a static buffer would be
        // shared by every warg's tree. Field initializers of a static run in the type initializer,
        // which no IL scan above reaches, so this is checked by reflection.
        List<string> offenders = TreeNodes
            .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(f => f.FieldType.IsInterface || f.FieldType == typeof(List<Agent>))
            .Select(f => $"{f.DeclaringType.Name}.{f.Name}")
            .ToList();
        Assert.AreEqual(0, offenders.Count, $"static service or buffer fields: {string.Join(", ", offenders)}");
    }

    // Controls: fixtures whose IL the rules above must reject, so a scanner that stops seeing a
    // call cannot turn every rule into a vacuous pass. Their methods are never executed.
    private sealed class ResolvesInAHelper
    {
        public void Tick() => Helper();
        private void Helper() => global::TAOM.IoC.Resolve<IModLogger>();
    }

    private sealed class ScansIntoAFreshList
    {
        public bool Evaluate()
        {
            SpatialGrid.Instance.GetNearAliveAgentsInRange(60, null, new List<Agent>());
            return false;
        }
    }

    [TestMethod]
    public void ResolveCheck_ResolveInASameTypeHelper_IsFound()
        => Assert.AreEqual(1, ResolvesReachedFrom(typeof(ResolvesInAHelper), "Tick").Count);

    [TestMethod]
    public void BufferCheck_ThreeArgumentScanIntoAFreshList_IsFound()
        => Assert.IsTrue(ConstructsAnAgentList(CallsReachedFrom(typeof(ScansIntoAFreshList), "Evaluate")));
}
