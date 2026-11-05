using System;
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
/// <para>1.4.x (plans/118-campaign-load-time.md): both categories apply only when the loaded
/// TAOM.Dependencies.dll keeps PatchShield off their seven targets (<see cref="ShieldExclusionCheck"/>, #371);
/// otherwise one warning replaces the two ready lines and the engine methods stay unpatched.</para>
/// </summary>
internal sealed class LoadTimeStampsModule : TaomFeatureModule
{
    internal const string LoadXmlCategory = "Patch100_LoadTimeStamps_LoadXml";
    internal const string LifecycleCategory = "Patch100_LoadTimeStamps_Lifecycle";

    /// <summary>The engine methods the two categories patch, named as PatchShieldPolicy names them.</summary>
    internal static readonly IReadOnlyList<(string Type, string Method)> ShieldTargets = new[]
    {
        ("TaleWorlds.ObjectSystem.MBObjectManager", "LoadXML"),
        ("TaleWorlds.ObjectSystem.MBObjectManager", "CreateMergedXmlFile"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnNewGameCreated"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnGameEarlyLoaded"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnGameLoaded"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnSessionStart"),
        ("TaleWorlds.CampaignSystem.CampaignEventDispatcher", "OnAfterSessionStart"),
    };

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(LoadXmlCategory, ApplyPhase.ProcessLoad),
        new(LifecycleCategory, ApplyPhase.ProcessLoad),
    };

    private readonly Func<IReadOnlyList<(string Type, string Method)>, string?> _shieldCheck;
    private string? _shieldProblem;

    public LoadTimeStampsModule() : this(ShieldExclusionCheck.Problem) { }

    internal LoadTimeStampsModule(Func<IReadOnlyList<(string Type, string Method)>, string?> shieldCheck) =>
        _shieldCheck = shieldCheck ?? throw new ArgumentNullException(nameof(shieldCheck));

    public override string Id => "LoadTimeStamps";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories =>
        _shieldProblem == null ? Categories : Array.Empty<PatchCategoryDecl>();

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
        _shieldProblem = _shieldCheck(ShieldTargets);
        if (_shieldProblem != null)
        {
            logger.LogWarning("[LoadXml] [Lifecycle] stamps off for this session: " + _shieldProblem);
            return;
        }
        logger.LogInfo(LoadTimeStampLines.LoadXmlReady());
        logger.LogInfo(LoadTimeStampLines.LifecycleReady(resolver.Resolve<ICampaignListenerAdapter>().BindingProblem));
    }
}
