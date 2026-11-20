using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaLockRulesTests
{
    [TestMethod]
    public void PathFor_NotExhausted_IsNoneForEitherController()
    {
        Assert.AreEqual(StaminaLockPath.None, StaminaLockRules.PathFor(exhausted: false, aiControlled: true, usingGameObject: false));
        Assert.AreEqual(StaminaLockPath.None, StaminaLockRules.PathFor(exhausted: false, aiControlled: false, usingGameObject: false));
    }

    [TestMethod]
    public void PathFor_ExhaustedAiSoldier_IsTheAiInputPath()
    {
        Assert.AreEqual(StaminaLockPath.AiInput, StaminaLockRules.PathFor(exhausted: true, aiControlled: true, usingGameObject: false));
    }

    [TestMethod]
    public void PathFor_ExhaustedPlayerSoldier_IsThePlayerInputPath()
    {
        Assert.AreEqual(StaminaLockPath.PlayerInput, StaminaLockRules.PathFor(exhausted: true, aiControlled: false, usingGameObject: false));
    }

    [TestMethod]
    public void PathFor_ExhaustedSoldierOnASiegeEngine_IsNotLocked()
    {
        Assert.AreEqual(StaminaLockPath.None, StaminaLockRules.PathFor(exhausted: true, aiControlled: true, usingGameObject: true));
        Assert.AreEqual(StaminaLockPath.None, StaminaLockRules.PathFor(exhausted: true, aiControlled: false, usingGameObject: true));
    }

    [DataTestMethod]
    [DataRow(StaminaLockPath.None, StaminaLockPath.AiInput, true, false, DisplayName = "an AI soldier becomes exhausted")]
    [DataRow(StaminaLockPath.PlayerInput, StaminaLockPath.AiInput, true, false, DisplayName = "an exhausted hero is handed to the AI")]
    [DataRow(StaminaLockPath.AiInput, StaminaLockPath.None, false, true, DisplayName = "an AI soldier recovers")]
    [DataRow(StaminaLockPath.AiInput, StaminaLockPath.PlayerInput, false, true, DisplayName = "the player takes an exhausted AI soldier")]
    [DataRow(StaminaLockPath.AiInput, StaminaLockPath.AiInput, false, false, DisplayName = "an AI soldier stays exhausted")]
    [DataRow(StaminaLockPath.None, StaminaLockPath.PlayerInput, false, false, DisplayName = "the player becomes exhausted")]
    [DataRow(StaminaLockPath.PlayerInput, StaminaLockPath.None, false, false, DisplayName = "the player recovers")]
    [DataRow(StaminaLockPath.None, StaminaLockPath.None, false, false, DisplayName = "nothing changes")]
    public void Step_PathChange_StartsAndStopsTheAiStrip(StaminaLockPath before, StaminaLockPath after, bool start, bool stop)
    {
        var step = StaminaLockRules.Step(before, after);

        Assert.AreEqual(start, step.StartAiStrip, "start");
        Assert.AreEqual(stop, step.StopAiStrip, "stop");
    }

    // The counters behind the taom.stamina.lock check: only a band the rules decided may count, or a console force
    // or an MCM switch would pass or fail the check by itself.
    [DataTestMethod]
    [DataRow(StaminaBand.Tired, StaminaBand.Exhausted, true, true, false, true, false, true, DisplayName = "the rules exhaust a soldier")]
    [DataRow(StaminaBand.Tired, StaminaBand.Exhausted, false, true, false, false, false, true, DisplayName = "the console forces a soldier Exhausted: not an exhaustion")]
    [DataRow(StaminaBand.Exhausted, StaminaBand.Tired, true, true, true, false, true, false, DisplayName = "a soldier the rules exhausted recovers")]
    [DataRow(StaminaBand.Exhausted, StaminaBand.Tired, true, false, false, false, false, false, DisplayName = "a soldier the console exhausted comes back: no entry counted, so no recovery")]
    [DataRow(StaminaBand.Exhausted, StaminaBand.Fresh, false, true, true, false, false, false, DisplayName = "an MCM switch or the console frees an Exhausted soldier: not a recovery")]
    [DataRow(StaminaBand.Exhausted, StaminaBand.Exhausted, true, false, false, false, false, true, DisplayName = "auto leaves him Exhausted: his 15 s start now, not at the force")]
    [DataRow(StaminaBand.Exhausted, StaminaBand.Exhausted, true, true, true, false, false, false, DisplayName = "still Exhausted under the rules: the clock runs on")]
    [DataRow(StaminaBand.Exhausted, StaminaBand.Exhausted, false, true, true, false, false, false, DisplayName = "forced while Exhausted")]
    [DataRow(StaminaBand.Fresh, StaminaBand.Winded, true, true, false, false, false, false, DisplayName = "a band change away from Exhausted")]
    public void Tally_OnlyABandTheRulesDecidedCounts(StaminaBand before, StaminaBand after, bool byRules, bool wasByRules, bool entryCounted,
        bool entered, bool recovered, bool startClock)
    {
        var tally = StaminaLockRules.Tally(before, after, byRules, wasByRules, entryCounted);

        Assert.AreEqual(entered, tally.Entered, "entered");
        Assert.AreEqual(recovered, tally.Recovered, "recovered");
        Assert.AreEqual(startClock, tally.StartClock, "start clock");
    }

    [TestMethod]
    public void Tally_AnEntryStaysCountedWhileTheSoldierIsExhausted_AndIsForgottenWhenHeLeaves()
    {
        Assert.IsTrue(StaminaLockRules.Tally(StaminaBand.Tired, StaminaBand.Exhausted, true, true, false).EntryCounted);
        Assert.IsTrue(StaminaLockRules.Tally(StaminaBand.Exhausted, StaminaBand.Exhausted, false, true, true).EntryCounted,
            "a force on top of a real exhaustion keeps the entry, so his later recovery under the rules counts");
        Assert.IsFalse(StaminaLockRules.Tally(StaminaBand.Tired, StaminaBand.Exhausted, false, true, false).EntryCounted);
        Assert.IsFalse(StaminaLockRules.Tally(StaminaBand.Exhausted, StaminaBand.Tired, true, true, true).EntryCounted);
    }
}
