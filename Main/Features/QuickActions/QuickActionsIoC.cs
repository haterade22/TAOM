using DryIoc;
using TAOM.Adapters;
using TAOM.Features.QuickActions.Audio;
using TAOM.Features.QuickActions.Hooks;

namespace TAOM.Features.QuickActions;

public static class QuickActionsIoC
{
    public static void RegisterQuickActionsFeature(IContainer container)
    {
        container.Register<IQuickActionsSettingsProvider, QuickActionsSettingsProvider>(Reuse.Singleton);
        container.Register<IInventoryVMAdapter, InventoryVMAdapter>(Reuse.Singleton);
        container.Register<IQuickActionsAudioPlayer, QuickActionsAudioPlayer>(Reuse.Singleton);
        container.Register<InventorySearchCampaignBehavior>(Reuse.Singleton);
        container.Register<IQuickActionsService, QuickActionsService>(Reuse.Singleton);
    }
}
