using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.XmlMerge.Hooks;

namespace TAOM.Features.XmlMerge;

/// <summary>
/// The module-XML merge fast path (docs/features/xml-merge-fast-path.md, plan 042) as a feature module: the engine
/// adapter, the XSLT cache and the service, all singletons; the patch class gets the service and the configuration
/// header is logged in <see cref="InitializeStatics"/>. Patch99 applies at ProcessLoad (OnSubModuleLoad) because a
/// game's first merges, its XML loads in Campaign.OnInitialize and CustomGame.OnInitialize, run long before
/// OnGameInitializationFinished, the late batch. The per-game summary line comes from one call in
/// SubModule.OnGameInitializationFinished, since the module runner's GameInit phase runs once per process.
/// </summary>
internal sealed class XmlMergeModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch99_XmlMergeFastPath";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "XmlMerge";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IXmlMergeEngineAdapter, XmlMergeEngineAdapter>(Reuse.Singleton);
        registrator.Register<XsltTransformCache>(Reuse.Singleton);
        registrator.Register<XmlMergeService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        var service = resolver.Resolve<XmlMergeService>();
        MBObjectManager_CreateMergedXmlFile_Patch.Initialize(service);
        service.LogConfigurationHeader();
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
