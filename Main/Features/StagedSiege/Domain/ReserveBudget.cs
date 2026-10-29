using System;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// How much of each side's starting reserve a stage has released. The allowance is cumulative (unspent shares carry forward) and
/// a side with nobody standing but troops in reserve may always release, so no battle can stall on an empty field or end as a
/// rout while its reserve is held back. "Standing" is an active agent that is not running away: vanilla's rout check treats a
/// side whose every active agent flees as beaten.
/// </summary>
public sealed class ReserveBudget
{
    private readonly int _attackerReserve;
    private readonly int _defenderReserve;
    private readonly ReserveShares _shares;
    private readonly StagedSide _playerSide;

    public ReserveBudget(int attackerReserve, int defenderReserve, ReserveShares shares, StagedSide playerSide)
    {
        _attackerReserve = Math.Max(0, attackerReserve);
        _defenderReserve = Math.Max(0, defenderReserve);
        _shares = shares;
        _playerSide = playerSide;
    }

    private int ReserveOf(StagedSide side) => side == StagedSide.Attacker ? _attackerReserve : _defenderReserve;

    /// <summary>The most of the starting reserve that may have been released by the end of <paramref name="stage"/>.</summary>
    public int Allowance(StagedSide side, StageId stage)
    {
        var reserve = ReserveOf(side);
        var shares = _shares.For(side, _playerSide);
        double sum = 0;
        for (var i = 0; i <= (int)stage && i < shares.Count; i++)
            sum += shares[i];
        return (int)Math.Min(reserve, Math.Floor(reserve * sum));
    }

    /// <summary>Nobody of the side is standing but reserve remains.</summary>
    public bool IsSoftlock(int standing, int remaining) => standing == 0 && remaining > 0;

    public bool MayRelease(StagedSide side, StageId stage, int remaining, int standing)
    {
        if (IsSoftlock(standing, remaining))
            return true;
        var spent = Math.Max(0, ReserveOf(side) - remaining);
        return spent < Allowance(side, stage);
    }
}
