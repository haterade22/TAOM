using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CustomBattles.Muster;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CustomBattles;

/// <summary>
/// A player who starts a Custom Battle as a captain serves under another lord (#1184). The muster screen picks that
/// lord: the first lord of the faction, in the screen's order, who is not the player's own character and is not
/// already in the army as a hero unit. The engine reads the general's name for every field battle, so a missing
/// general stopped the start with a null reference.
/// </summary>
[TestClass]
public class MusterGeneralChoiceTests
{
    private static readonly string[] Lords = { "lord_a", "lord_b", "lord_c" };

    [TestMethod]
    public void Pick_PlayerIsTheFirstLord_TakesTheNextLord()
        => Assert.AreEqual("lord_b", MusterGeneralChoice.Pick(Lords, "lord_a", new string[0]));

    [TestMethod]
    public void Pick_PlayerIsALaterLord_TakesTheFirstLord()
        => Assert.AreEqual("lord_a", MusterGeneralChoice.Pick(Lords, "lord_b", new string[0]));

    [TestMethod]
    public void Pick_LordAlreadyInTheArmy_IsSkipped()
        => Assert.AreEqual("lord_c", MusterGeneralChoice.Pick(Lords, "lord_a", new[] { "lord_b" }));

    [TestMethod]
    public void Pick_OnlyThePlayerIsALord_ReturnsNull()
        => Assert.IsNull(MusterGeneralChoice.Pick(new[] { "lord_a" }, "lord_a", new string[0]));

    [TestMethod]
    public void Pick_EveryOtherLordIsInTheArmy_ReturnsNull()
        => Assert.IsNull(MusterGeneralChoice.Pick(Lords, "lord_a", new[] { "lord_b", "lord_c" }));

    [TestMethod]
    public void Pick_NoLords_ReturnsNull()
        => Assert.IsNull(MusterGeneralChoice.Pick(new string[0], "lord_a", new string[0]));

    [TestMethod]
    public void Pick_PlayerCharacterUnknown_TakesTheFirstLord()
        => Assert.AreEqual("lord_a", MusterGeneralChoice.Pick(Lords, null, new string[0]));

    [TestMethod]
    public void Pick_TheEnemysCommanderAndHeroes_AreSkipped()
        => Assert.AreEqual("lord_c", MusterGeneralChoice.Pick(Lords, null, new[] { "lord_a", "lord_b" }));

    [TestMethod]
    public void ForStart_Commander_NeedsNoGeneral()
        => Assert.AreEqual(((string?)null, false), MusterGeneralChoice.ForStart(false, false, Lords, "lord_a", new string[0]));

    [TestMethod]
    public void ForStart_CaptainInAFieldBattle_GetsTheNextLord()
        => Assert.AreEqual(((string?)"lord_b", false), MusterGeneralChoice.ForStart(true, false, Lords, "lord_a", new string[0]));

    [TestMethod]
    public void ForStart_CaptainInAFieldBattleWithNoLordLeft_IsRefused()
        => Assert.AreEqual(((string?)null, true),
            MusterGeneralChoice.ForStart(true, false, Lords, "lord_a", new[] { "lord_b", "lord_c" }));

    [TestMethod]
    public void ForStart_CaptainInASiegeWithNoLordLeft_StartsWithoutAGeneral()
        => Assert.AreEqual(((string?)null, false),
            MusterGeneralChoice.ForStart(true, true, Lords, "lord_a", new[] { "lord_b", "lord_c" }));

    [TestMethod]
    public void ForStart_CaptainInASiege_GetsTheNextLordToo()
        => Assert.AreEqual(((string?)"lord_b", false), MusterGeneralChoice.ForStart(true, true, Lords, "lord_a", new string[0]));

    /// <summary>
    /// The view model is game-tested UI, so this pins its wiring by source text. The decision is ForStart's, taken on
    /// the player type and the game type, with both armies' heroes and the enemy's commander kept out of the choice.
    /// A refusal returns before the army is handed over, and the general goes to both engine calls.
    /// </summary>
    [TestMethod]
    public void TcbVM_TakesTheStartDecisionFromForStart_AndPassesTheGeneralToBothEngineCalls()
    {
        var source = File.ReadAllText(RepoPaths.RepoPath("Client", "Features", "CustomBattles", "Muster", "TcbVM.cs"));

        var decision = source.IndexOf(
            "MusterGeneralChoice.ForStart(Field.PlayerType == CustomBattlePlayerType.Sergeant, Field.IsSiege,",
            System.StringComparison.Ordinal);
        var refusal = source.IndexOf("if (refused)", System.StringComparison.Ordinal);
        var handOver = source.IndexOf("CustomBattleMuster.Pending = spec;", System.StringComparison.Ordinal);
        var parties = source.IndexOf("GetCustomBattleParties(playerCmd, general, enemyCmd,", System.StringComparison.Ordinal);
        var data = source.IndexOf("PrepareBattleData(playerCmd, general, parties[0], parties[1],", System.StringComparison.Ordinal);

        Assert.IsTrue(decision >= 0, "Start must take its decision from MusterGeneralChoice.ForStart");
        Assert.IsTrue(decision < refusal && refusal < handOver && handOver < parties && parties < data,
            "order: decide, refuse, hand the army over, build the parties, prepare the battle");
        StringAssert.Contains(source, "Player.HeroUnitIds.Concat(Enemy.HeroUnitIds).Append(enemyCmd.StringId)");
        StringAssert.Contains(source, "var general = generalId == null ? null : Player.Lord(generalId);");
        Assert.IsFalse(source.Contains("playerCmd, null,"),
            "A null general makes BannerlordMissions.OpenCustomBattleMission throw for a captain (#1184).");
    }
}
