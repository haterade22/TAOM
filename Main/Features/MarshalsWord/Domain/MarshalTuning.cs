namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>The numbers of Marshal's Word stages M1 and M2, pinned by the build specification (sections 5.3 and 6.4).</summary>
internal static class MarshalTuning
{
    /// <summary>Steps one company may hold in its queue. A full queue swallows the next order (5.2).</summary>
    internal const int MaxStepsPerCompany = 12;

    /// <summary>Mission seconds between two passes of the queue driver and of the march.</summary>
    internal const double SlowPassSeconds = 0.25;

    /// <summary>The least a step may be given to finish, in mission seconds.</summary>
    internal const double MinPatience = 25.0;

    /// <summary>The allowance as a multiple of the walk's estimated time.</summary>
    internal const double PatiencePerEta = 2.5;

    /// <summary>How close to its order point, in metres, a company counts as arrived.</summary>
    internal const float ArrivalSlack = 3f;

    /// <summary>The slowest pace a march gives any company, as a share of full speed.</summary>
    internal const float MinPace = 0.3f;

    /// <summary>A march ends when its longest remaining walk is under this many seconds.</summary>
    internal const float MarchEndSeconds = 2f;

    /// <summary>A march ends when a company's order point is farther than this from the point the march stored for it.</summary>
    internal const float MarchMovedMetres = 1f;

    /// <summary>The seconds apart that companies of one march may arrive whatever their walks, the constant term of the spread rule (6.5).</summary>
    internal const double SpreadBaseSeconds = 2.0;

    /// <summary>The share of the first pass's longest time to the slack that the spread rule adds to its allowance (6.5).</summary>
    internal const double SpreadShareOfLongest = 0.25;

    /// <summary>The shortest first-pass longest time to the slack, in seconds, for which a walk proves the pace, because a short walk's slack window for a slow near company can be wider than the allowance (6.5).</summary>
    internal const double SpreadMinLongest = 15.0;

    /// <summary>
    /// The walks must differ by this multiple of the allowance to prove the pace. A Line company can beat a tidy first read by at most 1.25,
    /// because a soldier stops at their own maximum (HumanAIComponent.cs:581) and the read carries the 0.8 restriction (Formation.cs:1484-1491,
    /// ArrangementOrder.cs:87). A far company at the catch-up factor's 1.1 with a near company at 1 is the case that needs 1.4 on the walks swept (6.5).
    /// </summary>
    internal const double SpreadProofMargin = 1.4;

    /// <summary>Vanilla's gap between two companies in a line, <c>OrderController.FormationGapInLine</c>.</summary>
    internal const float GapInLine = 1.5f;

    /// <summary>A step ended by patience, as a share of finished steps, above which the patience check fails (5.6).</summary>
    internal const int PatienceFailShareDenominator = 4;

    /// <summary>Finished steps needed before the patience check judges.</summary>
    internal const int PatienceJudgeAfter = 8;
}
