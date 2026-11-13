using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.RaceAbilities;

/// <summary>
/// The merge a soldier's own race ability and a hero aura go through (#1127): per channel the stronger value
/// counts and the two never add, and a price the ability pays on purpose (a negative value) stays.
/// </summary>
[TestClass]
public class RaceAbilityEffectsMergeTests
{
    [TestMethod]
    public void Combine_BothNull_ReturnsNull() =>
        Assert.IsNull(RaceAbilityEffectsMerge.Combine(null, null));

    [TestMethod]
    public void Combine_OnlyAura_ReturnsTheAura()
    {
        var aura = new RaceAbilityEffects { DamageReductionPercent = 10f };

        Assert.AreSame(aura, RaceAbilityEffectsMerge.Combine(null, aura));
    }

    [TestMethod]
    public void Combine_OnlyOwn_ReturnsOwn()
    {
        var own = new RaceAbilityEffects { MeleeDamagePercent = 20f };

        Assert.AreSame(own, RaceAbilityEffectsMerge.Combine(own, null));
    }

    [TestMethod]
    public void Combine_BothPositive_TakesTheLargerNeverTheSum()
    {
        var merged = RaceAbilityEffectsMerge.Combine(
            new RaceAbilityEffects { MeleeDamagePercent = 20f, DamageReductionPercent = 5f },
            new RaceAbilityEffects { MeleeDamagePercent = 10f, DamageReductionPercent = 15f })!;

        Assert.AreEqual(20f, merged.MeleeDamagePercent, 0.001f);
        Assert.AreEqual(15f, merged.DamageReductionPercent, 0.001f);
    }

    [TestMethod]
    public void Combine_OwnPaysAPrice_KeepsThePrice()
    {
        // Berserk drops block by 60 percent on purpose; an aura must not erase that.
        var merged = RaceAbilityEffectsMerge.Combine(
            new RaceAbilityEffects { BlockAbilityPercent = -60f },
            new RaceAbilityEffects { BlockAbilityPercent = 10f })!;

        Assert.AreEqual(-60f, merged.BlockAbilityPercent, 0.001f);
    }

    [TestMethod]
    public void Combine_AimError_LowerIsStronger()
    {
        var merged = RaceAbilityEffectsMerge.Combine(
            new RaceAbilityEffects { AimErrorPercent = -5f },
            new RaceAbilityEffects { AimErrorPercent = -20f })!;

        Assert.AreEqual(-20f, merged.AimErrorPercent, 0.001f);
    }

    [TestMethod]
    public void Combine_AimErrorPrice_KeepsThePrice()
    {
        var merged = RaceAbilityEffectsMerge.Combine(
            new RaceAbilityEffects { AimErrorPercent = 30f },
            new RaceAbilityEffects { AimErrorPercent = -20f })!;

        Assert.AreEqual(30f, merged.AimErrorPercent, 0.001f);
    }

    [TestMethod]
    public void Combine_FlagsAndMoraleFloor_EitherSourceGrants()
    {
        var merged = RaceAbilityEffectsMerge.Combine(
            new RaceAbilityEffects { ShrugOffBlows = true, MoraleFloor = 20f },
            new RaceAbilityEffects { HoldAgainstCrush = true, MoraleFloor = 30f })!;

        Assert.IsTrue(merged.ShrugOffBlows);
        Assert.IsTrue(merged.HoldAgainstCrush);
        Assert.AreEqual(30f, merged.MoraleFloor, 0.001f);
    }

    [TestMethod]
    public void Combine_AbilityOnlyFields_ComeFromOwn()
    {
        var merged = RaceAbilityEffectsMerge.Combine(
            new RaceAbilityEffects { MoraleOnEnd = -15f, HealPerKill = 5f },
            new RaceAbilityEffects { MeleeDamagePercent = 5f })!;

        Assert.AreEqual(-15f, merged.MoraleOnEnd, 0.001f);
        Assert.AreEqual(5f, merged.HealPerKill, 0.001f);
    }
}
