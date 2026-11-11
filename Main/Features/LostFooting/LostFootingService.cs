using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Features.LostFooting;

/// <summary>
/// Runs the Lost Footing census for ONE mission. It owns the one validated clock: a tick with a non-finite or
/// non-positive delta is ignored before anything else. Once a second it asks the gate whether the mission is still
/// eligible. While open and switched on, every tick reads its share of the agent list (a whole sweep aims at
/// <see cref="LostFootingTunables.SweepSeconds"/>; at least one agent a tick, never more than <see cref="LostFootingTunables.AgentsPerTick"/> agents in one
/// tick) and folds it into the per-mission state; every census interval, only while someone is stranded, it writes the
/// census lines. When the census pauses (switched off, or the gate closed) it forgets what it saw, so paused time is
/// never bridged. The census changes nothing; the rescue (#894 phase 2, its own toggle) moves a stuck agent through the rescue adapter. A fresh instance is built per mission. Main thread only: the owning
/// mission behavior calls <see cref="Tick"/> from <c>OnMissionTick</c> and <see cref="Finish"/> at the end.
/// </summary>
public sealed class LostFootingService
{
    private const float GateSeconds = 1f;

    private readonly ILostFootingAdapter _adapter;
    private readonly ILostFootingRescueAdapter _rescue;
    private readonly ILostFootingSettingsProvider _settings;
    private readonly IModLogger _logger;
    private readonly LostFootingTunables _tunables;
    private readonly IStampClock _clock;
    private readonly Func<bool> _isOpen;
    private readonly LostFootingMissionState _state = new LostFootingMissionState();
    private readonly List<AgentProbe> _slice = new List<AgentProbe>();

    private float _sinceGate = GateSeconds;
    private bool _open;
    private bool _paused;

    public LostFootingService(
        ILostFootingAdapter adapter, ILostFootingRescueAdapter rescue, ILostFootingSettingsProvider settings, IModLogger logger, LostFootingTunables tunables,
        IStampClock clock, Func<bool> isOpen)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _rescue = rescue ?? throw new ArgumentNullException(nameof(rescue));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tunables = tunables ?? throw new ArgumentNullException(nameof(tunables));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _isOpen = isOpen ?? throw new ArgumentNullException(nameof(isOpen));
    }

    public void Tick(float dt)
    {
        if (!FiniteFloatValidator.IsFinite(dt) || dt <= 0f) return;
        _state.Clock += dt;

        // The gate is read here, once a second, behind the validated delta.
        _sinceGate += dt;
        if (_sinceGate >= GateSeconds)
        {
            _sinceGate = 0f;
            _open = _isOpen();
        }

        if (!_open || !(_settings.CensusEnabled || _settings.RescueEnabled))
        {
            Pause();
            return;
        }

        _paused = false;
        RunTick(dt);
    }

    /// <summary>Writes the end-of-battle summary, once. Writes nothing for a battle the census never started on.</summary>
    public void Finish()
    {
        if (!_state.Started || _state.SummaryWritten) return;
        _state.SummaryWritten = true;
        LostFootingRules.CloseEpisodes(_state);
        _logger.LogInfo(CensusLine.Summary(_state));
        var cells = LostFootingRules.Hotspots(_state, LostFootingRules.TopHotspots);
        for (var i = 0; i < cells.Count; i++)
            _logger.LogInfo(CensusLine.Hotspot(i + 1, cells[i], _tunables.HotspotCellMeters));
    }

    private void Pause()
    {
        if (_paused || !_state.Started) return;
        _paused = true;
        LostFootingRules.Pause(_state);
    }

    private void RunTick(float dt)
    {
        if (!_state.Started) Start();

        var count = _adapter.AgentCount;
        var share = Math.Min((float)_tunables.AgentsPerTick, count * dt / _tunables.SweepSeconds);
        var plan = LostFootingRules.PlanSlice(_state.Cursor, (int)Math.Ceiling(share), count);
        _state.Cursor = plan.Next;
        if (plan.Take > 0) ReadAndFold(plan);

        if (plan.Wrapped)
        {
            LostFootingRules.EndSweep(_state);
            LostFootingRules.Tally(_state);
        }

        if (_state.Clock < _state.NextCensus) return;
        _state.NextCensus = _state.Clock + _tunables.CensusIntervalSeconds;
        var tally = LostFootingRules.Tally(_state);
        if (_settings.CensusEnabled) WriteCensus(tally);
    }

    private void ReadAndFold(SlicePlan plan)
    {
        var began = _clock.Now;
        _slice.Clear();
        _adapter.ReadSlice(plan.Start, plan.Take, _slice);
        foreach (var probe in _slice)
            Fold(probe);
        var ms = (_clock.Now - began) * 1000.0 / _clock.Frequency;
        _state.Probes += _slice.Count;
        _state.TotalMs += ms;
        if (ms > _state.MaxTickMs) _state.MaxTickMs = ms;
        _slice.Clear();
    }

    private void Start()
    {
        _state.Started = true;
        var scene = _adapter.ReadScene();
        _state.SceneId = scene.SceneId;
        _state.SceneWaterLevel = scene.WaterLevel;
        _state.NextCensus = _state.Clock + _tunables.CensusIntervalSeconds;
        _logger.LogInfo(CensusLine.Start(scene, _adapter.AgentCount));
    }

    private void Fold(AgentProbe probe)
    {
        var entry = LostFootingRules.Observe(_state, probe, _state.Clock, _tunables);

        if (entry == null) return;

        // The second look runs once per episode, and only for an agent that has held its miss long enough to count.
        if (entry.OffMesh && !entry.ConfirmTried)
        {
            entry.ConfirmTried = true;
            entry.Detail = _adapter.Confirm(probe.Handle);
        }

        if (_settings.RescueEnabled) Rescue(probe.Handle, entry);
    }

    private void Rescue(object handle, StrandEntry entry)
    {
        var plan = LostFootingRules.PlanRescue(entry, _state.Clock, _tunables);
        if (plan.Move == RescueMove.None) return;

        RescueResult result;
        if (plan.Move == RescueMove.Near)
        {
            entry.NearTried = true;
            var step = plan.NeedLand ? _tunables.RescueWaterStepMeters : _tunables.RescueMaxStepMeters;
            result = _rescue.MoveNear(handle, _tunables.RescueNearMeters, step, plan.NeedLand);
        }
        else
        {
            entry.FarTried = true;
            result = _rescue.MoveToFormation(handle, _tunables.RescuePlayerMeters, plan.NeedLand);
            if (!result.Moved && result.Reason == RescueResult.PlayerNear && !WaitForThePlayerToLeave(entry)) return;
        }

        if (result.Moved)
        {
            _state.Rescued++;
            entry.AwaitFree = true;
            ForgetTheSecondLook(entry);
        }
        else
        {
            _state.RescueRefused++;
        }
        _logger.LogInfo(CensusLine.Rescue(plan, entry, result, _state.Clock));
    }

    /// <summary>The player can walk away, so a player-near refusal is tried again later. True for the first one while
    /// stranded, which is logged and counted; later ones stay silent.</summary>
    private bool WaitForThePlayerToLeave(StrandEntry entry)
    {
        var first = float.IsNaN(entry.FarRetryAt);
        entry.FarTried = false;
        entry.FarRetryAt = _state.Clock + _tunables.RescueRetrySeconds;
        return first;
    }

    /// <summary>The second look was taken at the old spot; the next sighting confirms again where the agent now stands.</summary>
    private static void ForgetTheSecondLook(StrandEntry entry)
    {
        entry.Detail = null;
        entry.ConfirmTried = false;
    }

    private void WriteCensus(LostFootingTally tally)
    {
        foreach (var c in LostFootingTally.Classes)
        {
            if (tally.Count(c) == 0) continue;
            _logger.LogInfo(CensusLine.Class(c, _state.Clock, tally, tally.Tracked));
            foreach (var sample in LostFootingRules.Samples(_state, c, _tunables.SamplesPerClass))
                _logger.LogInfo(CensusLine.Sample(c, sample, _state.Clock));
        }
    }
}
