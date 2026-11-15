using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// #871: who starts a hold, and for how long. A capture is recorded when the owner changes by siege and judged once, on
/// the first hourly pass after it. The engine pauses the capturing army for 10 hours by itself; a hold lengthens that pause.
/// </summary>
[TestClass]
public class HoldTheWallsStartTests
{
    private HoldTheWallsRig _rig = null!;

    [TestInitialize]
    public void SetUp() => _rig = new HoldTheWallsRig();

    [TestMethod]
    public void AFreeArmy_HoldsFromTheCaptureHour_ForTheTypicalLordsTwentyTwoHours()
    {
        _rig.Capture();

        _rig.Tick(1.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:21" }, _rig.Adapter.Writes, "22 hours from the capture, one already gone");
    }

    [TestMethod]
    public void TheHold_IsCountedFromTheCapture_NotFromTheFirstPass()
    {
        _rig.Capture();

        _rig.Tick(0.5);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:22" }, _rig.Adapter.Writes, "21.5 hours left rounds up so the hold never ends early");
    }

    [TestMethod]
    public void ACalculatingLordHoldsLonger_AValorousOneShorter()
    {
        var calculating = HoldTheWallsRig.Army("calc");
        calculating.Calculating = 1;
        var valorous = HoldTheWallsRig.Army("brave");
        valorous.Valor = 1;
        _rig.Capture("town_A", calculating);
        _rig.Capture("town_B", valorous);

        _rig.Tick(0.0);

        CollectionAssert.AreEqual(new[] { "hold:calc:27", "hold:brave:17" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void TheTraitExtremes_ClampToTheConfiguredBounds()
    {
        var longest = HoldTheWallsRig.Army("long");
        longest.Calculating = 2;
        longest.Valor = -2;
        var shortest = HoldTheWallsRig.Army("short");
        shortest.Calculating = -2;
        shortest.Valor = 2;
        _rig.Capture("town_A", longest);
        _rig.Capture("town_B", shortest);

        _rig.Tick(0.0);

        CollectionAssert.AreEqual(new[] { "hold:long:30", "hold:short:12" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AnEvilArmy_DoesNotHold()
    {
        _rig.Capture("town_A", HoldTheWallsRig.Army("orc", "mordor"));

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void ANeutralArmy_DoesNotHoldByDefault_AndHoldsWhenTheConfigListsNeutral()
    {
        _rig.Capture("town_A", HoldTheWallsRig.Army("corsair", "umbar"));
        _rig.Tick();
        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "Neutral rolls on by default");

        _rig.Hold.Sides = new List<string> { "Free", "Neutral" };
        _rig.Capture("town_B", HoldTheWallsRig.Army("corsair2", "umbar"));
        _rig.Tick();

        CollectionAssert.AreEqual(new[] { "hold:corsair2:21" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AnEvilArmy_HoldsWhenTheConfigListsEvil()
    {
        _rig.Hold.Sides = new List<string> { "Evil" };
        _rig.Capture("town_A", HoldTheWallsRig.Army("orc", "mordor"));
        _rig.Capture("town_B", HoldTheWallsRig.Army("man"));

        _rig.Tick(0.0);

        CollectionAssert.AreEqual(new[] { "hold:orc:22" }, _rig.Adapter.Writes, "the Free army no longer holds");
    }

    // #1283: Mordor's armies take forts and then march off, so its realm holds although the Evil side does not.
    [TestMethod]
    public void AnEvilArmy_HoldsWhenTheConfigListsItsRealm_AndOtherEvilRealmsDoNot()
    {
        _rig.Hold.Factions = new List<string> { "mordor" };
        _rig.SideOfRealm["isengard"] = FactionSide.Evil;
        _rig.Capture("town_A", HoldTheWallsRig.Army("orc", "mordor"));
        _rig.Capture("town_B", HoldTheWallsRig.Army("uruk", "isengard"));
        _rig.Capture("town_C", HoldTheWallsRig.Army("man"));

        _rig.Tick(0.0);

        CollectionAssert.AreEquivalent(new[] { "hold:orc:22", "hold:man:22" }, _rig.Adapter.Writes,
            "the listed realm and the listed side hold; an unlisted realm of an unlisted side does not");
    }

    [TestMethod]
    public void TheSide_IsResolvedFromTheRealmsIdAndItsOwnCulture()
    {
        _rig.Capture("town_A", HoldTheWallsRig.Army("lord_1", "empire_w"));

        _rig.Tick();

        _rig.Alignment.Received(1).ResolveSide("empire_w", "empire");
    }

    [TestMethod]
    public void ACaptureThatIsNotBySiege_IsIgnored()
    {
        _rig.Adapter.Put("town_A", HoldTheWallsRig.Army());
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: false);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
        Assert.AreEqual(0, _rig.Adapter.Reads, "nothing was recorded, so nothing is read");
    }

    [TestMethod]
    public void ASiegeCaptureOfAVillage_IsIgnored()
    {
        _rig.Adapter.Put("village_A", HoldTheWallsRig.Army());
        _rig.Service.OnSettlementOwnerChanged("village_A", isFortification: false, bySiege: true);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
        Assert.AreEqual(0, _rig.Adapter.Reads);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ACaptureWithNoSettlementId_IsIgnored(string? id)
    {
        _rig.Service.OnSettlementOwnerChanged(id, isFortification: true, bySiege: true);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Reads);
    }

    [TestMethod]
    public void APlayerLedArmy_IsSkipped()
    {
        var army = HoldTheWallsRig.Army();
        army.IsPlayerLed = true;
        _rig.Capture("town_A", army);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void AnArmyBelowTheHoldFloor_IsSkipped_AndOneAtTheFloorHolds()
    {
        var weak = HoldTheWallsRig.Army("weak");
        weak.Cohesion = 44.9f;
        var edge = HoldTheWallsRig.Army("edge");
        edge.Cohesion = 45f;
        _rig.Capture("town_A", weak);
        _rig.Capture("town_B", edge);

        _rig.Tick(0.0);

        CollectionAssert.AreEqual(new[] { "hold:edge:22" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void AFortUnderSiegeAtTheStart_IsNotHeld()
    {
        var army = HoldTheWallsRig.Army();
        army.FortUnderSiege = true;
        _rig.Capture("town_A", army);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void ALeaderInAMapEventAtTheStart_IsNotHeld()
    {
        var army = HoldTheWallsRig.Army();
        army.InMapEvent = true;
        _rig.Capture("town_A", army);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void AnArmyOfAnotherRealm_OrOutsideTheWalls_IsNotHeld()
    {
        var foreign = HoldTheWallsRig.Army("foreign");
        foreign.SameRealmAsFort = false;
        var outside = HoldTheWallsRig.Army("outside");
        outside.InFort = false;
        _rig.Capture("town_A", foreign);
        _rig.Capture("town_B", outside);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void AnArmyWhoseAiIsNotPaused_IsNotHeld_SoALateArrivalIsNeverPaused()
    {
        var late = HoldTheWallsRig.Army("late");
        late.AiPaused = false;
        _rig.Capture("town_A", late);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "vanilla paused only the capturing leaders");
    }

    [TestMethod]
    public void AThrowingFortRead_IsLogged_AndTheOtherCapturesAreStillJudged()
    {
        _rig.Capture("town_A", HoldTheWallsRig.Army("lord_1"));
        _rig.Capture("town_B", HoldTheWallsRig.Army("lord_2"));
        _rig.Adapter.ThrowingForts.Add("town_A");

        _rig.Tick(0.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_2:22" }, _rig.Adapter.Writes);
        _rig.Logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[HoldTheWalls]") && s.Contains("town_A") && s.Contains("boom")));
    }

    [TestMethod]
    public void AStarvingArmy_IsNotHeld_BecauseItWouldBeReleasedAtOnce()
    {
        var army = HoldTheWallsRig.Army();
        army.LowestFoodDays = 0.5f;
        _rig.Capture("town_A", army);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void NoArmyInTheFort_StartsNothing()
    {
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: true);

        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void TwoEventsForOneFort_GiveOneRecord_AndOneHold()
    {
        _rig.Capture();
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: true);

        _rig.Tick(1.0);
        _rig.Tick(1.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:21" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void ASecondCaptureOfTheSameFort_CountsTheHoldFromTheLaterCapture()
    {
        _rig.Capture();
        _rig.Adapter.Hours += 3.0;
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: true);

        _rig.Tick(1.0);

        CollectionAssert.AreEqual(new[] { "hold:lord_1:21" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void ACapture_IsJudgedOnce_TheFirstPassAfterIt()
    {
        var army = HoldTheWallsRig.Army();
        army.Cohesion = 30f;
        _rig.Capture("town_A", army);
        _rig.Tick();

        army.Cohesion = 90f;
        _rig.Tick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "a later recovery does not start a hold for a capture already judged");
    }

    [TestMethod]
    public void TwoArmiesInOneFort_BothHold()
    {
        _rig.Adapter.Put("town_A", HoldTheWallsRig.Army("lord_1"));
        _rig.Adapter.Put("town_A", HoldTheWallsRig.Army("lord_2"));
        _rig.Service.OnSettlementOwnerChanged("town_A", isFortification: true, bySiege: true);

        _rig.Tick(0.0);

        CollectionAssert.AreEquivalent(new[] { "hold:lord_1:22", "hold:lord_2:22" }, _rig.Adapter.Writes);
    }

    [TestMethod]
    public void ATickLongAfterTheCapture_StartsNoHoldThatHasAlreadyEnded()
    {
        _rig.Capture();

        _rig.Tick(23.0);

        Assert.AreEqual(0, _rig.Adapter.Writes.Count, "22 hours from the capture are already over");
    }

    [TestMethod]
    public void AnUnreadableClock_RecordsNothing()
    {
        _rig.Adapter.Hours = double.NaN;
        _rig.Capture();
        _rig.Adapter.Hours = 1001.0;

        _rig.Service.OnHourlyTick();

        Assert.AreEqual(0, _rig.Adapter.Writes.Count);
    }

    [TestMethod]
    public void AnArmyThatHoldsOneFort_ThenCapturesAnother_HoldsTheSecond()
    {
        var army = _rig.Capture("town_A");
        _rig.Tick(1.0);
        Assert.AreEqual(1, _rig.Adapter.Writes.Count);

        _rig.Adapter.Forts["town_A"].Clear();
        _rig.Adapter.Put("town_B", army);
        _rig.Service.OnSettlementOwnerChanged("town_B", isFortification: true, bySiege: true);
        _rig.Tick(1.0);

        Assert.AreEqual("hold:lord_1:21", _rig.Adapter.Writes[1]);
        _rig.Adapter.Hours += 1.0;
        army.FortUnderSiege = true;
        _rig.Service.OnHourlyTick();
        Assert.AreEqual(3, _rig.Adapter.Writes.Count, "one record for the army: released once");
        Assert.AreEqual("hold:lord_1:8", _rig.Adapter.Writes[2], "restoring what is left of the second capture's 10 hours");
    }
}
