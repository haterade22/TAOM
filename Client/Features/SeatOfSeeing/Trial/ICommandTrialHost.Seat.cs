using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal partial interface ICommandTrialHost
{
    /// <summary>The Seat key, taken at the next screen tick: <c>SeatView.PressSeatKey</c>.</summary>
    void PressSeatKey();

    /// <summary>The press has not yet been taken by the session.</summary>
    bool PressPending { get; }

    /// <summary>Sets <c>SeatView.TrialInput</c>: the flight input the Seat uses in place of the player's.</summary>
    void SetInput(SeatInput? input);

    /// <summary>The ground direction the view looks along (a unit vector, Z zero), which a flight forward follows; null when the camera cannot be read.</summary>
    Vec3f? ViewForward();

    SeatState State { get; }

    /// <summary>Frames the Seat has owned since its latest entry.</summary>
    int Frames { get; }

    bool GlideBegan { get; }

    bool Faulted { get; }

    /// <summary>The key to hide the interface: <c>SeatSession.PressHide</c>, the method the real key calls.</summary>
    void PressHide();

    /// <summary>The order panel is open: <c>Mission.IsOrderMenuOpen</c>.</summary>
    bool PanelOpen { get; }

    /// <summary>The screen shows a cursor: <c>MissionScreen.MouseVisible</c>.</summary>
    bool MouseVisible { get; }

    /// <summary>The flag that hides the whole interface: <c>MBDebug.DisableAllUI</c>.</summary>
    bool UiHidden { get; }

    /// <summary>Opens the order panel through its view model, as the Seat's own reopen does. A mission flag write: call it after the in-mission-tick marker. False when the view model cannot be reached.</summary>
    bool OpenPanel();

    /// <summary>Closes the order panel; <paramref name="asPlayer"/> makes the close look like the player's own route, so the Seat does not reopen it. A mission flag write: a step calls it after the in-mission-tick marker.</summary>
    void ClosePanel(bool asPlayer);

    /// <summary>The game window has the focus, which the cursor step needs to place the cursor.</summary>
    bool WindowFocused { get; }

    /// <summary>Places the cursor at a fraction of the screen (<c>Input.SetMousePosition</c>).</summary>
    void MoveCursorTo(float fractionX, float fractionY);

    /// <summary>The pixel the cursor stands on, the pointer function's value for it and the focus handler's own point.</summary>
    SeatFocusReading ProbeFocus();

    /// <summary>How many messages the Seat has shown in this mission.</summary>
    int Said { get; }

    SeatMessage? LastSaid { get; }

    /// <summary>The key to keep to a target: <c>SeatSession.PressKeep</c>.</summary>
    void PressKeep();

    /// <summary>What the Seat keeps to now.</summary>
    SeatKeepKind Keeping { get; }

    /// <summary>The kept target is the soldier or the company the step chose (a reference, never an index).</summary>
    bool KeepsChosen { get; }

    /// <summary>The camera's distance from the kept target.</summary>
    float DistanceToKept();

    /// <summary>Chooses the player-team soldier nearest to the view (no hero, no mount); false when there is none.</summary>
    bool ChooseSoldier();

    /// <summary>The look input that turns the view onto the chosen soldier, and whether it is on him or he is gone.</summary>
    SeatAimStep AimAtChosen();

    /// <summary>Chooses a player company with units: the largest, or the smallest one that holds no hero. Returns its unit count, 0 for none.</summary>
    int ChooseCompany(bool largest);

    /// <summary>Makes the chosen company the only selected one. A formation write: call it after the in-mission-tick marker.</summary>
    void SelectChosenCompany();

    /// <summary>Kills the chosen soldier through <c>Mission.KillAgentCheat</c>. An agent write: call it after the in-mission-tick marker.</summary>
    void KillChosenSoldier();

    /// <summary>Kills every unit of the chosen company through <c>Mission.KillAgentCheat</c>. An agent write: call it after the in-mission-tick marker.</summary>
    void KillChosenCompany();
}
