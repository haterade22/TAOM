using DryIoc;
using TAOM.Adapters;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Features.Enlistment;

public static class EnlistmentIoC
{
    public static void RegisterEnlistmentFeature(IContainer container)
    {
        // Feature-owned adapters (sole consumers live in this feature)
        container.Register<ICommanderLordAdapter, CommanderLordAdapter>(Reuse.Singleton);
        container.Register<IMobilePartyAttachmentAdapter, MobilePartyAttachmentAdapter>(Reuse.Singleton);
        container.Register<IEncounterAdapter, EncounterAdapter>(Reuse.Singleton);
        container.Register<IGameMenuAdapter, GameMenuAdapter>(Reuse.Singleton);
        container.Register<IPlayerPartyAdapter, PlayerPartyAdapter>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);

        // Core state + services
        container.Register<IEnlistmentStore, EnlistmentStore>(Reuse.Singleton);
        container.Register<IEnlistmentStateMachine, EnlistmentStateMachine>(Reuse.Singleton);
        container.Register<IEnlistmentConfigProvider, EnlistmentConfigProvider>(Reuse.Singleton);
        container.Register<IServiceAttachmentService, ServiceAttachmentService>(Reuse.Singleton);
        container.Register<IDischargeService, DischargeService>(Reuse.Singleton);
        container.Register<IEnlistmentService, EnlistmentService>(Reuse.Singleton);
        container.Register<IEnlistmentStateQuery, EnlistmentStateQuery>(Reuse.Singleton);
        container.Register<IEnlistmentReconciler, EnlistmentReconciler>(Reuse.Singleton);
        container.Register<IEnlistmentLoadNormalizer, EnlistmentLoadNormalizer>(Reuse.Singleton);
        container.Register<IEnlistmentMenuService, EnlistmentMenuService>(Reuse.Singleton);
        container.Register<IServiceBattleService, ServiceBattleService>(Reuse.Singleton);

        container.Register<IEnlistmentWaitMenuPresenter, EnlistmentWaitMenuPresenter>(Reuse.Singleton);

        // Entry points (resolved by SubModule for AddBehavior)
        container.Register<EnlistmentBehavior>(Reuse.Singleton);
        container.Register<EnlistmentMenuBehavior>(Reuse.Singleton);
        container.Register<EnlistmentBattleBehavior>(Reuse.Singleton);
    }
}
