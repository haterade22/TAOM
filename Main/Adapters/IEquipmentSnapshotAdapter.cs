using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Adapters;

// ADR-007 boundary: converts the sealed Agent + Equipment + ItemObject into a plain
// EquipmentSnapshot DTO. The ONLY place in the BattleLoadDiagnostics path that touches
// TaleWorlds types. `agent` is typed `object` so the service and tests never reference
// the sealed Agent type. Returns null when the argument is not an Agent.
public interface IEquipmentSnapshotAdapter
{
    /// <param name="spawnOrigin">
    /// Pre-formatted caller chain from the patch boundary, or null. Passed through rather than
    /// captured here: a managed <c>StackTrace</c> is not a TaleWorlds type and does not belong
    /// behind the engine-adapter boundary.
    /// </param>
    EquipmentSnapshot? Capture(object agent, string? spawnOrigin = null);
}
