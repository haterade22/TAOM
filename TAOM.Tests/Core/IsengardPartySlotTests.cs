using System.Linq;
using System.IO;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// #1283: Isengard's war party slots. A clan's slots come from its tier alone (<c>DefaultClanTierModel.GetPartyLimitForTier</c>:
/// 1 below tier 3, 2 for tiers 3 and 4, 3 from tier 5; TAOM does not override it), so more lords in a clan add no party.
/// Isengard had 24 slots against Rohan's 31; its seven tier-3 clans went to tier 5, each with three adult heroes to lead.
/// Every Isengard clan is defined in <c>characters/clans.xml</c>, so the file alone gives the total.
/// </summary>
[TestClass]
public class IsengardPartySlotTests
{
    private static int SlotsForTier(int tier) => tier < 3 ? 1 : tier < 5 ? 2 : 3;

    [TestMethod]
    public void ShippedClans_IsengardHasThirtyOneWarPartySlots_AsManyAsRohan()
    {
        var clans = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "clans.xml"))
            .Descendants("Faction")
            .Where(f => (string)f.Attribute("super_faction") == "Kingdom.isengard")
            .ToList();
        Assert.AreEqual(11, clans.Count, "Isengard's clan count changed; recount the slots");
        Assert.IsFalse(clans.Any(c => (string)c.Attribute("is_minor_faction") == "true"),
            "a minor clan takes the other branch of GetPartyLimitForTier");

        int slots = clans.Sum(c => SlotsForTier(int.Parse((string)c.Attribute("tier") ?? "1")));

        Assert.AreEqual(31, slots);
    }
}
