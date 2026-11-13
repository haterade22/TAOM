using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CoopInterop;
using TAOM.Features.SpecialResources;
using TAOM.Features.SpecialResources.Domain;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The daily tick: the single-player branch moved from SpecialResourcesBehavior verbatim, plus the
/// co-op authority's per-player tick (#967 C1). Building the player-facing lines constructs TextObject.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SpecialResourceEarningServiceDailyTests
{
    private static readonly IReadOnlyList<TroopUpkeepInfo> Troops = new[] { new TroopUpkeepInfo("troll", 2) };

    private static DailyTickInput Input(EarnerRef hero = null)
        => new(hero ?? EarningTestKit.Local(), ownedTowns: 2, Troops);

    private static DailyResourceBreakdown Breakdown(float earning, float upkeepPerUnit)
        => new(earning, new[] { new TroopUpkeepLine("troll", 2, upkeepPerUnit) });

    private static void Arrange(EarningTestKit kit, string heroId, DailyResourceBreakdown breakdown, float before, float after)
    {
        kit.Service.GetDailyBreakdown(heroId, "k", "c", 2, Troops).Returns(breakdown);
        kit.Service.GetCurrentAmount(heroId, "k", "c").Returns(before, after);
    }

    // --- Single-player branch ---

    [TestMethod]
    public void OnDailyTick_DedicatedServerWithoutSession_DoesNotProcess()
    {
        // A dedicated server's placeholder MainHero is nobody's hero: with no session it earns nothing,
        // the same rule the battle and raid paths already apply (Codex review 2026-10-21).
        var kit = new EarningTestKit(dedicated: true);
        Arrange(kit, "main", Breakdown(5f, 1f), 10f, 13f);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsFalse(outcome.Processed);
        kit.Service.DidNotReceiveWithAnyArgs().ApplyDailyTick(default, default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_HeroMapsToNoResource_ProcessesWithoutTicking()
    {
        var kit = new EarningTestKit();
        kit.Service.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns((SpecialResource)null);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsTrue(outcome.Processed);
        Assert.IsNull(outcome.DailyLine);
        kit.Service.DidNotReceiveWithAnyArgs().ApplyDailyTick(default, default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_Resource_AppliesTheTickWithTownsAndUpkeep()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(5f, 1f), 10f, 13f);

        kit.Sut.OnDailyTick(Input());

        kit.Service.Received(1).ApplyDailyTick("main", "k", "c", 2, Troops);
    }

    [TestMethod]
    public void OnDailyTick_UpkeepDue_ShowsTheDailyLineAndIsLocal()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(5f, 1f), 10f, 13f);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsNotNull(outcome.DailyLine);
        Assert.IsFalse(outcome.DailyLineYellow);
        Assert.IsTrue(outcome.IsLocal);
        Assert.AreEqual("War Spoils", outcome.ResourceName);
    }

    [TestMethod]
    public void OnDailyTick_NoUpkeepDue_ShowsNoLine()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", new DailyResourceBreakdown(5f, new TroopUpkeepLine[0]), 10f, 15f);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsNull(outcome.DailyLine);
        Assert.IsNull(outcome.Overdraft);
        Assert.IsNull(outcome.Warning);
    }

    [TestMethod]
    public void OnDailyTick_NetNegative_MakesTheLineYellow()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(1f, 2f), 10f, 7f);

        Assert.IsTrue(kit.Sut.OnDailyTick(Input()).DailyLineYellow);
    }

    [TestMethod]
    public void OnDailyTick_BillLargerThanBalance_ReportsTheOverdraft()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 3f), 2f, 0f);

        Assert.IsNotNull(kit.Sut.OnDailyTick(Input()).Overdraft);
    }

    [TestMethod]
    public void OnDailyTick_BalanceCoversTheBill_ReportsNoOverdraft()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 3f), 20f, 14f);

        Assert.IsNull(kit.Sut.OnDailyTick(Input()).Overdraft);
    }

    [TestMethod]
    public void OnDailyTick_NaNNet_ReportsNoOverdraft()
    {
        // Moved gate: before + Net < 0f is false for NaN, so a poisoned breakdown stays silent.
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(float.NaN, 1f), 2f, 2f);

        Assert.IsNull(kit.Sut.OnDailyTick(Input()).Overdraft);
    }

    [TestMethod]
    public void OnDailyTick_EmptyBalanceInGrace_SparesTheTroopsAndSaysSo()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 1f), 0f, 0f);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsTrue(outcome.GraceLogged);
        Assert.AreEqual(0, outcome.Desertions.Count);
        kit.Service.DidNotReceiveWithAnyArgs().CalculateDesertion(default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_EmptyBalanceAfterGrace_DesertsTheCalculatedTroops()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 1f), 0f, 0f);
        var deserters = new[] { new TroopDesertionEntry("troll", 1) };
        kit.Service.CalculateDesertion("main", "k", "c", Troops).Returns(deserters);
        kit.Sut.OnDailyTick(Input());

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.AreEqual(1, outcome.Desertions.Count);
        Assert.AreEqual("troll", outcome.Desertions[0].TroopId);
        Assert.IsFalse(outcome.GraceLogged);
    }

    [TestMethod]
    public void OnDailyTick_FirstTickOfACampaign_SparesDesertionAndTheSecondDeserts()
    {
        // Single-player grace (#133 P2): one tick per service instance, which is one per campaign.
        var kit = new EarningTestKit();
        kit.Service.GetDailyBreakdown("main", "k", "c", 2, Troops).Returns(Breakdown(0f, 1f));
        kit.Service.GetCurrentAmount("main", "k", "c").Returns(0f);
        var deserters = new[] { new TroopDesertionEntry("troll", 1) };
        kit.Service.CalculateDesertion("main", "k", "c", Troops).Returns(deserters);

        var first = kit.Sut.OnDailyTick(Input());
        var second = kit.Sut.OnDailyTick(Input());

        Assert.IsTrue(first.GraceLogged);
        Assert.AreEqual(0, first.Desertions.Count);
        Assert.IsFalse(second.GraceLogged);
        Assert.AreEqual(1, second.Desertions.Count);
    }

    [TestMethod]
    public void OnDailyTick_NoResourceFirstTick_StillSpendsTheGrace()
    {
        // The old flag was cleared on a no-resource tick too, so a kingdom change mid-session could not
        // keep it set forever.
        var kit = new EarningTestKit();
        kit.Service.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns((SpecialResource)null);
        kit.Sut.OnDailyTick(Input());
        kit.Service.ResolveResource(Arg.Any<string>(), Arg.Any<string>()).Returns(EarningTestKit.Resource);
        Arrange(kit, "main", Breakdown(0f, 1f), 0f, 0f);
        var deserters = new[] { new TroopDesertionEntry("troll", 1) };
        kit.Service.CalculateDesertion("main", "k", "c", Troops).Returns(deserters);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsFalse(outcome.GraceLogged);
        Assert.AreEqual(1, outcome.Desertions.Count);
    }

    [TestMethod]
    public void OnDailyTick_NotProcessedTick_DoesNotSpendTheGrace()
    {
        // A session tick for a hero this process may not tick returns before anything is processed.
        var kit = new EarningTestKit(session: true);
        var skipped = kit.Sut.OnDailyTick(Input(EarningTestKit.Local(control: PlayerControl.NotPlayer)));
        kit.Session.IsSessionActive.Returns(false);
        Arrange(kit, "main", Breakdown(0f, 1f), 0f, 0f);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.IsFalse(skipped.Processed);
        Assert.IsTrue(outcome.GraceLogged);
    }

    [TestMethod]
    public void OnDailyTick_EmptyBalanceButNoUpkeepDue_DesertsNobody()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", new DailyResourceBreakdown(0f, new TroopUpkeepLine[0]), 0f, 0f);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.AreEqual(0, outcome.Desertions.Count);
        kit.Service.DidNotReceiveWithAnyArgs().CalculateDesertion(default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_NaNBalance_DesertsNobodyAndWarnsNothing()
    {
        // Moved gates: balance <= 0f and balance > 0f are both false for NaN, so a poisoned balance
        // neither deserts nor warns. Pinned as parity, not endorsed.
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 1f), 5f, float.NaN);

        var outcome = kit.Sut.OnDailyTick(Input());

        Assert.AreEqual(0, outcome.Desertions.Count);
        Assert.IsNull(outcome.Warning);
        kit.Service.DidNotReceiveWithAnyArgs().CalculateDesertion(default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_PositiveBalanceHeadingForZero_WarnsOfTheDeficit()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 3f), 11f, 5f);

        var outcome = kit.Sut.OnDailyTick(Input());

        StringAssert.Contains(outcome.Warning, "War Spoils running out: 5 left, losing 6/day");
    }

    [TestMethod]
    public void OnDailyTick_PositiveBalanceWithRoomToSpare_DoesNotWarn()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(0f, 3f), 20f, 14f);

        Assert.IsNull(kit.Sut.OnDailyTick(Input()).Warning);
    }

    [TestMethod]
    public void OnDailyTick_PositiveBalanceStable_DoesNotWarn()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(10f, 1f), 20f, 28f);

        Assert.IsNull(kit.Sut.OnDailyTick(Input()).Warning);
    }

    // --- Co-op authority ---

    [TestMethod]
    public void OnDailyTick_SessionRemotePlayer_SeedsThenTicksWithoutALocalMessage()
    {
        var kit = new EarningTestKit(session: true);
        var guest = EarningTestKit.Remote("guest_1");
        Arrange(kit, "guest_1", Breakdown(5f, 1f), 10f, 13f);

        var outcome = kit.Sut.OnDailyTick(Input(guest));

        Assert.IsTrue(outcome.Processed);
        Assert.IsFalse(outcome.IsLocal);
        kit.Storage.Received(1).Contains("guest_1", "war_spoils");
        kit.Service.Received(1).ApplyDailyTick("guest_1", "k", "c", 2, Troops);
    }

    [TestMethod]
    public void OnDailyTick_SessionUntrackedPlayer_GetsTheStartingBalanceBeforeTheTick()
    {
        var kit = new EarningTestKit(session: true);
        Arrange(kit, "guest_1", Breakdown(5f, 1f), 10f, 13f);
        kit.Storage.Contains("guest_1", "war_spoils").Returns(false);

        kit.Sut.OnDailyTick(Input(EarningTestKit.Remote("guest_1")));

        Received.InOrder(() =>
        {
            kit.Storage.Set("guest_1", "war_spoils", 10f);
            kit.Service.ApplyDailyTick("guest_1", "k", "c", 2, Troops);
        });
    }

    [TestMethod]
    public void OnDailyTick_SessionEmptyBalanceAfterGrace_DesertsNobody()
    {
        // Q1: remote desertion stays off until the balance mirror and notices land (C4, C6).
        var kit = new EarningTestKit(session: true);
        Arrange(kit, "guest_1", Breakdown(0f, 1f), 0f, 0f);

        kit.Sut.OnDailyTick(Input(EarningTestKit.Remote("guest_1")));

        var outcome = kit.Sut.OnDailyTick(Input(EarningTestKit.Remote("guest_1")));

        Assert.AreEqual(0, outcome.Desertions.Count);
        kit.Service.DidNotReceiveWithAnyArgs().CalculateDesertion(default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_SessionUpkeep_LogsTheTickedHeroId()
    {
        var kit = new EarningTestKit(session: true);
        Arrange(kit, "guest_1", Breakdown(5f, 1f), 10f, 13f);

        kit.Sut.OnDailyTick(Input(EarningTestKit.Remote("guest_1")));

        kit.Logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("guest_1") && m.Contains("DailyTick")));
    }

    [TestMethod]
    public void OnDailyTick_SinglePlayerUpkeep_AddsNoLogLine()
    {
        var kit = new EarningTestKit();
        Arrange(kit, "main", Breakdown(5f, 1f), 10f, 13f);

        kit.Sut.OnDailyTick(Input());

        kit.Logger.DidNotReceiveWithAnyArgs().LogInfo(default);
    }

    [TestMethod]
    public void OnDailyTick_SessionHostMainHero_IsNotProcessed()
    {
        var kit = new EarningTestKit(session: true);

        var outcome = kit.Sut.OnDailyTick(Input(EarningTestKit.Local(control: PlayerControl.NotPlayer)));

        Assert.IsFalse(outcome.Processed);
        kit.Service.DidNotReceiveWithAnyArgs().ApplyDailyTick(default, default, default, default, default);
    }

    [DataTestMethod]
    [DataRow(PlayerControl.NotPlayer)]
    [DataRow(PlayerControl.Unknown)]
    public void OnDailyTick_SessionNonPlayerHero_IsNotProcessed(PlayerControl control)
    {
        var kit = new EarningTestKit(session: true);

        var outcome = kit.Sut.OnDailyTick(Input(EarningTestKit.Remote("lord_1", control)));

        Assert.IsFalse(outcome.Processed);
        kit.Service.DidNotReceiveWithAnyArgs().ApplyDailyTick(default, default, default, default, default);
    }

    [TestMethod]
    public void OnDailyTick_SessionClient_WritesNothing()
    {
        var kit = new EarningTestKit(session: true, authority: false);

        var outcome = kit.Sut.OnDailyTick(Input(EarningTestKit.Remote("guest_1")));

        Assert.IsFalse(outcome.Processed);
        kit.Service.DidNotReceiveWithAnyArgs().ApplyDailyTick(default, default, default, default, default);
        kit.Storage.DidNotReceiveWithAnyArgs().Set(default, default, default);
    }
}
