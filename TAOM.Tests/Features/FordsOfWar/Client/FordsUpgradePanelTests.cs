using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Client.Features.FordsOfWar;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Features.FordsOfWar.Domain.FordsMatchFixture;

namespace TAOM.Tests.Features.FordsOfWar.Client;

/// <summary>The Upgrades panel: one row per track, what each row says, buying from it, and the key that hides it.</summary>
[TestClass]
public class FordsUpgradePanelTests
{
    private static FordsCommandVM Panel(out FordsMatch match, int gold = 5000)
    {
        match = new FordsMatch(Config(c => c with { Economy = c.Economy with { StartingGold = gold } }), Layout(), Factions(), Slots(), new SeededFordsRandom(7));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), new Vec3f(0f, 25f, 45f), new Vec3f(1000f, 25f, 0f), new List<Vec3f>());
        var vm = new FordsCommandVM(session, () => { });
        vm.Refresh();
        return vm;
    }

    [TestMethod]
    public void Refresh_ShowsOneRowPerTrack_WithItsNextPrice()
    {
        var vm = Panel(out _);

        Assert.IsTrue(vm.IsUpgradesVisible);
        Assert.AreEqual(6, vm.Upgrades.Count);
        Assert.AreEqual("Melee damage", vm.Upgrades[0].Name);
        StringAssert.Contains(vm.Upgrades[0].ActionText, "300");
        Assert.AreEqual("Level 0 of 3", vm.Upgrades[0].StatusText);
        Assert.IsTrue(vm.Upgrades[0].CanBuy);
        Assert.AreEqual("Shift+F6", vm.Upgrades[5].KeyText);
    }

    [TestMethod]
    public void BuyUpgrade_RaisesTheRow_AndCostsTheGold()
    {
        var vm = Panel(out var match);
        var gold = match.GetView(WestSlot).Gold;

        vm.BuyUpgrade(0);

        Assert.AreEqual(gold - 300, match.GetView(WestSlot).Gold);
        StringAssert.Contains(vm.Upgrades[0].StatusText, "+10%");
        StringAssert.Contains(vm.Upgrades[0].ActionText, "600");
    }

    [TestMethod]
    public void AnArmourRow_ShowsLessDamageTaken()
    {
        var vm = Panel(out _);

        vm.BuyUpgrade(2);

        StringAssert.Contains(vm.Upgrades[2].StatusText, "-8%");
    }

    [TestMethod]
    public void ACompleteRow_SaysSo_AndCannotBeBought()
    {
        var vm = Panel(out _);
        for (var i = 0; i < 3; i++)
            vm.BuyUpgrade(0);

        Assert.IsFalse(vm.Upgrades[0].CanBuy);
        StringAssert.Contains(vm.Upgrades[0].ActionText, "Complete");
    }

    [TestMethod]
    public void ARowTooDear_CannotBeBought_AndTheRefusalIsShown()
    {
        var vm = Panel(out var match, gold: 100);

        Assert.IsFalse(vm.Upgrades[0].CanBuy);

        vm.BuyUpgrade(0);

        Assert.AreEqual(100, match.GetView(WestSlot).Gold);
        Assert.AreNotEqual("", vm.NoticeText);
    }

    [TestMethod]
    public void BuyUpgrade_AKeyPastTheLastRow_DoesNothing()
    {
        var vm = Panel(out var match);
        var gold = match.GetView(WestSlot).Gold;

        vm.BuyUpgrade(7);

        Assert.AreEqual(gold, match.GetView(WestSlot).Gold);
    }

    [TestMethod]
    public void Refresh_NamesTheEnemyFaction_ForTheTopStrip()
    {
        var factions = Factions().ToDictionary(e => e.Key, e => e.Value);
        factions[MatchSide.West] = factions[MatchSide.West] with { Name = "{=g}Gondor" };
        factions[MatchSide.East] = factions[MatchSide.East] with { Name = "{=m}Mordor" };
        var match = new FordsMatch(Config(), Layout(), factions, Slots(), new SeededFordsRandom(7));
        var session = new FordsSession();
        session.Fill(new object(), match, WestSlot, Substitute.For<IFordsBattlefieldAdapter>(), new Vec3f(0f, 25f, 45f), new Vec3f(1000f, 25f, 0f), new List<Vec3f>());
        var vm = new FordsCommandVM(session, () => { });

        vm.Refresh();

        Assert.AreEqual("Gondor", vm.FactionName);
        Assert.AreEqual("Mordor", vm.EnemyFactionName);
    }

    [TestMethod]
    public void ToggleUpgrades_HidesAndShowsThePanel()
    {
        var vm = Panel(out _);

        vm.ToggleUpgrades();
        Assert.IsFalse(vm.IsUpgradesVisible);

        vm.ToggleUpgrades();
        Assert.IsTrue(vm.IsUpgradesVisible);
    }

    [TestMethod]
    public void ThePrefab_PutsTheVisibilityOnAWrapper_NotOnTheList()
    {
        var root = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "GUI", "PreFabs", "FordsOfWar", "FordsCommand.xml")).Root!;
        var wrapper = root.Descendants().Single(e => (string?)e.Attribute("IsVisible") == "@IsUpgradesVisible");
        var list = root.Descendants().Single(e => (string?)e.Attribute("DataSource") == "{Upgrades}");

        Assert.IsTrue(list.Ancestors().Contains(wrapper));
        Assert.IsNull(wrapper.Attribute("DataSource"));
    }
}
