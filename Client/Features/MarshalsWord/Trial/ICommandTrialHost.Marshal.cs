using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>Marshal's Word has a live session in this mission.</summary>
    bool MarshalLive { get; }

    /// <summary>The session has latched itself off after a fault, or the archers' callback has thrown and stands down for the rest of the process.</summary>
    bool MarshalFaulted { get; }

    /// <summary>Chooses the <paramref name="count"/> largest player companies that hold no hero, by index; returns how many were chosen (0 when fewer exist).</summary>
    int MarshalChooseCompanies(int count);

    /// <summary>Chooses the largest ranged player company; returns its unit count, 0 for none.</summary>
    int MarshalChooseArchers();

    /// <summary>Makes <paramref name="count"/> of the chosen companies, from <paramref name="first"/>, the only selected ones. A formation write: call it after the in-mission-tick marker.</summary>
    void MarshalSelect(int first, int count);

    /// <summary>Holds or lets go of key 540 as the view reads it, for the next screen ticks (the panel must be open).</summary>
    void MarshalHoldQueue(bool held);

    /// <summary>Holds or lets go of key 541.</summary>
    void MarshalHoldMark(bool held);

    /// <summary>Holds or lets go of key 542.</summary>
    void MarshalHoldWidths(bool held);

    /// <summary>Lets go of the three held keys the trial holds, as a player lifts them. The script calls it as each step ends, so a failed step leaves no key held for the next.</summary>
    void MarshalReleaseKeys();

    /// <summary>The statics the patches read: key 540 armed, key 541 armed, key 542 held.</summary>
    bool MarshalQueueArmed { get; }

    bool MarshalMarkArmed { get; }

    bool MarshalWidthsArmed { get; }

    /// <summary>Stores a ground point as <paramref name="slot"/>: the chosen company's place moved <paramref name="forward"/> along its facing and <paramref name="right"/> to its right. False when the company index names no chosen company; the ground is not tested.</summary>
    bool MarshalPlanPoint(int slot, int company, float forward, float right);

    /// <summary>Gives the selected companies a Move to a stored point through the player's order controller, as a click does. An order write: call it after the in-mission-tick marker.</summary>
    void MarshalMoveTo(int slot);

    /// <summary>
    /// Stores the drag along the chosen companies' own front, flank to flank, moved <paramref name="shiftMetres"/> sideways along itself: the metres the chosen companies stand
    /// apart along their facing, and the shift the drag makes. Null when there is no front.
    /// </summary>
    FrontDrag? MarshalPlanFrontDrag(float shiftMetres);

    /// <summary>Stores the drag that turns the chosen companies a quarter, ahead of them, for <c>arrive_together</c>; null when no candidate fits the contract's ratio, floor and cap.</summary>
    TrialDrag? MarshalPlanArrivalDrag();

    /// <summary>Gives the selected companies the stored drag as a line order through the player's order controller. An order write: call it after the in-mission-tick marker.</summary>
    void MarshalDrag();

    /// <summary>Gives the selected companies a Stop through the player's order controller. An order write.</summary>
    void MarshalStandGround();

    /// <summary>Metres from a chosen company to a stored point; NaN when either is missing.</summary>
    float MarshalDistanceToPoint(int company, int slot);

    /// <summary>The chosen company is not on its way anywhere: it holds no order point, or stands within the arrival slack of it. An earlier step's walk is over when this holds.</summary>
    bool MarshalSettled(int company);

    /// <summary>How far chosen company <paramref name="to"/> stands ahead of chosen company <paramref name="from"/>, along the facing of <paramref name="from"/>, where each stands now; NaN when unreadable.</summary>
    float MarshalAhead(int from, int to);

    /// <summary>The order point of chosen company <paramref name="to"/> less that of <paramref name="from"/> on the ground plane (Z zero), taking a company's current position when it holds no order point: the offset the array solver works from; null when unreadable.</summary>
    Vec3f? MarshalOrderedOffset(int from, int to);

    /// <summary>The position of chosen company <paramref name="to"/> less that of <paramref name="from"/> on the ground plane (Z zero), in metres; null when unreadable.</summary>
    Vec3f? MarshalOffset(int from, int to);

    /// <summary>The order point of a chosen company on the ground plane (Z zero), or its position when it holds none: the place the array solver works from; null when unreadable.</summary>
    Vec3f? MarshalOrderPoint(int company);

    /// <summary>The chosen company is in the Line arrangement.</summary>
    bool MarshalInLine(int company);

    /// <summary>The company's speed in metres a second, as the engine caches it.</summary>
    float MarshalSpeed(int company);

    int MarshalQueuedSteps(int company);

    /// <summary>Steps the capture stored, counted when it stored them.</summary>
    int MarshalStepsCaptured { get; }

    /// <summary>Steps whose last member left, and of those the ones that patience ended and the ones that ended by arrival.</summary>
    int MarshalStepsFinished { get; }

    int MarshalStepsByPatience { get; }

    int MarshalStepsByArrival { get; }

    /// <summary>Array commits the takeover laid out, counted by the commit and not the preview.</summary>
    int MarshalArraysSolved { get; }

    string MarshalMarchConclusion { get; }

    int MarshalMarchesConcluded { get; }

    /// <summary>Chooses the enemy company with the most units; returns that count, 0 for none.</summary>
    int MarshalChooseEnemy();

    /// <summary>Chooses the enemy company with units that stands nearest the first chosen company; returns its unit count, 0 for none.</summary>
    int MarshalChooseNearestEnemy();

    /// <summary>
    /// Stores as <paramref name="slot"/> the point toward the chosen enemy company that leaves about <paramref name="reachMetres"/> between the two once both have walked
    /// at their own speeds, and returns the walk in metres (0 when the gap is already shorter); NaN when either company is missing.
    /// </summary>
    float MarshalPlanApproach(int slot, int company, float reachMetres);

    /// <summary>Starts counting the shots of the chosen company in any fire mode, and the soldiers who loosed them. A TAOM table, not an engine write.</summary>
    void MarshalWatchBegin(int company);

    void MarshalWatchEnd();

    int MarshalWatchShots { get; }

    int MarshalWatchShooters { get; }

    /// <summary>The soldiers of the chosen company in neither draw state who stand aside, and the rest of them, at the latest census.</summary>
    int MarshalAside(int company);

    int MarshalIdle(int company);

    /// <summary>Gives the selected companies a charge on the chosen enemy company through the player's order controller. An order write.</summary>
    void MarshalChargeEnemy();

    /// <summary>Metres from a chosen company to the chosen enemy company; NaN when either is missing.</summary>
    float MarshalEnemyDistance(int company);

    /// <summary>The chosen company's target company is the chosen enemy.</summary>
    bool MarshalMarkedEnemy(int company);

    /// <summary>The chosen company's movement order, by the engine's name for it.</summary>
    string MarshalOrderName(int company);

    /// <summary>The latest mark-effect read of the chosen company, or null before the first.</summary>
    MarkSample? MarshalMarkRead(int company);

    /// <summary>The latest mark-effect verdict of the chosen company, the one its check line carries, or null until a mark has been judged.</summary>
    MarkVerdict? MarshalMarkJudged(int company);

    /// <summary>Stores a press of key 543 as the key's own press is stored.</summary>
    void MarshalPressFire();

    /// <summary>Stores a press of key 544.</summary>
    void MarshalPressWord();

    FireMode MarshalFireMode(int company);

    /// <summary>The soldiers of the chosen company that hold a drawn bow at the latest census.</summary>
    int MarshalHolding(int company);

    VolleyVerdict? MarshalHoldVerdict { get; }

    VolleyVerdict? MarshalReleaseVerdict { get; }

    /// <summary>Units that wear an outline Marshal's Word painted.</summary>
    int MarshalOutlinesPainted { get; }
}
