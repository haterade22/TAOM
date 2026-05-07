namespace TAOM.Features.EquipPresets.Models;

/// <summary>
/// Per-slot apply instruction passed to <see cref="Adapters.IInventoryScreenAdapter.LoadEquipment"/>.
/// Carries StringIds only; the adapter looks up <c>ItemObject</c> / <c>ItemModifier</c> internally.
/// </summary>
public sealed class PresetSlotRequest
{
    public PresetSlotRequest(int slotIndex, string? itemStringId, string? itemModifierStringId)
    {
        SlotIndex = slotIndex;
        ItemStringId = itemStringId ?? string.Empty;
        ItemModifierStringId = itemModifierStringId ?? string.Empty;
    }

    /// <summary>0..11 (per <c>EquipmentIndex</c>).</summary>
    public int SlotIndex { get; }

    /// <summary>Empty string means "clear this slot" (unequip currently-equipped item back to inventory).</summary>
    public string ItemStringId { get; }

    /// <summary>Empty string means "no modifier".</summary>
    public string ItemModifierStringId { get; }
}
