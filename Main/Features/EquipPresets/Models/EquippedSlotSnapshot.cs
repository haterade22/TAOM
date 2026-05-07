namespace TAOM.Features.EquipPresets.Models;

/// <summary>
/// POCO returned by <see cref="Adapters.IEquipmentSlotAdapter.Capture"/>. Carries the StringIds
/// only (not <c>EquipmentElement</c>) so the service stays free of TaleWorlds types per ADR-007.
/// The adapter implementation owns the round-trip back to <c>EquipmentElement(item, modifier)</c>
/// using the modifier-preserving setter on <c>Equipment[EquipmentIndex]</c>.
/// </summary>
public sealed class EquippedSlotSnapshot
{
    public EquippedSlotSnapshot(int slotIndex, string itemStringId, string? itemModifierStringId)
    {
        SlotIndex = slotIndex;
        ItemStringId = itemStringId;
        ItemModifierStringId = itemModifierStringId;
    }

    public int SlotIndex { get; }
    public string ItemStringId { get; }
    public string? ItemModifierStringId { get; }
}
