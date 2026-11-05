using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The hold-the-line proof slice (build-spec 9.2): who owns the attack flag, and the judge that compares a held company's blows with a control's.</summary>
[TestClass]
public class MarshalHoldRulesTests
{
    // ---- who writes the flag ----

    [TestMethod]
    public void FlagAction_HoldOnAndTheFlagSet_Clears()
    {
        Assert.AreEqual(AttackFlagAction.Clear, MarshalHoldRules.FlagAction(holdOn: true, flagSet: true, weCleared: false, engineCleared: false));
    }

    [TestMethod]
    public void FlagAction_HoldOnAndSomeoneElseClearedTheFlag_LeavesItAlone_SoWeNeverRestoreWhatWeDidNotClear()
    {
        Assert.AreEqual(AttackFlagAction.None, MarshalHoldRules.FlagAction(holdOn: true, flagSet: false, weCleared: false, engineCleared: false));
    }

    [TestMethod]
    public void FlagAction_HoldOnAndWeAlreadyCleared_DoesNothing()
    {
        Assert.AreEqual(AttackFlagAction.None, MarshalHoldRules.FlagAction(holdOn: true, flagSet: false, weCleared: true, engineCleared: false));
    }

    [TestMethod]
    public void FlagAction_HoldOffAfterWeCleared_Restores()
    {
        Assert.AreEqual(AttackFlagAction.Restore, MarshalHoldRules.FlagAction(holdOn: false, flagSet: false, weCleared: true, engineCleared: false));
    }

    [TestMethod]
    public void FlagAction_HoldOffAfterWeClearedAndTheFlagCameBackAnyway_ForgetsTheDebtWithoutAWrite()
    {
        Assert.AreEqual(AttackFlagAction.Forget, MarshalHoldRules.FlagAction(holdOn: false, flagSet: true, weCleared: true, engineCleared: false));
    }

    [TestMethod]
    public void FlagAction_HoldOffAndNeverCleared_DoesNothing()
    {
        Assert.AreEqual(AttackFlagAction.None, MarshalHoldRules.FlagAction(holdOn: false, flagSet: true, weCleared: false, engineCleared: false));
        Assert.AreEqual(AttackFlagAction.None, MarshalHoldRules.FlagAction(holdOn: false, flagSet: false, weCleared: false, engineCleared: false));
    }

    [TestMethod]
    public void FlagAction_HoldOffAfterWeClearedAndTheEngineClearedItToo_ForgetsTheDebt_SoTheFlagIsNotPutBackOnAClimber()
    {
        // ClimbingMachineDetachment.cs:241-243 clears CanAttack and CanDefend on a soldier it has just detached; our clear before it changes nothing, and a restore would arm him in the water.
        Assert.AreEqual(AttackFlagAction.Forget, MarshalHoldRules.FlagAction(holdOn: false, flagSet: false, weCleared: true, engineCleared: true));
    }

    [TestMethod]
    public void FlagAction_TheEngineClearedItFirst_NeverWritesIt_WhateverTheHoldDoes()
    {
        Assert.AreEqual(AttackFlagAction.None, MarshalHoldRules.FlagAction(holdOn: true, flagSet: false, weCleared: false, engineCleared: true));
        Assert.AreEqual(AttackFlagAction.None, MarshalHoldRules.FlagAction(holdOn: false, flagSet: false, weCleared: false, engineCleared: true));
    }

    [TestMethod]
    public void FlagAction_EveryCombination_RestoresOnlyAFlagWeClearedThatNobodyElseTook()
    {
        var seen = 0;
        foreach (var holdOn in new[] { false, true })
        foreach (var flagSet in new[] { false, true })
        foreach (var weCleared in new[] { false, true })
        foreach (var engineCleared in new[] { false, true })
        {
            var action = MarshalHoldRules.FlagAction(holdOn, flagSet, weCleared, engineCleared);
            var label = "holdOn=" + holdOn + " flagSet=" + flagSet + " weCleared=" + weCleared + " engineCleared=" + engineCleared;
            Assert.AreEqual(!holdOn && weCleared && !flagSet && !engineCleared, action == AttackFlagAction.Restore, label);
            Assert.AreEqual(holdOn && flagSet, action == AttackFlagAction.Clear, label);
            seen++;
        }

        Assert.AreEqual(16, seen);
    }

    // ---- the judge ----

    private static HoldProof Judge(int flagOff, int held, int control) => MarshalHoldRules.Judge(flagOff, held, control);

    [TestMethod]
    public void Judge_AHeldCompanyThatStruckNobodyWhileTheControlStruckManyTimes_Passes()
    {
        Assert.AreEqual(HoldProof.Pass, Judge(flagOff: 30, held: 0, control: 40));
    }

    [TestMethod]
    public void Judge_AHeldCompanyAtOneTenthOfTheControl_Passes_AndJustOverIt_Fails()
    {
        Assert.AreEqual(HoldProof.Pass, Judge(30, 4, 40));
        Assert.AreEqual(HoldProof.Fail, Judge(30, 5, 40));
    }

    [TestMethod]
    public void Judge_AHeldCompanyThatStruckAsOftenAsTheControl_Fails_SoADeadFlagCannotPass()
    {
        Assert.AreEqual(HoldProof.Fail, Judge(30, 40, 40));
    }

    [TestMethod]
    public void Judge_AControlThatStruckTooFewTimes_ProvesNothing()
    {
        Assert.AreEqual(HoldProof.NoControl, Judge(30, 0, MarshalTuning.HoldControlMinBlows - 1));
        Assert.AreEqual(HoldProof.Pass, Judge(30, 0, MarshalTuning.HoldControlMinBlows));
    }

    [TestMethod]
    public void Judge_TooFewSoldiersWithTheFlagOff_IsPending_SoAFlagThatNeverReachedAnyoneIsNotAPass()
    {
        Assert.AreEqual(HoldProof.Pending, Judge(MarshalTuning.HoldMinFlagOff - 1, 0, 40));
        Assert.AreEqual(HoldProof.Pass, Judge(MarshalTuning.HoldMinFlagOff, 0, 40));
    }

    // ---- the window ----

    [TestMethod]
    public void Window_JudgesOnlyAfterTheSettleAndTheProofSeconds_FromTheBlowsOfThatSpan()
    {
        var window = new MarshalHoldWindow();
        window.Begin(100.0);

        Assert.IsNull(window.Observe(101.0, flagOff: 30, heldBlows: 7, controlBlows: 50), "inside the settle");
        Assert.IsNull(window.Observe(103.0, 30, 9, 50), "the baseline is taken here: 9 and 50");
        Assert.IsNull(window.Observe(112.9, 30, 9, 60), "short of the proof seconds");
        var verdict = window.Observe(113.0, 30, 9, 62);

        Assert.IsNotNull(verdict);
        Assert.AreEqual(HoldProof.Pass, verdict!.Value.Proof);
        Assert.AreEqual(0, verdict.Value.HeldBlows, "the blows before the baseline do not count");
        Assert.AreEqual(12, verdict.Value.ControlBlows);
    }

    [TestMethod]
    public void Window_BlowsAfterTheBaseline_CountAgainstTheHeldCompany()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);
        window.Observe(3.0, 30, 0, 0);

        var verdict = window.Observe(13.0, 30, 15, 20);

        Assert.AreEqual(HoldProof.Fail, verdict!.Value.Proof);
        Assert.AreEqual(15, verdict.Value.HeldBlows);
    }

    [TestMethod]
    public void Window_WithTooFewFlagOffSoldiersAtTheSettle_WaitsForAnObservationThatHasThem()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);

        Assert.IsNull(window.Observe(3.0, flagOff: 0, heldBlows: 0, controlBlows: 0), "no baseline without soldiers holding");
        Assert.IsNull(window.Observe(14.0, 0, 0, 0));
        Assert.IsNull(window.Observe(15.0, 30, 0, 100), "the baseline is taken now");
        Assert.AreEqual(HoldProof.Pass, window.Observe(25.0, 30, 0, 130)!.Value.Proof);
    }

    [TestMethod]
    public void Window_AfterAVerdict_JudgesNothingMoreUntilBegunAgain()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);
        window.Observe(3.0, 30, 0, 0);
        Assert.IsNotNull(window.Observe(13.0, 30, 0, 50));

        Assert.IsNull(window.Observe(30.0, 30, 0, 90));
        window.Begin(40.0);
        Assert.IsNull(window.Observe(43.0, 30, 0, 90));
        Assert.IsNotNull(window.Observe(53.0, 30, 0, 140));
    }

    [TestMethod]
    public void Window_AControlThatStruckTooFewTimes_SaysNoControl_NotPass()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);
        window.Observe(3.0, 30, 0, 0);

        Assert.AreEqual(HoldProof.NoControl, window.Observe(13.0, 30, 0, 3)!.Value.Proof);
    }

    [TestMethod]
    public void Window_AQuietControl_TakesANewBaselineAndStaysOpen_SoALaterBusyWindowGetsARealVerdict()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);
        window.Observe(3.0, 30, 0, 0);
        Assert.AreEqual(HoldProof.NoControl, window.Observe(13.0, 30, 0, 9)!.Value.Proof, "nine control blows: under the minimum of ten");

        Assert.IsNull(window.Observe(14.0, 30, 0, 9), "the new baseline is taken here: the control stood at 9");
        Assert.IsNull(window.Observe(23.9, 30, 0, 40), "short of the proof seconds");
        var verdict = window.Observe(24.0, 30, 0, 29)!.Value;

        Assert.AreEqual(HoldProof.Pass, verdict.Proof);
        Assert.AreEqual(20, verdict.ControlBlows, "counted from the new baseline, not the first");
        Assert.AreEqual(0, verdict.HeldBlows);
    }

    [TestMethod]
    public void Window_AControlThatStaysQuiet_KeepsAnsweringNoControl_UntilItFights()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);
        window.Observe(3.0, 30, 0, 0);

        Assert.AreEqual(HoldProof.NoControl, window.Observe(13.0, 30, 0, 2)!.Value.Proof);
        window.Observe(14.0, 30, 0, 2);
        Assert.AreEqual(HoldProof.NoControl, window.Observe(24.0, 30, 0, 4)!.Value.Proof);
        window.Observe(25.0, 30, 0, 4);
        Assert.AreEqual(HoldProof.Pass, window.Observe(35.0, 30, 0, 24)!.Value.Proof);
        Assert.IsNull(window.Observe(50.0, 30, 0, 90), "a judged window closes");
    }

    [TestMethod]
    public void Window_AHeldCompanyThatStruckWhileTheControlWasQuiet_DoesNotCarryItsBlowsIntoTheNextWindow()
    {
        var window = new MarshalHoldWindow();
        window.Begin(0.0);
        window.Observe(3.0, 30, 0, 0);
        window.Observe(13.0, 30, 6, 4);
        window.Observe(14.0, 30, 6, 4);

        Assert.AreEqual(HoldProof.Pass, window.Observe(24.0, 30, 6, 30)!.Value.Proof, "the six blows came before the new baseline");
    }
}
