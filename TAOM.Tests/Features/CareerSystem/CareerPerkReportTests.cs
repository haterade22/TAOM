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
