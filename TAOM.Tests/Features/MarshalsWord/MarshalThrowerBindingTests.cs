using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The metadata half of the thrower patches' binding tests (build-spec 9.4). Harmony binds by NAME and the postfix target is private, so these pin both targets and
/// every parameter name. They read metadata only and have no per-test setup, so they also run on the reference assemblies; the engage-point check, which runs
/// engine code, stays in <see cref="MarshalThrowerPatchTests"/>.
/// </summary>
[TestClass]
public class MarshalThrowerBindingTests
{
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePostfixTarget_IsThePrivateInstanceGetPositionAux_WithThePinnedParameterNames()
    {
        var target = AccessTools.Method(typeof(MovementOrder), "GetPositionAux", new[] { typeof(Formation), typeof(WorldPosition.WorldPositionEnforcedCache) });

        Assert.IsNotNull(target, "MovementOrder.GetPositionAux (private, v1.5.6 MovementOrder.cs:1133)");
        Assert.IsTrue(target.IsPrivate && !target.IsStatic);
        Assert.AreEqual(typeof(WorldPosition), target.ReturnType);
        CollectionAssert.AreEqual(new[] { "f", "worldPositionEnforcedCache" }, target.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(typeof(MovementOrder).IsValueType);
        var attribute = typeof(MarshalThrowerPatch).GetCustomAttributes<HarmonyPatch>().Single().info;
        Assert.AreEqual("GetPositionAux", attribute.methodName);
        CollectionAssert.AreEqual(new[] { typeof(Formation), typeof(WorldPosition.WorldPositionEnforcedCache) }, attribute.argumentTypes);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePostfix_NamesItsParametersByTheEnginesNames_AndTakesTheStructByRef()
    {
        var postfix = typeof(MarshalThrowerPatch).GetMethod("Postfix")!;

        CollectionAssert.AreEqual(new[] { "__instance", "f", "__result" }, postfix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(postfix.GetParameters()[0].ParameterType.IsByRef, "a struct's instance is injected by reference");
        Assert.IsTrue(postfix.GetParameters()[2].ParameterType.IsByRef, "the result is rewritten in place");
        Assert.AreEqual(typeof(Formation), postfix.GetParameters()[1].ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTimerTarget_IsThePublicCreateNewOrderWorldPositionMT_AndItsPatchNamesEveryParameterByTheEnginesNames()
    {
        var target = AccessTools.Method(typeof(MovementOrder), nameof(MovementOrder.CreateNewOrderWorldPositionMT));

        Assert.IsNotNull(target);
        Assert.IsTrue(target.IsPublic);
        CollectionAssert.AreEqual(new[] { "f", "worldPositionEnforcedCache" }, target.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual("CreateNewOrderWorldPositionMT", typeof(MarshalThrowerTimer).GetCustomAttributes<HarmonyPatch>().Single().info.methodName);

        var prefix = typeof(MarshalThrowerTimer).GetMethod("Prefix")!;
        var postfix = typeof(MarshalThrowerTimer).GetMethod("Postfix")!;
        CollectionAssert.AreEqual(new[] { "__instance", "__state" }, prefix.GetParameters().Select(p => p.Name).ToArray());
        Assert.IsTrue(prefix.GetParameters()[0].ParameterType.IsByRef && !prefix.GetParameters()[0].IsOut, "the order is read, as the postfix reads it");
        Assert.IsTrue(prefix.GetParameters()[1].IsOut);
        CollectionAssert.AreEqual(new[] { "__instance", "f", "__result", "__state" }, postfix.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(void), prefix.ReturnType, "a void prefix cannot skip the original, so the co-op veto test has nothing to classify");
        Assert.AreEqual(typeof(long), postfix.GetParameters()[3].ParameterType, "the state the prefix wrote");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEngineMembers_TheThrowerPatchesReadAreStillThere()
    {
        Assert.AreEqual(typeof(MovementOrder.MovementOrderEnum), typeof(MovementOrder).GetField("OrderEnum")!.FieldType);
        Assert.IsTrue(typeof(MovementOrder).GetField("OrderEnum")!.IsInitOnly);
        Assert.AreEqual(typeof(void), typeof(WorldPosition).GetMethod("SetVec2MT")!.ReturnType);
        Assert.IsNotNull(typeof(WorldPosition).GetProperty("IsValid"));
        Assert.IsNotNull(typeof(WorldPosition).GetProperty("X"));
        Assert.IsNotNull(typeof(WorldPosition).GetProperty("Y"));
        Assert.AreEqual(typeof(MissionMode), typeof(Mission).GetProperty("Mode")!.PropertyType);
        Assert.AreEqual(10, (int)MovementOrder.MovementOrderEnum.Advance, "pin the numeric value, read from MovementOrder.cs:12-24");
        Assert.AreEqual(11, (int)MovementOrder.MovementOrderEnum.FallBack);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BothTargets_StayShielded_BecauseTheShieldIsCheapAtTheirRate()
    {
        foreach (var name in new[] { "GetPositionAux", nameof(MovementOrder.CreateNewOrderWorldPositionMT) })
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetMethod(typeof(MovementOrder).FullName, name), name);
    }
}
