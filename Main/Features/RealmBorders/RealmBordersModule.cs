using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.RealmBorders.Hooks;
using TAOM.Features.RealmBorders.UI;

namespace TAOM.Features.RealmBorders;

/// <summary>
/// Realm Borders (#698) as a feature module: the service graph, one campaign behavior (events, and it
/// attaches the map view that draws), and the two map keys registered at process load. No patch, no
/// model, no mission behavior, no save data: the provinces are recomputed or reused per map and the
/// map mode starts political each session. Order-free: nothing else draws on the map scene's border
/// meshes or reads these keys.
/// </summary>
internal sealed class RealmBordersModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(resolver => resolver.Resolve<RealmBordersCampaignBehavior>()),
    };

    public override string Id => "RealmBorders";

    public override void RegisterServices(IRegistrator registrator) =>
        RealmBordersIoC.RegisterRealmBordersFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    /// <summary>
    /// Registers the keys in OnSubModuleLoad, after Native has cleared and filled the key contexts, as
    /// the time controls do. A failure leaves the keys unbound and the borders on; letting it escape
    /// would fault the module and take the borders off for the session.
    /// </summary>
    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.ProcessLoad)
            return;
        try
        {
            TaomRealmBordersHotKeyCategory.Register();
        }
        catch (Exception ex)
        {
            resolver.Resolve<IModLogger>().LogWarning(
                $"[RealmBorders] hotkey registration failed, the border keys stay unbound: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
