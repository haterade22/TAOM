using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.Domain;
using TAOM.Features.WarOfTheRingMomentum.Models;
using TAOM.Features.WarOfTheRingMomentum.Snapshots;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

[TestClass]
public class MomentumEventServiceTests
{
    private IMomentumConfigProvider _configProvider = null!;
    private IPlayerMomentumService _playerService = null!;
    private IKingdomStrengthAdapter _strengthAdapter = null!;
    private IMomentumTextService _textService = null!;
    private MomentumWarState _state = null!;
    private MomentumConfig _config = null!;
    private MomentumEventService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _configProvider = Substitute.For<IMomentumConfigProvider>();
        _config = new MomentumConfig();
        // Isolate the battle-won math tests from the enemies-killed source (kills fire on every
        // battle); the dedicated kill-momentum tests below re-enable it explicitly.
        _config.Events.KillMomentumPerHundred = 0;
        _configProvider.GetConfig().Returns(_config);

        _playerService = Substitute.For<IPlayerMomentumService>();
        _playerService.GetParticipationMultiplier(true).Returns(1.5f);
        _playerService.GetParticipationMultiplier(false).Returns(1.0f);
        _playerService.IsPlayerOnStrongerSide(Arg.Any<MomentumWarState>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(false);

        _strengthAdapter = Substitute.For<IKingdomStrengthAdapter>();
        _textService = Substitute.For<IMomentumTextService>();
        _textService.BattleWonDescription(default, default, default, default, default)
            .ReturnsForAnyArgs("battle");
        _textService.SiegeDescription(default, default, default).ReturnsForAnyArgs("siege");
        _textService.RaidDescription(default, default, default).ReturnsForAnyArgs("raid");
        _textService.ArmyGatheredDescription(default).ReturnsForAnyArgs("army");
        _textService.StrengthDescription(default).ReturnsForAnyArgs("strength");
        _textService.KillsDescription(default).ReturnsForAnyArgs("kills");

        _state = new MomentumWarState();
        _state.MarkWarStarted();
        _state.Free.AddKingdom("empire_w");
        _state.Free.AddKingdom("vlandia");
        _state.Evil.AddKingdom("empire_s");
        _state.Evil.AddKingdom("isengard");

        _sut = new MomentumEventService(_configProvider, _playerService, _strengthAdapter, _textService);
    }

    private static BattleOutcomeSnapshot ValidBattle()
    {
        // empire_s (attacker, evil) beats empire_w (defender, free): 500 free casualties.
        return new BattleOutcomeSnapshot
        {
            HasWinner = true,
            AttackerWon = true,
            AttackerFactionId = "empire_s",
            DefenderFactionId = "empire_w",
            AttackerIsKingdomFaction = true,
            DefenderIsKingdomFaction = true,
            DefenderIsMobileParty = true,
            AttackerCasualties = 100,
            DefenderCasualties = 500,
            IsValidBattleType = true,
            PlayerInvolved = false,
            AttackerFactionName = "Mordor",
            DefenderFactionName = "Gondor",
            AttackerLeaderName = "Gothmog",
            DefenderLeaderName = "Boromir",
        };
    }

    private void SetSideStrengths(float freePerKingdom, float evilPerKingdom)
    {
        _strengthAdapter.GetTotalStrength("empire_w").Returns(freePerKingdom);
        _strengthAdapter.GetTotalStrength("vlandia").Returns(freePerKingdom);
        _strengthAdapter.GetTotalStrength("empire_s").Returns(evilPerKingdom);
        _strengthAdapter.GetTotalStrength("isengard").Returns(evilPerKingdom);
    }

    // ---- ProcessBattle: short-circuit filters ----

    [TestMethod]
    public void ProcessBattle_WarNotStarted_NoChange()
    {
        var fresh = new MomentumWarState();
        fresh.Free.AddKingdom("empire_w");
        fresh.Evil.AddKingdom("empire_s");

        _sut.ProcessBattle(ValidBattle(), fresh, 100.0);

        Assert.AreEqual(0, fresh.Evil.SideMomentum);
        Assert.AreEqual(0, fresh.Evil.TotalStats.TotalKills);
    }

    [TestMethod]
    public void ProcessBattle_WarEnded_NoChange()
    {
        SetSideStrengths(1000f, 1000f);
        _state.MarkWarEnded(TAOM.Features.Diplomacy.Models.WarOutcome.FreeVictory);

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessBattle_NoWinner_NoChange()
    {
        var battle = ValidBattle();
        battle.HasWinner = false;

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
        Assert.AreEqual(0, _state.Evil.TotalStats.TotalKills);
    }

    [TestMethod]
    public void ProcessBattle_AttackerNotEnrolled_NoChange()
    {
        var battle = ValidBattle();
        battle.AttackerFactionId = "umbar";

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(0, _state.Free.TotalStats.TotalKills);
        Assert.AreEqual(0, _state.Evil.TotalStats.TotalKills);
    }

    [TestMethod]
    public void ProcessBattle_NullFactionId_NoChange()
    {
        var battle = ValidBattle();
        battle.DefenderFactionId = null;

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    // ---- ProcessBattle: stats-before-filter ordering (LOTRAOM parity) ----

    [TestMethod]
    public void ProcessBattle_InvalidBattleType_AddsKillStatsButNoMomentum()
    {
        SetSideStrengths(1000f, 1000f);
        var battle = ValidBattle();
        battle.IsValidBattleType = false;

        _sut.ProcessBattle(battle, _state, 100.0);

        // Evil killed 500 (defender casualties), Free killed 100 (attacker casualties).
        Assert.AreEqual(500, _state.Evil.TotalStats.TotalKills);
        Assert.AreEqual(100, _state.Free.TotalStats.TotalKills);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
        Assert.AreEqual(0, _state.Evil.GetEvents(MomentumActionType.BattleWon).Count());
    }

    [TestMethod]
    public void ProcessBattle_DefenderNotMobile_NoMomentum()
    {
        SetSideStrengths(1000f, 1000f);
        var battle = ValidBattle();
        battle.DefenderIsMobileParty = false;

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessBattle_NonKingdomFaction_NoMomentum()
    {
        SetSideStrengths(1000f, 1000f);
        var battle = ValidBattle();
        battle.AttackerIsKingdomFaction = false;

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    // ---- ProcessBattle: momentum math ----

    [TestMethod]
    public void ProcessBattle_Valid_WinnerGainsRoundedCasualtyShare()
    {
        // Loser = Free side, strength 2000 total; 500 casualties → 0.25 × 300 × 100 = 7500 raw to Evil.
        SetSideStrengths(1000f, 1000f);

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        Assert.AreEqual(7500, _state.Evil.SideMomentum);
        Assert.AreEqual(0, _state.Free.SideMomentum);
        var ev = _state.Evil.GetEvents(MomentumActionType.BattleWon).Single();
        Assert.AreEqual(7500, ev.Value);
        Assert.AreEqual(100.0 + 504.0, ev.EndTimeHours, 0.0001);
    }

    [TestMethod]
    public void ProcessBattle_DefenderWins_DefenderSideGains()
    {
        SetSideStrengths(1000f, 1000f);
        var battle = ValidBattle();
        battle.AttackerWon = false;
        // Loser = Evil side (attacker), 100 casualties / 2000 → round(0.05 × 300 × 100) = 1500 raw to Free.

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(1500, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessBattle_ZeroLoserSideStrength_AddsZeroValueEvent()
    {
        // LOTRAOM parity: gain computes to 0 but the event is still enqueued.
        SetSideStrengths(0f, 1000f);

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
        Assert.AreEqual(1, _state.Evil.GetEvents(MomentumActionType.BattleWon).Count());
    }

    [TestMethod]
    public void ProcessBattle_PlayerInvolved_MultipliesAndRecordsPlayerEvent()
    {
        SetSideStrengths(1000f, 1000f);
        var battle = ValidBattle();
        battle.PlayerInvolved = true;

        _sut.ProcessBattle(battle, _state, 100.0);

        // (int)(7500 × 1.5) = 11250 — truncating cast, LOTRAOM parity.
        Assert.AreEqual(11250, _state.Evil.SideMomentum);
        _playerService.Received(1).RecordPlayerEvent(MomentumActionType.BattleWon);
    }

    [TestMethod]
    public void ProcessBattle_PlayerNotInvolved_NoPlayerEventRecorded()
    {
        SetSideStrengths(1000f, 1000f);

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        _playerService.DidNotReceive().RecordPlayerEvent(Arg.Any<MomentumActionType>());
    }

    [TestMethod]
    public void ProcessBattle_CasualtiesExceedLoserStrength_CapsAtMaxBattleMomentum()
    {
        // Endgame: Free side reduced to 50 total strength (2 kingdoms × 25), 500 casualties.
        // Ratio 10 must clamp to 1.0 → exactly MaxBattleMomentum(300) × 100 = 30000, NOT 300000.
        SetSideStrengths(25f, 1000f);

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        Assert.AreEqual(30000, _state.Evil.SideMomentum);
    }

    // ---- ProcessBattle: enemies-killed momentum ----

    [TestMethod]
    public void ProcessBattle_KillMomentumEnabled_BothSidesScoreForKills()
    {
        SetSideStrengths(1000f, 1000f);
        _config.Events.KillMomentumPerHundred = 10;

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        // Evil (attacker) killed 500 → 500×10 = 5000; Free (defender) killed 100 → 1000.
        var evilKill = _state.Evil.GetEvents(MomentumActionType.EnemiesKilled).Single();
        var freeKill = _state.Free.GetEvents(MomentumActionType.EnemiesKilled).Single();
        Assert.AreEqual(5000, evilKill.Value);
        Assert.AreEqual(1000, freeKill.Value);
        Assert.AreEqual(100.0 + 504.0, evilKill.EndTimeHours, 0.0001);
    }

    [TestMethod]
    public void ProcessBattle_KillMomentumEnabled_AccruesEvenForInvalidBattleType()
    {
        // Kill momentum tracks the kill STAT, which accrues before the validity filter.
        SetSideStrengths(1000f, 1000f);
        _config.Events.KillMomentumPerHundred = 10;
        var battle = ValidBattle();
        battle.IsValidBattleType = false;

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(5000, _state.Evil.GetEvents(MomentumActionType.EnemiesKilled).Single().Value);
        Assert.AreEqual(1000, _state.Free.GetEvents(MomentumActionType.EnemiesKilled).Single().Value);
        Assert.AreEqual(0, _state.Evil.GetEvents(MomentumActionType.BattleWon).Count());
    }

    [TestMethod]
    public void ProcessBattle_KillMomentumDisabled_NoEnemiesKilledEvents()
    {
        SetSideStrengths(1000f, 1000f);
        _config.Events.KillMomentumPerHundred = 0;

        _sut.ProcessBattle(ValidBattle(), _state, 100.0);

        Assert.AreEqual(0, _state.Evil.GetEvents(MomentumActionType.EnemiesKilled).Count());
        Assert.AreEqual(0, _state.Free.GetEvents(MomentumActionType.EnemiesKilled).Count());
    }

    [TestMethod]
    public void ProcessBattle_KillMomentumEnabled_SideThatKilledNobodyGetsNoEvent()
    {
        SetSideStrengths(1000f, 1000f);
        _config.Events.KillMomentumPerHundred = 10;
        var battle = ValidBattle();
        battle.AttackerCasualties = 0; // Free (defender) killed nobody

        _sut.ProcessBattle(battle, _state, 100.0);

        Assert.AreEqual(1, _state.Evil.GetEvents(MomentumActionType.EnemiesKilled).Count());
        Assert.AreEqual(0, _state.Free.GetEvents(MomentumActionType.EnemiesKilled).Count());
    }

    // ---- ProcessSiege ----

    [TestMethod]
    public void ProcessSiege_EnrolledCaptor_AddsFixedValueAndCaptureStat()
    {
        var siege = new SiegeOutcomeSnapshot
        {
            CaptorFactionId = "empire_w",
            CaptorFactionName = "Gondor",
            CaptorLeaderName = "Boromir",
            SettlementName = "Osgiliath",
        };

        _sut.ProcessSiege(siege, _state, 200.0);

        Assert.AreEqual(25000, _state.Free.SideMomentum);
        Assert.AreEqual(1, _state.Free.TotalStats.TotalSettlementsCaptured);
        var ev = _state.Free.GetEvents(MomentumActionType.Sieges).Single();
        Assert.AreEqual(200.0 + 504.0, ev.EndTimeHours, 0.0001);
    }

    [TestMethod]
    public void ProcessSiege_NotEnrolled_NoChange()
    {
        var siege = new SiegeOutcomeSnapshot { CaptorFactionId = "umbar" };

        _sut.ProcessSiege(siege, _state, 200.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessSiege_PlayerParty_MultipliedAndRecorded()
    {
        var siege = new SiegeOutcomeSnapshot { CaptorFactionId = "empire_w", PlayerInvolved = true };

        _sut.ProcessSiege(siege, _state, 200.0);

        Assert.AreEqual(37500, _state.Free.SideMomentum); // (int)(25000 × 1.5)
        _playerService.Received(1).RecordPlayerEvent(MomentumActionType.Sieges);
    }

    // ---- ProcessRaid ----

    [TestMethod]
    public void ProcessRaid_EnrolledAttackerVictory_AddsRaidValueAndStat()
    {
        var raid = new RaidOutcomeSnapshot
        {
            AttackerVictory = true,
            AttackerFactionId = "isengard",
            AttackerPartyName = "Uruk warband",
            AttackerFactionName = "Isengard",
            SettlementName = "Westfold village",
        };

        _sut.ProcessRaid(raid, _state, 300.0);

        Assert.AreEqual(20000, _state.Evil.SideMomentum);
        Assert.AreEqual(1, _state.Evil.TotalStats.TotalVillagesRaided);
    }

    [TestMethod]
    public void ProcessRaid_DefenderVictory_NoChange()
    {
        var raid = new RaidOutcomeSnapshot { AttackerVictory = false, AttackerFactionId = "isengard" };

        _sut.ProcessRaid(raid, _state, 300.0);

        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessRaid_UnenrolledRaider_NoChange()
    {
        // Deliberate deviation from LOTRAOM: bandit/looter raids never feed a side.
        var raid = new RaidOutcomeSnapshot { AttackerVictory = true, AttackerFactionId = "looters" };

        _sut.ProcessRaid(raid, _state, 300.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
        Assert.AreEqual(0, _state.Evil.TotalStats.TotalVillagesRaided);
    }

    // ---- ProcessArmyGathered ----

    [TestMethod]
    public void ProcessArmyGathered_Enrolled_AddsArmyValue()
    {
        var army = new ArmyGatheredSnapshot { KingdomId = "vlandia", ArmyLeaderName = "Théoden" };

        _sut.ProcessArmyGathered(army, _state, 400.0);

        Assert.AreEqual(20000, _state.Free.SideMomentum);
        var ev = _state.Free.GetEvents(MomentumActionType.ArmyGathered).Single();
        Assert.AreEqual(400.0 + 168.0, ev.EndTimeHours, 0.0001);
    }

    [TestMethod]
    public void ProcessArmyGathered_NotEnrolledOrNull_NoChange()
    {
        _sut.ProcessArmyGathered(new ArmyGatheredSnapshot { KingdomId = "umbar" }, _state, 400.0);
        _sut.ProcessArmyGathered(new ArmyGatheredSnapshot { KingdomId = null }, _state, 400.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    // ---- ProcessDailyTick: decay + strength award ----

    [TestMethod]
    public void ProcessDailyTick_DrainsExpiredEventsOnBothSides()
    {
        SetSideStrengths(0f, 0f); // suppress the strength award
        _state.Free.AddEvent(new MomentumEvent(100, "old", MomentumActionType.BattleWon, 50.0));
        _state.Evil.AddEvent(new MomentumEvent(80, "old", MomentumActionType.Sieges, 60.0));

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessDailyTick_StrengthAward_MaxRatio_GivesFullValue()
    {
        // Free 4000 vs Evil 1000 → ratio 4 = configured max → +300 internal = 30000 raw to Free.
        SetSideStrengths(2000f, 500f);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(30000, _state.Free.SideMomentum);
        var ev = _state.Free.GetEvents(MomentumActionType.RelativeStrength).Single();
        Assert.AreEqual(100.0 + 12.0, ev.EndTimeHours, 0.0001);
    }

    [TestMethod]
    public void ProcessDailyTick_StrengthAward_PartialRatio_Scales()
    {
        // Free 2500 vs Evil 1000 → ratio 2.5, excess 1.5 / 3 = 0.5 → (int)(0.5 × 300 × 100) = 15000 raw.
        SetSideStrengths(1250f, 500f);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(15000, _state.Free.SideMomentum);
    }

    [TestMethod]
    public void ProcessDailyTick_EvilStronger_EvilGains()
    {
        SetSideStrengths(500f, 2000f);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(30000, _state.Evil.SideMomentum);
        Assert.AreEqual(0, _state.Free.SideMomentum);
    }

    [TestMethod]
    public void ProcessDailyTick_EitherSideZeroStrength_NoAward()
    {
        SetSideStrengths(1000f, 0f);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
        Assert.AreEqual(0, _state.Evil.SideMomentum);
    }

    [TestMethod]
    public void ProcessDailyTick_PlayerOnStrongerSide_Multiplied()
    {
        SetSideStrengths(2000f, 500f);
        _playerService.IsPlayerOnStrongerSide(Arg.Any<MomentumWarState>(), Arg.Any<float>(), Arg.Any<float>())
            .Returns(true);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(45000, _state.Free.SideMomentum); // (int)(30000 × 1.5)
    }

    [TestMethod]
    public void ProcessDailyTick_WarEnded_NoProcessing()
    {
        SetSideStrengths(2000f, 500f);
        _state.MarkWarEnded(TAOM.Features.Diplomacy.Models.WarOutcome.FreeVictory);

        _sut.ProcessDailyTick(_state, 100.0);

        Assert.AreEqual(0, _state.Free.SideMomentum);
    }
}
