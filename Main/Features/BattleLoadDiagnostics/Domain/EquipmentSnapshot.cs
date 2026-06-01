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

    public EquipmentSnapshot(
        int agentIndex,
        string agentName,
        string characterId,
        string cultureId,
        IReadOnlyList<EquipmentSlotSnapshot> slots)
    {
        AgentIndex = agentIndex;
        AgentName = agentName;
        CharacterId = characterId;
        CultureId = cultureId;
        Slots = slots;
    }
}
