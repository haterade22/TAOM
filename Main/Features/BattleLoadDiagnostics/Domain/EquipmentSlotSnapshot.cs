namespace TAOM.Features.BattleLoadDiagnostics.Domain;

// One equipped slot captured at agent-spawn time, reduced to plain strings so the
// service/formatter never touch sealed TaleWorlds types (ADR-007). BodyName /
// CollisionBodyName are the collision-mesh refs (XML `body_name` / `shield_body_name`);
// a null/empty value on the last slot logged before a hang names the missing `bo_`
// collision mesh the engine stalled on.
public sealed class EquipmentSlotSnapshot
{
    public string SlotName { get; }
    public string ItemId { get; }
    public string? BodyName { get; }
    public string? CollisionBodyName { get; }
    public string? HolsterBodyName { get; }
    public string? MultiMeshName { get; }
    public string ComponentKind { get; }

    public EquipmentSlotSnapshot(
        string slotName,
        string itemId,
        string? bodyName,
        string? collisionBodyName,
        string? holsterBodyName,
        string? multiMeshName,
        string componentKind)
    {
        SlotName = slotName;
        ItemId = itemId;
        BodyName = bodyName;
        CollisionBodyName = collisionBodyName;
        HolsterBodyName = holsterBodyName;
        MultiMeshName = multiMeshName;
        ComponentKind = componentKind;
    }
}
