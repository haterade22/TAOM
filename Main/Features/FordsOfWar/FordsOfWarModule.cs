using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Fords of War (docs/features/fords-of-war/build-spec.md): a campaign-free mission mode started from the main menu, where the
/// player commands an army across three lanes with gold, towers and heroes. This module registers the config catalog, the
/// session the client reads, the menu adapter and the start flow; declares Patch131 (the tower damage table, a prefix on
/// <c>DestructableComponent.OnHit</c>, a core type) for the first mission; and adds the main-menu option once.
///
/// No mission-behavior declaration: <c>FordsOfWarMissionLogic</c> is added only by <c>FordsMissionLauncher</c>, so no other
/// mission ever carries it. No save data: a match lives and ends inside one mission. The game-manager factory is registered
/// as core's null default; TAOM.Client registers the real one and replaces it (ADR-013).
/// </summary>
internal sealed class FordsOfWarModule : TaomFeatureModule
{
    internal const string PatchCategory = "Patch131_FordsOfWar";

    private static readonly PatchCategoryDecl[] Categories =
    {
        new(PatchCategory, ApplyPhase.FirstMission),
    };

    public override string Id => "FordsOfWar";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<FordsConfigCatalog>(Reuse.Singleton);
        registrator.Register<FordsSession>(Reuse.Singleton);
        registrator.Register<FordsRunReport>(Reuse.Singleton);
        registrator.Register<IFordsMenuAdapter, FordsMenuAdapter>(Reuse.Singleton);
        registrator.Register<IFordsOfWarGameManagerFactory, NullFordsOfWarGameManagerFactory>(Reuse.Singleton);
        registrator.Register<FordsSetupPrompt>(Reuse.Singleton);
        registrator.Register<FordsMenuEntry>(Reuse.Singleton);
        registrator.Register<Hooks.FordsMissionLauncher>(Reuse.Singleton);
    }

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        // The engine calls the first-screen hook with no try and again on every tick while it throws (#807), so the step goes
        // through MainMenuStep. Adding the option is guarded by its id, so a repeat adds nothing.
        if (phase == ApplyPhase.MainMenu)
            MainMenuStep.Run("fords of war menu option", () => resolver.Resolve<FordsMenuEntry>().Install(),
                () => resolver.Resolve<IModLogger>());
    }
}
