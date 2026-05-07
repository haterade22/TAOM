using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.EquipPresets.Models;

/// <summary>
/// Saveable preset record for a single hero. Mirrors the decompiled-source shape verbatim:
/// separate <see cref="Items"/> / <see cref="CivilianItems"/> lists and independent
/// <see cref="IncludesMount"/> / <see cref="IncludesCivilianEquipment"/> flags. The flags
/// determine which slot sets are applied on load (mount and harness on/off, civilian copy on/off).
/// </summary>
public class HoNEquipmentPreset
{
    public HoNEquipmentPreset()
    {
        Name = string.Empty;
        Id = string.Empty;
        Items = new List<HoNPresetItemReference>();
        CivilianItems = new List<HoNPresetItemReference>();
    }

    [SaveableProperty(1)] public string Name { get; set; }
    [SaveableProperty(2)] public string Id { get; set; }
    [SaveableProperty(3)] public List<HoNPresetItemReference> Items { get; set; }
    [SaveableProperty(4)] public List<HoNPresetItemReference> CivilianItems { get; set; }
    [SaveableProperty(5)] public bool IncludesMount { get; set; }
    [SaveableProperty(6)] public bool IncludesCivilianEquipment { get; set; }
}
