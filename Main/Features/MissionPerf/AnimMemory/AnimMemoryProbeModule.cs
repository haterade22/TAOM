using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MissionPerf.AnimMemory.Hooks;

namespace TAOM.Features.MissionPerf.AnimMemory;

/// <summary>
/// The <c>[AnimMem]</c> animation clip memory probe (docs/features/mission-perf-heartbeat.md). The
/// mission behavior is added to every mission and reads its own toggle; the probe is a singleton so
/// the signature scan and its disable latch hold for the whole process.
/// </summary>
internal sealed class AnimMemoryProbeModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new AnimMemoryProbeMissionBehavior(
            r.Resolve<IAnimClipMemoryProbe>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "AnimMemoryProbe";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<INativeModuleMemoryAdapter, NativeModuleMemoryAdapter>(Reuse.Singleton);
        registrator.Register<IAnimationLoadingAdapter, AnimationLoadingAdapter>(Reuse.Singleton);
        registrator.Register<IAnimClipMemoryProbe, AnimMemoryProbe>(Reuse.Singleton);
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
