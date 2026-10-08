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

    [TestMethod]
    public void ShippedConfig_Arthedain_IsHostileToEveryRealmTheFreePeoplesAllFight()
    {
        var rows = ShippedRelationships();
        var evil = new HashSet<string>(HostileTo(rows, CoreFreeRealms[0]));
        foreach (var free in CoreFreeRealms.Skip(1))
            evil.IntersectWith(HostileTo(rows, free));
        Assert.IsTrue(evil.Count >= 6, $"only {evil.Count} realms are hostile to every core free realm");

        var missing = evil.Except(HostileTo(rows, "arthedain")).OrderBy(k => k).ToList();

        Assert.AreEqual(0, missing.Count, $"Arthedain is not Hostile to: {string.Join(", ", missing)}");
    }
}
