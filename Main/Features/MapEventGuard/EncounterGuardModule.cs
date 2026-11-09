using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.MapEventGuard.Hooks;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// The sally-out and empty-meeting guards (#1099, docs/features/map-event-guard.md). Patch142 and Patch143 are applied
/// from SubModule. No saved data.
/// </summary>
internal sealed class EncounterGuardModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new EmptyMeetingMenuBehavior(r.Resolve<EmptyMeetingGuardService>())),
    };

    public override string Id => "EncounterGuard";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<SallyOutGuardService>(Reuse.Singleton);
        registrator.Register<IEmptyMeetingAdapter, EmptyMeetingAdapter>(Reuse.Singleton);
        registrator.Register<EmptyMeetingGuardService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
