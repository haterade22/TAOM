using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Wanderer Allegiance (#575) as a feature module, the first feature moved off SubModule.cs and
/// IoC.cs. Two dimensions only: the service graph and one stateless dialog behavior. No patch, no
/// model, no mission behavior, and the behavior's SyncData is empty, so the module owns no save
/// data. The behavior stays a container singleton, as SubModule resolved it before. Adding it after
/// every hand-wired behavior is order-free: its two lines are the only TAOM lines on companion_hire
/// and outrank vanilla's reply by priority (110 over 100), and LotrIssueSuppression.SuppressAll
/// removes only vanilla issue types.
/// </summary>
internal sealed class WandererAllegianceModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(resolver => resolver.Resolve<Hooks.WandererAllegianceDialogBehavior>()),
    };

    public override string Id => "WandererAllegiance";

    public override void RegisterServices(IRegistrator registrator) =>
        WandererAllegianceIoC.RegisterWandererAllegianceFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
