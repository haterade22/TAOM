using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Tests.Features.TrainingGrounds.Waves;

/// <summary>The wave-bet state machine (#1529): every number and every move is plain integers, so no engine type is needed.</summary>
[TestClass]
public class WaveRunTests
{
    private const int Rich = 1_000_000;

    private static WaveRun NewRun(int tournamentCap = 15_000, int perkFactor = 1) =>
        new(WaveTable.Default, tournamentCap, perkFactor);

    private static WaveRow Row(int cap, int odds = 15) =>
        new(3, 1, new Dictionary<int, int> { [3] = 1 }, 1, cap, odds);

    private static WaveTable Table(WaveRules rules, params WaveRow[] rows) => new(rows, rules);

    private static WaveRun Fighting(WaveRun run, int stake = 100)
    {
        Assert.IsTrue(run.TryStake(stake, Rich));
        Assert.IsTrue(run.BeginFight());
        return run;
    }

    /// <summary>A run at the start of the given wave: each earlier wave staked at its limit and cleared.</summary>
    private static WaveRun AtWave(int wave, int tournamentCap = 15_000, int perkFactor = 1)
    {
        var run = NewRun(tournamentCap, perkFactor);
        while (run.Wave < wave)
        {
            Assert.IsTrue(run.TryStake(run.MaxStake, Rich));
            run.BeginFight();
            run.Clear();
        }

        return run;
    }

    private static string Snapshot(WaveRun r) =>
        $"{r.State}|{r.EndReason}|{r.Wave}|{r.Stake}|{r.TotalStaked}|{r.TotalPaid}|{r.Net}|{r.LastStake}|{r.LastPayout}";

    private static WaveRun InState(WaveRunState state)
    {
        var run = NewRun();
        switch (state)
        {
            case WaveRunState.Idle:
                break;
            case WaveRunState.WaveDue:
                run.TryStake(100, Rich);
                break;
            case WaveRunState.Fighting:
                Fighting(run);
                break;
            case WaveRunState.BetweenWaves:
                Fighting(run);
                run.Clear();
                break;
            case WaveRunState.Ended:
                run.Withdraw();
                break;
        }

        Assert.AreEqual(state, run.State);
        return run;
    }

    [TestMethod]
    public void NewRun_StartsIdleAtWaveOne_WithMaxStake500()
    {
        var run = NewRun();

        Assert.AreEqual(WaveRunState.Idle, run.State);
        Assert.AreEqual(WaveRunEnd.None, run.EndReason);
        Assert.AreEqual(1, run.Wave);
        Assert.AreEqual(500, run.MaxStake);
        Assert.AreEqual(0, run.Stake);
        Assert.AreEqual(0L, run.Net);
        Assert.AreSame(WaveTable.Default.Waves[0], run.Row);
    }

    [TestMethod]
    public void TryStake_AboveTheCap_IsRefused_AndChangesNothing()
    {
        var run = NewRun();
        var before = Snapshot(run);

        Assert.IsFalse(run.CanStake(501, Rich));
        Assert.IsFalse(run.TryStake(501, Rich));
        Assert.AreEqual(before, Snapshot(run));
    }

    [TestMethod]
    public void TryStake_UnderTheMinimum_IsRefused_AndChangesNothing()
    {
        var run = NewRun();
        var before = Snapshot(run);

        Assert.IsFalse(run.TryStake(WaveRules.Default.MinStake - 1, Rich));
        Assert.IsFalse(run.TryStake(0, Rich));
        Assert.IsFalse(run.TryStake(-50, Rich));
        Assert.AreEqual(before, Snapshot(run));
    }

    [TestMethod]
    public void TryStake_AboveTheGold_IsRefused_AndChangesNothing()
    {
        var run = NewRun();
        var before = Snapshot(run);

        Assert.IsFalse(run.TryStake(300, 299));
        Assert.AreEqual(before, Snapshot(run));
        Assert.IsTrue(run.CanStake(300, 300));
    }

    [TestMethod]
    public void TryStake_AtTheCapAndTheMinimum_IsAccepted()
    {
        var low = NewRun();
        var high = NewRun();

        Assert.IsTrue(low.TryStake(WaveRules.Default.MinStake, Rich));
        Assert.IsTrue(high.TryStake(500, Rich));
        Assert.AreEqual(500, high.Stake);
    }

    [TestMethod]
    public void TryStake_Valid_GivesWaveDue_AndCountsTheStake()
    {
        var run = NewRun();

        Assert.IsTrue(run.TryStake(333, Rich));

        Assert.AreEqual(WaveRunState.WaveDue, run.State);
        Assert.AreEqual(333, run.Stake);
        Assert.AreEqual(333L, run.TotalStaked);
        Assert.AreEqual(-333L, run.Net);
    }

    [TestMethod]
    public void BeginFight_FromWaveDue_MovesToFighting()
    {
        var run = NewRun();
        run.TryStake(100, Rich);

        Assert.IsTrue(run.BeginFight());

        Assert.AreEqual(WaveRunState.Fighting, run.State);
        Assert.AreEqual(100, run.Stake);
    }

    [TestMethod]
    public void Clear_Stake333_Pays499_MovesToBetweenWaves_AndRaisesTheCap()
    {
        var run = AtWave(1);
        Fighting(run, 333);

        var paid = run.Clear();

        Assert.AreEqual(499, paid);
        Assert.AreEqual(WaveRunState.BetweenWaves, run.State);
        Assert.AreEqual(2, run.Wave);
        Assert.AreEqual(1000, run.MaxStake);
        Assert.AreEqual(0, run.Stake);
        Assert.AreEqual(333, run.LastStake);
        Assert.AreEqual(499, run.LastPayout);
        Assert.AreEqual(499L, run.TotalPaid);
        Assert.AreEqual(166L, run.Net);
    }

    [TestMethod]
    public void Clear_TheLastWave_EndsAsCompleted()
    {
        var run = AtWave(WaveTable.Default.Waves.Count);
        Fighting(run, run.MaxStake);

        run.Clear();

        Assert.AreEqual(WaveRunState.Ended, run.State);
        Assert.AreEqual(WaveRunEnd.Completed, run.EndReason);
        Assert.AreEqual(WaveTable.Default.Waves.Count, run.Wave);
    }

    [TestMethod]
    public void Lose_Fighting_EndsAsLost_AndReturnsTheStake()
    {
        var run = Fighting(NewRun(), 250);

        var lost = run.Lose();

        Assert.AreEqual(250, lost);
        Assert.AreEqual(WaveRunState.Ended, run.State);
        Assert.AreEqual(WaveRunEnd.Lost, run.EndReason);
        Assert.AreEqual(250, run.LastStake);
        Assert.AreEqual(0, run.Stake);
        Assert.AreEqual(-250L, run.Net);
    }

    [TestMethod]
    public void Forfeit_Fighting_EndsAsForfeited_AndReturnsTheStake()
    {
        var run = Fighting(NewRun(), 120);

        var lost = run.Forfeit();

        Assert.AreEqual(120, lost);
        Assert.AreEqual(WaveRunEnd.Forfeited, run.EndReason);
        Assert.AreEqual(WaveRunState.Ended, run.State);
        Assert.AreEqual(-120L, run.Net);
    }

    [TestMethod]
    public void Refund_WaveDue_EndsAsRefunded_TakesTheStakeOutOfTheTotal()
    {
        var run = NewRun();
        run.TryStake(400, Rich);

        var back = run.Refund();

        Assert.AreEqual(400, back);
        Assert.AreEqual(WaveRunState.Ended, run.State);
        Assert.AreEqual(WaveRunEnd.Refunded, run.EndReason);
        Assert.AreEqual(0L, run.TotalStaked);
        Assert.AreEqual(0L, run.Net);
        Assert.AreEqual(0, run.Stake);
    }

    [TestMethod]
    public void Refund_AfterAnEarlierWave_KeepsTheEarlierStakes()
    {
        var run = AtWave(1);
        Fighting(run, 200);
        run.Clear();
        run.TryStake(300, Rich);

        run.Refund();

        Assert.AreEqual(200L, run.TotalStaked);
        Assert.AreEqual(100L, run.Net);
    }

    [TestMethod]
    public void Abort_Fighting_EndsAsRefunded_TakesTheStakeOutOfTheTotal()
    {
        var run = Fighting(NewRun(), 400);

        var back = run.Abort();

        Assert.AreEqual(400, back);
        Assert.AreEqual(WaveRunState.Ended, run.State);
        Assert.AreEqual(WaveRunEnd.Refunded, run.EndReason);
        Assert.AreEqual(0L, run.TotalStaked);
        Assert.AreEqual(0L, run.Net);
        Assert.AreEqual(0, run.Stake);
    }

    [TestMethod]
    public void Abort_AfterAnEarlierWave_KeepsTheEarlierStakesAndPayout()
    {
        var run = AtWave(1);
        Fighting(run, 200);
        run.Clear();
        Fighting(run, 300);

        run.Abort();

        Assert.AreEqual(200L, run.TotalStaked);
        Assert.AreEqual(300L, run.TotalPaid);
        Assert.AreEqual(100L, run.Net);
    }

    [TestMethod]
    public void Withdraw_DuringAFight_IsRefused()
    {
        var run = Fighting(NewRun());

        Assert.IsFalse(run.Withdraw());

        Assert.AreEqual(WaveRunState.Fighting, run.State);
    }

    [TestMethod]
    public void Withdraw_BetweenWavesAndIdle_EndsAsWithdrew()
    {
        var between = InState(WaveRunState.BetweenWaves);
        var idle = InState(WaveRunState.Idle);

        Assert.IsTrue(between.Withdraw());
        Assert.IsTrue(idle.Withdraw());

        Assert.AreEqual(WaveRunEnd.Withdrew, between.EndReason);
        Assert.AreEqual(WaveRunEnd.Withdrew, idle.EndReason);
        Assert.AreEqual(WaveRunState.Ended, between.State);
    }

    [TestMethod]
    public void MaxStake_TournamentCap1000_ClampsEveryWave()
    {
        Assert.AreEqual(500, NewRun(1000).MaxStake);
        Assert.AreEqual(1000, AtWave(2, 1000).MaxStake);
        Assert.AreEqual(1000, AtWave(3, 1000).MaxStake);
        Assert.AreEqual(1000, AtWave(8, 1000).MaxStake);
    }

    [TestMethod]
    public void MaxStake_TournamentCapZeroOrBelow_MeansTheHardCeiling()
    {
        var rules = new WaveRules(10, 15_000, true, 5, 3);
        var table = Table(rules, Row(1_000_000));

        Assert.AreEqual(15_000, new WaveRun(table, 0, 1).MaxStake);
        Assert.AreEqual(15_000, new WaveRun(table, -1, 1).MaxStake);
    }

    [TestMethod]
    public void MaxStake_TournamentCapAboveTheCeiling_StaysAtTheCeiling()
    {
        var table = Table(new WaveRules(10, 15_000, true, 5, 3), Row(1_000_000));

        Assert.AreEqual(15_000, new WaveRun(table, 900_000, 1).MaxStake);
    }

    [TestMethod]
    public void MaxStake_PerkFactor2_AppliesAfterTheClamp_WaveEightAllows30000()
    {
        Assert.AreEqual(30_000, AtWave(8, 15_000, 2).MaxStake);
        Assert.AreEqual(1000, NewRun(500, 2).MaxStake);
    }

    [TestMethod]
    public void MaxStake_PerkFactor_IsClampedToOneThroughTen()
    {
        Assert.AreEqual(500, NewRun(15_000, 0).MaxStake);
        Assert.AreEqual(500, NewRun(15_000, -3).MaxStake);
        Assert.AreEqual(5000, NewRun(15_000, 10).MaxStake);
        Assert.AreEqual(5000, NewRun(15_000, 99).MaxStake);
    }

    [TestMethod]
    public void MaxStake_ExtremeConfig_DoesNotOverflow()
    {
        var table = Table(new WaveRules(10, int.MaxValue, true, 5, 3), Row(int.MaxValue, 40));

        var run = new WaveRun(table, 0, 10);

        Assert.AreEqual(int.MaxValue, run.MaxStake);
        Assert.AreEqual(int.MaxValue, run.PayoutFor(int.MaxValue));
    }

    [TestMethod]
    public void PayoutFor_Stake1000000_AtTheTopOdds_PaysFourMillion()
    {
        var table = Table(WaveRules.Default, Row(1_000_000, 40));

        Assert.AreEqual(4_000_000, new WaveRun(table, 0, 1).PayoutFor(1_000_000));
    }

    [TestMethod]
    public void PayoutFor_Floors()
    {
        var run = NewRun();

        Assert.AreEqual(499, run.PayoutFor(333));
        Assert.AreEqual(15, run.PayoutFor(10));
        Assert.AreEqual(0, run.PayoutFor(0));
    }

    [TestMethod]
    public void FullRunAtTheFullCap_Nets54200_AndWithPerkFactor2Nets108400()
    {
        foreach (var (perk, staked, paid) in new[] { (1, 57_000L, 111_200L), (2, 114_000L, 222_400L) })
        {
            var run = NewRun(0, perk);
            while (run.State != WaveRunState.Ended)
            {
                Assert.IsTrue(run.TryStake(run.MaxStake, Rich));
                run.BeginFight();
                run.Clear();
            }

            Assert.AreEqual(WaveRunEnd.Completed, run.EndReason);
            Assert.AreEqual(staked, run.TotalStaked);
            Assert.AreEqual(paid, run.TotalPaid);
            Assert.AreEqual(paid - staked, run.Net);
        }
    }

    [TestMethod]
    public void EveryCall_InAWrongState_ChangesNothing()
    {
        foreach (var state in System.Enum.GetValues(typeof(WaveRunState)).Cast<WaveRunState>())
        {
            var run = InState(state);
            var before = Snapshot(run);
            var calls = new List<string>();

            if (state != WaveRunState.Idle && state != WaveRunState.BetweenWaves && (run.TryStake(100, Rich) || run.CanStake(100, Rich)))
                calls.Add("stake");
            if (state != WaveRunState.WaveDue && run.BeginFight())
                calls.Add("begin");
            if (state != WaveRunState.Fighting && (run.Clear() != 0 || run.Lose() != 0 || run.Forfeit() != 0))
                calls.Add("resolve");
            if (state != WaveRunState.WaveDue && run.Refund() != 0)
                calls.Add("refund");
            if (state != WaveRunState.Fighting && run.Abort() != 0)
                calls.Add("abort");
            if (state != WaveRunState.Idle && state != WaveRunState.BetweenWaves && run.Withdraw())
                calls.Add("withdraw");

            Assert.AreEqual(0, calls.Count, state + " accepted: " + string.Join(",", calls));
            Assert.AreEqual(before, Snapshot(run), state + " changed");
        }
    }

    [TestMethod]
    public void SmallStake_IsTheSmallestWagerOfTheFirstWave_AndDoesNotGrowWithTheWave()
    {
        Assert.AreEqual(120, AtWave(1).SmallStake);
        Assert.AreEqual(120, AtWave(2).SmallStake);
        Assert.AreEqual(120, AtWave(WaveTable.Default.Waves.Count).SmallStake);
    }

    [TestMethod]
    public void SmallStake_ATableMinimumAboveTheFirstQuarter_KeepsTheFirstWavesSmallestLegalWagerOnEveryWave()
    {
        var rules = new WaveRules(121, 15_000, true, 5, 3);
        var run = new WaveRun(Table(rules, Row(500), Row(1000), Row(2000)), 0, 1);

        Assert.AreEqual(250, run.SmallStake);
        CollectionAssert.AreEqual(new[] { 250, 500 }, StakeOptions.For(run.MaxStake, 121, run.SmallStake).ToArray());
        Fighting(run, 250);
        run.Clear();
        CollectionAssert.AreEqual(new[] { 250, 500, 1000 }, StakeOptions.For(run.MaxStake, 121, run.SmallStake).ToArray());
        Fighting(run, 250);
        run.Clear();
        CollectionAssert.AreEqual(new[] { 250, 1000, 2000 }, StakeOptions.For(run.MaxStake, 121, run.SmallStake).ToArray());
    }

    [TestMethod]
    public void SmallStake_NoWagerOnTheFirstWave_IsZero()
    {
        Assert.AreEqual(0, NewRun(tournamentCap: 5).SmallStake);
    }

    [TestMethod]
    public void SmallStake_FollowsTheTournamentCapAndThePerkFactor()
    {
        Assert.AreEqual(40, NewRun(tournamentCap: 180).SmallStake);
        Assert.AreEqual(250, NewRun(perkFactor: 2).SmallStake);
    }

    [TestMethod]
    public void RecordPaid_LessThanThePayout_TheAccountFollowsTheGoldThatArrived()
    {
        var run = AtWave(1);
        Fighting(run, 500);
        Assert.AreEqual(750, run.Clear());

        run.RecordPaid(500);

        Assert.AreEqual(500, run.LastPayout);
        Assert.AreEqual(500, run.TotalPaid);
        Assert.AreEqual(0, run.Net);
    }

    [DataTestMethod]
    [DataRow(750)]
    [DataRow(900)]
    [DataRow(-1)]
    public void RecordPaid_TheWholePayoutOrANonsenseAmount_ChangesNothing(int arrived)
    {
        var run = AtWave(1);
        Fighting(run, 500);
        run.Clear();

        run.RecordPaid(arrived);

        Assert.AreEqual(750, run.LastPayout);
        Assert.AreEqual(750, run.TotalPaid);
    }

    [TestMethod]
    public void SmallStake_LimitsThatGrowByLessThanDouble_TheFirstWavesSmallestWagerStaysOnOffer()
    {
        var rules = new WaveRules(200, 15_000, true, 5, 3);
        var run = new WaveRun(Table(rules, Row(500), Row(600)), 0, 1);

        CollectionAssert.AreEqual(new[] { 250, 500 }, StakeOptions.For(run.MaxStake, 200, run.SmallStake).ToArray());
        Fighting(run, 250);
        run.Clear();
        CollectionAssert.AreEqual(new[] { 250, 300, 600 }, StakeOptions.For(run.MaxStake, 200, run.SmallStake).ToArray());
    }

    /// <summary>
    /// The property of the stake rule, over a grid of tables the reader accepts: on every wave the smallest wager on offer is never
    /// above the first wave's, the list is ascending and distinct, every entry is between the table's minimum and the wave's limit,
    /// and the run accepts the smallest. A cleared wave never lowers the purse (the payout is at least 1.1 times the stake), so a
    /// player who could enter can then always go on.
    /// </summary>
    [TestMethod]
    public void StakeChoices_OverAGridOfTables_TheSmallestWagerNeverRisesAboveTheFirstWaves()
    {
        var capSets = new[]
        {
            WaveTable.Default.Waves.Select(w => w.Cap).ToArray(),
            new[] { 20, 30 }, new[] { 500, 600 }, new[] { 500, 600, 800 }, new[] { 500, 800, 900 },
            new[] { 1000, 1400, 2000 }, new[] { 500, 500, 500 }, new[] { 40 },
        };
        var checkedWaves = 0;
        foreach (var caps in capSets)
        foreach (var min in new[] { 1, 10, 11, 121, 130, 200, 250, 600, 1000 })
        foreach (var tournamentCap in new[] { 0, 5, 30, 180, 600 })
        foreach (var perk in new[] { 1, 2, 3 })
        {
            var rules = new WaveRules(min, 15_000, true, 5, 3);
            var run = new WaveRun(Table(rules, caps.Select(c => Row(c)).ToArray()), tournamentCap, perk);
            var where = $"caps=[{string.Join(",", caps)}] min={min} tournamentCap={tournamentCap} perk={perk}";
            var first = -1;
            for (var wave = 1; wave <= caps.Length; wave++)
            {
                var choices = StakeOptions.For(run.MaxStake, min, run.SmallStake).ToArray();
                if (choices.Length == 0)
                {
                    Assert.AreEqual(-1, first, $"{where}: wave {wave} offers nothing after an earlier wave offered a wager");
                    break;
                }

                if (first < 0)
                    first = choices[0];
                Assert.IsTrue(choices[0] <= first, $"{where}: wave {wave} asks at least {choices[0]}, the first wave asked {first}");
                Assert.IsTrue(choices.Length <= 3, $"{where}: wave {wave} offers {choices.Length} wagers");
                for (var i = 0; i < choices.Length; i++)
                {
                    Assert.IsTrue(choices[i] >= min && choices[i] <= run.MaxStake, $"{where}: wave {wave} offers {choices[i]}");
                    Assert.IsTrue(i == 0 || choices[i] > choices[i - 1], $"{where}: wave {wave} is not ascending and distinct");
                }

                Assert.IsTrue(run.TryStake(choices[0], Rich), $"{where}: wave {wave} refuses its own smallest wager");
                run.BeginFight();
                run.Clear();
                checkedWaves++;
            }
        }

        Assert.IsTrue(checkedWaves > 1000, $"only {checkedWaves} waves checked; the grid did not run");
    }
}
