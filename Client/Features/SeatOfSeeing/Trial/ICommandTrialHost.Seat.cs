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

    SeatState State { get; }

    /// <summary>Frames the Seat has owned since its latest entry.</summary>
    int Frames { get; }

    bool GlideBegan { get; }

    bool Faulted { get; }
}
