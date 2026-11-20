using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Features.Stamina.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.Stamina;

/// <summary>
/// The stamina steps of the command trial (#1506) against the stand-in: each step passes on a stamina layer that locks, slows and
/// gives back, and fails with its own facts when one of those is broken. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialStaminaStepsTests
{
    private const float Frame = 1f / 30f;

    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;
    private List<string> _finished = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
        _finished = new List<string>();
    }

    private void Run(params string[] steps)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(string.Join(",", steps), out var chosen, out var error), error);
        RunRows(chosen);
    }

    private void RunRows(IReadOnlyList<SeatTrialStep> rows)
    {
        var script = new CommandTrialScript(_world, rows, _log, _finished.Add);
        _world.Run(script, 600f, Frame);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line(string step) =>
        _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private static float Number(string line, string key) => float.Parse(Fact(line, key), CultureInfo.InvariantCulture);

    private void AssertPasses(string step)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=PASS", line);
    }

    private void AssertFails(string step, params string[] mentions)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        foreach (var mention in mentions)
            StringAssert.Contains(line, mention, line);
    }

    // ---- a stamina layer that works ----

    [TestMethod]
    public void TheBattle_OnAWorkingBuild_PassesEveryStep()
    {
        Run("stamina_gate", "stamina_walk", "stamina_fight");

        AssertPasses("stamina_gate");
        AssertPasses("stamina_walk");
        AssertPasses("stamina_fight");
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void TheSteps_ReadAndForceOnlyInTheMissionTick()
    {
        Run("stamina_gate", "stamina_walk", "stamina_fight");

        Assert.AreEqual(0, _world.StaminaReadsOutsideTheMissionTick);
        Assert.AreEqual(0, _world.WritesOutsideTheMissionTick.Count, string.Join(", ", _world.WritesOutsideTheMissionTick));
        Assert.IsTrue(_world.Writes.Contains("force-stamina"), "the steps forced something");
    }

    [TestMethod]
    public void EveryStep_EndsItsFactsWithTheSidesCounts()
    {
        Run("stamina_gate", "stamina_walk", "stamina_fight");

        foreach (var step in new[] { "stamina_gate", "stamina_walk", "stamina_fight" })
        {
            var line = Line(step);
            Assert.AreEqual("120", Fact(line, "allies"), line);
            Assert.AreEqual("280", Fact(line, "enemies"), line);
        }
    }

    [TestMethod]
    public void TheGate_TakesItsPicture()
    {
        Run("stamina_gate");

        CollectionAssert.AreEqual(new[] { "hud_stamina_gate.bmp" }, _world.Screenshots.ToArray());
        var line = Line("stamina_gate");
        AssertPasses("stamina_gate");
        Assert.AreEqual("1", Fact(line, "pictures"));
        Assert.AreEqual("open", Fact(line, "gate"));
        Assert.AreEqual("true", Fact(line, "hero_cell"));
        var pictureAt = _world.ScreenshotTimes.Single();
        Assert.IsTrue(pictureAt >= _world.DeploymentFinishedAt!.Value + StaminaTrialRules.GatePictureSeconds - 0.1f, "the picture is 3 s into the battle: " + pictureAt);
    }

    [TestMethod]
    public void AClosedGate_FailsTheGate_WithTheGateWord()
    {
        _world.GateText = "closed (MCM=False, fords=False, combatType=Combat, teamAI=Battle)";

        Run("stamina_gate");

        AssertFails("stamina_gate", "gate=closed");
    }

    [TestMethod]
    public void ARunThatIsNotArmed_FailsTheGate()
    {
        _world.StaminaArmed = false;

        Run("stamina_gate");

        AssertFails("stamina_gate", "armed=false");
    }

    [TestMethod]
    public void TooFewTrackedSoldiers_FailTheGate()
    {
        _world.TrackedShare = 0.5f;

        Run("stamina_gate");

        AssertFails("stamina_gate", "tracked=50", "soldiers=100");
    }

    [TestMethod]
    public void NoHeroCell_FailsTheGate()
    {
        _world.HeroCell = false;

        Run("stamina_gate");

        AssertFails("stamina_gate", "hero_cell=false");
    }

    // ---- the walk ----

    [TestMethod]
    public void TheWalk_ChargesOnce_AndHoldsTheControlFresh_SoOnlyTheForcedHalfTires()
    {
        Run("stamina_gate", "stamina_walk");

        Assert.AreEqual(1, _world.ChargeOrders);
        CollectionAssert.AreEqual(new[] { "1:Fresh", "0:Exhausted" }, _world.StaminaForced.ToArray());
        Assert.AreEqual(0, _world.ForcedNow);
        var line = Line("stamina_walk");
        AssertPasses("stamina_walk");
        Assert.AreEqual("3.00", Fact(line, "run"));
        Assert.AreEqual("30", Fact(line, "forced"));
        Assert.AreEqual("30", Fact(line, "control"));
    }

    [TestMethod]
    public void TheWalk_SaysWhatSpeedTheExhaustedKeep_AndWhatTheControlGroupDid()
    {
        Run("stamina_gate", "stamina_walk");

        var line = Line("stamina_walk");
        Assert.AreEqual(1.5f, Number(line, "speed") / 1f, 0.01f);
        Assert.AreEqual(0.3f, Number(line, "ratio"), 0.01f);
        Assert.AreEqual(1.0f, Number(line, "control_ratio"), 0.01f);
        Assert.AreEqual(0f, Number(line, "share"), 0.01f);
    }

    [TestMethod]
    public void NoRunners_FailsTheWalk()
    {
        _world.RunnerShare = 0f;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "reason=no-runners");
        Assert.AreEqual(0, _world.StaminaForced.Count);
    }

    [TestMethod]
    public void TooSmallAGroup_FailsTheWalkAsTooFew()
    {
        _world.GroupSize = StaminaTrialRules.MinGroup - 1;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "reason=too-few");
        Assert.AreEqual(0, _world.StaminaForced.Count);
    }

    [TestMethod]
    public void ExhaustedWhoStillRun_FailTheWalk()
    {
        _world.ExhaustedRunnerShare = 0.5f;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "share=0.50");
    }

    [TestMethod]
    public void ExhaustedWhoStand_FailTheWalk()
    {
        _world.ExhaustedSpeed = 0f;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "speed=0.00");
    }

    [TestMethod]
    public void AFormationThatSlowsAsAWhole_FailsTheWalk_BecauseTheExhaustedAreNoSlowerThanTheControl()
    {
        _world.ControlSpeedBesideTheExhausted = 1.5f;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "speed=1.50", "control_speed=1.50");
    }

    [TestMethod]
    public void ContactDuringTheWalk_FailsAsContact()
    {
        _world.ControlReleasesDuringWalk = 2f;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "reason=contact");
        Assert.AreEqual(0, _world.ForcedNow);
    }

    [TestMethod]
    public void ALockThatNeverApplies_FailsTheWalkAsNotApplied()
    {
        _world.LockApplies = false;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "reason=not-applied");
        Assert.AreEqual(0, _world.ForcedNow);
    }

    [TestMethod]
    public void RefreshesThatStayQueued_FailTheWalkAsNotApplied()
    {
        _world.RefreshesStayQueued = true;

        Run("stamina_gate", "stamina_walk");

        AssertFails("stamina_walk", "reason=not-applied");
    }

    // ---- the fight ----

    [TestMethod]
    public void TheFight_ForcesThreeGroups_AndGivesThemAllBack()
    {
        Run("stamina_gate", "stamina_fight");

        AssertPasses("stamina_fight");
        CollectionAssert.AreEqual(new[] { "2:Fresh", "1:Tired", "0:Exhausted" }, _world.StaminaForced.ToArray());
        Assert.AreEqual(0, _world.ForcedNow);
        Assert.IsTrue(_world.ReleasedForced >= 1);
        var line = Line("stamina_fight");
        Assert.AreEqual("0", Fact(line, "e_releases"));
        Assert.AreEqual("0", Fact(line, "e_defends"));
        Assert.IsTrue(Number(line, "c_releases") >= StaminaTrialRules.ControlMinReleases, line);
        Assert.IsTrue(Number(line, "restored") >= StaminaTrialRules.RestoreEnoughReleases, line);
    }

    [TestMethod]
    public void TheFight_ReportsTheRatioChanceAndContactTime()
    {
        Run("stamina_gate", "stamina_fight");

        var line = Line("stamina_fight");
        Assert.AreEqual(0.7f, Number(line, "ratio"), 0.01f);
        Assert.AreEqual(0.35f, Number(line, "t_chance"), 0.01f);
        Assert.AreEqual(0.5f, Number(line, "c_chance"), 0.01f);
        Assert.IsTrue(Number(line, "contact_at") >= _world.ContactAfterSeconds, line);
        StringAssert.Contains(line, "strips=+");
    }

    [TestMethod]
    public void TheFightWindow_OfABusyFight_EndsAtTheControlsEightiethRelease_NoSoonerThanTenSeconds()
    {
        _world.ControlRate = 0.3f;

        Run("stamina_gate", "stamina_fight");

        var line = Line("stamina_fight");
        AssertPasses("stamina_fight");
        Assert.IsTrue(Number(line, "c_releases") >= StaminaTrialRules.ControlEnoughReleases, line);
        Assert.AreEqual(StaminaTrialRules.FightMinSeconds, Number(line, "c_seconds") / _world.GroupSize, 0.2f, line);
    }

    [TestMethod]
    public void TheFightWindow_OfAQuietFight_IsCutAtThirtySeconds()
    {
        _world.ControlRate = 0.06f;

        Run("stamina_gate", "stamina_fight");

        var line = Line("stamina_fight");
        AssertPasses("stamina_fight");
        Assert.IsTrue(Number(line, "c_releases") < StaminaTrialRules.ControlEnoughReleases, line);
        Assert.AreEqual(StaminaTrialRules.FightMaxSeconds, Number(line, "c_seconds") / _world.GroupSize, 0.2f, line);
    }

    [TestMethod]
    public void NoContact_FailsWithinBudget_NotAsATimeout()
    {
        _world.ContactAfterSeconds = 100000f;

        Run("stamina_gate", "stamina_fight");

        AssertFails("stamina_fight", "reason=no-contact");
        Assert.AreEqual(0, _world.StaminaForced.Count);
    }

    [TestMethod]
    public void AnExhaustedAttack_FailsTheFight()
    {
        _world.ExhaustedRate = 0.05f;

        Run("stamina_gate", "stamina_fight");

        var line = Line("stamina_fight");
        AssertFails("stamina_fight");
        Assert.IsTrue(Number(line, "e_releases") >= 1f, line);
    }

    [TestMethod]
    public void AnExhaustedDefence_FailsTheFight()
    {
        _world.ExhaustedDefendRate = 0.05f;

        Run("stamina_gate", "stamina_fight");

        var line = Line("stamina_fight");
        AssertFails("stamina_fight");
        Assert.AreEqual("0", Fact(line, "e_releases"));
        Assert.IsTrue(Number(line, "e_defends") >= 1f, line);
    }

    [TestMethod]
    public void ALockedTiredGroup_FailsTheFight()
    {
        _world.TiredLocks = true;

        Run("stamina_gate", "stamina_fight");

        AssertFails("stamina_fight");
        Assert.AreEqual(0, _world.ForcedNow);
    }

    [TestMethod]
    public void NoTiredRelease_FailsTheFight()
    {
        _world.TiredFactor = 0f;

        Run("stamina_gate", "stamina_fight");

        AssertFails("stamina_fight", "t_releases=0");
    }

    [TestMethod]
    public void TooFewControlReleases_FailsAsContactLost()
    {
        _world.ContactEndsAfterSeconds = 10f;

        Run("stamina_gate", "stamina_fight");

        AssertFails("stamina_fight", "reason=contact-lost");
    }

    [TestMethod]
    public void NoAttackAfterAuto_FailsTheFight()
    {
        _world.ReleasesAfterAuto = 0f;

        Run("stamina_gate", "stamina_fight");

        AssertFails("stamina_fight", "restored=0");
    }

    [TestMethod]
    public void ALockThatNeverApplies_FailsTheFightAsNotApplied_AndGivesTheGroupsBack()
    {
        _world.LockApplies = false;

        Run("stamina_gate", "stamina_fight");

        AssertFails("stamina_fight", "reason=not-applied");
        Assert.AreEqual(0, _world.ForcedNow);
        Assert.IsTrue(_world.ReleasedForced >= 1);
    }

    [TestMethod]
    public void TiredAsOftenAsControl_StillPasses_AndReportsTheRatio()
    {
        _world.TiredFactor = 1f;

        Run("stamina_gate", "stamina_fight");

        var line = Line("stamina_fight");
        AssertPasses("stamina_fight");
        Assert.AreEqual(1f, Number(line, "ratio"), 0.02f);
    }

    // ---- soldiers given back ----

    [TestMethod]
    public void EveryPath_GivesTheSoldiersBack()
    {
        var breaks = new Action<CommandTrialWorld>[]
        {
            _ => { },
            w => w.LockApplies = false,
            w => w.ExhaustedRate = 0.05f,
            w => w.TiredLocks = true,
            w => w.ContactEndsAfterSeconds = 10f,
            w => w.ReleasesAfterAuto = 0f,
        };

        foreach (var broken in breaks)
        {
            Setup();
            broken(_world);

            Run("stamina_gate", "stamina_walk", "stamina_fight");

            Assert.AreEqual(0, _world.ForcedNow);
        }
    }

    [TestMethod]
    public void ATimedOutStep_GivesItsSoldiersBack()
    {
        _world.LockApplies = false;
        _world.ContactAfterSeconds = 1f;
        var rows = new[]
        {
            new SeatTrialStep("stamina_gate", Array.Empty<string>(), 45f),
            new SeatTrialStep("stamina_fight", new[] { "stamina_gate" }, 5f),
        };

        RunRows(rows);

        AssertFails("stamina_fight", "reason=timeout");
        Assert.IsTrue(_world.StaminaForced.Count > 0, "the fight had forced its groups before the budget ran out");
        Assert.AreEqual(0, _world.ForcedNow);
        Assert.IsTrue(_world.ReleasedForced >= 1);
    }

    [TestMethod]
    public void AnAbortedRun_GivesItsSoldiersBack()
    {
        _world.LockApplies = false;
        _world.ContactAfterSeconds = 1f;
        _world.ClockFailsFrom = 12.5f;

        var script = new CommandTrialScript(_world, SeatTrialSteps.Known.Where(s => s.Name.StartsWith("stamina_", StringComparison.Ordinal) && s.Name != "stamina_walk").ToList(), _log, _finished.Add);
        _world.Run(script, 600f, Frame);

        Assert.IsTrue(script.IsDone);
        CollectionAssert.AreEqual(new[] { "script_failed" }, _finished);
        Assert.IsTrue(_world.StaminaForced.Count > 0, "the fight had forced its groups");
        Assert.AreEqual(0, _world.ForcedNow);
    }

    // ---- the table ----

    [TestMethod]
    public void EveryStaminaRow_HasAHandler_AndABudgetThatCoversItsLongestRun()
    {
        var longest = new Dictionary<string, float>
        {
            [StaminaTrialRows.Gate] = StaminaTrialRules.BattleWaitSeconds + StaminaTrialRules.GateWaitSeconds,
            [StaminaTrialRows.Walk] = StaminaTrialRules.RunnersWaitSeconds + StaminaTrialRules.BaseWindowSeconds + StaminaTrialRules.ApplyLimitSeconds + StaminaTrialRules.WalkWindowSeconds,
            [StaminaTrialRows.Fight] = StaminaTrialRules.ContactWaitSeconds + StaminaTrialRules.ApplyLimitSeconds + StaminaTrialRules.SettleSeconds
                + StaminaTrialRules.FightMaxSeconds + StaminaTrialRules.RestoreLimitSeconds,
        };

        foreach (var row in StaminaTrialRows.Rows)
        {
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), row.Name);
            Assert.IsTrue(row.BudgetSeconds >= longest[row.Name] + 10f, row.Name + " has " + row.BudgetSeconds + " s for a run of up to " + longest[row.Name] + " s");
        }
    }
}
