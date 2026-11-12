using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HeroAuras.Domain;
using TAOM.Features.RaceAbilities.Domain;

// The aura key decides whether a pulse only moves a soldier's end time or restamps him and refreshes his stats.

namespace TAOM.Tests.Features.HeroAuras;

[TestClass]
public class HeroAuraKeyTests
{
    private static HeroAura Aura(string id) => new HeroAura(id, 15f, null, new RaceAbilityEffects());

    [TestMethod]
    public void KeyFor_SameAurasInAnyOrder_GivesTheSameKey()
    {
        var dain = Aura("dain");
        var elrond = Aura("elrond");
        var suffix = HeroAura.StrengthSuffix(1f);

        Assert.AreEqual(HeroAura.KeyFor(new[] { dain, elrond }, suffix), HeroAura.KeyFor(new[] { elrond, dain }, suffix));
    }

    [TestMethod]
    public void KeyFor_OtherStrength_GivesAnotherKey()
    {
        // A strength change in MCM mid-battle must restamp the soldiers already under an aura.
        var dain = new[] { Aura("dain") };

        Assert.AreNotEqual(HeroAura.KeyFor(dain, HeroAura.StrengthSuffix(1f)), HeroAura.KeyFor(dain, HeroAura.StrengthSuffix(1.5f)));
    }

    [TestMethod]
    public void KeyFor_OtherSetOfAuras_GivesAnotherKey()
    {
        var suffix = HeroAura.StrengthSuffix(1f);

        Assert.AreNotEqual(HeroAura.KeyFor(new[] { Aura("dain") }, suffix), HeroAura.KeyFor(new[] { Aura("dain"), Aura("elrond") }, suffix));
    }

    [TestMethod]
    public void KeyFor_OneAura_EqualsTheJoinedForm()
    {
        // The one-aura shortcut must give the same key the general form gives.
        var suffix = HeroAura.StrengthSuffix(1f);

        Assert.AreEqual("dain" + suffix, HeroAura.KeyFor(new[] { Aura("dain") }, suffix));
    }
}
