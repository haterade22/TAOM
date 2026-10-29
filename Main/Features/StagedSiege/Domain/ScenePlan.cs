using System;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>
/// One recorded scene: the four capture points, the detachment frame at A and the defender frames at B, C and D. Attackers
/// spawn at the previous point facing the next, so no attacker frame is stored.
/// </summary>
public sealed class ScenePlan
{
    private readonly StageZone[] _zones;
    private readonly StageFrame[] _defenders;

    public ScenePlan(string scene, StageZone[] zones, StageFrame detachment, StageFrame defenderB, StageFrame defenderC, StageFrame defenderD)
    {
        if (zones == null || zones.Length != 4)
            throw new ArgumentException("a scene plan needs exactly four zones", nameof(zones));
        Scene = scene;
        _zones = (StageZone[])zones.Clone();
        Detachment = detachment;
        _defenders = new[] { default(StageFrame), defenderB, defenderC, defenderD };
    }

    public string Scene { get; }

    /// <summary>Where the detachment of defenders is placed at the start of stage A.</summary>
    public StageFrame Detachment { get; }

    public StageZone Zone(StageId id) => _zones[(int)id];

    /// <summary>The defenders' hold frame for a point; null for A, which has none.</summary>
    public StageFrame? DefenderFrame(StageId id) => id == StageId.A ? (StageFrame?)null : _defenders[(int)id];

    /// <summary>Attacker reinforcements appear on the previous point facing this one; null for A.</summary>
    public StageFrame? AttackerSpawn(StageId id)
    {
        if (id == StageId.A)
            return null;
        var previous = Zone(id - 1).Center;
        return new StageFrame(previous, StageFacing.Toward(previous, Zone(id).Center));
    }

    /// <summary>Defender reinforcements appear on the recorded hold frame; null for A.</summary>
    public StageFrame? DefenderSpawn(StageId id) => DefenderFrame(id);
}
