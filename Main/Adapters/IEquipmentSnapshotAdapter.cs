using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Adapters;

// ADR-007 boundary: converts the sealed Agent + Equipment + ItemObject into a plain
// EquipmentSnapshot DTO. The ONLY place in the BattleLoadDiagnostics path that touches
// TaleWorlds types. `agent` is typed `object` so the service and tests never reference
// the sealed Agent type. Returns null when the argument is not an Agent.
public interface IEquipmentSnapshotAdapter
{
    EquipmentSnapshot? Capture(object agent);
}
