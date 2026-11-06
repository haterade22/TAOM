using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// Winged Terror's pulse arithmetic (phase2-plan.md Slice 8, decision D7): a defender within the radius of a standing attacker loses the amount, never below the floor, and one already
/// at or under the floor is left alone. The pulse counts, inside its own call, how many defenders it drained and how many it left below the floor, so the affix-effect check reads the
/// pulse's own work.
/// </summary>
[TestClass]
public class LdMoralePulseTests
{
    private static readonly LdMoralePulse Pulse = new(Radius: 8f, Amount: 3f, Floor: 35f);

    private sealed class Defender
    {
        public Defender(float x, float y, float morale)
        {
            X = x;
            Y = y;
            Morale = morale;
        }

        public float X { get; }
        public float Y { get; }
        public float Morale { get; set; }
    }

    private static LdPulseResult Run(LdMoralePulse pulse, IReadOnlyList<LdPulseSpot> attackers, IEnumerable<Defender> defenders, float overDrain = 0f) =>
        pulse.Apply(attackers, defenders, d => new LdPulseSpot(d.X, d.Y), d => d.Morale, (d, drain) => d.Morale -= drain + overDrain);

    private static readonly LdPulseSpot[] OneAttacker = { new(0f, 0f) };

    [DataTestMethod]
    [DataRow(80f, 3f)]
    [DataRow(38f, 3f)]
    [DataRow(37f, 2f)]
    [DataRow(35.5f, 0.5f)]
    [DataRow(35f, 0f)]
    [DataRow(20f, 0f)]
    [DataRow(0f, 0f)]
    [DataRow(-1f, 0f)]
    public void Drain_IsTheAmount_NeverMoreThanTheHeadroomOverTheFloor(float morale, float expected)
    {
        Assert.AreEqual(expected, Pulse.Drain(morale), 1e-5f);
    }

    [TestMethod]
    public void Drain_OfNotANumber_IsNothing()
    {
        Assert.AreEqual(0f, Pulse.Drain(float.NaN));
        Assert.AreEqual(0f, Pulse.Drain(float.NegativeInfinity));
        Assert.AreEqual(3f, Pulse.Drain(float.PositiveInfinity), "an infinite morale is far above any floor");
    }

    [TestMethod]
    public void ADefenderNearAnAttacker_LosesTheAmount_AndOneOutsideTheRadiusLosesNothing()
    {
        var near = new Defender(3f, 4f, 60f);
        var far = new Defender(30f, 0f, 60f);

        var result = Run(Pulse, OneAttacker, new[] { near, far });

        Assert.AreEqual(57f, near.Morale, 1e-4f);
        Assert.AreEqual(60f, far.Morale);
        Assert.AreEqual(2, result.Defenders);
        Assert.AreEqual(1, result.Drained);
        Assert.AreEqual(3f, result.Lost, 1e-4f);
        Assert.AreEqual(0, result.PushedBelow);
    }

    [TestMethod]
    public void TheRadius_IsInclusive_AndMeasuredAcrossTheGround()
    {
        var onTheEdge = new Defender(8f, 0f, 60f);
        var justOutside = new Defender(8.01f, 0f, 60f);

        Run(Pulse, OneAttacker, new[] { onTheEdge, justOutside });

        Assert.AreEqual(57f, onTheEdge.Morale, 1e-4f);
        Assert.AreEqual(60f, justOutside.Morale);
    }

    [TestMethod]
    public void ADefenderNearTheFloor_IsTakenDownToItAndNotBelow()
    {
        var shaky = new Defender(1f, 0f, 36f);

        var result = Run(Pulse, OneAttacker, new[] { shaky });

        Assert.AreEqual(35f, shaky.Morale, 1e-4f);
        Assert.AreEqual(1f, result.Lost, 1e-4f);
        Assert.AreEqual(0, result.PushedBelow);
    }

    [TestMethod]
    public void ADefenderAtOrUnderTheFloor_IsLeftAlone_AndNotCountedAsDrained()
    {
        var atFloor = new Defender(1f, 0f, 35f);
        var under = new Defender(1f, 0f, 20f);

        var result = Run(Pulse, OneAttacker, new[] { atFloor, under });

        Assert.AreEqual(35f, atFloor.Morale);
        Assert.AreEqual(20f, under.Morale);
        Assert.AreEqual(0, result.Drained);
        Assert.AreEqual(0, result.PushedBelow, "a defender that was under the floor before the pulse was not pushed there by it");
    }

    [TestMethod]
    public void APushBelowTheFloor_IsCountedInsideTheCall_FromTheMoraleBeforeAndAfter()
    {
        var a = new Defender(1f, 0f, 60f);
        var b = new Defender(1f, 0f, 60f);

        // The engine lowered each defender by one more than asked (a model that adds its own loss): the pulse reads the result back and counts it.
        var result = Run(Pulse with { Floor = 58f }, OneAttacker, new[] { a, b }, overDrain: 1f);

        Assert.AreEqual(2, result.Drained);
        Assert.AreEqual(2, result.PushedBelow);
    }

    [TestMethod]
    public void FloatNoiseAtTheFloor_IsNotAPush()
    {
        var shaky = new Defender(1f, 0f, 40f);

        var result = Pulse.Apply(OneAttacker, new[] { shaky }, d => new LdPulseSpot(d.X, d.Y), d => d.Morale, (d, drain) => d.Morale = 35f - 0.0004f);

        Assert.AreEqual(1, result.Drained);
        Assert.AreEqual(0, result.PushedBelow, "a morale of 34.9996 is the engine's float noise, not a push below 35");
    }

    [TestMethod]
    public void WithNoAttackerStanding_NobodyIsDrained()
    {
        var defender = new Defender(0f, 0f, 60f);

        var result = Run(Pulse, new LdPulseSpot[0], new[] { defender });

        Assert.AreEqual(60f, defender.Morale);
        Assert.AreEqual(1, result.Defenders);
        Assert.AreEqual(0, result.Drained);
    }

    [TestMethod]
    public void ADefenderWithNoMorale_AnswersMinusOne_AndIsSkipped()
    {
        var lord = new Defender(0f, 0f, -1f);

        var result = Run(Pulse, OneAttacker, new[] { lord });

        Assert.AreEqual(-1f, lord.Morale, "a Player hero has no CommonAIComponent, so GetMorale answers -1 and a write would be lost");
        Assert.AreEqual(0, result.Drained);
    }

    [TestMethod]
    public void EveryDefenderNearAnyOfSeveralAttackers_IsDrainedOnce()
    {
        var attackers = new LdPulseSpot[] { new(0f, 0f), new(2f, 0f), new(100f, 100f) };
        var d = new Defender(1f, 0f, 60f);

        var result = Run(Pulse, attackers, new[] { d });

        Assert.AreEqual(57f, d.Morale, 1e-4f, "two attackers in reach drain it once, not twice");
        Assert.AreEqual(1, result.Drained);
    }
}
