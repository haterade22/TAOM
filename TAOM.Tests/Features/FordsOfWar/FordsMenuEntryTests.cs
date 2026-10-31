using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsMenuEntryTests
{
    private static FordsMenuEntry Entry(FakeFordsMenu menu, string? factions, string? maps, FordsRunReport? report = null)
    {
        var catalog = FordsConfigCatalog.FromTexts(null, factions, maps, Substitute.For<IModLogger>());
        report ??= new FordsRunReport(Substitute.For<IModLogger>());
        var prompt = new FordsSetupPrompt(catalog, menu, Substitute.For<IFordsOfWarGameManagerFactory>(), Substitute.For<IModLogger>(), report);
        return new FordsMenuEntry(menu, catalog, prompt, report);
    }

    [TestMethod]
    public void Install_RecordsInstalledWithContent()
    {
        var report = new FordsRunReport(Substitute.For<IModLogger>());

        Entry(new FakeFordsMenu(), FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, report).Install();

        var evidence = report.Session.Menu;
        Assert.IsTrue(evidence.Installed);
        Assert.IsTrue(evidence.HasContent);
        Assert.IsTrue(evidence.Factions > 0);
        Assert.IsTrue(evidence.Maps > 0);
    }

    [TestMethod]
    public void Install_WhenAddOptionThrows_RecordsNothingInstalled()
    {
        var report = new FordsRunReport(Substitute.For<IModLogger>());
        var menu = Substitute.For<IFordsMenuAdapter>();
        menu.When(m => m.AddOption(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<System.Func<bool>>(), Arg.Any<System.Action>()))
            .Do(_ => throw new System.InvalidOperationException("no menu"));
        var catalog = FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, Substitute.For<IModLogger>());
        var entry = new FordsMenuEntry(menu, catalog, new FordsSetupPrompt(catalog, menu, Substitute.For<IFordsOfWarGameManagerFactory>(), Substitute.For<IModLogger>(), report), report);

        Assert.ThrowsException<System.InvalidOperationException>(() => entry.Install());

        Assert.IsFalse(report.Session.Menu.Installed);
    }

    [TestMethod]
    public void IsHidden_ReportsEachPoll()
    {
        var report = new FordsRunReport(Substitute.For<IModLogger>());
        var menu = new FakeFordsMenu();
        Entry(menu, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, report).Install();

        menu.Options["TaomFordsOfWar"].IsHidden();
        menu.Options["TaomFordsOfWar"].IsHidden();
        menu.Options["TaomFordsOfWar"].IsHidden();

        Assert.AreEqual(3, report.Session.Menu.Polls);
        Assert.AreEqual(false, report.Session.Menu.LastPollHidden);
    }

    [TestMethod]
    public void Install_AddsTheOptionWithItsKeysAndAnOrder()
    {
        var menu = new FakeFordsMenu();

        Entry(menu, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps).Install();

        Assert.AreEqual(1, menu.Options.Count);
        var option = menu.Options["TaomFordsOfWar"];
        Assert.AreEqual("{=TAOM_FOW_MenuOption}Fords of War", option.Label);
        StringAssert.StartsWith(option.Hint, "{=TAOM_FOW_MenuHint}");
        Assert.AreEqual(100, option.Order);
    }

    [TestMethod]
    public void Install_Twice_AddsItOnlyOnce()
    {
        var menu = new FakeFordsMenu();
        var entry = Entry(menu, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps);

        entry.Install();
        entry.Install();

        Assert.AreEqual(1, menu.AddCalls);
    }

    [TestMethod]
    public void Install_AnOptionAlreadyThere_IsLeftAlone()
    {
        var menu = new FakeFordsMenu();
        menu.AddOption("TaomFordsOfWar", "x", "y", 1, () => false, () => { });

        Entry(menu, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps).Install();

        Assert.AreEqual(1, menu.AddCalls);
    }

    [TestMethod]
    public void TheOption_IsHiddenWithoutFactionsOrMaps_AndShownWithBoth()
    {
        var withBoth = new FakeFordsMenu();
        Entry(withBoth, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps).Install();
        var noFactions = new FakeFordsMenu();
        Entry(noFactions, "{ }", FordsConfigFixtures.Maps).Install();
        var noMaps = new FakeFordsMenu();
        Entry(noMaps, FordsConfigFixtures.Factions, "{ }").Install();

        Assert.IsFalse(withBoth.Options["TaomFordsOfWar"].IsHidden());
        Assert.IsTrue(noFactions.Options["TaomFordsOfWar"].IsHidden());
        Assert.IsTrue(noMaps.Options["TaomFordsOfWar"].IsHidden());
    }

    [TestMethod]
    public void ChoosingTheOption_BeginsThePromptFlow()
    {
        var menu = new FakeFordsMenu();
        Entry(menu, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps).Install();

        menu.Options["TaomFordsOfWar"].OnChosen();

        Assert.AreEqual(1, menu.Prompts.Count, "the first prompt is up");
    }
}
