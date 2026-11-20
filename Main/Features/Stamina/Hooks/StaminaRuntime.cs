using System;
using System.Globalization;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Stamina's mission-time state and the composition root of its engine boundary (#1497): the cells, the refresh
/// queue, the battle's counters and the ticker. A singleton: <see cref="Clear"/> runs at every mission's first tick
/// and at its end. Main thread only, except <see cref="Armed"/>, <see cref="Tuning"/>, the store's lookup and the
/// counters, which the stat models, the hit callbacks and the AI input callback reach from any thread. Boundary
/// code, game-tested, as RaceAbilityRuntime is; every rule is in the Domain types and StaminaService.
/// </summary>
public sealed class StaminaRuntime
{
    private readonly StaminaConfigProvider _config;
    private readonly IRaceManager _races;
    private bool _racesChecked;
    private volatile bool _armed;
    private int _missionSerial;

    public StaminaRuntime(StaminaConfigProvider config, StaminaSettingsProvider settings, IModLogger logger, IOutcomeLog outcomes,
        IRaceManager races)
    {
        _config = config;
        _races = races;
        Settings = settings;
        Logger = logger;
        Outcomes = outcomes;
        Warn = logger.LogWarning;
        Ticker = new StaminaTicker(this);
    }

    public StaminaSettingsProvider Settings { get; }

    public IModLogger Logger { get; }

    public IOutcomeLog Outcomes { get; }

    // The thread tripwire's reporter (MissionThreadGuard.NoteCall), made once rather than on every call.
    internal Action<string> Warn { get; }

    /// <summary>Immutable; any thread.</summary>
    public StaminaTuning Tuning => _config.GetTuning();

    public StaminaStore<Agent> Store { get; } = new StaminaStore<Agent>();

    public StaminaRefreshQueue<Agent> Refreshes { get; } = new StaminaRefreshQueue<Agent>();

    public StaminaTelemetry Telemetry { get; } = new StaminaTelemetry();

    public StaminaTicker Ticker { get; }

    /// <summary>True while this mission runs the stamina rules. Any thread.</summary>
    public bool Armed => _armed;

    /// <summary>The mission gate's last answer, for taom.print_stamina.</summary>
    public string MissionGate { get; set; } = "no battle has started";

    /// <summary>The key of this mission's check lines: each battle writes its own.</summary>
    public string MissionKey { get; private set; } = "m0";

    /// <summary>Main thread, at a mission's first tick.</summary>
    public void BeginMission() => MissionKey = "m" + (++_missionSerial).ToString(CultureInfo.InvariantCulture);

    public void Arm()
    {
        WarnUnknownRaces();
        _armed = true;
    }

    /// <summary>
    /// A soldier's race name for the peoples table, or null. The id is checked first: the registry's name lookup
    /// answers "human" for an id it does not know (csharp-architecture.md, "Validate before lookup").
    /// </summary>
    public string? RaceNameOf(int? raceId) =>
        raceId is int id && _races.IsValidRaceId(id) ? _races.GetRaceNameFromId(id) : null;

    // Once per process, at the first armed battle: the race registry exists only with the game loaded. A race name
    // in stamina.json that the game does not know matches nobody, so the row would do nothing without a word.
    private void WarnUnknownRaces()
    {
        if (_racesChecked)
            return;
        _racesChecked = true;
        foreach (var name in Tuning.Peoples.Races.Keys)
        {
            if (!_races.IsValidRaceName(name))
                Logger.LogWarning($"[Stamina] stamina.json peoples.races.{name} is not a race the game knows; the row matches nobody");
        }
    }

    /// <summary>Readers stop first, then the state goes. Makes no engine call.</summary>
    public void Clear()
    {
        _armed = false;
        Store.Clear();
        Refreshes.Clear();
        Telemetry.Reset();
        Ticker.Clear();
        MissionGate = "no battle running";
    }
}
