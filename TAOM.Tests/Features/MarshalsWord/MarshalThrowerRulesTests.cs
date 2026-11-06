using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The pure rules of throwers on an advance (build-spec 9.4): who may stand, how far, where, and how each check reads.</summary>
[TestClass]
public class MarshalThrowerRulesTests
{
    private static readonly ThrowerCensus Forty = new(Units: 40, Armed: 40, Ammo: 120);

    private static ThrowerGates Open() => new(
        SettingOn: true, FieldBattle: true, Deployment: false, Advance: true, AiControlled: false, Held: false, OnFireMode: false, HoldFire: false,
        OpenArrangement: true, RangedBranch: false, HasTarget: true, Range: 80f, OwnDepth: 6f, Own: new GroundPoint(0f, 0f), Target: new GroundPoint(0f, 100f),
        TargetDepth: 6f);

    private static readonly (string Reason, Func<ThrowerGates, ThrowerGates> Spoil)[] Reasons =
    {
        ("setting-off", g => g with { SettingOn = false }),
        ("not-field", g => g with { FieldBattle = false }),
        ("deployment", g => g with { Deployment = true }),
        ("not-advance", g => g with { Advance = false }),
        ("ai-control", g => g with { AiControlled = true }),
        ("held", g => g with { Held = true }),
        ("fire-mode", g => g with { OnFireMode = true }),
        ("hold-fire", g => g with { HoldFire = true }),
        ("arrangement", g => g with { OpenArrangement = false }),
        ("ranged", g => g with { RangedBranch = true }),
        ("no-target", g => g with { HasTarget = false }),
        ("engaged", g => g with { Target = new GroundPoint(0f, 15f) }),
        ("short-range", g => g with { Range = 10f }),
    };

    [TestMethod]
    public void WhyNot_EveryGateOpenAndAFullCompany_IsEmpty()
    {
        Assert.AreEqual("", MarshalThrowerRules.WhyNot(Open(), Forty));
    }

    [TestMethod]
    public void WhyNot_EachReasonOnItsOwn_IsNamed()
    {
        foreach (var (reason, spoil) in Reasons)
            Assert.AreEqual(reason, MarshalThrowerRules.WhyNot(spoil(Open()), Forty), reason);
    }

    [TestMethod]
    public void WhyNot_TooFewArmedOnItsOwn_IsNamed()
    {
        Assert.AreEqual("too-few", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(40, 19, 50)));
    }

    [TestMethod]
    public void WhyNot_SeveralReasons_NamesTheFirstInTheContractsOrder()
    {
        var gates = Open();
        for (var from = 0; from < Reasons.Length; from++)
        {
            var spoiled = gates;
            for (var i = from; i < Reasons.Length; i++)
                spoiled = Reasons[i].Spoil(spoiled);
            Assert.AreEqual(Reasons[from].Reason, MarshalThrowerRules.WhyNot(spoiled, Forty), "from " + Reasons[from].Reason);
        }
    }

    [TestMethod]
    public void WhyNot_TooFewComesBeforeShortRange()
    {
        Assert.AreEqual("too-few", MarshalThrowerRules.WhyNot(Open() with { Range = 10f }, new ThrowerCensus(40, 3, 3)));
    }

    [TestMethod]
    public void WhyNot_EngagedComesBeforeTooFew_AndBeforeAnyRangeRead()
    {
        var near = Open() with { Range = null, Target = new GroundPoint(0f, 15f) };

        Assert.AreEqual("engaged", MarshalThrowerRules.WhyNot(near, new ThrowerCensus(40, 3, 3)));
        Assert.AreEqual("engaged", MarshalThrowerRules.WhyNot(near, Forty));
    }

    [TestMethod]
    public void WhyNot_TheFrontGapAtTwelveMetres_IsEngaged_AndTwelvePointFiveIsNot()
    {
        Assert.AreEqual("engaged", MarshalThrowerRules.WhyNot(Open() with { Target = new GroundPoint(0f, 15f) }, Forty), "12 m of gap: distance 15 less half of the 6 m depth");
        Assert.AreEqual("", MarshalThrowerRules.WhyNot(Open() with { Target = new GroundPoint(0f, 15.5f), Range = null }, Forty));
    }

    [TestMethod]
    public void WhyNot_APositionThatIsNotANumber_Refuses_ForAGapThatIsNotANumberIsNeverOpen()
    {
        Assert.AreEqual("engaged", MarshalThrowerRules.WhyNot(Open() with { Target = new GroundPoint(float.NaN, 0f), Range = null }, Forty));
    }

    [TestMethod]
    public void WhyNot_TwentyArmedOfForty_Qualifies_AndNineteenDoesNot()
    {
        Assert.AreEqual("", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(40, 20, 60)));
        Assert.AreEqual("too-few", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(40, 19, 57)));
    }

    [TestMethod]
    public void WhyNot_TheLeastArmedIsFive_EvenForASmallCompany()
    {
        Assert.AreEqual("", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(8, 5, 15)));
        Assert.AreEqual("too-few", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(8, 4, 12)));
        Assert.AreEqual("too-few", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(12, 5, 15)));
    }

    [TestMethod]
    public void WhyNot_ACensusOfNoUnits_IsTooFew()
    {
        Assert.AreEqual("too-few", MarshalThrowerRules.WhyNot(Open(), new ThrowerCensus(0, 0, 0)));
    }

    [TestMethod]
    public void WhyNot_ARangeNotRead_SkipsTheRangeTest()
    {
        Assert.AreEqual("", MarshalThrowerRules.WhyNot(Open() with { Range = null }, Forty));
    }

    [TestMethod]
    public void WhyNot_ARangeThatIsNotAUsableNumber_IsShortRange()
    {
        foreach (var range in new[] { float.NaN, 0f, -5f, float.PositiveInfinity, float.NegativeInfinity })
            Assert.AreEqual("short-range", MarshalThrowerRules.WhyNot(Open() with { Range = range }, Forty), range.ToString());
        Assert.AreEqual("short-range", MarshalThrowerRules.WhyNot(Open() with { OwnDepth = float.NaN }, Forty));
    }

    [TestMethod]
    public void ShortRange_TheReachBoundary_IsWhereTheRearRankStopsReachingTheFront()
    {
        Assert.IsTrue(MarshalThrowerRules.ShortRange(37.4f, 6f), "0.8 * 37.4 is under the 24 m stand gap plus 6 m of depth");
        Assert.IsFalse(MarshalThrowerRules.ShortRange(37.6f, 6f));
        Assert.IsFalse(MarshalThrowerRules.ShortRange(100f, 6f));
        Assert.IsTrue(MarshalThrowerRules.ShortRange(100f, 60f), "a deep company reaches less");
    }

    [TestMethod]
    public void StandGap_ALowRange_ClampsToTheNearestGap()
    {
        Assert.AreEqual(24f, MarshalThrowerRules.StandGap(40f), 1e-4f);
    }

    [TestMethod]
    public void StandGap_AHighRange_ClampsToTheFarthestGap()
    {
        Assert.AreEqual(32f, MarshalThrowerRules.StandGap(100f), 1e-4f);
    }

    [TestMethod]
    public void StandGap_ARangeBetween_IsAShareOfTheRange()
    {
        Assert.AreEqual(27f, MarshalThrowerRules.StandGap(60f), 1e-4f);
    }

    [TestMethod]
    public void StandDistance_IsTheGapPlusHalfTheTargetsDepth()
    {
        Assert.AreEqual(33f, MarshalThrowerRules.StandDistance(30f, 6f), 1e-4f);
    }

    [TestMethod]
    public void Gap_IsTheDistanceFromTheCompanysFrontToTheTargetsFront()
    {
        Assert.AreEqual(47f, MarshalThrowerRules.Gap(new GroundPoint(0f, 0f), new GroundPoint(0f, 50f), 6f), 1e-4f, "the company's position is its front; the target's is its middle");
        Assert.AreEqual(47f, MarshalThrowerRules.Gap(new GroundPoint(0f, 50f), new GroundPoint(0f, 0f), 6f), 1e-4f, "symmetric");
    }

    [TestMethod]
    public void Gap_Overlapping_IsFlooredAtZero()
    {
        Assert.AreEqual(0f, MarshalThrowerRules.Gap(new GroundPoint(0f, 0f), new GroundPoint(0f, 3f), 6f), 1e-4f);
    }

    [TestMethod]
    public void Gap_APositionThatIsNotANumber_IsNotANumber()
    {
        Assert.IsTrue(float.IsNaN(MarshalThrowerRules.Gap(new GroundPoint(float.NaN, 0f), new GroundPoint(0f, 3f), 6f)));
    }

    [TestMethod]
    public void Approach_AheadOfTheCompany_WalksToThePointShortOfTheTarget()
    {
        var answer = MarshalThrowerRules.Approach(new GroundPoint(0f, 0f), new GroundPoint(0f, 100f), 30f);

        Assert.AreEqual(ApproachKind.Walk, answer.Kind);
        Assert.AreEqual(0f, answer.Point.X, 1e-4f);
        Assert.AreEqual(70f, answer.Point.Y, 1e-4f);
        Assert.AreEqual(0f, answer.DirX, 1e-4f);
        Assert.AreEqual(1f, answer.DirY, 1e-4f);
    }

    [TestMethod]
    public void Approach_ThePointBehindTheCompany_IsStandHere_AtTheCompanysOwnPosition()
    {
        var answer = MarshalThrowerRules.Approach(new GroundPoint(0f, 80f), new GroundPoint(0f, 100f), 30f);

        Assert.AreEqual(ApproachKind.StandHere, answer.Kind);
        Assert.AreEqual(80f, answer.Point.Y, 1e-4f);
        Assert.AreEqual(0f, answer.Point.X, 1e-4f);
    }

    [TestMethod]
    public void Approach_ThePointLevelWithTheCompany_IsStandHere()
    {
        Assert.AreEqual(ApproachKind.StandHere, MarshalThrowerRules.Approach(new GroundPoint(0f, 70f), new GroundPoint(0f, 100f), 30f).Kind);
    }

    [TestMethod]
    public void Approach_TwoPositionsTheSame_HasNoPoint()
    {
        Assert.AreEqual(ApproachKind.NoPoint, MarshalThrowerRules.Approach(new GroundPoint(5f, 5f), new GroundPoint(5f, 5f), 30f).Kind);
    }

    [TestMethod]
    public void Approach_AnythingThatIsNotANumber_HasNoPoint()
    {
        Assert.AreEqual(ApproachKind.NoPoint, MarshalThrowerRules.Approach(new GroundPoint(float.NaN, 0f), new GroundPoint(0f, 100f), 30f).Kind);
        Assert.AreEqual(ApproachKind.NoPoint, MarshalThrowerRules.Approach(new GroundPoint(0f, 0f), new GroundPoint(0f, float.PositiveInfinity), 30f).Kind);
        Assert.AreEqual(ApproachKind.NoPoint, MarshalThrowerRules.Approach(new GroundPoint(0f, 0f), new GroundPoint(0f, 100f), float.NaN).Kind);
    }

    [TestMethod]
    public void JudgeStand_AStandWithinTheOvershoot_Passes()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Pass, ""), MarshalThrowerRules.JudgeStand(true, false, 6f, false));
    }

    [TestMethod]
    public void JudgeStand_AnOvershootPastTheLimit_FailsWalkedIn()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "walked-in"), MarshalThrowerRules.JudgeStand(true, false, 6.1f, false));
    }

    [TestMethod]
    public void JudgeStand_AGapClosedBeforeTheStand_FailsEvenAfterAStand()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "closed-before-stand"), MarshalThrowerRules.JudgeStand(true, true, 0f, false));
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "closed-before-stand"), MarshalThrowerRules.JudgeStand(false, true, 0f, false));
    }

    [TestMethod]
    public void JudgeStand_AFault_FailsBeforeAnythingElse()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "fault"), MarshalThrowerRules.JudgeStand(true, true, 99f, true));
    }

    [TestMethod]
    public void JudgeStand_ARunThatNeverStood_Skips()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Skip, "released-before-stand"), MarshalThrowerRules.JudgeStand(false, false, 0f, false));
    }

    [TestMethod]
    public void JudgeStand_ARunThatNeverStoodButWalkedPastItsPoint_FailsWalkedIn()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "walked-in"), MarshalThrowerRules.JudgeStand(false, false, 6.1f, false));
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Skip, "released-before-stand"), MarshalThrowerRules.JudgeStand(false, false, 6f, false));
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "closed-before-stand"), MarshalThrowerRules.JudgeStand(false, true, 20f, false));
    }

    [TestMethod]
    public void JudgeStand_AnOvershootThatIsNotANumber_Fails()
    {
        Assert.AreEqual(ThrowerVerdictKind.Fail, MarshalThrowerRules.JudgeStand(true, false, float.NaN, false).Kind);
    }

    [TestMethod]
    public void JudgeThrow_TenShots_Pass_NineShotsAfterTheWindow_Fail()
    {
        Assert.AreEqual(ThrowerVerdictKind.Pass, MarshalThrowerRules.JudgeThrow(true, 3.0, 10).Kind);
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "few-throws"), MarshalThrowerRules.JudgeThrow(true, 10.0, 9));
    }

    [TestMethod]
    public void JudgeThrow_FewShotsInAShortStand_Skip()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Skip, "short-stand"), MarshalThrowerRules.JudgeThrow(true, 9.9, 0));
    }

    [TestMethod]
    public void JudgeThrow_ARunThatNeverStood_Skips()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Skip, "never-stood"), MarshalThrowerRules.JudgeThrow(false, 0.0, 50));
    }

    [TestMethod]
    public void JudgeMelee_BlowsThatRoseWhileTheOrderIsAdvance_Pass()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Pass, ""), MarshalThrowerRules.JudgeMelee(true, false, false, true, 3f, 3.0));
    }

    [TestMethod]
    public void JudgeMelee_NoBlowsWithinTheWait_Fail_AndBeforeItIsPending()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "no-melee"), MarshalThrowerRules.JudgeMelee(true, false, false, false, 0f, 45.0));
        Assert.IsNull(MarshalThrowerRules.JudgeMelee(true, false, false, false, 0f, 44.9));
    }

    [TestMethod]
    public void JudgeMelee_BlowsWithoutTheCompanyGoingIn_FailNoAdvance_AndBeforeTheWaitArePending()
    {
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "no-advance"), MarshalThrowerRules.JudgeMelee(true, false, false, true, 2.9f, 45.0));
        Assert.IsNull(MarshalThrowerRules.JudgeMelee(true, false, false, true, 2.9f, 44.9));
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "no-advance"), MarshalThrowerRules.JudgeMelee(true, false, false, true, float.NaN, 45.0));
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "no-melee"), MarshalThrowerRules.JudgeMelee(true, false, false, false, 10f, 45.0));
    }

    [TestMethod]
    public void JudgeMelee_EachWayTheJudgmentCannotBeMade_IsASkipWithItsOwnReason()
    {
        Assert.AreEqual("order-changed", MarshalThrowerRules.JudgeMelee(false, false, false, true, 3f, 3.0)!.Value.Reason);
        Assert.AreEqual("emptied", MarshalThrowerRules.JudgeMelee(true, true, false, true, 3f, 3.0)!.Value.Reason);
        Assert.AreEqual("ai-control", MarshalThrowerRules.JudgeMelee(true, false, true, true, 3f, 3.0)!.Value.Reason);
        Assert.AreEqual(ThrowerVerdictKind.Skip, MarshalThrowerRules.JudgeMelee(false, false, false, false, 0f, 100.0)!.Value.Kind);
    }

    private const long TenMegahertz = 10_000_000;

    [TestMethod]
    public void CostVerdict_UnderTheMinimumCalls_Skips_AndAtItJudges()
    {
        var skipped = MarshalThrowerRules.CostVerdict(299, 299 * 500, 500, TenMegahertz);
        Assert.AreEqual(ThrowerVerdictKind.Skip, skipped.Kind);
        Assert.AreEqual("too-few-calls", skipped.Reason);
        Assert.AreEqual(ThrowerVerdictKind.Pass, MarshalThrowerRules.CostVerdict(300, 300 * 500, 500, TenMegahertz).Kind);
    }

    [TestMethod]
    public void CostVerdict_AFrequencyOfZero_Skips()
    {
        Assert.AreEqual(ThrowerVerdictKind.Skip, MarshalThrowerRules.CostVerdict(1000, 500_000, 500, 0).Kind);
    }

    [TestMethod]
    public void CostVerdict_TheMeanAtFiftyMicroseconds_Passes_AndAboveItFails()
    {
        Assert.AreEqual(ThrowerVerdictKind.Pass, MarshalThrowerRules.CostVerdict(1000, 500_000, 500, TenMegahertz).Kind);
        var fail = MarshalThrowerRules.CostVerdict(1000, 501_000, 501, TenMegahertz);
        Assert.AreEqual(ThrowerVerdictKind.Fail, fail.Kind);
        Assert.AreEqual("mean", fail.Reason);
        Assert.AreEqual(50.1, fail.MeanMicros, 1e-9);
    }

    [TestMethod]
    public void CostVerdict_TheMostAtFiveMilliseconds_Passes_AndAboveItFails()
    {
        Assert.AreEqual(ThrowerVerdictKind.Pass, MarshalThrowerRules.CostVerdict(1000, 400_000, 50_000, TenMegahertz).Kind);
        var fail = MarshalThrowerRules.CostVerdict(1000, 400_000, 50_001, TenMegahertz);
        Assert.AreEqual(new ThrowerVerdict(ThrowerVerdictKind.Fail, "max"), new ThrowerVerdict(fail.Kind, fail.Reason));
        Assert.AreEqual(5000.1, fail.MaxMicros, 1e-9);
    }

    [TestMethod]
    public void CostVerdict_MeanAndMostBothOver_NamesTheMeanFirst()
    {
        Assert.AreEqual("mean", MarshalThrowerRules.CostVerdict(1000, 2_000_000, 80_000, TenMegahertz).Reason);
    }
}
