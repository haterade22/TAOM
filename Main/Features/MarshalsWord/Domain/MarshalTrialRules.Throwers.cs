namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>Everything <c>advance_throwers</c> read, in the order its reasons ask.</summary>
internal readonly record struct ThrowerTrialRead(
    bool Faulted, int Armed0, bool EnemyFound, bool Controllable, bool Published, bool PostfixMoved, bool ClosedBeforeStand, bool Stood, bool Released,
    float MaxOvershoot, int Shots, int RunShots, int AmmoSpent, bool MeleePassed, long PubCalls, ThrowerCostVerdict Cost);

internal static partial class MarshalTrialRules
{
    /// <summary>The fewest armed soldiers the thrower steps accept as a thrower company.</summary>
    internal const int ThrowerTrialMinArmed = 10;

    /// <summary>Real seconds after the first table in which the postfix must have moved an answer.</summary>
    internal const float ThrowerPostfixWaitSeconds = 5f;

    /// <summary>Real seconds past the stand limit in which the run must have released.</summary>
    internal const float ThrowerReleaseSlackSeconds = 15f;

    /// <summary>Why <c>advance_throwers</c> failed: the first that holds, ground first, then the run, then the checks. Empty when it passed.</summary>
    internal static string ThrowerTrialReason(ThrowerTrialRead read)
    {
        if (read.Faulted)
            return "faulted";
        var ground = ThrowerGroundReason(read.Armed0, read.EnemyFound, read.Controllable);
        if (ground.Length > 0)
            return ground;
        if (!read.Published)
            return "never-published";
        if (!read.PostfixMoved)
            return "postfix-silent";
        if (read.ClosedBeforeStand)
            return "closed-before-stand";
        if (!(read.MaxOvershoot <= MarshalTuning.OvershootMetres))
            return "walked-in";
        if (!read.Stood)
            return "never-stood";
        if (!read.Released)
            return "never-released";
        if (read.Shots < MarshalTuning.MinThrows)
            return read.RunShots == 0 && read.AmmoSpent >= MarshalTuning.MinThrows ? "shots-not-counted" : "few-throws";
        if (!read.MeleePassed)
            return "no-melee";
        return ThrowerCostReason(read.PubCalls, read.Cost);
    }

    /// <summary>Why <c>advance_throwers_vanilla</c> failed: the control must show the same ground, no table written, and the company walking in to melee.</summary>
    internal static string ThrowerVanillaReason(bool faulted, int armed0, bool enemyFound, bool controllable, bool published, bool closed, float slowest)
    {
        if (faulted)
            return "faulted";
        var ground = ThrowerGroundReason(armed0, enemyFound, controllable);
        if (ground.Length > 0)
            return ground;
        if (published)
            return "published-while-off";
        if (!closed)
            return "never-closed";
        return slowest > MarshalTuning.StandStillSpeed ? "" : "stand-blind";
    }

    private static string ThrowerGroundReason(int armed0, bool enemyFound, bool controllable)
    {
        if (armed0 < ThrowerTrialMinArmed)
            return "no-throwers";
        if (!enemyFound)
            return "no-enemy";
        return controllable ? "" : "not-controllable";
    }

    private static string ThrowerCostReason(long pubCalls, ThrowerCostVerdict cost)
    {
        if (pubCalls == 0)
            return "timer-silent";
        switch (cost.Kind)
        {
            case ThrowerVerdictKind.Skip:
                return "too-few-calls";
            case ThrowerVerdictKind.Fail:
                return cost.Reason == "mean" ? "cost-mean" : "cost-max";
            default:
                return "";
        }
    }
}
