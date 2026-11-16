using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// #1283: Harad's war party slots. A clan's slots come from its tier alone (<c>DefaultClanTierModel.GetPartyLimitForTier</c>:
/// 1 below tier 3, 2 for tiers 3 and 4, 3 from tier 5). In a 150-day AI run Harad lost a castle on 11 of the 13 days Gondor gained a
/// fief, falling from 11 castles to 3 with 19 slots. Its nine clans went to tier 5, each with at least three adult heroes to lead.
/// Harad's clans are the vanilla <c>clan_aserai_1</c> to <c>_9</c>, and <c>spclans.xslt</c> sets each one's tier.
/// </summary>
[TestClass]
public class HaradPartySlotTests
{
    private static int SlotsForTier(int tier) => tier < 3 ? 1 : tier < 5 ? 2 : 3;

    [TestMethod]
    public void ShippedClans_HaradHasTwentySevenWarPartySlots()
    {
        XNamespace xsl = "http://www.w3.org/1999/XSL/Transform";
        var templates = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "spclans.xslt"))
            .Descendants(xsl + "template")
            .Where(t => Regex.IsMatch((string)t.Attribute("match") ?? "", @"^Faction\[@id='clan_aserai_\d+'\]$"))
            .ToList();
        Assert.AreEqual(9, templates.Count, "Harad's clan count changed; recount the slots");

        int slots = templates.Sum(t => SlotsForTier(int.Parse(t.Descendants(xsl + "attribute")
            .Single(a => (string)a.Attribute("name") == "tier").Value.Trim())));

        Assert.AreEqual(27, slots);
    }
}
