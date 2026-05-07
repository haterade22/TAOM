using DryIoc;
using TAOM.Adapters;
using TAOM.Features.EquipPresets.Hooks;

namespace TAOM.Features.EquipPresets;

public static class EquipPresetsIoC
{
    public static void RegisterEquipPresetsFeature(IContainer container)
    {
        container.Register<IEquipPresetsSettingsProvider, EquipPresetsSettingsProvider>(Reuse.Singleton);
        container.Register<IEquipmentSlotAdapter, EquipmentSlotAdapter>(Reuse.Singleton);
        container.Register<IItemModifierLookupAdapter, ItemModifierLookupAdapter>(Reuse.Singleton);
        container.Register<IInventoryScreenAdapter, InventoryScreenAdapter>(Reuse.Singleton);
        container.Register<IEquipmentPresetService, EquipmentPresetService>(Reuse.Singleton);
        container.Register<EquipmentPresetCampaignBehavior>(Reuse.Singleton);
    }
}
