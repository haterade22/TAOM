using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.Spider;

// The spider's strike rules as data (#692): the ridden spider keeps today's numbers (every enemy in the arc, full
// damage, knockdown at 30), and a creature bandit can cap each attack's targets, scale its damage and knock down on a
// crit only. The clip the service chooses decides which strike applies: the standing front strike is the bite, the
// running lunge the pounce, the left and right swings the swipe.

namespace TAOM.Tests.Features.Spider;

[TestClass]
public class SpiderStrikeTests
{
    private static readonly SpiderStrikeProfile Bite = new(maxTargets: 1, damageMultiplier: 1f, knockdownOnCritOnly: true);
    private static readonly SpiderStrikeProfile Pounce = new(maxTargets: 2, damageMultiplier: 1f, knockdownOnCritOnly: true);
    private static readonly SpiderStrikeProfile Swipe = new(maxTargets: 3, damageMultiplier: 0.5f, knockdownOnCritOnly: true);

    [TestMethod]
    public void Ridden_IsUncappedFullDamageThresholdKnockdown()
    {
        Assert.AreEqual(int.MaxValue, SpiderStrikeProfile.Ridden.MaxTargets);
        Assert.AreEqual(1f, SpiderStrikeProfile.Ridden.DamageMultiplier);
        Assert.IsFalse(SpiderStrikeProfile.Ridden.KnockdownOnCritOnly);
        Assert.IsFalse(SpiderStrikeProfile.Ridden.IsCapped);
    }

    [TestMethod]
    public void RiddenSet_GivesTheRiddenStrikeForEveryClip()
    {
        foreach (var clip in new[] { SpiderConfig.PounceFrontActionName, SpiderConfig.PounceChargeActionName,
                     SpiderConfig.SwingLeftActionName, SpiderConfig.SwingRightActionName })
            Assert.AreSame(SpiderStrikeProfile.Ridden, SpiderStrikeSet.Ridden.For(clip), clip);
    }

    [TestMethod]
    public void For_MapsEachClipToItsStrike()
    {
        var set = new SpiderStrikeSet(Bite, Pounce, Swipe);
        Assert.AreSame(Bite, set.For(SpiderConfig.PounceFrontActionName));
        Assert.AreSame(Pounce, set.For(SpiderConfig.PounceChargeActionName));
        Assert.AreSame(Swipe, set.For(SpiderConfig.SwingLeftActionName));
        Assert.AreSame(Swipe, set.For(SpiderConfig.SwingRightActionName));
        Assert.AreSame(Bite, set.For("act_unknown"), "an unknown clip takes the narrowest strike");
    }

    [TestMethod]
    public void Profile_ClampsItsInputs()
    {
        var bad = new SpiderStrikeProfile(maxTargets: 0, damageMultiplier: float.NaN, knockdownOnCritOnly: false);
        Assert.AreEqual(1, bad.MaxTargets, "a strike always hits at least one");
        Assert.AreEqual(1f, bad.DamageMultiplier, "a non-finite multiplier falls back to full damage");
        Assert.AreEqual(0f, new SpiderStrikeProfile(3, -2f, false).DamageMultiplier);
    }

    [TestMethod]
    public void Scale_AppliesTheMultiplier()
    {
        Assert.AreEqual(61, SpiderStrikes.Scale(61, SpiderStrikeProfile.Ridden));
        Assert.AreEqual(30, SpiderStrikes.Scale(61, Swipe));
    }

    [TestMethod]
    public void Reaction_Ridden_KeepsTodaysThresholds()
    {
        Assert.AreEqual(DamageAnimation.Nothing, SpiderStrikes.Reaction(7, isCrit: false, SpiderStrikeProfile.Ridden));
        Assert.AreEqual(DamageAnimation.Flinch, SpiderStrikes.Reaction(29, isCrit: false, SpiderStrikeProfile.Ridden));
        Assert.AreEqual(DamageAnimation.Fall, SpiderStrikes.Reaction(30, isCrit: false, SpiderStrikeProfile.Ridden));
    }

    [TestMethod]
    public void Reaction_CritOnly_FloorsOnlyOnACrit()
    {
        Assert.AreEqual(DamageAnimation.Flinch, SpiderStrikes.Reaction(61, isCrit: false, Bite), "a normal bite staggers");
        Assert.AreEqual(DamageAnimation.Fall, SpiderStrikes.Reaction(107, isCrit: true, Bite));
        Assert.AreEqual(DamageAnimation.Nothing, SpiderStrikes.Reaction(5, isCrit: true, Bite), "a scratch never floors");
    }

    [TestMethod]
    public void Pick_TakesTheNearest_StableOnTies()
    {
        var picked = SpiderStrikeTargets.Pick(new[]
        {
            new SpiderStrikeCandidate(id: 10, distanceSquared: 4f, riderId: -1),
            new SpiderStrikeCandidate(11, 1f, -1),
            new SpiderStrikeCandidate(12, 1f, -1),
            new SpiderStrikeCandidate(13, 9f, -1),
        }, maxTargets: 2);
        CollectionAssert.AreEqual(new[] { 11, 12 }, picked.ToArray());
    }

    [TestMethod]
    public void Pick_CountsARiderAndHisHorseOnce()
    {
        // The strike reaches a cavalryman's horse and the man himself; with a cap, the pair must not use two slots.
        var picked = SpiderStrikeTargets.Pick(new[]
        {
            new SpiderStrikeCandidate(id: 20, distanceSquared: 1f, riderId: 21),   // the horse, nearer
            new SpiderStrikeCandidate(21, 2f, -1),                                 // its rider
            new SpiderStrikeCandidate(22, 3f, -1),
        }, maxTargets: 2);
        CollectionAssert.AreEqual(new[] { 21, 22 }, picked.ToArray());
    }

    [TestMethod]
    public void Pick_CapAtOrAboveTheCount_KeepsAll()
    {
        var picked = SpiderStrikeTargets.Pick(new[] { new SpiderStrikeCandidate(1, 5f, -1), new SpiderStrikeCandidate(2, 1f, -1) },
            maxTargets: 5);
        CollectionAssert.AreEqual(new[] { 2, 1 }, picked.ToArray());
    }
}
