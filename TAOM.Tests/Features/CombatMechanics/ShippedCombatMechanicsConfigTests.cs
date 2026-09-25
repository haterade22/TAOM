using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features;
using TAOM.Features.CombatMechanics;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// Pins the combat-mechanics config actually shipped to players, and its agreement with the compiled
/// defaults and the MCM default.
///
/// Shield penetration is the reason this file exists. It ships OFF with empty grant lists since
/// 2026-08-17, and that state is spread across THREE surfaces which must agree: this JSON, the
/// compiled <see cref="ShieldPenetrationConfig"/> initializers (the revert-to target when the JSON
/// fails validation), and <see cref="TaomSettings.EnableShieldPenetration"/> (the MCM default).
/// Before this file, only the compiled surface was covered: restoring "Javelin" to the shipped JSON
/// would have handed every player a live class-wide shield-penetration grant with the whole suite
/// still green. The provider is fail-soft by design, so a bad shipped file reverts to defaults and
/// looks fine while ignoring what was authored.
/// </summary>
[TestClass]
public class ShippedCombatMechanicsConfigTests
{
    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, @"Main\_Module\ModuleData");

    private static string ConfigPath =>
        Path.Combine(ModuleDataPath, "combat_mechanics", "combat_mechanics_config.json");

    private IModLogger _logger = null!;
    private CombatMechanicsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(ModuleDataPath);
        _logger = Substitute.For<IModLogger>();
        _sut = new CombatMechanicsConfigProvider(pathService, _logger);
    }

    // ---- Cavalry (#610) ------------------------------------------------------------------------

    [TestMethod]
    public void ShippedConfig_ChargeKnockdown_IsVanillaParityWithAnyAngleAutoFloor()
    {
        // Mike, 2026-09-17: every ordinary victim gets vanilla's rule (the floor never scales the
        // penetration below it) and a full-speed horse + man contact floors a man from any angle
        // (auto ratio 6 == horse 400 + rider 80 over man 80). Change these with a control battle.
        var c = _sut.GetConfig().ChargeKnockdown;

        Assert.AreEqual(6f, c.NeutralWeightRatio, 0.0001f);
        Assert.AreEqual(6f, c.AutoKnockdownWeightRatio, 0.0001f);
        Assert.AreEqual(1f, c.MinPenetrationFactor, 0.0001f);
        Assert.AreEqual(0.4f, c.HorseChargePenetration, 0.0001f);
    }

    [TestMethod]
    public void ShippedConfig_OrcShieldCrushRaces_MatchCompiledDefaultsAndIncludeTheNine()
    {
        // Mike, 2026-09-23 (#644): three of the Nine had orc shield-crush only because they were
        // tagged uruk; as race nazghul all nine get it, beside sauron. The compiled list is what a
        // missing or invalid file reverts to, so the two surfaces agree entry for entry.
        var shipped = _sut.GetConfig().CrushThrough.OrcShieldCrushRaces;

        CollectionAssert.AreEqual(new CombatMechanicsConfig().CrushThrough.OrcShieldCrushRaces, shipped);
        CollectionAssert.Contains(shipped, "nazghul");
    }

    [TestMethod]
    public void ShippedConfig_OnlyTrollsDwarvesAndSauronResistAHorse()
    {
        // Once the weight term stops protecting the 160-weight victims, the race rows are the
        // only thing keeping a troll on its feet; uruk-hai deliberately lost theirs (parity).
        var races = _sut.GetConfig().RaceModifiers;

        Assert.AreEqual(4f, races["cave_troll"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(4f, races["hill_troll"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(2.5f, races["dwarf"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(3f, races["sauron"].KnockdownResistanceMultiplier, 0.0001f);
        Assert.AreEqual(1f, races["uruk_hai"].KnockdownResistanceMultiplier, 0.0001f);
    }

    [TestMethod]
    public void ShippedConfig_BothTrollsCarry200BaseHitPoints_AndNoOtherRaceDoes()
    {
        // Mike, 2026-09-25: the cave and the hill troll have 200 health. Custom Battle reads the Monster's
        // hit_points; the campaign starts every troop at 100, so this row is what lifts a campaign troll.
        var races = _sut.GetConfig().RaceModifiers;

        Assert.AreEqual(200, races["cave_troll"].BaseHitPoints);
        Assert.AreEqual(200, races["hill_troll"].BaseHitPoints);
        foreach (var pair in races)
            if (pair.Key != "cave_troll" && pair.Key != "hill_troll")
                Assert.AreEqual(0, pair.Value.BaseHitPoints, pair.Key + " gained a base hit point row");
    }

    [TestMethod]
    public void ShippedConfig_ChargeDamage_MatchesMikesKingdomTable()
    {
        var m = _sut.GetConfig().ChargeDamage.CultureMultipliers;

        foreach (var elf in new[] { "mirkwood", "lothlorien", "rivendell", "lindon" })
            Assert.AreEqual(1.6f, m[elf], 0.0001f, elf);
        Assert.AreEqual(1.5f, m["vlandia"], 0.0001f, "Rohan");
        Assert.AreEqual(1.4f, m["khuzait"], 0.0001f, "Rhun");
        Assert.AreEqual(1.3f, m["gondor"], 0.0001f);
        Assert.AreEqual(1.2f, m["sturgia"], 0.0001f, "Dale");
        foreach (var orc in new[] { "mordor", "isengard", "gundabad", "dolguldur", "umbar" })
            Assert.AreEqual(1.2f, m[orc], 0.0001f, orc);
        Assert.AreEqual(1f, m["erebor"], 0.0001f, "dwarves do not ride");
    }

    [TestMethod]
    public void ShippedConfig_EveryChargeDamageKeyIsAShippedCultureId()
    {
        // xml-data.md "Config ID Cross-Reference": a dead dictionary key is silent at every layer.
        // Custom cultures come from taom_spcultures.xml; the six XSLT cultures keep vanilla ids.
        var xml = File.ReadAllText(Path.Combine(ModuleDataPath, "taom_spcultures.xml"));
        var custom = System.Text.RegularExpressions.Regex.Matches(xml, "<Culture\\s[^>]*?\\bid=\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.Singleline);
        var known = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal)
            { "vlandia", "empire", "aserai", "khuzait", "sturgia", "battania" };
        foreach (System.Text.RegularExpressions.Match match in custom)
            known.Add(match.Groups[1].Value);

        foreach (var key in _sut.GetConfig().ChargeDamage.CultureMultipliers.Keys)
            Assert.IsTrue(known.Contains(key), $"'{key}' is not a shipped culture id (rohan is vlandia, dunland is empire).");
    }

    [TestMethod]
    public void ShippedConfig_FileExists()
        => Assert.IsTrue(File.Exists(ConfigPath), $"Shipped config missing at {ConfigPath}");

    [TestMethod]
    public void ShippedConfig_ParsesWithoutErrorOrRejection()
    {
        _sut.GetConfig();

        _logger.DidNotReceive().LogError(Arg.Any<string>());
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("not found")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("contained invalid values")));
    }

    [TestMethod]
    public void ShippedConfig_ShieldPenetrationDisabledWithEmptyGrantLists()
    {
        // The load-bearing one. An empty weaponClasses is what makes the mechanic inert for players
        // whose MCM profile still has the toggle saved as ON from before 2026-08-17 — MCM merges
        // over JSON per read, so the toggle itself cannot be flipped for them retroactively.
        // Re-adding "Javelin" here would restore a class-wide CanPenetrateShield +
        // MultiplePenetration grant to every agent in the game, and the 3.33x shield-damage
        // correction with it if that flag were also flipped.
        var config = _sut.GetConfig();

        Assert.IsFalse(config.ShieldPenetration.Enabled,
            "shieldPenetration.enabled must ship false — javelins pierce shields only via the " +
            "vanilla Throwing.Impale grant. See docs/features/combat-mechanics.md.");
        Assert.AreEqual(0, config.ShieldPenetration.WeaponClasses.Count,
            "shieldPenetration.weaponClasses must ship EMPTY. This is the backstop against a " +
            "persisted MCM toggle; a class entry here re-arms the mechanic for those players.");
        Assert.AreEqual(0, config.ShieldPenetration.ItemIds.Count);
        Assert.IsFalse(config.ShieldPenetration.AddMultiplePenetration);
        Assert.IsFalse(config.ShieldPenetration.RuntimeShieldDamageCorrectionEnabled,
            "The /0.3 correction was disproved against v1.4.8 (ComputeBlowDamageOnShield picks the " +
            "missile multiplier by weapon CLASS and never reads the penetration flags for Javelin).");
    }

    [TestMethod]
    public void ShippedConfig_ShieldPenetrationMatchesCompiledDefaults()
    {
        // CombatMechanicsConfig.cs states the compiled values "must match the shipped JSON" because
        // they are the revert-to target on a validation failure. Nothing enforced that before this
        // test, so the two surfaces could drift and the drift would only show as a behaviour change
        // in whichever direction a malformed edit happened to push.
        var shipped = _sut.GetConfig().ShieldPenetration;
        var compiled = new ShieldPenetrationConfig();

        Assert.AreEqual(compiled.Enabled, shipped.Enabled);
        Assert.AreEqual(compiled.WeaponClasses.Count, shipped.WeaponClasses.Count);
        Assert.AreEqual(compiled.ItemIds.Count, shipped.ItemIds.Count);
        Assert.AreEqual(compiled.AddMultiplePenetration, shipped.AddMultiplePenetration);
        Assert.AreEqual(compiled.RuntimeShieldDamageCorrectionEnabled, shipped.RuntimeShieldDamageCorrectionEnabled);
        Assert.AreEqual(compiled.RuntimeShieldDamageCorrectionDivisor, shipped.RuntimeShieldDamageCorrectionDivisor, 0.0001f);
    }

    [TestMethod]
    public void McmDefault_ShieldPenetrationOff_MatchesShippedConfig()
    {
        // The third surface. TaomSettings is the MCM default for a FRESH profile; existing profiles
        // keep whatever they saved. A revert of this one line is otherwise invisible to CI.
        var settings = new TaomSettings();

        Assert.IsFalse(settings.EnableShieldPenetration,
            "TaomSettings.EnableShieldPenetration must default false to match the shipped JSON.");
        Assert.AreEqual(_sut.GetConfig().ShieldPenetration.Enabled, settings.EnableShieldPenetration,
            "The MCM default and the shipped JSON 'enabled' must agree, or a fresh install and a " +
            "config-only reader disagree about whether the mechanic is on.");
    }
}
