// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.MissionStartGuard.Hooks;

namespace TAOM.Features.MissionStartGuard;

/// <summary>
/// Mission-start guard (docs/features/mission-start-guard.md): Patch103 wraps the six start calls inside
/// <c>Mission.AfterStart</c> so one behaviour or submodule that throws no longer leaves the mission initializing, which
/// the engine then loads again every frame (#699). Applied at GameInit: that phase precedes every mission, and Patch43
/// already patches the same method at the same point. The install line comes from <see cref="OnPhase"/>, which the
/// runner calls after the phase's categories, so the transpiler has run and its swap count is known.
/// </summary>
internal sealed class MissionStartGuardModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch103_MissionStartGuard";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "MissionStartGuard";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IMissionStartGuardAdapter, MissionStartGuardAdapter>(Reuse.Singleton);
        registrator.Register<IMissionStartGuardSettingsProvider, MissionStartGuardSettingsProvider>(Reuse.Singleton);
        registrator.Register<IMissionStartGuardService, MissionStartGuardService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        MissionStartGuardCalls.Initialize(resolver.Resolve<IMissionStartGuardService>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.GameInit)
            resolver.Resolve<IMissionStartGuardService>().LogInstall(MissionStartGuardSwaps.LastSwapped);
    }
}
