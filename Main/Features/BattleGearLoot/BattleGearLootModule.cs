using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BattleGearLoot.Hooks;

namespace TAOM.Features.BattleGearLoot;

internal sealed class BattleGearLootModule : TaomFeatureModule
{
    private static readonly PatchCategoryDecl[] Categories =
    {
        new(Patch172_BattleGearLoot.Category, ApplyPhase.GameInit),
    };

    public override string Id => "BattleGearLoot";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IBattleGearLootSettingsProvider, BattleGearLootSettingsProvider>(Reuse.Singleton);
        registrator.Register<BattleGearLootService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch172_BattleGearLoot.Initialize(resolver.Resolve<BattleGearLootService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
