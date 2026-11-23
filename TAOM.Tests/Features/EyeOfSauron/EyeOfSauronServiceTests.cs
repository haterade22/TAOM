using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8, the Eye of Sauron hunt: scores and decay, the start gates, hunter choice, the busy guard, the host top-up,
/// the out-of-sight steps, and that every end releases the pin the hunt set. The engine side is a substitute adapter.
/// </summary>
[TestClass]
public class EyeOfSauronServiceTests
{
    private const string Gondor = "empire_w";
    private const string MordorKingdom = "empire_s";
    private const string Party = "hunter_party_1";

    private IHunterPartyAdapter _adapter = null!;
    private IAlignmentService _alignment = null!;
    private IEyeOfSauronSettings _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private EyeOfSauronService _sut = null!;
    private double _now;

    [TestInitialize]
    public void SetUp()
    {
        _now = 1000d;
        _adapter = Substitute.For<IHunterPartyAdapter>();
        _adapter.NowDays.Returns(_ => _now);
        _adapter.PlayerKingdomId.Returns(Gondor);
        _adapter.PlayerStrength.Returns(100f);
        _adapter.ReadHero(Arg.Any<string>()).Returns(HunterHeroSnapshot.Missing);
        _adapter.ReadParty(Arg.Any<string>()).Returns(HuntPartySnapshot.Missing);
        _adapter.GetTroopPower(Arg.Any<string>(), Arg.Any<string>()).Returns(2f);
        _adapter.AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(ci => ci.ArgAt<int>(2));
        _adapter.PinAndEngagePlayer(Arg.Any<string>()).Returns(true);
        _adapter.SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>()).Returns(Party);
        _adapter.ReadStepState(Arg.Any<string>()).Returns(HuntStepState.Missing);
        _adapter.TryRebuildPath(Arg.Any<string>()).Returns(HuntPath.Empty);
        _adapter.ReadSight(Arg.Any<string>(), Arg.Any<float>()).Returns(HuntSightReading.Missing);

        // A real AlignmentService, so GetKingdomSide and ResolveSide (the culture fallback) agree as in the game.
        _alignment = NewAlignment();

        _settings = Substitute.For<IEyeOfSauronSettings>();
        _settings.Enabled.Returns(true);
        _settings.Threshold.Returns(10f);
        _settings.HalfLifeDays.Returns(30f);
        _settings.CooldownDays.Returns(60);
        _settings.HuntTimeoutDays.Returns(20);
        _settings.HunterSpeedMultiplier.Returns(1.3f);
        _settings.HunterStrengthRatio.Returns(1.3f);
        _settings.HunterMaxTroops.Returns(250);
        _settings.HunterStepCap.Returns(15f);

        _coop = Substitute.For<ICoopSessionProvider>();
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _sut = new EyeOfSauronService(_adapter, _alignment, _settings, _coop, _dedicated, _logger);
    }

    // ---------------------------------------------------------------- helpers

    internal static IAlignmentService NewAlignment()
    {
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(new System.Collections.Generic.Dictionary<string, string>
        {
            [Gondor] = "free",
            ["rivendell"] = "free",
            [MordorKingdom] = "evil",
            ["dolguldur"] = "evil",
            ["gondor"] = "free",
            ["mordor"] = "evil",
            ["umbar"] = "neutral",
        });
        return new AlignmentService(config, Substitute.For<IModLogger>());
    }

    private static HunterHeroSnapshot FreeHero(string faction = MordorKingdom, bool atWar = true) => new()
    {
        Exists = true, IsAlive = true, IsActive = true, FactionId = faction, AtWarWithPlayerKingdom = atWar,
        CanLeadParty = true, IsLord = true,
    };

    private static HunterHeroSnapshot Leading(string partyId, string faction = MordorKingdom) => new()
    {
        Exists = true, IsAlive = true, IsActive = true, FactionId = faction, AtWarWithPlayerKingdom = true,
        CanLeadParty = true, IsLord = true, IsInAParty = true, LedPartyId = partyId,
    };

    private static HuntPartySnapshot FreeParty(string leader, int heads = 50, float strength = 40f, bool engaging = true) => new()
    {
        Exists = true, IsActive = true, LeaderHeroId = leader, HeadCount = heads, Strength = strength,
        IsEngagingPlayer = engaging,
    };

    private void Battle(string? loserKingdom, DeedKind kind = DeedKind.Field, bool playerWon = true, bool playerEvent = true,
        bool involvesHunter = false, bool hasWinner = true, bool hunterLost = false) =>
        _sut.OnMapEventEnded(new HuntBattleSummary
        {
            IsPlayerEvent = playerEvent, PlayerWon = playerWon, LoserKingdomId = loserKingdom, Kind = kind,
            InvolvesHunter = involvesHunter, HasWinner = hasWinner, HunterLost = hunterLost,
        });

    /// <summary>Gives the player a Free kingdom, a score over the threshold, and the Witch-king free and at war.</summary>
    private void ArmWitchKingHunt()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId));
    }

    private void StartWitchKingHunt()
    {
        ArmWitchKingHunt();
        _sut.OnDailyTick();
        Assert.IsTrue(_sut.IsHunting, "precondition: the hunt started");
        _adapter.ClearReceivedCalls();
    }

    // ---------------------------------------------------------------- decay

    [TestMethod]
    public void Decay_OneHalfLife_Halves() => Assert.AreEqual(5f, HuntMath.Decay(10f, 30d, 30f), 1e-4f);

    [TestMethod]
    public void Decay_NaNScore_ReturnsZero() => Assert.AreEqual(0f, HuntMath.Decay(float.NaN, 1d, 30f));

    [TestMethod]
    public void Decay_InfiniteScore_ReturnsZero() =>
        Assert.AreEqual(0f, HuntMath.Decay(float.PositiveInfinity, 1d, 30f));

    [TestMethod]
    public void Decay_NegativeOrNaNElapsed_KeepsTheScore()
    {
        Assert.AreEqual(10f, HuntMath.Decay(10f, -5d, 30f));
        Assert.AreEqual(10f, HuntMath.Decay(10f, double.NaN, 30f));
    }

    [TestMethod]
    public void Decay_NaNHalfLife_KeepsTheScore() => Assert.AreEqual(10f, HuntMath.Decay(10f, 30d, float.NaN));

    [TestMethod]
    public void GetScore_DecaysFromTheLastDeed()
    {
        Battle(MordorKingdom, DeedKind.Assault);
        _now += 30d;

        Assert.AreEqual(1.5f, _sut.GetScore(FactionSide.Evil), 1e-3f);
    }

    // ---------------------------------------------------------------- deeds

    [DataTestMethod]
    [DataRow(DeedKind.Field, 1f)]
    [DataRow(DeedKind.SallyOrOutside, 2f)]
    [DataRow(DeedKind.Assault, 3f)]
    public void OnMapEventEnded_PlayerBeatsAnEvilKingdom_ScoresTheBattleWeight(DeedKind kind, float expected)
    {
        Battle(MordorKingdom, kind);

        Assert.AreEqual(expected, _sut.GetScore(FactionSide.Evil), 1e-4f);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnMapEventEnded_PlayerBeatsAFreeKingdom_ScoresAgainstFree()
    {
        Battle("rivendell");

        Assert.AreEqual(1f, _sut.GetScore(FactionSide.Free), 1e-4f);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnMapEventEnded_NeutralLoser_NeverScores()
    {
        Battle("some_minor_kingdom");

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnMapEventEnded_LoserIsNoKingdom_NeverScores()
    {
        Battle(null, DeedKind.Assault);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnMapEventEnded_PlayerLost_NeverScores()
    {
        Battle(MordorKingdom, playerWon: false);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnMapEventEnded_NotThePlayersBattle_NeverScores()
    {
        Battle(MordorKingdom, playerEvent: false);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnMapEventEnded_KingdomlessPlayer_StillScores_ButIsNeverHunted()
    {
        _adapter.PlayerKingdomId.Returns((string?)null);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        for (int i = 0; i < 4; i++)
            Battle(MordorKingdom, DeedKind.Assault);

        _sut.OnDailyTick();

        Assert.AreEqual(12f, _sut.GetScore(FactionSide.Evil), 1e-3f);
        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    // ---------------------------------------------------------------- start gates

    [TestMethod]
    public void OnDailyTick_FreePlayerOverTheThreshold_SpawnsAndPinsTheWitchKing()
    {
        ArmWitchKingHunt();

        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
        Assert.AreEqual(HuntTroopLists.WitchKingId, _sut.HunterHeroId);
        Assert.AreEqual(Party, _sut.HunterPartyId);
        _adapter.Received(1).SpawnHunterParty(HuntTroopLists.WitchKingId, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.Received(1).PinAndEngagePlayer(Party);
        _adapter.Received(1).Announce(HuntAnnouncement.StartFree, HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void OnDailyTick_BelowTheThreshold_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _settings.Threshold.Returns(13f);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_NaNThreshold_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _settings.Threshold.Returns(float.NaN);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_WarGateFalse_StartsNoHunt()
    {
        _sut.AddScore(FactionSide.Evil, 50f);
        foreach (var id in HuntTroopLists.WraithOrder)
            _adapter.ReadHero(id).Returns(FreeHero(atWar: false));

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnDailyTick_NeutralPlayerKingdom_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _adapter.PlayerKingdomId.Returns("some_minor_kingdom");

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    // E01: KingdomManager.CreateKingdom names a player-founded kingdom "new_kingdom", which alignment.json does not
    // list, so it is sided by its own culture (ResolveSide, #744), never left Neutral.
    [TestMethod]
    public void TryStartHunt_PlayerFoundedKingdom_SidedByCulture_IsHunted()
    {
        ArmWitchKingHunt();
        _adapter.PlayerKingdomId.Returns("new_kingdom");
        _adapter.PlayerKingdomCultureId.Returns("gondor");

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual(HuntTroopLists.WitchKingId, _sut.HunterHeroId);
        _adapter.Received(1).Announce(HuntAnnouncement.StartFree, HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void TryStartHunt_PlayerFoundedKingdom_EvilCulture_SendsGlorfindel()
    {
        _adapter.PlayerKingdomId.Returns("new_kingdom1");
        _adapter.PlayerKingdomCultureId.Returns("mordor");
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell"));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual(HuntTroopLists.GlorfindelId, _sut.HunterHeroId);
        _adapter.DidNotReceive().ReadHero(HuntTroopLists.WitchKingId);
    }

    [DataTestMethod]
    [DataRow("umbar")]
    [DataRow("")]
    [DataRow(null)]
    public void TryStartHunt_PlayerFoundedKingdom_NeutralOrMissingCulture_NoHunt(string? culture)
    {
        ArmWitchKingHunt();
        _adapter.PlayerKingdomId.Returns("new_kingdom");
        _adapter.PlayerKingdomCultureId.Returns(culture);

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting, culture ?? "null");
        StringAssert.Contains(verdict, "neither side");
    }

    [TestMethod]
    public void Describe_PlayerFoundedKingdom_ShowsItsCultureSide()
    {
        _adapter.PlayerKingdomId.Returns("new_kingdom");
        _adapter.PlayerKingdomCultureId.Returns("gondor");

        StringAssert.Contains(string.Join("\n", _sut.Describe()), "Your kingdom: new_kingdom (Free)");
    }

    [TestMethod]
    public void OnDailyTick_Disabled_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _settings.Enabled.Returns(false);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_CoopSessionLive_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _coop.IsSessionActive.Returns(true);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_DedicatedServer_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _dedicated.IsDedicatedServer.Returns(true);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnDailyTick_InCooldown_StartsNoHunt_UntilItPasses()
    {
        StartWitchKingHunt();
        _sut.EndHunt(HuntEndReason.Manual);
        _sut.AddScore(FactionSide.Evil, 50f);

        _now += 59d;
        _sut.OnDailyTick();
        Assert.IsFalse(_sut.IsHunting, "inside the 60-day cooldown");

        _now += 2d;
        _sut.OnDailyTick();
        Assert.IsTrue(_sut.IsHunting, "the cooldown has passed");
    }

    // ---------------------------------------------------------------- hunter choice

    [TestMethod]
    public void TryStartHunt_FreePlayer_SendsTheWitchKingFirst()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        foreach (var id in HuntTroopLists.WraithOrder)
            _adapter.ReadHero(id).Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual(HuntTroopLists.WitchKingId, _sut.HunterHeroId);
    }

    [TestMethod]
    public void TryStartHunt_WitchKingAtPeace_SendsTheNextWraith_AndKhamulOnlyLast()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());
        _adapter.ReadHero(HuntTroopLists.KhamulId).Returns(FreeHero("dolguldur"));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);
        Assert.AreEqual("lord_1_155", _sut.HunterHeroId);

        _sut.EndHunt(HuntEndReason.Manual);
        _adapter.ReadHero("lord_1_155").Returns(HunterHeroSnapshot.Missing);
        _sut.TryStartHunt(ignoreScoreAndCooldown: true);
        Assert.AreEqual(HuntTroopLists.KhamulId, _sut.HunterHeroId);
        Assert.AreEqual(HuntTroopLists.KhamulId, HuntTroopLists.WraithOrder.Last());
    }

    [TestMethod]
    public void TryStartHunt_EvilPlayer_SendsGlorfindelOnly()
    {
        _adapter.PlayerKingdomId.Returns(MordorKingdom);
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell"));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual(HuntTroopLists.GlorfindelId, _sut.HunterHeroId);
        _adapter.Received(1).Announce(HuntAnnouncement.StartEvil, HuntTroopLists.GlorfindelId);
        _adapter.DidNotReceive().ReadHero(HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void TryStartHunt_EvilPlayer_RivendellAtPeace_NoHunt_AndNoWraithIsAsked()
    {
        _adapter.PlayerKingdomId.Returns(MordorKingdom);
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell", atWar: false));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().ReadHero(HuntTroopLists.WitchKingId);
    }

    [TestMethod]
    public void TryStartHunt_FreePlayer_ScoreAgainstFreeDoesNotCount()
    {
        _sut.AddScore(FactionSide.Free, 50f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
    }

    [DataTestMethod]
    [DataRow("dead")]
    [DataRow("fugitive")]
    [DataRow("prisoner")]
    [DataRow("held")]
    [DataRow("deathmark")]
    [DataRow("missing")]
    public void TryStartHunt_WitchKingUnfit_IsSkippedForTheNextWraith(string state)
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        var wk = FreeHero();
        switch (state)
        {
            case "dead": wk.IsAlive = false; break;
            case "fugitive": wk.IsFugitive = true; break;
            case "prisoner": wk.IsPrisoner = true; break;
            case "held": wk.IsHeldByAParty = true; break;
            case "deathmark": wk.IsMarkedForDeath = true; break;
            case "missing": wk = HunterHeroSnapshot.Missing; break;
        }
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual("lord_1_155", _sut.HunterHeroId, state);
        _adapter.DidNotReceive().SpawnHunterParty(HuntTroopLists.WitchKingId, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    // ---------------------------------------------------------------- busy guard, re-task, spawn

    [TestMethod]
    public void TryStartHunt_LeadsAFreeParty_RetasksThatParty_AndNeverSpawns()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        _adapter.ReadParty("wk_party").Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual("wk_party", _sut.HunterPartyId);
        _adapter.Received(1).PinAndEngagePlayer("wk_party");
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    private void AssertTheNextWraithRode(string because)
    {
        Assert.AreEqual("lord_1_155", _sut.HunterHeroId, because);
        _adapter.Received(1).SpawnHunterParty("lord_1_155", Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().SpawnHunterParty(HuntTroopLists.WitchKingId, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer("wk_party");
    }

    [DataTestMethod]
    [DataRow("mapevent")]
    [DataRow("army")]
    [DataRow("siege")]
    [DataRow("settlement")]
    [DataRow("disbanding")]
    [DataRow("pinned")]
    [DataRow("sworn")]
    [DataRow("inactive")]
    public void TryStartHunt_HisPartyIsBusy_TheNextWraithRides_AndNeverASecondParty(string busy)
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());
        var party = FreeParty(HuntTroopLists.WitchKingId);
        switch (busy)
        {
            case "mapevent": party.InMapEvent = true; break;
            case "army": party.InArmy = true; break;
            case "siege": party.Besieging = true; break;
            case "settlement": party.InSettlement = true; break;
            case "disbanding": party.IsDisbanding = true; break;
            case "pinned": party.AiPinned = true; break;
            case "sworn": party.HoldsOffPlayer = true; break;
            case "inactive": party.IsActive = false; break;
        }
        _adapter.ReadParty("wk_party").Returns(party);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        AssertTheNextWraithRode(busy);
    }

    [TestMethod]
    public void TryStartHunt_InAnotherLordsParty_TheNextWraithRides()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        var wk = FreeHero();
        wk.IsInAParty = true;
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        AssertTheNextWraithRode("in another lord's party");
    }

    [DataTestMethod]
    [DataRow("cannotlead")]
    [DataRow("notlord")]
    [DataRow("tournamentorsiege")]
    [DataRow("inactive")]
    public void TryStartHunt_FreeHeroUnfitToLead_TheNextWraithRides(string state)
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        var wk = FreeHero();
        switch (state)
        {
            case "cannotlead": wk.CanLeadParty = false; break;
            case "notlord": wk.IsLord = false; break;
            case "tournamentorsiege": wk.SettlementBlocksSpawn = true; break;
            case "inactive": wk.IsActive = false; break;
        }
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        AssertTheNextWraithRode(state);
    }

    [TestMethod]
    public void TryStartHunt_EveryHunterBusy_Waits_LogsTheFirstReason_AndNeverSpawns()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        var party = FreeParty(HuntTroopLists.WitchKingId);
        party.InArmy = true;
        _adapter.ReadParty("wk_party").Returns(party);

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        StringAssert.StartsWith(verdict, "Waiting");
        StringAssert.Contains(verdict, HuntTroopLists.WitchKingId);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains("Waiting") && m.Contains(HuntTroopLists.WitchKingId)));
    }

    // E25: the daily tick logs a wait because WaitReason returned one, never by reading the verdict's wording. The
    // assertions name the hunter and his party only, so a reworded message keeps these tests green.
    private void ArmWitchKingWaitingInAnArmy()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        var party = FreeParty(HuntTroopLists.WitchKingId);
        party.InArmy = true;
        _adapter.ReadParty("wk_party").Returns(party);
    }

    [TestMethod]
    public void OnDailyTick_HunterWaits_LogsTheReason()
    {
        ArmWitchKingWaitingInAnArmy();

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
        _logger.Received(1).LogInfo(Arg.Is<string>(m => m.Contains(HuntTroopLists.WitchKingId) && m.Contains("wk_party")));
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnDailyTick_NoHunterAtWar_IsNotAWait_LogsNothing()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero(atWar: false));

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // E25, the part the behaviour tests above cannot pin: every wait reason begins "Waiting:" today, so a decision
    // that parsed the text would still pass them. The method that makes the logging decision must not inspect
    // the reason's text at all.
    [TestMethod]
    public void TryStartHunt_Source_TheWaitLogDecisionNeverReadsTheReasonText()
    {
        var source = RepoPaths.ReadSource("Main/Features/EyeOfSauron/EyeOfSauronService.cs", stripComments: true);
        var start = source.IndexOf("public string TryStartHunt(", StringComparison.Ordinal);
        var end = source.IndexOf("private static bool CanHunt(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0 && end > start, "TryStartHunt or the CanHunt that follows it not found");
        var body = source.Substring(start, end - start);

        StringAssert.Contains(body, "_logger.LogInfo(\"[EyeOfSauron] \" + firstWait)", "the wait is still logged");
        var parse = Regex.Match(body, @"\b(?:StartsWith|EndsWith|Contains|IndexOf|Equals|Substring|Split|Regex)\b");
        Assert.IsFalse(parse.Success,
            $"TryStartHunt inspects text ('{parse.Value}'): the wait is logged because WaitReason returned one, never by its wording.");
    }

    [TestMethod]
    public void TryStartHunt_DryRunWhileTheHunterWaits_ReturnsTheReason_LogsNothing()
    {
        ArmWitchKingWaitingInAnArmy();

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: false, dryRun: true);

        StringAssert.Contains(verdict, "wk_party");
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_SpawnRefused_StopsTheSearch_AndLogs()
    {
        ArmWitchKingHunt();
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());
        _adapter.SpawnHunterParty(HuntTroopLists.WitchKingId, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>()).Returns((string?)null);

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        StringAssert.StartsWith(verdict, "Refused");
        _adapter.Received(1).SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains(HuntTroopLists.WitchKingId)));
    }

    [TestMethod]
    public void TryStartHunt_SpawnsInTheRingBeyondTheSightMargin()
    {
        ArmWitchKingHunt();

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        _adapter.Received(1).SpawnHunterParty(HuntTroopLists.WitchKingId, EyeOfSauronService.SpawnRingInner,
            EyeOfSauronService.SpawnRingOuter, EyeOfSauronService.SightMargin);
        Assert.IsTrue(EyeOfSauronService.SpawnRingInner > EyeOfSauronService.SightMargin);
    }

    // E20: the host is topped up before the engage, since CheckPartyNeedsUpdate can start the encounter at once.
    [TestMethod]
    public void TryStartHunt_TopsUpTheHostAndFood_BeforeTheEngage()
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 30f));
        var order = new System.Collections.Generic.List<string>();
        _adapter.When(a => a.AddTroops(Party, Arg.Any<string>(), Arg.Any<int>())).Do(_ => order.Add("troops"));
        _adapter.When(a => a.TopUpFood(Party, Arg.Any<int>())).Do(_ => order.Add("food"));
        _adapter.When(a => a.PinAndEngagePlayer(Party)).Do(_ => order.Add("engage"));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.AreEqual("engage", order.Last());
        CollectionAssert.Contains(order, "troops");
        CollectionAssert.Contains(order, "food");
    }

    [TestMethod]
    public void TryStartHunt_PinRefused_EndsAtOnce_WithoutReleasingAPinItNeverSet()
    {
        ArmWitchKingHunt();
        _adapter.PinAndEngagePlayer(Party).Returns(false);

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_DryRun_Spawn_ChangesNothing_AndNamesTheGovernorship()
    {
        ArmWitchKingHunt();
        var wk = FreeHero();
        wk.GovernorOfSettlementId = "town_minas_morgul";
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(wk);

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: true, dryRun: true);

        Assert.IsFalse(_sut.IsHunting);
        StringAssert.Contains(verdict, "spawned");
        StringAssert.Contains(verdict, "town_minas_morgul");
        StringAssert.Contains(verdict, "taom.start_eye_hunt confirm");
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        _adapter.DidNotReceive().TopUpFood(Arg.Any<string>(), Arg.Any<int>());
        _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_DryRun_Retask_PrintsTheExactTopUp_AndChangesNothing()
    {
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(Leading("wk_party"));
        _adapter.ReadParty("wk_party").Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 30f, engaging: false));

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: true, dryRun: true);

        Assert.IsFalse(_sut.IsHunting);
        StringAssert.Contains(verdict, "re-tasked");
        StringAssert.Contains(verdict, "mordor_num_temple_knight x13");
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    // E14: the three refusals that guard the console path, each set up so the hunt would otherwise start.
    [TestMethod]
    public void TryStartHunt_WhileHunting_RefusesAndNeverSpawns()
    {
        StartWitchKingHunt();

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: true);

        StringAssert.StartsWith(verdict, "A hunt is already running");
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
        _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_WhileAnotherWraithHunts_NeverStartsASecondHunt()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero("lord_1_155").Returns(FreeHero());
        _sut.TryStartHunt(ignoreScoreAndCooldown: false);
        Assert.AreEqual("lord_1_155", _sut.HunterHeroId, "precondition");
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _adapter.SpawnHunterParty(HuntTroopLists.WitchKingId, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>()).Returns("wk_party");
        _adapter.ClearReceivedCalls();

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: true);

        StringAssert.StartsWith(verdict, "A hunt is already running");
        Assert.AreEqual("lord_1_155", _sut.HunterHeroId);
        Assert.AreEqual(Party, _sut.HunterPartyId);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("off")]
    [DataRow("coop")]
    [DataRow("coopdefer")]
    [DataRow("server")]
    public void TryStartHunt_ConsoleForce_SwitchedOffOrNotSinglePlayer_Refuses(string gate)
    {
        ArmWitchKingHunt();
        switch (gate)
        {
            case "off": _settings.Enabled.Returns(false); break;
            case "coop": _coop.IsSessionActive.Returns(true); break;
            case "coopdefer": _coop.ShouldDeferToHost.Returns(true); break;
            case "server": _dedicated.IsDedicatedServer.Returns(true); break;
        }

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: true);

        Assert.IsFalse(_sut.IsHunting, gate);
        StringAssert.StartsWith(verdict, "Refused");
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
    }

    // E08: Bannerlord Together cannot tell host from client, so the hunt stands down while it is loaded.
    [TestMethod]
    public void OnDailyTick_UnprobeableCoopMod_StartsNoHunt()
    {
        ArmWitchKingHunt();
        _coop.ShouldDeferToHost.Returns(true);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
    }

    [TestMethod]
    public void TryStartHunt_ConsoleForce_IgnoresScoreAndCooldown_ButHonoursTheWarGate()
    {
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero(atWar: false));

        _sut.TryStartHunt(ignoreScoreAndCooldown: true);
        Assert.IsFalse(_sut.IsHunting, "the war gate holds even for the console");

        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _sut.TryStartHunt(ignoreScoreAndCooldown: true);
        Assert.IsTrue(_sut.IsHunting, "a zero score does not stop the console");
    }

    // ---------------------------------------------------------------- host top-up and food

    [TestMethod]
    public void PlanTopUp_SplitsTheDeficitByShareAndPower()
    {
        var host = new[] { new HostTroop("a", 0.5f), new HostTroop("b", 0.5f) };

        var plan = HuntMath.PlanTopUp(host, 100f, id => id == "a" ? 2f : 5f, 500);

        CollectionAssert.AreEqual(new[] { ("a", 25), ("b", 10) }, plan.ToArray());
    }

    [TestMethod]
    public void PlanTopUp_CapsTheTotalAtTheRoom()
    {
        var host = new[] { new HostTroop("a", 0.5f), new HostTroop("b", 0.5f) };

        var plan = HuntMath.PlanTopUp(host, 1000f, _ => 1f, 30);

        Assert.AreEqual(30, plan.Sum(p => p.Count));
        Assert.AreEqual(15, plan.Single(p => p.TroopId == "a").Count);
    }

    [TestMethod]
    public void PlanTopUp_ScaledDown_HandsTheRemainderOutInListOrder()
    {
        var host = new[] { new HostTroop("a", 1f / 3f), new HostTroop("b", 1f / 3f), new HostTroop("c", 1f / 3f) };

        var plan = HuntMath.PlanTopUp(host, 300f, _ => 1f, 10);

        CollectionAssert.AreEqual(new[] { ("a", 4), ("b", 3), ("c", 3) }, plan.ToArray());
    }

    [TestMethod]
    public void PlanTopUp_NonFinitePower_SkipsThatTroop()
    {
        var host = new[] { new HostTroop("a", 0.5f), new HostTroop("b", 0.5f), new HostTroop("c", 0.5f) };

        var plan = HuntMath.PlanTopUp(host, 100f,
            id => id == "a" ? float.NaN : id == "b" ? 0f : 5f, 500);

        CollectionAssert.AreEqual(new[] { ("c", 10) }, plan.ToArray());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(0f)]
    [DataRow(-5f)]
    public void PlanTopUp_DeficitNotPositiveAndFinite_PlansNothing(float deficit)
    {
        var plan = HuntMath.PlanTopUp(HuntTroopLists.Mordor, deficit, _ => 2f, 500);

        Assert.AreEqual(0, plan.Count);
    }

    [TestMethod]
    public void PlanTopUp_NoRoom_PlansNothing()
    {
        Assert.AreEqual(0, HuntMath.PlanTopUp(HuntTroopLists.Mordor, 100f, _ => 2f, 0).Count);
    }

    [TestMethod]
    public void TryStartHunt_TopsUpToTheStrengthRatio_AfterTheHostIsRegistered()
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 30f));
        float bonusSeenAtFirstAdd = -1f;
        _adapter.AddTroops(Party, Arg.Any<string>(), Arg.Any<int>()).Returns(ci =>
        {
            if (bonusSeenAtFirstAdd < 0f)
                bonusSeenAtFirstAdd = _sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f);
            return ci.ArgAt<int>(2);
        });

        _sut.OnDailyTick();

        // deficit = 1.3 * 100 - 30 = 100; Mordor shares 0.25/0.25/0.2/0.15/0.15 at power 2.
        _adapter.Received(1).AddTroops(Party, "mordor_num_temple_knight", 13);
        _adapter.Received(1).AddTroops(Party, "mordor_num_shadowbow", 10);
        Assert.IsTrue(bonusSeenAtFirstAdd > 0f, "the party must count as the hunter's host before its roster grows");
    }

    [TestMethod]
    public void TryStartHunt_PlayerStrengthNaN_AddsNoTroops()
    {
        ArmWitchKingHunt();
        _adapter.PlayerStrength.Returns(float.NaN);

        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void TryStartHunt_HostStrengthNotFinite_AddsNoTroops(float strength)
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, strength: strength));

        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryStartHunt_TopsUpTheStartFood()
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 100));

        _sut.OnDailyTick();

        _adapter.Received(1).TopUpFood(Party, 15);
    }

    [TestMethod]
    public void TryStartHunt_HostAtTheHeadCap_AddsNoTroops()
    {
        ArmWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 250, strength: 1f));

        _sut.OnDailyTick();

        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryStartHunt_KhamulOfDolGuldur_GetsTheDolGuldurHost()
    {
        _sut.AddScore(FactionSide.Evil, 12f);
        _adapter.ReadHero(HuntTroopLists.KhamulId).Returns(FreeHero("dolguldur"));
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.KhamulId, strength: 0f));

        _sut.OnDailyTick();

        _adapter.Received().AddTroops(Party, "dg_khamul_shadow_knight", Arg.Any<int>());
        _adapter.DidNotReceive().AddTroops(Party, "mordor_num_temple_knight", Arg.Any<int>());
    }

    [TestMethod]
    public void TryStartHunt_Glorfindel_GetsTheElvenHost()
    {
        _adapter.PlayerKingdomId.Returns(MordorKingdom);
        _sut.AddScore(FactionSide.Free, 12f);
        _adapter.ReadHero(HuntTroopLists.GlorfindelId).Returns(FreeHero("rivendell"));
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.GlorfindelId, strength: 0f));

        _sut.OnDailyTick();

        _adapter.Received().AddTroops(Party, "rivendell_glorfindel_guard", Arg.Any<int>());
    }

    [TestMethod]
    public void FoodTarget_IsThreeDaysForTheHost() => Assert.AreEqual(38, HuntMath.FoodTarget(250));

    [TestMethod]
    public void OnDailyTick_WhileHunting_WeakenedHost_IsToppedUpAgain()
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 30f));

        _now += 1d;
        _sut.OnDailyTick();

        _adapter.Received(1).AddTroops(Party, "mordor_num_temple_knight", 13);
        _adapter.Received(1).TopUpFood(Party, Arg.Any<int>());
    }

    [TestMethod]
    public void OnDailyTick_WhileHunting_InABattle_NoTroops_ButFood()
    {
        StartWitchKingHunt();
        var party = FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 30f);
        party.InMapEvent = true;
        _adapter.ReadParty(Party).Returns(party);

        _now += 1d;
        _sut.OnDailyTick();

        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        _adapter.Received(1).TopUpFood(Party, Arg.Any<int>());
    }

    [TestMethod]
    public void OnDailyTick_WhileHunting_HealthyHost_NoTroops()
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 100, strength: 130f));

        _now += 1d;
        _sut.OnDailyTick();

        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void OnDailyTick_WhileHunting_TopsUpThreeDaysOfFood()
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 100));

        _now += 1d;
        _sut.OnDailyTick();

        _adapter.Received(1).TopUpFood(Party, 15);
    }

    // ---------------------------------------------------------------- steps

    [TestMethod]
    public void ComputeStep_GapInsideTheSightMargin_IsZero() =>
        Assert.AreEqual(0f, HuntMath.ComputeStep(16f, 10f, 15f));

    [TestMethod]
    public void ComputeStep_LongGap_IsCappedAtTheStepCap() =>
        Assert.AreEqual(15f, HuntMath.ComputeStep(200f, 10f, 15f));

    [TestMethod]
    public void ComputeStep_ShortGap_LeavesOneAndAHalfSightsPlusThree() =>
        Assert.AreEqual(5f, HuntMath.ComputeStep(23f, 10f, 15f), 1e-4f);

    [DataTestMethod]
    [DataRow(float.NaN, 10f, 15f)]
    [DataRow(200f, float.NaN, 15f)]
    [DataRow(200f, 10f, float.NaN)]
    [DataRow(float.PositiveInfinity, 10f, 15f)]
    public void ComputeStep_NonFiniteInput_IsZero(float gap, float sight, float cap) =>
        Assert.AreEqual(0f, HuntMath.ComputeStep(gap, sight, cap));

    [TestMethod]
    public void PathLength_SumsTheLegsFromTheStart() =>
        Assert.AreEqual(7f, HuntMath.PathLength(0f, 0f, new[] { 3f, 3f }, new[] { 0f, 4f }), 1e-4f);

    [TestMethod]
    public void WalkPath_StopsPartWayAlongTheSecondLeg()
    {
        var (x, y) = HuntMath.WalkPath(0f, 0f, new[] { 3f, 3f }, new[] { 0f, 4f }, 5f);

        Assert.AreEqual(3f, x, 1e-4f);
        Assert.AreEqual(2f, y, 1e-4f);
    }

    [TestMethod]
    public void WalkPath_PastTheEnd_StopsAtTheLastPoint()
    {
        var (x, y) = HuntMath.WalkPath(0f, 0f, new[] { 3f }, new[] { 0f }, 50f);

        Assert.AreEqual(3f, x, 1e-4f);
        Assert.AreEqual(0f, y, 1e-4f);
    }

    /// <summary>An all-clear step state with one flag flipped (E05: the service judges every flag).</summary>
    private static HuntStepState StepState(float playerX = 100f, float sight = 10f, string? flip = null) => new()
    {
        Valid = flip != "invalid",
        HunterActive = flip != "hunter inactive",
        HunterInMapEvent = flip == "hunter in battle",
        HunterBesieging = flip == "hunter besieging",
        HunterInArmy = flip == "hunter in army",
        HunterInSettlement = flip == "hunter in settlement",
        HunterAtSea = flip == "hunter at sea",
        HunterVisible = flip == "hunter visible",
        HunterEngagingPlayer = flip != "hunter not engaging",
        PlayerActive = flip != "player inactive",
        PlayerInMapEventOrSiege = flip == "player in battle",
        PlayerInSettlement = flip == "player in settlement",
        PlayerAtSea = flip == "player at sea",
        PlayerInEncounter = flip == "player in encounter",
        TrueSight = flip == "true sight",
        SameIsland = flip != "other island",
        SeeingRange = sight,
        HunterX = 0f,
        HunterY = 0f,
        PlayerX = playerX,
        PlayerY = 0f,
    };

    private void StraightPathTo(float playerX, float sight, string? flip = null)
    {
        _adapter.ReadStepState(Party).Returns(StepState(playerX, sight, flip));
        _adapter.TryRebuildPath(Party).Returns(new HuntPath(new[] { playerX / 2f, playerX }, new[] { 0f, 0f }));
        _adapter.ResolveLanding(Party, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(ci => new HuntLanding(true, ci.ArgAt<float>(1), ci.ArgAt<float>(2)));
    }

    [DataTestMethod]
    [DataRow("invalid")]
    [DataRow("hunter inactive")]
    [DataRow("hunter in battle")]
    [DataRow("hunter besieging")]
    [DataRow("hunter in army")]
    [DataRow("hunter in settlement")]
    [DataRow("hunter at sea")]
    [DataRow("hunter visible")]
    [DataRow("hunter not engaging")]
    [DataRow("player inactive")]
    [DataRow("player in battle")]
    [DataRow("player in settlement")]
    [DataRow("player at sea")]
    [DataRow("player in encounter")]
    [DataRow("true sight")]
    [DataRow("other island")]
    public void OnHourlyTickParty_StepBlocked_NeverRebuildsThePathOrRelocates(string flip)
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f, flip);
        Assert.IsNotNull(EyeOfSauronService.StepBlock(StepState(flip: flip)), flip);

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().TryRebuildPath(Arg.Any<string>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void StepBlock_AllClear_IsNull() => Assert.IsNull(EyeOfSauronService.StepBlock(StepState()));

    [TestMethod]
    public void OnHourlyTickParty_EmptyPath_DoesNotRelocate()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.TryRebuildPath(Party).Returns(HuntPath.Empty);

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().ResolveLanding(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
        StringAssert.Contains(string.Join("\n", _sut.Describe()), "Last hourly step: no path to the player");
    }

    [TestMethod]
    public void OnHourlyTickParty_OutOfSight_StepsByTheCap_AndLandsOutsideOneAndAHalfSights()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);

        _sut.OnHourlyTickParty(Party);

        _adapter.Received(1).Relocate(Party, Arg.Is<float>(x => Math.Abs(x - 15f) < 1e-3f), Arg.Is<float>(y => Math.Abs(y) < 1e-3f));
    }

    [TestMethod]
    public void OnHourlyTickParty_LandingNudgedTooClose_DoesNotRelocate()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.ResolveLanding(Party, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(new HuntLanding(true, 90f, 0f));

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_LandingRefused_DoesNotRelocate()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.ResolveLanding(Party, Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>()).Returns(new HuntLanding(false, 0f, 0f));

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_AlreadyClose_DoesNotJump()
    {
        StartWitchKingHunt();
        StraightPathTo(17f, 10f);

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().ResolveLanding(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_InsideTheSightMargin_NeverRebuildsThePath()
    {
        StartWitchKingHunt();
        StraightPathTo(12f, 10f);

        _sut.OnHourlyTickParty(Party);

        // The straight-line gate comes first: a hunter inside 1.5 sights never pays for a path rebuild.
        _adapter.DidNotReceive().TryRebuildPath(Arg.Any<string>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_Blocked_DoesNotRelocate()
    {
        StartWitchKingHunt();

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(0f)]
    public void OnHourlyTickParty_SightNotPositive_DoesNotRelocate(float sight)
    {
        StartWitchKingHunt();
        StraightPathTo(100f, sight);

        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnHourlyTickParty_AnotherParty_DoesNothing()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);

        _sut.OnHourlyTickParty("someone_else");

        _adapter.DidNotReceive().ReadStepState(Arg.Any<string>());
    }

    // E35: the hourly tick's own party checks.
    [DataTestMethod]
    [DataRow("missing")]
    [DataRow("newleader")]
    public void OnHourlyTickParty_PartyMissingOrNewLeader_EndsPartyLost(string state)
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(state == "missing" ? HuntPartySnapshot.Missing : FreeParty("someone_else"));

        _sut.OnHourlyTickParty(Party);

        Assert.IsFalse(_sut.IsHunting, state);
        _adapter.Received(1).ReleasePin(Party);
        _adapter.DidNotReceive().PinAndEngagePlayer(Party);
        _adapter.DidNotReceive().ReadStepState(Arg.Any<string>());
    }

    [TestMethod]
    public void OnHourlyTickParty_InBattle_OrderLost_DoesNotReissue()
    {
        StartWitchKingHunt();
        var party = FreeParty(HuntTroopLists.WitchKingId, engaging: false);
        party.InMapEvent = true;
        _adapter.ReadParty(Party).Returns(party);

        _sut.OnHourlyTickParty(Party);

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().PinAndEngagePlayer(Party);
    }

    [TestMethod]
    public void OnHourlyTickParty_OrderLost_ReissuesTheEngage_OnlyThen()
    {
        StartWitchKingHunt();
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: true));
        _sut.OnHourlyTickParty(Party);
        _adapter.DidNotReceive().PinAndEngagePlayer(Party);

        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    // ---------------------------------------------------------------- end conditions release the pin

    [DataTestMethod]
    [DataRow("beaten", HuntAnnouncement.Won)]
    [DataRow("lost", HuntAnnouncement.Over)]
    [DataRow("otherbattlelost", HuntAnnouncement.Over)]
    [DataRow("destroyed", HuntAnnouncement.Over)]
    [DataRow("dead", HuntAnnouncement.Over)]
    [DataRow("captured", HuntAnnouncement.Over)]
    [DataRow("fugitive", HuntAnnouncement.Over)]
    [DataRow("partygone", HuntAnnouncement.Over)]
    [DataRow("newleader", HuntAnnouncement.Over)]
    [DataRow("peace", HuntAnnouncement.Over)]
    [DataRow("kingdomless", HuntAnnouncement.Over)]
    [DataRow("timeout", HuntAnnouncement.Over)]
    [DataRow("disabled", HuntAnnouncement.Over)]
    [DataRow("coopdaily", null)]
    [DataRow("coophourly", null)]
    [DataRow("coopdefer", null)]
    [DataRow("manual", HuntAnnouncement.Over)]
    public void EveryEnd_ReleasesThePin_ClearsTheHunt_AndStartsTheCooldown(string end, HuntAnnouncement? shown)
    {
        StartWitchKingHunt();
        switch (end)
        {
            case "beaten": Battle(MordorKingdom, involvesHunter: true, hunterLost: true); break;
            case "lost": Battle(null, playerWon: false, involvesHunter: true); break;
            // E06: a battle without the player ends the hunt only when the hunter's side was defeated.
            case "otherbattlelost": Battle(null, playerWon: false, playerEvent: false, involvesHunter: true, hunterLost: true); break;
            case "destroyed": _sut.OnPartyDestroyed(Party); break;
            case "dead": Daily(h => h.IsAlive = false); break;
            case "captured": Daily(h => h.IsPrisoner = true); break;
            case "fugitive": Daily(h => h.IsFugitive = true); break;
            case "partygone":
                _adapter.ReadParty(Party).Returns(HuntPartySnapshot.Missing);
                _sut.OnDailyTick();
                break;
            case "newleader":
                _adapter.ReadParty(Party).Returns(FreeParty("someone_else"));
                _sut.OnDailyTick();
                break;
            case "peace": Daily(h => h.AtWarWithPlayerKingdom = false); break;
            case "kingdomless":
                _adapter.PlayerKingdomId.Returns((string?)null);
                _sut.OnDailyTick();
                break;
            case "timeout":
                _now += 20d;
                _sut.OnDailyTick();
                break;
            case "disabled":
                _settings.Enabled.Returns(false);
                _sut.OnDailyTick();
                break;
            case "coopdaily":
                _coop.IsSessionActive.Returns(true);
                _sut.OnDailyTick();
                break;
            case "coophourly":
                _dedicated.IsDedicatedServer.Returns(true);
                _sut.OnHourlyTickParty(Party);
                break;
            case "coopdefer":
                _coop.ShouldDeferToHost.Returns(true);
                _sut.OnDailyTick();
                break;
            case "manual": _sut.EndHunt(HuntEndReason.Manual); break;
        }

        Assert.IsFalse(_sut.IsHunting, end);
        _adapter.Received(1).ReleasePin(Party);
        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f), "the size bonus lapses");
        if (shown.HasValue)
            _adapter.Received(1).Announce(shown.Value, HuntTroopLists.WitchKingId);
        else
            _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());

        // The cooldown runs from the end: a forced restart is still allowed, an organic one is not.
        _sut.AddScore(FactionSide.Evil, 50f);
        _coop.IsSessionActive.Returns(false);
        _coop.ShouldDeferToHost.Returns(false);
        _dedicated.IsDedicatedServer.Returns(false);
        _settings.Enabled.Returns(true);
        _adapter.PlayerKingdomId.Returns(Gondor);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId));
        _sut.OnDailyTick();
        Assert.IsFalse(_sut.IsHunting, "cooldown after " + end);
    }

    // E03: every end gate also holds at the hunter's hourly tick, before the re-pin and the step. Vanilla sets Hold on
    // peace; without the gate the hourly re-issue would re-engage the player at peace for up to a day.
    [DataTestMethod]
    [DataRow("peace", HuntAnnouncement.Over)]
    [DataRow("kingdomless", HuntAnnouncement.Over)]
    [DataRow("disabled", HuntAnnouncement.Over)]
    [DataRow("timeout", HuntAnnouncement.Over)]
    [DataRow("dead", HuntAnnouncement.Over)]
    public void OnHourlyTickParty_EndGateHolds_EndsBeforeReengaging(string end, HuntAnnouncement shown)
    {
        StartWitchKingHunt();
        // Vanilla's Hold at peace: the party no longer engages the player.
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        switch (end)
        {
            case "peace": _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero(atWar: false)); break;
            case "kingdomless": _adapter.PlayerKingdomId.Returns((string?)null); break;
            case "disabled": _settings.Enabled.Returns(false); break;
            case "timeout": _now += 20d; break;
            case "dead":
                var hero = FreeHero();
                hero.IsAlive = false;
                _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(hero);
                break;
        }

        _sut.OnHourlyTickParty(Party);

        Assert.IsFalse(_sut.IsHunting, end);
        _adapter.Received(1).ReleasePin(Party);
        _adapter.Received(1).Announce(shown, HuntTroopLists.WitchKingId);
        _adapter.DidNotReceive().PinAndEngagePlayer(Party);
        _adapter.DidNotReceive().ReadStepState(Arg.Any<string>());
    }

    private void Daily(Action<HunterHeroSnapshot> change)
    {
        var hero = FreeHero();
        change(hero);
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(hero);
        _sut.OnDailyTick();
    }

    // E21: the size-rejection warning is once per hunt; the latch clears with the hunt, so the next hunt warns again.
    [TestMethod]
    public void GetHostSizeBonus_Rejected_WarnsOncePerHunt()
    {
        StartWitchKingHunt();
        _sut.GetHostSizeBonus(Party, float.NaN, 0f, 60, null, _ => 60f);
        _sut.GetHostSizeBonus(Party, float.NaN, 0f, 60, null, _ => 60f);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("Host size bonus")));

        _sut.ResetForNewSession();
        StartWitchKingHunt();
        _sut.GetHostSizeBonus(Party, float.NaN, 0f, 60, null, _ => 60f);

        _logger.Received(2).LogWarning(Arg.Is<string>(m => m.Contains("Host size bonus")));
    }

    [TestMethod]
    public void EndHunt_ClearsTheSnapshot_BeforeInvalidatingTheSpeed()
    {
        StartWitchKingHunt();
        InSight();
        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 0f, out _), "precondition: the floor is live");
        float floorAtInvalidate = -1f;
        _adapter.When(a => a.InvalidateSpeed(Party)).Do(_ =>
            floorAtInvalidate = _sut.TryGetSpeedFloor(Party, 0f, out var f) ? f : 0f);

        _sut.EndHunt(HuntEndReason.Manual);

        Assert.AreEqual(0f, floorAtInvalidate, "the recalculated speed must not see the old floor");
        _adapter.Received().InvalidateSpeed(Party);
    }

    [TestMethod]
    public void EndHunt_NoHunt_DoesNothing()
    {
        _sut.EndHunt(HuntEndReason.Manual);

        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
        _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnMapEventEnded_BattleWithoutTheHunter_LeavesTheHunt()
    {
        StartWitchKingHunt();

        Battle(MordorKingdom);

        Assert.IsTrue(_sut.IsHunting);
    }

    // E06 (maintainer, 2026-10-14): a player who gets away from the hunter keeps the hunt; nobody won that battle.
    [TestMethod]
    public void OnMapEventEnded_PlayerGetsAway_KeepsTheHunt_AndTheHourlyTickReengages()
    {
        StartWitchKingHunt();

        Battle(null, playerWon: false, involvesHunter: true, hasWinner: false);

        Assert.IsTrue(_sut.IsHunting, "a flee has no winner: the hunt goes on");
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
        _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());

        // The battle dropped the engage order; the hourly re-issue restores it.
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    // E06: a battle without the player that the hunter's side wins leaves the hunt running.
    [TestMethod]
    public void OnMapEventEnded_HunterWinsAThirdPartyBattle_KeepsTheHunt()
    {
        StartWitchKingHunt();

        Battle(null, playerWon: false, playerEvent: false, involvesHunter: true, hasWinner: true, hunterLost: false);

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    // E06: a battle without the player that nobody won leaves the hunt running too.
    [TestMethod]
    public void OnMapEventEnded_ThirdPartyBattleWithNoWinner_KeepsTheHunt()
    {
        StartWitchKingHunt();

        Battle(null, playerWon: false, playerEvent: false, involvesHunter: true, hasWinner: false);

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    // ---------------------------------------------------------------- E17: another lord's army

    [TestMethod]
    public void OnDailyTick_PlayerInAnotherLordsArmy_DefersTheStart_WithoutACooldown()
    {
        ArmWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);

        _sut.OnDailyTick();

        Assert.IsFalse(_sut.IsHunting, "no hunt while in another lord's army");
        _adapter.DidNotReceive().SpawnHunterParty(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());

        // Retried the next day once he has left the army: the deferral started no cooldown.
        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        _now += 1d;
        _sut.OnDailyTick();
        Assert.IsTrue(_sut.IsHunting, "the deferred hunt starts once the player leaves the army");
    }

    [TestMethod]
    public void TryStartHunt_Console_PlayerInAnotherLordsArmy_IsRefused()
    {
        ArmWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);

        string verdict = _sut.TryStartHunt(ignoreScoreAndCooldown: true);

        Assert.IsFalse(_sut.IsHunting, verdict);
        StringAssert.Contains(verdict, "another lord's army");
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerInAnotherLordsArmy_StandsOff_ThenResumes()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _adapter.PlayerInAnotherLordsArmy.Returns(true);

        _sut.OnHourlyTickParty(Party);
        _sut.OnHourlyTickParty(Party);

        Assert.IsTrue(_sut.IsHunting, "standing off is not an end");
        // The engage order is dropped through the end's release path, once, so normal AI resumes.
        _adapter.Received(1).ReleasePin(Party);
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _adapter.DidNotReceive().ReadStepState(Arg.Any<string>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
        StringAssert.Contains(string.Join("\n", _sut.Describe()), "Last hourly step: player in another lord's army");

        // He leaves the army: the next hour re-pins and re-engages exactly once, and steps again.
        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        _sut.OnHourlyTickParty(Party);

        _adapter.Received(1).PinAndEngagePlayer(Party);
        _adapter.Received(1).Relocate(Party, Arg.Any<float>(), Arg.Any<float>());
    }

    // E17: after the release the free AI may engage the player on its own; the resume still re-pins, exactly once.
    [TestMethod]
    public void OnHourlyTickParty_ResumeWhileTheFreeAiEngagesHim_StillRepinsOnce()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        _sut.OnHourlyTickParty(Party);

        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: true));
        _sut.OnHourlyTickParty(Party);
        _sut.OnHourlyTickParty(Party);

        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    // E17: a resume hour the hunter spends in a battle waits; the next free hour re-pins.
    [TestMethod]
    public void OnHourlyTickParty_ResumeWhileTheHunterIsInABattle_RepinsTheNextFreeHour()
    {
        StartWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        _sut.OnHourlyTickParty(Party);

        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        var fighting = FreeParty(HuntTroopLists.WitchKingId, engaging: true);
        fighting.InMapEvent = true;
        _adapter.ReadParty(Party).Returns(fighting);
        _sut.OnHourlyTickParty(Party);
        _adapter.DidNotReceive().PinAndEngagePlayer(Party);

        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: true));
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    // E17: the frame tick stands the hunter off at once (one release), and no speed floor applies in the army.
    [TestMethod]
    public void OnTick_PlayerInAnotherLordsArmy_ReleasesOnce_AndPublishesNoFloor()
    {
        StartWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);

        InSight(distance: 5f);
        InSight(distance: 5f);

        Assert.IsTrue(_sut.IsHunting);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _), "no floor while he rides in another lord's army");
        _adapter.Received(1).ReleasePin(Party);
    }

    // E17: out of the army, the floor waits for the hourly re-pin, then applies again.
    [TestMethod]
    public void OnTick_AfterTheArmy_FloorReturnsOnlyOnceTheHuntHoldsThePinAgain()
    {
        StartWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        InSight(distance: 5f);

        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        InSight(distance: 5f);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _), "the free AI is not the hunt: no floor yet");

        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _sut.OnHourlyTickParty(Party);
        InSight(distance: 5f);
        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 3f, out _), "re-pinned: the floor applies again");
    }

    // A paid safe passage (or an escape) makes vanilla promise the hunter will not attack the player for a while. The
    // hunt stands off for that span instead of re-engaging him every hour, then resumes.
    [TestMethod]
    public void OnHourlyTickParty_HunterSwornNotToAttack_StandsOff_ThenResumes()
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        var bought = FreeParty(HuntTroopLists.WitchKingId, engaging: true);
        bought.HoldsOffPlayer = true;
        _adapter.ReadParty(Party).Returns(bought);
        _adapter.ClearReceivedCalls();

        _sut.OnHourlyTickParty(Party);
        _sut.OnHourlyTickParty(Party);

        Assert.IsTrue(_sut.IsHunting, "a bribe pauses the hunt, it does not end it");
        _adapter.Received(1).ReleasePin(Party);
        _adapter.DidNotReceive().PinAndEngagePlayer(Arg.Any<string>());
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
        StringAssert.Contains(string.Join("\n", _sut.Describe()), "Last hourly step: hunter promised to leave you alone");

        // The promise runs out: the next hour re-pins and re-engages exactly once.
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _sut.OnHourlyTickParty(Party);

        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    // The frame tick sees the promise the moment the encounter ends, before the next hour re-engages him.
    [TestMethod]
    public void OnTick_HunterSwornNotToAttack_ReleasesOnce_AndPublishesNoFloor()
    {
        StartWitchKingHunt();
        _adapter.ReadSight(Party, Arg.Any<float>()).Returns(new HuntSightReading
        {
            Valid = true, HunterActive = true, PlayerActive = true, SeeingRange = 10f, Distance = 5f,
            PlayerSpeed = 4f, HunterHoldsOffPlayer = true,
        });

        _sut.OnTick();
        _sut.OnTick();

        Assert.IsTrue(_sut.IsHunting);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _), "no floor while he keeps his word");
        _adapter.Received(1).ReleasePin(Party);
    }

    // E17: an end during the stand-off keeps the hunt's other effects but never releases a second time; the timeout
    // still counts while standing off.
    [DataTestMethod]
    [DataRow("timeout")]
    [DataRow("peace")]
    [DataRow("manual")]
    [DataRow("beaten")]
    public void EndDuringTheStandOff_ReleasesOnlyOnce(string end)
    {
        StartWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).ReleasePin(Party);

        switch (end)
        {
            case "timeout":
                _now += 20d;
                _sut.OnHourlyTickParty(Party);
                break;
            case "peace": Daily(h => h.AtWarWithPlayerKingdom = false); break;
            case "manual": _sut.EndHunt(HuntEndReason.Manual); break;
            case "beaten": Battle(MordorKingdom, involvesHunter: true, hunterLost: true); break;
        }

        Assert.IsFalse(_sut.IsHunting, end);
        _adapter.Received(1).ReleasePin(Party);
        _adapter.Received(1).Announce(end == "beaten" ? HuntAnnouncement.Won : HuntAnnouncement.Over, HuntTroopLists.WitchKingId);
    }

    // E17: a save taken during the stand-off holds no pin; after the load the hunt knows it, so it re-pins on resume and
    // its end releases nothing a second time.
    [TestMethod]
    public void OnSessionLaunched_SavedDuringTheStandOff_RepinsOnResume_AndEndsWithoutASecondRelease()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, HuntStartDay = 1000f,
        });
        _adapter.ReadHero(HuntTroopLists.WitchKingId).Returns(FreeHero());
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: true));
        _sut.OnSessionLaunched();

        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).PinAndEngagePlayer(Party);

        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        _sut.OnHourlyTickParty(Party);
        _sut.EndHunt(HuntEndReason.Manual);
        _adapter.Received(1).ReleasePin(Party);
    }

    [TestMethod]
    public void OnSessionLaunched_SavedWhilePinned_EndReleasesThePin()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, HuntStartDay = 1000f,
        });
        var pinned = FreeParty(HuntTroopLists.WitchKingId);
        pinned.AiPinned = true;
        _adapter.ReadParty(Party).Returns(pinned);
        _sut.OnSessionLaunched();

        _sut.EndHunt(HuntEndReason.Manual);

        _adapter.Received(1).ReleasePin(Party);
    }

    [TestMethod]
    public void OnHourlyTickParty_PlayerLeadsHisOwnArmy_KeepsHunting()
    {
        // The adapter's fact is false for the player's own army; nothing else stands the hunter off.
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _adapter.PlayerInAnotherLordsArmy.Returns(false);

        _sut.OnHourlyTickParty(Party);

        _adapter.Received(1).PinAndEngagePlayer(Party);
        _adapter.Received(1).Relocate(Party, Arg.Any<float>(), Arg.Any<float>());
    }

    [TestMethod]
    public void OnPartyDestroyed_AnotherParty_LeavesTheHunt()
    {
        StartWitchKingHunt();

        _sut.OnPartyDestroyed("someone_else");

        Assert.IsTrue(_sut.IsHunting);
    }

    // ---------------------------------------------------------------- the sight snapshot and the speed floor

    private void InSight(float playerSpeed = 4f, float distance = 5f, float sight = 10f)
    {
        _adapter.ReadSight(Party, Arg.Any<float>()).Returns(new HuntSightReading
        {
            Valid = true, HunterActive = true, PlayerActive = true, SeeingRange = sight, Distance = distance,
            PlayerSpeed = playerSpeed,
        });
        _sut.OnTick();
    }

    [TestMethod]
    public void TryGetSpeedFloor_InSight_IsThePlayersSpeedTimesTheMultiplier()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: 4f);

        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 3f, out var floor));
        Assert.AreEqual(5.2f, floor, 1e-4f);
    }

    [TestMethod]
    public void TryGetSpeedFloor_AnotherParty_None()
    {
        StartWitchKingHunt();
        InSight();

        Assert.IsFalse(_sut.TryGetSpeedFloor("someone_else", 3f, out _));
        Assert.IsFalse(_sut.TryGetSpeedFloor(null, 3f, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_OutOfSight_None()
    {
        StartWitchKingHunt();
        InSight(distance: 16f, sight: 10f);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    // E09 (maintainer, 2026-10-14): the floor applies from 1.5 S inward, the same margin the steps keep, so a moving
    // player cannot be shadowed between S and 1.5 S. S = 10 here, so the edge is 15.
    [DataTestMethod]
    [DataRow(12f, true)]
    [DataRow(14.99f, true)]
    [DataRow(15f, true)]
    [DataRow(15.01f, false)]
    [DataRow(float.NaN, false)]
    public void TryGetSpeedFloor_FromOneAndAHalfSightsInward(float distance, bool floored)
    {
        StartWitchKingHunt();
        InSight(distance: distance, sight: 10f);

        Assert.AreEqual(floored, _sut.TryGetSpeedFloor(Party, 3f, out var floor), $"distance {distance}");
        if (floored) Assert.AreEqual(5.2f, floor, 1e-4f);
    }

    [TestMethod]
    public void OnTick_SearchesForTheHunterOutToTheSightMargin()
    {
        StartWitchKingHunt();

        _sut.OnTick();

        _adapter.Received(1).ReadSight(Party, EyeOfSauronService.SightMargin);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void TryGetSpeedFloor_NonFiniteMultiplier_None(float k)
    {
        StartWitchKingHunt();
        _settings.HunterSpeedMultiplier.Returns(k);
        InSight();

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void TryGetSpeedFloor_NonFinitePlayerSpeed_None(float speed)
    {
        StartWitchKingHunt();
        InSight(playerSpeed: speed);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    public void OnTick_NonFiniteDistanceOrSight_IsOutOfSight(float bad)
    {
        StartWitchKingHunt();
        InSight(distance: bad);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));

        InSight(sight: bad);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_FloorNotAboveTheCurrentSpeed_None()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: 4f);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 6f, out _));
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, float.NaN, out _));
    }

    [TestMethod]
    public void TryGetSpeedFloor_NeverUnderOne()
    {
        StartWitchKingHunt();
        InSight(playerSpeed: 0.5f);

        Assert.IsTrue(_sut.TryGetSpeedFloor(Party, 0.2f, out var floor));
        Assert.AreEqual(1f, floor);
    }

    [DataTestMethod]
    [DataRow("player at sea")]
    [DataRow("player in battle")]
    [DataRow("hunter in battle")]
    [DataRow("player inactive")]
    [DataRow("hunter inactive")]
    [DataRow("reading invalid")]
    public void OnTick_ExcludedState_IsOutOfSight(string state)
    {
        StartWitchKingHunt();
        var r = new HuntSightReading
        {
            Valid = state != "reading invalid", HunterActive = state != "hunter inactive",
            PlayerActive = state != "player inactive", SeeingRange = 10f, Distance = 5f, PlayerSpeed = 4f,
            PlayerAtSea = state == "player at sea", PlayerInMapEvent = state == "player in battle",
            HunterInMapEvent = state == "hunter in battle",
        };
        _adapter.ReadSight(Party, Arg.Any<float>()).Returns(r);

        _sut.OnTick();

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _), state);
    }

    [TestMethod]
    public void OnTick_SightFlipsOrSpeedMoves_InvalidatesTheHuntersSpeed_OtherwiseNot()
    {
        StartWitchKingHunt();

        InSight(playerSpeed: 4f);
        _adapter.Received(1).InvalidateSpeed(Party);

        InSight(playerSpeed: 4.1f);
        _adapter.Received(1).InvalidateSpeed(Party);

        InSight(playerSpeed: 5f);
        _adapter.Received(2).InvalidateSpeed(Party);

        InSight(distance: 20f);
        _adapter.Received(3).InvalidateSpeed(Party);
    }

    [TestMethod]
    public void OnTick_NoHunt_ReadsNothing()
    {
        _sut.OnTick();

        _adapter.DidNotReceive().ReadSight(Arg.Any<string>(), Arg.Any<float>());
    }

    // ---------------------------------------------------------------- the size bonus

    [TestMethod]
    public void GetHostSizeBonus_AnotherParty_ZeroAndTheRosterIsNeverWalked()
    {
        StartWitchKingHunt();
        bool walked = false;

        float bonus = _sut.GetHostSizeBonus("someone_else", 50f, 0f, 200, null, _ => { walked = true; return 300f; });

        Assert.AreEqual(0f, bonus);
        Assert.IsFalse(walked);
    }

    [TestMethod]
    public void GetHostSizeBonus_NoHunt_Zero() => Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 50f, 0f, 200, null, _ => 300f));

    [TestMethod]
    public void GetHostSizeBonus_HostOverTheLimit_LiftsItToTheWeightedCount_WithHalfASlotOfHeadroom()
    {
        StartWitchKingHunt();

        Assert.AreEqual(250.5f, _sut.GetHostSizeBonus(Party, 50f, 0.5f, 200, null, _ => 299.2f), 1e-3f);
    }

    [TestMethod]
    public void GetHostSizeBonus_WeightedBelowRaw_UsesTheRawCount()
    {
        StartWitchKingHunt();

        Assert.AreEqual(150.5f, _sut.GetHostSizeBonus(Party, 50f, 0f, 200, null, _ => 120f), 1e-3f);
    }

    [TestMethod]
    public void GetHostSizeBonus_NaNWeighted_UsesTheRawCount()
    {
        StartWitchKingHunt();

        Assert.AreEqual(150.5f, _sut.GetHostSizeBonus(Party, 50f, 0f, 200, null, _ => float.NaN), 1e-3f);
    }

    [TestMethod]
    public void GetHostSizeBonus_LimitAlreadyCoversTheHost_Zero()
    {
        StartWitchKingHunt();

        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 300f, 0f, 200, null, _ => 300f));
    }

    [DataTestMethod]
    [DataRow(float.NaN, 0f)]
    [DataRow(float.PositiveInfinity, 0f)]
    [DataRow(50f, float.NaN)]
    [DataRow(50f, -1f)]
    public void GetHostSizeBonus_RejectedInput_Zero(float limit, float sumOfFactors)
    {
        StartWitchKingHunt();

        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, limit, sumOfFactors, 200, null, _ => 300f));
    }

    [TestMethod]
    public void GetHostSizeBonus_HugeDeficit_ClampsToTheMaxFlatBonus()
    {
        StartWitchKingHunt();

        Assert.AreEqual(TAOM.Features.AiPartySize.AiPartySizeService.MaxFlatBonus,
            _sut.GetHostSizeBonus(Party, 1f, 0f, 200, null, _ => 5e6f));
    }

    // ---------------------------------------------------------------- session reset and persistence

    [TestMethod]
    public void ResetForNewSession_ClearsScoresTheHuntAndTheSnapshot()
    {
        StartWitchKingHunt();
        InSight();

        _sut.ResetForNewSession();

        Assert.IsFalse(_sut.IsHunting);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 0f, out _));
        Assert.AreEqual(0f, _sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f));
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    [TestMethod]
    public void ResetForNewSession_ClearsTheCooldown()
    {
        StartWitchKingHunt();
        _sut.EndHunt(HuntEndReason.Manual);

        _sut.ResetForNewSession();
        ArmWitchKingHunt();
        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
    }

    /// <summary>Distinct non-default values in every field, so a dropped or swapped field fails the round trip.</summary>
    internal static EyeOfSauronState Seed() => new()
    {
        ScoreVsEvil = 11f,
        ScoreVsFree = 7f,
        LastDecayDay = 990f,
        CooldownUntilDay = 1040f,
        HunterHeroId = HuntTroopLists.WitchKingId,
        HunterPartyId = "wk_party",
        HuntStartDay = 995f,
    };

    internal static void AssertSeedIsComplete(EyeOfSauronState seed)
    {
        foreach (var f in typeof(EyeOfSauronState).GetFields())
        {
            object? value = f.GetValue(seed);
            object? empty = f.FieldType == typeof(string) ? string.Empty : Activator.CreateInstance(f.FieldType);
            Assert.IsFalse(value == null || value.Equals(empty), $"Seed() must give {f.Name} a non-default value");
        }
    }

    internal static void AssertSameRecord(EyeOfSauronState expected, EyeOfSauronState actual)
    {
        foreach (var f in typeof(EyeOfSauronState).GetFields())
            Assert.AreEqual(f.GetValue(expected), f.GetValue(actual), f.Name);
    }

    [TestMethod]
    public void CaptureState_ThenLoadFrom_RoundTripsEveryField()
    {
        var seed = Seed();
        AssertSeedIsComplete(seed);

        _sut.LoadFrom(seed);

        AssertSameRecord(seed, _sut.CaptureState());
    }

    [TestMethod]
    public void LoadFrom_Null_LoadsNothing()
    {
        StartWitchKingHunt();

        _sut.LoadFrom(null!);

        Assert.IsFalse(_sut.IsHunting);
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void LoadFrom_HeroIdWithoutAPartyId_LoadsNoHunt_ButKeepsTheScores()
    {
        var seed = Seed();
        seed.HunterPartyId = string.Empty;
        _now = 990d;

        _sut.LoadFrom(seed);

        Assert.IsFalse(_sut.IsHunting);
        Assert.AreEqual(11f, _sut.GetScore(FactionSide.Evil), 1e-3f);
    }

    [TestMethod]
    public void LoadFrom_DropsTheOldSightState_ButKeepsTheHostRegistered()
    {
        StartWitchKingHunt();
        InSight();
        var saved = _sut.CaptureState();

        _sut.LoadFrom(saved);

        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 0f, out _), "no in-sight state survives a load");
        Assert.IsTrue(_sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f) > 0f, "the loaded host is not shed");
    }

    [TestMethod]
    public void LoadFrom_NonFiniteScores_LoadAsZero()
    {
        _sut.LoadFrom(new EyeOfSauronState { ScoreVsEvil = float.NaN, ScoreVsFree = float.PositiveInfinity, LastDecayDay = 1000f });

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Free));
    }

    [TestMethod]
    public void OnSessionLaunched_HuntersPartyGone_EndsTheHunt()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, HuntStartDay = 1000f,
        });

        _sut.OnSessionLaunched();

        Assert.IsFalse(_sut.IsHunting);
    }

    [TestMethod]
    public void OnSessionLaunched_CoopSessionLive_EndsTheRestoredHunt()
    {
        _sut.LoadFrom(Seed());
        var pinned = FreeParty(HuntTroopLists.WitchKingId);
        pinned.AiPinned = true;
        _adapter.ReadParty("wk_party").Returns(pinned);
        _coop.ShouldDeferToHost.Returns(true);

        _sut.OnSessionLaunched();

        Assert.IsFalse(_sut.IsHunting);
        _adapter.Received(1).ReleasePin("wk_party");
        _adapter.DidNotReceive().Announce(Arg.Any<HuntAnnouncement>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnMapEventEnded_CoopPeer_ScoresNoDeed()
    {
        _coop.IsSessionActive.Returns(true);

        Battle(MordorKingdom, DeedKind.Assault);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void OnSessionLaunched_HuntersPartyThere_KeepsTheHunt()
    {
        _sut.LoadFrom(new EyeOfSauronState
        {
            HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, HuntStartDay = 1000f,
        });
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId));

        _sut.OnSessionLaunched();

        Assert.IsTrue(_sut.IsHunting);
    }

    // ---------------------------------------------------------------- Codex review 2026-10-14

    // F3: a save taken during a stand-off holds no pin. Loaded into co-op, the launch-time end must not release a pin
    // the hunt no longer owns (the release drops orders and forces a rethink).
    [TestMethod]
    public void OnSessionLaunched_StandOffSaveLoadedIntoCoop_EndsWithoutReleasing()
    {
        _sut.LoadFrom(Seed());
        _adapter.ReadParty("wk_party").Returns(FreeParty(HuntTroopLists.WitchKingId));
        _coop.IsSessionActive.Returns(true);

        _sut.OnSessionLaunched();

        Assert.IsFalse(_sut.IsHunting);
        _adapter.DidNotReceive().ReleasePin(Arg.Any<string>());
    }

    // F1: while standing off, the hunter's own AI may lead an army, attach to one, enter a town, besiege or take
    // another order. The engage call cannot undo any of that, so the resume claims no pin: it waits, unpinned (no
    // floor, no step, no second release at the end), and retries every hour until he is free.
    [DataTestMethod]
    [DataRow("army leader")]
    [DataRow("attached army member")]
    [DataRow("in a settlement")]
    [DataRow("besieging")]
    [DataRow("pinned by another order")]
    public void Resume_HunterUnavailable_ClaimsNoPin_AndRetriesHourly(string state)
    {
        StartWitchKingHunt();
        StraightPathTo(100f, 10f);
        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).ReleasePin(Party);

        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        var busy = FreeParty(HuntTroopLists.WitchKingId, engaging: false);
        switch (state)
        {
            // The adapter reads a leader (Army set) and an attached member (Army and AttachedTo set) as InArmy.
            case "army leader": busy.InArmy = true; break;
            case "attached army member": busy.InArmy = true; busy.IsEngagingPlayer = true; break;
            case "in a settlement": busy.InSettlement = true; break;
            case "besieging": busy.Besieging = true; break;
            case "pinned by another order": busy.AiPinned = true; break;
        }
        _adapter.ReadParty(Party).Returns(busy);
        _sut.OnHourlyTickParty(Party);
        _sut.OnHourlyTickParty(Party);

        _adapter.DidNotReceive().PinAndEngagePlayer(Party);
        _adapter.DidNotReceive().Relocate(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<float>());
        InSight(distance: 5f);
        Assert.IsFalse(_sut.TryGetSpeedFloor(Party, 3f, out _), $"{state}: the hunt holds no pin, so no floor");

        // Free again: the next hour re-pins once.
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, engaging: false));
        _sut.OnHourlyTickParty(Party);
        _adapter.Received(1).PinAndEngagePlayer(Party);
    }

    [TestMethod]
    public void Resume_HunterInAnArmy_EndReleasesNothingASecondTime()
    {
        StartWitchKingHunt();
        _adapter.PlayerInAnotherLordsArmy.Returns(true);
        _sut.OnHourlyTickParty(Party);

        _adapter.PlayerInAnotherLordsArmy.Returns(false);
        var inArmy = FreeParty(HuntTroopLists.WitchKingId);
        inArmy.InArmy = true;
        _adapter.ReadParty(Party).Returns(inArmy);
        _sut.OnHourlyTickParty(Party);
        _sut.EndHunt(HuntEndReason.Manual);

        _adapter.Received(1).ReleasePin(Party);
    }

    // F2: PartySizeLimit is cached per MemberRoster.VersionNo, which MobileParty.UpdateVersionNo does not touch. Every
    // membership change invalidates it, after the snapshot changed, even when no troop is added.
    [TestMethod]
    public void TryStartHunt_AddingNoTroops_StillInvalidatesThePartySize_AfterRegistering()
    {
        ArmWitchKingHunt();
        // Strength 200 is over 1.3 times the player's 100: the top-up adds nobody.
        _adapter.ReadParty(Party).Returns(FreeParty(HuntTroopLists.WitchKingId, heads: 50, strength: 200f));
        float bonusAtInvalidate = -1f;
        _adapter.When(a => a.InvalidatePartySize(Party)).Do(_ =>
            bonusAtInvalidate = _sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f));

        _sut.OnDailyTick();

        Assert.IsTrue(_sut.IsHunting);
        _adapter.DidNotReceive().AddTroops(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
        _adapter.Received(1).InvalidatePartySize(Party);
        Assert.IsTrue(bonusAtInvalidate > 0f, "the recalculated limit must already see the hunter's host");
    }

    [DataTestMethod]
    [DataRow("manual")]
    [DataRow("timeout")]
    [DataRow("party destroyed")]
    public void EndHunt_InvalidatesThePartySize_AfterClearing(string end)
    {
        StartWitchKingHunt();
        float bonusAtInvalidate = -1f;
        _adapter.When(a => a.InvalidatePartySize(Party)).Do(_ =>
            bonusAtInvalidate = _sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f));

        switch (end)
        {
            case "manual": _sut.EndHunt(HuntEndReason.Manual); break;
            case "timeout": _now += 20d; _sut.OnHourlyTickParty(Party); break;
            case "party destroyed": _sut.OnPartyDestroyed(Party); break;
        }

        Assert.IsFalse(_sut.IsHunting, end);
        _adapter.Received(1).InvalidatePartySize(Party);
        Assert.AreEqual(0f, bonusAtInvalidate, "the recalculated limit must not see the old host");
    }

    [TestMethod]
    public void TryStartHunt_PinRefusedAfterRegistering_InvalidatesThePartySize_AfterClearing()
    {
        ArmWitchKingHunt();
        _adapter.PinAndEngagePlayer(Party).Returns(false);
        var bonuses = new System.Collections.Generic.List<float>();
        _adapter.When(a => a.InvalidatePartySize(Party)).Do(_ =>
            bonuses.Add(_sut.GetHostSizeBonus(Party, 10f, 0f, 60, null, _ => 60f)));

        _sut.TryStartHunt(ignoreScoreAndCooldown: false);

        Assert.IsFalse(_sut.IsHunting);
        Assert.AreEqual(0f, bonuses.Count > 0 ? bonuses[bonuses.Count - 1] : -1f, "the last invalidation follows the refused start's clear");
        Assert.AreEqual(2, bonuses.Count, "once at registration, once at the clear");
    }

    // ---------------------------------------------------------------- console helpers

    [TestMethod]
    public void AddScore_NeverGoesBelowZero()
    {
        _sut.AddScore(FactionSide.Evil, 5f);
        _sut.AddScore(FactionSide.Evil, -50f);

        Assert.AreEqual(0f, _sut.GetScore(FactionSide.Evil));
    }

    [TestMethod]
    public void AddScore_NonFinite_IsIgnored()
    {
        _sut.AddScore(FactionSide.Evil, 5f);
        _sut.AddScore(FactionSide.Evil, float.NaN);

        Assert.AreEqual(5f, _sut.GetScore(FactionSide.Evil), 1e-4f);
    }

    [TestMethod]
    public void Describe_NamesTheHuntAndTheScores()
    {
        StartWitchKingHunt();
        _adapter.HeroName(HuntTroopLists.WitchKingId).Returns("Witch-king of Angmar");

        var text = string.Join("\n", _sut.Describe());

        StringAssert.Contains(text, "Witch-king of Angmar");
        StringAssert.Contains(text, Party);
        StringAssert.Contains(text, "engaging you: yes");
        StringAssert.Contains(text, "12.0");
    }
}
