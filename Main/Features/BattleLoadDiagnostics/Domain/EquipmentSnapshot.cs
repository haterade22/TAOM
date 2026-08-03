using System.Collections.Generic;

namespace TAOM.Features.BattleLoadDiagnostics.Domain;

// A spawning agent's full loadout, captured BEFORE the engine equips it. Plain DTO
// (ADR-007) built by IEquipmentSnapshotAdapter from the sealed Agent/Equipment types.
public sealed class EquipmentSnapshot
{
    public int AgentIndex { get; }
    public string AgentName { get; }
    public string CharacterId { get; }
    public string CultureId { get; }
    public IReadOnlyList<EquipmentSlotSnapshot> Slots { get; }

    // Skeleton identity. A race/monster/action-set mismatch is the shape that access-violates in
    // native mesh assembly with nothing logged, so it belongs on the line that is written and
    // flushed BEFORE the engine equips the agent.
    public string? Race { get; }
    public string? MonsterId { get; }
    public string? ActionSetName { get; }

    // Which engine method built this agent, outward from the call site. Answers what the loadout
    // cannot — e.g. a `musician_dunland` agent inside a TournamentFight mission, whose behavior
    // list has no MissionAgentHandler and whose roster cannot select a musician.
    public string? SpawnOrigin { get; }

    // The identity arguments are optional: they come from engine getters that can throw on a
    // half-built agent, and a snapshot with a null race is still worth logging.
    public EquipmentSnapshot(
        int agentIndex,
        string agentName,
        string characterId,
        string cultureId,
        IReadOnlyList<EquipmentSlotSnapshot> slots,
        string? race = null,
        string? monsterId = null,
        string? actionSetName = null,
        string? spawnOrigin = null)
    {
        AgentIndex = agentIndex;
        AgentName = agentName;
        CharacterId = characterId;
        CultureId = cultureId;
        Slots = slots;
        Race = race;
        MonsterId = monsterId;
        ActionSetName = actionSetName;
        SpawnOrigin = spawnOrigin;
    }
}
