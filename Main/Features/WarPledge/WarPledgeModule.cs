using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WarPledge;

/// <summary>
/// War Pledge as a feature module (#1113, docs/features/war-pledge.md). The behavior is built with <c>new</c>
/// inside the declaration's factory, which the runner calls once per campaign start, so its constructor resets
/// the pledge store for every campaign; it owns save data, so a fault fails closed.
/// </summary>
internal sealed class WarPledgeModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new WarPledgeBehavior(
            r.Resolve<WarPledgeService>(),
            r.Resolve<WarPledgeStore>(),
            r.Resolve<IKingdomWarSnapshotAdapter>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "WarPledge";

    /// <summary><c>WarPledgeBehavior</c> persists the pledges and the next courting day in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) => WarPledgeIoC.RegisterWarPledgeFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
