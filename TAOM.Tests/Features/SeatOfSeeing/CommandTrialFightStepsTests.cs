using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The field trial's steps of stages S4 and S5 (build-spec section 12) against the stand-in: each step passes on a world that works, and fails
/// with its own facts when the one feature it proves is broken. A fix's test helper must not default to the state the fix is about, so every
/// knob below starts at the working value and a test turns exactly one. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialFightStepsTests
{
    private static readonly string[] StageFour = { "fight_on", "morale_held", "back_to_stand" };
    private static readonly string[] StageFive = { "hero_falls", "take_soldier" };

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

    private CommandTrialScript Run(params string[] steps)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose("enter," + string.Join(",", steps), out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _finished.Add);
        _world.Run(script, 200f);
        Assert.IsTrue(script.IsDone, "the run ended");
        return script;
    }

    private string Line(string step) => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    private void AssertFails(string step, string fact, string value)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        Assert.AreEqual(value, Fact(line, fact), line);
    }

    private void AssertPasses(string step) => StringAssert.Contains(Line(step), "result=PASS", Line(step));

    // ---- The table ----

    [TestMethod]
    public void TheRows_AreInTheContractsOrder_AfterStageThree()
    {
        var names = SeatTrialSteps.All.Select(s => s.Name).ToList();

        var fightRows = StageFour.Concat(StageFive).ToList();
        CollectionAssert.AreEqual(fightRows, names.Where(fightRows.Contains).ToList(), "stage six and seven rows sit between the hand-off steps and the fall");
        Assert.AreEqual(13, names.IndexOf("fight_on"));
    }

    [TestMethod]
    public void EachRow_NamesWhatItNeeds_AndASubsetWithoutItIsRefused()
    {
        var needs = new Dictionary<string, string>
        {
            ["fight_on"] = "enter", ["morale_held"] = "fight_on", ["back_to_stand"] = "fight_on", ["hero_falls"] = "enter", ["take_soldier"] = "hero_falls",
        };

        foreach (var pair in needs)
        {
            var step = pair.Key;
            var need = pair.Value;
            CollectionAssert.AreEqual(new[] { need }, SeatTrialSteps.All.Single(s => s.Name == step).Requires.ToArray(), step);
            var list = need == "enter" ? step : "enter," + step;
            Assert.IsFalse(SeatTrialSteps.TryChoose(list, out _, out var error), step);
            StringAssert.Contains(error, "needs step '" + need + "'");
        }
    }

    [DataTestMethod]
    [DataRow("enter,fight_on,morale_held,back_to_stand")]
    [DataRow("enter,hero_falls,take_soldier")]
    public void TheSubsetsTheFeatureDocNamesForStagesFourAndFive_AreAccepted_AndEachNeedsEnter(string subset)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(subset, out var chosen, out var error), error);
        CollectionAssert.AreEqual(subset.Split(','), chosen.Select(s => s.Name).ToArray());
        Assert.IsFalse(SeatTrialSteps.TryChoose(subset.Substring("enter,".Length), out _, out _), "without enter the game refuses the list");
    }

    [TestMethod]
    public void EveryRow_HasAHandler_AndABudgetInRealSeconds()
    {
        foreach (var step in StageFour.Concat(StageFive))
        {
            CollectionAssert.Contains(CommandTrialScript.HandlerNames.ToList(), step);
            Assert.IsTrue(SeatTrialSteps.All.Single(s => s.Name == step).BudgetSeconds >= 10f, step);
        }
    }

    // ---- Whole stages ----

    [TestMethod]
    public void StageFour_PassesOnAWorldThatWorks()
    {
        Run(StageFour);

        foreach (var step in StageFour)
            AssertPasses(step);
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
        Assert.AreEqual("AI", Fact(Line("fight_on"), "controller"));
        Assert.AreEqual("Player", Fact(Line("back_to_stand"), "controller"));
    }

    [TestMethod]
    public void StageFive_PassesOnAWorldThatWorks()
    {
        Run(StageFive);

        foreach (var step in StageFive)
            AssertPasses(step);
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
        Assert.AreEqual("3", Fact(Line("take_soldier"), "owned_before"));
        Assert.AreEqual("3", Fact(Line("take_soldier"), "owned_after"));
    }

    [TestMethod]
    public void TheWholeTable_PassesOnAWorldThatWorks()
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(null, out var all, out _));
        var script = new CommandTrialScript(_world, all, _log, _finished.Add);
        _world.Run(script, 400f);

        Assert.IsTrue(script.IsDone);
        Assert.AreEqual(0, _log.Lines.Count(l => l.Contains("result=FAIL")), string.Join("\n", _log.Lines.Where(l => l.Contains("result=FAIL"))));
        CollectionAssert.AreEqual(new[] { "final" }, _finished);
    }

    [TestMethod]
    public void TheTrialsSettings_StartWithTheHandOffOff_SoTheStageOneStepsReadAPlayerHero()
    {
        var settings = new CommandTrialSettings();

        Assert.IsFalse(settings.HeroFightsOn);
        Assert.IsTrue(settings.TakeSoldierAfterFall);
    }

    // ---- fight_on: each conjunct alone ----

    [TestMethod]
    public void FightOn_WhenTheEntryPressIsNotTaken_FailsWithThatReasonAndTheStateTheLeaveLeft()
    {
        _world.IgnorePressesFrom = 3;

        Run("fight_on");

        var line = Line("fight_on");
        StringAssert.Contains(line, "result=FAIL reason=press-not-taken state=Off", line);
        Assert.AreEqual(3, _world.Presses, "the entry, the leave and the entry that was not taken");
    }

    [TestMethod]
    public void FightOn_WhenTheHeroIsNeverHandedOver_FailsOnTheController()
    {
        _world.HandsTheHero = false;

        Run("fight_on");

        StringAssert.Contains(Line("fight_on"), "reason=not-handed");
        StringAssert.Contains(Line("fight_on"), "result=FAIL");
    }

    [TestMethod]
    public void FightOn_WhenTheHeroStandsStill_FailsOnTheDistanceMoved()
    {
        _world.HeroFightsForReal = false;

        Run("fight_on");

        AssertFails("fight_on", "controller", "AI");
        Assert.IsTrue(float.Parse(Fact(Line("fight_on"), "moved"), System.Globalization.CultureInfo.InvariantCulture) < SeatTrialRules.FoughtOnMetres, Line("fight_on"));
    }

    [TestMethod]
    public void FightOn_WhenTheHandOffRetreatsTheHero_FailsOnRetreating()
    {
        _world.RetreatsOnHandOff = true;

        Run("fight_on");

        AssertFails("fight_on", "retreating", "1");
    }

    [TestMethod]
    public void FightOn_WhenTheHandOffPanicsTheHero_FailsOnPanicked()
    {
        _world.PanicsOnHandOff = true;

        Run("fight_on");

        AssertFails("fight_on", "panicked", "1");
    }

    [TestMethod]
    public void FightOn_WhenTheGateIsNeverRaised_FailsOnTheGate()
    {
        _world.HandOffRaisesGate = false;

        Run("fight_on");

        AssertFails("fight_on", "gate", "0");
    }

    [TestMethod]
    public void FightOn_WhenThePanelRefusesTheHandedHero_FailsOnThePanel()
    {
        _world.PanelOpensForAiHero = false;

        Run("fight_on");

        AssertFails("fight_on", "panel", "0");
    }

    [TestMethod]
    public void FightOn_WhenTheSeatFaultsMidStep_FailsOnFaulted()
    {
        _world.FaultMidStep = true;

        Run("fight_on");

        AssertFails("fight_on", "faulted", "1");
    }

    // ---- morale_held ----

    [TestMethod]
    public void MoraleHeld_WritesMoraleZeroOnTheHandedHero_FromTheMissionTick()
    {
        Run("fight_on", "morale_held");

        Assert.IsTrue(_world.Writes.Contains("morale"));
        CollectionAssert.DoesNotContain(_world.WritesOutsideTheMissionTick, "morale");
    }

    [TestMethod]
    public void MoraleHeld_ReadsTheMoraleRightAfterTheWrite_AndPrintsIt()
    {
        Run("fight_on", "morale_held");

        AssertPasses("morale_held");
        Assert.AreEqual("0.0", Fact(Line("morale_held"), "written"));
    }

    [TestMethod]
    public void MoraleHeld_WhenTheWriteTakesNoEffect_FailsOnTheMoraleItReadsBack()
    {
        // Without a stimulus the hero cannot panic, so every later fact would read well: the step has to see the morale it asked for.
        _world.MoraleWriteIsIgnored = true;

        Run("fight_on", "morale_held");

        var line = Line("morale_held");
        StringAssert.Contains(line, "result=FAIL", line);
        StringAssert.Contains(line, "reason=morale-not-written");
        Assert.AreEqual("50.0", Fact(line, "written"), line);
    }

    [TestMethod]
    public void MoraleHeld_WhenTheLowMoralePanicsTheHero_FailsOnPanicked()
    {
        _world.MoraleMakesHimPanic = true;

        Run("fight_on", "morale_held");

        AssertPasses("fight_on");
        AssertFails("morale_held", "panicked", "1");
    }

    [TestMethod]
    public void MoraleHeld_WhenTheLowMoraleRetreatsTheHero_FailsOnRetreating()
    {
        _world.MoraleMakesHimRetreat = true;

        Run("fight_on", "morale_held");

        AssertFails("morale_held", "retreating", "1");
    }

    [TestMethod]
    public void MoraleHeld_WhenTheHandOffEndsDuringTheWait_FailsOnTheController()
    {
        _world.DropsTheHandOffAfter = 8f;

        Run("fight_on", "morale_held");

        AssertPasses("fight_on");
        AssertFails("morale_held", "controller", "other");
    }

    [TestMethod]
    public void MoraleHeld_WhenTheHeroIsNotHanded_FailsBeforeItWritesAnything()
    {
        // fight_on fails here, and the next step reads the hero it left behind.
        _world.HandsTheHero = false;

        Run("fight_on", "morale_held");

        StringAssert.Contains(Line("morale_held"), "reason=not-handed");
        CollectionAssert.DoesNotContain(_world.Writes, "morale");
    }

    // ---- back_to_stand ----

    [TestMethod]
    public void BackToStand_TurnsTheFightValueOff_AndEntersAgain()
    {
        Run("fight_on", "back_to_stand");

        Assert.IsFalse(_world.FightOnSetting);
        Assert.IsTrue(_world.Presses >= 5, "the leave and the entry of fight_on, then the leave and the entry of this step");
    }

    [TestMethod]
    public void BackToStand_WhenTheHeroIsNotHandedBack_FailsOnTheControllerAfterTheLeave()
    {
        _world.HandsTheHeroBack = false;

        Run("fight_on", "back_to_stand");

        AssertFails("back_to_stand", "after_leave", "other");
        Assert.AreEqual("other", Fact(Line("back_to_stand"), "controller"));
    }

    [TestMethod]
    public void BackToStand_WhenTheHeroIsHandedBackOnlyAfterTheLeaveSettled_FailsOnTheControllerAfterTheLeave()
    {
        _world.HandBackDelaySeconds = 0.6f;

        Run("fight_on", "back_to_stand");

        AssertFails("back_to_stand", "after_leave", "other");
        Assert.AreEqual("Player", Fact(Line("back_to_stand"), "controller"), "he is handed back in the end, so only the leave's own fact fails");
    }

    [TestMethod]
    public void BackToStand_WhenTheGateStaysUp_FailsOnTheGate()
    {
        _world.GateStaysUpAfterHandBack = true;

        Run("fight_on", "back_to_stand");

        AssertFails("back_to_stand", "gate", "1");
    }

    [TestMethod]
    public void BackToStand_WhenTheHeroKeepsWalkingAfterTheHandBack_FailsOnTheSpeed()
    {
        _world.DriftAfterHandBackPerSecond = 2f;

        Run("fight_on", "back_to_stand");

        AssertFails("back_to_stand", "speed", Fact(Line("back_to_stand"), "speed"));
        Assert.IsTrue(float.Parse(Fact(Line("back_to_stand"), "speed"), System.Globalization.CultureInfo.InvariantCulture) >= SeatTrialRules.HeroSpeedLimit);
    }

    [TestMethod]
    public void BackToStand_StopsTheHeroCompanyFromTheMissionTick_BeforeItLeaves_SoNoChargeIsLeftToMoveHim()
    {
        Run("fight_on", "back_to_stand");

        CollectionAssert.Contains(_world.Writes, "stop-formation");
        CollectionAssert.DoesNotContain(_world.WritesOutsideTheMissionTick, "stop-formation");
        Assert.AreEqual(_world.Presses - 2, _world.PressesWhenStopped, "the stop comes before the leave and the entry of this step");
    }

    [TestMethod]
    public void BackToStand_ReportsTheHeroSpeedOnTheAiBeforeTheLeave()
    {
        Run("fight_on", "back_to_stand");

        AssertPasses("back_to_stand");
        Assert.AreEqual("0.0", Fact(Line("back_to_stand"), "before"));
    }

    [TestMethod]
    public void BackToStand_WhenTheCompanyIgnoresTheStop_FailsOnTheGround_AndNeverLeaves()
    {
        _world.CompanyIgnoresTheStop = true;

        Run("fight_on", "back_to_stand");

        AssertFails("back_to_stand", "reason", "ground-not-quiet");
        Assert.AreEqual("3.0", Fact(Line("back_to_stand"), "before"));
        Assert.AreEqual(3, _world.Presses, "the leave and the entry of fight_on, and no press of this step: nothing was measured against a moving company");
    }

    [TestMethod]
    public void BackToStand_WhenTheSeatFaults_FailsOnFaulted()
    {
        _world.FaultMidStep = true;

        Run("fight_on", "back_to_stand");

        AssertFails("back_to_stand", "faulted", "1");
    }

    // ---- hero_falls ----

    [TestMethod]
    public void HeroFalls_KillsTheHeroFromTheMissionTick_ThroughTheCheat()
    {
        Run("hero_falls");

        CollectionAssert.Contains(_world.Kills, "hero");
        CollectionAssert.DoesNotContain(_world.WritesOutsideTheMissionTick, "kill-hero");
    }

    [TestMethod]
    public void HeroFalls_KeepsToALargeCompanyBeforeTheKill_AndClosesThePanelAsThePlayerDoes()
    {
        Run("hero_falls");

        var writes = _world.Writes;
        var select = writes.IndexOf("select-company");
        var open = writes.IndexOf("open-panel");
        var close = writes.IndexOf("close-panel");
        var kill = writes.IndexOf("kill-hero");
        Assert.IsTrue(select >= 0 && select < open && open < close && close < kill, string.Join(",", writes));
        Assert.AreEqual("Company", Fact(Line("hero_falls"), "kept"));
        Assert.AreEqual(1, _world.PlayerCloses);
    }

    [TestMethod]
    public void HeroFalls_WhenTheCompanyCannotBeKept_FailsBeforeItKillsAnyone()
    {
        _world.CompanyKeepFindsNobody = true;

        Run("hero_falls");

        AssertFails("hero_falls", "reason", "not-kept");
        CollectionAssert.DoesNotContain(_world.Kills, "hero");
    }

    [TestMethod]
    public void HeroFalls_WhenNoCompanyHasUnits_FailsBeforeItKillsAnyone()
    {
        _world.NoCompany = true;

        Run("hero_falls");

        AssertFails("hero_falls", "reason", "no-company");
        CollectionAssert.DoesNotContain(_world.Kills, "hero");
    }

    [TestMethod]
    public void HeroFalls_WhenTheHeroCannotDie_FailsBeforeItKillsAnyone()
    {
        _world.HeroCanDie = false;

        Run("hero_falls");

        StringAssert.Contains(Line("hero_falls"), "reason=hero-immortal");
        CollectionAssert.DoesNotContain(_world.Kills, "hero");
    }

    [TestMethod]
    public void HeroFalls_WhenTheGateStaysUpAfterTheFall_FailsOnTheGate()
    {
        _world.FallLowersTheGate = false;

        Run("hero_falls");

        AssertFails("hero_falls", "gate", "1");
        Assert.IsTrue(float.Parse(Fact(Line("hero_falls"), "waited"), System.Globalization.CultureInfo.InvariantCulture) >= SeatTrialRules.FallWaitSeconds - 0.1f,
            "a gate that never drops is waited for up to the limit, then fails: " + Line("hero_falls"));
    }

    [TestMethod]
    public void HeroFalls_WhenTheSeatReactsAtItsNextMissionTick_ReadsTheReactionNotTheKill()
    {
        // The engine clears the main agent inside the kill; the Seat lowers the gate and says the fall one mission tick later.
        _world.FallReactionSeconds = 0f;

        Run("hero_falls");

        AssertPasses("hero_falls");
        Assert.AreEqual("1", Fact(Line("hero_falls"), "messages"));
        Assert.AreEqual("0", Fact(Line("hero_falls"), "gate"));
    }

    [TestMethod]
    public void HeroFalls_WhenTheSeatReactsInsideTheLimit_Passes()
    {
        _world.FallReactionSeconds = SeatTrialRules.FallWaitSeconds / 2f;

        Run("hero_falls");

        AssertPasses("hero_falls");
    }

    [TestMethod]
    public void HeroFalls_WhenTheSeatReactsPastTheLimit_FailsOnTheGate()
    {
        _world.FallReactionSeconds = SeatTrialRules.FallWaitSeconds + 1f;

        Run("hero_falls");

        AssertFails("hero_falls", "gate", "1");
        AssertFails("hero_falls", "messages", "0");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    public void HeroFalls_WhenTheMessageIsMissingOrRepeated_FailsOnTheMessageCount(int says)
    {
        _world.FallSays = says;

        Run("hero_falls");

        AssertFails("hero_falls", "messages", says.ToString());
    }

    [TestMethod]
    public void HeroFalls_WhenTheMessageSaysAnotherThing_FailsOnTheMessage()
    {
        _world.FallSaysTheWrongThing = true;

        Run("hero_falls");

        AssertFails("hero_falls", "message_fell", "0");
    }

    [TestMethod]
    public void HeroFalls_WhenTheHeroOwnedNoFormation_Fails_BecauseATakeThenProvesNothing()
    {
        _world.OwnedFormations = 0;

        Run("hero_falls");

        AssertFails("hero_falls", "owned", "0");
    }

    [TestMethod]
    public void HeroFalls_WhenTheHeroIsNeverHanded_FailsOnTheController()
    {
        _world.HandsTheHero = false;

        Run("hero_falls");

        StringAssert.Contains(Line("hero_falls"), "reason=not-handed");
        CollectionAssert.DoesNotContain(_world.Kills, "hero");
    }

    // ---- take_soldier ----

    [TestMethod]
    public void TakeSoldier_PressesTheKeyWithTheCompanyStillKept_AndAimsAtNobody()
    {
        Run("hero_falls", "take_soldier");

        AssertPasses("take_soldier");
        Assert.AreEqual(1, _world.TakePresses);
        Assert.AreEqual(SeatKeepKind.Company, _world.KeepingAtTheTake, "the take goes through the kept company, not through where the view happens to look");
        Assert.IsFalse(_world.InputLog.Any(i => i.HasValue && (i.Value.LookYaw != 0f || i.Value.LookPitch != 0f)), "no look input aims: the pointer decides nothing");
    }

    [TestMethod]
    public void TakeSoldier_WhenNoTakeHappens_FailsOnTook()
    {
        _world.TakeWorks = false;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "took", "0");
    }

    [TestMethod]
    public void TakeSoldier_WhenAnotherSoldierIsTaken_FailsOnTook()
    {
        _world.TakesTheWrongSoldier = true;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "took", "0");
    }

    [TestMethod]
    public void TakeSoldier_WhenTheCommandIsNotHandedOver_FailsOnTheOwnedCount()
    {
        _world.TakeHandsTheCommand = false;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "owned_after", "0");
    }

    [TestMethod]
    public void TakeSoldier_WhenOneFormationStaysOnTheAi_FailsOnStillAi()
    {
        _world.TakeLeavesOneFormationOnTheAi = true;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "still_ai", "1");
    }

    [TestMethod]
    public void TakeSoldier_WhenTheControllerIsNotHandedOver_FailsOnTheControllerOwner()
    {
        _world.TakeHandsTheController = false;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "controller_owner", "0");
    }

    [TestMethod]
    public void TakeSoldier_WhenFewerFormationsAreHandedThanTheHeroOwned_Fails()
    {
        _world.TakeHandsOver = 2;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "owned_after", "2");
    }

    [TestMethod]
    public void TakeSoldier_WhenThePanelDoesNotOpenForTheNewAgent_FailsOnThePanel()
    {
        _world.PanelClosedForTheTakenSoldier = true;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "panel", "0");
    }

    [TestMethod]
    public void TakeSoldier_WhenTheFallEndedTheKeep_FailsWithoutPressingTheKey()
    {
        _world.TheFallEndsTheKeep = true;

        Run("hero_falls", "take_soldier");

        AssertFails("take_soldier", "reason", "no-kept-company");
        Assert.AreEqual(0, _world.TakePresses);
    }

    [TestMethod]
    public void TakeSoldier_WhenTheSeatFaultsMidStep_FailsOnFaulted()
    {
        _world.FaultOnTake = true;

        Run("hero_falls", "take_soldier");

        StringAssert.Contains(Line("take_soldier"), "result=FAIL");
        Assert.AreEqual("1", Fact(Line("take_soldier"), "faulted"));
    }
}
