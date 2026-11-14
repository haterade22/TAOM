using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Hooks;

namespace TAOM.Features.StoryMissions;

/// <summary>
/// Story Missions (#1105): one-shot battles hosted inside a loaded campaign, on the Long Defeat's run machine. Slice 1 hosts the Helm's Deep defence
/// from a dev menu and writes nothing to the campaign. Slice 4 offers it from the War Chronicle's Hornburg window and applies the verdict
/// afterwards, from a menu click on the map. No mission behavior is declared: the launcher builds the mission's own list, as Host Review's
/// does. No patch and no model; a declined offer is saved by the chronicle. TAOM.Client holds the view list.
/// </summary>
internal sealed class StoryMissionsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new StoryDevMenuBehavior(r.Resolve<StoryMissionCatalog>(), r.Resolve<StoryMissionLauncher>(),
            r.Resolve<IGameMenuAdapter>(), r.Resolve<ICoopSessionProvider>(), r.Resolve<IDedicatedServerProvider>(),
            r.Resolve<IEnlistmentStateQuery>(), r.Resolve<IEncounterAdapter>())),
        CampaignBehaviorDecl.Of(r => new StoryOfferBehavior(r.Resolve<StoryOfferService>(), r.Resolve<StoryMissionLauncher>(), r.Resolve<IGameMenuAdapter>())),
    };

    public override string Id => "StoryMissions";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<StoryMissionCatalog>(Reuse.Singleton);
        registrator.Register<StoryMissionLauncher>(Reuse.Singleton);
        registrator.Register<StoryOfferService>(Reuse.Singleton);
        registrator.Register<IStoryCampaignAdapter, StoryCampaignAdapter>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
