using System;
using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.XmlMerge.Hooks;

namespace TAOM.Features.XmlMerge;

/// <summary>
/// The module-XML merge fast path (docs/features/xml-merge-fast-path.md, plan 042) as a feature module: the engine
/// adapter, the XSLT cache and the service, all singletons; the patch class gets the service and the configuration
/// header is logged in <see cref="InitializeStatics"/>. Patch99 applies at ProcessLoad (OnSubModuleLoad) because a
/// game's first merges, its XML loads in Campaign.OnInitialize and CustomGame.OnInitialize, run long before
/// OnGameInitializationFinished, the late batch. The per-game summary line comes from one call in
/// SubModule.OnGameInitializationFinished, since the module runner's GameInit phase runs once per process.
/// <para>1.4.x (plans/118-campaign-load-time.md): Patch99 applies only when the
/// loaded TAOM.Dependencies.dll keeps PatchShield off CreateMergedXmlFile (<see cref="ShieldExclusionCheck"/>, #371);
/// otherwise the start line reads "fast path off" with the reason and every merge runs the engine's own code.</para>
/// </summary>
internal sealed class XmlMergeModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch99_XmlMergeFastPath";

    /// <summary>The engine method Patch99 patches, named as PatchShieldPolicy names it.</summary>
    internal static readonly IReadOnlyList<(string Type, string Method)> ShieldTargets = new[]
    {
        ("TaleWorlds.ObjectSystem.MBObjectManager", "CreateMergedXmlFile"),
    };

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    private readonly Func<IReadOnlyList<(string Type, string Method)>, string?> _shieldCheck;
    private string? _shieldProblem;

    public XmlMergeModule() : this(ShieldExclusionCheck.Problem) { }

    internal XmlMergeModule(Func<IReadOnlyList<(string Type, string Method)>, string?> shieldCheck) =>
        _shieldCheck = shieldCheck ?? throw new ArgumentNullException(nameof(shieldCheck));

    public override string Id => "XmlMerge";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IXmlMergeEngineAdapter, XmlMergeEngineAdapter>(Reuse.Singleton);
        registrator.Register<XsltTransformCache>(Reuse.Singleton);
        registrator.Register<XmlMergeService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        _shieldProblem = _shieldCheck(ShieldTargets);
        if (_shieldProblem != null)
        {
            resolver.Resolve<IModLogger>().LogWarning(XmlMergeLines.Off(_shieldProblem));
            return;
        }
        var service = resolver.Resolve<XmlMergeService>();
        MBObjectManager_CreateMergedXmlFile_Patch.Initialize(service);
        service.LogConfigurationHeader();
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories =>
        _shieldProblem == null ? Categories : Array.Empty<PatchCategoryDecl>();
}
