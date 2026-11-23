using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrainingGrounds.Teams;

namespace TAOM.Tests.Features.TrainingGrounds.Teams;

/// <summary>
/// #1528: the pure rules of a team practice fight. The six AI teams split by index: the first
/// <see cref="TeamPracticeRules.AllyTeamCount"/> are the player's side, the rest the other side.
/// </summary>
[TestClass]
public class TeamPracticeRulesTests
{
    private const int Six = 6;

    [DataTestMethod]
    [DataRow(0, true)]
    [DataRow(1, true)]
    [DataRow(2, false)]
    [DataRow(3, false)]
    [DataRow(4, false)]
    [DataRow(5, false)]
    public void IsAllyTeam_SixTeams_TheFirstTwoAreAllies(int index, bool expected)
    {
        Assert.AreEqual(expected, TeamPracticeRules.IsAllyTeam(index, Six));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    public void IsAllyTeam_TwoOrFewerTeams_HasNoAlly(int teamCount)
    {
        for (var i = 0; i < teamCount; i++)
            Assert.IsFalse(TeamPracticeRules.IsAllyTeam(i, teamCount), $"index {i} of {teamCount}");
    }

    [TestMethod]
    public void IsAllyTeam_NegativeIndex_IsNotAnAlly()
    {
        Assert.IsFalse(TeamPracticeRules.IsAllyTeam(-1, Six));
    }

    [TestMethod]
    public void AreEnemies_EveryPairOfSixTeams_AreEnemiesOnlyAcrossTheSides()
    {
        for (var a = 0; a < Six; a++)
        {
            for (var b = a + 1; b < Six; b++)
            {
                var sameSide = (a < 2) == (b < 2);
                Assert.AreEqual(!sameSide, TeamPracticeRules.AreEnemies(a, b, Six), $"teams {a} and {b}");
                Assert.AreEqual(!sameSide, TeamPracticeRules.AreEnemies(b, a, Six), $"teams {b} and {a}");
            }
        }
    }

    [TestMethod]
    public void AreEnemies_ATeamAndItself_AreNotEnemies()
    {
        Assert.IsFalse(TeamPracticeRules.AreEnemies(3, 3, Six));
    }

    [TestMethod]
    public void AreEnemies_NoAllySide_EveryoneIsAnEnemy()
    {
        Assert.IsTrue(TeamPracticeRules.AreEnemies(0, 1, 2), "a list of two teams keeps vanilla's free-for-all");
    }

    [TestMethod]
    public void IsTeamWin_PlayerStandingAllEnteredNoOpponentLeft_IsTrue()
    {
        Assert.IsTrue(TeamPracticeRules.IsTeamWin(playerStanding: true, unspawned: 0, alive: 2, anyOpponentStanding: false));
    }

    [TestMethod]
    public void IsTeamWin_PlayerDown_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.IsTeamWin(playerStanding: false, unspawned: 0, alive: 2, anyOpponentStanding: false));
    }

    [TestMethod]
    public void IsTeamWin_FightersStillToEnter_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.IsTeamWin(playerStanding: true, unspawned: 1, alive: 2, anyOpponentStanding: false));
    }

    [TestMethod]
    public void IsTeamWin_NoFighterAlive_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.IsTeamWin(playerStanding: true, unspawned: 0, alive: 0, anyOpponentStanding: false),
            "vanilla's own end check owns the case where nobody is left");
    }

    [TestMethod]
    public void IsTeamWin_AnOpponentStillStands_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.IsTeamWin(playerStanding: true, unspawned: 0, alive: 2, anyOpponentStanding: true));
    }

    [TestMethod]
    public void ShouldOutline_AnAllyWhilePractising_IsTrue()
    {
        Assert.IsTrue(TeamPracticeRules.ShouldOutline(practising: true, aiIndex: 1, aiCount: Six));
    }

    [TestMethod]
    public void ShouldOutline_NotPractising_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.ShouldOutline(practising: false, aiIndex: 1, aiCount: Six));
    }

    [TestMethod]
    public void ShouldOutline_AnOpponentWhilePractising_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.ShouldOutline(practising: true, aiIndex: 4, aiCount: Six));
    }

    [TestMethod]
    public void ShouldOutline_UnknownTeam_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.ShouldOutline(practising: true, aiIndex: -1, aiCount: Six));
    }

    [TestMethod]
    public void IsTeamMode_AccessAvailableAndTeamTown_IsTrue()
    {
        Assert.IsTrue(TeamPracticeRules.IsTeamMode(accessAvailable: true, () => true));
    }

    [TestMethod]
    public void IsTeamMode_AccessAvailableNotATeamTown_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.IsTeamMode(accessAvailable: true, () => false));
    }

    [TestMethod]
    public void IsTeamMode_AccessUnavailable_IsFalseAndTheTownIsNotAsked()
    {
        var asked = false;

        var teams = TeamPracticeRules.IsTeamMode(accessAvailable: false, () => { asked = true; return true; });

        Assert.IsFalse(teams, "a fight the patches cannot run is never described as a team fight");
        Assert.IsFalse(asked);
    }

    [TestMethod]
    public void ShowsTeamWin_TeamFightNobodyLeftPlayerStanding_IsTrue()
    {
        Assert.IsTrue(TeamPracticeRules.ShowsTeamWin(teams: true, remainingFromLastPractice: 0, playerSurvived: true));
    }

    [TestMethod]
    public void ShowsTeamWin_NotATeamFight_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.ShowsTeamWin(teams: false, remainingFromLastPractice: 0, playerSurvived: true));
    }

    [TestMethod]
    public void ShowsTeamWin_FightersLeft_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.ShowsTeamWin(teams: true, remainingFromLastPractice: 3, playerSurvived: true));
    }

    [TestMethod]
    public void ShowsTeamWin_PlayerWentDown_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.ShowsTeamWin(teams: true, remainingFromLastPractice: 0, playerSurvived: false),
            "vanilla pays 250 on the count alone, but the text says the player was still standing");
    }

    [TestMethod]
    public void AnyOpponentStanding_AnOpponentTeamHasBots_IsTrue()
    {
        Assert.IsTrue(TeamPracticeRules.AnyOpponentStanding(new[] { false, false, false, false, true, false }));
    }

    [TestMethod]
    public void AnyOpponentStanding_OnlyAlliesHaveBots_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.AnyOpponentStanding(new[] { true, true, false, false, false, false }));
    }

    [TestMethod]
    public void AnyOpponentStanding_NobodyHasBots_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.AnyOpponentStanding(new[] { false, false, false, false, false, false }));
    }

    [TestMethod]
    public void AnyOpponentStanding_EmptyList_IsTrueSoNoWinIsDeclaredWithoutData()
    {
        Assert.IsTrue(TeamPracticeRules.AnyOpponentStanding(System.Array.Empty<bool>()));
    }

    [TestMethod]
    public void WinCountSlipped_EveryFighterEntered_AndTheCountBelowZero_IsTrue()
        => Assert.IsTrue(TeamPracticeRules.WinCountSlipped(playerSurvived: true, unspawned: 0, alive: -1));

    [TestMethod]
    public void WinCountSlipped_ThePlayerFell_IsFalse()
        => Assert.IsFalse(TeamPracticeRules.WinCountSlipped(playerSurvived: false, unspawned: 0, alive: -1));

    [TestMethod]
    public void WinCountSlipped_TheCountAtZeroOrAbove_IsFalse()
    {
        Assert.IsFalse(TeamPracticeRules.WinCountSlipped(playerSurvived: true, unspawned: 0, alive: 0));
        Assert.IsFalse(TeamPracticeRules.WinCountSlipped(playerSurvived: true, unspawned: 0, alive: 2));
    }

    [TestMethod]
    public void WinCountSlipped_FightersStillToEnter_IsFalse()
        => Assert.IsFalse(TeamPracticeRules.WinCountSlipped(playerSurvived: true, unspawned: 3, alive: -1));

    [DataTestMethod]
    [DataRow(30, 0, 30)]
    [DataRow(5, 2, 3)]
    [DataRow(2, 2, 0)]
    public void OpponentsLeft_AlliesFewerThanOrEqualToTheCount_IsTheCountMinusTheAllies(int remaining, int alliesStanding, int expected)
        => Assert.AreEqual(expected, TeamPracticeRules.OpponentsLeft(remaining, alliesStanding));

    [TestMethod]
    public void OpponentsLeft_AfterTheTeamWin_CountZeroWithAlliesStanding_IsZeroNotNegative()
        => Assert.AreEqual(0, TeamPracticeRules.OpponentsLeft(remaining: 0, alliesStanding: 2));

    [TestMethod]
    public void OpponentsLeft_MoreAlliesThanTheCount_IsZero()
        => Assert.AreEqual(0, TeamPracticeRules.OpponentsLeft(remaining: 1, alliesStanding: 2));

    [TestMethod]
    public void OpponentsLeft_NegativeAllies_CountAsNoneStanding()
        => Assert.AreEqual(5, TeamPracticeRules.OpponentsLeft(remaining: 5, alliesStanding: -2));

    [TestMethod]
    public void OpponentsLeft_NegativeCount_IsZero()
    {
        Assert.AreEqual(0, TeamPracticeRules.OpponentsLeft(remaining: -1, alliesStanding: 0));
        Assert.AreEqual(0, TeamPracticeRules.OpponentsLeft(remaining: -4, alliesStanding: -2));
    }
}
