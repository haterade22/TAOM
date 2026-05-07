using DryIoc;

namespace TAOM.Features.Messengers;

public static class MessengerIoC
{
    public static void RegisterMessengerFeature(IContainer container)
    {
        container.Register<IMessengerSettingsProvider, MessengerSettingsProvider>(Reuse.Singleton);
        container.Register<IMessengerConfigProvider, MessengerConfigProvider>(Reuse.Singleton);
        container.Register<IMessengerStateStore, MessengerStateStore>(Reuse.Singleton);
        container.Register<IMessengerRandomSource, MessengerRandomSource>(Reuse.Singleton);
        container.Register<IMessengerService, MessengerService>(Reuse.Singleton);
        container.Register<MessengerCampaignBehavior>(Reuse.Singleton);
    }
}
