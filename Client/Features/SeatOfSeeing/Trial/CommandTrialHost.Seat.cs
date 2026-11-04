using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing.Trial;

internal sealed partial class CommandTrialHost
{
    public void PressSeatKey() => _view.PressSeatKey();

    public bool PressPending => _view.PressPending;

    public void SetInput(SeatInput? input) => _view.TrialInput = input;

    public SeatState State => _session.State;

    public int Frames => _session.Frames;

    public bool GlideBegan => _session.GlideBegan;

    public bool Faulted => _session.Faulted;
}
