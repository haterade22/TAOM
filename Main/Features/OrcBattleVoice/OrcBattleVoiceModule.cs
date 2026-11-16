using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.OrcBattleVoice.Hooks;

namespace TAOM.Features.OrcBattleVoice;

/// <summary>
/// Orc battle voice (docs/features/kingdom-voices.md, route C): male adult orc AI agents in any mission use the
/// Uruk voice instead of vanilla human screams. One mission behavior, no Harmony patch, no save data.
/// The service is built with the behavior, so every mission probes the voice index afresh.
/// </summary>
internal sealed class OrcBattleVoiceModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new OrcBattleVoiceMissionBehavior(
            new OrcBattleVoiceService(
                r.Resolve<IVoiceDefinitionCatalogAdapter>(), r.Resolve<IRaceManager>(),
                r.Resolve<IOutcomeLog>(), r.Resolve<IModLogger>()),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "OrcBattleVoice";

    public override void RegisterServices(IRegistrator registrator) =>
        registrator.Register<IVoiceDefinitionCatalogAdapter, VoiceDefinitionCatalogAdapter>(Reuse.Singleton);

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
