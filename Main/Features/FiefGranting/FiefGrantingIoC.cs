using DryIoc;

namespace TAOM.Features.FiefGranting;

public static class FiefGrantingIoC
{
    public static void RegisterFiefGrantingFeature(IContainer container)
    {
        container.Register<IFiefGrantSettingsProvider, FiefGrantSettingsProvider>(Reuse.Singleton);
        container.Register<IFiefGrantPolicyService, FiefGrantPolicyService>(Reuse.Singleton);
        // Per-campaign state on a process singleton: FiefGrantingCampaignBehavior owns its SyncData
        // halves and the session reset (#565).
        container.Register<IFiefSiegeParticipationService, FiefSiegeParticipationService>(Reuse.Singleton);
    }
}
