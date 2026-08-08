using System;

namespace TAOM.Features.Enlistment.Domain;

/// <summary>
/// What happens when the player asks their commander for release.
/// </summary>
public enum ReleaseVerdict
{
    /// <summary>The term is served — an honourable release, no cost.</summary>
    Granted = 0,

    /// <summary>There is a battle underway. Not a refusal of the request, a refusal of the moment.</summary>
    RefusedInBattle = 1,

    /// <summary>The term is not served. The player may still walk, but that is desertion.</summary>
    RefusedTooSoon = 2,
}

/// <summary>
/// The verdict plus the one number the player needs to make the choice: how many more days
/// are owed. Without it the refusal is just a wall — "not yet" with no way to plan around it.
/// </summary>
public readonly struct ReleaseRequest
{
    public ReleaseVerdict Verdict { get; }

    /// <summary>Whole days still owed. Meaningful only for <see cref="ReleaseVerdict.RefusedTooSoon"/>; 0 otherwise.</summary>
    public int DaysOwed { get; }

    private ReleaseRequest(ReleaseVerdict verdict, int daysOwed)
    {
        Verdict = verdict;
        DaysOwed = daysOwed;
    }

    public static readonly ReleaseRequest Granted = new ReleaseRequest(ReleaseVerdict.Granted, 0);

    public static readonly ReleaseRequest RefusedInBattle =
        new ReleaseRequest(ReleaseVerdict.RefusedInBattle, 0);

    /// <summary>
    /// Always reports at least one day. A refusal that says "0 more days are owed" reads as a
    /// bug to the player, and the rounding that produces it is an artefact of asking on the
    /// last fractional day rather than a real state.
    /// </summary>
    public static ReleaseRequest TooSoon(int daysOwed) =>
        new ReleaseRequest(ReleaseVerdict.RefusedTooSoon, Math.Max(1, daysOwed));
}
