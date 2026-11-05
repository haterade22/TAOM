using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat.Domain;

[TestClass]
public class RunStatsTests
{
    [TestMethod]
    public void LostInClearedWaves_SumsTheClosedRows_AndLeavesOutTheWaveInProgress()
    {
        var stats = new RunStats();
        stats.DefenderLost();
        stats.DefenderLost();
        stats.CloseWave(1, 10f, 0);
        stats.DefenderLost();
        stats.CloseWave(2, 10f, 0);

        stats.DefenderLost();

        Assert.AreEqual(3, stats.LostInClearedWaves);
    }

    [TestMethod]
    public void CloseWave_RecordsTheWavesCountsAndStartsTheNext()
    {
        var s = new RunStats();
        s.AttackerKilled();
        s.AttackerKilled();
        s.DefenderLost();
        s.CloseWave(1, 40f, felled: 0);
        s.AttackerKilled();
        s.CloseWave(2, 25f, felled: 3);

        Assert.AreEqual(2, s.Waves.Count);
        Assert.AreEqual(2, s.Waves[0].Kills);
        Assert.AreEqual(1, s.Waves[0].Lost);
        Assert.AreEqual(40f, s.Waves[0].Seconds);
        Assert.AreEqual(1, s.Waves[1].Kills);
        Assert.AreEqual(3, s.Waves[1].Felled);
        Assert.AreEqual(3, s.TotalKills);
        Assert.AreEqual(1, s.WavesFelled);
    }

    [TestMethod]
    public void WavesFelled_CountsAWaveWhereOnlySummonsOrTheCommanderWereFelled()
    {
        var s = new RunStats();
        var uses = new System.Collections.Generic.Dictionary<string, int>();
        s.CloseWave(1, 40f, felled: 0);
        s.CloseWave(2, 40f, felled: 0, roles: new WaveRoles(5, 5, 3, 2, null));
        var felledCommander = new CommanderRecord("lugdush", "captain", 0, CommanderOutcome.Felled, uses, 0, 30f);
        s.CloseWave(3, 40f, felled: 0, roles: new WaveRoles(0, 0, 0, 0, felledCommander));
        var killedCommander = felledCommander with { Outcome = CommanderOutcome.Killed };
        s.CloseWave(4, 40f, felled: 0, roles: new WaveRoles(0, 0, 0, 0, killedCommander));

        Assert.AreEqual(2, s.WavesFelled);
    }

    [TestMethod]
    public void FleeingPeak_KeepsTheLargestObservation()
    {
        var s = new RunStats();

        s.ObserveFleeing(2);
        s.ObserveFleeing(9);
        s.ObserveFleeing(4);

        Assert.AreEqual(9, s.FleeingPeak);
    }

    [TestMethod]
    public void Fled_AndPlayerDeaths_Count()
    {
        var s = new RunStats();

        s.AttackerFled();
        s.PlayerDied();
        s.PlayerDied();

        Assert.AreEqual(1, s.Fled);
        Assert.AreEqual(2, s.PlayerDeaths);
    }
}
