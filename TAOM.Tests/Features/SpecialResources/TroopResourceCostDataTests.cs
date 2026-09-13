using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TAOM.Tests.Core;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// Gates the shipped <c>troop_resource_costs.xml</c> against <c>special_resources_config.xml</c>.
/// Until #590 the row's <c>resource_id</c> was documentation only (every charge lands in the
/// PLAYER's resolved resource). The encyclopedia badge now reads it to pick the icon, so a row
/// naming a resource the config does not define would render a blank badge with no log.
/// </summary>
[TestClass]
public class TroopResourceCostDataTests
{
    private static XDocument Load(string fileName) =>
        XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "special_resources", fileName));

    private static Dictionary<string, string> IconByResourceId() =>
        Load("special_resources_config.xml").Root.Elements("Resource")
            .ToDictionary(r => (string)r.Attribute("id"), r => (string)r.Attribute("icon_sprite") ?? "");

    private static List<TroopResourceCostEntry> CostRows() =>
        Load("troop_resource_costs.xml").Root.Elements("Troop")
            .Select(t => new TroopResourceCostEntry(
                (string)t.Attribute("id"),
                (string)t.Attribute("resource_id"),
                (int)ParseFloat(t, "upgrade_cost"),
                ParseFloat(t, "daily_upkeep"),
                (int)ParseFloat(t, "recruit_cost"),
                (int)ParseFloat(t, "merchant_cost")))
            .ToList();

    private static float ParseFloat(XElement element, string attribute)
    {
        var raw = (string)element.Attribute(attribute);
        return string.IsNullOrEmpty(raw) ? 0f : float.Parse(raw, CultureInfo.InvariantCulture);
    }

    [TestMethod]
    public void EveryCostRow_ResourceId_NamesAConfiguredResource()
    {
        var known = IconByResourceId();
        var rows = CostRows();
        Assert.IsTrue(rows.Count > 0, "troop_resource_costs.xml has no rows");

        var unresolved = rows.Where(r => string.IsNullOrEmpty(r.ResourceId) || !known.ContainsKey(r.ResourceId))
            .Select(r => $"{r.TroopId} -> '{r.ResourceId}'").ToList();

        Assert.AreEqual(0, unresolved.Count,
            "Cost rows whose resource_id names no <Resource id> in special_resources_config.xml: " + string.Join(", ", unresolved));
    }

    [TestMethod]
    public void EveryMarkedRow_ResolvesAnIconSprite()
    {
        var icons = IconByResourceId();
        var marked = CostRows().Where(SpecialResourceTroopBadge.IsShown).ToList();
        Assert.IsTrue(marked.Count > 0, "no row carries an upgrade, recruit or upkeep cost, so the badge can never show");

        var blank = marked
            .Where(r => !icons.TryGetValue(r.ResourceId, out var icon) || !icon.StartsWith("SpecialResources\\"))
            .Select(r => r.TroopId).ToList();

        Assert.AreEqual(0, blank.Count,
            $"Of {marked.Count} badged rows, these reach no SpecialResources\\ icon sprite: " + string.Join(", ", blank));
    }
}
