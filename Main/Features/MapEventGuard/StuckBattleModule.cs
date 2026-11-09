using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MapEventGuard.Hooks;

namespace TAOM.Features.MapEventGuard;

/// <summary>
/// The stuck AI battle guard (#748, docs/features/map-event-guard.md "Stuck AI battles"): an hourly sweep that
/// ends map battles the engine can never finish. No patch, no saved data.
/// </summary>
internal sealed class StuckBattleModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new StuckBattleCampaignBehavior(
            r.Resolve<StuckBattleService>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "StuckBattle";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IStuckBattleAdapter, StuckBattleAdapter>(Reuse.Singleton);
        registrator.Register<StuckBattleService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
