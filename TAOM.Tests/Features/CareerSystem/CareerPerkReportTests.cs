using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Diagnostics;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #613: <c>taom.career_perks</c> renders one snapshot into the lines the console shows and the
/// TAOM debug log keeps. The builder is pure; the command gathers the snapshot at the boundary.
/// </summary>
[TestClass]
public class CareerPerkReportTests
{
    [TestMethod]
    public void Render_NoCareer_SaysSoInOneLine()
    {
        var snapshot = new CareerPerkSnapshot { HeroId = "h1", HeroName = "Eorl", HasCareer = false };

        var lines = CareerPerkReport.Render(snapshot);

        Assert.AreEqual(1, lines.Count);
        StringAssert.Contains(lines[0], "Eorl");
        StringAssert.Contains(lines[0], "no career");
    }

    [TestMethod]
    public void Render_ListsEveryPassiveWithMagnitudeMasksAndConsumer()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HeroName = "Eorl", HasCareer = true, CareerId = "rider_of_rohan", Level = 12,
            Rows =
            {
                new CareerPerkRow(PassiveEffectType.Resistance, 0.15f, new List<(AttackTypeMask, float)>
                {
                    (AttackTypeMask.All, 0.10f),
                    (AttackTypeMask.Blunt, 0.05f),
                }),
                new CareerPerkRow(PassiveEffectType.PartyMovementSpeed, 0.08f, null),
            },
        };

        var lines = CareerPerkReport.Render(snapshot);
        var text = string.Join("\n", lines);

        StringAssert.Contains(text, "rider_of_rohan");
        StringAssert.Contains(text, "Resistance");
        StringAssert.Contains(text, "+15%");
        StringAssert.Contains(text, "All +10%");
        StringAssert.Contains(text, "Blunt +5%");
        StringAssert.Contains(text, "PartyMovementSpeed");
        StringAssert.Contains(text, CareerPerkConsumerMap.Describe(PassiveEffectType.Resistance));
        StringAssert.Contains(text, CareerPerkConsumerMap.Describe(PassiveEffectType.PartyMovementSpeed));
    }

    [TestMethod]
    public void Render_ClassFilteredPassive_ShowsTheValuePerClassNextToTheMaskBuckets()
    {
        // #769 E9: an infantry-only arrow ward reads +20% for infantry and nothing for the rest (a troop with no
        // class, which gets only the army-wide pips, included).
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows =
            {
                new CareerPerkRow(PassiveEffectType.TroopResistance, 0.20f,
                    new List<(AttackTypeMask, float)> { (AttackTypeMask.Ranged, 0.20f), (AttackTypeMask.Melee, 0f) },
                    CareerPerkReport.ByClass(0f, 0.20f, 0f, 0f, 0f)),
            },
        };

        var text = string.Join("\n", CareerPerkReport.Render(snapshot));

        StringAssert.Contains(text, "Ranged +20%");
        StringAssert.Contains(text, "classes [army-wide +0%, Infantry +20%, Ranged +0%, Cavalry +0%, Ranged+Cavalry +0%]");
    }

    [DataTestMethod]
    [DataRow(PassiveEffectType.TroopFearResistance)]
    [DataRow(PassiveEffectType.TroopKnockdownResistance)]
    [DataRow(PassiveEffectType.TroopCrushHold)]
    public void Render_TroopCounter_ShowsTheValuePerClassAndNamesItsConsumer(PassiveEffectType type)
    {
        // #769 E10: the three counters are class-filtered, so the cheat probes each class for them.
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows = { new CareerPerkRow(type, 0.30f, null, CareerPerkReport.ByClass(0f, 0.30f, 0f, 0f, 0f)) },
        };

        var text = string.Join("\n", CareerPerkReport.Render(snapshot));

        StringAssert.Contains(text, "classes [army-wide +0%, Infantry +30%, Ranged +0%, Cavalry +0%, Ranged+Cavalry +0%]");
        StringAssert.Contains(text, CareerPerkConsumerMap.Describe(type));
        StringAssert.Contains(CareerPerkConsumerMap.Describe(type), "CareerTroopHooks");
    }

    [TestMethod]
    public void Render_ClassFilteredCountType_ShowsTheCountPerClassWithNoPercentSign()
    {
        // #769 E11: TroopTraining is a flat count AND class-filtered (+4 for everyone, +6 more for infantry), so its
        // per-class row prints counts ("+10"), not percents ("+1000%"), and names the training model that reads it.
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows = { new CareerPerkRow(PassiveEffectType.TroopTraining, 4f, null, CareerPerkReport.ByClass(4f, 10f, 4f, 4f, 4f)) },
        };

        var text = string.Join("\n", CareerPerkReport.Render(snapshot));

        StringAssert.Contains(text, "TroopTraining +4 classes [army-wide +4, Infantry +10, Ranged +4, Cavalry +4, Ranged+Cavalry +4]");
        Assert.IsFalse(text.Contains("%"), text);
        StringAssert.Contains(text, CareerPerkConsumerMap.Describe(PassiveEffectType.TroopTraining));
        StringAssert.Contains(text, "TaomPartyTrainingModel");
    }

    [DataTestMethod]
    [DataRow(PassiveEffectType.TroopFearResistance)]
    [DataRow(PassiveEffectType.TroopKnockdownResistance)]
    [DataRow(PassiveEffectType.TroopCrushHold)]
    public void Describe_TroopCounter_NamesItsEvidenceLine(PassiveEffectType type)
    {
        // The counters write a per-leader, per-class tally at mission end, so a tester knows where to look.
        StringAssert.Contains(CareerPerkConsumerMap.Describe(type), "[CareerPerks] troop counter summary");
    }

    [TestMethod]
    public void Describe_TroopKnockdownResistance_NamesWhatItCoversAndWhatItDoesNot()
    {
        var text = CareerPerkConsumerMap.Describe(PassiveEffectType.TroopKnockdownResistance);

        // The engine's own checks read GetKnockDownResistance: a blow, a charge and the knockdown route of a dismount.
        StringAssert.Contains(text, "weapon, charge and dismount");
        // TAOM's forced knockdowns set the flag and never ask for a resistance; the heavy-charge Branch A ignores the
        // raised value. Only Sauron's overhead slam forces it among the signature strikes (the Nine's knockDown is
        // false), and every elephant-like attack shares one task (ElephantLikeAttackTasks), so one sentence names them all.
        StringAssert.Contains(text, "not TAOM's forced knockdowns (Sauron's overhead slam, the elephant-like creature attacks: elephant, mumakil trample and tusk swing, war ram, great elk, Animalia elk and moose; the troll brute-force ring), which never ask for a resistance; the heavy-charge Branch A ignores it");
    }

    [TestMethod]
    public void Describe_EveryConsumedType_NamesAConsumer()
    {
        foreach (var type in PassiveEffectConsumers.All)
        {
            var text = CareerPerkConsumerMap.Describe(type);
            Assert.IsFalse(text.StartsWith("no consumer"), $"{type} is consumed but Describe says it is not");
        }
    }

    [TestMethod]
    public void Render_PassiveWithNoClassBreakdown_PrintsNoClassesBracket()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows = { new CareerPerkRow(PassiveEffectType.Damage, 0.10f, null) },
        };

        Assert.IsFalse(string.Join("\n", CareerPerkReport.Render(snapshot)).Contains("classes ["));
    }

    [TestMethod]
    public void ByClass_AllFiveEqual_CollapsesToOneAllEntry()
    {
        var buckets = CareerPerkReport.ByClass(0.05f, 0.05f, 0.05f, 0.05f, 0.05f);

        Assert.AreEqual(1, buckets.Count);
        Assert.AreEqual(TroopClassMask.All, buckets[0].troopClass);
        Assert.AreEqual(0.05f, buckets[0].magnitude, 0.0001f);
    }

    [TestMethod]
    public void ByClass_DifferentPerClass_KeepsTheArmyWideValueAndEveryClassInOrder()
    {
        var buckets = CareerPerkReport.ByClass(0.05f, 0.15f, 0.05f, 0f, 0.05f);

        CollectionAssert.AreEqual(
            new[] { TroopClassMask.None, TroopClassMask.Infantry, TroopClassMask.Ranged, TroopClassMask.Cavalry, TroopClassMask.Ranged | TroopClassMask.Cavalry },
            buckets.Select(b => b.troopClass).ToArray());
        CollectionAssert.AreEqual(new[] { 0.05f, 0.15f, 0.05f, 0f, 0.05f }, buckets.Select(b => b.magnitude).ToArray());
    }

    [TestMethod]
    public void ByClass_OnlyTheHorseArcherReadingDiffers_IsNotCollapsed()
    {
        // The army-wide reading and the three single-class readings all match; only the horse archer's (a Ranged
        // and a Cavalry pip both reach it) differs. Dropping the horse-archer comparison from the collapse test
        // would fold this into one All entry and hide the 10% penalty, so the report must keep all five.
        var buckets = CareerPerkReport.ByClass(0f, 0f, 0f, 0f, -0.10f);

        Assert.AreEqual(5, buckets.Count);
        Assert.AreEqual(-0.10f, buckets[4].magnitude, 0.0001f);
    }

    [TestMethod]
    public void Render_HorseArcherReading_IsLabelledByItsTwoClasses()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows = { new CareerPerkRow(PassiveEffectType.TroopDamage, 0.20f, null, CareerPerkReport.ByClass(0f, 0f, 0.10f, 0.10f, 0.20f)) },
        };

        StringAssert.Contains(string.Join("\n", CareerPerkReport.Render(snapshot)), "Ranged+Cavalry +20%");
    }

    [TestMethod]
    public void Render_ClassBreakdownCollapsed_PrintsOneAllEntry()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows = { new CareerPerkRow(PassiveEffectType.TroopDamage, 0.05f, null, CareerPerkReport.ByClass(0.05f, 0.05f, 0.05f, 0.05f, 0.05f)) },
        };

        StringAssert.Contains(string.Join("\n", CareerPerkReport.Render(snapshot)), "classes [All +5%]");
    }

    [TestMethod]
    public void Render_FlatPassives_ShowAsCounts()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Rows = { new CareerPerkRow(PassiveEffectType.Health, 6f, null), new CareerPerkRow(PassiveEffectType.PartySize, 4f, null) },
        };

        var text = string.Join("\n", CareerPerkReport.Render(snapshot));

        StringAssert.Contains(text, "Health +6");
        StringAssert.Contains(text, "PartySize +4");
        Assert.IsFalse(text.Contains("+600%"), "a flat count is not a percentage");
    }

    [TestMethod]
    public void Render_IncludesProbesAndTheMissionBlockWhenPresent()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Probes = { "party speed 4.20 (Career +8%)" },
            Mission = new CareerPerkMissionSnapshot
            {
                PlayerAgentAlive = true,
                SwingSpeedMultiplier = 0.98f,
                MaxSpeedMultiplier = 1.05f,
                DamageMultiplierBonus = 0.15f,
                HasMount = true,
                MountChargeDamage = 1.44f,
                MountSpeed = 0.25f,
                AmmoSlots = { "slot 1 javelin 6/6" },
                LiveBuff = "dmg +15%",
            },
        };

        var text = string.Join("\n", CareerPerkReport.Render(snapshot));

        StringAssert.Contains(text, "party speed 4.20");
        StringAssert.Contains(text, "SwingSpeedMultiplier 0.98");
        StringAssert.Contains(text, "MountChargeDamage 1.44");
        StringAssert.Contains(text, "slot 1 javelin 6/6");
        StringAssert.Contains(text, "dmg +15%");
    }

    [TestMethod]
    public void Render_MissionBlockWithoutMount_SaysNoMount()
    {
        var snapshot = new CareerPerkSnapshot
        {
            HeroId = "h1", HasCareer = true, CareerId = "c",
            Mission = new CareerPerkMissionSnapshot { PlayerAgentAlive = true, HasMount = false },
        };

        var text = string.Join("\n", CareerPerkReport.Render(snapshot));

        StringAssert.Contains(text, "no mount");
    }

    [TestMethod]
    public void ConsumerMap_NamesAConsumerForEveryConsumedType()
    {
        foreach (var type in PassiveEffectConsumers.All)
            Assert.IsFalse(string.IsNullOrWhiteSpace(CareerPerkConsumerMap.Describe(type)), type + " has no consumer description");
    }
}
