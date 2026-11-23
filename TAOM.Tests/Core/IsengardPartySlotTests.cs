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

    private static System.Collections.Generic.List<XElement> IsengardClans() =>
        XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "characters", "clans.xml"))
            .Descendants("Faction")
            .Where(f => (string)f.Attribute("super_faction") == "Kingdom.isengard")
            .ToList();

    [TestMethod]
    public void ShippedClans_IsengardHasThirtyThreeWarPartySlots_MoreThanRohan()
    {
        var clans = IsengardClans();
        Assert.AreEqual(11, clans.Count, "Isengard's clan count changed; recount the slots");
        Assert.IsFalse(clans.Any(c => (string)c.Attribute("is_minor_faction") == "true"),
            "a minor clan takes the other branch of GetPartyLimitForTier");

        int slots = clans.Sum(c => SlotsForTier(int.Parse((string)c.Attribute("tier") ?? "1")));

        Assert.AreEqual(33, slots);
    }

    /// <summary>
    /// Isengard's numbers pass (23 Nov 2026): every clan one tier up, so each lord leads 15 more men and each clan
    /// leader 25 more (<c>DefaultPartySizeLimitModel.GetTierEffectInternal</c>), and the armies they form grow with them.
    /// </summary>
    [TestMethod]
    public void ShippedClans_EveryIsengardClanIsTierFiveOrAbove_NineAtTheTop()
    {
        var tiers = IsengardClans().Select(c => int.Parse((string)c.Attribute("tier") ?? "1")).ToList();

        Assert.IsTrue(tiers.All(t => t >= 5), string.Join(",", tiers));
        Assert.AreEqual(9, tiers.Count(t => t == 6));
    }

    /// <summary>
    /// Isengard's numbers pass: every Isengard troop up to level 31 counts as one man against the party limit, so a
    /// veteran Uruk or warg rider host fields as many bodies as a Gondor one. Only the level-36 and level-41 elites pay
    /// more.
    /// </summary>
    [TestMethod]
    public void ShippedWeights_IsengardTroopsUpToLevel31_CountAsOneMan()
    {
        string data = CultureDataFixture.ModuleDataPath();
        var weighted = XDocument.Load(Path.Combine(data, "TroopWeights", "troop_weights.xml"))
            .Descendants("TroopWeight")
            .ToDictionary(w => (string)w.Attribute("id"), w => (string)w.Attribute("weight"));
        var troops = XDocument.Load(Path.Combine(data, "troops", "troops_isengard.xml"))
            .Descendants("NPCCharacter")
            .Where(t => t.Attribute("level") != null)
            .ToList();
        Assert.IsTrue(troops.Count > 40, "troops_isengard.xml not read");

        var heavy = troops
            .Where(t => int.Parse((string)t.Attribute("level")) <= 31 && weighted.ContainsKey((string)t.Attribute("id")))
            .Select(t => (string)t.Attribute("id"))
            .ToList();

        Assert.AreEqual(0, heavy.Count, "weighted at level 31 or below: " + string.Join(", ", heavy));
    }

    /// <summary>
    /// The level 31 to 40 sweep in <c>TroopWeightCoverageTests</c> no longer covers Isengard, so this test keeps the
    /// 2.0 floor for its level-36 warden and Nazg-hai: a lost row would drop them to 1.0 unseen.
    /// </summary>
    [TestMethod]
    public void ShippedWeights_IsengardTroopsAboveLevel31AndBelow41_WeighTwo()
    {
        string data = CultureDataFixture.ModuleDataPath();
        var weighted = XDocument.Load(Path.Combine(data, "TroopWeights", "troop_weights.xml"))
            .Descendants("TroopWeight")
            .ToDictionary(w => (string)w.Attribute("id"), w => (string)w.Attribute("weight"));
        var elites = XDocument.Load(Path.Combine(data, "troops", "troops_isengard.xml"))
            .Descendants("NPCCharacter")
            .Where(t => t.Attribute("level") != null)
            .Where(t => int.Parse((string)t.Attribute("level")) is > 31 and < 41)
            .Select(t => (string)t.Attribute("id"))
            .ToList();
        Assert.IsTrue(elites.Count >= 2, "no Isengard troop between level 32 and 40 read");

        var wrong = elites.Where(id => !weighted.TryGetValue(id, out var w) || w != "2.0").ToList();

        Assert.AreEqual(0, wrong.Count, "not weighted 2.0: " + string.Join(", ", wrong));
    }
}
