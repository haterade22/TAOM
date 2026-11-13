using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.KingdomAllegiance.Hooks;
using TAOM.Features.KingdomAllegiance.Models;

namespace TAOM.Features.KingdomAllegiance;

/// <summary>
/// Kingdom Allegiance (docs/features/kingdom-allegiance.md, #1111): AI clans keep to their side of the Free/Evil line when they
/// join, defect or hire on, and a new campaign starts with no Free-vs-Evil friendships. Two entry points share one service and one
/// MCM switch: two score overrides in <c>TaomDiplomacyModel</c>, which <c>SubModule.OnGameStart</c> builds by hand and hands the
/// service and <see cref="AllegianceFactsReader"/>; and Patch145, a postfix on vanilla's new-game relation roll. Nothing is
/// saved. Overlap check, 2026-11-10: no other TAOM code overrides the clan or mercenary join scores or patches
/// <c>CharacterRelationCampaignBehavior</c>. <c>IAlignmentService</c> (<c>ExecutionIoC</c>), <c>IPlayerControlQuery</c>,
/// <c>ICoopSessionProvider</c> and <c>IOutcomeLog</c> are registered elsewhere.
/// </summary>
internal sealed class KingdomAllegianceModule : TaomFeatureModule
{
    // CharacterRelationCampaignBehavior lives in TaleWorlds.CampaignSystem, which loads with the process. ProcessLoad is the
    // earliest phase; the target fires in Campaign.PostInitializeFourthState (OnNewGameCreated), after every apply phase but
    // FirstMission (v1.5.7).
    internal const string PatchCategory = "Patch145_KingdomAllegiance";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.ProcessLoad),
    };

    public override string Id => "KingdomAllegiance";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IKingdomAllegianceSettingsProvider, KingdomAllegianceSettingsProvider>(Reuse.Singleton);
        registrator.Register<AllegianceFactsReader>(Reuse.Singleton);
        registrator.Register<IAllegianceRelationAdapter, AllegianceRelationAdapter>(Reuse.Singleton);
        registrator.Register<KingdomAllegianceService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        Patch145_FriendshipSeeding_Postfix.Initialize(
            resolver.Resolve<KingdomAllegianceService>(), resolver.Resolve<IAllegianceRelationAdapter>(), resolver.Resolve<IModLogger>());

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;
}
