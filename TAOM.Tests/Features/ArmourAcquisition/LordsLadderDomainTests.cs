using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ArmourAcquisition.Domain;

namespace TAOM.Tests.Features.ArmourAcquisition;

/// <summary>The lord's gear ladder's domain rules (#693): slot parsing and mapping, and the material drop curve.</summary>
[TestClass]
public class LordsLadderDomainTests
{
    [TestMethod]
    [DataRow("hands", LadderSlot.Hands)]
    [DataRow(" Shoulders ", LadderSlot.Shoulders)]
    [DataRow("WEAPON", LadderSlot.Weapon)]
    public void TryParse_ASlotName_AnyCase(string raw, LadderSlot expected)
    {
        Assert.IsTrue(LadderSlotRules.TryParse(raw, out var slot));
        Assert.AreEqual(expected, slot);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("feet")]
    [DataRow("3")]
    [DataRow("-1")]
    [DataRow("legs,shoulders")]
    [DataRow("hands, legs")]
    public void TryParse_NotASlotName_Refused(string? raw)
    {
        // Enum.TryParse alone accepts "3" as the fourth member, and a comma list as the members OR'd
        // together ("legs,shoulders" is Head): neither is a slot name (RCA 2026-09-28 row 10).
        Assert.IsFalse(LadderSlotRules.TryParse(raw, out _));
    }

    [TestMethod]
    public void ArmourSlotOf_EachArmourRung_MapsToItsWornSlot_AndTheWeaponToNone()
    {
        Assert.AreEqual(ArmourSlot.Hand, LadderSlotRules.ArmourSlotOf(LadderSlot.Hands));
        Assert.AreEqual(ArmourSlot.Leg, LadderSlotRules.ArmourSlotOf(LadderSlot.Legs));
        Assert.AreEqual(ArmourSlot.Cape, LadderSlotRules.ArmourSlotOf(LadderSlot.Shoulders));
        Assert.AreEqual(ArmourSlot.Head, LadderSlotRules.ArmourSlotOf(LadderSlot.Head));
        Assert.AreEqual(ArmourSlot.Body, LadderSlotRules.ArmourSlotOf(LadderSlot.Body));
        Assert.AreEqual(ArmourSlot.None, LadderSlotRules.ArmourSlotOf(LadderSlot.Weapon));
    }

    [TestMethod]
    [DataRow(0, 0.10f)]
    [DataRow(9, 0.10f)]
    [DataRow(10, 0.11f)]
    [DataRow(125, 0.22f)]
    [DataRow(-40, 0.10f)]
    [DataRow(5000, 0.60f)]
    public void ChanceFor_RisesOnePointPerTenKills_AndStopsAtTheCap(int kills, float expected)
    {
        var drop = new MaterialDrop(baseChance: 0.1f, chancePerTenKills: 0.01f, maxChance: 0.6f, minUnits: 1, maxUnits: 3);

        Assert.AreEqual(expected, drop.ChanceFor(kills), 1e-5f);
    }
}
