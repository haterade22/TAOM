// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;

namespace TAOM.Features.NameplateCull;

/// <summary>
/// Settlement nameplate cull (docs/features/nameplate-cull.md): Patch104 replaces the campaign map's per-frame
/// <c>SettlementNameplatesVM.Update</c> with the same update over the plates that are not hidden-and-staying-hidden.
/// Applied at GameInit: the patched method reads only instance fields, <c>Camera.Position</c> and <c>TWParallel</c>, whose
/// static state is built when the engine starts, so compiling the replacement runs no type initializer that needs a campaign
/// (the decompile read for this patch is in the registry entry). The module's step after the category binds the engine
/// members the update reads (<see cref="INameplateCullAdapter.Initialize"/>) and writes the install line; a game build that
/// lacks one leaves the cull off and vanilla running.
/// </summary>
internal sealed class NameplateCullModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch104_NameplateCull";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "NameplateCull";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<INameplateCullAdapter, NameplateCullAdapter>(Reuse.Singleton);
        registrator.Register<INameplateCullSettingsProvider, NameplateCullSettingsProvider>(Reuse.Singleton);
        registrator.Register<INameplateCullService, NameplateCullService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        NameplateCullCalls.Initialize(resolver.Resolve<INameplateCullService>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.GameInit)
            resolver.Resolve<INameplateCullService>().Install();
    }
}
