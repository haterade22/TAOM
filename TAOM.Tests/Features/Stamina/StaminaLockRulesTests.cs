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
}
