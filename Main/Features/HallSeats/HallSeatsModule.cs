using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.HallSeats.Hooks;

namespace TAOM.Features.HallSeats;

/// <summary>
/// Hall Seats (docs/features/hall-seats.md): the lord's hall spawns only as many lords as it has lord spots. Patch148
/// applies at GameInit (its targets run only in campaign settlement missions) and holds two hooks on
/// <c>MissionAgentHandler</c>. No save data, no behavior, no model: nothing outlives one spawn pass.
/// </summary>
internal sealed class HallSeatsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch148_HallSeats";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "HallSeats";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHallSeatsAdapter, HallSeatsAdapter>(Reuse.Singleton);
        registrator.Register<HallSeatsService>(Reuse.Singleton);
        registrator.Register<HallSeatsSettingsProvider>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        var service = resolver.Resolve<HallSeatsService>();
        var logger = resolver.Resolve<IModLogger>();
        MissionAgentHandler_SpawnLocationCharacters_HallSeats_Patch.Initialize(service, resolver.Resolve<HallSeatsSettingsProvider>(), logger);
        MissionAgentHandler_SpawnDefaultLocationCharacter_HallSeats_Patch.Initialize(service, logger);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
