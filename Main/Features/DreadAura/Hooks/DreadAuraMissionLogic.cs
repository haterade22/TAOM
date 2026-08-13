using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Features.DreadAura.Hooks;

/// <summary>
/// Entry point for the Aura of Unnatural Dread: gate the mission, schedule the pulse, stand down on
/// failure. Tracking is <see cref="DreadSourceTracker"/>, executing a pulse <see cref="DreadPulseRunner"/>,
/// scheduling <see cref="DreadPulseScheduler"/>, every decision <see cref="IDreadAuraService"/>.
/// Design and balance: <c>docs/features/dread-aura.md</c>.
///
/// Inherits <see cref="MissionLogic"/>, NOT <see cref="MissionBehavior"/>: MissionBehavior plus
/// <c>BehaviorType =&gt; Logic</c> NREs every tick
/// (<c>docs/reviews/rca-looter-battle-nre-2026-05-24.md</c>; pinned by DreadAuraBindingTests).
/// </summary>
public sealed class DreadAuraMissionLogic : MissionLogic
{
    /// <summary>Agents are still spawning early on; a source registered before its team is
    /// assigned would pulse against a null team.</summary>
    private const float WarmupSeconds = 1f;

    private readonly IDreadAuraService _service;
    private readonly IDreadAuraSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly DreadSourceTracker _tracker;
    private readonly DreadPulseRunner _pulse;
    private readonly DreadPulseScheduler _scheduler = new DreadPulseScheduler();
    private readonly List<DreadPulseScheduler.DuePulse> _dueBuffer = new List<DreadPulseScheduler.DuePulse>();

    private bool _scanned;
    private bool _disabledForThisMission;
    private float _timeSinceStart;
    public DreadAuraMissionLogic()
    {
        _service = IoC.Resolve<IDreadAuraService>();
        _settings = IoC.Resolve<IDreadAuraSettingsProvider>();
        _logger = IoC.Resolve<IModLogger>();
        _tracker = new DreadSourceTracker(IoC.Resolve<IDreadRegistry>());
        _pulse = new DreadPulseRunner(_service, _settings);
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        // Unconditional, BEFORE any enabled gate: behind IsEnabled the warm-up clock would freeze
        // while the feature is off, stranding it in warm-up when toggled back on mid-battle.
        _timeSinceStart += dt;

        if (_disabledForThisMission)
            return;

        // Patch37_CrashReport turns an OnMissionTick throw into a crash report, so an unguarded
        // per-frame exception becomes tens of thousands of them. Fail once, then stand down.
        try
        {
            Tick();
        }
        catch (Exception ex)
        {
            StandDown($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private void Tick()
    {
        if (_timeSinceStart < WarmupSeconds || !_service.IsEnabled)
            return;

        var mission = Mission.Current;
        if (mission == null)
            return;

        // Re-read every tick, NEVER cached at init: MissionTeamAIType is assigned in
        // MissionCombatantsLogic.EarlyStart, after every OnBehaviorInitialize.
        if (!DreadMissionGate.IsEligible(mission))
            return;

        if (!_scanned)
        {
            _tracker.ScanMission(mission);
            _scanned = true;
            if (_tracker.Count > 0)
                _logger.LogInfo($"[DreadAura] {_tracker.Count} dread source(s) on the field");
        }

        _tracker.Prune();
        if (_tracker.Count == 0)
            return;

        PulseDueSources(mission);
    }

    private void PulseDueSources(Mission mission)
    {
        _scheduler.SelectDue(
            _tracker.Sources,
            mission.CurrentTime,
            _service.PulseIntervalSeconds,
            _service.MaxSourcesPerFrame,
            _dueBuffer);

        var affectsPlayerTroops = _settings.AffectsPlayerTroops;
        var playerTeam = mission.PlayerTeam;

        foreach (var due in _dueBuffer)
            _pulse.Run(mission, _tracker.Sources[due.Index], due.Elapsed, playerTeam, affectsPlayerTroops);
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        // Before the initial scan the scan itself picks this agent up; registering here too
        // would double-add it.
        if (_disabledForThisMission || !_scanned)
            return;

        try
        {
            _tracker.TryRegister(agent, Mission.Current?.CurrentTime ?? 0f);
        }
        catch (Exception ex)
        {
            StandDown($"source registration failed, {ex.Message}");
        }
    }

    private void StandDown(string reason)
    {
        _disabledForThisMission = true;
        _logger.LogError($"[DreadAura] disabled for this mission after {reason}");
    }
    protected override void OnEndMission()
    {
        base.OnEndMission();

        // Agent references must not outlive the mission, and Agent.Index is reused within one.
        _tracker.Clear();
        _pulse.Clear();
        _scheduler.Reset();
        _dueBuffer.Clear();
        _scanned = false;
        _disabledForThisMission = false;
        _timeSinceStart = 0f;
    }
}
