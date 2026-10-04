using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;

namespace TAOM.Features.LoadTimeStamps;

/// <summary>
/// Load-time stamps (docs/features/load-time-stamps.md, plan 040): splits every load in
/// taom_debug.log into where its time went. The patch-group phase totals are always written; the
/// per-category patch lines and the steps of TAOM's game-start and game-initialization hooks follow
/// "Enable Load-Time Stamps" on the Battle Load Diagnostics page (default off). The per-type
/// [LoadXml] lines are always written too: Patch100_LoadTimeStamps_LoadXml applies at OnSubModuleLoad,
/// before the first game's XML loads, for every player whatever the toggle.
/// Patch100_LoadTimeStamps_Lifecycle writes a dispatch line for each campaign dispatch of a new
/// game, a loaded save and the session start, again whatever the toggle; with the toggle on it also
/// times every campaign handler. It is its own category, so engine drift there never costs the XML
/// stamp.
/// </summary>
internal sealed class LoadTimeStampsModule : TaomFeatureModule
{
    internal const string LoadXmlCategory = "Patch100_LoadTimeStamps_LoadXml";
    internal const string LifecycleCategory = "Patch100_LoadTimeStamps_Lifecycle";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(LoadXmlCategory, ApplyPhase.ProcessLoad),
        new(LifecycleCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "LoadTimeStamps";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IStampClock, StopwatchStampClock>(Reuse.Singleton);
        registrator.Register<LoadStampDetailGate>(Reuse.Singleton);
        registrator.Register<HookStampService>(Reuse.Singleton);
        registrator.Register<LoadXmlStampService>(Reuse.Singleton);
        registrator.Register<ICampaignListenerAdapter, CampaignListenerAdapter>(Reuse.Singleton);
        registrator.Register<LifecycleTimingService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        LoadTimeStampsHooks.Initialize(resolver.Resolve<LoadStampDetailGate>(), resolver.Resolve<HookStampService>());
        LoadTimeStampsHooks.InitializeLoadXml(resolver.Resolve<LoadXmlStampService>());
        LoadTimeStampsHooks.InitializeLifecycle(resolver.Resolve<LifecycleTimingService>());
        var logger = resolver.Resolve<IModLogger>();
        logger.LogInfo(LoadTimeStampLines.Ready());
        logger.LogInfo(LoadTimeStampLines.LoadXmlReady());
        logger.LogInfo(LoadTimeStampLines.LifecycleReady(resolver.Resolve<ICampaignListenerAdapter>().BindingProblem));
    }
}
