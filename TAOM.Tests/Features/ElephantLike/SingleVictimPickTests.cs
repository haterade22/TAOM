using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ElephantLike;

// Single-target victim choice for elephant-like attacks (2026-09-18, #618). The war ram's head-butt hits ONE
// enemy; the elephant's and mumakil's tramples stay radial. Pure: the attack task offers each candidate's facing
// dot (toEnemy . lookDirection) and distance in scan order, and Offer answers "does this one become the pick".
// Rule: the enemy the creature faces most squarely wins, the nearer one breaks a tie, the first one wins a full
// tie, and a candidate whose numbers are not finite never wins and never disturbs the current pick.

namespace TAOM.Tests.Features.ElephantLike;

[TestClass]
public class SingleVictimPickTests
{
    [TestMethod]
    public void Offer_FirstFiniteCandidate_BecomesThePick()
    {
        var pick = new SingleVictimPick();
        Assert.IsTrue(pick.Offer(0.9f, 1.2f));
    }

    [TestMethod]
    public void Offer_BetterFacingEnemy_Wins_EvenWhenFarther()
    {
        // the first is nearer but off to the side; the second is straight ahead
        var pick = new SingleVictimPick();
        Assert.IsTrue(pick.Offer(0.30f, 0.8f));
        Assert.IsTrue(pick.Offer(0.95f, 1.8f));
    }

    [TestMethod]
    public void Offer_WorseFacingEnemy_DoesNotReplaceThePick()
    {
        var pick = new SingleVictimPick();
        Assert.IsTrue(pick.Offer(0.95f, 1.8f));
        Assert.IsFalse(pick.Offer(0.30f, 0.8f));
    }

    [TestMethod]
    public void Offer_EqualFacing_NearerWins_AndAFullTieKeepsTheFirst()
    {
        var pick = new SingleVictimPick();
        Assert.IsTrue(pick.Offer(0.9f, 1.9f));
        Assert.IsTrue(pick.Offer(0.9f, 0.7f));
        Assert.IsFalse(pick.Offer(0.9f, 1.4f));
        Assert.IsFalse(pick.Offer(0.9f, 0.7f));
    }

    [TestMethod]
    public void Offer_NonFiniteCandidate_NeverWins()
    {
        var pick = new SingleVictimPick();
        Assert.IsFalse(pick.Offer(float.NaN, 0.5f));
        Assert.IsFalse(pick.Offer(float.PositiveInfinity, 0.5f));
        Assert.IsFalse(pick.Offer(0.9f, float.NaN));
        Assert.IsFalse(pick.Offer(0.9f, float.PositiveInfinity));
        Assert.IsTrue(pick.Offer(0.5f, 1.0f), "a finite candidate still wins after non-finite ones");
    }

    [TestMethod]
    public void Offer_NonFiniteCandidate_DoesNotDisturbTheCurrentPick()
    {
        var pick = new SingleVictimPick();
        Assert.IsTrue(pick.Offer(0.5f, 1.0f));
        Assert.IsFalse(pick.Offer(float.NaN, 0.1f));
        Assert.IsFalse(pick.Offer(0.4f, 0.2f), "the pick is still the 0.5 candidate");
        Assert.IsTrue(pick.Offer(0.6f, 1.5f));
    }
}
