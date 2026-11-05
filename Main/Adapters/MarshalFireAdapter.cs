using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TAOM.Features.MarshalsWord;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Adapters;

/// <summary><see cref="IMarshalFireAdapter"/> over the current mission's player team, keying each touched soldier by reference.</summary>
internal sealed class MarshalFireAdapter : IMarshalFireAdapter
{
    private readonly IModLogger _logger;
    private readonly HashSet<Agent> _touched = new();
    private readonly HashSet<Agent> _seen = new();
    private readonly List<FireCensus> _census = new();
    private readonly List<Agent> _leaving = new();
    private int _drawing;
    private int _holding;
    private int _idle;
    private int _aside;

    public MarshalFireAdapter(IModLogger logger) => _logger = logger;

    public bool HasTouched => _touched.Count > 0;

    public IReadOnlyList<FireCensus> Sweep(IReadOnlyList<int> ruled)
    {
        MissionThreadGuard.NoteCall("MarshalFireAdapter.Sweep", _logger.LogError);
        _census.Clear();
        _seen.Clear();
        var formations = Mission.Current?.PlayerTeam?.FormationsIncludingSpecialAndEmpty;
        foreach (var company in ruled)
        {
            if (formations == null || company < 0 || company >= formations.Count)
                continue;
            _drawing = 0;
            _holding = 0;
            _idle = 0;
            _aside = 0;
            formations[company].ApplyActionOnEachUnit(Touch);
            _census.Add(new FireCensus(company, _drawing, _holding, _idle, _aside));
        }

        Release();
        return _census;
    }

    public void ReleaseAll()
    {
        MissionThreadGuard.NoteCall("MarshalFireAdapter.ReleaseAll", _logger.LogError);
        foreach (var agent in _touched)
            TurnOff(agent);
        Drop();
    }

    public void Drop()
    {
        _touched.Clear();
        _seen.Clear();
        _census.Clear();
        _leaving.Clear();
    }

    private void Touch(Agent agent)
    {
        if (!agent.IsHuman || agent.IsPlayerControlled || !agent.IsActive())
            return;
        var component = agent.GetComponent<MarshalFireComponent>();
        if (component == null)
        {
            component = new MarshalFireComponent(agent);
            agent.AddComponent(component);
        }

        if (_touched.Add(agent))
            agent.SetHasOnAiInputSetCallback(true);
        _seen.Add(agent);
        var state = component.State;
        if (state == DrawState.Drawing)
            _drawing++;
        else if (state == DrawState.Holding)
            _holding++;
        else if (component.StandingAside)
            _aside++;
        else
            _idle++;
    }

    // Native never calls a component whose flag is off, so its state would freeze at Holding: it is reset here, on the main thread.
    private void Release()
    {
        _leaving.Clear();
        foreach (var agent in _touched)
        {
            if (!_seen.Contains(agent))
                _leaving.Add(agent);
        }

        foreach (var agent in _leaving)
        {
            _touched.Remove(agent);
            TurnOff(agent);
        }
    }

    private static void TurnOff(Agent agent)
    {
        if (!AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State == AgentState.Deleted)
            return;
        agent.SetHasOnAiInputSetCallback(false);
        agent.GetComponent<MarshalFireComponent>()?.Reset();
    }
}
