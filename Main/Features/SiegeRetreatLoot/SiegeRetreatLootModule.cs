using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.SiegeRetreatLoot.Hooks;

namespace TAOM.Features.SiegeRetreatLoot;

/// <summary>
/// Loot after a siege defence ends by attacker retreat (#1121, docs/features/siege-retreat-loot.md). Patch146 applies
/// at game init: the target runs only from a campaign encounter. No saved data.
/// </summary>
internal sealed class SiegeRetreatLootModule : TaomFeatureModule
{
    private static readonly PatchCategoryDecl[] Categories =
    {
        new(Patch146_SiegeRetreatLoot.Category, ApplyPhase.GameInit),
    };

    public override string Id => "SiegeRetreatLoot";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISiegeRetreatLootAdapter, SiegeRetreatLootAdapter>(Reuse.Singleton);
        registrator.Register<SiegeRetreatLootService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch146_SiegeRetreatLoot.Initialize(resolver.Resolve<SiegeRetreatLootService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
