using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CreatureBandits.Hooks;

namespace TAOM.Features.CreatureBandits;

/// <summary>
/// Creature Bandits (#692) as a feature module. No services: the decisions are the static
/// <see cref="CreatureBanditRules"/>, read by patches that run on the engine's worker threads. Two patch categories:
/// Patch93 at process load like the other mission patches (Patch92): the spawn swap, the weapon-state hook, the panic
/// and rout blocks and the weapon guards; Patch94 at game init, the brood's map icon and, for broods and troll bands
/// (#694), the encounter and the bandit join path. One mission behavior: the creature's tree and the routed-count
/// backstop. Two campaign behaviors, the Mirkwood brood spawner and the troll band spawner (no save data). No game model:
/// the creatures' damage-taken rule rides <c>CreatureBanditDamage.Reduce</c> in the campaign's TaomCombatMechanicsModel and
/// in Custom Battle's TaomCustomBattleDamageModel (CombatMechanics, added by SubModule.RegisterCustomBattleModels, #788),
/// not here: a module-declared Custom Battle model is added after that step and would shadow it. Order-free: nothing else patches <c>Mission.SpawnTroop</c>, <c>CommonAIComponent.OnHit</c>,
/// <c>Mission.CanAgentRout</c> or the bandit join roster, and the backstop only acts on creature agents. The temporary
/// diagnostics add one mission and one campaign behavior.
/// </summary>
internal sealed class CreatureBanditsModule : TaomFeatureModule
{
    private static readonly PatchCategoryDecl[] Categories =
    {
        new(CreatureBanditsConfig.PatchCategory, ApplyPhase.ProcessLoad),
        // The map icon and encounter patches target SandBox.View and campaign menu types.
        new(CreatureBanditsConfig.CampaignPatchCategory, ApplyPhase.GameInit),
    };

    private static readonly MissionBehaviorDecl[] Behaviors =
    {
        MissionBehaviorDecl.Of((mission, resolver) => new CreatureBanditMissionBehavior(resolver.Resolve<IModLogger>())),
        // Temporary diagnostics (strip after sign-off with the Diagnostics folder).
        MissionBehaviorDecl.Of((mission, resolver) => new Diagnostics.CreatureBanditDiagnosticsBehavior()),
    };

    private static readonly CampaignBehaviorDecl[] Campaign =
    {
        CampaignBehaviorDecl.Of(resolver => new CreatureBroodSpawnBehavior(resolver.Resolve<IModLogger>())),
        CampaignBehaviorDecl.Of(resolver => new TrollBandSpawnBehavior(resolver.Resolve<IModLogger>())),
        // Temporary diagnostics (strip after sign-off with the Diagnostics folder).
        CampaignBehaviorDecl.Of(resolver => new Diagnostics.CreatureBroodCampaignDiagBehavior()),
    };

    public override string Id => "CreatureBandits";

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Campaign;

    public override void InitializeStatics(IResolver resolver) =>
        CreatureBanditLog.Logger = resolver.Resolve<IModLogger>();

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Behaviors;
}
