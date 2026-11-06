using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Hooks;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// The filter <c>OnScoreHit</c> applies before a hit goes anywhere near the run (phase2-plan.md section 4, "The hit path"): no affector, a hit on oneself, a blocked hit, a blow that took no health, a
/// blow that left the victim dead and a mount's blow all have an answer. Generic over the agent, so the rules are tested without an engine.
/// </summary>
[TestClass]
public class LongDefeatHitFilterTests
{
    private sealed class Body
    {
        public bool IsMount { get; init; }
        public Body? Rider { get; init; }
    }

    private static Body? Attacker(Body? affector) => LongDefeatHitFilter.Attacker(affector, b => b.IsMount, b => b.Rider);

    [TestMethod]
    public void AnOrdinaryHit_IsAdmitted()
    {
        Assert.IsTrue(LongDefeatHitFilter.Admits(new Body(), new Body(), isBlocked: false, damagedHp: 12f, victimHealth: 40f));
    }

    [TestMethod]
    public void ANullAffector_IsDropped()
    {
        Assert.IsFalse(LongDefeatHitFilter.Admits(new Body(), (Body?)null, false, 12f, 40f));
    }

    [TestMethod]
    public void ANullVictim_IsDropped()
    {
        Assert.IsFalse(LongDefeatHitFilter.Admits((Body?)null, new Body(), false, 12f, 40f));
    }

    [TestMethod]
    public void AHitOnOneself_IsDropped_AsKillAgentCheatRaisesWhenTheVictimOwnsTheBlow()
    {
        var both = new Body();

        Assert.IsFalse(LongDefeatHitFilter.Admits(both, both, false, 12f, 40f));
    }

    [TestMethod]
    public void ABlockedHit_IsDropped()
    {
        Assert.IsFalse(LongDefeatHitFilter.Admits(new Body(), new Body(), isBlocked: true, damagedHp: 12f, victimHealth: 40f));
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-3f)]
    [DataRow(float.NaN)]
    public void AHitThatTookNoHealth_IsDropped(float damaged)
    {
        Assert.IsFalse(LongDefeatHitFilter.Admits(new Body(), new Body(), false, damaged, 40f));
    }

    // Agent.HandleBlow writes the health, raises OnAgentHit (which reaches OnScoreHit) and only then calls Die when "Health < 1f" (Agent.cs:5513, :5527, :5529-5532): a victim read at 0 is a
    // killing blow, a man who is about to be forgotten and was never poisoned.
    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.5f)]
    [DataRow(-1f)]
    [DataRow(float.NaN)]
    public void AHitThatLeftTheVictimDead_IsDropped(float health)
    {
        Assert.IsFalse(LongDefeatHitFilter.Admits(new Body(), new Body(), false, 12f, health));
    }

    [TestMethod]
    public void AHitThatLeftTheVictimOneHealth_IsAdmitted()
    {
        Assert.IsTrue(LongDefeatHitFilter.Admits(new Body(), new Body(), false, 12f, 1f));
    }

    [TestMethod]
    public void AMountsBlow_IsItsRiders()
    {
        var rider = new Body();
        var horse = new Body { IsMount = true, Rider = rider };

        Assert.AreSame(rider, Attacker(horse));
    }

    [TestMethod]
    public void AMountWithNoRider_HasNoAttacker()
    {
        Assert.IsNull(Attacker(new Body { IsMount = true, Rider = null }));
    }

    [TestMethod]
    public void AManOnFoot_IsHisOwnAttacker_AndNullStaysNull()
    {
        var man = new Body();

        Assert.AreSame(man, Attacker(man));
        Assert.IsNull(Attacker(null));
    }

    [TestMethod]
    public void ARidersHitOnHisOwnHorse_IsAHitOnOneself()
    {
        // A mount that struck its own rider maps to the rider, and the rider is the victim: the same body, dropped.
        var rider = new Body();
        var horse = new Body { IsMount = true, Rider = rider };

        Assert.IsFalse(LongDefeatHitFilter.Admits(rider, Attacker(horse), false, 5f, 40f));
    }
}
