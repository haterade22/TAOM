using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Pins the SHIPPED diplomacy.json. At Full War the War of the Ring declares war only between
/// Hostile pairs (WarOfTheRingService.DeclareHostileTierWars) and blocks their peace, so a free
/// realm missing a Hostile row sits that war out with the realm on the other side.
/// </summary>
[TestClass]
public class DiplomacyShippedConfigTests
{
    // The free realms every evil realm is set against (diplomacy.json, 2026-10-07).
    private static readonly string[] CoreFreeRealms =
        { "empire_w", "vlandia", "erebor", "sturgia", "rivendell", "lothlorien", "mirkwood" };

    private static List<KingdomRelationship> ShippedRelationships()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(CultureDataFixture.ModuleDataPath());
        var logger = Substitute.For<IModLogger>();
        var relationships = new DiplomacyConfigProvider(paths, logger).LoadConfig().Relationships;
        Assert.IsTrue(relationships.Count > 0, "diplomacy.json loaded no relationships");
        return relationships;
    }

    private static HashSet<string> HostileTo(IEnumerable<KingdomRelationship> rows, string kingdom) =>
        new(rows.Where(r => r.Tier == AllianceTier.Hostile && (r.KingdomA == kingdom || r.KingdomB == kingdom))
                .Select(r => r.KingdomA == kingdom ? r.KingdomB : r.KingdomA));

    // The table is keyed on the two id strings (DiplomacyService.MakeKey) and nothing resolves them, so a row with a
    // lore name ("rohan" for "vlandia") loads without error and never matches a live kingdom (#1291).
    [TestMethod]
    public void ShippedConfig_EveryKingdomId_IsARealKingdom()
    {
        var kingdoms = CultureDataFixture.KingdomIds();

        var unknown = ShippedRelationships().SelectMany(r => new[] { r.KingdomA, r.KingdomB })
            .Where(id => !kingdoms.Contains(id)).Distinct().OrderBy(id => id).ToList();

        Assert.AreEqual(0, unknown.Count, $"diplomacy.json names ids that are no kingdom: {string.Join(", ", unknown)}");
    }

    // Dunland ("empire") fights Rohan for Isengard. A Hostile row with Gondor ("empire_w") made Full War
    // declare that war and block its peace in every campaign.
    [TestMethod]
    public void ShippedConfig_Dunland_IsNotHostileToGondor()
    {
        var dunlandFoes = HostileTo(ShippedRelationships(), "empire");

        Assert.IsTrue(dunlandFoes.Contains("vlandia"), "Dunland lost its Hostile row with Rohan");
        Assert.IsFalse(dunlandFoes.Contains("empire_w"), "Dunland is Hostile to Gondor again");
    }

    // Arthedain and Ered Luin, the free realms added after the core seven, carry the same Hostile rows.
    [DataTestMethod]
    [DataRow("arthedain")]
    [DataRow("eredluin")]
    public void ShippedConfig_NewFreeRealm_IsHostileToEveryRealmTheFreePeoplesAllFight(string kingdom)
    {
        var rows = ShippedRelationships();
        var evil = new HashSet<string>(HostileTo(rows, CoreFreeRealms[0]));
        foreach (var free in CoreFreeRealms.Skip(1))
            evil.IntersectWith(HostileTo(rows, free));
        Assert.IsTrue(evil.Count >= 6, $"only {evil.Count} realms are hostile to every core free realm");

        var missing = evil.Except(HostileTo(rows, kingdom)).OrderBy(k => k).ToList();

        Assert.AreEqual(0, missing.Count, $"{kingdom} is not Hostile to: {string.Join(", ", missing)}");
    }

    private static List<CallToWarRefusal> ShippedRefusals()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(CultureDataFixture.ModuleDataPath());
        return new DiplomacyConfigProvider(paths, Substitute.For<IModLogger>()).LoadConfig().CallToWarRefusals;
    }

    // #1283: the second 150-day run showed Dunland ("empire") at war with Gondor ("empire_w") from day 2, called in by
    // the Misty Mountain Orcs. The maintainer's decision (2026-11-15): Dunland refuses a call to war against Gondor.
    [TestMethod]
    public void ShippedConfig_Dunland_RefusesACallToWarAgainstGondor()
    {
        Assert.IsTrue(ShippedRefusals().Any(r => r.Called == "empire" && r.Target == "empire_w"),
            "diplomacy.json has no callToWarRefusals row for Dunland against Gondor");
    }

    [TestMethod]
    public void ShippedConfig_EveryRefusalId_IsARealKingdom()
    {
        var kingdoms = CultureDataFixture.KingdomIds();

        var unknown = ShippedRefusals().SelectMany(r => new[] { r.Called, r.Target })
            .Where(id => !kingdoms.Contains(id)).Distinct().OrderBy(id => id).ToList();

        Assert.AreEqual(0, unknown.Count, $"callToWarRefusals names ids that are no kingdom: {string.Join(", ", unknown)}");
    }
}
