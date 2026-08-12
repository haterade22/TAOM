using DryIoc;
using TAOM.Adapters;
using TAOM.Features.Enlistment.Hooks;

using TAOM.Features.Enlistment.Presentation;
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
        container.Register<IEnlistmentDiagnosticsSettingsProvider, EnlistmentDiagnosticsSettingsProvider>(Reuse.Singleton);
        container.Register<IEnlistmentFeatureSettingsProvider, EnlistmentFeatureSettingsProvider>(Reuse.Singleton);
        container.Register<TAOM.Adapters.IMapConversationAdapter, TAOM.Adapters.MapConversationAdapter>(Reuse.Singleton);
        container.Register<IEnlistmentPlayerActionService, EnlistmentPlayerActionService>(Reuse.Singleton);
        container.Register<IServiceStatusTextWriter, ServiceStatusTextWriter>(Reuse.Singleton);
        container.Register<Presentation.IEnlistmentWaitMenuOptions, Presentation.EnlistmentWaitMenuOptions>(Reuse.Singleton);
        container.Register<Presentation.IServiceDailyAnnouncer, Presentation.ServiceDailyAnnouncer>(Reuse.Singleton);
        container.Register<IServiceStatusService, ServiceStatusService>(Reuse.Singleton);
        container.Register<IServiceAttachmentService, ServiceAttachmentService>(Reuse.Singleton);
        container.Register<IDischargeService, DischargeService>(Reuse.Singleton);
        container.Register<IEnlistmentService, EnlistmentService>(Reuse.Singleton);
        container.Register<IEnlistmentStateQuery, EnlistmentStateQuery>(Reuse.Singleton);
        container.Register<IEncounterOwnershipPolicy, EncounterOwnershipPolicy>(Reuse.Singleton);
        // MOVED UP from DutiesIoC on 2026-08-09. It lived in the duties sub-module because the
        // duty presenter was its only consumer; the reconciler now raises the commander-loss modal
        // through it, and the reconciler is registered HERE. DryIoc resolves lazily so runtime
        // order never mattered, but the IoC validation tests build a container per feature module
        // and caught the split immediately.
        container.Register<TAOM.Adapters.IArmyMembershipAdapter, TAOM.Adapters.ArmyMembershipAdapter>(Reuse.Singleton);
        container.Register<TAOM.Adapters.IHeroRenownAdapter, TAOM.Adapters.HeroRenownAdapter>(Reuse.Singleton);
        container.Register<TAOM.Adapters.IServiceDiplomacyAdapter, TAOM.Adapters.ServiceDiplomacyAdapter>(Reuse.Singleton);
        container.Register<IServiceDiplomacyService, ServiceDiplomacyService>(Reuse.Singleton);
        container.Register<TAOM.Adapters.IInquiryAdapter, TAOM.Adapters.InquiryAdapter>(Reuse.Singleton);
        container.Register<IEnlistmentReconciler, EnlistmentReconciler>(Reuse.Singleton);
        container.Register<IServiceMaintenanceService, ServiceMaintenanceService>(Reuse.Singleton);
        container.Register<IEnlistmentLoadNormalizer, EnlistmentLoadNormalizer>(Reuse.Singleton);
        container.Register<IEnlistmentMenuService, EnlistmentMenuService>(Reuse.Singleton);
        container.Register<IServiceBattleService, ServiceBattleService>(Reuse.Singleton);

        container.Register<IEnlistmentWaitMenuPresenter, EnlistmentWaitMenuPresenter>(Reuse.Singleton);
        container.Register<IEnlistmentDialogGateService, EnlistmentDialogGateService>(Reuse.Singleton);

        // Equipment issuance (#375 Phase 4). IItemPoolAdapter is owned by CultureMarketplaceIoC —
        // Keep-guard mirrors the IPlayerPartyAdapter pattern above.
        container.Register<IEquipmentRosterCatalogAdapter, EquipmentRosterCatalogAdapter>(Reuse.Singleton);
        container.Register<IPartyItemRosterAdapter, PartyItemRosterAdapter>(Reuse.Singleton);
        container.Register<IItemPoolAdapter, ItemPoolAdapter>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        // Persisted via the content record's SyncData section — the in-memory ledger let a
        // full game restart re-allow one free kit draw per rank.
        container.Register<Equipment.IEquipmentIssueLedger, Equipment.PersistedEquipmentIssueLedger>(Reuse.Singleton);
        container.Register<Equipment.IEnlistmentEquipmentService, Equipment.EnlistmentEquipmentService>(Reuse.Singleton);

        // Content layer (#375 Phase 3): daily loop, wages, promotion, rhythm snapshot.
        container.Register<IArmyRhythmProbeAdapter, ArmyRhythmProbeAdapter>(Reuse.Singleton);
        container.Register<IHeroSkillXpAdapter, HeroSkillXpAdapter>(Reuse.Singleton);
        container.Register<IGoldTransferAdapter, GoldTransferAdapter>(Reuse.Singleton);
        container.Register<IGoldGiftAdapter, GoldGiftAdapter>(Reuse.Singleton, ifAlreadyRegistered: IfAlreadyRegistered.Keep);
        container.Register<Content.IEnlistmentContentConfigProvider, Content.EnlistmentContentConfigProvider>(Reuse.Singleton);
        container.Register<Content.IEnlistmentContentStore, Content.EnlistmentContentStore>(Reuse.Singleton);
        container.Register<Content.IArmyRhythmSnapshotService, Content.ArmyRhythmSnapshotService>(Reuse.Singleton);
        container.Register<Content.IServiceRewardService, Content.ServiceRewardService>(Reuse.Singleton);
        // SAME instance under the preview interface — the wallet projection must read the very wage the
        // payment uses, not a second service that could drift from it.
        container.RegisterMapping<Content.IEnlistmentWagePreview, Content.IServiceRewardService>();
        container.Register<Content.ISkillCheckService, Content.SkillCheckService>(Reuse.Singleton);
        container.Register<IRealTimeProvider, RealTimeProvider>(Reuse.Singleton);
        container.Register<Content.IPromotionService, Content.PromotionService>(Reuse.Singleton);
        container.Register<Content.IDischargeConsequenceService, Content.DischargeConsequenceService>(Reuse.Singleton);
        container.Register<Content.IAssignmentService, Content.AssignmentService>(Reuse.Singleton);
        container.Register<Content.IEnlistmentDailyService, Content.EnlistmentDailyService>(Reuse.Singleton);
        container.Register<Content.IBattleMeritAccumulator, Content.BattleMeritAccumulator>(Reuse.Singleton);
        container.Register<Content.IEnlistmentBattlePayoutService, Content.EnlistmentBattlePayoutService>(Reuse.Singleton);

        // Entry points (resolved by SubModule for AddBehavior)
        container.Register<EnlistmentBehavior>(Reuse.Singleton);
        container.Register<EnlistmentMenuBehavior>(Reuse.Singleton);
        container.Register<EnlistmentBattleBehavior>(Reuse.Singleton);
        container.Register<EnlistmentMaintenanceBehavior>(Reuse.Singleton);
        container.Register<EnlistmentDialogBehavior>(Reuse.Singleton);
        container.Register<EnlistmentReleaseDialogBehavior>(Reuse.Singleton);
        container.Register<EnlistmentContentBehavior>(Reuse.Singleton);
        container.Register<EnlistmentQuartermasterBehavior>(Reuse.Singleton);
        container.Register<EnlistmentDutyBehavior>(Reuse.Singleton);
        container.Register<EnlistmentAssignmentDialogBehavior>(Reuse.Singleton);
    }
}
