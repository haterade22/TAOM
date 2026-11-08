using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.DeadTargetReset.Hooks;

namespace TAOM.Features.DeadTargetReset;

/// <summary>
/// The dead-target reset (#1096, docs/features/dead-target-reset.md): a mission behavior in every mission that resets AI targets
/// on dead agents before the engine's corpse delete turns them into a crash. No services: the behavior needs only the logger.
/// </summary>
internal sealed class DeadTargetResetModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new DeadTargetResetMissionBehavior(r.Resolve<IModLogger>())),
    };

    public override string Id => "DeadTargetReset";

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
