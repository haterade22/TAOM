using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.CreatureSaddles;

/// <summary>
/// Creature saddles (docs/features/creature-saddles.md): the warg, dire wolf and spider rigs get their own harness
/// family, so the inventory no longer puts a horse saddle on them (or their saddles on a horse), and old saves lose any
/// such pair at load. Two postfixes applied at process load, before any game reads its XML; one campaign behavior.
/// </summary>
internal sealed class CreatureSaddlesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch154_CreatureSaddleFamilies";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new CreatureSaddleSaveRepairBehavior(r.Resolve<IModLogger>(), r.Resolve<TAOM.Features.CoopInterop.ICoopSessionProvider>())),
    };

    internal static IModLogger? Logger { get; private set; }

    public override string Id => "CreatureSaddles";

    public override void InitializeStatics(IResolver resolver)
    {
        Logger = resolver.Resolve<IModLogger>();
        CreatureSaddleFamilyWriter.Initialize(Logger);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
