using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.StagedSiege.Spike;

/// <summary>
/// Phase 0 spike of the Staged Siege feature (#1054): exploratory, console-driven, inert unless a <c>taom.ss_*</c>
/// command is used. Registers one mission behavior and nothing else; never merges as is.
/// </summary>
internal sealed class StagedSiegeSpikeModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Behaviors =
    {
        MissionBehaviorDecl.Of((_, resolver) => new StagedSiegeSpikeMissionBehavior(resolver.Resolve<IModLogger>())),
    };

    public override string Id => "StagedSiegeSpike";

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Behaviors;
}
