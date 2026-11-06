using System.Collections.Generic;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.LongDefeat.Domain;

/// <summary>
/// A group of soldiers to spawn into one formation. <c>FirstIndex</c> runs 0 to <c>BatchTotal</c> - 1 across the batch's soldiers,
/// and <c>Place</c> is true only for the first batch of a formation, because a later batch joins the formation where it stands.
/// <c>HealthFactor</c> is what the adapter multiplies each spawned soldier's health by: an attacker's endless factor (times his rank's factor for a commander), a defender's combined health boon factor for its group.
/// <c>Role</c> is what an attacker is to its wave, and <c>Use</c> the number of the summon that queued a summoned soldier (0 for every other soldier); a defender is always a roster soldier.
/// </summary>
internal sealed record SoldierBatch(string TroopId, FormationSlot Slot, int Count, int FirstIndex, int BatchTotal, bool Reinforcement, bool Place, float HealthFactor = 1f,
    AttackerRole Role = AttackerRole.Roster, int Use = 0);

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
    Summoned,
    WarCry,
    WarCryEnded,
    CommanderEnded,
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

    /// <summary>
    /// The factor the live rams' and siege towers' hit points are set to over their vanilla value (<see cref="RunModifiers.EngineHitPoints"/>), at each wave start and applied with
    /// <see cref="PublishStats"/> before any soldier spawns: 1 restores the vanilla value. Null at every other tick.
    /// </summary>
    public float? EngineHitPoints { get; set; }

    /// <summary>A Winged Terror pulse is due: lower the morale of the defenders near the attackers, never below the pulse's floor. Null at every other tick.</summary>
    public LdMoralePulse? MoralePulse { get; set; }

    /// <summary>A troop or class boon was taken since the last tick: refresh and scale those standing, after <see cref="PublishStats"/> and before any soldier spawns.</summary>
    public LdBoonApply? ApplyBoon { get; set; }
    public List<SoldierBatch> AttackerBatches { get; } = new();

    /// <summary>The Morgul Blades damage due this tick: whole points for each poisoned defender, to the attacker who dosed him. Empty outside a blades wave.</summary>
    public List<PoisonTick> PoisonTicks { get; } = new();

    /// <summary>A war cry began or ended (or the commander who raised it fell): derive every standing attacker's properties again, after <see cref="PublishStats"/>.</summary>
    public bool RefreshAttackers { get; set; }

    /// <summary>The wave's first batch is out: force the attacker tactic and give it a fresh TacticBreachWalls.</summary>
    public bool RefreshAttackerTactic { get; set; }

    public bool OrderCharge { get; set; }
    public bool RestoreDefenderTactic { get; set; }
    public float RestShare { get; set; }
    public int FellAttackers { get; set; }
    public List<RunEvent> Events { get; } = new();
}
