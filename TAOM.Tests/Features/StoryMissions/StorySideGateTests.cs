using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.StoryMissions.Domain;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// Which sides of a story window the player may take (#1326): never the side of the opposite alignment, and never against his own kingdom.
/// Helm's Deep is the worked case: Rohan defends (Free), Isengard attacks (Evil).
/// </summary>
[TestClass]
public class StorySideGateTests
{
    private static StoryChoices HelmsDeep(FactionSide player, StoryTie tie = StoryTie.None, bool hasDefend = true, bool hasAttack = true) =>
        StorySideGate.Choose(player, FactionSide.Free, FactionSide.Evil, tie, hasDefend, hasAttack);

    [TestMethod]
    public void Choose_FreeAtHelmsDeep_DefendOnly()
    {
        var choices = HelmsDeep(FactionSide.Free);

        Assert.IsTrue(choices.Defend);
        Assert.IsFalse(choices.Attack);
    }

    [TestMethod]
    public void Choose_EvilAtHelmsDeep_AttackOnly()
    {
        var choices = HelmsDeep(FactionSide.Evil);

        Assert.IsFalse(choices.Defend);
        Assert.IsTrue(choices.Attack);
    }

    [TestMethod]
    public void Choose_NeutralAtHelmsDeep_Both()
    {
        var choices = HelmsDeep(FactionSide.Neutral);

        Assert.IsTrue(choices.Defend);
        Assert.IsTrue(choices.Attack);
    }

    [TestMethod]
    public void Choose_EvilNoAttackMission_Nothing()
    {
        var choices = HelmsDeep(FactionSide.Evil, hasAttack: false);

        Assert.IsFalse(choices.Any, "an evil player is not asked to defend, and there is no attack to offer");
    }

    [TestMethod]
    public void Choose_FreeNoDefendMission_Nothing()
    {
        Assert.IsFalse(HelmsDeep(FactionSide.Free, hasDefend: false).Any);
    }

    [TestMethod]
    public void Choose_NeutralDefenders_AnyPlayerMayDefend()
    {
        // Nobody is opposed to a neutral side: an evil player may defend a neutral fortress.
        var choices = StorySideGate.Choose(FactionSide.Evil, FactionSide.Neutral, FactionSide.Evil, StoryTie.None, true, true);

        Assert.IsTrue(choices.Defend);
        Assert.IsTrue(choices.Attack);
    }

    [TestMethod]
    public void Choose_KingdomIsTheDefender_DefendOnly()
    {
        // A neutral player could take either side, but never against his own realm.
        var choices = HelmsDeep(FactionSide.Neutral, StoryTie.Defender);

        Assert.IsTrue(choices.Defend);
        Assert.IsFalse(choices.Attack);
    }

    [TestMethod]
    public void Choose_KingdomIsAnAttacker_AttackOnly()
    {
        var choices = HelmsDeep(FactionSide.Neutral, StoryTie.Attacker);

        Assert.IsFalse(choices.Defend);
        Assert.IsTrue(choices.Attack);
    }

    [TestMethod]
    public void Choose_KingdomIsAnAttackerButNoAttackMission_Nothing()
    {
        // Today's shipped state for an Isengard player: a lost defence must never hand the fortress to his own ruler.
        Assert.IsFalse(HelmsDeep(FactionSide.Evil, StoryTie.Attacker, hasAttack: false).Any);
    }

    [TestMethod]
    public void Choose_KingdomIsAnAttackerOfTheOppositeAlignment_Nothing()
    {
        // A pledged realm can sit in an attacker list whose lead kingdom reads the other side: the tie removes the defence, the alignment the attack.
        Assert.IsFalse(HelmsDeep(FactionSide.Free, StoryTie.Attacker).Any);
    }
}
