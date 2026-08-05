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
        container.Register<Content.ISkillCheckService, Content.SkillCheckService>(Reuse.Singleton);
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
        container.Register<EnlistmentDialogBehavior>(Reuse.Singleton);
        container.Register<EnlistmentContentBehavior>(Reuse.Singleton);
        container.Register<EnlistmentQuartermasterBehavior>(Reuse.Singleton);
        container.Register<EnlistmentDutyBehavior>(Reuse.Singleton);
        container.Register<EnlistmentAssignmentDialogBehavior>(Reuse.Singleton);
    }
}
