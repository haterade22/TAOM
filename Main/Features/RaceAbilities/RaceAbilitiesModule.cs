using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities.Hooks;

namespace TAOM.Features.RaceAbilities;

/// <summary>
/// Race Abilities (docs/features/race-abilities.md): a timed battle ability per race, or for men per
/// culture, fired by a behaviour tree on each AI soldier when his moment comes. One mission logic attaches
/// the trees, ages the abilities and reports what they did; the effects reach the engine through the shared
/// stat, damage and morale models, which call <see cref="RaceAbilityHooks"/> (wired here, in
/// <see cref="InitializeStatics"/>). No save data: every ability lives and ends inside one battle.
/// </summary>
internal sealed class RaceAbilitiesModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new RaceAbilitiesMissionLogic(
            r.Resolve<RaceAbilityRuntime>(),
            r.Resolve<RaceAbilitySettingsProvider>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "RaceAbilities";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IRaceAbilitiesConfigProvider, RaceAbilitiesConfigProvider>(Reuse.Singleton);
        registrator.Register<RaceAbilitySettingsProvider>(Reuse.Singleton);
        registrator.Register<RaceAbilityProfileResolver>(Reuse.Singleton);
        registrator.Register<RaceAbilityService>(Reuse.Singleton);
        registrator.Register<RaceAbilityRuntime>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver) =>
        RaceAbilityHooks.Runtime = resolver.Resolve<RaceAbilityRuntime>();

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
