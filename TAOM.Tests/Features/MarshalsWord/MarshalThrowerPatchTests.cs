using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The two thrower patches (build-spec 9.4) on <c>MovementOrder</c>. Harmony binds by NAME and the postfix target is private, so the binding tests pin both
/// targets and every parameter name; the behaviour tests drive each patch with identity-only engine objects.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalThrowerPatchTests
{
    private static readonly Type[] PatchClasses = { typeof(MarshalThrowerPatch), typeof(MarshalThrowerTimer) };

    [TestInitialize]
    public void Setup() => Reset();

    [TestCleanup]
    public void Cleanup() => Reset();

    private static void Reset()
    {
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Clear();
        MarshalThrowerCost.Reset();
        SetCurrentMission(null);
    }

    private static T Blank<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void SetCurrentMission(Mission? mission) =>
        typeof(Mission).GetProperty("Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, mission);

    private static WorldPosition ValidAt(float x, float y)
    {
        object boxed = TestPositions.At(x, y, 0f);
        typeof(WorldPosition).GetField("_scene", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(boxed, new UIntPtr(1));
        return (WorldPosition)boxed;
    }

    private static Formation NewFormation() => Blank<Formation>();

    private static void Publish(Formation formation, float x, float y) =>
        MarshalHooks.Throwers = new MarshalThrowerTable(new[] { formation }, new[] { x }, new[] { y });

    private static ref WorldPosition EngagePoint(ref MovementOrder order) =>
        ref AccessTools.StructFieldRefAccess<MovementOrder, WorldPosition>("_engageTargetPositionCache")(ref order);

    // The constructors read Mission.Current (MovementOrder.cs:175) and so does FieldInfo.SetValue (it runs the type initializer), so the order is a default value
    // whose kind is written through a generated field reference, which touches no static member.
    private static MovementOrder AdvanceOrder()
    {
        var order = default(MovementOrder);
        AccessTools.StructFieldRefAccess<MovementOrder, MovementOrder.MovementOrderEnum>("OrderEnum")(ref order) = MovementOrder.MovementOrderEnum.Advance;
        return order;
    }

    // MovementOrder.cs:1180-1189 and 1196 transcribed over the real field, without the pull-back of 1190-1195 (it needs a LineFormation).
    private static WorldPosition VanillaEngagePoint(ref MovementOrder order, Vec2 company, WorldPosition point, float reachSquared)
    {
        ref var cache = ref EngagePoint(ref order);
        if (!cache.IsValid)
            cache = point;
        if (cache.AsVec2.DistanceSquared(point.AsVec2) > company.DistanceSquared(cache.AsVec2) * 0.1f && point.AsVec2.DistanceSquared(company) <= reachSquared)
            cache = point;
        return cache;
    }

    // ---- wiring and bindings ----

    [TestMethod]
    public void BothClasses_JoinTheSharedDeferredCategory_BecauseTheirParametersCarryAMovementOrder()
    {
        foreach (var patch in PatchClasses)
        {
            var categories = patch.GetCustomAttributes<HarmonyPatchCategory>(inherit: false).ToList();
            Assert.AreEqual(1, categories.Count, patch.Name);
            Assert.AreEqual("Patch_MissionTime_SetMovementOrder", categories[0].info.category, patch.Name);
            Assert.AreEqual("TAOM.Features.MarshalsWord.Hooks", patch.Namespace);
        }
    }

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

    // ---- the postfix ----
    // MovementOrder's type initializer reads Mission.Current.CurrentTime (MovementOrder.cs:175, 1245-1253) and a throw there is cached for the process, so no test calls an
    // order's constructor, static member or method. A test that needs an Advance sets the kind on a boxed default value (AdvanceOrder).

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestMethod]
    public void Postfix_WithNoTable_ReturnsBeforeItReadsTheOrder_AndLeavesTheAnswerAlone()
    {
        var order = default(MovementOrder);
        var result = ValidAt(1f, 2f);

        MarshalThrowerPatch.Postfix(ref order, NewFormation(), ref result);

        Assert.AreEqual(1f, result.X);
        Assert.AreEqual(2f, result.Y);
        Assert.AreEqual(0, MarshalThrowerCost.Read().Moved);
    }

    [TestMethod]
    public void Postfix_ALatchedFault_ReturnsAtItsFirstTest_UntilTheNextBegin()
    {
        Publish(NewFormation(), 12f, 34f);
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("first"));
        var order = default(MovementOrder);
        var result = ValidAt(1f, 2f);

        MarshalThrowerPatch.Postfix(ref order, NewFormation(), ref result);

        Assert.AreEqual(1f, result.X);
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        Assert.IsNull(MarshalHooks.ThrowerFault, "a new mission starts with the patch available again");
    }

    [TestMethod]
    public void Stand_ACompanyInTheTable_GetsItsStandPoint_AndCountsTheMove()
    {
        var formation = NewFormation();
        Publish(formation, 12f, 34f);
        var result = ValidAt(1f, 2f);

        var moved = MarshalThrowerPatch.Stand(MarshalHooks.Throwers!, formation, ref result);

        Assert.IsTrue(moved, "only a moved answer is followed by the forget");
        Assert.AreEqual(12f, result.X);
        Assert.AreEqual(34f, result.Y);
        Assert.AreEqual(1, MarshalThrowerCost.Read().Moved);
    }

    [TestMethod]
    public void Stand_ACompanyNotInTheTable_IsLeftAlone()
    {
        Publish(NewFormation(), 12f, 34f);
        var result = ValidAt(1f, 2f);

        Assert.IsFalse(MarshalThrowerPatch.Stand(MarshalHooks.Throwers!, NewFormation(), ref result));
        Assert.IsFalse(MarshalThrowerPatch.Stand(MarshalHooks.Throwers!, null!, ref result));

        Assert.AreEqual(1f, result.X);
        Assert.AreEqual(0, MarshalThrowerCost.Read().Moved);
    }

    [TestMethod]
    public void Stand_AnAnswerThatIsNotValid_IsNeverWrittenTo()
    {
        var formation = NewFormation();
        Publish(formation, 12f, 34f);
        var result = TestPositions.At(1f, 2f, 0f);

        Assert.IsFalse(MarshalThrowerPatch.Stand(MarshalHooks.Throwers!, formation, ref result));

        Assert.AreEqual(1f, result.X, "a position with no scene would reach native with a null scene");
    }

    [TestMethod]
    public void Stand_InDeployment_YieldsToVanillas()
    {
        var formation = NewFormation();
        Publish(formation, 12f, 34f);
        var mission = Blank<Mission>();
        typeof(Mission).GetField("_missionMode", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(mission, MissionMode.Deployment);
        SetCurrentMission(mission);
        var result = ValidAt(1f, 2f);

        Assert.IsFalse(MarshalThrowerPatch.Stand(MarshalHooks.Throwers!, formation, ref result));

        Assert.AreEqual(1f, result.X, "MovementOrder.cs:1139-1142 answers the company's own order position in Deployment");
    }

    [TestMethod]
    public void Stand_ATableWhoseArraysAreShorterThanItsFormations_Throws_AndOnlyTheFirstFaultIsKept()
    {
        var formation = NewFormation();
        var table = new MarshalThrowerTable(new[] { formation }, Array.Empty<float>(), Array.Empty<float>());
        var result = ValidAt(1f, 2f);

        Assert.ThrowsException<IndexOutOfRangeException>(() => MarshalThrowerPatch.Stand(table, formation, ref result));

        var first = new InvalidOperationException("first");
        MarshalHooks.LatchThrowerFault(first);
        MarshalHooks.LatchThrowerFault(new InvalidOperationException("second"));
        Assert.AreSame(first, MarshalHooks.ThrowerFault);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEngagePoint_IsAPrivateWorldPositionFieldOfMovementOrder_AndForgetSetsItInvalid()
    {
        var field = typeof(MovementOrder).GetField("_engageTargetPositionCache", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.IsNotNull(field, "MovementOrder._engageTargetPositionCache (private, v1.5.6 MovementOrder.cs:75)");
        Assert.AreEqual(typeof(WorldPosition), field!.FieldType);
        Assert.IsFalse(field.IsInitOnly, "a readonly field cannot be set through a field reference");
        MarshalEngagePoint.Bind();
        var order = default(MovementOrder);
        EngagePoint(ref order) = ValidAt(5f, 98f);
        Assert.IsTrue(EngagePoint(ref order).IsValid);

        MarshalEngagePoint.Forget(ref order);

        Assert.IsFalse(EngagePoint(ref order).IsValid, "every constructor gives the field WorldPosition.Invalid (MovementOrder.cs:182, 202)");
    }

    [TestMethod]
    public void Postfix_AMovedAdvanceAnswer_ForgetsTheEngagePoint_AndAnAnswerLeftAloneKeepsIt()
    {
        MarshalEngagePoint.Bind();
        var order = AdvanceOrder();
        EngagePoint(ref order) = ValidAt(0f, 98f);
        var formation = NewFormation();
        Publish(formation, 12f, 34f);
        var result = ValidAt(1f, 2f);

        MarshalThrowerPatch.Postfix(ref order, NewFormation(), ref result);
        Assert.IsTrue(EngagePoint(ref order).IsValid, "a company not in the table is left alone, and so is vanilla's point");

        MarshalThrowerPatch.Postfix(ref order, formation, ref result);

        Assert.AreEqual(12f, result.X);
        Assert.IsFalse(EngagePoint(ref order).IsValid, "the answer was moved, so the point is forgotten");
        Assert.IsNull(MarshalHooks.ThrowerFault);
    }

    [TestMethod]
    public void AfterTheStand_AnEnemyThatDrewOff_IsTheFirstAnswerLeftAlone_BecauseEachMovedAnswerForgotTheOldPoint()
    {
        MarshalEngagePoint.Bind();
        var reach = 4f * 4f * 9f * 6f;
        var kept = AdvanceOrder();
        var forgotten = AdvanceOrder();
        VanillaEngagePoint(ref kept, new Vec2(0f, 0f), ValidAt(0f, 98f), reach);
        VanillaEngagePoint(ref forgotten, new Vec2(0f, 0f), ValidAt(0f, 98f), reach);
        MarshalEngagePoint.Forget(ref forgotten);

        // The enemy draws off 100 m while the company follows under the stand, 33 m short of it and so outside the reach (MovementOrder.cs:1184-1185).
        for (var i = 0; i < 3; i++)
        {
            VanillaEngagePoint(ref kept, new Vec2(0f, 165f), ValidAt(0f, 198f), reach);
            VanillaEngagePoint(ref forgotten, new Vec2(0f, 165f), ValidAt(0f, 198f), reach);
            MarshalEngagePoint.Forget(ref forgotten);
        }

        var releasedKept = VanillaEngagePoint(ref kept, new Vec2(0f, 165f), ValidAt(0f, 198f), reach);
        var releasedForgotten = VanillaEngagePoint(ref forgotten, new Vec2(0f, 165f), ValidAt(0f, 198f), reach);

        Assert.AreEqual(98f, releasedKept.Y, "without the forget, vanilla walks the company 67 m back");
        Assert.AreEqual(198f, releasedForgotten.Y);
    }

    [TestMethod]
    public void ThePostfix_TestsInTheContractsOrder_CatchesAndLatches_AndTakesNoLockAllocatesNothingAndLogsNothing()
    {
        var code = Regex.Replace(RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalThrowerPatch.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(code, "try { if (MarshalHooks.ThrowerFault != null) return; var table = MarshalHooks.Throwers; if (table == null || __instance.OrderEnum != MovementOrder.MovementOrderEnum.Advance) return; "
            + "if (Stand(table, f, ref __result)) MarshalEngagePoint.Forget(ref __instance); } catch (Exception ex) { MarshalHooks.LatchThrowerFault(ex); }");
        StringAssert.Contains(code, "if (Mission.Current?.Mode == MissionMode.Deployment) return false; var index = table.IndexOf(f); if (index < 0 || !result.IsValid) return false; "
            + "result.SetVec2MT(new Vec2(table.X[index], table.Y[index])); MarshalThrowerCost.AddMoved(); return true;");
        foreach (var banned in new[] { "lock", "Outcomes", "Log", "new List", "string", "Query", "Interlocked" })
            Assert.IsFalse(code.Contains(banned), "the postfix runs on the AI thread once per company a frame: " + banned);
    }

    [TestMethod]
    public void TheBind_LatchesTheRealExceptionOnTheMainThread_SoThePostfixMovesNothingAndTheNextPassReportsIt()
    {
        var flat = Regex.Replace(RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalEngagePoint.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(flat, "catch (Exception ex) { MarshalHooks.LatchThrowerFault(ex); }");
    }

    [TestMethod]
    public void TheForget_IsOneFieldWrite_AndTakesNoLockAllocatesNothingAndLogsNothing()
    {
        var code = RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalEngagePoint.cs", stripComments: true);
        var flat = Regex.Replace(code, @"\s+", " ");

        StringAssert.Contains(flat, "var cache = Volatile.Read(ref _cache) ?? throw new MissingFieldException(nameof(MovementOrder), FieldName); cache(ref order) = WorldPosition.Invalid;");
        foreach (var banned in new[] { "lock", "Log", "new List", "Interlocked" })
            Assert.IsFalse(code.Contains(banned), "the forget runs on the AI thread inside vanilla's own lock: " + banned);
    }

    // ---- the timer ----

    [TestMethod]
    public void TimerPrefix_WithNoTable_StoresZero_AndWithATable_StoresTheClockForAnAdvanceOnly()
    {
        var advance = AdvanceOrder();
        var charge = default(MovementOrder);
        MarshalThrowerTimer.Prefix(ref advance, out var idle);
        Publish(NewFormation(), 1f, 2f);
        MarshalThrowerTimer.Prefix(ref advance, out var timed);
        MarshalThrowerTimer.Prefix(ref charge, out var notAdvance);

        Assert.AreEqual(0L, idle);
        Assert.AreNotEqual(0L, timed);
        Assert.AreEqual(0L, notAdvance, "a call under another order is dropped by Record, so it is not timed at all");
    }

    [TestMethod]
    public void TimerPostfix_AStateOfZero_ReturnsBeforeItReadsTheOrder_AndAddsNothing()
    {
        var order = default(MovementOrder);

        MarshalThrowerTimer.Postfix(ref order, NewFormation(), default, 0L);

        Assert.AreEqual(0, MarshalThrowerCost.Read().Other.Calls + MarshalThrowerCost.Read().Published.Calls);
    }

    [TestMethod]
    public void TimerRecord_APublishedCompany_FillsThePublishedBucket_AndCountsAReplacedAnswer()
    {
        var formation = NewFormation();
        Publish(formation, 12f, 34f);

        MarshalThrowerTimer.Record(1L, true, formation, TestPositions.At(12f, 34f, 0f));
        MarshalThrowerTimer.Record(1L, true, formation, TestPositions.At(5f, 6f, 0f));

        var read = MarshalThrowerCost.Read();
        Assert.AreEqual(2, read.Published.Calls);
        Assert.AreEqual(1, read.Replaced, "the caller swapped one answer for an alternate");
        Assert.AreEqual(0, read.Other.Calls);
    }

    [TestMethod]
    public void TimerRecord_AnotherCompanysAdvance_FillsTheOtherBucket_AndAnotherOrderFillsNeither()
    {
        Publish(NewFormation(), 12f, 34f);

        MarshalThrowerTimer.Record(1L, true, NewFormation(), default);
        MarshalThrowerTimer.Record(1L, false, NewFormation(), default);

        var read = MarshalThrowerCost.Read();
        Assert.AreEqual(1, read.Other.Calls);
        Assert.AreEqual(0, read.Published.Calls);
    }

    [TestMethod]
    public void TimerRecord_APublishedCompanysCallUnderAnotherOrder_FillsNeitherBucket_AndCountsNoReplacedAnswer()
    {
        var formation = NewFormation();
        Publish(formation, 12f, 34f);

        MarshalThrowerTimer.Record(1L, false, formation, TestPositions.At(5f, 6f, 0f));

        var read = MarshalThrowerCost.Read();
        Assert.AreEqual(0, read.Published.Calls, "between an order change and the next pass the company is still in the table, but it is not on the stand");
        Assert.AreEqual(0, read.Other.Calls);
        Assert.AreEqual(0, read.Replaced);
    }

    [TestMethod]
    public void TimerRecord_ATableThatWentNullAfterThePrefix_AddsToTheOtherBucketOnAnAdvance_AndNeverThrows()
    {
        MarshalThrowerTimer.Record(5L, true, NewFormation(), default);

        Assert.AreEqual(1, MarshalThrowerCost.Read().Other.Calls);
    }

    [TestMethod]
    public void ThePostfixOfTheTimer_ReadsTheOrderOnlyAfterTheStateTest_HasNoTryAndNoLogLine()
    {
        var code = RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalThrowerTimer.cs", stripComments: true);
        var flat = Regex.Replace(code, @"\s+", " ");

        StringAssert.Contains(flat, "if (__state == 0L) return; Record(Stopwatch.GetTimestamp() - __state, __instance.OrderEnum == MovementOrder.MovementOrderEnum.Advance, f, __result);");
        Assert.IsFalse(code.Contains("try"), "like the pace postfix, a throw here is the engine's problem to see");
        Assert.IsFalse(code.Contains("Outcomes") || code.Contains("Log") || code.Contains("lock"));
    }

    [TestMethod]
    public void TheCostBuckets_KeepTheLargestCallUnderContention()
    {
        const int Threads = 8;
        const int Each = 20_000;
        Parallel.For(0, Threads, t =>
        {
            for (var i = 0; i < Each; i++)
                MarshalThrowerCost.AddPublished(1 + (i * 7 + t) % 997, replaced: i % 100 == 0);
        });

        var read = MarshalThrowerCost.Read();
        Assert.AreEqual(Threads * Each, read.Published.Calls);
        Assert.AreEqual(997, read.Published.Most);
        Assert.AreEqual(Threads * Each / 100, read.Replaced);
    }
}
