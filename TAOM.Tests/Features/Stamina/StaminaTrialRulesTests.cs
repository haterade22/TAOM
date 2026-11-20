using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Stamina;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

/// <summary>
/// The stamina steps of the command trial (#1506): the rows and the switch, the counting rules, the windows a step reads
/// and the pass rule of each step. Every pass term is a positive requirement, so a value that is no number fails it.
/// </summary>
[TestClass]
public class StaminaTrialRulesTests
{
    private static StaminaCensus Census(bool armed = true, string gate = "open (MCM=True)", int tracked = 100, int soldiers = 100, bool heroCell = true) =>
        new(armed, gate, tracked, soldiers, heroCell, 0, 0L, 3f);

    private static StaminaGroupSample Sample(int live = 10, int runners = 0, float speedSum = 0f, int locked = 0, long releases = 0, long defends = 0,
        float chanceSum = 0f, float defensivenessSum = 0f) =>
        new(live, runners, speedSum, locked, releases, defends, chanceSum, defensivenessSum);

    // ---- the rows ----

    [TestMethod]
    public void NamedIn_AListWithAStaminaStep_IsTrue()
    {
        Assert.IsTrue(StaminaTrialRows.NamedIn("stamina_gate"));
        Assert.IsTrue(StaminaTrialRows.NamedIn(" Stamina_Gate , stamina_walk"));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("enter,fly")]
    [DataRow("crew_mumakil")]
    public void NamedIn_AListWithNoStaminaStep_IsFalse(string? steps)
    {
        Assert.IsFalse(StaminaTrialRows.NamedIn(steps));
    }

    [TestMethod]
    public void StepThatCannotShareAStaminaBattle_NamesEnterAndOtherFeatures()
    {
        Assert.IsNull(StaminaTrialRows.StepThatCannotShareAStaminaBattle(new[] { "stamina_gate", "stamina_walk", "stamina_fight" }));
        Assert.IsNull(StaminaTrialRows.StepThatCannotShareAStaminaBattle(new[] { "enter", "fly" }));
        Assert.AreEqual("enter", StaminaTrialRows.StepThatCannotShareAStaminaBattle(new[] { "stamina_gate", "enter" }));
        Assert.AreEqual("crew_howdah", StaminaTrialRows.StepThatCannotShareAStaminaBattle(new[] { "crew_howdah", "stamina_fight" }));
    }

    [TestMethod]
    public void Rows_WellFormed_PrerequisitesPrecede()
    {
        var names = StaminaTrialRows.Rows.Select(r => r.Name).ToList();

        CollectionAssert.AreEqual(new[] { "stamina_gate", "stamina_walk", "stamina_fight" }, names);
        Assert.AreEqual(names.Count, names.Distinct().Count());
        foreach (var row in StaminaTrialRows.Rows)
        {
            Assert.IsTrue(row.BudgetSeconds > 0f, row.Name);
            foreach (var need in row.Requires)
                Assert.IsTrue(names.IndexOf(need) >= 0 && names.IndexOf(need) < names.IndexOf(row.Name), row.Name + " needs " + need);
        }
    }

    [TestMethod]
    public void TheRowConstants_AreTheRowNames()
    {
        Assert.AreEqual("stamina_gate", StaminaTrialRows.Gate);
        Assert.AreEqual("stamina_walk", StaminaTrialRows.Walk);
        Assert.AreEqual("stamina_fight", StaminaTrialRows.Fight);
        Assert.AreEqual(45f, StaminaTrialRows.Rows.Single(r => r.Name == StaminaTrialRows.Gate).BudgetSeconds);
    }

    // ---- the counts ----

    [DataTestMethod]
    [DataRow(0, 16, true)]
    [DataRow(0, 17, true)]
    [DataRow(15, 20, true)]
    [DataRow(20, 20, false, DisplayName = "a held release is not a new one")]
    [DataRow(0, 15, false, DisplayName = "a ready swing is not a release")]
    [DataRow(16, 0, false)]
    [DataRow(0, 1, false)]
    public void IsRelease_OnlyAChangeIntoARelease(int before, int now, bool expected)
    {
        Assert.AreEqual(expected, StaminaTrialRules.IsRelease(before, now));
    }

    [DataTestMethod]
    [DataRow(0, 1, true)]
    [DataRow(0, 14, true)]
    [DataRow(15, 7, true)]
    [DataRow(3, 9, false, DisplayName = "a held defence is not a new entry")]
    [DataRow(0, 15, false)]
    [DataRow(0, 0, false)]
    [DataRow(5, 0, false)]
    public void IsDefendEntry_OnlyAChangeIntoTheDefendRangeFromOutsideIt(int before, int now, bool expected)
    {
        Assert.AreEqual(expected, StaminaTrialRules.IsDefendEntry(before, now));
    }

    // ---- the gate ----

    [TestMethod]
    public void GatePasses_AnOpenGateWithTheHeroCellAndMostSoldiersTracked_Passes()
    {
        Assert.IsTrue(StaminaTrialRules.GatePasses(Census()));
        Assert.IsTrue(StaminaTrialRules.GatePasses(Census(tracked: 95)));
    }

    [TestMethod]
    public void GatePasses_ClosedGate_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(gate: "closed (MCM=True)")));

    [TestMethod]
    public void GatePasses_NotArmed_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(armed: false)));

    [TestMethod]
    public void GatePasses_NoHeroCell_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(heroCell: false)));

    [TestMethod]
    public void GatePasses_NoCell_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(tracked: 0)));

    [TestMethod]
    public void GatePasses_BelowNinetyFivePercent_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(tracked: 94)));

    [TestMethod]
    public void GatePasses_NoSoldiers_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(tracked: 0, soldiers: 0)));

    [TestMethod]
    public void GatePasses_NoGateText_Fails() => Assert.IsFalse(StaminaTrialRules.GatePasses(Census(gate: null!)));

    // ---- the groups ----

    [TestMethod]
    public void AllLocked_NeedsEveryLive()
    {
        Assert.IsTrue(StaminaTrialRules.AllLocked(Sample(live: 5, locked: 5)));
        Assert.IsFalse(StaminaTrialRules.AllLocked(Sample(live: 5, locked: 4)));
        Assert.IsFalse(StaminaTrialRules.AllLocked(Sample(live: 0, locked: 0)));
    }

    [TestMethod]
    public void WalkContact_FromThreeControlReleases()
    {
        Assert.IsFalse(StaminaTrialRules.WalkContact(2));
        Assert.IsTrue(StaminaTrialRules.WalkContact(3));
    }

    [TestMethod]
    public void WalkPasses_ExhaustedWhoWalkAfterRunners_Passes()
    {
        Assert.IsTrue(StaminaTrialRules.WalkPasses(0.6f, 0f, 1.5f, 3f));
        Assert.IsTrue(StaminaTrialRules.WalkPasses(0.3f, 0.1f, 0.5f, 1f));
    }

    [TestMethod]
    public void WalkPasses_BaseShareTooLow_Fails() => Assert.IsFalse(StaminaTrialRules.WalkPasses(0.29f, 0f, 1.5f, 3f));

    [TestMethod]
    public void WalkPasses_ExhaustedStillRun_Fails() => Assert.IsFalse(StaminaTrialRules.WalkPasses(0.6f, 0.2f, 1.5f, 3f));

    [TestMethod]
    public void WalkPasses_ExhaustedStandStill_Fails() => Assert.IsFalse(StaminaTrialRules.WalkPasses(0.6f, 0f, 0.2f, 3f));

    [TestMethod]
    public void WalkPasses_ExhaustedNoSlowerThanTheControl_Fails_BecauseTheWholeFormationSlowed()
    {
        Assert.IsFalse(StaminaTrialRules.WalkPasses(0.6f, 0f, 1.5f, 1.5f));
        Assert.IsFalse(StaminaTrialRules.WalkPasses(0.6f, 0f, 1.5f, 1.6f));
        Assert.IsTrue(StaminaTrialRules.WalkPasses(0.6f, 0f, 1.5f, 1.7f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f, 1.5f, 3f)]
    [DataRow(0.6f, float.NaN, 1.5f, 3f)]
    [DataRow(0.6f, 0f, float.NaN, 3f)]
    [DataRow(0.6f, 0f, 1.5f, float.NaN)]
    public void WalkPasses_ANumberThatIsNoNumber_Fails(float baseShare, float exhaustedShare, float speed, float controlSpeed)
    {
        Assert.IsFalse(StaminaTrialRules.WalkPasses(baseShare, exhaustedShare, speed, controlSpeed));
    }

    // ---- the fight ----

    [TestMethod]
    public void FightPasses_AllSixTerms_Passes() => Assert.IsTrue(StaminaTrialRules.FightPasses(0, 0, 2, 0, 30, 3));

    [TestMethod]
    public void FightPasses_AnExhaustedAttack_Fails() => Assert.IsFalse(StaminaTrialRules.FightPasses(1, 0, 2, 0, 30, 3));

    [TestMethod]
    public void FightPasses_AnExhaustedDefence_Fails() => Assert.IsFalse(StaminaTrialRules.FightPasses(0, 1, 2, 0, 30, 3));

    [TestMethod]
    public void FightPasses_NoTiredRelease_Fails() => Assert.IsFalse(StaminaTrialRules.FightPasses(0, 0, 0, 0, 30, 3));

    [TestMethod]
    public void FightPasses_ATiredGroupThatWasLocked_Fails() => Assert.IsFalse(StaminaTrialRules.FightPasses(0, 0, 2, 1, 30, 3));

    [TestMethod]
    public void FightPasses_TooFewControlReleases_Fails() => Assert.IsFalse(StaminaTrialRules.FightPasses(0, 0, 2, 0, 9, 3));

    [TestMethod]
    public void FightPasses_NoAttackAfterTheAutoRelease_Fails() => Assert.IsFalse(StaminaTrialRules.FightPasses(0, 0, 2, 0, 30, 0));

    [TestMethod]
    public void FightPasses_TiredAsOftenAsControl_StillPasses() => Assert.IsTrue(StaminaTrialRules.FightPasses(0, 0, 40, 0, 40, 3));

    [TestMethod]
    public void Ratio_NaNSafe()
    {
        Assert.AreEqual(0.5f, StaminaTrialRules.Ratio(1f, 2f));
        Assert.IsTrue(float.IsNaN(StaminaTrialRules.Ratio(1f, 0f)));
        Assert.IsTrue(float.IsNaN(StaminaTrialRules.Ratio(float.NaN, 2f)));
        Assert.IsTrue(float.IsNaN(StaminaTrialRules.Ratio(1f, float.NaN)));
    }

    // ---- the window ----

    [TestMethod]
    public void Window_CountsReleasesAndDefendsSinceItsStart_AndSoldierSeconds()
    {
        var window = new StaminaWindow(Sample(live: 10, releases: 100, defends: 7), at: 5f);

        window.Add(Sample(live: 10, releases: 112, defends: 9), at: 6f);
        window.Add(Sample(live: 8, releases: 120, defends: 9), at: 8f);

        Assert.AreEqual(20L, window.Releases);
        Assert.AreEqual(2L, window.Defends);
        Assert.AreEqual(10f + 16f, window.SoldierSeconds, 0.001f);
        Assert.AreEqual(20f / 26f, window.Rate, 0.001f);
    }

    [TestMethod]
    public void Window_RunnerShareAndMeanSpeed_AreAveragedOverTheFramesWithSoldiers()
    {
        var window = new StaminaWindow(Sample(), at: 0f);

        window.Add(Sample(live: 10, runners: 5, speedSum: 30f), at: 1f);
        window.Add(Sample(live: 0, runners: 0, speedSum: 0f), at: 2f);
        window.Add(Sample(live: 10, runners: 1, speedSum: 10f), at: 3f);

        Assert.AreEqual(0.3f, window.RunnerShare, 0.001f);
        Assert.AreEqual(2f, window.MeanSpeed, 0.001f);
    }

    [TestMethod]
    public void Window_CountsFramesWithALockedSoldier()
    {
        var window = new StaminaWindow(Sample(), at: 0f);

        window.Add(Sample(locked: 0), at: 1f);
        window.Add(Sample(locked: 2), at: 2f);
        window.Add(Sample(locked: 1), at: 3f);

        Assert.AreEqual(2, window.LockedFrames);
    }

    [TestMethod]
    public void Window_MeanChanceAndDefensiveness_AreAveragedPerSoldier()
    {
        var window = new StaminaWindow(Sample(), at: 0f);

        window.Add(Sample(live: 10, chanceSum: 1f, defensivenessSum: 4f), at: 1f);
        window.Add(Sample(live: 10, chanceSum: 3f, defensivenessSum: 6f), at: 2f);

        Assert.AreEqual(0.2f, window.MeanChance, 0.001f);
        Assert.AreEqual(0.5f, window.MeanDefensiveness, 0.001f);
    }

    [TestMethod]
    public void Window_WithNoFrame_ReadsNaN_AndNoCounts()
    {
        var window = new StaminaWindow(Sample(releases: 4), at: 0f);

        Assert.AreEqual(0L, window.Releases);
        Assert.AreEqual(0L, window.Defends);
        Assert.AreEqual(0f, window.SoldierSeconds);
        Assert.IsTrue(float.IsNaN(window.Rate));
        Assert.IsTrue(float.IsNaN(window.RunnerShare));
        Assert.IsTrue(float.IsNaN(window.MeanSpeed));
        Assert.IsTrue(float.IsNaN(window.MeanChance));
        Assert.IsTrue(float.IsNaN(window.MeanDefensiveness));
    }

    // ---- the facts ----

    [TestMethod]
    public void Facts_AreInvariantWords()
    {
        var census = Census(gate: "open (MCM=True, fords=True)", tracked: 98, soldiers: 100);
        var baseWindow = new StaminaWindow(Sample(), 0f);
        baseWindow.Add(Sample(live: 10, runners: 5, speedSum: 40f), 2f);
        var window = new StaminaWindow(Sample(), 0f);
        window.Add(Sample(live: 10, runners: 0, speedSum: 15f, releases: 12), 4f);

        var gate = StaminaTrialRules.GateFacts(census, 1);
        var walk = StaminaTrialRules.WalkFacts(20, 20, 3f, baseWindow, baseWindow, window, window);
        var fight = StaminaTrialRules.FightFacts(10, 10, 10, window, window, window, 6L, 3L, 42.5f);

        StringAssert.StartsWith(gate, "armed=true gate=open tracked=98 soldiers=100 share=0.98 hero_cell=true pictures=1");
        StringAssert.StartsWith(walk, "forced=20 control=20 run=3.00 base_share=0.50");
        StringAssert.Contains(walk, " ratio=");
        StringAssert.Contains(walk, " control_ratio=");
        StringAssert.Contains(walk, " control_speed=1.50");
        StringAssert.StartsWith(fight, "exhausted=10 tired=10 control=10 e_releases=12");
        StringAssert.Contains(fight, " strips=+6 restored=3 contact_at=42.50");
        foreach (var facts in new[] { gate, walk, fight })
        {
            Assert.IsFalse(facts.Contains(','), facts);
            Assert.IsTrue(facts.Split(' ').All(word => word.Contains('=')), facts);
        }
    }

    [TestMethod]
    public void GatePicture_IsHudBmp()
    {
        Assert.AreEqual("hud_stamina_gate.bmp", StaminaTrialRules.GatePicture);
        StringAssert.StartsWith(StaminaTrialRules.GatePicture, "hud_");
    }

    [TestMethod]
    public void EveryRowBudget_CoversItsLongestWait()
    {
        var gate = StaminaTrialRows.Rows.Single(r => r.Name == StaminaTrialRows.Gate).BudgetSeconds;
        var walk = StaminaTrialRows.Rows.Single(r => r.Name == StaminaTrialRows.Walk).BudgetSeconds;
        var fight = StaminaTrialRows.Rows.Single(r => r.Name == StaminaTrialRows.Fight).BudgetSeconds;

        Assert.IsTrue(gate >= StaminaTrialRules.BattleWaitSeconds + StaminaTrialRules.GateWaitSeconds + 10f);
        Assert.IsTrue(walk >= StaminaTrialRules.RunnersWaitSeconds + StaminaTrialRules.BaseWindowSeconds + StaminaTrialRules.ApplyLimitSeconds
            + StaminaTrialRules.WalkWindowSeconds + 10f);
        Assert.IsTrue(fight >= StaminaTrialRules.ContactWaitSeconds + StaminaTrialRules.ApplyLimitSeconds + StaminaTrialRules.SettleSeconds
            + StaminaTrialRules.FightMaxSeconds + StaminaTrialRules.RestoreLimitSeconds + 10f);
    }
}
