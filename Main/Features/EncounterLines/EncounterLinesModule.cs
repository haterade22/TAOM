using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.EncounterLines.Hooks;

namespace TAOM.Features.EncounterLines;

/// <summary>
/// Encounter Lines (docs/features/encounter-lines.md): lords, caravans, villagers, bandits and hideout bosses speak in their
/// people's voice and by who the player is to them. Lord lines are tag-scored game-text rows (the tag classes in
/// <c>Tags/</c>); the other beats are set by four patches in Patch149, applied at GameInit. No behavior, model or save
/// data, nothing in <c>IoC.cs</c> or <c>SubModule.cs</c>.
/// </summary>
internal sealed class EncounterLinesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch149_EncounterLines";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "EncounterLines";

    public override void RegisterServices(IRegistrator registrator) => EncounterLinesIoC.RegisterEncounterLinesFeature(registrator);

    public override void InitializeStatics(IResolver resolver)
    {
        var service = resolver.Resolve<EncounterLinesService>();
        var logger = resolver.Resolve<IModLogger>();
        EncounterTagGate.Initialize(service, resolver.Resolve<EncounterLinesAdapter>().DescribeLord, logger);
        CaravanGreetingPatch.Initialize(service, logger);
        VillagerGreetingPatch.Initialize(service, logger);
        RobberyLinesPatch.Initialize(service, logger);
        EncounterSentenceTextPatch.Initialize(service, logger);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
