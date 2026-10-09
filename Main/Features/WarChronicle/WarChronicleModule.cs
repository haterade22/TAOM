using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.WarChronicle;

/// <summary>
/// The War Chronicle as a feature module (docs/features/war-chronicle.md, issue #765): the timed-effect
/// registry, the baselines, the rally and the war ledger; the event chain, the army-focus window and the
/// Eye of Sauron hunt come in later milestones.
/// The campaign behavior is built fresh for each campaign (a lambda body, not a container singleton)
/// because its constructor resets the per-campaign singletons; WarChronicleWiringTests pins the shape.
/// </summary>
internal sealed class WarChronicleModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new WarChronicleBehavior(
            r.Resolve<WarChronicleTickService>(),
            r.Resolve<WarChronicleStateService>(),
            r.Resolve<Ledger.WarLedgerService>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "WarChronicle";

    /// <summary>The campaign behavior persists the effects, baselines and rally tiers in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) =>
        WarChronicleIoC.RegisterWarChronicleFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
