using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;
using TAOM.Features.CareerSystem.UI;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

// Issue #388 — presentation map behind the diamond career screen: which banner icon a choice
// shows, and how a passive's magnitude reads in the Active Effects panel.
//
// The percent-vs-flat split is by effect TYPE, not by magnitude size. The reference
// implementation used "|value| < 1 means percent", which renders a 1.0 magnitude (=100%) as
// "+1" and would render a +1 companion limit as "+100%". TAOM's own data settles it: Damage /
// Ammo / MovementSpeed are authored as fractions (0.05, 0.10), Health / PartySize as counts
// (25, 50, 2, 4).
//
// The numeric decision (SignedNumber) is a pure helper with no TextObject, so it is tested in
// the no-game step; only the composed text needs the engine's TextObject.
[TestClass]
public class CareerEffectDisplayMapTests
{
    [TestMethod]
    public void IconFor_KnownEffect_ReturnsBannerIconId()
    {
        Assert.AreEqual("30012", CareerEffectDisplayMap.IconFor(PassiveEffectType.Damage));
        Assert.AreEqual("10001", CareerEffectDisplayMap.IconFor(PassiveEffectType.Health));
        Assert.AreEqual("26109", CareerEffectDisplayMap.IconFor(PassiveEffectType.StealthBonus));
    }

    [TestMethod]
    public void IconFor_EveryEnumValue_HasItsOwnIconRow()
    {
        // IconFor answers the fallback for a type with no row, so asking it can never fail: read the Icons table
        // itself, the way the Labels test does. A missing row renders the fallback glyph with no error.
        var source = ReadSource("Main/Features/CareerSystem/UI/CareerEffectDisplayMap.cs", stripComments: true);
        var rows = Regex.Matches(source, @"\[PassiveEffectType\.(\w+)\] = ""(\d+)""")
            .Cast<Match>()
            .Select(m => (PassiveEffectType)Enum.Parse(typeof(PassiveEffectType), m.Groups[1].Value))
            .ToList();

        foreach (PassiveEffectType type in Enum.GetValues(typeof(PassiveEffectType)))
        {
            CollectionAssert.Contains(rows, type, $"{type} has no row in CareerEffectDisplayMap.Icons");
            Assert.IsFalse(string.IsNullOrEmpty(CareerEffectDisplayMap.IconFor(type)), $"{type} has no diamond icon mapped");
        }
        Assert.AreEqual(Enum.GetValues(typeof(PassiveEffectType)).Length, rows.Count, "one Icons row per enum value");
    }

    [TestMethod]
    public void SignedNumber_FractionalEffect_IsAPercentWithItsSign()
    {
        Assert.AreEqual("+20", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, 0.20f, out var isPercent));
        Assert.IsTrue(isPercent);
        Assert.AreEqual("+7", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.MovementSpeed, 0.07f, out isPercent));
        Assert.IsTrue(isPercent);
    }

    [TestMethod]
    public void SignedNumber_FlatEffect_IsACountNotAPercent()
    {
        Assert.AreEqual("+25", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Health, 25f, out var isPercent));
        Assert.IsFalse(isPercent);
        Assert.AreEqual("+4", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.PartySize, 4f, out isPercent));
        Assert.IsFalse(isPercent);
        Assert.AreEqual("+2", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.CompanionLimit, 2f, out isPercent));
        Assert.IsFalse(isPercent);
    }

    [TestMethod]
    public void SignedNumber_FullMagnitudePercent_IsHundredPercent_NotOne()
    {
        // The reference formatter's bug: |v| < 1 => percent, so exactly 1.0 fell through to
        // the flat branch and printed "+1 damage" for what is a doubling.
        Assert.AreEqual("+100", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, 1f, out var isPercent));
        Assert.IsTrue(isPercent);
    }

    [TestMethod]
    public void SignedNumber_NegativeMagnitude_KeepsSign()
    {
        Assert.AreEqual("-15", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.TroopWages, -0.15f, out var isPercent));
        Assert.IsTrue(isPercent);
        Assert.AreEqual("-3", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Health, -3f, out isPercent));
        Assert.IsFalse(isPercent);
    }

    [TestMethod]
    public void SignedNumber_RoundsToNearestWholeNumber()
    {
        Assert.AreEqual("+8", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, 0.075f, out _));
        Assert.AreEqual("+3", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Health, 2.6f, out _));
    }

    [TestMethod]
    public void SignedNumber_NonFiniteMagnitude_ReturnsEmpty()
    {
        // Config floats are NaN-guarded at load, but the panel must never print "+NaN%".
        Assert.AreEqual("", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, float.NaN, out _));
        Assert.AreEqual("", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, float.PositiveInfinity, out _));
        Assert.AreEqual("", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Health, float.NegativeInfinity, out _));
    }

    [TestMethod]
    public void SignedNumber_ZeroOrRoundsToZero_ReturnsEmpty()
    {
        Assert.AreEqual("", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, 0f, out _));
        Assert.AreEqual("", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Damage, 0.004f, out _), "0.4% rounds to nothing");
        Assert.AreEqual("", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.Health, 0.4f, out _), "0.4 of a count rounds to nothing");
    }

    // F5 (#769 convergence): both of these are FRACTIONS at runtime (CareerAgentStatService multiplies the mount's
    // base health by 1 + mountHealth; TaomInventoryCapacityModel feeds ApplyFactor), so a flat reading printed
    // nothing at all for 0.10 (63 shipped MountHealth pips).
    [TestMethod]
    public void SignedNumber_MountHealthAndInventoryCapacity_AreFractionsNotCounts()
    {
        Assert.AreEqual("+10", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.MountHealth, 0.10f, out var isPercent));
        Assert.IsTrue(isPercent);
        Assert.AreEqual("+15", CareerEffectDisplayMap.SignedNumber(PassiveEffectType.InventoryCapacity, 0.15f, out isPercent));
        Assert.IsTrue(isPercent);
    }

    [TestMethod]
    public void TheCountEffects_AreExactlyHealthPartySizeCompanionLimitDailyGoldAndTroopTraining()
    {
        // The panel and the diagnostic report read one table (CareerPerkConsumerMap.IsFlat): pin its members by name.
        var flat = new System.Collections.Generic.List<PassiveEffectType>();
        foreach (PassiveEffectType type in System.Enum.GetValues(typeof(PassiveEffectType)))
        {
            CareerEffectDisplayMap.SignedNumber(type, 1f, out var isPercent);
            if (!isPercent) flat.Add(type);
        }
        CollectionAssert.AreEquivalent(
            new[]
            {
                PassiveEffectType.Health, PassiveEffectType.PartySize, PassiveEffectType.CompanionLimit,
                PassiveEffectType.DailyGold, PassiveEffectType.TroopTraining,
            }, flat);
    }

    [TestMethod]
    public void TheThreeTroopCounters_HaveARelatedIconAndReadAsPercent()
    {
        Assert.AreEqual(CareerEffectDisplayMap.IconFor(PassiveEffectType.TroopMorale), CareerEffectDisplayMap.IconFor(PassiveEffectType.TroopFearResistance));
        Assert.AreEqual(CareerEffectDisplayMap.IconFor(PassiveEffectType.TroopResistance), CareerEffectDisplayMap.IconFor(PassiveEffectType.TroopKnockdownResistance));
        Assert.AreEqual(CareerEffectDisplayMap.IconFor(PassiveEffectType.ShrugOff), CareerEffectDisplayMap.IconFor(PassiveEffectType.TroopCrushHold));
        foreach (var type in new[] { PassiveEffectType.TroopFearResistance, PassiveEffectType.TroopKnockdownResistance, PassiveEffectType.TroopCrushHold })
        {
            Assert.AreEqual("+20", CareerEffectDisplayMap.SignedNumber(type, 0.20f, out var isPercent), type.ToString());
            Assert.IsTrue(isPercent, type.ToString());
        }
    }

    // ── Composed text: needs the engine's TextObject ──

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Format_FractionalEffect_RendersPercentBeforeTheNoun()
    {
        Assert.AreEqual("+20% damage", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, 0.20f));
        Assert.AreEqual("+10% mount health", CareerEffectDisplayMap.Format(PassiveEffectType.MountHealth, 0.10f));
        Assert.AreEqual("", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, float.NaN));
        Assert.AreEqual("", CareerEffectDisplayMap.Format(PassiveEffectType.Damage, 0f));
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Format_FlatEffect_RendersCount()
    {
        Assert.AreEqual("+25 max health", CareerEffectDisplayMap.Format(PassiveEffectType.Health, 25f));
        Assert.AreEqual("+4 party size", CareerEffectDisplayMap.Format(PassiveEffectType.PartySize, 4f));
    }

    // ── #769 E9: the troop-class suffix ──

    [DataTestMethod]
    [TestCategory("RequiresGame")]
    [DataRow(TroopClassMask.All, "+10% troop damage")]
    [DataRow(TroopClassMask.Infantry, "+10% troop damage for infantry")]
    [DataRow(TroopClassMask.Ranged, "+10% troop damage for ranged troops")]
    [DataRow(TroopClassMask.Cavalry, "+10% troop damage for cavalry")]
    [DataRow(TroopClassMask.Infantry | TroopClassMask.Ranged, "+10% troop damage for infantry and ranged troops")]
    [DataRow(TroopClassMask.Ranged | TroopClassMask.Cavalry, "+10% troop damage for ranged troops and cavalry")]
    [DataRow(TroopClassMask.Infantry | TroopClassMask.Cavalry, "+10% troop damage for infantry and cavalry")]
    public void Format_ClassFilteredEffect_AppendsTheClasses(TroopClassMask troopClass, string expected)
    {
        Assert.AreEqual(expected, CareerEffectDisplayMap.Format(PassiveEffectType.TroopDamage, 0.10f, troopClass));
        Assert.AreEqual("", CareerEffectDisplayMap.Format(PassiveEffectType.TroopDamage, 0f, troopClass), "a zero magnitude prints no suffix on nothing");
    }
}
