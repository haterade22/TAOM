using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.PlayerSwitcher;

public static class PlayerSwitcherIoC
{
    public static void RegisterPlayerSwitcherFeature(IContainer container)
    {
        container.Register<IHeroPickerAdapter, HeroPickerAdapter>(Reuse.Singleton);
        container.Register<IPlayerIdentityAdapter, PlayerIdentityAdapter>(Reuse.Singleton);
        container.Register<IHeroPickerService, HeroPickerService>(Reuse.Singleton);
        container.Register<ISwitchPlanner, SwitchPlanner>(Reuse.Singleton);
        container.Register<IHeroSwitchService, HeroSwitchService>(Reuse.Singleton);
        container.Register<IKingdomJoinAdapter, KingdomJoinAdapter>(Reuse.Singleton);
        container.Register<IKingdomJoinOfferService, KingdomJoinOfferService>(Reuse.Singleton);
        container.Register<IPlayerSwitchPolicyProvider, PlayerSwitchPolicyProvider>(Reuse.Singleton);

        // One store, two faces. The reader goes to consumers that must observe the selection but
        // never change it (Patch9_RaceFilter); the writer goes to the picker and the patch that
        // clears it. Mapping rather than two registrations, because two instances would mean the
        // race filter reading a selection the picker never made.
        container.Register<IPlayerSwitchSession, PlayerSwitchSessionStore>(Reuse.Singleton);
        container.RegisterMapping<IPlayerSwitchSessionWriter, IPlayerSwitchSession>();
    }
}
