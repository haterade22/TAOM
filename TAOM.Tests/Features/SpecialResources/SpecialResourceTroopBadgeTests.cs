using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The encyclopedia troop-tree badge (#590): which cost rows earn it, and the tooltip it carries.
/// Assertions read <c>Value</c> and <c>Attributes</c>, never <c>ToString()</c>, which needs
/// MBTextManager and a running Module. Every number rides on a slot or a pre-formatted value
/// string, never baked into a default text (#434).
/// </summary>
[TestClass]
public class SpecialResourceTroopBadgeTests
{
    private static SpecialResource Resource(string id, string name) =>
        new SpecialResource(id, new[] { id + "_kingdom" }, new[] { id + "_culture" }, name,
            "SpecialResources\\taom_" + id + "_icon", 10000f, 0f, 0.2f, 14f, 12f, 20f, 2f);

    private static void AssertNoBakedNumber(TextObject text)
    {
        Assert.IsFalse(Regex.IsMatch(text.Value, "[0-9]"),
            $"A number is baked into the default text, so no translation can carry it: {text.Value}");
    }

    // ── IsShown: the badge marks a troop that costs a resource to upgrade, recruit or keep ──

    [TestMethod]
    public void IsShown_UpgradeCostOnly_ReturnsTrue()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_vanguard", "war_spoils", upgradeCost: 2, dailyUpkeep: 0f);

        Assert.IsTrue(SpecialResourceTroopBadge.IsShown(cost));
    }

    [TestMethod]
    public void IsShown_RecruitCostOnly_ReturnsTrue()
    {
        var cost = new TroopResourceCostEntry("taom_spider_creature", "war_spoils", upgradeCost: 0, dailyUpkeep: 0f, recruitCost: 5);

        Assert.IsTrue(SpecialResourceTroopBadge.IsShown(cost));
    }

    [TestMethod]
    public void IsShown_DailyUpkeepOnly_ReturnsTrue()
    {
        var cost = new TroopResourceCostEntry("harad_mumakil_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 500f);

        Assert.IsTrue(SpecialResourceTroopBadge.IsShown(cost));
    }

    [TestMethod]
    public void IsShown_MerchantCostOnly_ReturnsFalse()
    {
        // The shape of all 50 Elite Emissary rows: an ordinary tree troop the emissary sells.
        var cost = new TroopResourceCostEntry("mirkwood_thingolheir", "elven_wine", upgradeCost: 0, dailyUpkeep: 0f, merchantCost: 45);

        Assert.IsFalse(SpecialResourceTroopBadge.IsShown(cost));
    }

    [TestMethod]
    public void IsShown_NullEntry_ReturnsFalse()
    {
        Assert.IsFalse(SpecialResourceTroopBadge.IsShown(null));
    }

    // ── Title ──

    [TestMethod]
    public void Title_BindsResourceNameAsSlot()
    {
        var title = SpecialResourceTroopBadge.Title(Resource("elven_wine", "Elven Wine"));

        StringAssert.Contains(title.Value, "{=taom_res_badge_title}");
        StringAssert.Contains(title.Value, "{RESOURCE}");
        Assert.AreEqual("Elven Wine", title.Attributes["RESOURCE"]);
        AssertNoBakedNumber(title);
    }

    // ── Rows: one per cost field above zero, in a fixed order ──

    [TestMethod]
    public void Rows_UrukCaptain_UpgradeUpkeepAndEmissaryInOrder()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_captain", "war_spoils", upgradeCost: 4, dailyUpkeep: 0.2f, merchantCost: 14);

        var rows = SpecialResourceTroopBadge.Rows(cost);

        Assert.AreEqual(3, rows.Count);
        StringAssert.Contains(rows[0].Key.Value, "{=taom_res_badge_upgrade}");
        Assert.AreEqual("4", rows[0].Value);
        StringAssert.Contains(rows[1].Key.Value, "{=taom_res_badge_upkeep}");
        Assert.AreEqual("0.2", rows[1].Value);
        StringAssert.Contains(rows[2].Key.Value, "{=taom_res_badge_emissary}");
        Assert.AreEqual("14", rows[2].Value);
    }

    [TestMethod]
    public void Rows_ElephantRider_RecruitUpkeepAndEmissaryInOrder()
    {
        var cost = new TroopResourceCostEntry("harad_elephant_rider", "war_drums", upgradeCost: 0, dailyUpkeep: 10f, recruitCost: 50, merchantCost: 70);

        var rows = SpecialResourceTroopBadge.Rows(cost);

        Assert.AreEqual(3, rows.Count);
        StringAssert.Contains(rows[0].Key.Value, "{=taom_res_badge_recruit}");
        Assert.AreEqual("50", rows[0].Value);
        StringAssert.Contains(rows[1].Key.Value, "{=taom_res_badge_upkeep}");
        Assert.AreEqual("10", rows[1].Value);
        StringAssert.Contains(rows[2].Key.Value, "{=taom_res_badge_emissary}");
        Assert.AreEqual("70", rows[2].Value);
    }

    [TestMethod]
    public void Rows_IronpassRam_AllFourInOrderUpgradeRecruitUpkeepEmissary()
    {
        var cost = new TroopResourceCostEntry("ironpass_ram_breaker", "gems", upgradeCost: 2, dailyUpkeep: 0.1f, recruitCost: 10, merchantCost: 10);

        var rows = SpecialResourceTroopBadge.Rows(cost);

        CollectionAssert.AreEqual(
            new List<string> { "{=taom_res_badge_upgrade}", "{=taom_res_badge_recruit}", "{=taom_res_badge_upkeep}", "{=taom_res_badge_emissary}" },
            rows.Select(r => r.Key.Value.Substring(0, r.Key.Value.IndexOf('}') + 1)).ToList());
        CollectionAssert.AreEqual(new List<string> { "2", "10", "0.1", "10" }, rows.Select(r => r.Value).ToList());
    }

    [TestMethod]
    public void Rows_OmitsEveryZeroField()
    {
        var cost = new TroopResourceCostEntry("mordor_uruk_vanguard", "war_spoils", upgradeCost: 2, dailyUpkeep: 0f);

        var rows = SpecialResourceTroopBadge.Rows(cost);

        Assert.AreEqual(1, rows.Count);
        StringAssert.Contains(rows[0].Key.Value, "{=taom_res_badge_upgrade}");
    }

    // ── PaidInNote: named only when the player's resource is not the troop's ──

    [TestMethod]
    public void PaidInNote_PlayerResourceDiffers_NamesThePlayersResource()
    {
        var note = SpecialResourceTroopBadge.PaidInNote(Resource("war_spoils", "War Spoils"), Resource("caster", "Castar"));

        Assert.IsNotNull(note);
        StringAssert.Contains(note.Value, "{=taom_res_badge_paid_in}");
        StringAssert.Contains(note.Value, "{RESOURCE}");
        Assert.AreEqual("Castar", note.Attributes["RESOURCE"]);
        AssertNoBakedNumber(note);
    }

    [TestMethod]
    public void PaidInNote_PlayerResourceSame_ReturnsNull()
    {
        var troop = Resource("war_spoils", "War Spoils");
        var player = Resource("war_spoils", "War Spoils");

        Assert.IsNull(SpecialResourceTroopBadge.PaidInNote(troop, player));
    }

    [TestMethod]
    public void PaidInNote_PlayerResourceNull_ReturnsNull()
    {
        Assert.IsNull(SpecialResourceTroopBadge.PaidInNote(Resource("war_spoils", "War Spoils"), null));
    }

    // ── Slot rule ──

    [TestMethod]
    public void Rows_NoDigitBakedIntoAnyLabel()
    {
        var cost = new TroopResourceCostEntry("ironpass_ram_breaker", "gems", upgradeCost: 2, dailyUpkeep: 0.1f, recruitCost: 10, merchantCost: 10);

        foreach (var row in SpecialResourceTroopBadge.Rows(cost))
            AssertNoBakedNumber(row.Key);
    }
}
