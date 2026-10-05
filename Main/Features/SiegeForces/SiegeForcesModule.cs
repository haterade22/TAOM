using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.SiegeForces.Models;

namespace TAOM.Features.SiegeForces;

/// <summary>
/// Siege Forces (#734, docs/features/siege-forces.md): the "choose your forces" step before a wall battle. Patch102's two
/// prefixes target <c>PlayerSiege</c> (campaign) and the core spawn logic, so the category applies at GameInit like the
/// other campaign-menu patches. The exclusion seam is a campaign model, <see cref="TaomTroopSupplierProbabilityModel"/>,
/// added by FeatureModuleHooks after SandBox's defaults. No mission behavior, no campaign behavior and no save data.
///
/// Overlap census, 2026-10-05. No other TAOM code names the three targets (<c>StartSiegeMission</c>,
/// <c>InitWithSinglePhase</c> and the model's <c>EnqueueTroopSpawnProbabilitiesAccordingToUnitSpawnPrioritization</c>).
/// A byte scan by member name (a name built at runtime would not show) of every DLL under the game's Modules folder and
/// the Steam Workshop folder finds them named by no module except the engine's own, Bannerlord Coop (Workshop
/// CoopNightly v0.1.5; Coop v0.1.6 in the Modules folder) and TOR_Core v1.3.15 (Workshop). Bannerlord Coop stays
/// comparison-only in docs/reference/provenance-register.md. TOR_Core is registered there as a behavioural port, for
/// Creature Siege Role; Siege Forces derives nothing from it. Coop's own DLLs, in both builds, name the targets only
/// where its source at commit 0d8dd2280 (Bannerlord-Coop-Team/BannerlordCoop) does: one postfix on
/// <c>DefaultTroopSupplierProbabilityModel</c>'s method (PlayerHeroSimulationExclusionPatch.cs:26-43) that returns at
/// once when <c>includePlayer</c> is true, while <see cref="SiegeForcesService.FilterAppended"/> acts only when that flag
/// is true, so the two never act on one call; a call to <c>InitWithSinglePhase</c> (CoopBattleMissionSpawnHandler.cs:285);
/// a call to the model with <c>includePlayer</c> false (BattleSimulationRunHandler.cs:362-364); no patch on
/// <c>StartSiegeMission</c> or <c>InitWithSinglePhase</c>. Coop v0.1.6's GameInterface.dll adds one more surface,
/// <c>BattleTroopSupplierInjectionPatch</c>: a constructor prefix on <c>DefaultBattleMissionAgentSpawnLogic</c> that
/// swaps in a <c>CoopTroopSupplier</c> during co-op battles. It is harmless here, because the D3 co-op gate offers no
/// picker in a co-op session. So the picker needs no patch ordering against Coop. The one surface that can be
/// contested is the model slot: TOR_Core registers its own <c>TORTroopSupplierModel</c> in it, and a later owner of the
/// slot makes the service warn that the selection was ignored.
/// </summary>
internal sealed class SiegeForcesModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch102_SiegeForces";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.GameInit),
    };

    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<TroopSupplierProbabilityModel, TaomTroopSupplierProbabilityModel>(ModelTarget.Campaign,
            resolver => new TaomTroopSupplierProbabilityModel(resolver.Resolve<SiegeForcesService>())),
    };

    public override string Id => "SiegeForces";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ISiegeForcesSettingsProvider, SiegeForcesSettingsProvider>(Reuse.Singleton);
        registrator.Register<ISiegeForcesConfigProvider, SiegeForcesConfigProvider>(Reuse.Singleton);
        registrator.Register<ISiegeForcesAdapter, SiegeForcesAdapter>(Reuse.Singleton);
        registrator.Register<SiegeForcesService>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
