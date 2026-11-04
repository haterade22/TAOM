using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatMenuEntryTests
{
    private const string OptionId = "TaomLongDefeat";

    private static string Shipped(string file) =>
        File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", file));

    private static LongDefeatConfigCatalog Catalog(bool withFortress = true) => withFortress
        ? LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), Substitute.For<IModLogger>())
        : LongDefeatConfigCatalog.FromTexts(null, null, null, Substitute.For<IModLogger>());

    private static LongDefeatMenuEntry Entry(FakeLongDefeatMenu menu, RecordingOutcomeLog? log = null, bool dedicated = false, bool withFortress = true)
    {
        var catalog = Catalog(withFortress);
        var prompt = new LongDefeatSetupPrompt(menu, Substitute.For<ILongDefeatGameManagerFactory>(), Substitute.For<IModLogger>(), catalog,
            LongDefeatRunRecords.ForFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "taom-ld-unused"), null));
        var server = Substitute.For<IDedicatedServerProvider>();
        server.IsDedicatedServer.Returns(dedicated);
        return new LongDefeatMenuEntry(menu, catalog, prompt, server, log ?? new RecordingOutcomeLog());
    }

    [TestMethod]
    public void Install_AddsTheOptionWithItsKeysAndAnOrder()
    {
        var menu = new FakeLongDefeatMenu();

        Entry(menu).Install();

        var option = menu.Options[OptionId];
        Assert.AreEqual("{=TAOM_LD_MenuOption}The Long Defeat", option.Label);
        StringAssert.StartsWith(option.Hint, "{=TAOM_LD_MenuHint}");
        Assert.AreEqual(101, option.Order, "after Fords of War (100), well before Multiplayer (9997)");
    }

    [TestMethod]
    public void Install_Twice_AddsItOnlyOnce()
    {
        var menu = new FakeLongDefeatMenu();
        var entry = Entry(menu);

        entry.Install();
        entry.Install();

        Assert.AreEqual(1, menu.AddCalls);
    }

    [TestMethod]
    public void Install_AnOptionAlreadyThere_IsLeftAlone()
    {
        var menu = new FakeLongDefeatMenu();
        menu.AddOption(OptionId, "x", "y", 1, () => false, () => { });

        Entry(menu).Install();

        Assert.AreEqual(1, menu.AddCalls);
    }

    [TestMethod]
    public void TheOption_IsShownWithAFortress_AndHiddenWithNone()
    {
        var shown = new FakeLongDefeatMenu();
        Entry(shown).Install();
        var none = new FakeLongDefeatMenu();
        Entry(none, withFortress: false).Install();

        Assert.IsFalse(shown.Options[OptionId].IsHidden());
        Assert.IsTrue(none.Options[OptionId].IsHidden());
    }

    [TestMethod]
    public void TheOption_IsHiddenOnADedicatedServer_EvenWithAFortress()
    {
        var menu = new FakeLongDefeatMenu();
        Entry(menu, dedicated: true).Install();

        Assert.IsTrue(menu.Options[OptionId].IsHidden());
    }

    [TestMethod]
    public void ChoosingTheOption_BeginsThePrompt()
    {
        var menu = new FakeLongDefeatMenu();
        Entry(menu).Install();

        menu.Options[OptionId].OnChosen();

        Assert.AreEqual(1, menu.Prompts.Count);
    }

    [TestMethod]
    public void TheMenuCheck_PassesOnceTheOptionIsPolledShown_AndNamesTheFortressCount()
    {
        var menu = new FakeLongDefeatMenu();
        var log = new RecordingOutcomeLog();
        Entry(menu, log).Install();
        Assert.IsFalse(log.Has("pass", LongDefeatChecks.MenuId), "nothing is proven until the menu draws the option");

        menu.Options[OptionId].IsHidden();
        menu.Options[OptionId].IsHidden();

        Assert.AreEqual("pass", log.Lines[0].Split(' ')[0]);
        StringAssert.Contains(log.Lines[0], "fortresses=1");
        Assert.AreEqual(1, log.Lines.FindAll(l => l.Contains(LongDefeatChecks.MenuId)).Count, "one line per poll would flood the log");
    }

    [TestMethod]
    public void TheMenuCheck_FailsWhenTheOptionIsHiddenForWantOfAFortress_AndSkipsOnAServer()
    {
        var noFortress = new FakeLongDefeatMenu();
        var failLog = new RecordingOutcomeLog();
        Entry(noFortress, failLog, withFortress: false).Install();
        noFortress.Options[OptionId].IsHidden();

        var server = new FakeLongDefeatMenu();
        var skipLog = new RecordingOutcomeLog();
        Entry(server, skipLog, dedicated: true).Install();
        server.Options[OptionId].IsHidden();

        Assert.IsTrue(failLog.Has("fail", LongDefeatChecks.MenuId));
        Assert.IsTrue(skipLog.Has("skip", LongDefeatChecks.MenuId));
    }
}
