using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CampaignStartOptions;

namespace TAOM.Tests.Features.CampaignStartOptions;

[TestClass]
public class CampaignStartOptionsResolverTests
{
    private static StartDefaults Defaults(bool wotr = true, int d1 = 30, int d2 = 44, bool weight = true)
        => new StartDefaults(wotr, d1, d2, weight);

    [TestMethod]
    public void NoScreenValues_GiveTheDefaults()
    {
        var r = CampaignStartOptionsResolver.Resolve(new ScreenValues(), Defaults(wotr: true, d1: 25, d2: 50, weight: false));

        Assert.IsTrue(r.WarOfTheRing);
        Assert.AreEqual(25, r.Phase1Day);
        Assert.AreEqual(50, r.Phase2Day);
        Assert.IsFalse(r.TroopWeight);
        Assert.IsNull(r.Warning);
    }

    [TestMethod]
    public void AbsentScenario_FollowsTheDefaultSwitch()
    {
        Assert.IsTrue(CampaignStartOptionsResolver.Resolve(new ScreenValues(), Defaults(wotr: true)).WarOfTheRing);
        Assert.IsFalse(CampaignStartOptionsResolver.Resolve(new ScreenValues(), Defaults(wotr: false)).WarOfTheRing);
    }

    [DataTestMethod]
    [DataRow(CampaignStartOptionKeys.WarOfTheRingScenarioItem, true)]
    [DataRow(CampaignStartOptionKeys.SandboxScenarioItem, false)]
    [DataRow("some_other_mod_scenario", false)]
    public void ChosenScenario_BeatsTheDefault(string scenario, bool expected)
    {
        foreach (var defaultOn in new[] { true, false })
        {
            var r = CampaignStartOptionsResolver.Resolve(new ScreenValues { Scenario = scenario }, Defaults(wotr: defaultOn));
            Assert.AreEqual(expected, r.WarOfTheRing, "default switch " + defaultOn);
        }
    }

    [DataTestMethod]
    [DataRow(null, 33, 47)]
    [DataRow(CampaignStartOptionKeys.StartStandardItem, 33, 47)]
    [DataRow(CampaignStartOptionKeys.StartEarlyItem, 10, 20)]
    [DataRow(CampaignStartOptionKeys.StartLateItem, 60, 90)]
    public void Preset_GivesItsDays_StandardKeepsTheMcmSliders(string? preset, int day1, int day2)
    {
        var r = CampaignStartOptionsResolver.Resolve(new ScreenValues { StartDate = preset }, Defaults(d1: 33, d2: 47));

        Assert.AreEqual(day1, r.Phase1Day);
        Assert.AreEqual(day2, r.Phase2Day);
        Assert.IsNull(r.Warning);
    }

    [TestMethod]
    public void Custom_UsesTheTwoSliders()
    {
        var screen = new ScreenValues { StartDate = CampaignStartOptionKeys.StartCustomItem, CustomDay1 = 100, CustomDay2 = 200 };

        var r = CampaignStartOptionsResolver.Resolve(screen, Defaults());

        Assert.AreEqual(100, r.Phase1Day);
        Assert.AreEqual(200, r.Phase2Day);
    }

    [TestMethod]
    public void Custom_WithASliderUntouched_UsesTheDefaultForIt()
    {
        var screen = new ScreenValues { StartDate = CampaignStartOptionKeys.StartCustomItem, CustomDay2 = 200 };

        var r = CampaignStartOptionsResolver.Resolve(screen, Defaults(d1: 30, d2: 44));

        Assert.AreEqual(30, r.Phase1Day);
        Assert.AreEqual(200, r.Phase2Day);
    }

    [DataTestMethod]
    [DataRow(0, 5, 1, 5)]       // day 1 below the range
    [DataRow(500, 600, 364, 365)] // both above: day 1 leaves room for day 2
    [DataRow(50, 50, 50, 51)]   // equal days: phase 2 moves one day later
    [DataRow(80, 40, 80, 81)]   // inverted: phase 2 moves after phase 1
    [DataRow(365, 365, 364, 365)]
    public void Custom_IsClamped_SoPhase2LandsStrictlyAfterPhase1(int in1, int in2, int out1, int out2)
    {
        var screen = new ScreenValues { StartDate = CampaignStartOptionKeys.StartCustomItem, CustomDay1 = in1, CustomDay2 = in2 };

        var r = CampaignStartOptionsResolver.Resolve(screen, Defaults());

        Assert.AreEqual(out1, r.Phase1Day);
        Assert.AreEqual(out2, r.Phase2Day);
    }

    [TestMethod]
    public void UnknownPreset_FallsBackToTheStandardDays_WithAWarning()
    {
        var r = CampaignStartOptionsResolver.Resolve(new ScreenValues { StartDate = "taom_wotr_start_vanished" }, Defaults(d1: 33, d2: 47));

        Assert.AreEqual(33, r.Phase1Day);
        Assert.AreEqual(47, r.Phase2Day);
        StringAssert.Contains(r.Warning, "taom_wotr_start_vanished");
    }

    [TestMethod]
    public void RulesToggles_BeatTheirDefaults()
    {
        var r = CampaignStartOptionsResolver.Resolve(new ScreenValues { TroopWeight = false },
            Defaults(weight: true));

        Assert.IsFalse(r.TroopWeight);
    }

    [TestMethod]
    public void IsValidPair_AcceptsOnlyAnOrderedPairInRange()
    {
        Assert.IsTrue(CampaignStartOptionsResolver.IsValidPair(1, 2));
        Assert.IsTrue(CampaignStartOptionsResolver.IsValidPair(364, 365));
        Assert.IsFalse(CampaignStartOptionsResolver.IsValidPair(0, 10));
        Assert.IsFalse(CampaignStartOptionsResolver.IsValidPair(10, 366));
        Assert.IsFalse(CampaignStartOptionsResolver.IsValidPair(30, 30));
        Assert.IsFalse(CampaignStartOptionsResolver.IsValidPair(50, 40));
    }
}
