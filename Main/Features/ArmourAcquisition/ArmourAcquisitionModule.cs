using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition.Hooks;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// Armour Acquisition as a feature module (docs/features/armour-acquisition.md). Two things live outside
/// it on purpose: SubModule.OnGameInitializationFinished calls <see cref="IArmourGateService.ApplyGating"/>
/// on EVERY game init (this runner's GameInit phase runs once per process, and each game reloads its
/// items), and CultureMarketplaceBehavior, still hand-wired, takes the stock gate in its constructor.
/// The campaign behavior is built fresh for each campaign (not a container singleton) so its
/// loaded-this-session flag cannot carry from one campaign to the next.
/// </summary>
internal sealed class ArmourAcquisitionModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new ArmourAcquisitionCampaignBehavior(
            r.Resolve<ArmourAcquisitionState>(),
            r.Resolve<IArmourGateService>(),
            r.Resolve<VisitingArmourerService>(),
            r.Resolve<ArmourStockSweepService>(),
            r.Resolve<IArmouryTownAdapter>(),
            r.Resolve<IArmourAcquisitionSettingsProvider>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IDedicatedServerProvider>(),
            r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new ArmouryMenuBehavior(
            r.Resolve<ArmouryPresenter>(),
            r.Resolve<IArmourGateService>(),
            r.Resolve<ArmouryLevelService>(),
            r.Resolve<IArmouryTownAdapter>())),
        CampaignBehaviorDecl.Of(r => new LordHarnessQuestBehavior(
            r.Resolve<LordHarnessService>(),
            r.Resolve<IArmourGateService>(),
            r.Resolve<ArmouryLevelService>(),
            r.Resolve<IArmouryTownAdapter>(),
            r.Resolve<IArmouryPlayerAdapter>(),
            r.Resolve<CareerSystem.ICareerQuestService>(),
            r.Resolve<IArmourAcquisitionConfigProvider>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IDedicatedServerProvider>(),
            r.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(r => new LordHarnessEventBehavior(
            r.Resolve<LordHarnessService>(),
            r.Resolve<IArmourGateService>(),
            r.Resolve<IArmourAcquisitionConfigProvider>(),
            r.Resolve<IArmourAcquisitionSettingsProvider>(),
            r.Resolve<IArmouryTownAdapter>(),
            r.Resolve<IArmouryPlayerAdapter>(),
            r.Resolve<ICoopSessionProvider>(),
            r.Resolve<IDedicatedServerProvider>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "ArmourAcquisition";

    /// <summary>The campaign behavior persists the feature's state in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) =>
        ArmourAcquisitionIoC.RegisterArmourAcquisitionFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
