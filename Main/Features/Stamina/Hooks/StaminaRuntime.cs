using System;
using TAOM.Core.Logging;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// Stamina's mission-time state and the composition root of its engine boundary (#1497): the cells, the refresh
/// queue, the battle's counters and the ticker. A singleton: <see cref="Clear"/> runs at every mission's first tick
/// and at its end. Main thread only, except <see cref="Armed"/>, <see cref="Tuning"/>, the store's lookup and the
/// counters, which the stat models and the AI input callback reach from any thread. Boundary code, game-tested, as
/// RaceAbilityRuntime is; every rule is in the Domain types and the service.
/// </summary>
public sealed class StaminaRuntime
{
    private readonly StaminaConfigProvider _config;
    private volatile bool _armed;

    public StaminaRuntime(StaminaConfigProvider config, StaminaSettingsProvider settings, IModLogger logger)
    {
        _config = config;
        Settings = settings;
        Logger = logger;
        Warn = logger.LogWarning;
        Ticker = new StaminaTicker(this);
    }

    public StaminaSettingsProvider Settings { get; }

    public IModLogger Logger { get; }

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

    public void Arm() => _armed = true;

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
