using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard.Hooks;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// The sally-out, empty-meeting and battle-death roster guards (#1099, #1475, docs/features/map-event-guard.md).
/// Patch142 and Patch143 are applied from SubModule; Patch169 and Patch173 are this module's own categories. No saved data.
/// </summary>
internal sealed class EncounterGuardModule : TaomFeatureModule
{
    internal const string BattleDeathSnapshotCategory = "Patch169_BattleDeathRosterSnapshot";

    private static readonly PatchCategoryDecl[] Categories =
    {
        // Map events end under Campaign.Tick, so nothing reaches the target before the first game init.
        new(BattleDeathSnapshotCategory, ApplyPhase.GameInit),
        new(Patch173_OrphanedBattleEncounter.Category, ApplyPhase.GameInit),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new EmptyMeetingMenuBehavior(r.Resolve<EmptyMeetingGuardService>())),
        CampaignBehaviorDecl.Of(r => new OrphanedBattleCampaignBehavior(
            r.Resolve<OrphanedBattleService>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "EncounterGuard";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<SallyOutGuardService>(Reuse.Singleton);
        registrator.Register<IEmptyMeetingAdapter, EmptyMeetingAdapter>(Reuse.Singleton);
        registrator.Register<EmptyMeetingGuardService>(Reuse.Singleton);
        registrator.Register<IHalfFinishedEncounterAdapter, HalfFinishedEncounterAdapter>(Reuse.Singleton);
        registrator.Register<HalfFinishedEncounterService>(Reuse.Singleton);
        registrator.Register<IOrphanedBattleAdapter, OrphanedBattleAdapter>(Reuse.Singleton);
        registrator.Register<OrphanedBattleService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        Patch169_BattleDeathRosterSnapshot.Initialize(resolver.Resolve<IModLogger>());
        var orphaned = resolver.Resolve<OrphanedBattleService>();
        Patch173_OrphanedBattleEncounter.Initialize(orphaned, resolver.Resolve<IModLogger>());
        Patch173_OrphanedBattleSettlementEncounter.Initialize(orphaned, resolver.Resolve<IModLogger>());
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
