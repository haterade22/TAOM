using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CampaignStartOptions.Hooks;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// Campaign start options (docs/features/campaign-start-options.md): the module carries only the session behavior
/// that shows the "this campaign keeps its own start choices" notice. The services register in
/// <see cref="CampaignStartOptionsIoC"/> (called from <c>IoC.Configure</c>) and the pin runs from
/// <c>SubModule.OnGameStart</c>, both before any module. Nothing is saved by the behavior.
/// </summary>
internal sealed class CampaignStartOptionsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new CampaignStartOptionsBehavior(
            r.Resolve<CampaignStartMismatchNotice>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "CampaignStartOptions";

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
