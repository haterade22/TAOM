using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.WarOfTheRingMomentum;

public static class WarOfTheRingMomentumIoC
{
    // IPlayerContextAdapter and IAllianceAdapter are registered by SiegeDefenseIoC /
    // DiplomacyIoC — do not re-register (DryIoc appends duplicates).
    public static void RegisterWarOfTheRingMomentumFeature(IContainer container)
    {
        container.Register<IMomentumConfigProvider, MomentumConfigProvider>(Reuse.Singleton);
        container.Register<IMomentumSettingsProvider, MomentumSettingsProvider>(Reuse.Singleton);
        container.Register<IMomentumStateStore, MomentumStateStore>(Reuse.Singleton);
        container.Register<IMomentumTextService, MomentumTextService>(Reuse.Singleton);
        container.Register<IPlayerMomentumService, PlayerMomentumService>(Reuse.Singleton);
        container.Register<IMomentumEventService, MomentumEventService>(Reuse.Singleton);
        container.Register<IMomentumEnrollmentService, MomentumEnrollmentService>(Reuse.Singleton);
        container.Register<IMomentumVictoryService, MomentumVictoryService>(Reuse.Singleton);
        container.Register<IMomentumQueryService, MomentumQueryService>(Reuse.Singleton);
        container.Register<IWarEventSnapshotAdapter, WarEventSnapshotAdapter>(Reuse.Singleton);
        container.Register<IKingdomStrengthAdapter, KingdomStrengthAdapter>(Reuse.Singleton);
        container.Register<UI.IMomentumUIService, UI.MomentumUIService>(Reuse.Singleton);
        container.Register<WarOfTheRingMomentumBehavior>(Reuse.Singleton);
    }
}
