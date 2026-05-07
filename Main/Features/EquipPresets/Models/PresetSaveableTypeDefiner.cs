using System.Collections.Generic;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.EquipPresets.Models;

/// <summary>
/// Registers <see cref="HoNEquipmentPreset"/> + <see cref="HoNPresetItemReference"/> with the
/// save system. <see cref="SaveableTypeDefiner"/> instances are auto-discovered by TaleWorlds
/// via reflection — no manual SubModule wiring required.
///
/// BaseId 726900501 — verified unique across TAOM at port time (no other SaveableTypeDefiner
/// in Main/). Item-ref id 101, preset id 102 (under the base id, both unique). Document the
/// chosen range here so future TAOM features know to pick a different base id.
/// </summary>
public sealed class PresetSaveableTypeDefiner : SaveableTypeDefiner
{
    public const int SaveBaseId = 726900501;

    public PresetSaveableTypeDefiner() : base(SaveBaseId)
    {
    }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(HoNPresetItemReference), 101);
        AddClassDefinition(typeof(HoNEquipmentPreset), 102);
    }

    protected override void DefineContainerDefinitions()
    {
        ConstructContainerDefinition(typeof(List<HoNPresetItemReference>));
        ConstructContainerDefinition(typeof(List<HoNEquipmentPreset>));
        ConstructContainerDefinition(typeof(Dictionary<string, List<HoNEquipmentPreset>>));
    }
}
