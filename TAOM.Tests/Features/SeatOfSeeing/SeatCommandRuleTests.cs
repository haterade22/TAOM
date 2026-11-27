using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>The command mouse's pure rules: when it is live, when one order may be given, click against drag, the selection and the order a click gives, and the pick.</summary>
[TestClass]
public class SeatCommandRuleTests
{
    private static SeatCommandFacts Ready() => new(MainAgentExists: true, Deployment: false, EnginePlayerControlled: true, HeroFightsOn: false,
        HasBots: true, HasTroopsUnderThem: true, PlayerGeneralOrSergeant: true, MissionEnding: false, BattleInRetreat: false);

    // ---- live ----

    [TestMethod]
    public void IsLive_WithEveryConditionMet_IsTrue() => Assert.IsTrue(SeatCommandRule.IsLive(true, true, true, false));

    [TestMethod]
    public void IsLive_WithTheSettingOff_IsFalse() => Assert.IsFalse(SeatCommandRule.IsLive(false, true, true, false));

    [TestMethod]
    public void IsLive_WhenNotSeated_IsFalse() => Assert.IsFalse(SeatCommandRule.IsLive(true, false, true, false));

    [TestMethod]
    public void IsLive_WhenTheKindGivesNoOrders_IsFalse() => Assert.IsFalse(SeatCommandRule.IsLive(true, true, false, false));

    [TestMethod]
    public void IsLive_InANetworkSession_IsFalse() => Assert.IsFalse(SeatCommandRule.IsLive(true, true, true, true));

    // ---- the panel's own gate ----

    [TestMethod]
    public void MayCommand_WhenTheHeroLivesAndTroopsStand_IsTrue() => Assert.IsTrue(SeatCommandRule.MayCommand(Ready()));

    [TestMethod]
    public void Refusal_WithNoMainAgent_IsHeroGone()
    {
        var facts = Ready() with { MainAgentExists = false };

        Assert.AreEqual(SeatCommandRefusal.HeroGone, SeatCommandRule.Refusal(facts));
        Assert.IsFalse(SeatCommandRule.MayCommand(facts));
    }

    [TestMethod]
    public void Refusal_WithAHeroTheEngineDoesNotControl_IsNotPlayerControlled() =>
        Assert.AreEqual(SeatCommandRefusal.NotPlayerControlled, SeatCommandRule.Refusal(Ready() with { EnginePlayerControlled = false }));

    [TestMethod]
    public void MayCommand_WhenTheHeroFightsOnUnderTheAi_CountsHimAsControlled() =>
        Assert.IsTrue(SeatCommandRule.MayCommand(Ready() with { EnginePlayerControlled = false, HeroFightsOn = true }));

    [TestMethod]
    public void MayCommand_InDeployment_SkipsTheControlTest() =>
        Assert.IsTrue(SeatCommandRule.MayCommand(Ready() with { EnginePlayerControlled = false, Deployment = true }));

    [TestMethod]
    public void Refusal_AfterTheHeroFellAndBeforeATake_IsHeroGoneNotNoTroops() =>
        Assert.AreEqual(SeatCommandRefusal.HeroGone, SeatCommandRule.Refusal(Ready() with { MainAgentExists = false, HasTroopsUnderThem = false }));

    [TestMethod]
    public void Refusal_WithNoBots_IsNoTroops() =>
        Assert.AreEqual(SeatCommandRefusal.NoTroops, SeatCommandRule.Refusal(Ready() with { HasBots = false }));

    [TestMethod]
    public void Refusal_WithNoTroopsUnderThePlayer_IsNoTroops() =>
        Assert.AreEqual(SeatCommandRefusal.NoTroops, SeatCommandRule.Refusal(Ready() with { HasTroopsUnderThem = false }));

    [TestMethod]
    public void Refusal_WhenThePlayerIsNeitherGeneralNorSergeant_IsNoTroops() =>
        Assert.AreEqual(SeatCommandRefusal.NoTroops, SeatCommandRule.Refusal(Ready() with { PlayerGeneralOrSergeant = false }));

    [TestMethod]
    public void Refusal_WhenTheMissionIsEndingNotInRetreat_IsMissionEnding() =>
        Assert.AreEqual(SeatCommandRefusal.MissionEnding, SeatCommandRule.Refusal(Ready() with { MissionEnding = true }));

    [TestMethod]
    public void MayCommand_WhenTheMissionIsEndingInARetreat_IsTrue() =>
        Assert.IsTrue(SeatCommandRule.MayCommand(Ready() with { MissionEnding = true, BattleInRetreat = true }));

    [TestMethod]
    public void Reason_NamesEveryRefusal()
    {
        Assert.AreEqual("hero-gone", SeatCommandRule.Reason(SeatCommandRefusal.HeroGone));
        Assert.AreEqual("not-controlled", SeatCommandRule.Reason(SeatCommandRefusal.NotPlayerControlled));
        Assert.AreEqual("no-troops", SeatCommandRule.Reason(SeatCommandRefusal.NoTroops));
        Assert.AreEqual("mission-ending", SeatCommandRule.Reason(SeatCommandRefusal.MissionEnding));
    }

    // ---- the shield ----

    [TestMethod]
    public void ShieldHolds_WithTheCursorShownAndTheCommandMouseLive_IsTrue() => Assert.IsTrue(SeatCommandRule.ShieldHolds(true, true, false, false));

    [TestMethod]
    public void ShieldHolds_WithNoCursor_IsFalse() => Assert.IsFalse(SeatCommandRule.ShieldHolds(false, true, false, false));

    [TestMethod]
    public void ShieldHolds_WhenTheCommandMouseIsNotLive_IsFalse() => Assert.IsFalse(SeatCommandRule.ShieldHolds(true, false, false, false));

    [TestMethod]
    public void ShieldHolds_WhileTheMiddleButtonLooks_IsFalse() => Assert.IsFalse(SeatCommandRule.ShieldHolds(true, true, true, false));

    [TestMethod]
    public void ShieldHolds_WhenAnotherOverlayClaimsThePointer_YieldsToIt() => Assert.IsFalse(SeatCommandRule.ShieldHolds(true, true, false, true));

    // ---- click against drag ----

    [TestMethod]
    public void IsClick_WithASmallMoveAndALongHold_IsAClick() => Assert.IsTrue(SeatClickRule.IsClick(100f, 100f, 105f, 95f, 2f));

    [TestMethod]
    public void IsClick_WithABigMoveReleasedQuickly_IsAClick() => Assert.IsTrue(SeatClickRule.IsClick(100f, 100f, 300f, 300f, 0.1f));

    [TestMethod]
    public void IsClick_WithABigMoveAndALongHold_IsNotAClick()
    {
        Assert.IsFalse(SeatClickRule.IsClick(100f, 100f, 300f, 100f, 0.5f));
        Assert.IsTrue(SeatClickRule.IsDrag(100f, 100f, 300f, 100f, 0.5f));
    }

    [TestMethod]
    public void IsClick_AtTenPixelsOnOneAxis_IsTheEdgeOfADrag()
    {
        Assert.IsTrue(SeatClickRule.IsClick(100f, 100f, 109.9f, 109.9f, 1f));
        Assert.IsFalse(SeatClickRule.IsClick(100f, 100f, 110f, 100f, 1f));
        Assert.IsFalse(SeatClickRule.IsClick(100f, 100f, 100f, 90f, 1f));
    }

    [TestMethod]
    public void IsClick_AtThreeTenthsOfASecond_IsNoLongerQuick()
    {
        Assert.IsTrue(SeatClickRule.IsClick(0f, 0f, 50f, 50f, 0.299f));
        Assert.IsFalse(SeatClickRule.IsClick(0f, 0f, 50f, 50f, 0.3f));
    }

    // ---- selection ----

    [TestMethod]
    public void Decide_PlainClickOnAnUnselectedCompany_ReplacesTheSelection() =>
        Assert.AreEqual(SeatSelectAction.ReplaceWith, SeatSelectRule.Decide(false, true, false, 2));

    [TestMethod]
    public void Decide_PlainClickOnTheOnlySelectedCompany_ChangesNothing() =>
        Assert.AreEqual(SeatSelectAction.None, SeatSelectRule.Decide(false, true, true, 1));

    [TestMethod]
    public void Decide_PlainClickOnOneOfSeveralSelected_NarrowsToIt() =>
        Assert.AreEqual(SeatSelectAction.ReplaceWith, SeatSelectRule.Decide(false, true, true, 3));

    [TestMethod]
    public void Decide_ShiftClickOnAnUnselectedCompany_AddsIt() =>
        Assert.AreEqual(SeatSelectAction.Add, SeatSelectRule.Decide(true, true, false, 1));

    [TestMethod]
    public void Decide_ShiftClickOnASelectedCompany_RemovesIt() =>
        Assert.AreEqual(SeatSelectAction.Remove, SeatSelectRule.Decide(true, true, true, 2));

    [TestMethod]
    public void Decide_PlainClickOnNothing_ClearsTheSelection() =>
        Assert.AreEqual(SeatSelectAction.ClearAll, SeatSelectRule.Decide(false, false, false, 2));

    [TestMethod]
    public void Decide_PlainClickOnNothingWithNothingSelected_ChangesNothing() =>
        Assert.AreEqual(SeatSelectAction.None, SeatSelectRule.Decide(false, false, false, 0));

    [TestMethod]
    public void Decide_ShiftClickOnNothing_KeepsTheSelection() =>
        Assert.AreEqual(SeatSelectAction.None, SeatSelectRule.Decide(true, false, false, 2));

    // ---- the order a right click gives ----

    [TestMethod]
    public void Decide_OnAnEnemyCompany_Charges() =>
        Assert.AreEqual(SeatOrderKind.Charge, SeatOrderRule.Decide(true, true, true, true));

    [TestMethod]
    public void Decide_OnAnOrderableObject_UsesIt() =>
        Assert.AreEqual(SeatOrderKind.Orderable, SeatOrderRule.Decide(true, false, true, true));

    [TestMethod]
    public void Decide_OnValidGround_Moves() =>
        Assert.AreEqual(SeatOrderKind.Move, SeatOrderRule.Decide(true, false, false, true));

    [TestMethod]
    public void Decide_OnGroundThatTakesNoCompany_GivesNoOrder() =>
        Assert.AreEqual(SeatOrderKind.None, SeatOrderRule.Decide(true, false, false, false));

    [TestMethod]
    public void Decide_WithNothingSelected_GivesNoOrderAtAll()
    {
        Assert.AreEqual(SeatOrderKind.None, SeatOrderRule.Decide(false, true, true, true));
        Assert.AreEqual("nothing-selected", SeatOrderRule.Reason(false));
        Assert.AreEqual("no-target", SeatOrderRule.Reason(true));
    }

    // ---- the pick ----

    private static IReadOnlyList<FordsControlCandidate> Units(params (float X, float Y)[] at)
    {
        var list = new List<FordsControlCandidate>();
        foreach (var (x, y) in at)
            list.Add(new FordsControlCandidate(x, y, false));
        return list;
    }

    [TestMethod]
    public void Winner_NamesTheNearestUnitInReach()
    {
        var units = Units((500f, 500f), (520f, 505f), (900f, 900f));

        Assert.AreEqual(1, SeatPick.Winner(units, 522f, 506f, 1080f));
    }

    [TestMethod]
    public void Winner_WithNoUnitInReach_IsMinusOne()
    {
        var units = Units((100f, 100f));

        Assert.AreEqual(-1, SeatPick.Winner(units, 800f, 800f, 1080f));
    }

    [TestMethod]
    public void Winner_ReachGrowsWithTheScreen()
    {
        var units = Units((100f, 100f));

        Assert.AreEqual(-1, SeatPick.Winner(units, 100f, 150f, 1080f), "50 px is outside 4 percent of 1080");
        Assert.AreEqual(0, SeatPick.Winner(units, 100f, 150f, 2160f), "and inside 4 percent of 2160");
    }

    [TestMethod]
    public void Winner_WithTwoUnitsInReach_NamesTheNearer()
    {
        var units = new List<FordsControlCandidate> { new(100f, 100f, false), new(104f, 100f, false) };

        Assert.AreEqual(0, SeatPick.Winner(units, 99f, 100f, 1080f));
    }

    [TestMethod]
    public void TestedDistance_StopsShortOfTheChestAndNeverGoesBelowZero()
    {
        Assert.AreEqual(19.25f, SeatPick.TestedDistance(20f), 1e-4f);
        Assert.AreEqual(0f, SeatPick.TestedDistance(0.2f));
    }

    [TestMethod]
    public void TestedDistance_WithTheKeepRule_HidesASoldierBehindARise()
    {
        // Terrain 10 m away, the soldier's chest 30 m away: the ground hides him. Terrain 29.9 m away: it is only his own feet.
        Assert.IsFalse(SeatKeepRule.AgentCounts(true, 10f, SeatPick.TestedDistance(30f)));
        Assert.IsTrue(SeatKeepRule.AgentCounts(true, 29.9f, SeatPick.TestedDistance(30f)));
        Assert.IsTrue(SeatKeepRule.AgentCounts(false, float.NaN, SeatPick.TestedDistance(30f)));
    }
}
