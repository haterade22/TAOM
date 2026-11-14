using DryIoc;
using TAOM.Adapters;
using TAOM.Features.WarChronicle.Effects;
using TAOM.Features.WarChronicle.Events;
using TAOM.Features.WarChronicle.Ledger;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// Registers the War Chronicle (docs/features/war-chronicle.md) through its feature module. Every
/// registration is a singleton: the services hold campaign state and are reset by
/// <c>WarChronicleBehavior</c>'s constructor, which the module builds fresh in every campaign.
/// </summary>
public static class WarChronicleIoC
{
    public static void RegisterWarChronicleFeature(IRegistrator container)
    {
        container.Register<IWarChronicleSettingsProvider, WarChronicleSettingsProvider>(Reuse.Singleton);
        container.Register<IKingdomWarSnapshotAdapter, KingdomWarSnapshotAdapter>(Reuse.Singleton);
        container.Register<IWarEffectService, WarEffectService>(Reuse.Singleton);
        container.Register<IPrisonerEscapeAdapter, PrisonerEscapeAdapter>(Reuse.Singleton);
        container.Register<WarEscapeService>(Reuse.Singleton);
        container.Register<WarEscapeDailyPass>(Reuse.Singleton);
        container.Register<WarBaselineService>(Reuse.Singleton);
        container.Register<RallyTierStore>(Reuse.Singleton);
        container.Register<IRallyConfigProvider, RallyConfigProvider>(Reuse.Singleton);
        container.Register<RallyService>(Reuse.Singleton);
        container.Register<WarLedgerService>(Reuse.Singleton);
        container.Register<WarChronicleDailyGate>(Reuse.Singleton);
        container.Register<WarChronicleStateService>(Reuse.Singleton);
        container.Register<WarChronicleTickService>(Reuse.Singleton);

        // #765 M3, M4: the event chain. The focus registry is ArmyTargeting's (ArmyTargetingIoC) and the
        // momentum award is WarOfTheRingMomentum's.
        container.Register<ChronicleStateStore>(Reuse.Singleton);
        container.Register<IChronicleConfigProvider, ChronicleConfigProvider>(Reuse.Singleton);
        container.Register<IChronicleWorldAdapter, ChronicleWorldAdapter>(Reuse.Singleton);
        container.Register<IChroniclePresenter, ChroniclePresenter>(Reuse.Singleton);
        container.Register<ChronicleAnnouncementQueue>(Reuse.Singleton);
        container.Register<ChronicleOutcomeService>(Reuse.Singleton);
        container.Register<ChronicleService>(Reuse.Singleton);
        // Story Missions settles a story window through this seam (#1105 slice 4): the same singleton.
        container.RegisterMapping<IChronicleStoryPort, ChronicleService>();
    }
}
