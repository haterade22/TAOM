using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.NavalTravel;

/// <summary>
/// Naval travel is parked (docs/features/naval-travel.md): its model and patches stay unregistered in SubModule.cs. This
/// module carries only the load-time repair that moves parties an old save left at sea back to land.
/// </summary>
internal sealed class NavalTravelModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new AtSeaRepairBehavior(r.Resolve<IModLogger>(), r.Resolve<ICoopSessionProvider>())),
    };

    public override string Id => "NavalTravel";

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
