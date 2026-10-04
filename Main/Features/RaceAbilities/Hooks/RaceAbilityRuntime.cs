using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Logging;
using TAOM.Features.RaceAbilities.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.RaceAbilities.Hooks;

/// <summary>
/// The race abilities' mission-time state and the engine boundary's composition root: the live store, the
/// battle's telemetry, the fallen-kin memory and the four engine-facing parts (<see cref="Sensor"/>,
/// <see cref="Activator"/>, <see cref="Ticker"/>, <see cref="Deaths"/>). Every rule is
/// <see cref="RaceAbilityService"/>'s or the resolver's. A singleton: <see cref="Clear"/> runs at every
/// mission end. Main thread only, except <see cref="StateOf"/> and the telemetry, which the stat and damage
/// models reach from any thread. Boundary code, game-tested (ADR-008).
/// </summary>
public sealed class RaceAbilityRuntime
{
    // The first waves of a battle are logged in full even with the debug log off.
    internal const int DetailedWaves = 12;

    private bool _treeFailureLogged;

    public RaceAbilityRuntime(RaceAbilityService service, RaceAbilityProfileResolver resolver,
        RaceAbilitySettingsProvider settings, IModLogger logger)
    {
        Service = service;
        Resolver = resolver;
        Settings = settings;
        Logger = logger;
        Sensor = new RaceAbilitySensor(this);
        Activator = new RaceAbilityActivator(this);
        Ticker = new RaceAbilityTicker(this);
        Deaths = new RaceAbilityDeaths(this);
    }

    public RaceAbilityService Service { get; }

    public RaceAbilityProfileResolver Resolver { get; }

    public RaceAbilitySettingsProvider Settings { get; }

    public IModLogger Logger { get; }

    public RaceAbilityStore<Agent> Store { get; } = new RaceAbilityStore<Agent>();

    public RaceAbilityTelemetry Telemetry { get; } = new RaceAbilityTelemetry();

    public RaceAbilityFallenMemory<Team> Fallen { get; } = new RaceAbilityFallenMemory<Team>();

    public RaceAbilityWaveCounter Waves { get; } = new RaceAbilityWaveCounter();

    public RaceAbilitySensor Sensor { get; }

    public RaceAbilityActivator Activator { get; }

    public RaceAbilityTicker Ticker { get; }

    public RaceAbilityDeaths Deaths { get; }

    public bool Enabled => Settings.Enabled;

    // The mission gate's last answer, for taom.race_abilities.
    public string MissionGate { get; set; } = "no battle has started";

    internal int WavesLogged { get; set; }

    public RaceAbilityProfile? ProfileOf(Agent? agent) =>
        agent != null && agent.IsHuman ? Resolver.Resolve(agent.Character?.Race, agent.Character?.Culture?.StringId) : null;

    public bool IsKin(RaceAbilityProfile profile, Agent other) =>
        Resolver.IsKin(profile, other.Character?.Race, ProfileOf(other));

    // The live state of this soldier's ability, for the stat and damage models. Any thread.
    public RaceAbilityState? StateOf(Agent? agent) => agent == null ? null : Store.Get(agent);

    // One error line per battle for a tree node that threw, never one per soldier.
    public void ReportTreeFailure(string node, Exception ex)
    {
        if (_treeFailureLogged)
            return;
        _treeFailureLogged = true;
        Logger.LogError($"[RaceAbilities] {node} threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }

    public void LogReport(string heading) =>
        Logger.LogInfo($"[RaceAbilities] {heading}:\n{Telemetry.Report()}");

    // What taom.race_abilities prints: the gate, who is live right now, and the battle's counters.
    public string DescribeStatus()
    {
        var live = Store.Entries
            .Where(pair => pair.Value.Phase != RaceAbilityPhase.Ready)
            .GroupBy(pair => pair.Value.Profile.AbilityId)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key} {group.Count(p => p.Value.Phase == RaceAbilityPhase.Active)} active, " +
                             $"{group.Count(p => p.Value.Phase == RaceAbilityPhase.Spent)} spent")
            .ToList();
        return $"Race abilities: {(Settings.Enabled ? "enabled" : "DISABLED in MCM")}; mission gate: {MissionGate}\n"
               + $"Live now: {(live.Count == 0 ? "none" : string.Join("; ", live))}\n"
               + Telemetry.Report();
    }

    public void Clear()
    {
        Store.Clear();
        Fallen.Clear();
        Waves.Clear();
        Telemetry.Reset();
        Ticker.Clear();
        _treeFailureLogged = false;
        WavesLogged = 0;
        MissionGate = "no battle running";
    }
}
