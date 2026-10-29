using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege.Hooks;

namespace TAOM.Features.StagedSiege;

/// <summary>
/// Staged Siege (docs/features/staged-siege.md, #1054): in a castle assault on a scene with recorded capture points, the
/// attackers take points A to D in order and each capture brings fresh troops. This module registers the two providers the
/// feature reads (the MCM switch and the scene catalog), adds the mission behavior and applies Patch126 at GameInit: its two
/// patches target <c>DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase</c> and <c>Mission.GetFormationSpawnFrame</c>, core
/// types, like Siege Forces' Patch102. The category is declared here and each patch class names it through
/// <see cref="PatchCategory"/>.
///
/// No interface on either provider: nothing fakes them and each has one consumer (ADR-002). Both cache for the process, so a
/// JSON edit needs a restart of the game, not a new battle. No save data: everything lives and ends inside one battle.
/// </summary>
internal sealed class StagedSiegeModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch126_StagedSiege";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly MissionBehaviorDecl[] Behaviors =
    {
        MissionBehaviorDecl.Of((_, resolver) => new StagedSiegeMissionBehavior(
            resolver.Resolve<StagedSiegeSettingsProvider>(),
            resolver.Resolve<StagedSiegeSceneCatalog>(),
            resolver.Resolve<IModLogger>())),
    };

    public override string Id => "StagedSiege";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<StagedSiegeSettingsProvider>(Reuse.Singleton);
        registrator.Register<StagedSiegeSceneCatalog>(Reuse.Singleton);
        registrator.Register<IStagedSiegeFieldReader, StagedSiegeFieldReader>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Behaviors;
}
