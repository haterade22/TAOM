using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.HeroAbilities.Hooks;
using TAOM.Features.RaceAbilities.Hooks;

namespace TAOM.Features.HeroAbilities;

/// <summary>
/// Hero Abilities (#1359, docs/features/hero-abilities.md): the mission heroes listed in hero_abilities.json cast timed
/// abilities that buff their allies and shake their enemies, in the Long Defeat missions only (the story mission's gate opens, but no kit hero stands there yet).
/// The buffs are the race abilities' channels and reach the engine through the same models, via
/// <see cref="RaceAbilityHooks.BuffEffects"/> (wired here). No save data: a buff lives inside one battle.
/// </summary>
internal sealed class HeroAbilitiesModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new HeroAbilitiesMissionLogic(r.Resolve<HeroAbilityRuntime>())),
    };

    public override string Id => "HeroAbilities";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHeroAbilitiesConfigProvider, HeroAbilitiesConfigProvider>(Reuse.Singleton);
        registrator.Register<HeroAbilityService>(Reuse.Singleton);
        registrator.Register<HeroAbilityRuntime>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        RaceAbilityHooks.BuffEffects = resolver.Resolve<HeroAbilityRuntime>().Ledger.EffectsOf;
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
