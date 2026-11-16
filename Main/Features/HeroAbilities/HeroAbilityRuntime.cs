using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.DreadAura;
using TAOM.Features.HeroAbilities.Domain;
using TAOM.Features.RaceAbilities.Hooks;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.HeroAbilities;

/// <summary>
/// The hero abilities' mission-time state (#1359): the buff ledger the models read through RaceAbilityHooks.BuffEffects,
/// plus what the mission logic, the host and the caster need. A singleton; the mission logic clears the ledger at every
/// mission end. The ledger is the only part read off the main thread. Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class HeroAbilityRuntime
{
    public HeroAbilityRuntime(HeroAbilityService service, RaceAbilityRuntime raceAbilities, ICareerAgentStatService careerStats,
        IDreadRegistry dreadRegistry, IModLogger logger, IOutcomeLog outcomes)
    {
        Service = service;
        RaceAbilities = raceAbilities;
        CareerStats = careerStats;
        DreadRegistry = dreadRegistry;
        Logger = logger;
        Outcomes = outcomes;
    }

    public HeroAbilityService Service { get; }

    // The sensor that reads a hero's surroundings for the triggers, and the visuals (burst, outline).
    public RaceAbilityRuntime RaceAbilities { get; }

    // For MoraleShock: the player's career fear scaling and the race resistances.
    public ICareerAgentStatService CareerStats { get; }

    public IDreadRegistry DreadRegistry { get; }

    public IModLogger Logger { get; }

    // Main thread only.
    public IOutcomeLog Outcomes { get; }

    public HeroBuffLedger<Agent> Ledger { get; } = new HeroBuffLedger<Agent>();

    // The owned heroes whose ranged kit is armed (#1361); the stat and damage models read it through HeroRangedHooks.
    public HeroRangedRoster<Agent> Ranged { get; } = new();

    // The running extra-arrow windows, set by the cast and read by the ranged mission logic. Main thread only; the host
    // removes a dropped hero and clears it at mission end.
    public Dictionary<Agent, HeroBurst> Bursts { get; } = new();
}
