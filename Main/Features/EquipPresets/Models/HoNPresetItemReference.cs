using TaleWorlds.SaveSystem;

namespace TAOM.Features.EquipPresets.Models;

/// <summary>
/// Saveable record for a single equipment slot in a preset. Stores StringIds rather than the
/// resolved <c>EquipmentElement</c> so the data survives item-XML changes between saves —
/// missing items are reported via <see cref="PresetLoadResult.MissingItems"/> at load time.
/// </summary>
public class HoNPresetItemReference
{
    public HoNPresetItemReference()
    {
        ItemStringId = string.Empty;
        ItemModifierStringId = string.Empty;
    }

    public HoNPresetItemReference(int slotIndex, string itemStringId, string itemModifierStringId)
    {
        SlotIndex = slotIndex;
        ItemStringId = itemStringId ?? string.Empty;
        ItemModifierStringId = itemModifierStringId ?? string.Empty;
    }

    /// <summary>The integer cast of <c>EquipmentIndex</c> (0..11).</summary>
    [SaveableProperty(1)] public int SlotIndex { get; set; }

    /// <summary><c>ItemObject.StringId</c> — empty when the slot was empty at capture time.</summary>
    [SaveableProperty(2)] public string ItemStringId { get; set; }

    /// <summary><c>ItemModifier.StringId</c> if present; empty string for "no modifier".</summary>
    [SaveableProperty(3)] public string ItemModifierStringId { get; set; }
}
