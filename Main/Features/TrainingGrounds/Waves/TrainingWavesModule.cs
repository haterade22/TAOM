using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.TrainingGrounds.Waves.Hooks;

namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// The wave fight at the training master (docs/features/training-waves.md) as a feature module: a service graph, one dialog behavior
/// and one mission logic, no patch and no model. The behavior is built with <c>new</c> for every campaign, because its constructor
/// resets the service that holds the run and the day ledger and it saves the ledger (a container singleton is constructed once per
/// process), so the module owns save data and fails closed. The mission logic joins every mission and does something only where
/// vanilla's practice controller runs, the town's Training Grounds; in a co-op session it is not added (the role table).
/// </summary>
internal sealed class TrainingWavesModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new TrainingWavesDialogBehavior(r.Resolve<WaveBetService>(), r.Resolve<IModLogger>())),
    };

    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new TrainingWavesMissionLogic(
            r.Resolve<WaveBetService>(),
            r.Resolve<ITrainingWavesConfigProvider>(),
            r.Resolve<IOutcomeLog>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "TrainingWaves";

    /// <summary>True: the day each town last ran a wave fight is saved in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) =>
        TrainingWavesIoC.RegisterTrainingWavesFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
