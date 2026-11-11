using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;
using TAOM.Tests.Core;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// Pins the SHIPPED war_pledge.json against the shipped kingdoms, alignment.json and diplomacy.json (#1113).
/// Every other War Pledge test injects its own table, so nothing else reads the values players get. A lean
/// row that names no kingdom, or a realm that already has a side, is silently never courted in play.
/// </summary>
[TestClass]
public class WarPledgeShippedConfigTests
{
    private static WarPledgeConfig ShippedConfig(out IModLogger logger)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(CultureDataFixture.ModuleDataPath());
        logger = Substitute.For<IModLogger>();
        return new WarPledgeConfigProvider(paths, logger).LoadConfig();
    }

    private static AlignmentService ShippedAlignment()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(CultureDataFixture.ModuleDataPath());
        return new AlignmentService(new AlignmentConfigProvider(paths, Substitute.For<IModLogger>()), Substitute.For<IModLogger>());
    }

    private static List<KingdomRelationship> ShippedRelationships()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(CultureDataFixture.ModuleDataPath());
        return new DiplomacyConfigProvider(paths, Substitute.For<IModLogger>()).LoadConfig().Relationships;
    }

    // The kingdoms taom_spkingdoms.xml declares plus the vanilla ones spkingdoms.xslt keeps, read from the
    // data as WarTheaterConfigInvariantsTests does.
    private static HashSet<string> ShippedKingdomIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kingdom in XDocument.Parse(RepoPaths.ReadSource("Main/_Module/ModuleData/taom_spkingdoms.xml")).Descendants("Kingdom"))
        {
            var id = (string?)kingdom.Attribute("id");
            if (!string.IsNullOrEmpty(id))
                ids.Add(id!);
        }

        foreach (Match m in Regex.Matches(RepoPaths.ReadSource("Main/_Module/ModuleData/spkingdoms.xslt"), @"Kingdom\[@id=['""]([a-z_]+)['""]\]"))
            ids.Add(m.Groups[1].Value);

        Assert.IsTrue(ids.Count >= 20, $"expected at least 20 kingdoms, parsed {ids.Count}");
        return ids;
    }

    [TestMethod]
    public void ShippedConfig_LoadsWithoutAWarningOrAnError()
    {
        var config = ShippedConfig(out var logger);

        Assert.IsTrue(config.Leans.Count > 0, "war_pledge.json loaded no lean row");
        logger.DidNotReceiveWithAnyArgs().LogWarning(default);
        logger.DidNotReceiveWithAnyArgs().LogError(default);
    }

    [TestMethod]
    public void ShippedConfig_Numbers_AreTheDocumentedOnes()
    {
        var config = ShippedConfig(out _);

        Assert.AreEqual(25, config.LosingBalance);
        Assert.AreEqual(30, config.DaysBetweenAiJoins);
    }

    [TestMethod]
    public void ShippedConfig_EveryLeanRow_NamesAShippedKingdom()
    {
        var kingdoms = ShippedKingdomIds();

        var dead = ShippedConfig(out _).Leans.Select(l => l.KingdomId).Where(id => !kingdoms.Contains(id)).ToList();

        Assert.AreEqual(0, dead.Count, "war_pledge.json lean rows that are not kingdom ids: " + string.Join(", ", dead));
    }

    // A realm that has a side in alignment.json can never pledge, so a lean row for it does nothing.
    [TestMethod]
    public void ShippedConfig_EveryLeanRow_NamesARealmThatIsNeutralInAlignmentJson()
    {
        var alignment = ShippedAlignment();

        var sided = ShippedConfig(out _).Leans
            .Where(l => alignment.GetKingdomSide(l.KingdomId) != FactionSide.Neutral)
            .Select(l => l.KingdomId).ToList();

        Assert.AreEqual(0, sided.Count, "lean rows for realms that already have a side: " + string.Join(", ", sided));
    }

    // The other direction: a Neutral kingdom with no lean row is a decision nobody made.
    [TestMethod]
    public void ShippedConfig_EveryNeutralKingdom_HasALeanRow()
    {
        var alignment = ShippedAlignment();
        var leans = new HashSet<string>(ShippedConfig(out _).Leans.Select(l => l.KingdomId), StringComparer.Ordinal);

        var missing = ShippedKingdomIds()
            .Where(id => alignment.GetKingdomSide(id) == FactionSide.Neutral && !leans.Contains(id))
            .OrderBy(id => id, StringComparer.Ordinal).ToList();

        Assert.AreEqual(0, missing.Count,
            "Neutral kingdoms with no row in war_pledge.json (give each a lean, Neutral for a true neutral): " + string.Join(", ", missing));
    }

    // Mike's leans of 2026-11-10 (#1113), in courting order per side.
    [TestMethod]
    public void ShippedConfig_Leans_AreTheDecidedOnesInCourtingOrder()
    {
        var leans = ShippedConfig(out _).Leans;

        CollectionAssert.AreEqual(
            new[] { "battania", "rhudaur", "umbar", "shaghana", "abanissa" },
            leans.Where(l => l.Lean == FactionSide.Evil).Select(l => l.KingdomId).ToList(), "Evil");
        CollectionAssert.AreEqual(
            new[] { "cardolan", "dorwinion" },
            leans.Where(l => l.Lean == FactionSide.Free).Select(l => l.KingdomId).ToList(), "Free");
        CollectionAssert.AreEquivalent(
            new[] { "anduinvale", "angaladh", "southrhovanion" },
            leans.Where(l => l.Lean == FactionSide.Neutral).Select(l => l.KingdomId).ToList(), "true neutral");
    }

    // The join refuses a realm with a Hostile row to a realm of the side it joins. A lean that always meets
    // that guard would never be courted, so the shipped leans must be free of it.
    [TestMethod]
    public void ShippedConfig_NoLean_PointsAtASideTheRealmIsHostileTo()
    {
        var alignment = ShippedAlignment();
        var rows = ShippedRelationships();
        var blocked = new List<string>();

        foreach (var lean in ShippedConfig(out _).Leans.Where(l => l.Lean != FactionSide.Neutral))
        {
            foreach (var row in rows.Where(r => r.Tier == AllianceTier.Hostile))
            {
                var other = row.KingdomA == lean.KingdomId ? row.KingdomB : row.KingdomB == lean.KingdomId ? row.KingdomA : null;
                if (other != null && alignment.GetKingdomSide(other) == lean.Lean)
                    blocked.Add($"{lean.KingdomId} leans {lean.Lean} but is Hostile to {other}");
            }
        }

        Assert.AreEqual(0, blocked.Count, string.Join("; ", blocked));
    }
}
