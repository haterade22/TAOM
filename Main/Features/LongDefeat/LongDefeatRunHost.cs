using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat.Diagnostics;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// Sits between the mission logic's engine callbacks and the <see cref="LongDefeatDirector"/>; a throw from the start or from any
/// tick abandons the run, writes its report and leaves the mission (#699).
/// </summary>
internal sealed class LongDefeatRunHost
{
    // The engine needs the first half second to build its teams and deployment plan.
    private const float ReadyAt = 0.5f;
    private const int MaxFaultsLogged = 5;

    private readonly RunPlan _plan;
    private readonly LongDefeatSession _session;
    private readonly IModLogger _logger;
    private readonly IOutcomeLog _outcomes;
    private readonly ILongDefeatMenuAdapter _menu;
    private readonly string _missionKey;
    private readonly Func<ILongDefeatBattlefieldAdapter> _fieldFactory;
    private readonly Func<ILongDefeatEffectsAdapter>? _effectsFactory;
    private readonly LongDefeatRunRecords? _records;
    private LongDefeatDirector? _director;
    private bool _startDue;
    private bool _leaving;
    private int _faults;

    /// <param name="missionKey">One value per mission (the launcher counts them): the outcome log latches each check line once per id, result and key for the whole process.</param>
    public LongDefeatRunHost(RunPlan plan, LongDefeatSession session, IModLogger logger, IOutcomeLog outcomes, ILongDefeatMenuAdapter menu,
        string missionKey, Func<ILongDefeatBattlefieldAdapter> fieldFactory, LongDefeatRunRecords? records = null,
        Func<ILongDefeatEffectsAdapter>? effectsFactory = null)
    {
        _plan = plan;
        _session = session;
        _logger = logger;
        _outcomes = outcomes;
        _menu = menu;
        _missionKey = missionKey;
        _fieldFactory = fieldFactory;
        _effectsFactory = effectsFactory;
        _records = records;
    }

    /// <summary>The mission is set up: the next tick that is ready starts the run. Spawns nothing itself (rca-fords-of-war-2026-10-31.md).</summary>
    public void MarkStartDue() => _startDue = true;

    /// <summary><paramref name="missionTime"/> is read only while the start is due, so a mission that is not ready is never asked.</summary>
    public void Step(float dt, Func<float> missionTime)
    {
        try
        {
            if (_startDue && missionTime() >= ReadyAt)
            {
                _startDue = false;
                StartRun();
            }

            _director?.Tick(dt);
        }
        catch (Exception ex)
        {
            OnFailure("OnMissionTick", ex, fault: true);
        }
    }

    public void OnAgentRemoved(object affected, bool loss) => Guarded("OnAgentRemoved", () => _director?.OnAgentRemoved(affected, loss));

    public void OnAgentFleeing(object agent) => Guarded("OnAgentFleeing", () => _director?.OnAgentFleeing(agent));

    public void ShutDown()
    {
        var director = _director;
        _director = null;
        Guarded("TearDown", () => director?.Shutdown());
    }

    public void Leave()
    {
        if (_leaving)
            return;

        _leaving = true;
        Guarded("Leave", _menu.EndGame);
    }

    private void StartRun()
    {
        var director = new LongDefeatDirector(_plan, _fieldFactory(), _session, _logger, _outcomes, _missionKey, _records, _effectsFactory?.Invoke());
        _director = director;
        if (director.Start())
            return;

        _session.RecordFault("Boot");
        ShutDown();
        Leave();
    }

    private void Guarded(string where, Action step)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            OnFailure(where, ex, fault: false);
        }
    }

    private void OnFailure(string where, Exception ex, bool fault)
    {
        if (++_faults <= MaxFaultsLogged)
            _logger.LogError($"[LongDefeat] {where} failed ({ex.GetType().Name}: {ex.Message}) {ex.StackTrace}");
        if (fault)
            EndRunOnFault(where, ex);
    }

    private void EndRunOnFault(string where, Exception ex)
    {
        _startDue = false;
        _session.RecordFault(where);
        LongDefeatChecks.Record(_outcomes, LongDefeatChecks.EndId, _missionKey, LongDefeatChecks.Fault(where, ex));
        ShutDown();
        Leave();
    }
}
