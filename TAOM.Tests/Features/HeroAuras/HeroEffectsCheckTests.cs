using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAuras;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Tests.Features.HeroAuras;

[TestClass]
public class HeroEffectsCheckTests
{
    [TestMethod]
    public void Validate_NegativeGain_ZeroedWithLabelledMessage()
    {
        var messages = new List<string>();

        var result = HeroEffectsCheck.Validate("aura 'x'", new RaceAbilityEffects { MeleeDamagePercent = -5f }, 50f, messages.Add);

        Assert.AreEqual(0f, result.MeleeDamagePercent);
        CollectionAssert.AreEqual(new[] { "aura 'x': meleeDamagePercent -5 would weaken the soldiers, set to 0" }, messages);
    }

    [TestMethod]
    public void Validate_NaNAndPositiveAimError_ZeroedWithMessages()
    {
        var messages = new List<string>();

        var result = HeroEffectsCheck.Validate("L", new RaceAbilityEffects { MoveSpeedPercent = float.NaN, AimErrorPercent = 10f }, 50f, messages.Add);

        Assert.AreEqual(0f, result.MoveSpeedPercent);
        Assert.AreEqual(0f, result.AimErrorPercent);
        Assert.AreEqual(2, messages.Count);
    }

    [TestMethod]
    public void Validate_AimErrorBelowTheFloor_ZeroedWithMessage()
    {
        var messages = new List<string>();

        var result = HeroEffectsCheck.Validate("L", new RaceAbilityEffects { AimErrorPercent = -96f }, 50f, messages.Add);

        Assert.AreEqual(0f, result.AimErrorPercent);
        CollectionAssert.AreEqual(new[] { "L: aimErrorPercent -96 is below -95, set to 0" }, messages);
    }

    [TestMethod]
    public void Validate_AimErrorAtTheFloor_Kept()
    {
        var messages = new List<string>();

        Assert.AreEqual(-95f, HeroEffectsCheck.Validate("L", new RaceAbilityEffects { AimErrorPercent = -95f }, 50f, messages.Add).AimErrorPercent);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void Validate_AbilityOnlyField_ZeroedWithMessage()
    {
        var messages = new List<string>();

        var result = HeroEffectsCheck.Validate("L", new RaceAbilityEffects { HealPerKill = 5f }, 50f, messages.Add);

        Assert.AreEqual(0f, result.HealPerKill);
        CollectionAssert.AreEqual(new[] { "L: healPerKill is read only for a race ability, set to 0" }, messages);
    }

    [TestMethod]
    public void Validate_MoraleFloor_CappedAtTheCallersMaximum()
    {
        var messages = new List<string>();
        var e = new RaceAbilityEffects { MoraleFloor = 80f, SwingSpeedPercent = 10f, ShrugOffBlows = true };

        Assert.AreEqual(50f, HeroEffectsCheck.Validate("L", e, 50f, messages.Add).MoraleFloor);
        var wide = HeroEffectsCheck.Validate("L", e, 100f, messages.Add);

        Assert.AreEqual(80f, wide.MoraleFloor);
        Assert.AreEqual(10f, wide.SwingSpeedPercent);
        Assert.IsTrue(wide.ShrugOffBlows);
        Assert.AreEqual(0, messages.Count);
    }
}
