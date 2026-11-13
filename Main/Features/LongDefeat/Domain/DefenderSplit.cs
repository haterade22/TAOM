using System;
using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// The shares of the opening split (Mike, 2026-11-12), from a fortress row's <c>split</c> block: <c>WallFoot</c> of the infantry go to the wall and the rest to the gate,
/// <c>LowArchers</c> of the archers go to the lower wall and the rest to the high walls. A share leaves at least one soldier in each group, so it lies in [0.05, 0.95].
/// </summary>
internal sealed record SplitShares(float WallFoot, float LowArchers)
{
    public const float Min = 0.05f;
    public const float Max = 0.95f;

    public static bool TryCreate(float wallFoot, float lowArchers, out SplitShares? shares)
    {
        shares = InRange(wallFoot) && InRange(lowArchers) ? new SplitShares(wallFoot, lowArchers) : null;
        return shares != null;
    }

    private static bool InRange(float share) => share >= Min && share <= Max;
}

/// <summary>What the next tick does with the split.</summary>
internal enum SplitStep
{
    None,

    /// <summary>A fresh split: a new hold starts its clock and takes the close attacker counts.</summary>
    Split,

    /// <summary>A split again inside the hold (a pack mustered): the clock and the counts stay.</summary>
    Resplit,
    Release,
}

/// <summary>The pure rules of the opening split: the size of each group and what counts as the first contact of a wave.</summary>
internal static class DefenderSplit
{
    // A small allowance so a float share such as 0.7f (0.69999999) gives the same group as 0.7 does.
    private const double RoundingAllowance = 1e-4;

    /// <summary>
    /// How many of <paramref name="total"/> soldiers stand in the first group for <paramref name="share"/>: the nearest whole number, with at least one soldier in each group
    /// when there are two or more. A sole soldier stays in the first group; none gives none.
    /// </summary>
    public static int FirstGroup(int total, float share)
    {
        if (total <= 1)
            return Math.Max(0, total);

        var rounded = (int)Math.Floor(total * (double)share + 0.5 + RoundingAllowance);
        return Math.Min(total - 1, Math.Max(1, rounded));
    }

    /// <summary>
    /// Contact: a side whose count of attackers at the walls (the siege AI's close attacker count: near the side's defender origin, or on an inside navmesh face such as a docked tower's floor)
    /// rose since the hold began, or a list that changed. A lane's defense level is no contact: the engine raises it when an attacker formation takes one of the lane's engines, which a wave
    /// does within a second of its spawn (first run, 2026-11-13). A count already up when the hold began is not new contact.
    /// </summary>
    public static bool Contact(IReadOnlyList<int> atHold, IReadOnlyList<int> now)
    {
        if (atHold.Count != now.Count)
            return true;

        for (var i = 0; i < now.Count; i++)
        {
            if (now[i] > atHold[i])
                return true;
        }

        return false;
    }
}

/// <summary>
/// When the opening split is made and when it ends. It is made in each calm (the preparation and every intermission) once no defender is still to muster, and made again
/// after a pack has mustered while it holds. It ends at the first contact of a wave, and the AI then keeps that wave. A run that starts inside a wave leaves that wave to the AI.
/// </summary>
internal sealed class SplitHold
{
    private bool _lastCalm;
    private bool _doneThisCalm;
    private bool _packWaiting;

    public bool Held { get; private set; }

    public SplitStep Next(RunPhase phase, bool mustering, bool contact)
    {
        if (phase == RunPhase.Ended)
            return SplitStep.None;

        var calm = phase == RunPhase.Preparation || phase == RunPhase.Intermission;
        if (calm && !_lastCalm)
            _doneThisCalm = false;
        _lastCalm = calm;

        if (Held && phase == RunPhase.Wave && contact)
        {
            Held = false;
            _packWaiting = false;
            return SplitStep.Release;
        }

        if (mustering)
        {
            _packWaiting = true;
            return SplitStep.None;
        }

        if (calm && !_doneThisCalm)
        {
            _doneThisCalm = true;
            _packWaiting = false;
            Held = true;
            return SplitStep.Split;
        }

        if (Held && _packWaiting)
        {
            _packWaiting = false;
            return SplitStep.Resplit;
        }

        return SplitStep.None;
    }

    /// <summary>Ends the hold with no release call: something else (the stalemate charge) already handed every formation to the AI.</summary>
    public void Drop()
    {
        Held = false;
        _packWaiting = false;
    }
}

/// <summary>
/// What one split did: whether it was made (<c>Problem</c> says why not), how many soldiers stand in each group, and the place each group was sent to (null for a group with
/// no place, which is then left to the AI).
/// </summary>
internal sealed record LdSplitResult(
    string Problem,
    int Wall,
    int Gate,
    int LowArchers,
    int HighArchers,
    LdPoint? WallAt = null,
    LdPoint? GateAt = null,
    LdPoint? LowAt = null,
    LdPoint? HighAt = null)
{
    public bool Made => Problem.Length == 0;

    public static LdSplitResult Refused(string problem) => new(problem, 0, 0, 0, 0);
}

/// <summary>
/// One held group as it stands: its name (<c>wall</c>, <c>gate</c>, <c>low</c> or <c>high</c>), how many soldiers it holds, the median ground distance from its soldiers to its place
/// (NaN with none), and how many stand at or above the wall line.
/// </summary>
internal readonly record struct LdSplitGroup(string Name, int Count, float Median, int High, bool Archers);

/// <summary>What a release did: the groups handed back to the AI, and the groups left alone because the player gave them an order of their own.</summary>
internal readonly record struct LdSplitRelease(int Released, int KeptByPlayer);
