using System;

namespace TAOM.Features.TrainingGrounds.Waves.Domain;

internal enum WaveRunState
{
    Idle,
    WaveDue,
    Fighting,
    BetweenWaves,
    Ended,
}

internal enum WaveRunEnd
{
    None,
    Withdrew,
    Lost,
    Completed,
    Forfeited,
    Refunded,
}

/// <summary>
/// One run of wave bets at a town's training master (docs/features/training-waves.md), as a state machine over whole numbers:
/// Idle or BetweenWaves, a stake gives WaveDue, the fight gives Fighting, a clear gives BetweenWaves (or Ended after the last
/// wave), a loss or a forfeit ends it. A call made in the wrong state changes nothing and returns false or 0. The gold itself moves
/// elsewhere (the service); this type only counts it, in long, so no sum can overflow.
/// </summary>
internal sealed class WaveRun
{
    private const int MinPerk = 1;
    private const int MaxPerk = 10;

    private readonly WaveTable _table;
    private readonly int _tournamentCap;
    private readonly int _perkFactor;
    private int _index;

    /// <param name="tournamentCap">The MCM tournament bet cap; 0 or less means no cap beyond the table's hard ceiling.</param>
    /// <param name="perkFactor">The player's bet multiplier (Deep Pockets), clamped to 1 through 10.</param>
    public WaveRun(WaveTable table, int tournamentCap, int perkFactor)
    {
        _table = table;
        _tournamentCap = tournamentCap;
        _perkFactor = Math.Min(MaxPerk, Math.Max(MinPerk, perkFactor));
    }

    public WaveRunState State { get; private set; } = WaveRunState.Idle;

    public WaveRunEnd EndReason { get; private set; } = WaveRunEnd.None;

    /// <summary>The wave the player is on, from 1. After the last wave clears it stays at the last wave.</summary>
    public int Wave => _index + 1;

    /// <summary>The stake on the wave in hand (WaveDue and Fighting); 0 otherwise.</summary>
    public int Stake { get; private set; }

    /// <summary>The stake of the wave resolved last (cleared, lost, forfeited), kept for the master's words; 0 before one.</summary>
    public int LastStake { get; private set; }

    /// <summary>What the last cleared wave paid; 0 before one.</summary>
    public int LastPayout { get; private set; }

    public long TotalStaked { get; private set; }

    public long TotalPaid { get; private set; }

    public long Net => TotalPaid - TotalStaked;

    public WaveRow Row => _table.Waves[_index];

    /// <summary>
    /// The most that may be staked on the wave in hand: the row's cap, held to the hard ceiling (itself lowered to the tournament cap
    /// when that is above 0), times the perk factor. Computed in long and clamped to int.
    /// </summary>
    public int MaxStake
    {
        get
        {
            long ceiling = _table.Rules.HardCeiling;
            if (_tournamentCap > 0)
                ceiling = Math.Min(ceiling, _tournamentCap);
            var limit = Math.Min(Row.Cap, ceiling) * _perkFactor;
            return (int)Math.Min(int.MaxValue, limit);
        }
    }

    /// <summary>The payout for a stake on the wave in hand: stake times odds over 10, floored, in long and clamped to int.</summary>
    public int PayoutFor(int stake) => (int)Math.Min(int.MaxValue, (long)stake * Row.OddsTenths / 10);

    public bool CanStake(int amount, int gold) =>
        (State == WaveRunState.Idle || State == WaveRunState.BetweenWaves)
        && amount >= _table.Rules.MinStake && amount <= MaxStake && amount <= gold;

    /// <summary>Takes the stake into the run: Idle or BetweenWaves to WaveDue. False, with nothing changed, when <see cref="CanStake"/> says no.</summary>
    public bool TryStake(int amount, int gold)
    {
        if (!CanStake(amount, gold))
            return false;
        Stake = amount;
        TotalStaked += amount;
        State = WaveRunState.WaveDue;
        return true;
    }

    public bool BeginFight()
    {
        if (State != WaveRunState.WaveDue)
            return false;
        State = WaveRunState.Fighting;
        return true;
    }

    /// <summary>Fighting to BetweenWaves with the next wave, or to Ended (Completed) after the last row. Returns the payout; 0 in another state.</summary>
    public int Clear()
    {
        if (State != WaveRunState.Fighting)
            return 0;

        var payout = PayoutFor(Stake);
        TotalPaid += payout;
        LastStake = Stake;
        LastPayout = payout;
        Stake = 0;
        if (_index + 1 < _table.Waves.Count)
        {
            _index++;
            State = WaveRunState.BetweenWaves;
        }
        else
        {
            End(WaveRunEnd.Completed);
        }

        return payout;
    }

    /// <summary>Fighting to Ended (Lost). Returns the stake the house keeps; 0 in another state.</summary>
    public int Lose() => EndFight(WaveRunEnd.Lost);

    /// <summary>Fighting to Ended (Forfeited), for a fight left unfinished. Returns the stake the house keeps; 0 in another state.</summary>
    public int Forfeit() => EndFight(WaveRunEnd.Forfeited);

    /// <summary>WaveDue to Ended (Refunded), for a wave that never started. Takes the stake back out of the total and returns it; 0 in another state.</summary>
    public int Refund()
    {
        if (State != WaveRunState.WaveDue)
            return 0;

        var back = Stake;
        TotalStaked -= back;
        Stake = 0;
        End(WaveRunEnd.Refunded);
        return back;
    }

    /// <summary>
    /// Fighting to Ended (Refunded), for a fight that a fault of ours ended, never the player's doing: the stake is not lost, so it comes
    /// back out of the total as <see cref="Refund"/> does for a wave that never started. Returns it; 0 in another state.
    /// </summary>
    public int Abort()
    {
        if (State != WaveRunState.Fighting)
            return 0;

        var back = Stake;
        TotalStaked -= back;
        Stake = 0;
        End(WaveRunEnd.Refunded);
        return back;
    }

    /// <summary>Idle or BetweenWaves to Ended (Withdrew). False during a fight, with a wave due, or once ended.</summary>
    public bool Withdraw()
    {
        if (State != WaveRunState.Idle && State != WaveRunState.BetweenWaves)
            return false;
        End(WaveRunEnd.Withdrew);
        return true;
    }

    private int EndFight(WaveRunEnd reason)
    {
        if (State != WaveRunState.Fighting)
            return 0;

        var lost = Stake;
        LastStake = lost;
        LastPayout = 0;
        Stake = 0;
        End(reason);
        return lost;
    }

    private void End(WaveRunEnd reason)
    {
        State = WaveRunState.Ended;
        EndReason = reason;
    }
}
