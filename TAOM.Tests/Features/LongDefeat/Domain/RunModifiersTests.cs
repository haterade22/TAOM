using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

/// <summary>What a wave's affixes fold into: the attackers' stat record and armour percent, built fresh for every publish.</summary>
[TestClass]
public class RunModifiersTests
{
    private static AffixPlan Plan()
    {
        var powers = new Dictionary<string, IReadOnlyDictionary<string, float>>
        {
            ["lash"] = new Dictionary<string, float> { ["swingSpeedPercent"] = 15f, ["attackEagernessPercent"] = 25f },
            ["drums"] = new Dictionary<string, float> { ["moveSpeedPercent"] = 12f, ["accelerationPercent"] = 25f },
            ["eye"] = new Dictionary<string, float>
            {
                ["aimErrorPercent"] = -30f, ["drawSpeedPercent"] = 15f, ["reloadSpeedPercent"] = 15f, ["missileSpeedPercent"] = 10f,
            },
            ["discipline"] = new Dictionary<string, float> { ["blockAbilityPercent"] = 25f, ["parryAbilityPercent"] = 20f },
            ["hide"] = new Dictionary<string, float> { ["armourPercent"] = 30f },
            ["hammer"] = new Dictionary<string, float> { ["engineHitPointsPercent"] = 50f },
        };
        return new AffixPlan(new[] { new AffixStage(5, 3) }, powers, new Dictionary<string, string>());
    }

    [TestMethod]
    public void NoAffixes_PublishOnlyTheDamageFactor_AndNoRecord()
    {
        var table = RunModifiers.StatTable(Plan(), new string[0], 1.06f);

        Assert.AreEqual(1.06f, table.AttackerDamage);
        Assert.IsNull(table.Attackers, "a null record keeps the stat hook inert");
        Assert.AreEqual(0f, table.AttackerArmourPercent);
    }

    [TestMethod]
    public void Lash_SetsSwingSpeedAndEagerness()
    {
        var fx = RunModifiers.StatTable(Plan(), new[] { "lash" }, 1f).Attackers!;

        Assert.AreEqual(15f, fx.SwingSpeedPercent);
        Assert.AreEqual(25f, fx.AttackEagernessPercent);
        Assert.AreEqual(0f, fx.MoveSpeedPercent);
    }

    [TestMethod]
    public void Drums_SetMoveSpeedAndAcceleration()
    {
        var fx = RunModifiers.StatTable(Plan(), new[] { "drums" }, 1f).Attackers!;

        Assert.AreEqual(12f, fx.MoveSpeedPercent);
        Assert.AreEqual(25f, fx.AccelerationPercent);
    }

    [TestMethod]
    public void Eye_SetsAimDrawReloadAndMissileSpeed()
    {
        var fx = RunModifiers.StatTable(Plan(), new[] { "eye" }, 1f).Attackers!;

        Assert.AreEqual(-30f, fx.AimErrorPercent);
        Assert.AreEqual(15f, fx.DrawSpeedPercent);
        Assert.AreEqual(15f, fx.ReloadSpeedPercent);
        Assert.AreEqual(10f, fx.MissileSpeedPercent);
    }

    [TestMethod]
    public void Discipline_SetsBlockAndParry()
    {
        var fx = RunModifiers.StatTable(Plan(), new[] { "discipline" }, 1f).Attackers!;

        Assert.AreEqual(25f, fx.BlockAbilityPercent);
        Assert.AreEqual(20f, fx.ParryAbilityPercent);
    }

    [TestMethod]
    public void Hide_IsArmourOnly_AndLeavesNoStatRecord()
    {
        var table = RunModifiers.StatTable(Plan(), new[] { "hide" }, 1f);

        Assert.AreEqual(30f, table.AttackerArmourPercent);
        Assert.IsNull(table.Attackers, "armour is written at spawn by its own hook; no stat record is owed");
    }

    [TestMethod]
    public void APowerThatIsNotAStatPower_AddsNothingToTheRecord()
    {
        var table = RunModifiers.StatTable(Plan(), new[] { "hammer", "terror", "blades" }, 1f);

        Assert.IsNull(table.Attackers);
        Assert.AreEqual(0f, table.AttackerArmourPercent);
    }

    [TestMethod]
    public void TwoPowers_FillDifferentFieldsOfOneRecord()
    {
        var table = RunModifiers.StatTable(Plan(), new[] { "lash", "hide", "drums" }, 1f);

        Assert.AreEqual(15f, table.Attackers!.SwingSpeedPercent);
        Assert.AreEqual(12f, table.Attackers.MoveSpeedPercent);
        Assert.AreEqual(30f, table.AttackerArmourPercent);
    }

    [TestMethod]
    public void AMountedRow_CopiesMoveSpeedIntoMountSpeed_BecauseOnlyApplyMountReadsIt()
    {
        var fx = RunModifiers.StatTable(Plan(), new[] { "drums" }, 1f).Attackers!;

        Assert.AreEqual(fx.MoveSpeedPercent, fx.MountSpeedPercent);
        Assert.AreEqual(12f, fx.MountSpeedPercent);
    }

    [TestMethod]
    public void EveryPublish_GetsFreshRecords_SoNothingPublishedIsEverMutated()
    {
        var first = RunModifiers.StatTable(Plan(), new[] { "lash" }, 1f);
        var second = RunModifiers.StatTable(Plan(), new[] { "lash" }, 1f);

        Assert.AreNotSame(first, second);
        Assert.AreNotSame(first.Attackers, second.Attackers);
        second.Attackers!.SwingSpeedPercent = 99f;
        Assert.AreEqual(15f, first.Attackers!.SwingSpeedPercent);
    }

    [TestMethod]
    public void AnUnknownId_IsIgnored()
    {
        var table = RunModifiers.StatTable(Plan(), new[] { "nonsense" }, 1f);

        Assert.IsNull(table.Attackers);
    }
}
