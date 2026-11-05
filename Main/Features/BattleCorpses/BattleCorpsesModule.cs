using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BattleCorpses.Hooks;

namespace TAOM.Features.BattleCorpses;

/// <summary>
/// Battle corpse cleanup and the ragdoll and corpse advisor (#701, docs/features/battle-corpses.md).
/// The mission behavior is added to every mission and gates itself; the advisor runs on the first
/// main menu of the process, where the stall and patch-failure notices already show their inquiries.
/// </summary>
internal sealed class BattleCorpsesModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new BattleCorpseMissionBehavior(
            r.Resolve<BattleCorpsePolicy>(),
            r.Resolve<IGraphicsOptionsAdapter>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "BattleCorpses";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IBattleCorpseSettingsProvider, BattleCorpseSettingsProvider>(Reuse.Singleton);
        registrator.Register<IGraphicsOptionsAdapter, GraphicsOptionsAdapter>(Reuse.Singleton);
        registrator.Register<BattleCorpsePolicy>(Reuse.Singleton);
        registrator.Register<BattleSettingsAdvisor>(Reuse.Singleton);
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.MainMenu)
            BattleSettingsAdviceNotifier.OfferIfRisky(resolver.Resolve<BattleSettingsAdvisor>(), resolver.Resolve<IModLogger>());
    }
}
