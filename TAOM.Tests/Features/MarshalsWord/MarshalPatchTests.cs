using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Patch141's three classes (build-spec 5.2, 6.1, 6.4). Harmony injects by NAME and the patched members are private or virtual, so the
/// binding tests pin every target and every parameter name; the behaviour tests drive each patch with identity-only engine objects.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalPatchTests
{
    private static readonly Type[] PatchClasses = { typeof(MarshalOrderCapturePatch), typeof(MarshalArrayPatch), typeof(MarshalPacePatch) };

    private static readonly string[] CaptureTargets =
    {
        nameof(OrderController.SetOrder), nameof(OrderController.SetOrderWithAgent), nameof(OrderController.SetOrderWithPosition),
        nameof(OrderController.SetOrderWithFormation), nameof(OrderController.SetOrderWithTwoPositions), nameof(OrderController.SetOrderWithOrderableObject),
    };

    private sealed class Sink : IMarshalHookSink
    {
        internal readonly List<MarshalOrderPayload> Offered = new();
        internal readonly List<(bool Click, int Companies)> ArraysSolved = new();
        internal readonly List<IReadOnlyList<MarchTarget>> Marches = new();
        internal bool Answer;
        internal Exception? Fault;

        public bool TryCapture(MarshalOrderPayload payload)
        {
            Offered.Add(payload);
            return Answer;
        }

        public void ArraySolved(bool click, int companies) => ArraysSolved.Add((click, companies));

        public void ArrayFault(Exception exception) => Fault = exception;

        public void MarchRequested(IReadOnlyList<MarchTarget> targets) => Marches.Add(targets.ToList());
    }

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup() => MarshalHooks.Clear();

    [TestCleanup]
    public void Cleanup() => MarshalHooks.Clear();

    private static T Blank<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    // ---- wiring ----

    [TestMethod]
    public void EveryPatchClass_CarriesPatch141Category_AndTheCategoryLiteralIsPinned()
    {
        Assert.AreEqual("Patch141_MarshalsWord", MarshalsWordModule.PatchCategory);
        foreach (var patch in PatchClasses)
        {
            var categories = patch.GetCustomAttributes<HarmonyPatchCategory>(inherit: false).ToList();
            Assert.AreEqual(1, categories.Count, patch.Name);
            Assert.AreEqual("Patch141_MarshalsWord", categories[0].info.category, patch.Name);
            Assert.AreEqual("TAOM.Features.MarshalsWord.Hooks", patch.Namespace, "PatchCategoryNumberTests reads the namespace for the owner");
        }
    }

    [TestMethod]
    public void TheCapturePatch_HasOnePrefixForEachOfTheSixOrderMethods()
    {
        var prefixes = typeof(MarshalOrderCapturePatch).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "Prefix").ToList();
        var targets = prefixes.SelectMany(m => m.GetCustomAttributes<HarmonyPatch>()).Select(a => a.info.methodName).OrderBy(n => n).ToList();

        CollectionAssert.AreEqual(CaptureTargets.OrderBy(n => n).ToList(), targets);
        Assert.IsTrue(prefixes.All(m => m.ReturnType == typeof(bool) && m.GetCustomAttributes<HarmonyPrefix>().Any()));
    }

    // ---- bindings ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryCaptureTarget_IsAPublicVirtualMethod_AndEachPrefixNamesItsParametersByTheEnginesNames()
    {
        foreach (var prefix in typeof(MarshalOrderCapturePatch).GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "Prefix"))
        {
            var name = prefix.GetCustomAttributes<HarmonyPatch>().Single().info.methodName;
            var matches = typeof(OrderController).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == name).ToList();
            Assert.AreEqual(1, matches.Count, name + " must be one public instance method");
            Assert.IsTrue(matches[0].IsVirtual, name + " is virtual in v1.5.6 (OrderController.cs:232, 731, 756, 800, 981, 1008)");

            var engine = matches[0].GetParameters();
            foreach (var parameter in prefix.GetParameters().Where(p => !p.Name!.StartsWith("__", StringComparison.Ordinal)))
            {
                var counterpart = engine.SingleOrDefault(p => p.Name == parameter.Name);
                Assert.IsNotNull(counterpart, name + " has no parameter named " + parameter.Name);
                Assert.AreEqual(counterpart!.ParameterType, parameter.ParameterType, name + "." + parameter.Name);
            }
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEnginesParameterNames_OfTheSixMethods_ArePinned()
    {
        var expected = new Dictionary<string, string[]>
        {
            [nameof(OrderController.SetOrder)] = new[] { "orderType" },
            [nameof(OrderController.SetOrderWithAgent)] = new[] { "orderType", "agent" },
            [nameof(OrderController.SetOrderWithPosition)] = new[] { "orderType", "orderPosition" },
            [nameof(OrderController.SetOrderWithFormation)] = new[] { "orderType", "orderFormation" },
            [nameof(OrderController.SetOrderWithTwoPositions)] = new[] { "orderType", "position1", "position2" },
            [nameof(OrderController.SetOrderWithOrderableObject)] = new[] { "target" },
        };

        foreach (var pair in expected)
            CollectionAssert.AreEqual(pair.Value, typeof(OrderController).GetMethod(pair.Key)!.GetParameters().Select(p => p.Name).ToArray(), pair.Key);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheArrayTarget_IsAPrivateStaticMethod_WithThePinnedParameterNamesAndOutShape()
    {
        var target = AccessTools.Method(typeof(OrderController), "SimulateNewOrderWithPositionAndDirectionAux");

        Assert.IsNotNull(target, "OrderController.SimulateNewOrderWithPositionAndDirectionAux (private static, v1.5.6 OrderController.cs:1206)");
        Assert.IsTrue(target.IsStatic && target.IsPrivate);
        CollectionAssert.AreEqual(
            new[]
            {
                "formations", "simulationFormations", "formationLineBegin", "formationLineEnd", "isSimulatingAgentFrames", "simulationAgentFrames",
                "isSimulatingFormationChanges", "simulationFormationChanges", "isLineShort", "isFormationLayoutVertical",
            },
            target.GetParameters().Select(p => p.Name).ToArray());

        var ours = typeof(MarshalArrayPatch).GetMethod("Prefix")!.GetParameters();
        var theirs = target.GetParameters();
        Assert.AreEqual(theirs.Length, ours.Length);
        for (var i = 0; i < theirs.Length; i++)
        {
            Assert.AreEqual(theirs[i].Name, ours[i].Name);
            Assert.AreEqual(theirs[i].ParameterType, ours[i].ParameterType, theirs[i].Name);
            Assert.AreEqual(theirs[i].IsOut, ours[i].IsOut, theirs[i].Name + " must keep its out-ness");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePaceTarget_IsPublic_TakesIsCharging_AndTheInjectedAgentFieldResolves()
    {
        var target = AccessTools.Method(typeof(HumanAIComponent), nameof(HumanAIComponent.GetDesiredSpeedInFormation));

        Assert.IsNotNull(target);
        Assert.AreEqual(typeof(float), target.ReturnType);
        CollectionAssert.AreEqual(new[] { "isCharging" }, target.GetParameters().Select(p => p.Name).ToArray());
        var field = AccessTools.Field(typeof(HumanAIComponent), "Agent");
        Assert.IsNotNull(field, "Harmony strips three underscores from ___Agent and looks up a field named Agent (protected, on AgentComponent)");
        Assert.AreEqual(typeof(Agent), field.FieldType);

        var postfix = typeof(MarshalPacePatch).GetMethod("Postfix")!;
        CollectionAssert.AreEqual(new[] { "isCharging", "__result", "___Agent" }, postfix.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePaceTarget_IsOnPatchShieldsHotMethodList_BecauseItRunsForEveryAgentOnWorkers()
    {
        var target = AccessTools.Method(typeof(HumanAIComponent), nameof(HumanAIComponent.GetDesiredSpeedInFormation));

        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
            "a shield finalizer on a per-agent worker-thread target costs a call per soldier per frame");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEngineMembers_TheFeatureReadsOrIssuesAreStillThere()
    {
        Assert.AreEqual(1.5f, OrderController.FormationGapInLine, "MarshalTuning.GapInLine restates it");
        Assert.AreEqual(OrderController.FormationGapInLine, MarshalTuning.GapInLine);
        Assert.IsNotNull(typeof(Formation).GetField("Index", BindingFlags.Public | BindingFlags.Instance), "a company is the public readonly Formation.Index");
        Assert.IsTrue(typeof(Formation).GetField("Index")!.IsInitOnly);
        Assert.IsNotNull(typeof(Mission).GetField("IsOrderMenuOpen", BindingFlags.Public | BindingFlags.Instance));
        Assert.IsNotNull(typeof(OrderController).GetEvent("OnOrderIssued"));
        Assert.IsNotNull(typeof(Team).GetProperty("PlayerOrderController"));
        Assert.IsNotNull(typeof(Team).GetProperty("MasterOrderController"));
        Assert.IsNotNull(typeof(Mission.TeamCollection).GetEvent("OnPlayerTeamChanged"));
        foreach (var name in new[] { "CurrentPosition", "OrderPosition", "OrderPositionIsValid", "CachedMovementSpeed", "Depth", "MinimumWidth", "MaximumWidth", "Width" })
            Assert.IsNotNull(typeof(Formation).GetProperty(name), "Formation." + name);
        Assert.AreEqual(typeof(MovementOrder.MovementOrderEnum), typeof(MovementOrder).GetField("OrderEnum")!.FieldType);
        Assert.AreEqual(1, (int)MovementOrder.MovementOrderEnum.AttackEntity, "pin the numeric value, not only the name");
        Assert.AreEqual(1, (int)MovementOrder.MovementStateEnum.Hold, "read from MovementOrder.cs:27-33");
    }

    // ---- the capture prefixes ----

    private static OrderController ArmedController(Sink sink, bool answer)
    {
        var controller = Blank<OrderController>();
        sink.Answer = answer;
        MarshalHooks.PlayerController = controller;
        MarshalHooks.Begin(sink);
        MarshalHooks.Arm(queue: true, keepWidths: false, keepArray: true, arriveTogether: true);
        return controller;
    }

    [TestMethod]
    public void Capture_WhenTheSessionIsNotLive_EveryPrefixReturnsTrueWithoutOfferingAnything()
    {
        var sink = new Sink();
        var controller = ArmedController(sink, answer: true);
        MarshalHooks.Clear();

        Assert.IsTrue(MarshalOrderCapturePatch.Prefix(controller, OrderType.Move));
        Assert.AreEqual(0, sink.Offered.Count);
    }

    [TestMethod]
    public void Capture_WhenTheKeyIsNotDown_ReturnsTrue()
    {
        var sink = new Sink();
        var controller = ArmedController(sink, answer: true);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);

        Assert.IsTrue(MarshalOrderCapturePatch.Prefix(controller, OrderType.Charge));
        Assert.AreEqual(0, sink.Offered.Count);
    }

    [TestMethod]
    public void Capture_AnotherControllersOrder_ReturnsTrue_SoTheMasterControllerIsNeverCaptured()
    {
        var sink = new Sink();
        ArmedController(sink, answer: true);
        var master = Blank<OrderController>();

        Assert.IsTrue(MarshalOrderCapturePatch.Prefix(master, OrderType.Charge));
        Assert.AreEqual(0, sink.Offered.Count, "a step issued on the master controller must not queue itself again");
    }

    [TestMethod]
    public void Capture_ArmedOnThePlayersController_OffersTheOrder_AndSkipsVanillaWhenItWasQueued()
    {
        var sink = new Sink();
        var controller = ArmedController(sink, answer: true);

        var runVanilla = MarshalOrderCapturePatch.Prefix(controller, OrderType.Charge);

        Assert.IsFalse(runVanilla, "true means it was captured, so vanilla is skipped");
        Assert.AreEqual(1, sink.Offered.Count);
        Assert.AreEqual(OrderType.Charge, sink.Offered[0].Type);
        Assert.AreEqual(StepEnd.AtOnce, sink.Offered[0].End);
    }

    [TestMethod]
    public void Capture_ArmedButDeclined_LetsVanillaRun()
    {
        var sink = new Sink();
        var controller = ArmedController(sink, answer: false);

        Assert.IsTrue(MarshalOrderCapturePatch.Prefix(controller, OrderType.Charge));
        Assert.AreEqual(1, sink.Offered.Count);
    }

    [TestMethod]
    public void Capture_TheAgentPositionFormationAndTwoPositionMethods_OfferTheirOwnPayload()
    {
        var sink = new Sink();
        var controller = ArmedController(sink, answer: true);
        var position = default(WorldPosition);
        var formation = Blank<Formation>();

        Assert.IsFalse(MarshalOrderCapturePatch.Prefix(controller, OrderType.Move, position));
        Assert.IsFalse(MarshalOrderCapturePatch.Prefix(controller, OrderType.Charge, formation));
        Assert.IsFalse(MarshalOrderCapturePatch.Prefix(controller, OrderType.MoveToLineSegment, position, position));
        Assert.IsFalse(MarshalOrderCapturePatch.Prefix(controller, OrderType.FollowMe, Blank<Agent>()));

        CollectionAssert.AreEqual(
            new[] { StepEnd.Arrival, StepEnd.TargetGone, StepEnd.Arrival }, sink.Offered.Take(3).Select(p => p.End!.Value).ToArray());
        Assert.IsNull(sink.Offered[3].End, "FollowMe has no end: it is offered so the decline is logged, and vanilla runs it");
    }

    [TestMethod]
    public void Capture_AnOrderableWhoseSideCannotBeRead_LeavesVanillaToRun()
    {
        var sink = new Sink();
        var controller = ArmedController(sink, answer: true);

        // The controller has no team, so reading its side throws inside the payload; the prefix must not.
        Assert.IsTrue(MarshalOrderCapturePatch.Prefix(controller, (IOrderable)null!));
        Assert.AreEqual(0, sink.Offered.Count);
    }

    // ---- the array prefix ----

    private static bool RunArray(bool vertical, out List<WorldPosition> frames)
    {
        return MarshalArrayPatch.Prefix(new List<Formation>(), new Dictionary<Formation, Formation>(), default, default, true, out frames, false,
            out _, out _, vertical);
    }

    [TestMethod]
    public void Array_WhenNotLive_RunsVanilla()
    {
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);

        Assert.IsTrue(RunArray(vertical: true, out _));
    }

    [TestMethod]
    public void Array_WithTheSettingOff_RunsVanilla()
    {
        MarshalHooks.Begin(new Sink());
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: false, arriveTogether: true);

        Assert.IsTrue(RunArray(vertical: true, out _));
    }

    [TestMethod]
    public void Array_AHorizontalLayout_RunsVanilla_BecauseTheContractTakesOverOnlyTheVerticalOne()
    {
        MarshalHooks.Begin(new Sink());
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);

        Assert.IsTrue(RunArray(vertical: false, out _));
    }

    [TestMethod]
    public void Array_WithNoMission_RunsVanilla_AndLeavesTheOutputsToIt()
    {
        var sink = new Sink();
        SetCurrentMission(null);
        MarshalHooks.Begin(sink);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);

        Assert.IsTrue(RunArray(vertical: true, out var frames));
        Assert.IsNull(frames, "the prefix writes nothing it does not own: vanilla assigns every output itself");
        Assert.IsNull(sink.Fault, "no mission is a refusal, not a fault");
    }

    [TestMethod]
    public void Array_WithOneCompany_RunsVanilla_WithoutLatchingTheTakeoverOff()
    {
        // A mission that is not in Deployment, so the company count is the first test that refuses. Without it the player team would be asked of
        // a mission that has none, and the throw would latch the takeover off: the sink would hear of a fault.
        try
        {
            var sink = new Sink();
            MarshalHooks.Begin(sink);
            MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);
            SetCurrentMission(Blank<Mission>());

            var runVanilla = MarshalArrayPatch.Prefix(new List<Formation> { Blank<Formation>() }, new Dictionary<Formation, Formation>(), default, default,
                true, out var frames, false, out _, out _, true);

            Assert.IsTrue(runVanilla);
            Assert.IsNull(frames);
            Assert.IsNull(sink.Fault, "one company is a refusal, not a fault");
            Assert.IsTrue(MarshalHooks.KeepArray);
        }
        finally
        {
            SetCurrentMission(null);
        }
    }

    [TestMethod]
    public void Array_ALatchedFault_TurnsTheTakeoverOffForTheMission_AndTellsTheSession()
    {
        var sink = new Sink();
        MarshalHooks.Begin(sink);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);
        Assert.IsTrue(MarshalHooks.KeepArray);

        MarshalHooks.LatchArrayOff(new InvalidOperationException("layout broke"));

        Assert.IsFalse(MarshalHooks.KeepArray);
        Assert.IsNotNull(sink.Fault);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);
        Assert.IsFalse(MarshalHooks.KeepArray, "the setting copied on the next frame does not undo the latch");

        MarshalHooks.Begin(sink);
        Assert.IsTrue(MarshalHooks.KeepArray, "a new mission starts with the takeover available again");
    }

    // ---- the array prefix's own latch ----

    private sealed class FormationsThatMustNotBeRead : IEnumerable<Formation>
    {
        public IEnumerator<Formation> GetEnumerator() => throw new InvalidOperationException("the prefix read the companies");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static void SetCurrentMission(Mission? mission) =>
        typeof(Mission).GetProperty("Current", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, mission);

    [TestMethod]
    public void Array_AThrowInsideTheTakeover_LatchesItOffForTheMission_TellsTheSession_AndLetsVanillaRun()
    {
        // Every gate is open (live, the setting on, a vertical layout, a mission that is not Deployment), so the prefix reads the companies,
        // and this enumerable throws: the catch is the only thing between the throw and the engine.
        try
        {
            var sink = new Sink();
            MarshalHooks.Begin(sink);
            MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);
            SetCurrentMission(Blank<Mission>());

            var runVanilla = MarshalArrayPatch.Prefix(new FormationsThatMustNotBeRead(), new Dictionary<Formation, Formation>(), default, default, true,
                out var frames, false, out _, out _, true);

            Assert.IsTrue(runVanilla, "a throw falls to vanilla");
            Assert.IsNull(frames, "and the prefix wrote no output");
            Assert.IsNotNull(sink.Fault);
            Assert.IsFalse(MarshalHooks.KeepArray);
        }
        finally
        {
            SetCurrentMission(null);
        }
    }

    // ---- the commit's report (the layout's AfterCommit and the prefix's split) ----

    private static (Formation, int, float, WorldPosition, Vec2) Change(int index, float x, float y)
    {
        var formation = Blank<Formation>();
        typeof(Formation).GetField("Index")!.SetValue(formation, index);
        return (formation, 0, 20f, TestPositions.At(x, y, 0f), new Vec2(0f, 1f));
    }

    [TestMethod]
    public void AfterCommit_ADrag_ReportsADrag_AndAsksForAMarchWithEachCompanysOwnPoint()
    {
        var sink = new Sink();
        MarshalHooks.Begin(sink);
        var changes = new List<(Formation, int, float, WorldPosition, Vec2)> { Change(2, 5f, 6f), Change(4, 15f, 16f) };

        MarshalArrayLayout.AfterCommit(changes, isLineShort: false);

        CollectionAssert.AreEqual(new[] { (false, 2) }, sink.ArraysSolved);
        CollectionAssert.AreEqual(new[] { new MarchTarget(2, 5f, 6f), new MarchTarget(4, 15f, 16f) }, sink.Marches.Single().ToArray());
    }

    [TestMethod]
    public void AfterCommit_AClick_ReportsAClick()
    {
        var sink = new Sink();
        MarshalHooks.Begin(sink);

        MarshalArrayLayout.AfterCommit(new List<(Formation, int, float, WorldPosition, Vec2)> { Change(1, 0f, 0f), Change(2, 0f, 0f) }, isLineShort: true);

        CollectionAssert.AreEqual(new[] { (true, 2) }, sink.ArraysSolved);
    }

    [TestMethod]
    public void AfterCommit_WithArriveTogetherOff_StillAsksForTheMarch_BecauseTheMarchDriversPassDecides()
    {
        var sink = new Sink();
        MarshalHooks.Begin(sink);
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: false);

        MarshalArrayLayout.AfterCommit(new List<(Formation, int, float, WorldPosition, Vec2)> { Change(1, 0f, 0f), Change(2, 0f, 0f) }, isLineShort: false);

        Assert.AreEqual(1, sink.Marches.Count, "the setting is read in one place: MarshalMarchDriver.Pass drops the request");
    }

    [TestMethod]
    public void ThePrefix_ReportsACommit_AndCountsAPreview_NeverTheOtherWayRound()
    {
        var code = System.Text.RegularExpressions.Regex.Replace(
            RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalArrayPatch.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(code, "isSimulatingAgentFrames, out var frames, isSimulatingFormationChanges, out var changes, out var lineIsShort);");
        StringAssert.Contains(code, "if (isSimulatingFormationChanges) MarshalArrayLayout.AfterCommit(changes!, lineIsShort); else Preview.Pass(); return false;");
    }

    // ---- the order adapter and the controller static ----

    private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    [TestMethod]
    public void Detach_UnsubscribesAndForgetsItsController_ButNeverWritesThePlayerControllerStatic()
    {
        // A session that finishes late must not clear the next mission's hooks: only MarshalHooks.Clear, run by the owner, nulls it.
        var next = Blank<OrderController>();
        MarshalHooks.PlayerController = next;
        var adapter = new MarshalOrderAdapter(Substitute.For<IModLogger>());
        var field = typeof(MarshalOrderAdapter).GetField("_controller", AnyInstance)!;
        field.SetValue(adapter, Blank<OrderController>());

        adapter.Detach();

        Assert.IsNull(field.GetValue(adapter), "it forgets its own controller");
        Assert.AreSame(next, MarshalHooks.PlayerController, "the static belongs to whoever owns the hooks now");
    }

    [TestMethod]
    public void ATeamChange_StillWritesThePlayerControllerStatic_ANullIncluded()
    {
        MarshalHooks.PlayerController = Blank<OrderController>();
        var adapter = new MarshalOrderAdapter(Substitute.For<IModLogger>());

        typeof(MarshalOrderAdapter).GetMethod("OnTeamChanged", AnyInstance)!.Invoke(adapter, new object?[] { null, null });

        Assert.IsNull(MarshalHooks.PlayerController, "a live session follows the player's team, and no team means no controller");
    }

    // ---- the pace table ----

    [TestMethod]
    public void KeepWidthsHeld_CanBeBorrowedForACall_AndTheClearDropsIt()
    {
        MarshalHooks.Arm(queue: false, keepWidths: false, keepArray: true, arriveTogether: true);

        MarshalHooks.KeepWidthsHeld = true;
        Assert.IsTrue(MarshalHooks.KeepWidthsHeld);
        MarshalHooks.KeepWidthsHeld = false;
        Assert.IsFalse(MarshalHooks.KeepWidthsHeld);

        MarshalHooks.KeepWidthsHeld = true;
        MarshalHooks.Clear();
        Assert.IsFalse(MarshalHooks.KeepWidthsHeld);
    }

    [TestMethod]
    public void Begin_AStalePaceTableOfAnEarlierSession_IsGoneForTheNewOne()
    {
        // A session that finishes late must not clear the next mission's hooks, so the new session clears what the old one left behind.
        MarshalHooks.Paces = new MarshalPaceTable(new[] { Blank<Formation>() }, new[] { 0.5f });

        MarshalHooks.Begin(new Sink());

        Assert.IsNull(MarshalHooks.Paces, "the table named formations of the earlier mission");
    }

    [TestMethod]
    public void Scale_ACompanyInTheTable_IsMultipliedByItsPace()
    {
        var a = Blank<Formation>();
        var b = Blank<Formation>();
        var table = new MarshalPaceTable(new[] { a, b }, new[] { 0.5f, 1f });

        Assert.AreEqual(0.45f, table.Scale(a, 0.9f), 1e-6f);
        Assert.AreEqual(0.9f, table.Scale(b, 0.9f), 1e-6f);
    }

    [TestMethod]
    public void Scale_ACompanyNotInTheTable_KeepsTheEnginesMultiplier()
    {
        var table = new MarshalPaceTable(new[] { Blank<Formation>() }, new[] { 0.5f });

        Assert.AreEqual(0.8f, table.Scale(Blank<Formation>(), 0.8f), 1e-6f);
        Assert.AreEqual(0.8f, table.Scale(null, 0.8f), 1e-6f, "an agent with no formation is left alone");
    }

    [TestMethod]
    public void Scale_NeverGoesBelowTheEnginesFloorOfPointTwo_OrAboveOne()
    {
        var a = Blank<Formation>();
        var table = new MarshalPaceTable(new[] { a }, new[] { 0.3f });

        Assert.AreEqual(0.2f, table.Scale(a, 0.3f), 1e-6f);
        Assert.AreEqual(1f, new MarshalPaceTable(new[] { a }, new[] { 4f }).Scale(a, 1f), 1e-6f);
    }

    [TestMethod]
    public void Scale_FindsTheFormationWithReferenceEquals_BecauseGetHashCodeReadsTheTeamAndASimulationCopyHasNone()
    {
        // Formation.GetHashCode reads Team.TeamIndex, and a team-less simulation copy would throw on it (lessons/adapters-taleworlds-api.md), so
        // the table may not look a formation up with Equals, a dictionary or a list search. An identity-only Formation cannot tell the two apart.
        var code = RepoPaths.ReadSource("Main/Features/MarshalsWord/MarshalPaceTable.cs", stripComments: true);

        StringAssert.Contains(code, "ReferenceEquals(Formations[i], formation)");
        foreach (var banned in new[] { "Dictionary", "HashSet", "IndexOf(", "Contains(", ".Equals(", "== formation" })
            Assert.IsFalse(code.Contains(banned), "the lookup is by reference: " + banned);
    }

    [TestMethod]
    public void ThePostfix_SkipsCharging_ThenTheNullTable_BeforeTouchingTheAgent()
    {
        var code = RepoPaths.ReadSource("Main/Features/MarshalsWord/Hooks/MarshalPacePatch.cs", stripComments: true);
        var body = System.Text.RegularExpressions.Regex.Replace(code.Substring(code.IndexOf("public static void Postfix", StringComparison.Ordinal)), @"\s+", " ");

        StringAssert.Contains(body, "if (isCharging) return; var table = MarshalHooks.Paces; if (table != null) __result = table.Scale(___Agent.Formation, __result);");
        Assert.IsFalse(body.Contains("lock"), "it runs on three threads and takes no lock");
        Assert.IsFalse(body.Contains("Outcomes") || body.Contains("Log"), "no log line on a per-agent path");
    }
}
