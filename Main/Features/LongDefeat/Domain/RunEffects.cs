using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// A group of soldiers to spawn into one formation. <c>FirstIndex</c> runs 0 to <c>BatchTotal</c> - 1 across the batch's soldiers,
/// and <c>Place</c> is true only for the first batch of a formation, because a later batch joins the formation where it stands.
/// <c>HealthFactor</c> is what the adapter multiplies each spawned soldier's health by: an attacker's endless factor, a defender's combined health boon factor for its group.
/// </summary>
internal sealed record SoldierBatch(string TroopId, FormationSlot Slot, int Count, int FirstIndex, int BatchTotal, bool Reinforcement, bool Place, float HealthFactor = 1f);

/// <summary>A wave begins: how many soldiers its first batch holds by class. The wave's factors ride <see cref="SoldierBatch.HealthFactor"/> and <see cref="RunEffects.PublishStats"/>, and the director reads its spec for the log.</summary>
internal sealed record WaveStart(int Wave, int Foot, int Ranged);

internal enum RunEventKind
{
    WaveCleared,
    Charge,
    Felled,
    Bought,
    Horn,
    BoonOffered,
    BoonLapsed,
    BoonTaken,
    Rerolled,
}

internal sealed record RunEvent(RunEventKind Kind, int Wave, string Detail);

/// <summary>A health factor for those standing in one group: one boon's own factor, never the combined one.</summary>
internal readonly record struct LdHealthScale(BoonTarget Target, float Factor);

/// <summary>What a troop or class pick does to those already standing: the groups whose driven properties must be derived again, and the health factors to scale by. Plain data; the table itself rides <see cref="RunEffects.PublishStats"/>.</summary>
internal sealed record LdBoonApply(IReadOnlyList<BoonTarget> Refresh, IReadOnlyList<LdHealthScale> Health);

/// <summary>What the engine must do after a tick or a call. Plain data: the director turns it into adapter calls.</summary>
internal sealed class RunEffects
{
    public List<SoldierBatch> DefenderBatches { get; } = new();
    public bool SpawnPlayer { get; set; }
    public bool FinishInitialSpawn { get; set; }
    public WaveStart? StartWave { get; set; }

    /// <summary>What the effects adapter publishes into the engine's hooks, applied before any soldier of the wave spawns: set with <see cref="StartWave"/>.</summary>
    public LdStatTable? PublishStats { get; set; }

    /// <summary>A troop or class boon was taken since the last tick: refresh and scale those standing, after <see cref="PublishStats"/> and before any soldier spawns.</summary>
    public LdBoonApply? ApplyBoon { get; set; }
    public List<SoldierBatch> AttackerBatches { get; } = new();

    /// <summary>The wave's first batch is out: force the attacker tactic and give it a fresh TacticBreachWalls.</summary>
    public bool RefreshAttackerTactic { get; set; }

    public bool OrderCharge { get; set; }
    public bool RestoreDefenderTactic { get; set; }
    public float RestShare { get; set; }
    public int FellAttackers { get; set; }
    public List<RunEvent> Events { get; } = new();
}
