using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.TrainingGrounds.Waves;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>
/// The wave fight's mission side (#1529) against a fake yard: when a wave starts, how it spawns, when it ends and how the player
/// gets back to the master. One test moves each field the director reads. The engine half (spawns, fades, the controller) is the
/// adapter's and is proven in the game.
/// </summary>
[TestClass]
public class WaveFightDirectorTests
{
    private WaveBetFixture _f = null!;
    private FakeArena _arena = null!;
    private WaveFightDirector _director = null!;
    private float _now;

    private WaveBetService Bets => _f.Service;

    [TestInitialize]
    public void Setup()
    {
        _f = new WaveBetFixture();
        Bets.DetachArena();
        _arena = new FakeArena();
        _director = new WaveFightDirector(Bets, _arena, _f.Config, _f.Outcomes, _f.Logger, _ => 0);
        _now = 100f;
    }

    private void Tick(float dt = 0.1f)
    {
        _now += dt;
        _director.Tick(_now);
    }

    /// <summary>Attaches the yard and stakes on the wave in hand, as the master's dialogue does; the conversation then ends.</summary>
    private void StakeAndLeave(int stake = 200)
    {
        if (!Bets.ArenaAttached)
            _director.Attach();
        Bets.PrepareRun();
        Assert.IsTrue(Bets.PlaceStake(stake), "the stake was refused");
        _arena.InConversation = false;
        Bets.OnConversationEnded();
    }

    private void StartFight(int stake = 200)
    {
        StakeAndLeave(stake);
        Tick();
        Assert.AreEqual(WaveRunState.Fighting, Bets.RunState);
    }

    /// <summary>Kills every fighter on the field each second until the wave resolves.</summary>
    private void FightToTheEnd()
    {
        for (var i = 0; i < 500 && Bets.RunState == WaveRunState.Fighting; i++)
        {
            _arena.Kill(_arena.ActiveFighters);
            Tick(1f);
        }

        Assert.AreNotEqual(WaveRunState.Fighting, Bets.RunState, "the wave never resolved");
    }

    private void WaitForTheReturn()
    {
        for (var i = 0; i < 20 && _arena.ReturnCalls == 0; i++)
            Tick(1f);
        Assert.AreEqual(1, _arena.ReturnCalls, "the player was never taken back to the master");
    }

    // ---- attach: the offer needs a yard with spawn frames

    [TestMethod]
    public void Attach_TwoFrames_AttachesTheYard()
    {
        _arena.FrameCount = 2;

        _director.Attach();

        Assert.IsTrue(Bets.ArenaAttached);
    }

    [TestMethod]
    public void Attach_OneFrame_LeavesTheYardDetached_SoTheOfferHides()
    {
        _arena.FrameCount = 1;

        _director.Attach();

        Assert.IsFalse(Bets.ArenaAttached);
        Assert.AreEqual(OfferRefusal.NotArena, Bets.Refusal);
        Assert.IsFalse(Bets.ShowsOffer);
        _f.Logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("1 spawn frame")));
    }

    [TestMethod]
    public void Attach_NoFrames_LeavesTheYardDetached()
    {
        _arena.FrameCount = 0;

        _director.Attach();

        Assert.IsFalse(Bets.ArenaAttached);
    }

    [TestMethod]
    public void Tick_YardWithoutFrames_NeverStartsAWave()
    {
        _arena.FrameCount = 1;
        _director.Attach();

        Tick();

        CollectionAssert.AreEqual(new string[0], _arena.Calls);
    }

    // ---- the start of a wave

    [TestMethod]
    public void Tick_WaveDue_ConversationOpen_WaitsThenStartsOnceItEnds()
    {
        StakeAndLeave();
        _arena.InConversation = true;

        Tick();
        Assert.AreEqual(WaveRunState.WaveDue, Bets.RunState);
        CollectionAssert.AreEqual(new string[0], _arena.Calls);

        _arena.InConversation = false;
        Tick();
        Assert.AreEqual(WaveRunState.Fighting, Bets.RunState);
    }

    [TestMethod]
    public void Tick_WaveDue_StandsVanillaDown_ThenSpawnsThePlayer_ThenFights()
    {
        StakeAndLeave();

        Tick();

        CollectionAssert.AreEqual(new[] { "standdown", "player:1", "fighter" }, _arena.Calls);
        Assert.AreEqual(WaveRunState.Fighting, Bets.RunState);
        Assert.IsTrue(_director.FightInProgress);
        Assert.AreEqual((WaveAnnouncement.WaveStart, 1, 3), _arena.Announcements.Single());
    }

    [TestMethod]
    public void Tick_WaveDue_TheWeaponStageIsTheRowsStage_ForThePlayerAndForEachFighter()
    {
        _f.Config.GetTable().Returns(StageTable(3));
        StakeAndLeave();

        Tick();

        CollectionAssert.Contains(_arena.Calls, "player:3");
        Assert.AreEqual(3, _arena.Spawns.Single().Stage);
    }

    private static WaveTable StageTable(int stage) => new(
        new[] { new WaveRow(4, 1, new System.Collections.Generic.Dictionary<int, int> { [3] = 1 }, stage, 500, 15) },
        WaveRules.Default);

    private void WaitForNextReturn(int calls)
    {
        for (var i = 0; i < 20 && _arena.ReturnCalls < calls; i++)
            Tick(1f);
        Assert.AreEqual(calls, _arena.ReturnCalls, "the player was never taken back to the master");
    }

    [TestMethod]
    public void Tick_WaveDue_StandDownLogsThePassWithTheFadedCountAndVanillasCount()
    {
        _arena.Faded = 6;
        StakeAndLeave();

        Tick();

        _f.Outcomes.Received().Pass(TrainingWavesChecks.StandDown, "", Arg.Is<string>(d => d.Contains("faded=6") && d.Contains("vanilla_spawned=30")));
    }

    [TestMethod]
    public void Tick_WaveDue_VanillaStillCountsOtherThan30_LogsTheFail_AndStillFights()
    {
        _arena.VanillaSpawnedCount = 4;
        StakeAndLeave();

        Tick();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.StandDown, "", Arg.Is<string>(d => d.Contains("vanilla_spawned=4")));
        Assert.AreEqual(WaveRunState.Fighting, Bets.RunState);
    }

    [TestMethod]
    public void Tick_WaveDue_EmptyRoster_RefundsTheStake_AndTouchesNothing()
    {
        _arena.Troops.Clear();
        StakeAndLeave(300);

        Tick();

        Assert.AreEqual(WaveRunState.Ended, Bets.RunState);
        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        CollectionAssert.AreEqual(new string[0], _arena.Calls);
        CollectionAssert.AreEqual(new[] { (WaveAnnouncement.Refunded, 1, 300) }, _arena.Announcements);
    }

    [TestMethod]
    public void Tick_WaveDue_StandDownFails_RefundsTheStake_AndDoesNotReturn()
    {
        _arena.StandDownResult = false;
        StakeAndLeave(300);

        Tick();

        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(0, _arena.ReturnCalls);
        CollectionAssert.DoesNotContain(_arena.Calls, "player:1");
        CollectionAssert.AreEqual(new[] { (WaveAnnouncement.Refunded, 1, 300) }, _arena.Announcements);
    }

    [TestMethod]
    public void Tick_WaveDue_PlayerSpawnFails_RefundsTheStake_AndBringsHimBackToTheMaster()
    {
        _arena.SpawnPlayerResult = false;
        StakeAndLeave(300);

        Tick();

        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(1, _arena.ReturnCalls);
        CollectionAssert.DoesNotContain(_arena.Calls, "fighter");
        CollectionAssert.AreEqual(new[] { (WaveAnnouncement.Refunded, 1, 300) }, _arena.Announcements);
    }

    [TestMethod]
    public void Tick_NoRun_DoesNothing()
    {
        _director.Attach();

        Tick();

        CollectionAssert.AreEqual(new string[0], _arena.Calls);
    }

    [TestMethod]
    public void Tick_MissionEnded_StopsEverything()
    {
        StakeAndLeave();
        _arena.MissionEnded = true;

        Tick();

        Assert.AreEqual(WaveRunState.WaveDue, Bets.RunState);
        CollectionAssert.AreEqual(new string[0], _arena.Calls);
    }

    [TestMethod]
    public void Tick_MissionEnded_StopsAFightInProgress()
    {
        StartFight();
        _arena.MissionEnded = true;
        _arena.Calls.Clear();

        Tick(10f);

        CollectionAssert.AreEqual(new string[0], _arena.Calls);
        Assert.AreEqual(WaveRunState.Fighting, Bets.RunState);
    }

    // ---- the spawn rhythm

    [TestMethod]
    public void Tick_Fighting_SpawnsOneFighterForEachTick_AndStopsAtAtOnce()
    {
        StartFight(); // wave 1: 3 fighters, 1 at once; the first spawned with the start

        Tick();
        Tick();

        Assert.AreEqual(1, _arena.Spawns.Count);
        Assert.AreEqual(1, _arena.ActiveFighters);
    }

    [TestMethod]
    public void Tick_Fighting_RefillsOnlyAfterTheRefillSeconds()
    {
        StartFight(); // spawned at _now; refill is 3 s
        var firstSpawnAt = _now;
        _arena.Kill();

        _now = firstSpawnAt + 2f;
        _director.Tick(_now);
        Assert.AreEqual(1, _arena.Spawns.Count, "refilled before the refill time");

        _now = firstSpawnAt + 3f;
        _director.Tick(_now);
        Assert.AreEqual(2, _arena.Spawns.Count);
    }

    [TestMethod]
    public void Tick_Fighting_TheRefillClockRunsFromTheLastSpawn_NotFromTheFall()
    {
        StartFight(); // the one fighter spawned at t = _now; refill is 3 s
        var spawnedAt = _now;
        _now = spawnedAt + 10f;
        _arena.Kill(); // falls at t = 10, 7 s after the refill time of the spawn

        _director.Tick(_now);

        Assert.AreEqual(2, _arena.Spawns.Count, "counted from the fall, the replacement would wait until t = 13");
    }

    [TestMethod]
    public void Tick_Fighting_TheFirstAtOnceFightersSpawnOnePerTickWithoutWaiting()
    {
        _f.Config.GetTable().Returns(TwoAtOnceTable());
        StartFight();

        Assert.AreEqual(1, _arena.Spawns.Count);
        Tick();
        Assert.AreEqual(2, _arena.Spawns.Count);
        Tick();
        Assert.AreEqual(2, _arena.Spawns.Count, "stood at the limit");
    }

    private static WaveTable TwoAtOnceTable() => new(
        new[] { new WaveRow(5, 2, new System.Collections.Generic.Dictionary<int, int> { [3] = 1 }, 1, 500, 15) },
        WaveRules.Default);

    [TestMethod]
    public void Tick_Fighting_NoRoom_SpawnsNothing_ThenSpawnsOnceThereIsRoom()
    {
        _arena.AgentRoom = false;
        StakeAndLeave();

        Tick();
        Tick();

        Assert.AreEqual(0, _arena.Spawns.Count);
        CollectionAssert.DoesNotContain(_arena.Calls, "fighter", "a spawn was attempted with no room");

        _arena.AgentRoom = true;
        Tick();
        Assert.AreEqual(1, _arena.Spawns.Count);
    }

    [TestMethod]
    public void Tick_Fighting_EachFighterIsAnIdFromTheRoster_WithTheRowsStage()
    {
        _arena.Troops = new() { new PoolTroop("only_t3", 3), new PoolTroop("only_t4", 4) };
        StartFight();

        Assert.AreEqual(("only_t3", 1), _arena.Spawns.Single());
    }

    [TestMethod]
    public void Tick_Fighting_ASpawnThatFails_DropsThatFighter_AndTheWaveStillEnds()
    {
        _arena.Troops = new() { new PoolTroop("bad", 3) };
        _arena.FailingTroops.Add("bad");
        _arena.SpawnPlayerResult = true;
        StakeAndLeave();

        for (var i = 0; i < 20 && Bets.RunState != WaveRunState.Ended; i++)
            Tick(1f);

        // nothing ever spawned: the stake comes back, the wave is not paid for no fight
        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void Tick_Fighting_OneSpawnFails_TheRestSpawn_AndTheClearLogsTheWaveFail()
    {
        _arena.Troops = new() { new PoolTroop("bad", 3), new PoolTroop("good", 3) };
        _arena.FailingTroops.Add("bad");
        // per fighter the roster draws a tier (of 1) and then a troop (of 2): the first fighter is "bad", the other two "good"
        var draws = new System.Collections.Generic.Queue<int>(new[] { 0, 0, 0, 1, 0, 1 });
        _director = new WaveFightDirector(Bets, _arena, _f.Config, _f.Outcomes, _f.Logger, _ => draws.Dequeue());
        StakeAndLeave();

        for (var i = 0; i < 30 && Bets.RunState != WaveRunState.BetweenWaves; i++)
        {
            _arena.Kill(_arena.ActiveFighters);
            Tick(1f);
        }

        Assert.AreEqual(WaveRunState.BetweenWaves, Bets.RunState);
        _f.Outcomes.Received().Fail(TrainingWavesChecks.Wave, "", Arg.Is<string>(d => d.Contains("count=3")));
    }

    // ---- the end of a wave

    [TestMethod]
    public void Tick_AllFightersSpawnedAndBeaten_PaysTheWave_AndLogsTheWaveCheck()
    {
        StartFight(200);

        FightToTheEnd();

        Assert.AreEqual(WaveRunState.BetweenWaves, Bets.RunState);
        Assert.AreEqual(100_000 - 200 + 300, _f.Purse.Gold);
        Assert.AreEqual(3, _arena.Spawns.Count);
        _f.Outcomes.Received().Pass(TrainingWavesChecks.Wave, "", Arg.Is<string>(d => d.Contains("spawned=3") && d.Contains("count=3") && d.Contains("peak=1") && d.Contains("at_once=1")));
        Assert.AreEqual((WaveAnnouncement.WaveCleared, 1, 300), _arena.Announcements.Last());
    }

    [TestMethod]
    public void Tick_TheWaveLine_HasTheExactShapeTheManifestReads()
    {
        StartFight(200);

        FightToTheEnd();

        _f.Outcomes.Received(1).Pass(TrainingWavesChecks.Wave, "", "wave=1 spawned=3 count=3 peak=1 at_once=1 cleared=true");
    }

    [TestMethod]
    public void Tick_TheWavesPeakNeverExceedsAtOnce()
    {
        _f.Config.GetTable().Returns(TwoAtOnceTable());
        StartFight();

        for (var i = 0; i < 100 && Bets.RunState == WaveRunState.Fighting; i++)
        {
            if (i % 2 == 1)
                _arena.Kill();
            Tick(1f);
            Assert.IsTrue(_arena.ActiveFighters <= 2, "more than AtOnce stood at once");
        }

        Assert.AreEqual(2, _arena.Peak);
        Assert.AreEqual(5, _arena.Spawns.Count);
    }

    [TestMethod]
    public void Tick_FightersStillStanding_NothingIsPaid()
    {
        StartFight();
        for (var i = 0; i < 5; i++)
            Tick(1f);

        Assert.AreEqual(WaveRunState.Fighting, Bets.RunState);
        Assert.AreEqual(100_000 - 200, _f.Purse.Gold);
    }

    [TestMethod]
    public void Tick_PlayerDown_LosesTheWave_TheTownKeepsTheStake()
    {
        StartFight(250);
        _arena.PlayerDown = true;

        Tick();

        Assert.AreEqual(WaveRunEnd.Lost, Bets.Run!.EndReason);
        _f.Adapter.Received(1).GiveToTown(250);
        Assert.AreEqual(100_000 - 250, _f.Purse.Gold);
        Assert.AreEqual((WaveAnnouncement.WaveLost, 1, 250), _arena.Announcements.Last());
    }

    [TestMethod]
    public void Tick_PlayerDownAndTheLastFighterBeatenInOneTick_PlayerDownWins()
    {
        StartFight(250);
        while (_arena.Spawns.Count < 3)
        {
            _arena.Kill();
            Tick(1f);
        }

        _arena.Kill(_arena.ActiveFighters);
        _arena.PlayerDown = true;
        Tick();

        Assert.AreEqual(WaveRunEnd.Lost, Bets.Run!.EndReason);
        Assert.AreEqual(100_000 - 250, _f.Purse.Gold);
    }

    [TestMethod]
    public void Tick_PlayerDown_NoFurtherFighterSpawns()
    {
        StartFight();
        _arena.PlayerDown = true;
        Tick();
        var spawned = _arena.Spawns.Count;

        Tick(10f);

        Assert.AreEqual(spawned, _arena.Spawns.Count);
    }

    // ---- the way back to the master

    [TestMethod]
    public void Tick_AfterAClear_ReturnsThePlayerOnlyAfterTheReturnSeconds()
    {
        StartFight();
        FightToTheEnd();
        var clearedAt = _now;

        _now = clearedAt + 4f;
        _director.Tick(_now);
        Assert.AreEqual(0, _arena.ReturnCalls, "returned before the return seconds");

        _now = clearedAt + 5f;
        _director.Tick(_now);
        Assert.AreEqual(1, _arena.ReturnCalls);
    }

    [TestMethod]
    public void Tick_TheReturn_MarksTheDebriefBeforeItOpensTheConversation()
    {
        var pendingAtReturn = false;
        _arena.OnReturn = () => pendingAtReturn = Bets.DebriefPending;
        StartFight();
        FightToTheEnd();

        WaitForTheReturn();

        Assert.IsTrue(pendingAtReturn, "the debrief flag must be set before the master speaks");
        Assert.AreEqual(DebriefKind.Cleared, Bets.PendingDebrief);
    }

    [TestMethod]
    public void Tick_TheReturn_LogsThePassWhenTheConversationIsOpenAndThePlayerIsActive()
    {
        StartFight();
        FightToTheEnd();

        WaitForTheReturn();

        _f.Outcomes.Received().Pass(TrainingWavesChecks.Return, "", Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_TheReturnLine_HasTheExactShapeTheManifestReads()
    {
        StartFight();
        FightToTheEnd();

        WaitForTheReturn();

        _f.Outcomes.Received(1).Pass(TrainingWavesChecks.Return, "", "opened=true in_conversation=true player_down=false");
    }

    [TestMethod]
    public void Tick_TheReturn_ThePlayerHasNoBodyAfterIt_LogsTheFail()
    {
        _arena.PlayerDownAfterReturn = true;
        StartFight();
        FightToTheEnd();

        WaitForTheReturn();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.Return, "", Arg.Any<string>());
        _f.Outcomes.DidNotReceive().Pass(TrainingWavesChecks.Return, Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_TheReturn_NoConversationOpenedAfterIt_LogsTheFail()
    {
        _arena.ReturnOpensNoConversation = true;
        StartFight();
        FightToTheEnd();

        WaitForTheReturn();

        _f.Outcomes.Received().Fail(TrainingWavesChecks.Return, "", Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_TheReturnFails_BetweenWaves_WithdrawsTheRun_AndLogsTheFail()
    {
        _arena.ReturnResult = false;
        StartFight();
        FightToTheEnd();

        WaitForTheReturn();

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
        _f.Outcomes.Received().Fail(TrainingWavesChecks.Return, "", Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_AfterALoss_ReturnsThePlayer_WithTheLostDebriefDue()
    {
        var pending = DebriefKind.None;
        _arena.OnReturn = () => pending = Bets.PendingDebrief;
        StartFight(250);
        _arena.PlayerDown = true;
        Tick();

        WaitForTheReturn();

        Assert.AreEqual(WaveRunEnd.Lost, Bets.Run!.EndReason);
        Assert.AreEqual(DebriefKind.Lost, pending);
    }

    [TestMethod]
    public void Tick_AfterTheReturn_NothingMoreHappensUntilANewStake()
    {
        StartFight();
        FightToTheEnd();
        WaitForTheReturn();
        _arena.Calls.Clear();

        Tick(30f);

        CollectionAssert.AreEqual(new string[0], _arena.Calls);
        Assert.AreEqual(1, _arena.ReturnCalls);
    }

    [TestMethod]
    public void Tick_ARunPastTheLastWave_Completes_AndTheMasterOwesTheLastDebrief()
    {
        StartFight(500);
        var waves = 0;
        while (Bets.RunState != WaveRunState.Ended && waves < 12)
        {
            FightToTheEnd();
            WaitForNextReturn(++waves);
            if (Bets.RunState == WaveRunState.Ended)
                break;
            _arena.InConversation = false;
            Assert.IsTrue(Bets.PlaceStake(Bets.StakeChoice(0)), "wave " + (waves + 1));
            Bets.OnConversationEnded();
            Tick();
        }

        Assert.AreEqual(8, waves);
        Assert.AreEqual(WaveRunEnd.Completed, Bets.Run!.EndReason);
        Assert.AreEqual(DebriefKind.Completed, Bets.PendingDebrief);
        Assert.AreEqual(Bets.Run.Net, _f.Purse.Gold - 100_000);
    }

    // ---- the end of the mission

    [TestMethod]
    public void MissionEnding_Fighting_ForfeitsTheStakeToTheTown()
    {
        StartFight(250);

        _director.MissionEnding();

        Assert.AreEqual(WaveRunEnd.Forfeited, Bets.Run!.EndReason);
        _f.Adapter.Received(1).GiveToTown(250);
        Assert.IsFalse(Bets.ArenaAttached);
    }

    [TestMethod]
    public void MissionEnding_WaveDue_RefundsTheStake()
    {
        StakeAndLeave(300);

        _director.MissionEnding();

        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void MissionEnding_WaveDue_AnnouncesTheRefund()
    {
        StakeAndLeave(300);

        _director.MissionEnding();

        CollectionAssert.Contains(_arena.Announcements, (WaveAnnouncement.Refunded, 1, 300));
    }

    [TestMethod]
    public void EndWave_NoFighterCouldSpawn_AnnouncesTheRefund()
    {
        _arena.Troops = new() { new PoolTroop("bad", 3) };
        _arena.FailingTroops.Add("bad");
        StakeAndLeave(300);

        for (var i = 0; i < 20 && Bets.RunState != WaveRunState.Ended; i++)
            Tick(1f);

        CollectionAssert.Contains(_arena.Announcements, (WaveAnnouncement.Refunded, 1, 300));
    }

    [TestMethod]
    public void Fault_TheReturnToTheMasterThrows_TheRefundIsStillAnnounced()
    {
        _arena.SpawnPlayerThrows = true;
        StakeAndLeave(300);
        Assert.ThrowsException<System.InvalidOperationException>(() => Tick());
        _arena.OnReturn = () => throw new System.InvalidOperationException("the return failed");

        Assert.ThrowsException<System.InvalidOperationException>(() => _director.Fault());

        CollectionAssert.Contains(_arena.Announcements, (WaveAnnouncement.Refunded, 1, 300));
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void Fault_AfterAReturnThatThrew_TriesTheReturnAgain()
    {
        StartFight();
        FightToTheEnd();
        var throwOnce = true;
        _arena.OnReturn = () =>
        {
            if (!throwOnce)
                return;
            throwOnce = false;
            throw new System.InvalidOperationException("the return failed part way");
        };
        _now += 5f;
        Assert.ThrowsException<System.InvalidOperationException>(() => _director.Tick(_now));

        _director.Fault();

        Assert.AreEqual(2, _arena.ReturnCalls, "the first return threw part way, so the fault tries it once more");
    }

    [TestMethod]
    public void MissionEnding_BetweenWaves_WithdrawsWithTheWinnings()
    {
        StartFight(200);
        FightToTheEnd();

        _director.MissionEnding();

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
        Assert.AreEqual(100_100, _f.Purse.Gold);
    }

    [TestMethod]
    public void MissionEnding_NoStakeYet_EndsTheIdleRun()
    {
        _director.Attach();
        Bets.PrepareRun();

        _director.MissionEnding();

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
    }

    [TestMethod]
    public void MissionEnding_NoRun_DoesNotThrow()
    {
        _director.Attach();

        _director.MissionEnding();

        Assert.IsFalse(Bets.ArenaAttached);
    }

    [TestMethod]
    public void Detach_CalledTwice_IsSafe_AndSettlesAFightOnce()
    {
        StartFight(250);

        _director.Detach();
        _director.Detach();

        _f.Adapter.Received(1).GiveToTown(250);
        Assert.IsFalse(Bets.ArenaAttached);
    }

    [TestMethod]
    public void MissionEnding_ThenDetach_SettlesOnce()
    {
        StartFight(250);

        _director.MissionEnding();
        _director.Detach();

        _f.Adapter.Received(1).GiveToTown(250);
    }

    [TestMethod]
    public void Tick_AfterDetach_DoesNothing()
    {
        StartFight();
        _director.Detach();
        _arena.Calls.Clear();

        Tick(10f);

        CollectionAssert.AreEqual(new string[0], _arena.Calls);
    }

    // ---- a fault in the tick

    [TestMethod]
    public void Fault_Fighting_RefundsTheStake_ForfeitsNothing_AndBringsThePlayerBack()
    {
        StartFight(250);

        _director.Fault();

        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        _f.Adapter.DidNotReceiveWithAnyArgs().GiveToTown(default);
        Assert.AreEqual(1, _arena.ReturnCalls);
        Assert.IsFalse(Bets.ArenaAttached, "the offer must be hidden once the yard is in an unknown state");
        CollectionAssert.Contains(_arena.Announcements, (WaveAnnouncement.Refunded, 1, 250));
    }

    [TestMethod]
    public void Fault_WaveDue_RefundsTheStake()
    {
        StakeAndLeave(300);

        _director.Fault();

        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(0, _arena.ReturnCalls, "vanilla was not stood down, so there is nothing to put back");
        CollectionAssert.AreEqual(new[] { (WaveAnnouncement.Refunded, 1, 300) }, _arena.Announcements);
    }

    [TestMethod]
    public void Fault_BetweenWaves_WithdrawsWithTheWinnings()
    {
        StartFight(200);
        FightToTheEnd();

        _director.Fault();

        Assert.AreEqual(WaveRunEnd.Withdrew, Bets.Run!.EndReason);
        Assert.AreEqual(100_100, _f.Purse.Gold);
        Assert.IsFalse(_arena.Announcements.Any(a => a.Kind == WaveAnnouncement.Refunded), "no stake came back, so no refund notice");
    }

    [TestMethod]
    public void Fault_TheStandDownThrowsAfterItFadedTheFighters_ReturnsThePlayerToTheMaster()
    {
        _arena.StandDownThrows = true;
        StakeAndLeave(300);
        Assert.ThrowsException<System.InvalidOperationException>(() => Tick());

        _director.Fault();

        Assert.AreEqual(1, _arena.ReturnCalls, "vanilla's yard was part stood down, so the player goes back to the master");
        Assert.AreEqual(100_000, _f.Purse.Gold);
    }

    [TestMethod]
    public void Fault_AWaveIsDueAfterTheStandDown_ReturnsThePlayerToTheMaster_AndRefundsTheStake()
    {
        _arena.SpawnPlayerThrows = true;
        StakeAndLeave(300);
        Assert.ThrowsException<System.InvalidOperationException>(() => Tick());
        Assert.AreEqual(WaveRunState.WaveDue, Bets.RunState, "the wave is still due: the stand-down is done, the fight never began");

        _director.Fault();

        Assert.AreEqual(1, _arena.ReturnCalls);
        Assert.AreEqual(WaveRunEnd.Refunded, Bets.Run!.EndReason);
        Assert.AreEqual(100_000, _f.Purse.Gold);
        CollectionAssert.Contains(_arena.Announcements, (WaveAnnouncement.Refunded, 1, 300));
    }

    [TestMethod]
    public void Fault_AConversationIsOpen_DoesNotOpenASecondOne()
    {
        StartFight();
        _arena.InConversation = true;

        _director.Fault();

        Assert.AreEqual(0, _arena.ReturnCalls);
    }

    [TestMethod]
    public void Fault_ThenTick_DoesNothing()
    {
        StartFight();
        _director.Fault();
        _arena.Calls.Clear();

        Tick(10f);

        CollectionAssert.AreEqual(new string[0], _arena.Calls);
    }

    [TestMethod]
    public void Fault_CalledTwice_SettlesOnce()
    {
        StartFight(250);

        _director.Fault();
        _director.Fault();

        Assert.AreEqual(100_000, _f.Purse.Gold);
        Assert.AreEqual(1, _arena.ReturnCalls);
    }

    [TestMethod]
    public void FightInProgress_OnlyWhileFighting()
    {
        _director.Attach();
        Assert.IsFalse(_director.FightInProgress);

        StakeAndLeave();
        Assert.IsFalse(_director.FightInProgress, "a wave that is only due is not a fight");

        Tick();
        Assert.IsTrue(_director.FightInProgress);

        _arena.PlayerDown = true;
        Tick();
        Assert.IsFalse(_director.FightInProgress);
    }
}
