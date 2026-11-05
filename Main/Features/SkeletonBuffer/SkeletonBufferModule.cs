// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.SkeletonBuffer.Hooks;

namespace TAOM.Features.SkeletonBuffer;

/// <summary>
/// The skeleton buffer guard and watch (docs/features/skeleton-buffer-guard.md). No Harmony patch: the guard rewrites 13
/// bytes of engine code at each of up to two sites, once, at the first main menu, and the watch is a mission behavior
/// added to every mission. MainMenu rather than ProcessLoad because MCM settings are still null during OnSubModuleLoad
/// (see SubModule.cs) and the toggle must be readable; at MainMenu nothing that draws skeletons is running, so no thread
/// is inside the bytes being replaced.
/// </summary>
internal sealed class SkeletonBufferModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new SkeletonBufferWatchMissionBehavior(r.Resolve<SkeletonBufferWatchService>())),
    };

    public override string Id => "SkeletonBuffer";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISkeletonBufferMemoryAdapter, SkeletonBufferMemoryAdapter>(Reuse.Singleton);
        registrator.Register<ISkeletonBufferEngineAdapter, SkeletonBufferEngineAdapter>(Reuse.Singleton);
        registrator.Register<ISkeletonBufferSettingsProvider, SkeletonBufferSettingsProvider>(Reuse.Singleton);
        registrator.Register<ISkeletonBufferGuardService, SkeletonBufferGuardService>(Reuse.Singleton);
        registrator.Register<SkeletonBufferWatchService>(Reuse.Singleton);
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.MainMenu)
            resolver.Resolve<ISkeletonBufferGuardService>().Install();
    }
}
