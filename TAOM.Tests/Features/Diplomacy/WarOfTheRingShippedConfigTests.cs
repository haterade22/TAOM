using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using TAOM.Features.Diplomacy.Models;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Pins the SHIPPED war_of_the_ring.json. Every other WotR test injects its own fixture, so
/// nothing asserted the values players actually get — which is how the docs drifted to claiming
/// 30/45 while the mod shipped 2/14 (caught by review 2026-07-30, not by the suite).
/// </summary>
[TestClass]
public class WarOfTheRingShippedConfigTests
{
    private const int ExpectedPhase1Day = 30;
    private const int ExpectedPhase2Day = 44;

    // Walk-up mirrors ConfigIdValidationTests.FindModuleDataPath (private there; duplicated
    // rather than refactoring a passing test into a shared helper).
    private static string FindModuleDataPath()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "Main", "_Module", "ModuleData");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }

        return null;
    }

    private static WarOfTheRingConfig LoadShippedConfig()
    {
        var moduleData = FindModuleDataPath();
        if (moduleData == null)
        {
            Assert.Inconclusive("ModuleData path not found — run from repo root");
            return null;
        }

        var path = Path.Combine(moduleData, "diplomacy", "war_of_the_ring.json");
        Assert.IsTrue(File.Exists(path), $"Shipped config missing: {path}");

        return JsonConvert.DeserializeObject<WarOfTheRingConfig>(File.ReadAllText(path));
    }

    [TestMethod]
    public void ShippedConfig_Phase1TriggerDay_IsThirty()
    {
        var config = LoadShippedConfig();
        if (config == null) return;

        Assert.AreEqual(ExpectedPhase1Day, config.Phase1.TriggerDay);
    }

    [TestMethod]
    public void ShippedConfig_Phase2TriggerDay_IsFortyFour()
    {
        var config = LoadShippedConfig();
        if (config == null) return;

        Assert.AreEqual(ExpectedPhase2Day, config.Phase2.TriggerDay);
    }

    [TestMethod]
    public void ShippedConfig_Phase2_IsStrictlyAfterPhase1()
    {
        var config = LoadShippedConfig();
        if (config == null) return;

        Assert.IsTrue(
            config.Phase2.TriggerDay > config.Phase1.TriggerDay,
            $"Phase2 ({config.Phase2.TriggerDay}) must be strictly after Phase1 ({config.Phase1.TriggerDay}) — "
            + "equal days collapse both transitions into one tick and IsengardWar is never observable.");
    }

    [TestMethod]
    public void ShippedConfig_TestModePhase2Day_IsStrictlyAfterPhase1Day()
    {
        var config = LoadShippedConfig();
        if (config == null) return;

        Assert.IsTrue(
            config.TestMode.Phase2Day > config.TestMode.Phase1Day,
            $"TestMode Phase2Day ({config.TestMode.Phase2Day}) must be strictly after Phase1Day ({config.TestMode.Phase1Day}).");
    }

    [TestMethod]
    public void ShippedConfig_Phase1_DeclaresIsengardAndDunlandOnRohan()
    {
        var config = LoadShippedConfig();
        if (config == null) return;

        // vlandia == Rohan, empire == Dunland (XSLT cultures keep vanilla engine ids).
        CollectionAssert.Contains(
            config.Phase1.Wars.ConvertAll(w => $"{w.Attacker}->{w.Defender}"), "isengard->vlandia");
        CollectionAssert.Contains(
            config.Phase1.Wars.ConvertAll(w => $"{w.Attacker}->{w.Defender}"), "empire->vlandia");
    }

    [TestMethod]
    public void ShippedConfig_TestMode_IsDisabled()
    {
        var config = LoadShippedConfig();
        if (config == null) return;

        Assert.IsFalse(config.TestMode.Enabled, "Test mode must never ship enabled.");
    }
}
