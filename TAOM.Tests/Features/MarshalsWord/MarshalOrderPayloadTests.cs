using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The allow list of build-spec 5.2 and the orders that clear a queue (5.3, rule 6), row by row.</summary>
[TestClass]
[TestCategory("RequiresGame")]
public class MarshalOrderPayloadTests
{
    [DataTestMethod]
    [DataRow(OrderType.Charge)]
    [DataRow(OrderType.StandYourGround)]
    [DataRow(OrderType.Advance)]
    [DataRow(OrderType.FallBack)]
    [DataRow(OrderType.Retreat)]
    [DataRow(OrderType.AdvanceTenPaces)]
    [DataRow(OrderType.FallBackTenPaces)]
    [DataRow(OrderType.LookAtEnemy)]
    [DataRow(OrderType.HoldFire)]
    [DataRow(OrderType.FireAtWill)]
    [DataRow(OrderType.Mount)]
    [DataRow(OrderType.Dismount)]
    [DataRow(OrderType.ArrangementLine)]
    [DataRow(OrderType.ArrangementCloseOrder)]
    [DataRow(OrderType.ArrangementLoose)]
    [DataRow(OrderType.ArrangementCircular)]
    [DataRow(OrderType.ArrangementSchiltron)]
    [DataRow(OrderType.ArrangementVee)]
    [DataRow(OrderType.ArrangementColumn)]
    [DataRow(OrderType.ArrangementScatter)]
    [DataRow(OrderType.FormDeep)]
    [DataRow(OrderType.FormWide)]
    [DataRow(OrderType.FormWider)]
    public void EndFor_ASetOrderOnTheAllowList_EndsAtOnce(OrderType type)
    {
        Assert.AreEqual(StepEnd.AtOnce, MarshalOrderPayload.EndFor(MarshalCall.Order, type));
    }

    [DataTestMethod]
    [DataRow(OrderType.FollowMe)]
    [DataRow(OrderType.FollowEntity)]
    [DataRow(OrderType.Use)]
    [DataRow(OrderType.Transfer)]
    [DataRow(OrderType.AIControlOn)]
    [DataRow(OrderType.AIControlOff)]
    [DataRow(OrderType.FormCustom)]
    [DataRow(OrderType.RideFree)]
    [DataRow(OrderType.CohesionHigh)]
    [DataRow(OrderType.Move)]
    [DataRow(OrderType.None)]
    public void EndFor_ASetOrderOffTheAllowList_IsNotQueued(OrderType type)
    {
        Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.Order, type));
    }

    [TestMethod]
    public void EndFor_AMoveToAPosition_EndsOnArrival_AndALookAtDirectionAtOnce()
    {
        Assert.AreEqual(StepEnd.Arrival, MarshalOrderPayload.EndFor(MarshalCall.Position, OrderType.Move));
        Assert.AreEqual(StepEnd.AtOnce, MarshalOrderPayload.EndFor(MarshalCall.Position, OrderType.LookAtDirection));
        Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.Position, OrderType.Charge));
    }

    [TestMethod]
    public void EndFor_AChargeOrAdvanceOnAFormation_EndsWhenTheTargetIsGone()
    {
        Assert.AreEqual(StepEnd.TargetGone, MarshalOrderPayload.EndFor(MarshalCall.Formation, OrderType.Charge));
        Assert.AreEqual(StepEnd.TargetGone, MarshalOrderPayload.EndFor(MarshalCall.Formation, OrderType.Advance));
        Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.Formation, OrderType.Retreat));
    }

    [TestMethod]
    public void EndFor_ALineSegment_EndsOnArrival_InBothLayouts()
    {
        Assert.AreEqual(StepEnd.Arrival, MarshalOrderPayload.EndFor(MarshalCall.TwoPositions, OrderType.MoveToLineSegment));
        Assert.AreEqual(StepEnd.Arrival, MarshalOrderPayload.EndFor(MarshalCall.TwoPositions, OrderType.MoveToLineSegmentWithHorizontalLayout));
        Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.TwoPositions, OrderType.Move));
    }

    [TestMethod]
    public void EndFor_AnOrderableObject_EndsOnArrivalOrWhenTheEntityOrderIsSpent_AndNeverForUse()
    {
        Assert.AreEqual(StepEnd.Arrival, MarshalOrderPayload.EndFor(MarshalCall.Orderable, OrderType.PointDefence));
        Assert.AreEqual(StepEnd.Arrival, MarshalOrderPayload.EndFor(MarshalCall.Orderable, OrderType.Move));
        Assert.AreEqual(StepEnd.Arrival, MarshalOrderPayload.EndFor(MarshalCall.Orderable, OrderType.MoveToLineSegment));
        Assert.AreEqual(StepEnd.EntityOrderEnded, MarshalOrderPayload.EndFor(MarshalCall.Orderable, OrderType.AttackEntity));
        Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.Orderable, OrderType.Use), "Use toggles and depends on state at the click");
        Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.Orderable, OrderType.None));
    }

    [TestMethod]
    public void EndFor_AnOrderWithAnAgent_IsNeverQueued()
    {
        foreach (var type in new[] { OrderType.FollowMe, OrderType.Move, OrderType.Charge })
            Assert.IsNull(MarshalOrderPayload.EndFor(MarshalCall.Agent, type));
        Assert.IsNull(MarshalOrderPayload.ForAgent(OrderType.FollowMe).End);
    }

    [DataTestMethod]
    [DataRow(OrderType.HoldFire)]
    [DataRow(OrderType.FireAtWill)]
    [DataRow(OrderType.RideFree)]
    [DataRow(OrderType.Mount)]
    [DataRow(OrderType.Dismount)]
    [DataRow(OrderType.ArrangementLine)]
    [DataRow(OrderType.ArrangementScatter)]
    [DataRow(OrderType.FormCustom)]
    [DataRow(OrderType.FormDeep)]
    [DataRow(OrderType.FormWide)]
    [DataRow(OrderType.FormWider)]
    public void ClearsQueue_FiringRidingArrangementAndFormOrders_DoNotContradictAWalk(OrderType type)
    {
        Assert.IsFalse(MarshalOrderPayload.ClearsQueue(type));
    }

    [DataTestMethod]
    [DataRow(OrderType.Move)]
    [DataRow(OrderType.MoveToLineSegment)]
    [DataRow(OrderType.MoveToLineSegmentWithHorizontalLayout)]
    [DataRow(OrderType.Charge)]
    [DataRow(OrderType.StandYourGround)]
    [DataRow(OrderType.Retreat)]
    [DataRow(OrderType.FollowMe)]
    [DataRow(OrderType.AttackEntity)]
    [DataRow(OrderType.LookAtEnemy)]
    [DataRow(OrderType.LookAtDirection)]
    [DataRow(OrderType.AIControlOn)]
    public void ClearsQueue_AnyOtherDirectOrder_ClearsTheCompanysQueue(OrderType type)
    {
        Assert.IsTrue(MarshalOrderPayload.ClearsQueue(type));
    }

    [TestMethod]
    public void ForTwoPositions_RecordsTheWidthsKeyAsItStoodAtTheCapture_NotAsItStandsLater()
    {
        // A queued drag is issued when its step starts, so it must carry the key the player held when the drag was given (build-spec 6.2).
        try
        {
            MarshalHooks.Clear();
            MarshalHooks.Arm(queue: true, keepWidths: true, keepArray: true, arriveTogether: true);
            var held = MarshalOrderPayload.ForTwoPositions(OrderType.MoveToLineSegment, default, default);
            MarshalHooks.Arm(queue: true, keepWidths: false, keepArray: true, arriveTogether: true);
            var released = MarshalOrderPayload.ForTwoPositions(OrderType.MoveToLineSegment, default, default);
            MarshalHooks.Arm(queue: true, keepWidths: true, keepArray: true, arriveTogether: true);

            Assert.IsTrue(held.KeepWidths, "a release after the capture does not reach back");
            Assert.IsFalse(released.KeepWidths, "a press after the capture does not either");
        }
        finally
        {
            MarshalHooks.Clear();
        }
    }

    [TestMethod]
    public void EveryOtherPayload_RecordsNoWidthsKey_EvenWithTheKeyHeld()
    {
        try
        {
            MarshalHooks.Clear();
            MarshalHooks.Arm(queue: true, keepWidths: true, keepArray: true, arriveTogether: true);

            Assert.IsFalse(MarshalOrderPayload.ForOrder(OrderType.Charge).KeepWidths);
            Assert.IsFalse(MarshalOrderPayload.ForPosition(OrderType.Move, default).KeepWidths);
        }
        finally
        {
            MarshalHooks.Clear();
        }
    }

    [DataTestMethod]
    [DataRow(0f, 10f, 10f)]
    [DataRow(4f, -3f, -3f)]
    [DataRow(5f, 0f, 5f)]
    public void FlagHeight_AGroundAnswer_WinsOverTheAverageOfTheEnds_AndNothingFallsBackToIt(float average, float probed, float expected)
    {
        // Endpoints at 0 with a hill of 10 at the middle put a flag in the hill; the native probe answers 0 when nothing is there.
        Assert.AreEqual(expected, MarshalOrderPayload.FlagHeight(average, probed));
    }

    [TestMethod]
    public void FlagHeight_NoSceneToAsk_IsTheAverageOfTheEnds()
    {
        Assert.AreEqual(2.5f, MarshalOrderPayload.FlagHeight(2.5f, null));
    }

    [TestMethod]
    public void ForOrderable_TakesTheOrderTypeTheOrderableGivesThePlayersSide_AndEndsAccordingly()
    {
        // SetOrderWithOrderableObject asks the target for the order of the controller's side (OrderController.cs:1010).
        var orderable = Substitute.For<IOrderable>();
        orderable.GetOrder(BattleSideEnum.Attacker).Returns(OrderType.AttackEntity);
        orderable.GetOrder(BattleSideEnum.Defender).Returns(OrderType.Move);

        var attack = MarshalOrderPayload.ForOrderable(orderable, BattleSideEnum.Attacker);
        var move = MarshalOrderPayload.ForOrderable(orderable, BattleSideEnum.Defender);

        Assert.AreEqual(OrderType.AttackEntity, attack.Type);
        Assert.AreEqual(StepEnd.EntityOrderEnded, attack.End);
        Assert.AreSame(orderable, attack.Orderable);
        Assert.AreEqual(StepEnd.Arrival, move.End);
        Assert.AreEqual("Orderable:AttackEntity", attack.Describe());
    }

    [TestMethod]
    public void TryPoint_AMoveAndALine_GiveTheSamePointOnEveryAsk()
    {
        // The positions are built valid so that no native call is needed: the point is the ground of the Move, and the middle of a line.
        var move = MarshalOrderPayload.ForPosition(OrderType.Move, TestPositions.At(3f, 4f, 5f));
        var line = MarshalOrderPayload.ForTwoPositions(OrderType.MoveToLineSegment,
            TestPositions.At(0f, 0f, 2f),
            TestPositions.At(10f, 20f, 4f));

        for (var ask = 0; ask < 3; ask++)
        {
            Assert.IsTrue(move.TryPoint(out var x, out var y, out var z));
            CollectionAssert.AreEqual(new[] { 3f, 4f, 5f }, new[] { x, y, z });
            Assert.IsTrue(line.TryPoint(out x, out y, out z));
            CollectionAssert.AreEqual(new[] { 5f, 10f, 3f }, new[] { x, y, z }, "the middle, at the average of the ends when no scene can be asked");
        }
    }

    [TestMethod]
    public void TryPoint_AnOrderWithNoPlace_GivesNone()
    {
        Assert.IsFalse(MarshalOrderPayload.ForOrder(OrderType.Charge).TryPoint(out _, out _, out _));
        Assert.IsFalse(MarshalOrderPayload.ForPosition(OrderType.LookAtDirection, default).TryPoint(out _, out _, out _));
    }

    [TestMethod]
    public void ArrangementRange_CoversExactlyTheEightArrangementOrders()
    {
        var arrangements = Enum.GetValues(typeof(OrderType)).Cast<OrderType>()
            .Where(t => t.ToString().StartsWith("Arrangement", StringComparison.Ordinal)).ToList();

        Assert.AreEqual(8, arrangements.Count);
        Assert.IsTrue(arrangements.All(t => !MarshalOrderPayload.ClearsQueue(t)));
        Assert.IsTrue(arrangements.All(t => MarshalOrderPayload.EndFor(MarshalCall.Order, t) == StepEnd.AtOnce),
            "the contiguous range the code tests must be the engine's own eight");
    }
}
