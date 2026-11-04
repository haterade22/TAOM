using System.Collections.Generic;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// A group of soldiers to spawn into one formation. <c>FirstIndex</c> runs 0 to <c>BatchTotal</c> - 1 across the batch's soldiers,
/// and <c>Place</c> is true only for the first batch of a formation, because a later batch joins the formation where it stands.
/// </summary>
internal sealed record SoldierBatch(string TroopId, FormationSlot Slot, int Count, int FirstIndex, int BatchTotal, bool Reinforcement, bool Place);

internal sealed record WaveStart(int Wave, int Foot, int Ranged);

internal enum RunEventKind
{
    WaveCleared,
    Charge,
    Felled,
    Bought,
    Horn,
}

internal sealed record RunEvent(RunEventKind Kind, int Wave, string Detail);

/// <summary>What the engine must do after a tick or a call. Plain data: the director turns it into adapter calls.</summary>
internal sealed class RunEffects
{
    public List<SoldierBatch> DefenderBatches { get; } = new();
    public bool SpawnPlayer { get; set; }
    public bool FinishInitialSpawn { get; set; }
    public WaveStart? StartWave { get; set; }
    public List<SoldierBatch> AttackerBatches { get; } = new();

    /// <summary>The wave's first batch is out: force the attacker tactic and give it a fresh TacticBreachWalls.</summary>
    public bool RefreshAttackerTactic { get; set; }

    public bool OrderCharge { get; set; }
    public bool RestoreDefenderTactic { get; set; }
    public float RestShare { get; set; }
    public int FellAttackers { get; set; }
    public List<RunEvent> Events { get; } = new();
}
