using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Features.WarPledge;

namespace TAOM.Tests.Features.WarPledge;

/// <summary>
/// #1113: the join action and the AI's daily courting step. See <see cref="WarPledgeFixture"/> for what is
/// real (the alignment, diplomacy and War of the Ring services) and what is a fake (the engine boundary).
/// </summary>
[TestClass]
public class WarPledgeServiceTests
{
    private WarPledgeFixture _f = null!;

    [TestInitialize]
    public void SetUp() => _f = new WarPledgeFixture();

    private static string Key(string a, string b) => FakeAllianceAdapter.Key(a, b);

    // ---- the join ----

    [TestMethod]
    public void TryJoin_NeutralRealmInFullWar_PledgesAndDeclaresWarOnEveryRealmOfTheOtherSide()
    {
        var sut = _f.Build();

        var joined = sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out var reason);

        Assert.IsTrue(joined, reason);
        Assert.AreEqual(FactionSide.Free, _f.Alignment.GetKingdomSide("cardolan"));
        Assert.IsTrue(_f.Alliance.Wars.Contains(Key("cardolan", "empire_s")));
        Assert.IsTrue(_f.Alliance.Wars.Contains(Key("cardolan", "isengard")));
        Assert.IsFalse(_f.Alliance.Wars.Contains(Key("cardolan", "empire_w")), "no war with its own side");
        Assert.IsFalse(_f.Alliance.Wars.Contains(Key("cardolan", "rhudaur")), "no war with a Neutral realm");
    }

    [TestMethod]
    public void TryJoin_Joined_TheNewWarsCannotEndInPeace()
    {
        var sut = _f.Build();
        Assert.IsFalse(_f.Wotr.ShouldBlockPeace("cardolan", "empire_s"), "precondition: no lock before the pledge");

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        Assert.IsTrue(_f.Wotr.ShouldBlockPeace("cardolan", "empire_s"));
        Assert.IsTrue(_f.Wotr.ShouldBlockPeace("isengard", "cardolan"));
        Assert.IsFalse(_f.Wotr.ShouldBlockPeace("cardolan", "empire_w"));
        Assert.IsFalse(_f.Wotr.ShouldBlockPeace("cardolan", "rhudaur"));
    }

    [TestMethod]
    public void TryJoin_AtWarWithRealmsOfItsNewSide_MakesPeaceWithThemBeforeAnyWarIsDeclared()
    {
        var sut = _f.Build();
        _f.Alliance.Wars.Add(Key("cardolan", "empire_w"));
        _f.Alliance.Wars.Add(Key("cardolan", "lindon"));

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        Assert.IsFalse(_f.Alliance.Wars.Contains(Key("cardolan", "empire_w")));
        Assert.IsFalse(_f.Alliance.Wars.Contains(Key("cardolan", "lindon")));
        var firstWar = _f.Alliance.Calls.FindIndex(c => c.StartsWith("war ", StringComparison.Ordinal));
        var lastPeace = _f.Alliance.Calls.FindLastIndex(c => c.StartsWith("peace ", StringComparison.Ordinal));
        Assert.IsTrue(lastPeace >= 0 && firstWar > lastPeace, string.Join(", ", _f.Alliance.Calls));
        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=2 expected=2 peace=2 stuck=0");
    }

    [TestMethod]
    public void TryJoin_AtWarWithANeutralRealm_KeepsThatWar()
    {
        var sut = _f.Build();
        _f.Alliance.Wars.Add(Key("cardolan", "rhudaur"));

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        Assert.IsTrue(_f.Alliance.Wars.Contains(Key("cardolan", "rhudaur")));
        CollectionAssert.DoesNotContain(_f.Alliance.Calls, "peace " + Key("cardolan", "rhudaur"));
    }

    [TestMethod]
    public void TryJoin_Joined_WritesTheLedgerLineTheNoticeAndThePassLine()
    {
        var sut = _f.Build();

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out var reason);

        _f.LedgerLogger.Received(1).LogInfo("[WarLedger] v=1 t=event cid=c1 day=80 ev=pledge k=cardolan side=free by=ai");
        _f.World.Received(1).AnnouncePledge("cardolan", FactionSide.Free);
        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=2 expected=2 peace=0 stuck=0");
        _f.Outcomes.DidNotReceiveWithAnyArgs().Fail(default!, default!, default!);
        StringAssert.Contains(reason, "cardolan pledged to the Free side");
    }

    [TestMethod]
    public void TryJoin_ByTheConsole_SaysSoInTheLedgerAndTheCheckLine()
    {
        var sut = _f.Build();

        sut.TryJoin("rhudaur", FactionSide.Evil, byAi: false, dryRun: false, out _);

        _f.LedgerLogger.Received(1).LogInfo("[WarLedger] v=1 t=event cid=c1 day=80 ev=pledge k=rhudaur side=evil by=console");
        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "rhudaur",
            "side=evil by=console wars=3 expected=3 peace=0 stuck=0");
    }

    [TestMethod]
    public void TryJoin_AWarTheEngineRefuses_WritesAFailLine()
    {
        var sut = _f.Build();
        _f.Alliance.RefusedWar = Key("cardolan", "isengard");

        var joined = sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out var reason);

        Assert.IsTrue(joined, "the pledge stands: it is saved state, and the next session's reconcile retries the war");
        Assert.IsTrue(_f.Alliance.AreAtWar("cardolan", "isengard"), "the model holds the pair at war");
        Assert.IsFalse(_f.Alliance.HasDeclaredWar("cardolan", "isengard"), "the stance does not: the count reads the stance");
        StringAssert.Contains(reason, "Not declared: isengard");
        _f.Outcomes.Received(1).Fail(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=1 expected=2 peace=0 stuck=0");
        _f.Outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
    }

    [TestMethod]
    public void TryJoin_APeaceTheEngineRefuses_WritesAFailLine()
    {
        var sut = _f.Build();
        _f.Alliance.Wars.Add(Key("cardolan", "empire_w"));
        _f.Alliance.RefusedPeace = Key("cardolan", "empire_w");

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        _f.Outcomes.Received(1).Fail(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=2 expected=2 peace=0 stuck=1");
    }

    [TestMethod]
    public void TryJoin_TheNoticeThrows_StillJoinsAndCountsTheSwallow()
    {
        var sut = _f.Build();
        var boom = new InvalidOperationException("no UI");
        _f.World.When(w => w.AnnouncePledge(Arg.Any<string>(), Arg.Any<FactionSide>())).Do(_ => throw boom);

        var joined = sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        Assert.IsTrue(joined);
        Assert.IsTrue(_f.Store.IsPledged("cardolan"));
        _f.Outcomes.Received(1).Swallowed("WarPledge", "announce", boom);
        _f.Outcomes.ReceivedWithAnyArgs(1).Pass(default!, default!, default!);
    }

    [TestMethod]
    public void TryJoin_PermanentRowWithARealmOfTheOtherSide_DeclaresNoWarThere()
    {
        _f.Relationships.Add(new KingdomRelationship { KingdomA = "cardolan", KingdomB = "isengard", Tier = AllianceTier.Permanent });
        var sut = _f.Build();

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        Assert.IsTrue(_f.Alliance.Wars.Contains(Key("cardolan", "empire_s")));
        Assert.IsFalse(_f.Alliance.Wars.Contains(Key("cardolan", "isengard")));
        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=1 expected=1 peace=0 stuck=0");
    }

    [TestMethod]
    public void TryJoin_DryRun_ChangesNothingAndSaysWhatWouldHappen()
    {
        var sut = _f.Build();
        _f.Alliance.Wars.Add(Key("cardolan", "empire_w"));

        var would = sut.TryJoin("cardolan", FactionSide.Free, byAi: false, dryRun: true, out var reason);

        Assert.IsTrue(would, reason);
        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(0, _f.Alliance.Calls.Count);
        _f.World.DidNotReceiveWithAnyArgs().AnnouncePledge(default!, default);
        _f.LedgerLogger.DidNotReceiveWithAnyArgs().LogInfo(default!);
        _f.Outcomes.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
        StringAssert.Contains(reason, "would pledge");
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    [DataRow(WarPhase.WarEnded)]
    public void TryJoin_NotFullWar_IsRefused(WarPhase phase)
    {
        var sut = _f.Build(phase);

        Assert.IsFalse(sut.TryJoin("cardolan", FactionSide.Free, byAi: false, dryRun: false, out var reason));
        StringAssert.Contains(reason, "Full War");
        AssertNothingChanged();
    }

    [TestMethod]
    public void TryJoin_WarOfTheRingOff_IsRefused()
    {
        var sut = _f.Build();
        _f.WotrSettings.WarOfTheRingEnabled.Returns(false);

        Assert.IsFalse(sut.TryJoin("cardolan", FactionSide.Free, byAi: false, dryRun: false, out var reason));
        StringAssert.Contains(reason, "Full War");
        AssertNothingChanged();
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("  ")]
    public void TryJoin_BlankId_IsRefused(string id)
    {
        var sut = _f.Build();

        Assert.IsFalse(sut.TryJoin(id, FactionSide.Free, byAi: false, dryRun: false, out _));
        AssertNothingChanged();
    }

    [TestMethod]
    public void TryJoin_NeutralSide_IsRefused()
    {
        var sut = _f.Build();

        Assert.IsFalse(sut.TryJoin("cardolan", FactionSide.Neutral, byAi: false, dryRun: false, out _));
        AssertNothingChanged();
    }

    // An eliminated kingdom is not in the adapter's list either.
    [DataTestMethod]
    [DataRow("gondolin")]
    [DataRow("Cardolan")]
    public void TryJoin_NoLiveKingdomHasTheId_IsRefused(string id)
    {
        var sut = _f.Build();

        Assert.IsFalse(sut.TryJoin(id, FactionSide.Free, byAi: false, dryRun: false, out var reason));
        StringAssert.Contains(reason, "no live kingdom");
        AssertNothingChanged();
    }

    [TestMethod]
    public void TryJoin_AlreadyPledged_IsRefusedAndKeepsTheFirstSide()
    {
        var sut = _f.Build();
        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);
        _f.Alliance.Calls.Clear();

        Assert.IsFalse(sut.TryJoin("cardolan", FactionSide.Evil, byAi: false, dryRun: false, out var reason));

        StringAssert.Contains(reason, "already pledged");
        Assert.AreEqual(FactionSide.Free, _f.Alignment.GetKingdomSide("cardolan"));
        Assert.AreEqual(0, _f.Alliance.Calls.Count);
    }

    [DataTestMethod]
    [DataRow("empire_w", FactionSide.Evil)]
    [DataRow("empire_s", FactionSide.Evil)]
    public void TryJoin_NativeMemberOfASide_IsRefused(string id, FactionSide side)
    {
        var sut = _f.Build();

        Assert.IsFalse(sut.TryJoin(id, side, byAi: false, dryRun: false, out var reason));
        StringAssert.Contains(reason, "is a member of");
        AssertNothingChanged();
    }

    // Umbar is Hostile to Gondor in diplomacy.json: it cannot stand beside it.
    [TestMethod]
    public void TryJoin_HostileRowWithALiveRealmOfThatSide_IsRefused()
    {
        var sut = _f.Build();

        Assert.IsFalse(sut.TryJoin("umbar", FactionSide.Free, byAi: false, dryRun: false, out var reason));
        StringAssert.Contains(reason, "empire_w");
        AssertNothingChanged();
    }

    [TestMethod]
    public void TryJoin_HostileRowWithARealmOfTheOtherSide_IsAllowed()
    {
        var sut = _f.Build();

        Assert.IsTrue(sut.TryJoin("umbar", FactionSide.Evil, byAi: false, dryRun: false, out var reason), reason);
        Assert.IsTrue(_f.Alliance.Wars.Contains(Key("umbar", "empire_w")));
    }

    // The guard reads the realms the side holds now: once Gondor is gone, nothing stands between them.
    [TestMethod]
    public void TryJoin_HostileRowWithAnEliminatedRealmOfThatSide_IsAllowed()
    {
        var sut = _f.Build();
        _f.Alliance.Kingdoms.Remove("empire_w");

        Assert.IsTrue(sut.TryJoin("umbar", FactionSide.Free, byAi: false, dryRun: false, out var reason), reason);
    }

    // The guard also reads a realm that is on the side by its pledge, not only by the table.
    [TestMethod]
    public void TryJoin_HostileRowWithARealmThatPledgedToThatSide_IsRefused()
    {
        _f.Relationships.Add(new KingdomRelationship { KingdomA = "dorwinion", KingdomB = "cardolan", Tier = AllianceTier.Hostile });
        var sut = _f.Build();
        Assert.IsTrue(sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out var first), first);

        Assert.IsFalse(sut.TryJoin("dorwinion", FactionSide.Free, byAi: false, dryRun: false, out var reason));

        StringAssert.Contains(reason, "cardolan");
        Assert.IsFalse(_f.Store.IsPledged("dorwinion"));
    }

    // A kingdom that alignment.json does not list (a player-founded realm) has a side through its culture for
    // momentum, the ledger and the map. It cannot pledge to either side: it is not a Neutral realm.
    [DataTestMethod]
    [DataRow(FactionSide.Evil)]
    [DataRow(FactionSide.Free)]
    public void TryJoin_KingdomThatHasASideThroughItsCulture_IsRefused(FactionSide side)
    {
        _f.Alignments["gondor"] = "free";
        _f.Alliance.Kingdoms.Add("playerrealm");
        _f.Alliance.Cultures["playerrealm"] = "gondor";
        var sut = _f.Build();

        Assert.IsFalse(sut.TryJoin("playerrealm", side, byAi: false, dryRun: false, out var reason));

        StringAssert.Contains(reason, "culture");
        AssertNothingChanged();
    }

    // The Full War rules read kingdom ids only (DiplomacyService.IsWarAllowed), so a player-founded realm is
    // never forced into those wars. A pledged realm follows the same rule: no war and no peace with it.
    [TestMethod]
    public void TryJoin_Joined_MakesNoWarAndNoPeaceWithAKingdomSidedOnlyByItsCulture()
    {
        _f.Alignments["gondor"] = "free";
        _f.Alignments["mordor"] = "evil";
        _f.Alliance.Kingdoms.Add("freeplayer");
        _f.Alliance.Cultures["freeplayer"] = "gondor";
        _f.Alliance.Kingdoms.Add("evilplayer");
        _f.Alliance.Cultures["evilplayer"] = "mordor";
        var sut = _f.Build();
        _f.Alliance.Wars.Add(Key("cardolan", "freeplayer"));

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        Assert.IsTrue(_f.Alliance.Wars.Contains(Key("cardolan", "freeplayer")), "the player's war is the player's own");
        Assert.IsFalse(_f.Alliance.Wars.Contains(Key("cardolan", "evilplayer")));
        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=2 expected=2 peace=0 stuck=0");
    }

    // Vanilla charges a broken alliance to the ruler of the kingdom that declares (v1.5.7
    // AllianceCampaignBehavior.ApplyBrokenAlliancePenalty reads the first faction). The realm that pledged
    // is the one that broke it, so it declares each of its wars itself. Gondor sorts before Umbar in the
    // kingdom list, which is the order the Full War script would declare in.
    [TestMethod]
    public void TryJoin_DeclaresEachWarItself_SoABrokenAllianceIsChargedToThePledgedRealm()
    {
        var sut = _f.Build();

        sut.TryJoin("umbar", FactionSide.Evil, byAi: true, dryRun: false, out _);

        foreach (var free in new[] { "empire_w", "vlandia", "lindon" })
            Assert.AreEqual("umbar", _f.Alliance.Declarers[Key("umbar", free)], free);
    }

    // The join declares the wars the model holds for the pledged realm, and no other realm's.
    [TestMethod]
    public void TryJoin_DeclaresOnlyThePledgedRealmsWars()
    {
        var sut = _f.Build();

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        CollectionAssert.AreEquivalent(
            new[] { "war " + Key("cardolan", "empire_s"), "war " + Key("cardolan", "isengard") }, _f.Alliance.Calls);
    }

    // Once the pledge is stored the model holds the realm at war with the other side, so the engine's
    // IsAtWarWith is true before any declaration. The join reads the stored stance (HasDeclaredWar): a guard
    // on IsAtWarWith would declare nothing.
    [TestMethod]
    public void TryJoin_TheModelAlreadyHoldsThePairAtWar_StillDeclaresTheWar()
    {
        var sut = _f.Build();

        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);

        CollectionAssert.Contains(_f.Alliance.Calls, "war " + Key("cardolan", "empire_s"));
        Assert.IsTrue(_f.Alliance.HasDeclaredWar("cardolan", "empire_s"));
        Assert.IsTrue(_f.Alliance.HasDeclaredWar("cardolan", "isengard"));
    }

    // With blockPeaceBetweenHostileTiers off the War of the Ring holds no pair at war, so a join owes none.
    [TestMethod]
    public void TryJoin_BlockPeaceSwitchedOff_OwesNoWar()
    {
        _f.BlockPeace = false;
        var sut = _f.Build();

        Assert.IsTrue(sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out var reason), reason);

        Assert.AreEqual(FactionSide.Free, _f.Alignment.GetKingdomSide("cardolan"));
        Assert.AreEqual(0, _f.Alliance.Calls.Count);
        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=0 expected=0 peace=0 stuck=0");
    }

    // The console is the player's own act: the MCM switch and the player-ruled rule bind the AI only.
    [TestMethod]
    public void TryJoin_SwitchOffAndAPlayerRuledRealm_StillJoins()
    {
        var sut = _f.Build();
        _f.Settings.Enabled.Returns(false);
        _f.World.IsPlayerRuled("cardolan").Returns(true);

        Assert.IsTrue(sut.TryJoin("cardolan", FactionSide.Free, byAi: false, dryRun: false, out var reason), reason);
        Assert.IsTrue(_f.Store.IsPledged("cardolan"));
    }

    // No realm of the other side is alive: the join owes no war, and the line says so with wars=0.
    [TestMethod]
    public void TryJoin_NoLiveRealmOfTheOtherSide_JoinsWithNoWar()
    {
        var sut = _f.Build();
        _f.Alliance.Kingdoms.Remove("empire_s");
        _f.Alliance.Kingdoms.Remove("isengard");

        Assert.IsTrue(sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out var reason), reason);

        _f.Outcomes.Received(1).Pass(WarPledgeService.JoinedCheckId, "cardolan",
            "side=free by=ai wars=0 expected=0 peace=0 stuck=0");
        Assert.AreEqual(0, _f.Alliance.Calls.Count);
    }

    private void AssertNothingChanged()
    {
        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(0, _f.Alliance.Calls.Count);
        _f.World.DidNotReceiveWithAnyArgs().AnnouncePledge(default!, default);
        _f.LedgerLogger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    // ---- the AI's daily courting ----

    private WarPledgeService BuildCourting(int balance, int nextCourtingDay = 50)
    {
        var sut = _f.Build();
        _f.Momentum.SliderValue.Returns(balance);
        _f.Store.NextCourtingDay = nextCourtingDay;
        return sut;
    }

    [TestMethod]
    public void RunDailyCourting_FirstFullWarDay_SetsTheAnchorAndCourtsNobody()
    {
        var sut = BuildCourting(balance: -80, nextCourtingDay: WarPledgeStore.NoCourtingDay);

        sut.RunDailyCourting(44);

        Assert.AreEqual(74, _f.Store.NextCourtingDay);
        Assert.AreEqual(0, _f.Store.Count);
    }

    [TestMethod]
    public void RunDailyCourting_BeforeTheCourtingDay_CourtsNobody()
    {
        var sut = BuildCourting(balance: -80, nextCourtingDay: 74);

        sut.RunDailyCourting(73);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(74, _f.Store.NextCourtingDay);
    }

    [TestMethod]
    public void RunDailyCourting_FreeFarBehind_PledgesTheFirstRealmThatLeansFree()
    {
        var sut = BuildCourting(balance: -40);

        sut.RunDailyCourting(50);

        CollectionAssert.AreEqual(new[] { "cardolan" }, _f.Store.Snapshot().Select(p => p.Key).ToList());
        Assert.AreEqual(FactionSide.Free, _f.Alignment.GetKingdomSide("cardolan"));
        _f.LedgerLogger.Received(1).LogInfo(Arg.Is<string>(s => s.EndsWith("ev=pledge k=cardolan side=free by=ai")));
    }

    [TestMethod]
    public void RunDailyCourting_EvilFarBehind_PledgesTheFirstRealmThatLeansEvil()
    {
        var sut = BuildCourting(balance: 40);

        sut.RunDailyCourting(50);

        CollectionAssert.AreEqual(new[] { "rhudaur" }, _f.Store.Snapshot().Select(p => p.Key).ToList());
        Assert.AreEqual(FactionSide.Evil, _f.Alignment.GetKingdomSide("rhudaur"));
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(24)]
    [DataRow(-24)]
    public void RunDailyCourting_BalanceInsideTheBand_CourtsNobody(int balance)
    {
        var sut = BuildCourting(balance);

        sut.RunDailyCourting(50);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(50, _f.Store.NextCourtingDay, "no join, so the wait does not start again");
    }

    [DataTestMethod]
    [DataRow(25, "rhudaur")]
    [DataRow(-25, "cardolan")]
    public void RunDailyCourting_BalanceExactlyAtTheThreshold_Courts(int balance, string expected)
    {
        var sut = BuildCourting(balance);

        sut.RunDailyCourting(50);

        Assert.IsTrue(_f.Store.IsPledged(expected));
    }

    [TestMethod]
    public void RunDailyCourting_AfterAJoin_WaitsTheConfiguredDaysThenTakesTheNextRealm()
    {
        var sut = BuildCourting(balance: -40);

        sut.RunDailyCourting(50);
        Assert.AreEqual(80, _f.Store.NextCourtingDay);
        sut.RunDailyCourting(79);
        Assert.AreEqual(1, _f.Store.Count, "day 79 is inside the wait");

        sut.RunDailyCourting(80);

        CollectionAssert.AreEqual(new[] { "cardolan", "dorwinion" }, _f.Store.Snapshot().Select(p => p.Key).ToList());
        Assert.AreEqual(110, _f.Store.NextCourtingDay);
    }

    // A candidate that the checks refuse stores no pledge, so it must not start the wait either: the side
    // tries again tomorrow.
    [TestMethod]
    public void RunDailyCourting_EveryCandidateIsRefused_DoesNotStartTheWait()
    {
        _f.Leans = new List<WarPledgeLean> { new("umbar", FactionSide.Free) };
        var sut = BuildCourting(balance: -40);

        sut.RunDailyCourting(50);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(50, _f.Store.NextCourtingDay);
    }

    // The two numbers come from war_pledge.json, not from the code.
    [TestMethod]
    public void RunDailyCourting_OtherThresholdAndWaitInTheConfig_AreTheOnesUsed()
    {
        _f.LosingBalance = 40;
        _f.DaysBetweenAiJoins = 10;
        var sut = BuildCourting(balance: -39);

        sut.RunDailyCourting(50);
        Assert.AreEqual(0, _f.Store.Count, "39 is inside a band of 40");

        _f.Momentum.SliderValue.Returns(-40);
        sut.RunDailyCourting(51);

        Assert.IsTrue(_f.Store.IsPledged("cardolan"));
        Assert.AreEqual(61, _f.Store.NextCourtingDay);
    }

    // "At most one realm per 30 days" must hold on the failure path too. A campaign listener that throws
    // inside the peace or a declaration leaves the pledge stored; without the wait, the AI would take the
    // next realm of that side on the next day, and one more on every day the throw repeats.
    [TestMethod]
    public void RunDailyCourting_TheJoinThrowsAfterThePledgeIsStored_StillStartsTheWait()
    {
        var sut = BuildCourting(balance: -40);
        _f.Alliance.Wars.Add(Key("cardolan", "empire_w"));
        _f.Alliance.PeaceThrows = new InvalidOperationException("a listener threw");

        Assert.ThrowsException<InvalidOperationException>(() => sut.RunDailyCourting(50));

        Assert.IsTrue(_f.Store.IsPledged("cardolan"), "the pledge is written before the peace");
        Assert.AreEqual(80, _f.Store.NextCourtingDay);

        _f.Alliance.PeaceThrows = null;
        sut.RunDailyCourting(51);
        Assert.AreEqual(1, _f.Store.Count, "day 51 is inside the wait");
    }

    // A save made in the first half of a day reruns that day's tick after a load.
    [TestMethod]
    public void RunDailyCourting_TheSameDayTwice_JoinsOneRealm()
    {
        var sut = BuildCourting(balance: -40);

        sut.RunDailyCourting(50);
        sut.RunDailyCourting(50);

        Assert.AreEqual(1, _f.Store.Count);
    }

    [TestMethod]
    public void RunDailyCourting_PlayerRuledRealm_IsSkipped()
    {
        var sut = BuildCourting(balance: -40);
        _f.World.IsPlayerRuled("cardolan").Returns(true);

        sut.RunDailyCourting(50);

        CollectionAssert.AreEqual(new[] { "dorwinion" }, _f.Store.Snapshot().Select(p => p.Key).ToList());
    }

    [TestMethod]
    public void RunDailyCourting_OnlyTrueNeutralAndOtherLeaningRealmsLeft_CourtsNobody()
    {
        var sut = BuildCourting(balance: -40);
        _f.Store.Pledge("cardolan", FactionSide.Free);
        _f.Store.Pledge("dorwinion", FactionSide.Free);

        sut.RunDailyCourting(50);

        Assert.AreEqual(2, _f.Store.Count);
        Assert.IsFalse(_f.Store.IsPledged("anduinvale"), "a true neutral is never courted by the AI");
        Assert.IsFalse(_f.Store.IsPledged("rhudaur"), "a realm that leans to the other side is never courted");
        Assert.AreEqual(50, _f.Store.NextCourtingDay);
    }

    // Umbar leans Free here only to reach the guard: it is Hostile to Gondor, so the AI takes the next realm.
    [TestMethod]
    public void RunDailyCourting_TheGuardRefusesTheFirstRealm_TakesTheNext()
    {
        _f.Leans = new List<WarPledgeLean> { new("umbar", FactionSide.Free), new("cardolan", FactionSide.Free) };
        var sut = BuildCourting(balance: -40);

        sut.RunDailyCourting(50);

        CollectionAssert.AreEqual(new[] { "cardolan" }, _f.Store.Snapshot().Select(p => p.Key).ToList());
    }

    [TestMethod]
    public void RunDailyCourting_ALeanRowNoLiveKingdomHas_IsSkippedAndWarnedOnce()
    {
        _f.Leans = new List<WarPledgeLean> { new("gondolin", FactionSide.Free), new("cardolan", FactionSide.Free) };
        var sut = BuildCourting(balance: -40);

        sut.RunDailyCourting(50);
        sut.RunDailyCourting(80);

        Assert.IsTrue(_f.Store.IsPledged("cardolan"));
        _f.Logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("gondolin")));
    }

    [TestMethod]
    public void RunDailyCourting_SwitchOff_CourtsNobodyAndSetsNoAnchor()
    {
        var sut = BuildCourting(balance: -80, nextCourtingDay: WarPledgeStore.NoCourtingDay);
        _f.Settings.Enabled.Returns(false);

        sut.RunDailyCourting(50);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
    }

    // With momentum tracking off the meter does not move, so its last value says nothing about today.
    [TestMethod]
    public void RunDailyCourting_MomentumTrackingOff_CourtsNobody()
    {
        var sut = BuildCourting(balance: -80);
        _f.MomentumSettings.MomentumEnabled.Returns(false);

        sut.RunDailyCourting(50);

        Assert.AreEqual(0, _f.Store.Count);
    }

    [DataTestMethod]
    [DataRow(WarPhase.Peace)]
    [DataRow(WarPhase.IsengardWar)]
    [DataRow(WarPhase.WarEnded)]
    public void RunDailyCourting_NotFullWar_CourtsNobodyAndSetsNoAnchor(WarPhase phase)
    {
        var sut = _f.Build(phase);
        _f.Momentum.SliderValue.Returns(-80);

        sut.RunDailyCourting(50);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
    }

    [TestMethod]
    public void RunDailyCourting_WarOfTheRingOff_CourtsNobody()
    {
        var sut = BuildCourting(balance: -80);
        _f.WotrSettings.WarOfTheRingEnabled.Returns(false);

        sut.RunDailyCourting(50);

        Assert.AreEqual(0, _f.Store.Count);
    }

    [TestMethod]
    public void RunDailyCourting_ANegativeDay_DoesNothing()
    {
        var sut = BuildCourting(balance: -80, nextCourtingDay: WarPledgeStore.NoCourtingDay);

        sut.RunDailyCourting(-1);

        Assert.AreEqual(0, _f.Store.Count);
        Assert.AreEqual(WarPledgeStore.NoCourtingDay, _f.Store.NextCourtingDay);
    }

    [TestMethod]
    public void RunDailyCourting_ADayNearTheIntLimit_DoesNotWrapTheCourtingDay()
    {
        var sut = BuildCourting(balance: 0, nextCourtingDay: WarPledgeStore.NoCourtingDay);

        sut.RunDailyCourting(int.MaxValue - 5);

        Assert.AreEqual(int.MaxValue, _f.Store.NextCourtingDay);
    }

    // ---- the status the console prints ----

    [DataTestMethod]
    [DataRow(-25, FactionSide.Free)]
    [DataRow(25, FactionSide.Evil)]
    [DataRow(24, FactionSide.Neutral)]
    [DataRow(-24, FactionSide.Neutral)]
    public void Status_CourtingSide_IsTheSideTheDailyStepWouldCourtFor(int balance, FactionSide expected)
    {
        var sut = BuildCourting(balance);

        Assert.AreEqual(expected, sut.Status().CourtingSide);
    }

    [TestMethod]
    public void Status_ListsEveryLeanRowThenAPledgeWithoutOne()
    {
        var sut = BuildCourting(balance: -30, nextCourtingDay: 74);
        _f.World.IsPlayerRuled("dorwinion").Returns(true);
        _f.Alliance.Kingdoms.Remove("anduinvale");
        sut.TryJoin("cardolan", FactionSide.Free, byAi: true, dryRun: false, out _);
        _f.Alliance.Kingdoms.Add("newrealm");
        sut.TryJoin("newrealm", FactionSide.Evil, byAi: false, dryRun: false, out _);

        var status = sut.Status();

        Assert.IsTrue(status.Enabled);
        Assert.IsTrue(status.MomentumEnabled);
        Assert.IsTrue(status.WarOfTheRingEnabled);
        Assert.AreEqual(WarPhase.FullWar, status.Phase);
        Assert.AreEqual(-30, status.Balance);
        Assert.AreEqual(FactionSide.Free, status.CourtingSide);
        Assert.AreEqual(25, status.LosingBalance);
        Assert.AreEqual(30, status.DaysBetweenAiJoins);
        Assert.AreEqual(74, status.NextCourtingDay);
        CollectionAssert.AreEqual(
            new[] { "rhudaur", "umbar", "cardolan", "dorwinion", "anduinvale", "newrealm" },
            status.Rows.Select(r => r.KingdomId).ToList());

        var cardolan = status.Rows[2];
        Assert.AreEqual(FactionSide.Free, cardolan.Lean);
        Assert.AreEqual(FactionSide.Free, cardolan.PledgedSide);
        Assert.IsTrue(cardolan.IsLive);
        Assert.IsTrue(status.Rows[3].IsPlayerRuled);
        Assert.IsFalse(status.Rows[4].IsLive);
        Assert.AreEqual(FactionSide.Neutral, status.Rows[0].PledgedSide);
        Assert.IsNull(status.Rows[5].Lean, "a realm pledged by the console has no lean row");
        Assert.AreEqual(FactionSide.Evil, status.Rows[5].PledgedSide);
    }
}
