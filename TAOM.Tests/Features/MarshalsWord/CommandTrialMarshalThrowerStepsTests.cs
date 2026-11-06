using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.MarshalsWord;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The two thrower steps against the stand-in (build-spec 9.4): each passes on a world where the feature works and fails with its own reason when the one thing it proves
/// is broken. The real driver and run run inside the stand-in; the game is the only proof that the soldiers throw while they stand.
/// </summary>
[TestClass]
public class CommandTrialMarshalThrowerStepsTests
{
    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;

    [TestInitialize]
    public void Setup()
    {
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Clear();
        MarshalFireTable.Begin();
        MarshalThrowerCost.Reset();
        _world = new CommandTrialWorld();
        _log = new ListLogger();
    }

    [TestCleanup]
    public void Cleanup()
    {
        MarshalFireTable.Clear();
        MarshalHooks.Begin(Substitute.For<IMarshalHookSink>());
        MarshalHooks.Clear();
    }

    private void Run(string step)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(step, out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _ => { });
        _world.Run(script, 600f);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line(string step) => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private void AssertFails(string step, string reason)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        StringAssert.Contains(line, "reason=" + reason, line);
    }

    // ---- advance_throwers ----

    [TestMethod]
    public void AdvanceThrowers_Passes_OnAWorldWhereTheCompanyStandsThrowsAndGoesIn()
    {
        Run("advance_throwers");

        var line = Line("advance_throwers");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("40", Fact(line, "units"));
        Assert.AreEqual("40", Fact(line, "armed0"));
        Assert.AreEqual("120", Fact(line, "ammo0"));
        Assert.AreEqual("80.0", Fact(line, "range"));
        Assert.AreEqual("32.0", Fact(line, "stand_gap"));
        Assert.AreEqual("97.0", Fact(line, "gap0"));
        Assert.AreEqual("spent", Fact(line, "release"));
        Assert.AreEqual("0", Fact(line, "faulted"));
        Assert.IsTrue(int.Parse(Fact(line, "shots")) >= 10, line);
        Assert.IsTrue(int.Parse(Fact(line, "ammo_spent")) >= 10, line);
        Assert.IsTrue(int.Parse(Fact(line, "blows")) > 0, line);
        Assert.IsTrue(int.Parse(Fact(line, "pub_calls")) > 300, line);
        Assert.IsTrue(int.Parse(Fact(line, "postfix")) > 0, line);
        Assert.AreEqual(0, _world.WritesOutsideTheMissionTick.Count, "an order and a selection are written from the mission tick");
    }

    [TestMethod]
    public void AdvanceThrowers_NamesEveryFact_TheContractListsOnItsLine()
    {
        Run("advance_throwers");

        foreach (var key in new[]
        {
            "units", "armed0", "ammo0", "range", "stand_gap", "gap0", "gap_at_stand", "min_gap", "overshoot", "stood_s", "shots", "run_shots", "ammo_spent", "armed_at_release", "release", "blows",
            "melee_s", "advanced_m", "pub_calls", "pub_mean_us", "pub_max_us", "adv_calls", "adv_mean_us", "adv_max_us", "replaced", "published", "postfix", "faulted",
        })
            Fact(Line("advance_throwers"), key);
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenThePostfixThrows()
    {
        _world.ThrowerFaultsThePostfix = true;
        Run("advance_throwers");
        AssertFails("advance_throwers", "faulted");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenNoCompanyHoldsEnoughThrowers()
    {
        _world.NoThrowers = true;
        Run("advance_throwers");
        AssertFails("advance_throwers", "no-throwers");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenThereIsNoEnemyCompany()
    {
        _world.EnemyUnits = 0;
        Run("advance_throwers");
        AssertFails("advance_throwers", "no-enemy");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheCompanyIsNotTheirs()
    {
        _world.CompaniesAreControllable = false;
        Run("advance_throwers");
        AssertFails("advance_throwers", "not-controllable");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenNoRunStarts_AndNamesTheGateThatRefusedIt()
    {
        _world.ThrowerRefusedAtTheOrder = true;
        Run("advance_throwers");
        AssertFails("advance_throwers", "never-published why=hold-fire");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenThePostfixNeverMovesAnAnswer()
    {
        _world.ThrowerPostfixMoves = false;
        Run("advance_throwers");
        AssertFails("advance_throwers", "postfix-silent");
    }

    [TestMethod]
    public void AdvanceThrowers_WhenThePostfixIsSilent_EndsAtOnce_AndDoesNotWaitOutTheWalkToTheStand()
    {
        _world.ThrowerPostfixMoves = false;
        Run("advance_throwers");

        AssertFails("advance_throwers", "postfix-silent");
        Assert.IsTrue(_world.Time < 12f, "the walk to the stand takes over fifteen seconds, and a step that cannot trust its facts does not wait for them: " + _world.Time);
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheGapClosesBeforeTheStand()
    {
        _world.ThrowersClose = true;
        Run("advance_throwers");
        AssertFails("advance_throwers", "closed-before-stand");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheCompanyNeverReachesItsPoint()
    {
        _world.ThrowerStopsShortMetres = 22f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "never-stood");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheStandNeverEnds()
    {
        _world.ThrowersThrow = false;
        _world.ThrowerClockFactor = 0.3f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "never-released");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheCompanyWalksPastItsPoint()
    {
        _world.ThrowerOvershootMetres = 10f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "walked-in");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheAmmunitionFallsAndNoShotIsCounted()
    {
        _world.ThrowerShotsAreCounted = false;
        Run("advance_throwers");
        AssertFails("advance_throwers", "shots-not-counted");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheSoldiersThrowTooLittle()
    {
        _world.ThrowerThrowsPerSecond = 0.15f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "few-throws");
        Assert.AreEqual("stand-time", Fact(Line("advance_throwers"), "release"));
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheCompanyDoesNotGoInAfterTheRelease()
    {
        _world.ThrowerMeleeFollows = false;
        Run("advance_throwers");
        AssertFails("advance_throwers", "no-melee");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheTimerNeverRuns()
    {
        _world.ThrowerTimerRuns = false;
        Run("advance_throwers");
        AssertFails("advance_throwers", "timer-silent");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheTimerSawTooFewCalls()
    {
        _world.ThrowerCostCallsPerSecond = 5;
        Run("advance_throwers");
        AssertFails("advance_throwers", "too-few-calls");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenTheMeanCallIsSlow()
    {
        _world.ThrowerCostTicksPerCall = 80;
        Run("advance_throwers");
        AssertFails("advance_throwers", "cost-mean");
    }

    [TestMethod]
    public void AdvanceThrowers_Fails_WhenOneCallIsVerySlow()
    {
        _world.ThrowerOneSlowCallMicros = 9000;
        Run("advance_throwers");
        AssertFails("advance_throwers", "cost-max");
    }

    [TestMethod]
    public void TheMainStep_ThrowsMadeOnlyOnTheApproach_FailFewThrows()
    {
        _world.ThrowersThrow = false;
        _world.ThrowerApproachThrowsPerSecond = 1f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "few-throws");
    }

    [TestMethod]
    public void TheMainStep_ThrowsMadeOnlyAfterTheRelease_FailFewThrows()
    {
        _world.ThrowersThrow = false;
        _world.ThrowerReleasedThrowsPerSecond = 10f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "few-throws");
    }

    [TestMethod]
    public void TheMainStep_ReadsFalse_WhenTheEnemyWalksIntoACompanyThatStays()
    {
        _world.ThrowerHoldsAfterRelease = true;
        _world.ThrowerEnemySpeed = 3f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "no-melee");
        Assert.AreEqual("0.0", Fact(Line("advance_throwers"), "advanced_m"));
    }

    [TestMethod]
    public void TheMainStep_CanReadFalse_BecauseARuleThatDoesNotHoldTheCompanyWalksItIn()
    {
        _world.ThrowersStand = false;
        Run("advance_throwers");
        AssertFails("advance_throwers", "walked-in");
    }

    [TestMethod]
    public void BothSteps_ReadTheQuerySystemAndWalkTheSoldiersOnlyInTheMissionTick_BecauseTheAiJobOverlapsTheScreenTick()
    {
        foreach (var step in new[] { "advance_throwers", "advance_throwers_vanilla" })
        {
            Setup();
            Run(step);
            CollectionAssert.AreEqual(Array.Empty<string>(), _world.EngineReadsOutsideTheMissionTick.ToArray(), step);
        }

        Setup();
        _world.ThrowerRefusedAtTheOrder = true;
        Run("advance_throwers");
        AssertFails("advance_throwers", "never-published why=hold-fire");
        CollectionAssert.AreEqual(Array.Empty<string>(), _world.EngineReadsOutsideTheMissionTick.ToArray(), "the gate that refused the run is read in the mission tick too");
    }

    [TestMethod]
    public void TheMainStep_ReadsFalse_WhenTheCompanyWalksThroughItsPointWithoutStopping()
    {
        _world.ThrowersStand = false;
        _world.ThrowerEnemySpeed = 12f;
        Run("advance_throwers");
        AssertFails("advance_throwers", "never-stood");
    }

    // ---- advance_throwers_vanilla ----

    [TestMethod]
    public void AdvanceThrowersVanilla_Passes_WhenTheRuleIsOffAndTheCompanyWalksToMelee()
    {
        Run("advance_throwers_vanilla");

        var line = Line("advance_throwers_vanilla");
        StringAssert.Contains(line, "result=PASS", line);
        Assert.AreEqual("0", Fact(line, "published"));
        Assert.AreEqual("40", Fact(line, "armed_at_close"));
        Assert.AreEqual("97.0", Fact(line, "gap0"));
        Assert.AreEqual("4.0", Fact(line, "slowest"));
        Fact(line, "walked_m");
        Assert.AreEqual("0", Fact(line, "faulted"));
        Assert.AreEqual(0, _world.WritesOutsideTheMissionTick.Count);
    }

    [TestMethod]
    public void TheControl_ACompanyThatNeverMovedWhileTheEnemyWalkedIn_FailsStandBlind()
    {
        _world.ThrowerNeverCloses = true;
        _world.ThrowerEnemySpeed = 3f;
        Run("advance_throwers_vanilla");
        AssertFails("advance_throwers_vanilla", "stand-blind");
    }

    [TestMethod]
    public void TheControl_AFastEnemy_StillPasses_BecauseTheSpeedIsTheCompanysOwn()
    {
        _world.ThrowerEnemySpeed = 12f;
        Run("advance_throwers_vanilla");
        StringAssert.Contains(Line("advance_throwers_vanilla"), "result=PASS", Line("advance_throwers_vanilla"));
    }

    [TestMethod]
    public void AdvanceThrowersVanilla_TurnsTheSwitchBackOn_WhateverItFound()
    {
        _world.ThrowerNeverCloses = true;

        Run("advance_throwers_vanilla");

        Assert.IsFalse(_world.ThrowersAreOff, "the step clears its static at its end");
    }

    [TestMethod]
    public void AdvanceThrowersVanilla_Fails_WhenTheRuleIsStillOn()
    {
        _world.ThrowerOffSwitchIsIgnored = true;
        Run("advance_throwers_vanilla");
        AssertFails("advance_throwers_vanilla", "published-while-off");
    }

    [TestMethod]
    public void AdvanceThrowersVanilla_Fails_WhenTheCompanyNeverClosesWithTheEnemy()
    {
        _world.ThrowerNeverCloses = true;
        Run("advance_throwers_vanilla");
        AssertFails("advance_throwers_vanilla", "never-closed");
    }

    [TestMethod]
    public void AdvanceThrowersVanilla_SharesItsGroundReasonsWithTheMainStep()
    {
        _world.NoThrowers = true;
        Run("advance_throwers_vanilla");
        AssertFails("advance_throwers_vanilla", "no-throwers");
    }
}
