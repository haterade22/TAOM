using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

/// <summary>
/// The ledger is a process singleton: a second campaign in one process must start empty, and a real
/// save must restore exactly what it held (csharp-architecture.md, session-reset rule).
/// </summary>
[TestClass]
public class OathLedgerSessionResetTests
{
    private static OathLedger Fresh() => new(Substitute.For<ICoopSessionProvider>());

    [TestMethod]
    public void SecondCampaign_StartsEmptyAfterReset()
    {
        var ledger = Fresh();
        ledger.Set("campaign_a");
        ledger.SetTopicLastDay("c", "t", 9);

        ledger.ResetForNewSession();

        Assert.IsFalse(ledger.Has("campaign_a"));
        Assert.IsFalse(ledger.TryGetTopicLastDay("c", "t", out _));
        var exported = ledger.Export();
        Assert.AreEqual(0, exported.Flags.Count);
        Assert.AreEqual(0, exported.TopicDays.Count);
    }

    [TestMethod]
    public void ImportAfterReset_RestoresTheSave()
    {
        var ledger = Fresh();
        ledger.Set("saved");
        ledger.SetTopicLastDay("c", "t", 21);
        var save = ledger.Export();

        ledger.ResetForNewSession();
        ledger.Set("leftover_from_other_campaign");
        ledger.Import(save);

        Assert.IsTrue(ledger.Has("saved"));
        Assert.IsFalse(ledger.Has("leftover_from_other_campaign"));
        Assert.IsTrue(ledger.TryGetTopicLastDay("c", "t", out var day));
        Assert.AreEqual(21, day);
    }

    [TestMethod]
    public void IntroRecord_SurvivesExportImport_AndIsClearedByReset()
    {
        var ledger = Fresh();
        ledger.SetTopicLastDay("c", OathTopicService.IntroTopicId, 7);
        var save = ledger.Export();

        ledger.ResetForNewSession();
        Assert.IsFalse(ledger.TryGetTopicLastDay("c", OathTopicService.IntroTopicId, out _));

        ledger.Import(save);
        Assert.IsTrue(ledger.TryGetTopicLastDay("c", OathTopicService.IntroTopicId, out var day));
        Assert.AreEqual(7, day);

        ledger.ResetForNewSession();
        Assert.IsFalse(ledger.TryGetTopicLastDay("c", OathTopicService.IntroTopicId, out _));
    }
}
