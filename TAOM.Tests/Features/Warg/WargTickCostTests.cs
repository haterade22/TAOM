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
/// Execute calls, it calls per frame per engaged warg. Five rules, pinned in the IL and by
/// reflection (plan 015, #659): a per-tick method never reaches IoC.Resolve or IoC.ResolveAll,
/// directly or through a method of its own type; no body of the five nodes in TreeNodes reaches
/// either at all (constructors and field initializers included); WargBehaviorTree.BuildTree resolves each
/// node service exactly once per tree; a grid scan fills a buffer through the three-argument
/// SpatialGrid overload and never constructs a List&lt;Agent&gt; per call; and a node keeps its
/// services and buffers in instance fields, never static ones.
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
            .Where(IsResolve)
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

    /// <summary>Every body a type declares: methods, accessors, instance constructors (where field
    /// initializers compile), the type initializer, and the same for its nested types (lambdas).</summary>
    private static IEnumerable<MethodBase> DeclaredBodies(Type type) =>
        type.GetMethods(Declared).Cast<MethodBase>()
            .Concat(type.GetConstructors(Declared))
            .Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic).SelectMany(DeclaredBodies))
            .Distinct();

    /// <summary>A container lookup: IoC.Resolve or IoC.ResolveAll.</summary>
    private static bool IsResolve(MethodBase m) =>
        m.DeclaringType == typeof(global::TAOM.IoC) && (m.Name == "Resolve" || m.Name == "ResolveAll");

    private static List<string> ResolvesAnywhereIn(Type type) =>
        DeclaredBodies(type)
            .SelectMany(body => ReadCalls(body).Where(IsResolve).Select(m => $"{type.Name}.{body.Name}: {m}"))
            .ToList();

    // The nodes that need a service take it from WargBehaviorTree.BuildTree, which resolves each
    // service once per tree (maintainer decision 2026-09-24, #659), so none of these five nodes is
    // a service locator. (LogTask, shared from BaseBehaviorTree, still resolves its logger per
    // Execute; it is not a warg node and is not scanned here.)
    [TestMethod]
    public void TreeNodes_ConstructorsAndMembers_NeverResolveFromIoC()
    {
        List<string> resolves = TreeNodes.SelectMany(ResolvesAnywhereIn).ToList();
        Assert.AreEqual(0, resolves.Count,
            $"a warg tree node takes its services through its constructor from WargBehaviorTree, never from IoC: {string.Join("; ", resolves)}");
    }

    [TestMethod]
    public void WargBehaviorTree_BuildTree_ResolvesEachServiceOncePerTree()
    {
        List<string> resolved = ReadCalls(typeof(WargBehaviorTree).GetMethod(nameof(WargBehaviorTree.BuildTree), Declared))
            .Where(IsResolve)
            .Select(m => ((MethodInfo)m).GetGenericArguments()[0].Name)
            .OrderBy(n => n)
            .ToList();
        CollectionAssert.AreEqual(new[] { "IMissionAdapterFactory", "IWargAttackService" }, resolved,
            $"BuildTree resolves: {string.Join(", ", resolved)}");
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

    private sealed class ResolvesInAFieldInitializer
    {
        private readonly IModLogger _logger = global::TAOM.IoC.Resolve<IModLogger>();
        public IModLogger Logger => _logger;
    }

    [TestMethod]
    public void ResolveCheck_ResolveInAFieldInitializer_IsFound()
        => Assert.AreEqual(1, ResolvesAnywhereIn(typeof(ResolvesInAFieldInitializer)).Count);

    private sealed class ResolvesAllInAMethod
    {
        public void Tick() => global::TAOM.IoC.ResolveAll<IModLogger>();
    }

    [TestMethod]
    public void ResolveCheck_ResolveAllInAPerTickMethod_IsFound()
        => Assert.AreEqual(1, ResolvesReachedFrom(typeof(ResolvesAllInAMethod), "Tick").Count);

    [TestMethod]
    public void ResolveCheck_ResolveAllInAConstructorOrMember_IsFound()
        => Assert.AreEqual(1, ResolvesAnywhereIn(typeof(ResolvesAllInAMethod)).Count);

    [TestMethod]
    public void BufferCheck_ThreeArgumentScanIntoAFreshList_IsFound()
        => Assert.IsTrue(ConstructsAnAgentList(CallsReachedFrom(typeof(ScansIntoAFreshList), "Evaluate")));
}
