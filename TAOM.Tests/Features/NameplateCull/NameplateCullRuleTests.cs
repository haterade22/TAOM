using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NameplateCull;
using TAOM.Features.NameplateCull.Models;

namespace TAOM.Tests.Features.NameplateCull;

/// <summary>
/// The skip decision from plain values. Each condition of <see cref="NameplateCullRule.MaySkip"/> has its own test that
/// flips it alone against a plate that would otherwise be skipped, so no condition can be dropped unnoticed.
/// </summary>
[TestClass]
public class NameplateCullRuleTests
{
    // A plate the cull skips: hidden everywhere, parked, untracked, out of range, a village 500 units from a camera at
    // height 50 (the near test is 50 + 100 = 150), nothing about its settlement changed.
    private static NameplateFacts Skippable() => new NameplateFacts
    {
        IsVisibleOnMap = false,
        BindIsVisibleOnMap = false,
        ParkedOffScreen = true,
        IsTracked = false,
        IsInRange = false,
        IsTargetedByTutorial = false,
        CameraZ = 50f,
        DistanceToCamera = 500f,
        IsTown = false,
        IsFortification = false,
        SettlementInRange = false,
        PartyVisualDirty = false,
        VisuallyTracked = false,
    };

    // ---- CanBeVisible: the distance part of SettlementNameplateVM.IsVisible (v1.5.4) ----

    [TestMethod]
    public void CanBeVisible_AboveTheTownHeight_OnlyATownCan()
    {
        Assert.IsTrue(NameplateCullRule.CanBeVisible(401f, 99999f, isTown: true, isFortification: true));
        Assert.IsFalse(NameplateCullRule.CanBeVisible(401f, 0f, isTown: false, isFortification: true), "a castle is not enough above 400");
        Assert.IsFalse(NameplateCullRule.CanBeVisible(401f, 0f, isTown: false, isFortification: false), "distance does not matter above 400");
    }

    [TestMethod]
    public void CanBeVisible_ExactlyAtTheTownHeight_FallsToTheFortificationTest()
    {
        // vanilla compares z > 400, so 400 itself is in the middle band
        Assert.IsTrue(NameplateCullRule.CanBeVisible(400f, 99999f, isTown: false, isFortification: true));
        Assert.IsFalse(NameplateCullRule.CanBeVisible(400f, 0f, isTown: false, isFortification: false));
    }

    [TestMethod]
    public void CanBeVisible_BetweenTheHeights_OnlyAFortificationCan()
    {
        Assert.IsTrue(NameplateCullRule.CanBeVisible(300f, 99999f, isTown: false, isFortification: true));
        Assert.IsFalse(NameplateCullRule.CanBeVisible(300f, 0f, isTown: false, isFortification: false), "a village is out of range from 200 up whatever its distance");
    }

    [TestMethod]
    public void CanBeVisible_ExactlyAtTheFortificationHeight_FallsToTheDistanceTest()
    {
        // vanilla compares z > 200, so 200 itself is the near band: the limit is 200 + 100
        Assert.IsTrue(NameplateCullRule.CanBeVisible(200f, 299f, isTown: false, isFortification: false));
        Assert.IsFalse(NameplateCullRule.CanBeVisible(200f, 300f, isTown: false, isFortification: false));
    }

    [TestMethod]
    public void CanBeVisible_NearBand_IsStrictlyCloserThanHeightPlus100()
    {
        Assert.IsTrue(NameplateCullRule.CanBeVisible(50f, 149.9f, false, false));
        Assert.IsFalse(NameplateCullRule.CanBeVisible(50f, 150f, false, false), "equal is not closer");
        Assert.IsFalse(NameplateCullRule.CanBeVisible(50f, 151f, false, false));
    }

    [TestMethod]
    public void CanBeVisible_NonFiniteInput_CannotBeRuledOut()
    {
        Assert.IsTrue(NameplateCullRule.CanBeVisible(float.NaN, 500f, false, false));
        Assert.IsTrue(NameplateCullRule.CanBeVisible(50f, float.NaN, false, false));
        Assert.IsTrue(NameplateCullRule.CanBeVisible(float.PositiveInfinity, 500f, false, false));
        Assert.IsTrue(NameplateCullRule.CanBeVisible(50f, float.PositiveInfinity, false, false));
        Assert.IsTrue(NameplateCullRule.CanBeVisible(50f, float.NegativeInfinity, false, false));
    }

    // ---- MaySkip: every condition ----

    [TestMethod]
    public void MaySkip_AHiddenParkedUntrackedOutOfRangePlate_IsSkipped()
    {
        var facts = Skippable();

        Assert.IsTrue(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_APlateTheWidgetStillShows_IsNotSkipped()
    {
        var facts = Skippable();
        facts.IsVisibleOnMap = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_APlateTheLastUpdateMadeVisible_IsNotSkipped()
    {
        var facts = Skippable();
        facts.BindIsVisibleOnMap = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_APlateNeverParkedOffScreen_IsNotSkipped()
    {
        // a new plate sits at (0, 0) until its first update parks it at (-1000, -1000); skipping it would leave it there
        var facts = Skippable();
        facts.ParkedOffScreen = false;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_ATrackedPlate_IsNotSkipped()
    {
        var facts = Skippable();
        facts.IsTracked = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_APlateInRange_IsNotSkipped()
    {
        var facts = Skippable();
        facts.IsInRange = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_ATutorialTarget_IsNotSkipped()
    {
        var facts = Skippable();
        facts.IsTargetedByTutorial = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_APlateTheCameraCouldReveal_IsNotSkipped()
    {
        var facts = Skippable();
        facts.DistanceToCamera = 100f;   // closer than 50 + 100

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_ATownUnderAHighCamera_IsNotSkipped()
    {
        var facts = Skippable();
        facts.CameraZ = 500f;
        facts.IsTown = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_AVillageUnderAHighCamera_IsSkipped()
    {
        var facts = Skippable();
        facts.CameraZ = 500f;
        facts.DistanceToCamera = 5f;

        Assert.IsTrue(NameplateCullRule.MaySkip(in facts), "above 400 only towns can show, however close");
    }

    [TestMethod]
    public void MaySkip_ASettlementThatHasComeInRange_IsNotSkipped()
    {
        var facts = Skippable();
        facts.SettlementInRange = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_APartyWhoseNameIsDirty_IsNotSkipped()
    {
        var facts = Skippable();
        facts.PartyVisualDirty = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_ASettlementTheVisualTrackerHolds_IsNotSkipped()
    {
        var facts = Skippable();
        facts.VisuallyTracked = true;

        Assert.IsFalse(NameplateCullRule.MaySkip(in facts));
    }

    [TestMethod]
    public void MaySkip_NonFiniteCameraOrDistance_IsNotSkipped()
    {
        var badCamera = Skippable();
        badCamera.CameraZ = float.NaN;
        var badDistance = Skippable();
        badDistance.DistanceToCamera = float.NaN;

        Assert.IsFalse(NameplateCullRule.MaySkip(in badCamera));
        Assert.IsFalse(NameplateCullRule.MaySkip(in badDistance));
    }
}
