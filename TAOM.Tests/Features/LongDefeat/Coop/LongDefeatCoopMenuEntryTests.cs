using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Coop;

[TestClass]
public class LongDefeatCoopMenuEntryTests
{
    private readonly ILongDefeatCoopLink _link = Substitute.For<ILongDefeatCoopLink>();

    private static string Shipped(string file) =>
        File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat", file));

    private (LongDefeatCoopMenuEntry Entry, LongDefeatCoopRuns Runs) Entry(FakeLongDefeatMenu menu, bool dedicated = false)
    {
        var logger = Substitute.For<IModLogger>();
        var catalog = LongDefeatConfigCatalog.FromTexts(Shipped("fortresses.json"), Shipped("waves.json"), Shipped("shop.json"), logger);
        var prompt = new LongDefeatSetupPrompt(menu, Substitute.For<ILongDefeatGameManagerFactory>(), logger, catalog,
            LongDefeatRunRecords.ForFolder(Path.Combine(Path.GetTempPath(), "taom-ld-unused"), null));
        var runs = new LongDefeatCoopRuns(new LongDefeatCoopState(), prompt.Start, menu, logger);
        var server = Substitute.For<IDedicatedServerProvider>();
        server.IsDedicatedServer.Returns(dedicated);
        return (new LongDefeatCoopMenuEntry(menu, catalog, prompt, runs, server), runs);
    }

    [TestMethod]
    public void TheOption_IsHidden_UntilCoopAttaches_AndOnADedicatedServer()
    {
        var menu = new FakeLongDefeatMenu();
        var (entry, runs) = Entry(menu);
        entry.Install();
        entry.Install();

        var option = menu.Options[LongDefeatCoopMenuEntry.OptionId];
        Assert.AreEqual(1, menu.AddCalls);
        Assert.IsTrue(option.IsHidden());

        runs.Attach(_link);
        Assert.IsFalse(option.IsHidden());

        var serverMenu = new FakeLongDefeatMenu();
        var (serverEntry, serverRuns) = Entry(serverMenu, dedicated: true);
        serverEntry.Install();
        serverRuns.Attach(_link);
        Assert.IsTrue(serverMenu.Options[LongDefeatCoopMenuEntry.OptionId].IsHidden());
    }

    [TestMethod]
    public void Host_AsksForTheFortressAndDifficulty_ThenHostsInsteadOfStartingAlone()
    {
        var menu = new FakeLongDefeatMenu(LongDefeatCoopMenuEntry.HostChoice, "helms", "normal");
        var (entry, runs) = Entry(menu);
        runs.Attach(_link);
        entry.Install();

        menu.Options[LongDefeatCoopMenuEntry.OptionId].OnChosen();

        _link.Received(1).Host(Arg.Is<byte[]>(payload => Decodes(payload, "helms")));
    }

    [TestMethod]
    public void Host_AHardRun_CarriesTheDifficultyInThePayload()
    {
        var menu = new FakeLongDefeatMenu(LongDefeatCoopMenuEntry.HostChoice, "helms", "hard");
        var (entry, runs) = Entry(menu);
        runs.Attach(_link);
        entry.Install();

        menu.Options[LongDefeatCoopMenuEntry.OptionId].OnChosen();

        _link.Received(1).Host(Arg.Is<byte[]>(payload => DecodesHard(payload)));
    }

    [TestMethod]
    public void Join_AsksForTheAddress_ThenJoins()
    {
        var menu = new FakeLongDefeatMenu(LongDefeatCoopMenuEntry.JoinChoice);
        menu.TextAnswers.Enqueue("10.0.0.5:4200");
        var (entry, runs) = Entry(menu);
        runs.Attach(_link);
        entry.Install();

        menu.Options[LongDefeatCoopMenuEntry.OptionId].OnChosen();

        _link.Received(1).Join("10.0.0.5", 4200);
    }

    private static bool DecodesHard(byte[] payload) =>
        LongDefeatSessionPayload.TryDecode(payload, out _, out var options) && options!.Difficulty == RunDifficulty.Hard;

    private static bool Decodes(byte[] payload, string fortress) =>
        LongDefeatSessionPayload.TryDecode(payload, out var id, out _) && id == fortress;
}
