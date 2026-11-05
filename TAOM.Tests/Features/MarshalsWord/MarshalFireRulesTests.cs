using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

[TestClass]
public class MarshalFireRulesTests
{
    private const double Now = 100.0;
    private const double Since = 90.0;

    private static DrawInput Input(bool attacking = false, bool cancelling = false, bool standAside = false, bool wordDue = false) =>
        new(attacking, cancelling, standAside, wordDue, Now);

    [TestMethod]
    public void Step_HoldingAndStandingAside_GoesIdleAndReleases()
    {
        var step = MarshalFireRules.Step(DrawState.Holding, Since, Input(standAside: true));

        Assert.AreEqual(new DrawStep(DrawState.Idle, Now, DrawAction.Release), step);
    }

    [TestMethod]
    [DataRow((int)DrawState.Idle)]
    [DataRow((int)DrawState.Drawing)]
    public void Step_IdleOrDrawingAndStandingAside_GoesIdleAndLeavesTheFlagsAlone(int state)
    {
        var step = MarshalFireRules.Step((DrawState)state, Since, Input(attacking: true, standAside: true));

        Assert.AreEqual(DrawState.Idle, step.State);
        Assert.AreEqual(DrawAction.Leave, step.Action);
    }

    [TestMethod]
    public void Step_IdleAndAttacking_StartsDrawingNow()
    {
        var step = MarshalFireRules.Step(DrawState.Idle, Since, Input(attacking: true));

        Assert.AreEqual(new DrawStep(DrawState.Drawing, Now, DrawAction.Leave), step);
    }

    [TestMethod]
    public void Step_IdleAndNotAttacking_StaysIdleWithItsOwnTime()
    {
        var step = MarshalFireRules.Step(DrawState.Idle, Since, Input());

        Assert.AreEqual(new DrawStep(DrawState.Idle, Since, DrawAction.Leave), step);
    }

    [TestMethod]
    public void Step_DrawingAndStillAttacking_KeepsTheTimeTheDrawBegan()
    {
        var step = MarshalFireRules.Step(DrawState.Drawing, Since, Input(attacking: true));

        Assert.AreEqual(new DrawStep(DrawState.Drawing, Since, DrawAction.Leave), step);
    }

    [TestMethod]
    public void Step_DrawingThenTheAiCancels_GoesIdleAndLeavesTheFlagsAlone()
    {
        var step = MarshalFireRules.Step(DrawState.Drawing, Since, Input(cancelling: true));

        Assert.AreEqual(DrawState.Idle, step.State);
        Assert.AreEqual(DrawAction.Leave, step.Action);
    }

    [TestMethod]
    public void Step_DrawingThenTheAiDropsTheAttackWithNoCancel_StartsHolding()
    {
        var step = MarshalFireRules.Step(DrawState.Drawing, Since, Input());

        Assert.AreEqual(new DrawStep(DrawState.Holding, Now, DrawAction.Hold), step);
    }

    [TestMethod]
    public void Step_HoldingWithNoWordUnderTheLimit_KeepsHoldingFromTheSameTime()
    {
        var step = MarshalFireRules.Step(DrawState.Holding, Since, Input(attacking: true));

        Assert.AreEqual(new DrawStep(DrawState.Holding, Since, DrawAction.Hold), step);
    }

    [TestMethod]
    public void Step_HoldingWhenTheWordIsDue_GoesIdleAndReleases()
    {
        var step = MarshalFireRules.Step(DrawState.Holding, Since, Input(wordDue: true));

        Assert.AreEqual(new DrawStep(DrawState.Idle, Now, DrawAction.Release), step);
    }

    [TestMethod]
    public void Step_HoldingForTheHoldLimit_ReleasesWithNoWord()
    {
        var step = MarshalFireRules.Step(DrawState.Holding, Now - MarshalTuning.HoldLimit, Input());

        Assert.AreEqual(DrawAction.Release, step.Action);
        Assert.AreEqual(DrawState.Idle, step.State);
    }

    [TestMethod]
    public void Step_HoldingJustUnderTheHoldLimit_StillHolds()
    {
        var step = MarshalFireRules.Step(DrawState.Holding, Now - MarshalTuning.HoldLimit + 0.01, Input());

        Assert.AreEqual(DrawAction.Hold, step.Action);
    }

    [TestMethod]
    public void WordDue_AWordGivenAfterTheHoldBegan_IsDueOnceTheJitterHasPassed()
    {
        Assert.IsFalse(MarshalFireRules.WordDue(wordTime: 95.0, since: 90.0, jitter: 0.3, now: 95.2));
        Assert.IsTrue(MarshalFireRules.WordDue(95.0, 90.0, 0.3, 95.3));
    }

    [TestMethod]
    public void WordDue_AWordGivenBeforeTheHoldBegan_NeverReleasesThisSoldier()
    {
        Assert.IsFalse(MarshalFireRules.WordDue(wordTime: 89.9, since: 90.0, jitter: 0.0, now: 200.0));
    }

    [TestMethod]
    public void WordDue_AWordGivenAtTheSecondTheHoldBegan_Releases()
    {
        Assert.IsTrue(MarshalFireRules.WordDue(wordTime: 90.0, since: 90.0, jitter: 0.0, now: 90.0));
    }

    [TestMethod]
    public void WordDue_NoWordYet_IsNeverDue()
    {
        Assert.IsFalse(MarshalFireRules.WordDue(double.NegativeInfinity, 90.0, 0.0, 1e9));
    }

    [TestMethod]
    public void NextMode_CyclesFreeAndOnWord_InThisSlice()
    {
        Assert.AreEqual(FireMode.OnWord, MarshalFireRules.NextMode(FireMode.Free));
        Assert.AreEqual(FireMode.Free, MarshalFireRules.NextMode(FireMode.OnWord));
    }

    private static StandAsideFacts Calm() => new(false, false, false, false, false, false, double.PositiveInfinity);

    [TestMethod]
    public void StandsAside_AllCalm_IsFalse()
    {
        Assert.IsFalse(MarshalFireRules.StandsAside(Calm()));
    }

    [TestMethod]
    public void StandsAside_ModeFree_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { ModeFree = true }));
    }

    [TestMethod]
    public void StandsAside_NoFormation_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { NoFormation = true }));
    }

    [TestMethod]
    public void StandsAside_NoRangedWeaponWielded_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { NoRangedWielded = true }));
    }

    [TestMethod]
    public void StandsAside_TheCompanyIsOnHoldFire_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { HoldFireOrder = true }));
    }

    [TestMethod]
    public void StandsAside_DetachedFromTheFormation_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { Detached = true }));
    }

    [TestMethod]
    public void StandsAside_UsingAGameObject_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { UsingObject = true }));
    }

    [TestMethod]
    public void StandsAside_ATargetInsideTheCloseQuarter_IsTrue()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { TargetDistance = MarshalTuning.CloseQuarter - 0.1 }));
    }

    [TestMethod]
    public void StandsAside_ATargetAtTheCloseQuarter_IsFalse()
    {
        Assert.IsFalse(MarshalFireRules.StandsAside(Calm() with { TargetDistance = MarshalTuning.CloseQuarter }));
    }

    [TestMethod]
    public void StandsAside_ATargetDistanceThatIsNotANumber_StandsAside_SoTheAiKeepsItsOwnFlags()
    {
        Assert.IsTrue(MarshalFireRules.StandsAside(Calm() with { TargetDistance = double.NaN }));
    }
}
