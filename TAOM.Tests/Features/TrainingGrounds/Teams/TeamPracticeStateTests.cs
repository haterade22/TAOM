using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrainingGrounds.Teams.Hooks;

namespace TAOM.Tests.Features.TrainingGrounds.Teams;

/// <summary>
/// #1528: the mode is decided once for each arena visit (one controller instance), so an MCM change never
/// leaves a fight half team and half free-for-all.
/// </summary>
[TestClass]
public class TeamPracticeStateTests
{
    [TestMethod]
    public void Decide_CalledTwiceForOneController_RunsTheDecisionOnce()
    {
        var controller = new object();
        var runs = 0;

        var first = TeamPracticeState.Decide(controller, () => { runs++; return true; });
        var second = TeamPracticeState.Decide(controller, () => { runs++; return false; });

        Assert.AreEqual(1, runs);
        Assert.IsTrue(first);
        Assert.IsTrue(second, "the first answer stands, a later switch change does not flip it");
    }

    [TestMethod]
    public void Decide_NotATeamTown_StaysFalseForTheVisit()
    {
        var controller = new object();

        Assert.IsFalse(TeamPracticeState.Decide(controller, () => false));
        Assert.IsFalse(TeamPracticeState.Decide(controller, () => true));
    }

    [TestMethod]
    public void IsTeams_UnknownController_IsFalse()
    {
        Assert.IsFalse(TeamPracticeState.IsTeams(new object()));
    }

    [TestMethod]
    public void IsTeams_AfterATeamDecision_IsTrue()
    {
        var controller = new object();
        TeamPracticeState.Decide(controller, () => true);

        Assert.IsTrue(TeamPracticeState.IsTeams(controller));
    }

    [TestMethod]
    public void Decide_TwoControllers_AreIndependent()
    {
        var team = new object();
        var free = new object();

        TeamPracticeState.Decide(team, () => true);
        TeamPracticeState.Decide(free, () => false);

        Assert.IsTrue(TeamPracticeState.IsTeams(team));
        Assert.IsFalse(TeamPracticeState.IsTeams(free));
    }

    [TestMethod]
    public void Decide_TheDecisionThrows_NothingIsRemembered()
    {
        var controller = new object();

        Assert.ThrowsException<System.InvalidOperationException>(
            () => TeamPracticeState.Decide(controller, () => throw new System.InvalidOperationException("boom")));

        Assert.IsFalse(TeamPracticeState.IsTeams(controller));
        Assert.IsTrue(TeamPracticeState.Decide(controller, () => true), "the next call decides afresh");
    }
}
