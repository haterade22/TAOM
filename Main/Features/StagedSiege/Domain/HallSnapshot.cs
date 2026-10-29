namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// An immutable picture of point E in the lord's hall at one reconcile pass: the HUD's only input from the hall service, read with
/// one volatile read and compared by reference to the mission it serves (as <see cref="StageSnapshot"/>).
/// </summary>
public sealed class HallSnapshot
{
    public HallSnapshot(object missionToken, float progress, CaptureStatus status, StagePoint centre, bool finished)
    {
        MissionToken = missionToken;
        Progress = progress;
        Status = status;
        Centre = centre;
        Finished = finished;
    }

    public object MissionToken { get; }

    public float Progress { get; }

    public CaptureStatus Status { get; }

    /// <summary>The centroid of the E markers: where the flag stands and the HUD marker floats.</summary>
    public StagePoint Centre { get; }

    /// <summary>The attackers have taken the hall.</summary>
    public bool Finished { get; }
}
