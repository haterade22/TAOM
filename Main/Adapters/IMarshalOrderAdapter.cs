using System;
using System.Collections.Generic;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary>
/// Every read and write Marshal's Word makes on the player's team, its formations and its order controllers (build-spec 5.1 to 5.4 and
/// 6.4), so the queue driver and the march run in a test. A company is <c>Formation.Index</c> of the player's team, and a payload is the
/// adapter's own object (<c>MarshalOrderPayload</c>), opaque to everything else. Reads are cheap: cached for a line, the vanguard's live
/// position for a column; every member runs on the main thread, from <c>OnMissionTick</c> or the screen tick.
/// </summary>
internal interface IMarshalOrderAdapter
{
    /// <summary>Mission time in seconds (<c>Mission.CurrentTime</c>).</summary>
    double Now { get; }

    /// <summary><c>Mission.Mode</c> is Deployment.</summary>
    bool IsDeployment { get; }

    /// <summary>A direct player order reached the player's controller; the companies it ordered. Raised only for orders that contradict a walk.</summary>
    event Action<IReadOnlyList<int>>? DirectOrder;

    /// <summary>The player gave an Advance through the player's controller; the companies it ordered. Only this, or a change of movement order, ends a thrower run.</summary>
    event Action<IReadOnlyList<int>>? AdvanceGiven;

    /// <summary>The mission's player team changed. Every stored company number named a formation of the old team.</summary>
    event Action? PlayerTeamChanged;

    /// <summary>Subscribes to the player's controller and to the team change, and gives the patches the controller. Safe to call twice.</summary>
    void Attach();

    /// <summary>Unsubscribes and forgets its own controller. It does not write the patches' controller: the owner's <c>MarshalHooks.Clear</c> does. Safe to repeat; makes no native call.</summary>
    void Detach();

    /// <summary>The player's team is commanded by the player: <c>IsPlayerGeneral || IsPlayerSergeant</c>, the question the order panel asks before it opens.</summary>
    bool PlayerCommands { get; }

    /// <summary>The companies the player has selected on the panel.</summary>
    IReadOnlyList<int> SelectedCompanies();

    /// <summary>The company has units and is not under AI control, so a step may still order it (5.3, rule 2).</summary>
    bool IsControllable(int company);

    /// <summary>The company's current order is a Move and it has not arrived.</summary>
    bool WalkUnderWay(int company);

    /// <summary>The company has an order point, has undetached units, and its current position is within the arrival slack of the point (5.4).</summary>
    bool Arrived(int company);

    /// <summary>The company's order is no longer an attack on an entity.</summary>
    bool OrderEnded(int company);

    /// <summary>The payload's target company has no units left, or its orderable gives the player's side no order.</summary>
    bool PayloadSpent(object payload);

    /// <summary>Metres from the company's current position to its order point, or to the payload's entity; NaN when unknown.</summary>
    double RemainingDistance(int company, object? payload);

    /// <summary>The company's cached movement speed, in metres a second.</summary>
    double Speed(int company);

    /// <summary>
    /// Selects the companies on the master controller, issues the payload's order and clears the selection. False when none could be selected.
    /// A mark payload selects nothing and issues nothing: it sets each member's target company, and is false when no member took it.
    /// </summary>
    bool Issue(IReadOnlyList<int> companies, object payload);

    /// <summary>The ground point a payload orders toward, for the flag that shows the road; false when it has none.</summary>
    bool TryPoint(object payload, out float x, out float y, out float z);

    /// <summary>
    /// One leg per march target, read from the formations (6.4). A company with no units has no leg, and one that left its Move or went to
    /// the AI reports a shift of <c>float.MaxValue</c>.
    /// </summary>
    IReadOnlyList<MarchLeg> ReadMarch(IReadOnlyList<MarchTarget> targets);

    /// <summary>One read per player company that has a target company: the soldiers that aim at it, for the mark-effect check (build-spec 8.1).</summary>
    IReadOnlyList<MarkSample> ReadMarks();

    /// <summary>One read per player company in the plain engine square, for the square's proof line (build-spec 9.1).</summary>
    IReadOnlyList<SquareRead> ReadSquares();

    /// <summary>The first player company with a rider but not a significant number of them, with its four limits and the two values it could read; null when there is none (build-spec 9.3).</summary>
    SpacingRead? ReadSpacing();

    /// <summary>Publishes the paces to the speed postfix with one reference write; an empty list clears it.</summary>
    void PublishPaces(IReadOnlyList<MarchPace> paces);
}
