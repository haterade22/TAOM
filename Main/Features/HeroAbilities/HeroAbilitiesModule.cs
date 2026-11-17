using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Features.HeroAbilities.Hooks;
using TAOM.Features.RaceAbilities.Hooks;
using TAOM.Features.SignatureStrikes;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Features.HeroAbilities;

/// <summary>
/// Hero Abilities (#1359, docs/features/hero-abilities.md): the mission heroes listed in hero_abilities.json cast timed
/// abilities that buff their allies and shake their enemies, in the Long Defeat missions and in the Helm's Deep story mission, where the three stand free (#1366).
/// The buffs are the race abilities' channels and reach the engine through the same models, via
/// <see cref="RaceAbilityHooks.BuffEffects"/> (wired here). No save data: a buff lives inside one battle.
/// </summary>
internal sealed class HeroAbilitiesModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new HeroAbilitiesMissionLogic(r.Resolve<HeroAbilityRuntime>())),
        // The ranged kit (#1361). Its arrow kill goes through the one executor class the melee strikes use (decision R3).
        MissionBehaviorDecl.Of((_, r) =>
        {
            var runtime = r.Resolve<HeroAbilityRuntime>();
            return new HeroRangedMissionLogic(runtime, new SignatureInstantKill(r.Resolve<ISignatureStrikeService>(), r.Resolve<ISignatureAgentRoster>(), r.Resolve<ISignatureStrikeRegistry>(), runtime.Outcomes, runtime.Logger));
        }),
        // The balance tally: kills, damage dealt and taken, time alive. Log lines only.
        MissionBehaviorDecl.Of((_, r) => new HeroCombatLogic(r.Resolve<HeroAbilityRuntime>())),
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
        HeroRangedHooks.Roster = resolver.Resolve<HeroAbilityRuntime>().Ranged;
        CombatMechanicsHooks.ShooterPiercesShields = HeroRangedHooks.PiercesShields;
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
