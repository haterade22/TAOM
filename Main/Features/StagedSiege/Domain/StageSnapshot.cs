namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// An immutable picture of the siege at one reconcile pass. The service replaces it each pass; the AI thread, the spawn patch and
/// the HUD only ever read it, so they need no lock.
/// </summary>
public sealed class StageSnapshot
{
    private StageSnapshot(object missionToken, StagedSide playerSide, bool hallFollows, StageId stage, StagedEnd end, int capturedMask, float progress,
        CaptureStatus status, StageZone zone, StageFrame? defenderHold, StagePoint? attackerSupport, StageFrame? attackerSpawn,
        StageFrame? defenderSpawn)
    {
        MissionToken = missionToken;
        PlayerSide = playerSide;
        HallFollows = hallFollows;
        Stage = stage;
        End = end;
        CapturedMask = capturedMask;
        Progress = progress;
        Status = status;
        Zone = zone;
        DefenderHold = defenderHold;
        AttackerSupport = attackerSupport;
        AttackerSpawn = attackerSpawn;
        DefenderSpawn = defenderSpawn;
    }

    /// <summary>The mission this snapshot belongs to; a reader compares it to the mission it serves.</summary>
    public object MissionToken { get; }

    public StagedSide PlayerSide { get; }

    /// <summary>The lord's hall (point E) follows the walls: a campaign siege with the player attacking.</summary>
    public bool HallFollows { get; }

    public StageId Stage { get; }
    public StagedEnd End { get; }
    public bool Finished => End != StagedEnd.None;
    public int CapturedMask { get; }
    public float Progress { get; }
    public CaptureStatus Status { get; }
    public StageZone Zone { get; }

    /// <summary>Where the defenders' ranged troops hold; null in stage A.</summary>
    public StageFrame? DefenderHold { get; }

    /// <summary>Where the attackers' ranged troops support from (the previous point's centre); null in stage A.</summary>
    public StagePoint? AttackerSupport { get; }

    public StageFrame? AttackerSpawn { get; }
    public StageFrame? DefenderSpawn { get; }

    public static StageSnapshot Build(object token, ScenePlan scene, StageMachine machine, float progress, CaptureStatus status)
    {
        var stage = machine.Current;
        return new StageSnapshot(
            token,
            machine.PlayerSide,
            machine.HallFollows,
            stage,
            machine.End,
            machine.CapturedMask,
            progress,
            status,
            scene.Zone(stage),
            scene.DefenderFrame(stage),
            stage == StageId.A ? (StagePoint?)null : scene.Zone(stage - 1).Center,
            scene.AttackerSpawn(stage),
            scene.DefenderSpawn(stage));
    }
}
