using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>Command mouse: sets the trial's own value of "Command with the mouse", in memory only (a run starts with it off).</summary>
    void SetCommandMouse(bool on);

    /// <summary>The Seat's command mouse is live (setting on, seated, orders allowed, no network session).</summary>
    bool CommandLive { get; }

    /// <summary>
    /// Chooses the player's soldier nearest the view, on foot or mounted, with no hero. With <paramref name="otherCompany"/> true, the nearest one in
    /// a company other than the first one chosen. False when there is none. The soldier is also the one <c>AimAtChosen</c> turns the view onto.
    /// </summary>
    bool MouseChooseSoldier(bool otherCompany);

    /// <summary>Chooses the enemy soldier nearest the view (the one <c>AimAtChosen</c> turns the view onto); false when there is none.</summary>
    bool MouseChooseEnemy();

    /// <summary>Makes the first chosen soldier's company the only selected one. A formation write: call it after the in-mission-tick marker.</summary>
    void MouseSelectFirstCompany();

    /// <summary>The chosen soldier's chest on the screen; not found when it is behind the camera or off the window.</summary>
    SeatMousePoint MouseProjectChosen();

    /// <summary>The ground point at the centre of the view, moved right of the view's heading by <paramref name="rightMetres"/>, on the screen.</summary>
    SeatMousePoint MouseGround(float rightMetres);

    /// <summary>A click of the left or right button at a point, with the modifier keys, injected into the pointer the Seat reads in place of the mouse (never an operating system click).</summary>
    void MouseClick(SeatMousePoint at, bool right, bool shift, bool ctrl);

    /// <summary>A right drag from one point to another: a press, a held frame past a click's 0.3 s, and a release.</summary>
    void MouseDragRight(SeatMousePoint from, SeatMousePoint to);

    /// <summary>How many companies the player's order controller has selected.</summary>
    int MouseSelectedCount { get; }

    /// <summary>The first chosen company, and only it, is selected.</summary>
    bool MouseOnlyFirstSelected { get; }

    /// <summary>Both the first and the second chosen company are selected, and nothing else.</summary>
    bool MouseFirstAndSecondSelected { get; }

    /// <summary>The distance in metres from a point to the order position of the first selected company; NaN with none.</summary>
    float MouseOrderDistance(SeatMousePoint to);

    /// <summary>The angle in degrees between the first selected company's facing and the normal of the line between two points; NaN with none.</summary>
    float MouseFacingError(SeatMousePoint from, SeatMousePoint to);

    /// <summary>The first selected company has the Charge order against the company of the chosen enemy soldier.</summary>
    bool MouseChargesChosenEnemy { get; }

    /// <summary>Gives back the command mouse value, the injected pointer and the queue key; safe to call at any time.</summary>
    void ReleaseMouseTrial();
}
