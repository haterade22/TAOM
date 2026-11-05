using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.MarshalsWord;
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

    /// <summary>Massed words the driver has given, and the numbers the latest was given on, or null before the first.</summary>
    int MarshalMassedWords { get; }

    MassedWord? MarshalLastMassed { get; }

    /// <summary>Release windows the driver has judged, so a step can tell a new verdict from an older one of the same numbers.</summary>
    int MarshalReleasesJudged { get; }

    /// <summary>The soldiers of the chosen company drawing a bow at the latest census.</summary>
    int MarshalDrawing(int company);

    /// <summary>
    /// Splits the largest player company of foot soldiers that holds no hero in two, by giving the player's Transfer order for half of it to an empty formation of the player's team, and chooses the two:
    /// the first keeps the larger half. Returns how many companies are chosen, 0 when none could be split; the soldiers have moved only if both companies then hold some. A formation write: call it after the in-mission-tick marker.
    /// </summary>
    int MarshalSplitInfantry();

    /// <summary>The chosen company has soldiers and is not under AI control, so a key can toggle it.</summary>
    bool MarshalControllable(int company);

    /// <summary>Stores a press of key 545 as the key's own press is stored.</summary>
    void MarshalPressHold();

    /// <summary>Hold the line is on for the chosen company.</summary>
    bool MarshalHeld(int company);

    /// <summary>Melee blows the chosen company's soldiers have dealt this mission.</summary>
    int MarshalBlows(int company);

    /// <summary>Blows the chosen company's wounded soldiers dealt inside their strike back window.</summary>
    int MarshalStrikeBlows(int company);

    /// <summary>The soldiers of the chosen company whose attack flag reads off now, read from each soldier's own flags.</summary>
    int MarshalFlagOff(int company);

    /// <summary>Enemy soldiers within <paramref name="metres"/> of the chosen company.</summary>
    int MarshalEnemiesNear(int company, float metres);

    /// <summary>The latest hold-the-line verdict of the driver, or null.</summary>
    HoldVerdict? MarshalHoldLineVerdict { get; }

    /// <summary>Gives the selected companies the shield wall arrangement through the player's order controller. An order write.</summary>
    void MarshalOrderShieldWall();

    /// <summary>The driver's latest count of the chosen held company's line: who qualifies, the slot distances, the wounds and the strike back; empty before its first pass.</summary>
    HoldCensus MarshalHoldCensus(int company);

    /// <summary>The line of the chosen company has answered a wound with its attack flag, as the driver's strike check judged it.</summary>
    bool MarshalStrikePassed(int company);

    /// <summary>Soldiers the slot clamp steered who ended a hold read nearer their slot, summed over the chosen company's hold; 0 once the hold is lifted.</summary>
    int MarshalSlotCloser(int company);

    /// <summary>Soldiers the slot clamp steered who ended a hold read farther from their slot, summed over the chosen company's hold; 0 once the hold is lifted.</summary>
    int MarshalSlotFarther(int company);

    /// <summary>Steered hold reads of the chosen held company, summed since the hold went on: the denominator of the two sides.</summary>
    int MarshalSlotSteered(int company);

    /// <summary>The driver's slot check has failed for the chosen held company since the hold went on.</summary>
    bool MarshalSlotFailed(int company);

    /// <summary>The proof line's laidOut fact for the chosen company: the soldiers take their places from the mixed layout and not from the square.</summary>
    bool MarshalSquareLaidOut(int company);

    /// <summary>Inputs the slot clamp has replaced since the process began, from the counter the session summary prints.</summary>
    int MarshalClamps { get; }

    /// <summary>The metres between the chosen company's position and its order point, 0 with no order point.</summary>
    float MarshalOrderGap(int company);

    /// <summary>Chooses the largest player company with no rider and no ranged soldier that holds no hero; returns its unit count, 0 for none.</summary>
    int MarshalChoosePureInfantry();

    /// <summary>Gives the selected companies the Square order through the player's order controller. An order write.</summary>
    void MarshalOrderSquare();

    /// <summary>The chosen company's arrangement is the plain engine square and not the packed schiltron.</summary>
    bool MarshalPlainSquare(int company);

    /// <summary>The chosen company's arrangement name, for the line a failed step writes.</summary>
    string MarshalArrangementName(int company);

    /// <summary>The square's side and its bounds, the width of one unit, and the soldiers with and without a slot; NaN or 0 when the arrangement is not the plain square.</summary>
    float MarshalSquareSide(int company);

    float MarshalSquareMinimum(int company);

    float MarshalSquareMaximum(int company);

    float MarshalUnitWidth(int company);

    int MarshalUnits(int company);

    int MarshalPlaced(int company);

    /// <summary>Stores a line of <paramref name="side"/> metres across the chosen company's front, centred where the company stands, for <see cref="MarshalDrag"/>.</summary>
    void MarshalPlanSideDrag(int company, float side);

    /// <summary>Kills the last soldier of the chosen company through the engine's own kill. An agent write: call it after the in-mission-tick marker. False when the company has none.</summary>
    bool MarshalKillOne(int company);

    /// <summary>Gives the player's Transfer order for one rider of the largest company of riders into the chosen company of foot soldiers. Returns that company's unit count after the order, 0 when there was no pair to order. A formation write.</summary>
    int MarshalMoveOneRider();

    /// <summary>How the spacing proof read the company with one rider, or null while it has not.</summary>
    SpacingReading? MarshalSpacingVerdict { get; }

    /// <summary>Why the spacing repair is not applied, or an empty string while it is.</summary>
    string MarshalSpacingWhy { get; }

    /// <summary>The riders of the chosen company counted fresh, and whether the engine counts it as having any rider and a significant number of them.</summary>
    int MarshalRiders(int company);

    bool MarshalHasAnyRider(int company);

    bool MarshalHasSignificantRiders(int company);

    /// <summary>The chosen company's four spacing limits and the values it could read as foot soldiers and as cavalry, read from the engine now; null for no company.</summary>
    SpacingRead? MarshalSpacingOfChosen(int company);
}
