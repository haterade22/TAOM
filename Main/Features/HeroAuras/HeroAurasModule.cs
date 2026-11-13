using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.HeroAuras.Hooks;
using TAOM.Features.RaceAbilities.Hooks;

namespace TAOM.Features.HeroAuras;

/// <summary>
/// Hero Auras (#1127, docs/features/hero-auras.md): named heroes, listed by hero StringId in hero_auras.json, strengthen
/// the allied soldiers near them. The effects are the race abilities' channels and reach the engine through the same
/// models, via <see cref="RaceAbilityHooks.AuraEffects"/> (wired here). No save data: an aura lives inside one battle.
/// </summary>
internal sealed class HeroAurasModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((_, r) => new HeroAurasMissionLogic(r.Resolve<HeroAuraRuntime>())),
    };

    public override string Id => "HeroAuras";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHeroAurasConfigProvider, HeroAurasConfigProvider>(Reuse.Singleton);
        registrator.Register<HeroAuraSettingsProvider>(Reuse.Singleton);
        registrator.Register<HeroAuraService>(Reuse.Singleton);
        registrator.Register<HeroAuraRuntime>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        var ledger = resolver.Resolve<HeroAuraRuntime>().Ledger;
        RaceAbilityHooks.AuraEffects = ledger.EffectsOf;
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
