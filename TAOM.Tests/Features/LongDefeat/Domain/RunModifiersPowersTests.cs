using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>
/// What the Hammer of the Underworld and Winged Terror fold into: the factor on the siege engines' hit points and the morale pulse (phase2-plan.md Slice 8, decision D10). The endless
/// factors and the hammer multiply different things and never each other: the endless health factor is the attackers' soldiers' health limit, the endless damage factor their blows
/// (it reaches the gate, walls and shields through the damage model but not the ram, which damages the gate through the destructible component), and the hammer only the engines' hit points.
/// </summary>
[TestClass]
public class RunModifiersPowersTests
{
    private static AffixPlan Plan(float hammer = 50f, float radius = 8f, float seconds = 2f, float amount = 3f, float floor = 35f)
    {
        var powers = new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = hammer },
            ["terror"] = new Dictionary<string, float> { ["radius"] = radius, ["pulseSeconds"] = seconds, ["amount"] = amount, ["floor"] = floor },
            ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f },
        };
        return new AffixPlan(new[] { new AffixStage(5, 3) }, powers, new Dictionary<string, string>());
    }

    [TestMethod]
    public void NoHammer_IsFactorOne_SoAFollowingWaveRestoresTheVanillaHitPoints()
    {
        Assert.AreEqual(1f, RunModifiers.EngineHitPoints(Plan(), new string[0]));
        Assert.AreEqual(1f, RunModifiers.EngineHitPoints(Plan(), new[] { "hide", "terror" }));
    }

    [TestMethod]
    public void Hammer_IsOnePlusItsPercent()
    {
        Assert.AreEqual(1.5f, RunModifiers.EngineHitPoints(Plan(hammer: 50f), new[] { "lash", "hammer" }), 1e-6f);
        Assert.AreEqual(2.25f, RunModifiers.EngineHitPoints(Plan(hammer: 125f), new[] { "hammer" }), 1e-6f);
    }

    [TestMethod]
    public void Hammer_NeverReachesZero_SoItCannotDestroyTheEnginesItHardens()
    {
        Assert.AreEqual(0.1f, RunModifiers.EngineHitPoints(Plan(hammer: -90f), new[] { "hammer" }), 1e-6f);
        Assert.AreEqual(0.1f, RunModifiers.EngineHitPoints(Plan(hammer: -500f), new[] { "hammer" }), 1e-6f);
    }

    [TestMethod]
    public void Hammer_OfNotANumber_ChangesNothing()
    {
        Assert.AreEqual(1f, RunModifiers.EngineHitPoints(Plan(hammer: float.NaN), new[] { "hammer" }));
        Assert.AreEqual(1f, RunModifiers.EngineHitPoints(Plan(hammer: float.PositiveInfinity), new[] { "hammer" }));
    }

    [TestMethod]
    public void TheHammer_AndTheEndlessFactors_AreSeparateNumbers()
    {
        // A wave 13 attacker at the endless factors: the table's damage factor and the engines' factor come from different inputs, and neither feeds the other.
        var plan = Plan(hammer: 50f);
        var table = RunModifiers.StatTable(plan, new[] { "hammer" }, attackerDamage: 1.12f);

        Assert.AreEqual(1.12f, table.AttackerDamage, "the endless damage factor stays the soldiers' blows");
        Assert.AreEqual(1.5f, RunModifiers.EngineHitPoints(plan, new[] { "hammer" }), "the engines take the hammer alone, whatever the wave's endless factors are");
        Assert.IsNull(table.Attackers, "the hammer is no stat power");
        Assert.AreEqual(0f, table.AttackerArmourPercent);
    }

    [TestMethod]
    public void NoTerror_IsNoPulse()
    {
        Assert.IsNull(RunModifiers.Terror(Plan(), new string[0]));
        Assert.IsNull(RunModifiers.Terror(Plan(), new[] { "hammer", "hide" }));
    }

    [TestMethod]
    public void Terror_CarriesTheFourNumbersOfItsPower()
    {
        var terror = RunModifiers.Terror(Plan(radius: 9f, seconds: 2.5f, amount: 4f, floor: 40f), new[] { "hide", "terror" });

        Assert.IsNotNull(terror);
        Assert.AreEqual(2.5f, terror!.PulseSeconds);
        Assert.AreEqual(new LdMoralePulse(9f, 4f, 40f), terror.Pulse);
    }

    [TestMethod]
    public void Terror_WithNoPositiveInterval_Pulses_Never()
    {
        Assert.IsNull(RunModifiers.Terror(Plan(seconds: 0f), new[] { "terror" }));
        Assert.IsNull(RunModifiers.Terror(Plan(seconds: float.NaN), new[] { "terror" }));
        Assert.IsNull(RunModifiers.Terror(Plan(seconds: float.PositiveInfinity), new[] { "terror" }));
    }

    [TestMethod]
    public void Terror_OfNotANumber_FromAnyOfItsNumbers_Pulses_Never()
    {
        Assert.IsNull(RunModifiers.Terror(Plan(radius: float.NaN), new[] { "terror" }));
        Assert.IsNull(RunModifiers.Terror(Plan(amount: float.NaN), new[] { "terror" }));
        Assert.IsNull(RunModifiers.Terror(Plan(floor: float.NaN), new[] { "terror" }));
    }
}
