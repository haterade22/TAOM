using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>What the commander slice adds to the folds: a war cry in the attackers' stat record, the Legend cooldown factor, and the troops a launch must find.</summary>
[TestClass]
public class RunModifiersCommanderTests
{
    private static BoonRow Cooldown(string id, float percent) =>
        new(id, BoonCategory.Legend, "{=K}" + id, "{=T}text", new[] { new BoonEffect(BoonKeys.CommanderCooldownPercent, percent) });

    private static AffixPlan Lash() => new(
        new[] { new AffixStage(1, 1) },
        new Dictionary<string, IReadOnlyDictionary<string, float>> { ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f } },
        new Dictionary<string, string>());

    [TestMethod]
    public void AWarCry_AddsItsPercentsToTheAttackersRecord_AndOnTopOfAnAffix()
    {
        var withoutCry = RunModifiers.StatTable(Lash(), new[] { "lash" }, 1f);
        var withCry = RunModifiers.StatTable(Lash(), new[] { "lash" }, 1f, null, CommanderPlanTests.WarCry());

        Assert.AreEqual(15f, withoutCry.Attackers!.SwingSpeedPercent);
        Assert.AreEqual(30f, withCry.Attackers!.SwingSpeedPercent, "the affix's 15 and the cry's 15");
        Assert.AreEqual(10f, withCry.Attackers.MoveSpeedPercent);
        Assert.AreEqual(10f, withCry.Attackers.MountSpeedPercent, "a mounted attacker's horse takes the move percent as well");
    }

    [TestMethod]
    public void AWarCry_AloneStillGivesTheAttackersARecord()
    {
        var table = RunModifiers.StatTable(AffixPlan.None, new string[0], 1f, null, CommanderPlanTests.WarCry());

        Assert.AreEqual(15f, table.Attackers!.SwingSpeedPercent);
        Assert.IsNull(RunModifiers.StatTable(AffixPlan.None, new string[0], 1f).Attackers, "no cry and no stat power: the hook stays inert");
    }

    [TestMethod]
    public void TheCommanderCooldownFactor_IsOnePlusTheSummedPercentsOverAHundred_NeverBelowZero()
    {
        Assert.AreEqual(1f, RunModifiers.CommanderCooldown(new BoonRow[0]));
        Assert.AreEqual(1.5f, RunModifiers.CommanderCooldown(new[] { Cooldown("a", 50f) }), 1e-4f);
        Assert.AreEqual(1.3f, RunModifiers.CommanderCooldown(new[] { Cooldown("a", 50f), Cooldown("b", -20f) }), 1e-4f);
        Assert.AreEqual(0f, RunModifiers.CommanderCooldown(new[] { Cooldown("a", -90f), Cooldown("b", -90f) }));
    }

    [TestMethod]
    public void AllTroopIds_HoldsEveryCommanderAndEverySummonTroop()
    {
        var plan = new RunPlan(
            "helms",
            new[] { new GarrisonRow("g_foot", FormationSlot.Infantry, 10) },
            new WavePlan(new[] { new WaveSpec(110, new[] { new WaveTroop("a", FormationSlot.Infantry, 1) }) }, new WaveScaling(10, 300), null, CommanderPlanTests.Plan()),
            new ShopPlan(600, new[] { new ShopOffer("militia", "Militia", "shop_militia", FormationSlot.Infantry, 12, 180) }),
            RunRules.Default,
            RunOptions.Default);

        var ids = RunPlanTroops.AllTroopIds(plan);

        CollectionAssert.AreEquivalent(new[] { "g_foot", "shop_militia", "a", "lugdush", "ugluk", "lurtz", "urukinf", "urukxbow" }, ids.ToArray());
        Assert.AreEqual(ids.Count, ids.Distinct().Count());
    }
}
