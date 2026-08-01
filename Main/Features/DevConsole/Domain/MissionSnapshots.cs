using System.Collections.Generic;

namespace TAOM.Features.DevConsole.Domain;

/// <summary>
/// Engine-free views of mission state, so every console report that describes a mission can be
/// rendered and unit-tested without a live <c>Mission</c> (ADR-007: sealed TaleWorlds types are
/// converted at the boundary, never carried into the reporting layer).
/// </summary>
public sealed class AgentSnapshot
{
    public int Index { get; set; }
    public string Name { get; set; }
    public string CharacterId { get; set; }

    /// <summary>Null when the agent's race id is not in the registry — see the validate-before-lookup note in the formatter.</summary>
    public string RaceName { get; set; }
    public int RaceId { get; set; }

    public string MonsterId { get; set; }
    public string ActionSetName { get; set; }
    public string SkeletonName { get; set; }

    /// <summary>
    /// Nullable on purpose. A failed read must NOT collapse to <c>0</c>: zero is a plausible,
    /// meaningful health value (a downed agent), so a reader would take "the read threw" for "this
    /// agent is dead". Null renders as an explicit unknown instead.
    /// </summary>
    public float? Health { get; set; }

    public float? MaxHealth { get; set; }
    public string TeamLabel { get; set; }
    public string FormationLabel { get; set; }
    public bool IsHuman { get; set; }
    public bool IsMount { get; set; }
    public string MountMonsterId { get; set; }
    public string RiderName { get; set; }
    public IReadOnlyList<string> EquipmentSlots { get; set; }
}

/// <summary>Result of a <c>taom.spawn_troops</c> run — partial success is a normal outcome, not an error.</summary>
public sealed class SpawnOutcome
{
    public string TroopId { get; set; }
    public int Requested { get; set; }
    public int Spawned { get; set; }
    public string TeamLabel { get; set; }

    /// <summary>Non-null only when nothing could be spawned at all.</summary>
    public string FailureReason { get; set; }
}

/// <summary>What battle terrain a fight at the party's current map position would load.</summary>
public sealed class BattleSceneQuery
{
    public float X { get; set; }
    public float Y { get; set; }
    public int MapIndex { get; set; }
    public IReadOnlyList<string> CandidateSceneIds { get; set; }
}
