using System;
using System.Linq;
using BehaviorTreeWrapper;
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

    private bool _failureLogged;

    public RaceAbilityRuntime(RaceAbilityService service, RaceAbilityProfileResolver resolver,
        RaceAbilitySettingsProvider settings, IModLogger logger)
    {
        Service = service;
        Resolver = resolver;
        Settings = settings;
        Logger = logger;
        Warn = logger.LogWarning;
        Sensor = new RaceAbilitySensor(this);
        Activator = new RaceAbilityActivator(this);
        Ticker = new RaceAbilityTicker(this);
        Deaths = new RaceAbilityDeaths(this);
    }

    public RaceAbilityService Service { get; }

    public RaceAbilityProfileResolver Resolver { get; }

    public RaceAbilitySettingsProvider Settings { get; }

    public IModLogger Logger { get; }

    // The thread tripwire's reporter (MissionThreadGuard.NoteCall), made once rather than on every call.
    internal Action<string> Warn { get; }

    public RaceAbilityStore<Agent> Store { get; } = new RaceAbilityStore<Agent>();

    public RaceAbilityTelemetry Telemetry { get; } = new RaceAbilityTelemetry();

    public RaceAbilityFallenMemory<Team> Fallen { get; } = new RaceAbilityFallenMemory<Team>();

    public RaceAbilityWaveCounter Waves { get; } = new RaceAbilityWaveCounter();

    public RaceAbilitySensor Sensor { get; }

    public RaceAbilityActivator Activator { get; }

    public RaceAbilityTicker Ticker { get; }

    public RaceAbilityDeaths Deaths { get; }

    public bool Enabled => Settings.Enabled;

    // The mission gate's last answer, for taom.print_race_abilities.
    public string MissionGate { get; set; } = "no battle has started";

    internal int WavesLogged { get; set; }

    public RaceAbilityProfile? ProfileOf(Agent? agent) =>
        agent != null && agent.IsHuman ? Resolver.Resolve(agent.Character?.Race, agent.Character?.Culture?.StringId) : null;

    public bool IsKin(RaceAbilityProfile profile, Agent other) =>
        Resolver.IsKin(profile, other.Character?.Race, ProfileOf(other));

    // Who gets a tree: a soldier with a profile and no tree yet. BehaviorTreeMissionLogic keeps one tree per
    // agent, and a second would take the first one's place and silence its listeners.
    public bool CarriesTree(Agent agent) =>
        ProfileOf(agent) != null && agent.GetComponent<BehaviorTreeAgentComponent>() == null;

    public void CountTree(Agent agent)
    {
        var profile = ProfileOf(agent);
        if (profile != null)
            Telemetry.Add(profile.AbilityId, RaceAbilityStat.TreesAttached);
    }

    // The live state of this soldier's ability, for the stat and damage models. Any thread.
    public RaceAbilityState? StateOf(Agent? agent) => agent == null ? null : Store.Get(agent);

    // One error line per battle for a step that threw (a tree pass, a death), never one per soldier.
    public void ReportFailure(string site, Exception ex)
    {
        if (_failureLogged)
            return;
        _failureLogged = true;
        Logger.LogError($"[RaceAbilities] {site} threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
    }

    public void LogReport(string heading) =>
        Logger.LogInfo($"[RaceAbilities] {heading}:\n{Telemetry.Report()}");

    // What taom.print_race_abilities prints: the gate, who is live right now, and the battle's counters.
    public string DescribeStatus()
    {
        var live = Store.LiveCounts().Select(count => $"{count.AbilityId} {count.Active} active, {count.Spent} spent").ToList();
        return $"Race abilities: {(Settings.Enabled ? "enabled" : "DISABLED in MCM")}"
               + (Resolver.ConfigEnabled ? "" : ", DISABLED in race_abilities.json")
               + $"; mission gate: {MissionGate}\n"
               + $"Live now: {(live.Count == 0 ? "none" : string.Join("; ", live))}\n"
               + Telemetry.Report();
    }

    public void Clear()
    {
        Store.Clear();
        Fallen.Clear();
        Waves.Clear();
        Telemetry.Reset();
        Sensor.Clear();
        Activator.Clear();
        Ticker.Clear();
        Deaths.Clear();
        _failureLogged = false;
        WavesLogged = 0;
        MissionGate = "no battle running";
    }
}
