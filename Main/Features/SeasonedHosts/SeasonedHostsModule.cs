using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.SeasonedHosts.Hooks;
using TAOM.Features.SeasonedHosts.Models;

namespace TAOM.Features.SeasonedHosts;

/// <summary>
/// Seasoned Hosts (#890, docs/features/seasoned-hosts.md): the XP economy of AI lords. Three campaign models
/// (<c>GenericXpModel</c>, <c>PartyTrainingModel</c>, <c>CharacterDevelopmentModel</c>) and one Harmony prefix (Patch118) for the
/// Steward XP of a finished project, which has no model seam. Registry: docs/reference/gamemodel-registry.md. No behavior,
/// nothing saved.
/// </summary>
internal sealed class SeasonedHostsModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch118_SeasonedHostsStewardXp";

    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<GenericXpModel, TaomGenericXpModel>(ModelTarget.Campaign,
            r => new TaomGenericXpModel(r.Resolve<SeasonedHostsService>(), r.Resolve<ISeasonedHostsAdapter>())),
        GameModelDecl.Of<PartyTrainingModel, TaomPartyTrainingModel>(ModelTarget.Campaign,
            r => new TaomPartyTrainingModel(r.Resolve<SeasonedHostsService>(), r.Resolve<ISeasonedHostsAdapter>(),
                r.Resolve<TAOM.Features.CareerSystem.ICareerPassiveService>())),
        GameModelDecl.Of<CharacterDevelopmentModel, TaomCharacterDevelopmentModel>(ModelTarget.Campaign,
            r => new TaomCharacterDevelopmentModel(r.Resolve<SeasonedHostsService>())),
    };

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    public override string Id => "SeasonedHosts";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISeasonedHostsSettingsProvider, SeasonedHostsSettingsProvider>(Reuse.Singleton);
        registrator.Register<ISeasonedHostsAdapter, SeasonedHostsAdapter>(Reuse.Singleton);
        registrator.Register<SeasonedHostsService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch118_ProjectStewardXp.Initialize(resolver.Resolve<SeasonedHostsService>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<GameModelDecl> GameModels => Models;

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
