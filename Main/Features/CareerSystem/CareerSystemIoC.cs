using DryIoc;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Features.CareerSystem;

public static class CareerSystemIoC
{
    public static void RegisterCareerSystemFeature(IContainer container)
    {
        // Phase 1: Data persistence
        container.Register<ICareerDataService, CareerDataService>(Reuse.Singleton);

        // Phase 2: Config, registry, passives, mutations
        container.Register<ICareerConfigProvider, CareerConfigProvider>(Reuse.Singleton);
        container.Register<ICareerRegistry, CareerRegistry>(Reuse.Singleton);
        container.Register<ICareerPassiveService, CareerPassiveService>(Reuse.Singleton);
        container.Register<IMutationCalculatorRegistry, MutationCalculatorRegistry>(Reuse.Singleton);

        // Phase 3: Campaign integration
        container.Register<ICareerCreationHandler, CareerCreationHandler>(Reuse.Singleton);
        container.Register<ICareerSwitchService, CareerSwitchService>(Reuse.Singleton);

        // Phase 4: Abilities and mutations
        container.Register<Abilities.ICareerAbilityService, Abilities.CareerAbilityService>(Reuse.Singleton);
        container.Register<IMutationService, MutationService>(Reuse.Singleton);

        // Phase 4C: Ability effect execution — all 50 careers mapped to 3 archetypes
        container.RegisterDelegate(r => BuildAbilityEffectRegistry(r.Resolve<ICareerConfigProvider>()), Reuse.Singleton);

        // Phase 5: GameModel support
        container.Register<ICareerHeroAdapterFactory, CareerHeroAdapterFactory>(Reuse.Singleton);
    }

    private static CareerAbilityEffectRegistry BuildAbilityEffectRegistry(ICareerConfigProvider config)
    {
        var registry = new CareerAbilityEffectRegistry();

        // ═══ GONDOR ═══
        registry.Register(new InfantryAbilityExecutor("captain_of_osgiliath", config));
        registry.Register(new RangedAbilityExecutor("ranger_of_ithilien", config));
        registry.Register(new CavalryAbilityExecutor("knight_of_belfalas", config));

        // ═══ MORDOR ═══
        registry.Register(new InfantryAbilityExecutor("black_uruk_captain", config));
        registry.Register(new InfantryAbilityExecutor("olog_hai_warchief", config));
        registry.Register(new RangedAbilityExecutor("mulkerhili_cultist", config));
        registry.Register(new CavalryAbilityExecutor("snaga_rider", config));

        // ═══ ROHAN ═══
        registry.Register(new InfantryAbilityExecutor("watchman_of_stangard", config));
        registry.Register(new RangedAbilityExecutor("marksman_of_aldburg", config));
        registry.Register(new CavalryAbilityExecutor("eotheod_windrider", config));

        // ═══ DUNLAND ═══
        registry.Register(new InfantryAbilityExecutor("avanc_luth_raider", config));
        registry.Register(new RangedAbilityExecutor("wolfskin_hunter", config));
        registry.Register(new CavalryAbilityExecutor("clanguard_rider", config));

        // ═══ KHAND ═══
        registry.Register(new InfantryAbilityExecutor("blademaster_of_ren", config));
        registry.Register(new RangedAbilityExecutor("steppe_bowmaster", config));
        registry.Register(new CavalryAbilityExecutor("chariot_warlord", config));

        // ═══ HARAD ═══
        registry.Register(new InfantryAbilityExecutor("tribesman_of_jelut", config));
        registry.Register(new InfantryAbilityExecutor("far_harad_halftroll", config));
        registry.Register(new RangedAbilityExecutor("pezarsani_javelineer", config));
        registry.Register(new CavalryAbilityExecutor("mahud_beast_rider", config));

        // ═══ EASTERLINGS / RHÛN ═══
        registry.Register(new InfantryAbilityExecutor("codyan_legionaire", config));
        registry.Register(new RangedAbilityExecutor("lokhas_drus_marksman", config));
        registry.Register(new CavalryAbilityExecutor("balchoth_kan", config));

        // ═══ DALE ═══
        registry.Register(new InfantryAbilityExecutor("dale_guardsman", config));
        registry.Register(new RangedAbilityExecutor("dale_marksman", config));
        registry.Register(new CavalryAbilityExecutor("dale_outrider", config));

        // ═══ EREBOR ═══
        registry.Register(new InfantryAbilityExecutor("ironguard", config));
        registry.Register(new RangedAbilityExecutor("crossbow_master", config));
        registry.Register(new CavalryAbilityExecutor("ram_rider", config));

        // ═══ RIVENDELL ═══
        registry.Register(new InfantryAbilityExecutor("blade_dancer", config));
        registry.Register(new RangedAbilityExecutor("elven_archer", config));
        registry.Register(new CavalryAbilityExecutor("rivendell_knight", config));

        // ═══ LOTHLORIEN ═══
        registry.Register(new InfantryAbilityExecutor("warden", config));
        registry.Register(new RangedAbilityExecutor("galadhrim_archer", config));
        registry.Register(new CavalryAbilityExecutor("sentinel", config));

        // ═══ MIRKWOOD ═══
        registry.Register(new InfantryAbilityExecutor("shadow_walker", config));
        registry.Register(new RangedAbilityExecutor("silvan_archer", config));
        registry.Register(new CavalryAbilityExecutor("elk_rider", config));

        // ═══ ISENGARD ═══
        registry.Register(new InfantryAbilityExecutor("uruk_berserker", config));
        registry.Register(new RangedAbilityExecutor("uruk_crossbow", config));
        registry.Register(new CavalryAbilityExecutor("warg_scout", config));

        // ═══ GUNDABAD ═══
        registry.Register(new InfantryAbilityExecutor("cave_troll_master", config));
        registry.Register(new RangedAbilityExecutor("goblin_sniper", config));
        registry.Register(new CavalryAbilityExecutor("warg_pack_leader", config));

        // ═══ DOL GULDUR ═══
        registry.Register(new InfantryAbilityExecutor("shadow_warrior", config));
        registry.Register(new RangedAbilityExecutor("necromancer_acolyte", config));
        registry.Register(new CavalryAbilityExecutor("fell_rider", config));

        // ═══ UMBAR ═══
        registry.Register(new InfantryAbilityExecutor("corsair_boarder", config));
        registry.Register(new RangedAbilityExecutor("corsair_crossbow", config));
        registry.Register(new CavalryAbilityExecutor("corsair_captain", config));

        return registry;
    }

    public static void InitializeCalculators(IMutationCalculatorRegistry registry)
    {
        BuiltInCalculators.RegisterAll(registry);
    }
}
