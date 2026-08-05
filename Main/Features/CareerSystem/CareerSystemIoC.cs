using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.CareerSystem.Abilities.Executors;
using TAOM.Features.CareerSystem.Domain;
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

        // Issue #102 — CareerPerkMissionBehavior decomposition. Controllers extracted from
        // the legacy 302-line mission behavior so the V-key + effect-execution state machines
        // are independently unit-testable. (The #102 HUD controller was retired by #382 — the
        // in-battle career UI is now the energy bar injected into AgentStatus via
        // MissionAgentStatusCareerMixin + CareerEnergyBarPrefab, registered with UIExtenderEx.)
        container.Register<Abilities.IAbilityInputAdapter, Abilities.AbilityInputAdapter>(Reuse.Singleton);
        container.Register<Abilities.IMissionTimeProvider, Abilities.MissionTimeProvider>(Reuse.Singleton);
        container.Register<Abilities.IAbilityActivationController, Abilities.AbilityActivationController>(Reuse.Singleton);
        container.Register<Abilities.IAbilityEffectExecutor, Abilities.AbilityEffectExecutor>(Reuse.Singleton);

        // Phase 9b #142 — agent-stat service extracted out of TaomAgentStatCalculateModel /
        // TaomAgentApplyDamageModel bodies (gamemodels.md rule 4 — no inline branching in
        // override bodies). Reads ICareerPassiveService + the static CareerAbilityBuffTracker.
        container.Register<Abilities.ICareerAgentStatService, Abilities.CareerAgentStatService>(Reuse.Singleton);

        // Phase 4C: Ability effect execution — all 50 careers mapped to 3 archetypes.
        // GetCareerArchetypeMap() is the single source of truth — the executor registry and
        // the ICareerArchetypeService both read from it.
        container.RegisterDelegate(r => BuildAbilityEffectRegistry(r.Resolve<ICareerConfigProvider>()), Reuse.Singleton);
        container.RegisterDelegate<ICareerArchetypeService>(_ => new CareerArchetypeService(GetCareerArchetypeMap()), Reuse.Singleton);

        // Phase 5: GameModel support
        container.Register<ICareerHeroAdapterFactory, CareerHeroAdapterFactory>(Reuse.Singleton);

        // Phase 6: Career-tied quest system (verified on 1.4.5)
        container.Register<ICareerQuestConfigProvider, CareerQuestConfigProvider>(Reuse.Singleton);
        container.Register<ICareerQuestService, CareerQuestService>(Reuse.Singleton);
        container.Register<IQuestHeroAdapterFactory, QuestHeroAdapterFactory>(Reuse.Singleton);
    }

    private static CareerAbilityEffectRegistry BuildAbilityEffectRegistry(ICareerConfigProvider config)
    {
        var registry = new CareerAbilityEffectRegistry();
        foreach (var pair in GetCareerArchetypeMap())
        {
            switch (pair.Value)
            {
                case CareerArchetype.Infantry: registry.Register(new InfantryAbilityExecutor(pair.Key, config)); break;
                case CareerArchetype.Ranged:   registry.Register(new RangedAbilityExecutor(pair.Key, config));   break;
                case CareerArchetype.Cavalry:  registry.Register(new CavalryAbilityExecutor(pair.Key, config));  break;
            }
        }
        return registry;
    }

    // Single source of truth: careerId → archetype. Both the ability executor registry
    // and ICareerArchetypeService (used by CareerStartingEquipmentService) read from this.
    // Cached in a static field — invoked twice during IoC startup (executor builder +
    // archetype service registration), no reason to allocate the dict twice.
    // Disabled careers (troll WIP) are absent — adding them here re-enables their executor.
    private static readonly IReadOnlyDictionary<string, CareerArchetype> CareerArchetypeMap = BuildCareerArchetypeMap();

    internal static IReadOnlyDictionary<string, CareerArchetype> GetCareerArchetypeMap() => CareerArchetypeMap;

    private static IReadOnlyDictionary<string, CareerArchetype> BuildCareerArchetypeMap() => new Dictionary<string, CareerArchetype>
    {
        // ═══ GONDOR ═══
        ["captain_of_osgiliath"]   = CareerArchetype.Infantry,
        ["ranger_of_ithilien"]     = CareerArchetype.Ranged,
        ["knight_of_belfalas"]     = CareerArchetype.Cavalry,

        // ═══ MORDOR ═══
        ["black_uruk_captain"]     = CareerArchetype.Infantry,
        ["olog_hai_warchief"]      = CareerArchetype.Infantry,
        ["mulkerhili_cultist"]     = CareerArchetype.Ranged,
        ["snaga_rider"]            = CareerArchetype.Cavalry,

        // ═══ ROHAN ═══
        ["watchman_of_stangard"]   = CareerArchetype.Infantry,
        ["marksman_of_aldburg"]    = CareerArchetype.Ranged,
        ["eotheod_windrider"]      = CareerArchetype.Cavalry,

        // ═══ DUNLAND ═══
        ["avanc_luth_raider"]      = CareerArchetype.Infantry,
        ["wolfskin_hunter"]        = CareerArchetype.Ranged,
        ["clanguard_rider"]        = CareerArchetype.Cavalry,

        // ═══ KHAND ═══
        ["blademaster_of_ren"]     = CareerArchetype.Infantry,
        ["steppe_bowmaster"]       = CareerArchetype.Ranged,
        ["chariot_warlord"]        = CareerArchetype.Cavalry,

        // ═══ HARAD ═══
        ["tribesman_of_jelut"]     = CareerArchetype.Infantry,
        // DISABLED 2026-05-14: Troll careers WIP — re-enable by uncommenting.
        // ["far_harad_halftroll"]    = CareerArchetype.Infantry,
        ["pezarsani_javelineer"]   = CareerArchetype.Ranged,
        ["mahud_beast_rider"]      = CareerArchetype.Cavalry,

        // ═══ EASTERLINGS / RHÛN ═══
        ["codyan_legionaire"]      = CareerArchetype.Infantry,
        ["lokhas_drus_marksman"]   = CareerArchetype.Ranged,
        ["balchoth_kan"]           = CareerArchetype.Cavalry,

        // ═══ DALE ═══
        ["dale_guardsman"]         = CareerArchetype.Infantry,
        ["dale_marksman"]          = CareerArchetype.Ranged,
        ["dale_outrider"]          = CareerArchetype.Cavalry,

        // ═══ EREBOR ═══
        ["ironguard"]              = CareerArchetype.Infantry,
        ["crossbow_master"]        = CareerArchetype.Ranged,
        ["ram_rider"]              = CareerArchetype.Cavalry,

        // ═══ RIVENDELL ═══
        ["blade_dancer"]           = CareerArchetype.Infantry,
        ["elven_archer"]           = CareerArchetype.Ranged,
        ["rivendell_knight"]       = CareerArchetype.Cavalry,

        // ═══ LOTHLORIEN ═══
        ["warden"]                 = CareerArchetype.Infantry,
        ["galadhrim_archer"]       = CareerArchetype.Ranged,
        ["sentinel"]               = CareerArchetype.Cavalry,

        // ═══ MIRKWOOD ═══
        ["shadow_walker"]          = CareerArchetype.Infantry,
        ["silvan_archer"]          = CareerArchetype.Ranged,
        ["elk_rider"]              = CareerArchetype.Cavalry,

        // ═══ ISENGARD ═══
        ["uruk_berserker"]         = CareerArchetype.Infantry,
        ["uruk_crossbow"]          = CareerArchetype.Ranged,
        ["warg_scout"]             = CareerArchetype.Cavalry,

        // ═══ GUNDABAD ═══
        ["cave_troll_master"]      = CareerArchetype.Infantry,
        ["goblin_sniper"]          = CareerArchetype.Ranged,
        ["warg_pack_leader"]       = CareerArchetype.Cavalry,

        // ═══ DOL GULDUR ═══
        ["shadow_warrior"]         = CareerArchetype.Infantry,
        ["necromancer_acolyte"]    = CareerArchetype.Ranged,
        ["fell_rider"]             = CareerArchetype.Cavalry,

        // ═══ UMBAR ═══
        ["corsair_boarder"]        = CareerArchetype.Infantry,
        ["corsair_crossbow"]       = CareerArchetype.Ranged,
        ["corsair_captain"]        = CareerArchetype.Cavalry,
    };

    public static void InitializeCalculators(IMutationCalculatorRegistry registry)
    {
        BuiltInCalculators.RegisterAll(registry);
    }
}
