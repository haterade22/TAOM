using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Client;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Patch140's two patches (build-spec 5.8): a patch needs its target attribute, its category and an apply line, and Harmony injects
/// by NAME, so a renamed engine parameter or field would leave a patch applied and doing nothing. Plus the postfix's arithmetic.
/// The engine types sit in TaleWorlds.MountAndBlade.View, which the test project does not reference, so they are found by name.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SeatPatchWiringTests
{
    private const string OrderFlagName = "TaleWorlds.MountAndBlade.View.MissionViews.Order.OrderFlag";
    private const string MissionScreenName = "TaleWorlds.MountAndBlade.View.Screens.MissionScreen";

    private static readonly Type[] PatchClasses =
    {
        typeof(Patch140_OrderFlagGetCollidedEntity),
        typeof(Patch140_MissionScreenScreenPointToWorldRay),
    };

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    private static readonly FieldInfo ContainerField = typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly FieldInfo ReportedField = typeof(Patch140_OrderFlagGetCollidedEntity).GetField("_reported", BindingFlags.NonPublic | BindingFlags.Static)!;

    private object? _savedContainer;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        Assert.IsNotNull(ReportedField, "Patch140_OrderFlagGetCollidedEntity._reported (the log-once latch) moved: update this test");
        _savedContainer = ContainerField.GetValue(null);
        ReportedField.SetValue(null, 0);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        ContainerField.SetValue(null, _savedContainer);
        ReportedField.SetValue(null, 0);
    }

    private static Type Engine(string fullName)
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName, throwOnError: false)).FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, fullName + " is not in the installed engine");
        return type!;
    }

    private static HarmonyMethod TargetOf(Type patch) => patch.GetCustomAttributes<HarmonyPatch>(inherit: false).Single().info;

    [TestMethod]
    public void EveryPatchClass_HasOneTargetAndTheCategory()
    {
        foreach (var patch in PatchClasses)
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length, patch.Name + " lost its [HarmonyPatch].");
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.Contains(categories, SeatOfSeeingModule.PatchCategory, patch.Name + " lost its [HarmonyPatchCategory].");
            Assert.AreEqual(typeof(TaomClientComposition).Assembly, patch.Assembly, patch.Name + " names a client-only type, so it lives in TAOM.Client.");
        }
    }

    [TestMethod]
    public void Index_FromTheLoadedTaomAssemblies_HoldsTheTwoClientClasses()
    {
        Engine(MissionScreenName);
        var previous = ClientComposition.Current;
        ClientComposition.Current = new TaomClientComposition();
        try
        {
            var index = PatchCategoryIndex.Build(ClientComposition.LoadedTaomAssemblies());

            Assert.AreEqual(0, index.SkippedClasses.Count, string.Join("; ", index.SkippedClasses.Select(k => k.Key.FullName)));
            CollectionAssert.AreEquivalent(PatchClasses, index.ClassesOf(SeatOfSeeingModule.PatchCategory).ToArray());
        }
        finally
        {
            ClientComposition.Current = previous;
        }
    }

    [TestMethod]
    public void OrderFlagTarget_IsThePrivateGetCollidedEntity_AndTheInjectedNamesAreTheEngines()
    {
        var orderFlag = Engine(OrderFlagName);
        var info = TargetOf(typeof(Patch140_OrderFlagGetCollidedEntity));
        var target = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);

        Assert.AreEqual(orderFlag, info.declaringType);
        Assert.IsNotNull(target, "OrderFlag.GetCollidedEntity did not resolve (v1.5.6 OrderFlag.cs:335)");
        Assert.IsTrue(target!.IsPrivate);
        Assert.AreEqual(typeof(WeakGameEntity), target.ReturnType);
        CollectionAssert.AreEqual(new[] { "closestPoint" }, target.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(target.GetParameters()[0].IsOut);

        var prefix = typeof(Patch140_OrderFlagGetCollidedEntity).GetMethod("Prefix")!;
        var parameters = prefix.GetParameters();
        CollectionAssert.AreEqual(new[] { "__result", "closestPoint", "____missionScreen", "____mission" },
            parameters.Select(p => p.Name).ToArray(), "Harmony binds by name");
        Assert.AreEqual(typeof(Vec3).MakeByRefType(), parameters[1].ParameterType);

        // Four underscores: Harmony strips three, and the engine's own fields keep their leading one.
        Assert.AreEqual(Engine(MissionScreenName), AccessTools.Field(orderFlag, "_missionScreen")?.FieldType);
        Assert.AreEqual(typeof(Mission), AccessTools.Field(orderFlag, "_mission")?.FieldType);
        Assert.AreEqual(Engine(MissionScreenName), parameters[2].ParameterType);
        Assert.AreEqual(typeof(Mission), parameters[3].ParameterType);
    }

    [TestMethod]
    public void RayPostfixTarget_IsScreenPointToWorldRay_AndTheInjectedNamesAreTheEngines()
    {
        var info = TargetOf(typeof(Patch140_MissionScreenScreenPointToWorldRay));
        var target = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);

        Assert.AreEqual(Engine(MissionScreenName), info.declaringType);
        Assert.IsNotNull(target, "MissionScreen.ScreenPointToWorldRay did not resolve (v1.5.6 MissionScreen.cs:3502)");
        CollectionAssert.AreEqual(new[] { "screenPoint", "rayBegin", "rayEnd" }, target!.GetParameters().Select(p => p.Name).ToArray());

        var postfix = typeof(Patch140_MissionScreenScreenPointToWorldRay).GetMethod("Postfix")!;
        var parameters = postfix.GetParameters();
        CollectionAssert.AreEqual(new[] { "rayBegin", "rayEnd" }, parameters.Select(p => p.Name).ToArray(), "Harmony binds by name");
        Assert.IsFalse(parameters[0].ParameterType.IsByRef, "the ray's start is read, not written");
        Assert.IsTrue(parameters[1].ParameterType.IsByRef, "the ray's end is rewritten");
        foreach (var parameter in parameters)
            Assert.AreEqual(typeof(Vec3), target.GetParameters().Single(p => p.Name == parameter.Name).ParameterType.GetElementType());
    }

    // ---- The prefix: vanilla runs whole off the Seat, and a throw seated answers "no entity, no point" ----

    // By reflection: the prefix names MissionScreen, which the test project does not reference. The two engine arguments are null.
    private static (bool Proceed, WeakGameEntity Entity, Vec3 Point) RunPrefix(WeakGameEntity entity, Vec3 point)
    {
        var args = new object?[] { entity, point, null, null };
        var proceed = (bool)typeof(Patch140_OrderFlagGetCollidedEntity).GetMethod("Prefix")!.Invoke(null, args)!;
        return (proceed, (WeakGameEntity)args[0]!, (Vec3)args[1]!);
    }

    [TestMethod]
    public void Prefix_WhileOff_ReturnsTrue_AndLeavesTheResult()
    {
        SeatPresence.Set(false);
        var point = new Vec3(1f, 2f, 3f, -1f);

        var (proceed, entity, closest) = RunPrefix(WeakGameEntity.Invalid, point);

        Assert.IsTrue(proceed, "vanilla's GetCollidedEntity runs whole");
        Assert.IsFalse(entity.IsValid);
        Assert.AreEqual(point, closest);
    }

    [TestMethod]
    public void Prefix_WhenSeatedAndTheRayThrows_ReturnsFalse_WithNoEntityAndNoPoint_AndLogsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger);
        ContainerField.SetValue(null, container);
        SeatPresence.Set(true);

        var (proceed, entity, closest) = RunPrefix(WeakGameEntity.Invalid, new Vec3(1f, 2f, 3f, -1f));
        RunPrefix(WeakGameEntity.Invalid, new Vec3(1f, 2f, 3f, -1f));

        Assert.IsFalse(proceed, "the replaced method must not run vanilla's body, which throws with no main agent");
        Assert.IsFalse(entity.IsValid);
        Assert.IsFalse(closest.IsValid);
        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s => s.StartsWith("[SeatOfSeeing] order ray failed", StringComparison.Ordinal)));
    }

    // ---- The postfix: a no-op off the Seat, and a 1000 m ray on it ----

    private static Vec3 Extend(Vec3 begin, Vec3 end)
    {
        Patch140_MissionScreenScreenPointToWorldRay.Postfix(begin, ref end);
        return end;
    }

    [TestMethod]
    public void Postfix_WhileOff_LeavesTheRayAlone()
    {
        SeatPresence.Set(false);
        var end = new Vec3(0f, 30f, 0f, -1f);

        Assert.AreEqual(end, Extend(new Vec3(0f, 0f, 0f, -1f), end));
    }

    [TestMethod]
    public void Postfix_WhileSeated_ExtendsAShortRayToAThousandMetresAlongTheSameLine()
    {
        SeatPresence.Set(true);

        var end = Extend(new Vec3(10f, 10f, 50f, -1f), new Vec3(10f, 40f, 50f, -1f));

        Assert.AreEqual(10f, end.x, 0.001f);
        Assert.AreEqual(10f + 1000f, end.y, 0.01f);
        Assert.AreEqual(50f, end.z, 0.001f);
    }

    [TestMethod]
    public void Postfix_WhileSeated_KeepsTheDirectionOfADownwardRay()
    {
        SeatPresence.Set(true);

        var begin = new Vec3(0f, 0f, 100f, -1f);
        var end = Extend(begin, new Vec3(0f, 3f, 96f, -1f));

        var ray = end - begin;
        Assert.AreEqual(1000f, ray.Length, 0.05f);
        Assert.AreEqual(0.6f, ray.y / 1000f, 0.001f);
        Assert.AreEqual(-0.8f, ray.z / 1000f, 0.001f);
    }

    [TestMethod]
    public void Postfix_WhileSeated_DoesNotShortenALongerRay()
    {
        SeatPresence.Set(true);
        var end = new Vec3(0f, 4000f, 0f, -1f);

        Assert.AreEqual(end, Extend(new Vec3(0f, 0f, 0f, -1f), end));
    }

    [TestMethod]
    public void Postfix_WhileSeated_IgnoresAZeroLengthRay()
    {
        SeatPresence.Set(true);
        var point = new Vec3(5f, 5f, 5f, -1f);

        Assert.AreEqual(point, Extend(point, point));
    }
}
