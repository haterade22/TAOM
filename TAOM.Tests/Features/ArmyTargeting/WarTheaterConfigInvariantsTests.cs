using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Tests.Features.ArmyTargeting;

/// <summary>
/// Pins the SHIPPED army_targeting.json against the shipped kingdom data.
///
/// Why this suite exists: a config keyed on kingdom ids where a lore name silently resolves to
/// nothing has shipped in TAOM five or more times (xml-data.md "Config ID Cross-Reference"). Six
/// TAOM kingdoms keep vanilla StringIds, so "rohan" is a dead key and "vlandia" is the live one.
/// A dead key here would not throw, would not log, and would just quietly drop a whole front from
/// the weighting. The service is deliberately fail-open, which makes the failure even quieter, so
/// the guard has to live at the data layer instead.
/// </summary>
[TestClass]
public class WarTheaterConfigInvariantsTests
{
    /// <summary>The six kingdoms whose runtime StringId is the vanilla one, not the lore name.</summary>
    private static readonly Dictionary<string, string> LoreNameTraps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["rohan"] = "vlandia",
        ["dunland"] = "empire",
        ["harad"] = "aserai",
        ["rhun"] = "khuzait",
        ["dale"] = "sturgia",
        ["khand"] = "battania",
    };

    /// <summary>
    /// Kingdoms deliberately carrying no theater. Both sit on a closed 24-node land-navigation
    /// component and can reach nothing by land, so an empty list records the truth rather than
    /// implying a capability they do not have. Pinned so nobody can passive a kingdom silently.
    /// </summary>
    private static readonly string[] ExpectedPassiveKingdoms = { "bluecraig", "lindon" };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static ArmyTargetingConfig LoadShippedConfig()
    {
        var path = Path.Combine(FindRepoRoot(), "Main", "_Module", "ModuleData", "configs", "army_targeting.json");
        Assert.IsTrue(File.Exists(path), $"army_targeting.json not found at {path}");
        var config = JsonConvert.DeserializeObject<ArmyTargetingConfig>(File.ReadAllText(path));
        Assert.IsNotNull(config, "army_targeting.json did not deserialize");
        return config;
    }

    /// <summary>
    /// The 22 live kingdom StringIds: 14 declared in taom_spkingdoms.xml plus 8 vanilla kingdoms
    /// retagged by spkingdoms.xslt. Read from the data rather than hardcoded, so adding a kingdom
    /// makes this suite demand a theater decision for it.
    /// </summary>
    private static HashSet<string> LoadKingdomIds()
    {
        var root = FindRepoRoot();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        var newKingdoms = Path.Combine(root, "Main", "_Module", "ModuleData", "taom_spkingdoms.xml");
        Assert.IsTrue(File.Exists(newKingdoms), $"taom_spkingdoms.xml not found at {newKingdoms}");
        foreach (var element in XDocument.Load(newKingdoms).Descendants("Kingdom"))
        {
            var id = (string)element.Attribute("id");
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }

        // The XSLT overrides vanilla kingdoms via `<xsl:template match="Kingdom[@id='empire_w']">`.
        var xslt = Path.Combine(root, "Main", "_Module", "ModuleData", "spkingdoms.xslt");
        Assert.IsTrue(File.Exists(xslt), $"spkingdoms.xslt not found at {xslt}");
        foreach (Match m in Regex.Matches(File.ReadAllText(xslt), @"Kingdom\[@id=['""]([a-z_]+)['""]\]"))
            ids.Add(m.Groups[1].Value);

        Assert.IsTrue(ids.Count >= 20, $"expected at least 20 kingdoms, parsed {ids.Count}");
        return ids;
    }

    [TestMethod]
    public void EveryKingdomTheaterKey_ResolvesToARealKingdom()
    {
        var config = LoadShippedConfig();
        var kingdoms = LoadKingdomIds();

        var dead = config.KingdomTheaters.Keys.Where(k => !kingdoms.Contains(k)).ToList();

        Assert.AreEqual(0, dead.Count,
            $"army_targeting.json KingdomTheaters has keys that are not real kingdom StringIds: {string.Join(", ", dead)}. " +
            "A dead key is silent at every layer and drops that kingdom's front from the weighting.");
    }

    [TestMethod]
    public void KingdomTheaters_UsesNoLoreNameForAnXsltKingdom()
    {
        var config = LoadShippedConfig();

        var traps = config.KingdomTheaters.Keys
            .Where(k => LoreNameTraps.ContainsKey(k))
            .Select(k => $"'{k}' should be '{LoreNameTraps[k]}'")
            .ToList();

        Assert.AreEqual(0, traps.Count,
            $"army_targeting.json uses lore names where the runtime StringId is the vanilla one: {string.Join("; ", traps)}");
    }

    [TestMethod]
    public void EveryPriorityTargetFactionKey_ResolvesToARealKingdom()
    {
        var config = LoadShippedConfig();
        var kingdoms = LoadKingdomIds();

        var dead = config.FactionPriorityTargets.Keys.Where(k => !kingdoms.Contains(k)).ToList();
        var deadAggression = config.FactionAggressionMultipliers.Keys.Where(k => !kingdoms.Contains(k)).ToList();

        Assert.AreEqual(0, dead.Count, $"FactionPriorityTargets has unresolvable keys: {string.Join(", ", dead)}");
        Assert.AreEqual(0, deadAggression.Count, $"FactionAggressionMultipliers has unresolvable keys: {string.Join(", ", deadAggression)}");
    }

    [TestMethod]
    public void EveryKingdom_HasATheaterDecisionRecorded()
    {
        // Absent means neutral at runtime, which is the right fail-open default for a kingdom that
        // cannot exist in config (new_kingdom, rebels). For a kingdom that ships with the mod,
        // absence is an oversight, and this is where it surfaces.
        var config = LoadShippedConfig();
        var kingdoms = LoadKingdomIds();

        var missing = kingdoms.Where(k => !config.KingdomTheaters.ContainsKey(k)).OrderBy(k => k).ToList();

        Assert.AreEqual(0, missing.Count,
            $"these shipped kingdoms have no entry in KingdomTheaters: {string.Join(", ", missing)}. " +
            "Give each one a theater list, or an empty list to mark it deliberately passive.");
    }

    [TestMethod]
    public void EveryTheaterNameUsed_IsDeclared()
    {
        var config = LoadShippedConfig();
        var declared = new HashSet<string>(config.Theaters, StringComparer.Ordinal);

        var undeclared = config.KingdomTheaters
            .SelectMany(kvp => (kvp.Value ?? new List<string>()).Select(t => new { kvp.Key, Theater = t }))
            .Where(x => !declared.Contains(x.Theater))
            .Select(x => $"{x.Key} -> '{x.Theater}'")
            .ToList();

        Assert.AreEqual(0, undeclared.Count,
            $"undeclared theater names (a typo becomes a private theater of one): {string.Join(", ", undeclared)}");
    }

    [TestMethod]
    public void DeclaredTheaters_AreNonEmptyAndUnique()
    {
        var config = LoadShippedConfig();

        Assert.IsTrue(config.Theaters.Count > 0, "no theaters declared");
        CollectionAssert.AllItemsAreUnique(config.Theaters, "duplicate theater name");
        CollectionAssert.AllItemsAreNotNull(config.Theaters);
    }

    [TestMethod]
    public void PassiveKingdoms_AreExactlyTheAcknowledgedOnes()
    {
        var config = LoadShippedConfig();

        var passive = config.KingdomTheaters
            .Where(kvp => kvp.Value == null || kvp.Value.Count == 0)
            .Select(kvp => kvp.Key)
            .OrderBy(k => k)
            .ToList();

        CollectionAssert.AreEquivalent(ExpectedPassiveKingdoms.OrderBy(k => k).ToList(), passive,
            "a kingdom with no theater is invisible to the weighting. If that is intended, add it to " +
            "ExpectedPassiveKingdoms with the reason; if not, give it a front.");
    }

    [TestMethod]
    public void EachKingdomsTheaterList_HasNoDuplicates()
    {
        var config = LoadShippedConfig();

        foreach (var kvp in config.KingdomTheaters)
        {
            var list = kvp.Value ?? new List<string>();
            CollectionAssert.AllItemsAreUnique(list, $"{kvp.Key} lists a theater twice; the first entry is its primary front, so duplicates make the ordering ambiguous");
        }
    }

    [TestMethod]
    public void TheaterWeights_AreOrderedForeignBelowSecondaryBelowPrimary()
    {
        var config = LoadShippedConfig();

        Assert.IsTrue(config.ForeignTheaterWeight <= config.SecondaryTheaterWeight,
            $"foreign ({config.ForeignTheaterWeight}) must not outrank secondary ({config.SecondaryTheaterWeight})");
        Assert.IsTrue(config.SecondaryTheaterWeight <= config.PrimaryTheaterWeight,
            $"secondary ({config.SecondaryTheaterWeight}) must not outrank primary ({config.PrimaryTheaterWeight})");
        Assert.IsTrue(config.ForeignTheaterWeight > 0f,
            "a zero foreign weight is a veto, and a vetoed kingdom gathers, finds no legal target, patrols, then loses its army to Army.CheckInactivity");
    }

    [TestMethod]
    public void BorderRescueRadius_IsInRangeAndClearsTheWidestRealFront()
    {
        var config = LoadShippedConfig();

        Assert.IsTrue(config.BorderRescueRadiusInTownGaps >= 1.0f && config.BorderRescueRadiusInTownGaps <= 20.0f,
            $"border rescue radius {config.BorderRescueRadiusInTownGaps} is outside [1,20]");

        // Lothlorien to Gundabad is the widest genuine hostile front on the live map at 3.08 town
        // gaps, measured against the engine's own path cache. The gate must clear it, or Patch22
        // refuses to rescue a real border target vanilla's two-hop topology scored as unreachable.
        Assert.IsTrue(config.BorderRescueRadiusInTownGaps >= 3.1f,
            $"border rescue radius {config.BorderRescueRadiusInTownGaps} would refuse the 3.08-gap Lothlorien to Gundabad front");

        // And it must not swallow the map. An earlier version shared this value with a score-side
        // falloff radius; widening that to 6 gaps silently admitted all 80 authored entries and the
        // gate stopped bounding anything.
        Assert.IsTrue(config.BorderRescueRadiusInTownGaps <= 4.0f,
            $"border rescue radius {config.BorderRescueRadiusInTownGaps} is wide enough to stop bounding the rescue");
    }

    [TestMethod]
    public void ShippedConfig_SurvivesProviderValidationUnchanged()
    {
        // The shipped file must be clean by its own validator, or players see warnings on boot and
        // the compiled defaults quietly replace what we authored.
        var config = LoadShippedConfig();
        var logger = Substitute.For<TAOM.Core.Logging.IModLogger>();
        var pathService = Substitute.For<TAOM.Core.Infrastructure.IPathService>();
        var provider = new ArmyTargetingConfigProvider(pathService, logger);

        var before = new
        {
            config.BorderRescueRadiusInTownGaps,
            config.PrimaryTheaterWeight,
            config.SecondaryTheaterWeight,
            config.ForeignTheaterWeight,
            TheaterEntries = config.KingdomTheaters.Sum(k => k.Value?.Count ?? 0),
        };

        var after = provider.Validate(config);

        Assert.AreEqual(before.BorderRescueRadiusInTownGaps, after.BorderRescueRadiusInTownGaps);
        Assert.AreEqual(before.PrimaryTheaterWeight, after.PrimaryTheaterWeight);
        Assert.AreEqual(before.SecondaryTheaterWeight, after.SecondaryTheaterWeight);
        Assert.AreEqual(before.ForeignTheaterWeight, after.ForeignTheaterWeight);
        Assert.AreEqual(before.TheaterEntries, after.KingdomTheaters.Sum(k => k.Value?.Count ?? 0),
            "validation dropped a theater entry, so a name in the shipped file is undeclared");
        Assert.AreEqual(80, after.FactionPriorityTargets.Sum(k => k.Value?.Count ?? 0),
            "the shipped priority lists lost entries to validation, or the 2026-08-21 prune came back");

        logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }
}
