using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.WandererAllegiance.Hooks;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Wanderer Allegiance (#575) as a feature module, the first feature moved off SubModule.cs and
/// IoC.cs. The service graph, one stateless dialog behavior, and (since the placement rule, Patch175) one
/// patch category. No model, no mission behavior, and the behavior's SyncData is empty, so the module owns no save
/// data. The behavior stays a container singleton, as SubModule resolved it before. Adding it after
/// every hand-wired behavior is order-free: its two lines outrank vanilla's reply by priority (110 over
/// 100) and Oaths of Service's hire lines (105), so registration order does not matter, and
/// LotrIssueSuppression.SuppressAll removes only vanilla issue types.
/// </summary>
internal sealed class WandererAllegianceModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(resolver => resolver.Resolve<Hooks.WandererAllegianceDialogBehavior>()),
    };

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(Patch175_WandererSideEnter.Category, ApplyPhase.GameInit),
    };

    public override string Id => "WandererAllegiance";

    public override void RegisterServices(IRegistrator registrator) =>
        WandererAllegianceIoC.RegisterWandererAllegianceFeature(registrator);

    public override void InitializeStatics(IResolver resolver) =>
        Patch175_WandererSideEnter.Initialize(resolver.Resolve<WandererTownAdapter>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
