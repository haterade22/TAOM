using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Stamina.Cheats;
using TAOM.Features.Stamina.Domain;
using TAOM.Features.Stamina.Hooks;

namespace TAOM.Tests.Features.Stamina;

[TestClass]
public class StaminaGateAndCheatTests
{
    [DataTestMethod]
    [DataRow(true, false, false, DisplayName = "field battle")]
    [DataRow(false, true, false, DisplayName = "siege")]
    [DataRow(false, false, true, DisplayName = "sally-out")]
    public void IsArmable_EnabledCombatBattle_IsTrue(bool fieldBattle, bool siege, bool sallyOut)
    {
        Assert.IsTrue(StaminaMissionGate.IsArmable(enabled: true, fordsMatch: false, combat: true, fieldBattle, siege, sallyOut));
    }

    [TestMethod]
    public void IsArmable_SwitchOff_IsFalseForEveryMission()
    {
        Assert.IsFalse(StaminaMissionGate.IsArmable(enabled: false, fordsMatch: false, combat: true, fieldBattle: true, siege: false, sallyOut: false));
        Assert.IsFalse(StaminaMissionGate.IsArmable(enabled: false, fordsMatch: true, combat: true, fieldBattle: false, siege: false, sallyOut: false));
    }

    [TestMethod]
    public void IsArmable_FordsMatchWithNoTeamAi_IsTrue()
    {
        // The Fords of War match is built with NoTeamAI (FordsMissionLauncher.cs:100), so the three battle tests are
        // false for it. It is the unattended test battle, so it must run the rules.
        Assert.IsTrue(StaminaMissionGate.IsArmable(enabled: true, fordsMatch: true, combat: true, fieldBattle: false, siege: false, sallyOut: false));
    }

    [TestMethod]
    public void IsArmable_HideoutOrTownFight_CombatWithNoTeamAi_IsFalse()
    {
        Assert.IsFalse(StaminaMissionGate.IsArmable(enabled: true, fordsMatch: false, combat: true, fieldBattle: false, siege: false, sallyOut: false));
    }

    [TestMethod]
    public void IsArmable_ArenaOrConversation_NotACombatMission_IsFalse()
    {
        Assert.IsFalse(StaminaMissionGate.IsArmable(enabled: true, fordsMatch: false, combat: false, fieldBattle: true, siege: false, sallyOut: false));
    }

    [DataTestMethod]
    [DataRow("fresh", StaminaBand.Fresh)]
    [DataRow("Winded", StaminaBand.Winded)]
    [DataRow("TIRED", StaminaBand.Tired)]
    [DataRow(" exhausted ", StaminaBand.Exhausted)]
    public void TryParse_BandWord_GivesThatBand(string word, StaminaBand expected)
    {
        Assert.IsTrue(StaminaCheatArgs.TryParse(new[] { word, "player" }, out var band, out _, out var error), error);

        Assert.AreEqual(expected, band);
    }

    [TestMethod]
    public void TryParse_Auto_GivesNoBand_SoTheRulesDecide()
    {
        Assert.IsTrue(StaminaCheatArgs.TryParse(new[] { "auto", "all" }, out var band, out var target, out _));

        Assert.IsNull(band);
        Assert.AreEqual(StaminaCheatTarget.All, target);
    }

    [DataTestMethod]
    [DataRow("player", StaminaCheatTarget.Player)]
    [DataRow("Allies", StaminaCheatTarget.Allies)]
    [DataRow("ENEMIES", StaminaCheatTarget.Enemies)]
    [DataRow("all", StaminaCheatTarget.All)]
    public void TryParse_TargetWord_GivesThatTarget(string word, StaminaCheatTarget expected)
    {
        Assert.IsTrue(StaminaCheatArgs.TryParse(new[] { "tired", word }, out _, out var target, out var error), error);

        Assert.AreEqual(expected, target);
    }

    [DataTestMethod]
    [DataRow("sleepy", "player", "band")]
    [DataRow("3", "player", "band")]
    [DataRow("", "player", "band")]
    [DataRow("tired", "everyone", "target")]
    [DataRow("tired", "1", "target")]
    [DataRow("tired", "", "target")]
    [DataRow("winded,tired", "player", "band")]
    [DataRow("+1", "player", "band")]
    [DataRow("-0", "player", "band")]
    [DataRow("tired", "player,allies", "target")]
    [DataRow("tired", "+2", "target")]
    public void TryParse_UnknownWordOrANumber_FailsAndNamesTheWord(string bandWord, string targetWord, string named)
    {
        // Enum.TryParse reads a number, a sign and a comma list as a value: "3" must not read as Exhausted, "+1" as
        // Winded, nor "winded,tired" as their bitwise sum, which is Exhausted.
        Assert.IsFalse(StaminaCheatArgs.TryParse(new[] { bandWord, targetWord }, out _, out _, out var error));

        StringAssert.Contains(error, named);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void TryParse_WrongNumberOfWords_Fails(int count)
    {
        var args = new string[count];
        for (var i = 0; i < count; i++)
            args[i] = "tired";

        Assert.IsFalse(StaminaCheatArgs.TryParse(args, out _, out _, out var error));
        StringAssert.Contains(error, "Expected");
    }
}
