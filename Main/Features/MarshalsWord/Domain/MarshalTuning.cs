namespace TAOM.Features.MarshalsWord.Domain;

/// <summary>The numbers of Marshal's Word, pinned by the build specification (sections 5.3, 6.4, 7.2, 7.3 and 8).</summary>
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

    /// <summary>The longest a soldier holds a draw before the callback looses it, in mission seconds.</summary>
    internal const double HoldLimit = 14.0;

    /// <summary>A soldier with a target nearer than this many metres stands aside and fights as the AI would.</summary>
    internal const double CloseQuarter = 7.0;

    /// <summary>The most a soldier's own delay after a word can be, drawn once when its component is built.</summary>
    internal const double WordJitter = 0.3;

    /// <summary>A Drawing soldier whose draw began longer ago than this many seconds stops counting as eligible for Massed, because it has no view of a target.</summary>
    internal const double DrawLimit = 6.0;

    /// <summary>The share of the eligible soldiers that must hold before Massed gives the word.</summary>
    internal const double ReadyShare = 0.7;

    /// <summary>Mission seconds that must pass between two words of a Massed company.</summary>
    internal const double WordGap = 2.5;

    /// <summary>Mission seconds between two Massed counts.</summary>
    internal const double MassedPassSeconds = 0.2;

    /// <summary>Mission seconds after hold the line goes on before the proof counts blows, so a swing already begun does not count.</summary>
    internal const double HoldSettleSeconds = 3.0;

    /// <summary>Mission seconds the hold-the-line proof counts blows for.</summary>
    internal const double HoldProofSeconds = 10.0;

    /// <summary>Soldiers whose attack flag must read off before the proof judges.</summary>
    internal const int HoldMinFlagOff = 5;

    /// <summary>Blows the control company must deal in the window before it proves anything.</summary>
    internal const int HoldControlMinBlows = 10;

    /// <summary>The held company's blows may be at most one in this many of the control's.</summary>
    internal const int HoldShareDenominator = 10;

    /// <summary>Mission seconds between two hold-the-line summary lines of one company.</summary>
    internal const double HoldSummarySeconds = 5.0;

    /// <summary>The least mission seconds between two square proof lines of one company.</summary>
    internal const double SquareLineSeconds = 1.0;

    /// <summary>Two spacing limits closer than this many metres are the same value.</summary>
    internal const float SpacingEpsilon = 0.001f;

    /// <summary>Holding soldiers a company needs before either volley check judges.</summary>
    internal const int VolleyMinHolding = 5;

    /// <summary>Mission seconds a company must hold with no word before the hold check judges.</summary>
    internal const double HoldWindowSeconds = 5.0;

    /// <summary>Shots while holding may be at most one in this many of the Holding count.</summary>
    internal const int HoldShotShareDenominator = 20;

    /// <summary>Percent of the Holding soldiers that must loose within the release window.</summary>
    internal const int ReleaseSharePercent = 70;

    /// <summary>Seconds added to <see cref="WordJitter"/> for the release window.</summary>
    internal const double ReleaseWindowPadSeconds = 1.0;

    /// <summary>A shot this soon after a Release counts as a shot after the word.</summary>
    internal const double RecentReleaseSeconds = 2.0;

    /// <summary>Mission seconds between two walks of a company's soldiers.</summary>
    internal const double FireCensusSeconds = 1.0;

    /// <summary>Mission seconds between two reads of a soldier's stand-aside facts.</summary>
    internal const double StandAsideReadSeconds = 1.0 / 3.0;

    /// <summary>Mission seconds between two summary lines of one company.</summary>
    internal const double SummarySeconds = 5.0;

    /// <summary>The outline of a selected company's units, as an ARGB colour.</summary>
    internal const uint SelectedOutline = 0xFFD9B44Au;

    /// <summary>The outline of an enemy company that a selected company marks or charges.</summary>
    internal const uint MarkedOutline = 0xFFB03A2Eu;

    /// <summary>Mission seconds between two outline passes.</summary>
    internal const double OutlinePassSeconds = 0.25;

    /// <summary>Soldiers of a marked company read for the mark's effect.</summary>
    internal const int MarkSampleMax = 20;

    /// <summary>Mission seconds between two reads of a mark's effect.</summary>
    internal const double MarkPassSeconds = 1.0;

    /// <summary>A mark is judged only after it has stood this long, because the AI needs a few passes to pick targets.</summary>
    internal const double MarkSettleSeconds = 3.0;

    /// <summary>Fewest sampled soldiers with a target for the mark check to judge.</summary>
    internal const int MarkMinSampled = 3;

    /// <summary>Mission seconds a soldier keeps the attack flag after a real wound, so that a held line still answers a blow (9.2).</summary>
    internal const double StrikeBackSeconds = 4.0;

    /// <summary>A walk is called away from the slot when the cosine between the input and the direction to the slot is under minus this (9.2).</summary>
    internal const float SlotAwayDot = 0.3f;

    /// <summary>A held soldier closer than this many metres to the slot is in place and is never steered (9.2).</summary>
    internal const float SlotSlack = 0.25f;

    /// <summary>Mission seconds between two reads of a held soldier's qualifying facts and slot, and between two passes that apply them (9.2).</summary>
    internal const double HoldReadSeconds = 1.0 / 3.0;

    /// <summary>Mission seconds between two lines of the slot proof of one held company.</summary>
    internal const double SlotSummarySeconds = 5.0;

    /// <summary>A held soldier within this many metres of the slot is not counted in the far fact of the slot line.</summary>
    internal const float SlotNearMetres = 2f;

    /// <summary>Steered hold reads a window needs before the slot proof judges which way the clamp moved the soldiers.</summary>
    internal const int SlotMinSteered = 5;

    /// <summary>A change in the distance to the slot of less than this many metres, from the first replaced tick to the next hold read, is neither closer nor farther.</summary>
    internal const float SlotTrendMetres = 0.1f;
}
