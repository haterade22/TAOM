using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Features.MarshalsWord.Domain;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Features.SeatOfSeeing;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// The field trial steps of Massed fire, the hold-the-line proof slice, the hollow square and the spacing repair against the stand-in: each passes on a world where the
/// feature works and fails with its own reason when the one thing it proves is broken. The game is the only proof of the facts themselves.
/// </summary>
[TestClass]
public class CommandTrialMarshalM5StepsTests
{
    private CommandTrialWorld _world = null!;
    private ListLogger _log = null!;

    [TestInitialize]
    public void Setup()
    {
        _world = new CommandTrialWorld();
        _log = new ListLogger();
    }

    private void Run(string step)
    {
        Assert.IsTrue(SeatTrialSteps.TryChoose(step, out var chosen, out var error), error);
        var script = new CommandTrialScript(_world, chosen, _log, _ => { });
        _world.Run(script, 400f);
        Assert.IsTrue(script.IsDone, "the run ended");
    }

    private string Line(string step) => _log.Lines.Single(l => l.StartsWith("[SeatTrial] step=" + step + " ", StringComparison.Ordinal));

    private void AssertPasses(string step) => StringAssert.Contains(Line(step), "result=PASS", Line(step));

    private void AssertFails(string step, params string[] mentions)
    {
        var line = Line(step);
        StringAssert.Contains(line, "result=FAIL", line);
        foreach (var mention in mentions)
            StringAssert.Contains(line, mention, line);
    }

    private static string Fact(string line, string key)
    {
        var match = Regex.Match(line, @"\b" + key + @"=(\S+)");
        Assert.IsTrue(match.Success, key + " is not in: " + line);
        return match.Groups[1].Value;
    }

    // ---- Massed fire ----

    [TestMethod]
    public void VolleyMassed_Passes_WhenTheShareRuleGivesTheWordAndTheArchersLoose()
    {
        Run("volley_massed");

        AssertPasses("volley_massed");
        var line = Line("volley_massed");
        Assert.AreEqual("25", Fact(line, "massed_holding"));
        Assert.AreEqual("30", Fact(line, "eligible"));
        Assert.AreEqual("25", Fact(line, "peak_holding"), "the census the word was given on is among the peaks");
        Assert.AreEqual("12", Fact(line, "free_shooters"), "the free-fire control is on the line");
        Assert.AreEqual("1", Fact(line, "judged"));
        Assert.IsFalse(_world.AnyKeyHeld);
        Assert.AreEqual(FireMode.Free, _world.MarshalFireMode(0), "the step puts the archers back at free fire");
    }

    [TestMethod]
    public void VolleyMassed_NamesItsOwnReason_WhenNoWordIsEverGiven()
    {
        _world.MassedGivesWord = false;

        Run("volley_massed");

        AssertFails("volley_massed", "reason=no-word", "peak_holding=", "peak_idle=");
    }

    [TestMethod]
    public void VolleyMassed_NamesWordNotRecorded_WhenTheCountRoseAndItsNumbersDidNot()
    {
        _world.MassedNumbersComeWithTheRelease = true;

        Run("volley_massed");

        AssertFails("volley_massed", "reason=word-not-recorded", "peak_holding=");
    }

    [TestMethod]
    public void VolleyMassed_Fails_WhenTheWordWasGivenBelowTheShare()
    {
        _world.MassedHoldingAtWord = 10;

        Run("volley_massed");

        AssertFails("volley_massed", "massed_holding=10", "eligible=30");
    }

    [TestMethod]
    public void VolleyMassed_Fails_WhenTooFewHoldToProveARelease()
    {
        _world.MassedHoldingAtWord = MarshalTuning.VolleyMinHolding - 1;
        _world.MassedEligibleAtWord = _world.MassedHoldingAtWord;

        Run("volley_massed");

        AssertFails("volley_massed", "massed_holding=4");
    }

    [TestMethod]
    public void VolleyMassed_Fails_WhenTheArchersDoNotLooseAtTheWord()
    {
        _world.WordLoosesThem = false;

        Run("volley_massed");

        AssertFails("volley_massed", "judged=1", "loosed=1");
    }

    [TestMethod]
    public void VolleyMassed_Fails_WhenTheSessionFaulted()
    {
        _world.MarshalSessionFaulted = true;

        Run("volley_massed");

        AssertFails("volley_massed");
    }

    // ---- hold the line: the proof rests on the driver's judged check ----

    [TestMethod]
    public void HoldLineProof_Passes_OnTheDriversJudgedCheck_WithItsNumbers_AndTheFlagComesBack()
    {
        Run("hold_line_proof");

        AssertPasses("hold_line_proof");
        var line = Line("hold_line_proof");
        Assert.AreEqual("pass", Fact(line, "driver"));
        Assert.AreEqual("0", Fact(line, "held_blows"));
        Assert.IsTrue(int.Parse(Fact(line, "control_blows")) >= MarshalTuning.HoldControlMinBlows, line);
        Assert.AreEqual("30", Fact(line, "flag_off"));
        Assert.AreEqual("1", Fact(line, "restored"));
        Assert.IsFalse(_world.AnyKeyHeld);
    }

    [TestMethod]
    public void HoldLineProof_PassesOnALaterBusyWindow_WhenTheFirstWindowFoundTheControlQuiet_TheRunOf010922()
    {
        _world.ControlQuietSeconds = 14f;
        _world.ControlFightSeconds = 40f;

        Run("hold_line_proof");

        AssertPasses("hold_line_proof");
        Assert.AreEqual("pass", Fact(Line("hold_line_proof"), "driver"));
        Assert.IsTrue(int.Parse(Fact(Line("hold_line_proof"), "control_blows")) >= MarshalTuning.HoldControlMinBlows);
    }

    [TestMethod]
    public void HoldLineProof_PrintsTheClampsTotalsOfTheWholeHold_BeforeTheLiftDropsThem()
    {
        Run("hold_line_proof");

        var line = Line("hold_line_proof");
        Assert.IsTrue(int.Parse(Fact(line, "closer")) > int.Parse(Fact(line, "farther")), line);
        Assert.IsTrue(int.Parse(Fact(line, "farther")) > 0, line);
    }

    [TestMethod]
    public void HoldLineProof_Passes_WhenTheControlsFightEndsBeforeAnyLaterWindowCouldOpen_ThePassOfRun225226()
    {
        _world.ControlFightSeconds = 12f;
        _world.EnemiesArriveAfterSeconds = 40f;

        Run("hold_line_proof");

        AssertPasses("hold_line_proof");
        Assert.AreEqual("pass", Fact(Line("hold_line_proof"), "driver"));
    }

    [TestMethod]
    public void HoldLineProof_NamesEnemyNotInReach_WhenTooFewEnemiesComeNear()
    {
        _world.EnemiesNearCount = MarshalTrialRules.HoldMinEnemiesInReach - 1;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=enemy-not-in-reach", "peak_near=9", "driver=pass");
    }

    [TestMethod]
    public void HoldLineProof_Fails_WhenTheDriverJudgesTheHeldCompanyStruck()
    {
        _world.HeldCompanyStillStrikes = true;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=held-company-struck", "driver=fail");
        Assert.AreNotEqual("0", Fact(Line("hold_line_proof"), "held_blows"));
    }

    [TestMethod]
    public void HoldLineProof_NamesControlQuiet_WhenTheControlStrikesTooFewBlows()
    {
        _world.ControlBlowsPerSecond = 0.1f;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=control-quiet", "driver=nocontrol");
    }

    [TestMethod]
    public void HoldLineProof_NamesFlagNotOff_WhenTheFlagNeverClears_BecauseTheDriverHasNoWindow()
    {
        _world.HoldFlagWorks = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=flag-not-off", "driver=none");
    }

    [TestMethod]
    public void HoldLineProof_NamesDriverNeverJudged_WithinItsNamedLimit_WhenTheDriverKeepsNoWindow()
    {
        _world.DriverJudgesHoldLine = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=driver-never-judged", "driver=none");
    }

    [TestMethod]
    public void HoldLineProof_NamesFlagNotRestored_WhenTheHoldEndsAndTheFlagStaysOff()
    {
        _world.HoldFlagIsRestored = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=flag-not-restored", "restored=0");
    }

    [TestMethod]
    public void HoldLineProof_NamesNotQualified_WhenTheLineNeverQualifiesForTheHold_WithTheClauseThatFailed()
    {
        _world.ShieldWallOrderWorks = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=not-qualified", "why=arrangement", "qualified=0");
    }

    [TestMethod]
    public void HoldLineProof_NamesNoSplit_BeforeAnyBlowIsCounted()
    {
        _world.SplitWorks = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=no-split");
    }

    [TestMethod]
    public void HoldLineProof_NamesNoSplit_WithBothCompaniesSoldiers_WhenTheOrderLeavesTheControlEmpty()
    {
        _world.SplitLeavesTheControlEmpty = true;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=no-split", "held_units=30", "control_units=0");
    }

    [TestMethod]
    public void HoldLineProof_NamesNotControllable_WhenACompanyIsUnderTheAi()
    {
        _world.CompaniesAreControllable = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=not-controllable");
    }

    [TestMethod]
    public void HoldLineProof_NamesHoldNotSet_WhenTheKeyDoesNothing()
    {
        _world.HoldKeyWorks = false;

        Run("hold_line_proof");

        AssertFails("hold_line_proof", "reason=hold-not-set");
    }

    // ---- hold the line: the slot under Stop ----

    [TestMethod]
    public void HoldLineSlot_Passes_WhenTheStruckLineStaysNearItsAnchorsUnderStop_WithTheOrderBesideTheDistance()
    {
        Run("hold_line_slot");

        AssertPasses("hold_line_slot");
        var line = Line("hold_line_slot");
        Assert.AreEqual("Stop", Fact(line, "order"));
        Assert.AreEqual("30", Fact(line, "anchored"));
        Assert.IsTrue(int.Parse(Fact(line, "samples")) >= MarshalTrialRules.SlotMinSamples, line);
        Assert.IsTrue(int.Parse(Fact(line, "steered")) >= MarshalTuning.SlotMinSteered, line);
        Assert.IsFalse(_world.AnyKeyHeld);
    }

    [TestMethod]
    public void HoldLineSlot_NamesNoClamp_WhenTheClampNeverSteersAnyone_EvenThoughTheLineStaysInPlace()
    {
        _world.ClampSteersNobody = true;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=no-clamp", "clamps=0", "far=0");
        Assert.AreEqual("0", Fact(Line("hold_line_slot"), "closer"));
    }

    [TestMethod]
    public void HoldLineSlot_CountsTheClampsFromItsOwnStart_SoAProcessThatSteeredBeforeDoesNotCountForTheStep()
    {
        _world.ClampsBeforeTheStep = 5000;
        _world.ClampSteersNobody = true;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=no-clamp", "clamps=0");
    }

    [TestMethod]
    public void HoldLineSlot_WaitsForTheClampToAct_WhenItActsLongAfterTheSamplesAreJudged()
    {
        _world.ClampStartsAfterSeconds = 30f;

        Run("hold_line_slot");

        AssertPasses("hold_line_slot");
        Assert.IsTrue(int.Parse(Fact(Line("hold_line_slot"), "clamps")) >= 1, Line("hold_line_slot"));
    }

    [TestMethod]
    public void HoldLineSlot_NamesNoClamp_WhenTheClampActsOnlyAfterTheStepsLimit()
    {
        _world.ClampStartsAfterSeconds = MarshalTrialRules.HoldVerdictWaitSeconds + 60f;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=no-clamp", "clamps=0");
    }

    [TestMethod]
    public void HoldLineSlot_PrintsHowManySteeredReadsEndedCloserAndFarther_AndHowManyInputsTheClampReplaced()
    {
        Run("hold_line_slot");

        AssertPasses("hold_line_slot");
        var line = Line("hold_line_slot");
        Assert.IsTrue(int.Parse(Fact(line, "clamps")) >= 1, line);
        Assert.IsTrue(int.Parse(Fact(line, "closer")) > int.Parse(Fact(line, "farther")), line);
    }

    [TestMethod]
    public void HoldLineSlot_NamesSteeredAway_WhenTheClampWalksTheWrongWay_WithTheTotalsOnTheLine()
    {
        _world.ClampWalksTheWrongWay = true;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=steered-away");
        var line = Line("hold_line_slot");
        Assert.IsTrue(int.Parse(Fact(line, "farther")) > int.Parse(Fact(line, "closer")), line);
    }

    [TestMethod]
    public void HoldLineSlot_NamesSteeredAway_WhenTheDriversSlotCheckFailsLongAfterTheLinePassedItsFirstSamples()
    {
        _world.DriverSlotFailsAfterSteeringSeconds = 8f;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=steered-away");
    }

    [TestMethod]
    public void HoldLineSlot_CountsNoSampleBeforeTheFirstSteer_SoItReadsAFullWindowOfCompletedReadsAfterIt()
    {
        _world.ClampStartsAfterSeconds = 30f;

        Run("hold_line_slot");

        AssertPasses("hold_line_slot");
        var readAfterTheSteer = _world.Time - _world.FirstClampAt;
        Assert.IsTrue(readAfterTheSteer >= MarshalTrialRules.SlotMinSamples * (float)MarshalTuning.HoldReadSeconds, "the step ended " + readAfterTheSteer + " s after the first steer");
    }

    [TestMethod]
    public void HoldLineSlot_NamesTooFewSteers_WhenTheClampActsButFewerThanFiveReadsWereSteered()
    {
        _world.SteeredReadsPerSecond = 0.1f;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=too-few-steers");
        Assert.IsTrue(int.Parse(Fact(Line("hold_line_slot"), "clamps")) >= 1);
    }

    [TestMethod]
    public void HoldLineSlot_Passes_WhenSoldiersStandFarButTheClampSteersThemCloser_BecauseFarIsAFactAndNotATest()
    {
        _world.SlotsAreKept = false;

        Run("hold_line_slot");

        AssertPasses("hold_line_slot");
        Assert.AreEqual("12", Fact(Line("hold_line_slot"), "far"));
    }

    [TestMethod]
    public void HoldLineSlot_NamesNobodyStruck_WhenTheLineIsNeverWounded_SoAZeroUnderStopIsNotAPass()
    {
        _world.TheLineIsNeverWounded = true;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=nobody-struck", "wounded=0");
    }

    [TestMethod]
    public void HoldLineSlot_NamesEnemyNotInReach_WhenTooFewEnemiesComeNear()
    {
        _world.EnemiesNearCount = MarshalTrialRules.HoldMinEnemiesInReach - 1;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=enemy-not-in-reach");
    }

    [TestMethod]
    public void HoldLineSlot_NamesNotQualified_WhenTheShieldWallNeverStands()
    {
        _world.ShieldWallOrderWorks = false;

        Run("hold_line_slot");

        AssertFails("hold_line_slot", "reason=not-qualified", "why=arrangement");
    }

    // ---- hold the line: the strike back ----

    [TestMethod]
    public void HoldLineStrike_Passes_WhenAWoundedSoldiersFlagReadsOnAndTheLineStrikesBack()
    {
        Run("hold_line_strike");

        AssertPasses("hold_line_strike");
        var line = Line("hold_line_strike");
        Assert.AreEqual("5", Fact(line, "settled"));
        Assert.AreEqual("5", Fact(line, "flag_on"));
        Assert.IsTrue(int.Parse(Fact(line, "blows_after")) >= 1, line);
        Assert.AreEqual("1", Fact(line, "driver_strike"));
    }

    [TestMethod]
    public void HoldLineStrike_NamesFlagNotOff_WhenTheFlagNeverClearsBeforeTheWound_SoABuildThatNeverClearsItCannotPass()
    {
        _world.HoldFlagWorks = false;

        Run("hold_line_strike");

        AssertFails("hold_line_strike", "reason=flag-not-off", "flag_off_before=0");
    }

    [TestMethod]
    public void HoldLineStrike_ReadsTheFlagsOffBeforeAnyWound_AndPrintsHowManyWereOff()
    {
        Run("hold_line_strike");

        Assert.AreEqual("30", Fact(Line("hold_line_strike"), "flag_off_before"));
    }

    [TestMethod]
    public void HoldLineStrike_NamesFlagStayedOff_WhenTheStrikeBackNeverGivesTheFlagBack()
    {
        _world.StrikeBackWorks = false;

        Run("hold_line_strike");

        AssertFails("hold_line_strike", "reason=flag-stayed-off", "flag_on=0");
    }

    [TestMethod]
    public void HoldLineStrike_Fails_WhenTheDriversOwnStrikeCheckNeverPasses_EvenWithTheFlagsBack()
    {
        _world.StrikeCheckStaysSilent = true;

        Run("hold_line_strike");

        AssertFails("hold_line_strike", "driver_strike=0");
    }

    [TestMethod]
    public void HoldLineStrike_Passes_WhenTheFirstWoundedSoldiersDieBlowlessAndALaterWoundLandsABlow()
    {
        _world.LaterWoundStrikesAfterSeconds = 12f;

        Run("hold_line_strike");

        AssertPasses("hold_line_strike");
        Assert.IsTrue(int.Parse(Fact(Line("hold_line_strike"), "blows_after")) >= 1);
    }

    [TestMethod]
    public void HoldLineStrike_NamesNoBlowAfterWound_WhenNoBlowEverLandsWithinTheLimit()
    {
        _world.StrikeBlowsNever = true;

        Run("hold_line_strike");

        AssertFails("hold_line_strike", "reason=no-blow-after-wound", "blows_after=0");
    }

    [TestMethod]
    public void HoldLineStrike_NamesNobodyWounded_WhenTheEnemyNeverHurtsTheLine()
    {
        _world.TheLineIsNeverWounded = true;

        Run("hold_line_strike");

        AssertFails("hold_line_strike", "reason=nobody-wounded");
    }

    // ---- hold the line: a charge gives the flag back ----

    [TestMethod]
    public void HoldLineFlag_Passes_WhenTheChargeGivesEverySoldierTheFlagBackInTime()
    {
        Run("hold_line_flag");

        AssertPasses("hold_line_flag");
        var line = Line("hold_line_flag");
        Assert.AreEqual("30", Fact(line, "flag_off_before"));
        Assert.AreEqual("0", Fact(line, "flag_off_after"));
        Assert.AreEqual("Charge", Fact(line, "order"));
        Assert.AreEqual("1", Fact(line, "held"), "the toggle stays on: the order ended the line, not the key");
    }

    [TestMethod]
    public void HoldLineFlag_NamesFlagNotRestored_WhenTheChargeLeavesTheFlagOff()
    {
        _world.ChargeRestoresTheFlag = false;

        Run("hold_line_flag");

        AssertFails("hold_line_flag", "reason=flag-not-restored", "flag_off_after=30");
    }

    [TestMethod]
    public void HoldLineFlag_NamesFlagNotRestored_WhenTheRestoreComesLaterThanTheLimit()
    {
        _world.RestoreLagSeconds = MarshalTrialRules.HoldRestoreSeconds + 1f;

        Run("hold_line_flag");

        AssertFails("hold_line_flag", "reason=flag-not-restored");
    }

    [TestMethod]
    public void HoldLineFlag_NamesFlagNotOff_WhenTheFlagNeverClearedSoARestoreProvesNothing()
    {
        _world.HoldFlagWorks = false;

        Run("hold_line_flag");

        AssertFails("hold_line_flag", "reason=flag-not-off", "flag_off_before=0");
    }

    // ---- the approach both volley steps share ----

    [TestMethod]
    public void VolleyMassed_AWalkLongerThanTheOldFlatLimit_StillSettlesWithinTheLimitThatFitsIt()
    {
        _world.EnemyStartMetres = 600f;
        _world.Companies[0].Speed = 2f;

        Run("volley_massed");

        AssertPasses("volley_massed");
    }

    [TestMethod]
    public void VolleyMassed_WhenTheArchersNeverSettle_NamesTheWalkTheSpeedTheLimitAndTheOrder()
    {
        _world.ArchersCannotSettle = true;

        Run("volley_massed");

        var line = Line("volley_massed");
        AssertFails("volley_massed", "reason=not-settled", "walk=", "speed=", "limit=", "to_order=", "order=", "controllable=", "gap=");
        Assert.IsTrue(float.Parse(Fact(line, "limit"), System.Globalization.CultureInfo.InvariantCulture) >= MarshalTrialRules.ApproachFloorSeconds, line);
    }

    // ---- the hollow square ----

    [TestMethod]
    public void SquareSideKept_Passes_WhenThePlainSquareKeepsItsSideAndEverySoldierHasASlot()
    {
        Run("square_side_kept");

        AssertPasses("square_side_kept");
        var line = Line("square_side_kept");
        Assert.AreEqual("SquareFormation", Fact(line, "type"));
        Assert.AreEqual("11.0", Fact(line, "before"), "the side the square formed with is on the line");
        Assert.AreEqual("15.0", Fact(line, "chosen"), "four unit widths over the side it had");
        Assert.AreEqual(Fact(line, "units0"), (int.Parse(Fact(line, "units1")) + 1).ToString());
        Assert.AreEqual(Fact(line, "units1"), Fact(line, "placed"));
    }

    [TestMethod]
    public void SquareSideKept_NamesLaidOut_WhenTheMixedLayoutPlacesTheSoldiers_AndPrintsTheFact()
    {
        _world.SquareLaidOut = true;

        Run("square_side_kept");

        AssertFails("square_side_kept", "reason=laid-out", "laid_out=1");
    }

    [TestMethod]
    public void SquareSideKept_PrintsLaidOutZero_WhenTheSquareAlonePlacesTheSoldiers()
    {
        Run("square_side_kept");

        AssertPasses("square_side_kept");
        Assert.AreEqual("0", Fact(Line("square_side_kept"), "laid_out"));
    }

    [TestMethod]
    public void SquareSideKept_NamesNotThePlainSquare_WhenTheSchiltronIsBuilt()
    {
        _world.SquareIsPlain = false;

        Run("square_side_kept");

        AssertFails("square_side_kept", "reason=not-the-plain-square", "type=RectilinearSchiltronFormation");
    }

    [TestMethod]
    public void SquareSideKept_NamesDragDidNothing_WhenTheDragLeavesTheSideItFormedWith()
    {
        _world.SquareDragDoesNothing = true;

        Run("square_side_kept");

        AssertFails("square_side_kept", "reason=drag-did-nothing", "before=11.0", "side0=11.0");
    }

    [TestMethod]
    public void SquareSideKept_NamesDragDidNothing_EvenWhenTheSquareFormedWithinOneUnitOfTheOldChosenSide_TheRunThatPassed()
    {
        _world.SquareFormedSide = 13.5f;
        _world.SquareDragDoesNothing = true;

        Run("square_side_kept");

        AssertFails("square_side_kept", "reason=drag-did-nothing");
    }

    [TestMethod]
    public void SquareSideKept_NamesSideNotKept_WhenTheSquareGrowsByItsPerimeter()
    {
        _world.SquareKeepsItsSide = false;

        Run("square_side_kept");

        AssertFails("square_side_kept", "reason=side-not-kept");
    }

    [TestMethod]
    public void SquareSideKept_NamesUnplaced_WhenASoldierHasNoSlot()
    {
        _world.SquarePlacesEveryone = false;

        Run("square_side_kept");

        AssertFails("square_side_kept", "reason=unplaced");
    }

    // ---- the spacing repair ----

    [TestMethod]
    public void SpacingOneRider_Passes_WhenTheLimitsReadAsFootSoldiersOnACompanyWithOneRider()
    {
        Run("spacing_one_rider");

        AssertPasses("spacing_one_rider");
        var line = Line("spacing_one_rider");
        Assert.AreEqual("Infantry", Fact(line, "reads"));
        Assert.AreEqual("1", Fact(line, "any"));
        Assert.AreEqual("0", Fact(line, "significant"));
    }

    [TestMethod]
    public void SpacingOneRider_Fails_WhenTheLimitsStillReadAsCavalry()
    {
        _world.LimitsReadAsInfantry = false;

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reads=Cavalry");
    }

    [TestMethod]
    public void SpacingOneRider_NamesTheReasonTheRepairIsNotApplied_WithTheWord()
    {
        _world.SpacingWhy = "repair-off";

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=repair-not-applied", "why=repair-off");
    }

    [TestMethod]
    public void SpacingOneRider_NamesRiderNotCounted_WhenTheCompanyHasAMountedMajority()
    {
        _world.RiderIsSignificant = true;

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=rider-not-counted", "significant=1");
    }

    [TestMethod]
    public void SpacingOneRider_NamesRiderNotMoved_WithTheCompanysSoldiers_WhenTheTransferMovesNobody()
    {
        _world.RiderTransferMovesNobody = true;

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=rider-not-moved", "before=30", "units=30");
    }

    [TestMethod]
    public void SpacingOneRider_NamesNoRiderInCompany_WhenTheSoldierWhoJoinedIsOnFoot()
    {
        _world.TheSoldierWhoJoinedIsOnFoot = true;

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=no-rider-in-company", "units=31", "riders=0");
    }

    [TestMethod]
    public void SpacingOneRider_WaitsForTheEnginesCountToCatchUpWithTheRider()
    {
        _world.EngineCountsTheRiderAfter = 4f;

        Run("spacing_one_rider");

        AssertPasses("spacing_one_rider");
    }

    [TestMethod]
    public void SpacingOneRider_NamesRiderNotCounted_WhenTheCensusSeesTheRiderAndTheEngineNeverCountsHim()
    {
        _world.EngineCountsTheRiderAfter = 1000f;

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=rider-not-counted", "riders=1", "any=0");
    }

    [TestMethod]
    public void SpacingOneRider_NamesNoInfantry_WhenNoCompanyOfFootSoldiersStands()
    {
        _world.Companies.RemoveAll(c => !c.Ranged);

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=no-infantry");
    }

    [TestMethod]
    public void SpacingOneRider_NamesNoRiderCompany_WhenNoRiderCanBeMoved()
    {
        _world.RiderMoves = false;

        Run("spacing_one_rider");

        AssertFails("spacing_one_rider", "reason=no-rider-company");
    }
}
