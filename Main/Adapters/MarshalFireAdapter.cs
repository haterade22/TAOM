using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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
    private readonly int[] _drawing = new int[MarshalFireTable.Companies];
    private readonly int[] _holding = new int[MarshalFireTable.Companies];
    private readonly int[] _idle = new int[MarshalFireTable.Companies];
    private readonly int[] _aside = new int[MarshalFireTable.Companies];
    private readonly int[] _stale = new int[MarshalFireTable.Companies];
    private readonly int[] _flagOff = new int[MarshalFireTable.Companies];
    private readonly int[] _present = new int[MarshalFireTable.Companies];
    private readonly List<HoldCensus> _holdCensus = new();
    private double _now;
    private bool _holdThisCompany;

    public MarshalFireAdapter(IModLogger logger) => _logger = logger;

    public bool HasTouched => _touched.Count > 0;

    public bool CallbackFaulted => MarshalWordComponent.IsFaulted;

    public IReadOnlyList<HoldCensus> Hold(IReadOnlyList<int> held, double now)
    {
        MissionThreadGuard.NoteCall("MarshalFireAdapter.Hold", _logger.LogError);
        _holdCensus.Clear();
        foreach (var company in held)
        {
            if (company < 0 || company >= _present.Length)
                continue;
            var tally = new HoldTally(company);
            foreach (var agent in _touched)
            {
                if ((agent.Formation?.Index ?? -1) != company)
                    continue;
                var component = agent.GetComponent<MarshalWordComponent>();
                if (component == null || !AgentSlotIdentity.IsCurrentOccupant(agent) || !agent.IsAIControlled)
                    continue;
                component.RefreshHold(true, now);
                tally.Add(component, now);
            }

            if (tally.Units > 0)
                _holdCensus.Add(tally.ToCensus(OrderOf(company)));
        }

        return _holdCensus;
    }

    public IReadOnlyList<FireCensus> Sweep(IReadOnlyList<int> ruled, IReadOnlyList<int> held, double now)
    {
        MissionThreadGuard.NoteCall("MarshalFireAdapter.Sweep", _logger.LogError);
        _census.Clear();
        _seen.Clear();
        _now = now;
        var formations = Mission.Current?.PlayerTeam?.FormationsIncludingSpecialAndEmpty;
        foreach (var company in ruled)
        {
            if (formations == null || company < 0 || company >= formations.Count)
                continue;
            Clear(company);
            _holdThisCompany = Contains(held, company);
            formations[company].ApplyActionOnEachUnit(Touch);
            _census.Add(Read(company));
        }

        Release();
        return _census;
    }

    public IReadOnlyList<FireCensus> Count(IReadOnlyList<int> companies, double now)
    {
        _census.Clear();
        foreach (var company in companies)
            Clear(company);
        foreach (var agent in _touched)
        {
            var index = agent.Formation?.Index ?? -1;
            if (index < 0 || index >= _present.Length || !Contains(companies, index))
                continue;
            var component = agent.GetComponent<MarshalWordComponent>();
            if (component != null)
                Tally(index, component, now);
        }

        foreach (var company in companies)
        {
            if (_present[company] > 0)
                _census.Add(Read(company));
        }

        return _census;
    }

    public void ReleaseAll()
    {
        MissionThreadGuard.NoteCall("MarshalFireAdapter.ReleaseAll", _logger.LogError);
        Exception? first = null;
        foreach (var agent in _touched)
        {
            try
            {
                TurnOff(agent);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[MarshalsWord] giving back a soldier's flag and firing order failed: {ex}");
                first ??= ex;
            }
        }

        Drop();
        if (first != null)
            ExceptionDispatchInfo.Capture(first).Throw();
    }

    public void Drop()
    {
        _touched.Clear();
        _seen.Clear();
        _census.Clear();
        _leaving.Clear();
    }

    private static bool Contains(IReadOnlyList<int> list, int value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
                return true;
        }

        return false;
    }

    private static string OrderOf(int company)
    {
        var formations = Mission.Current?.PlayerTeam?.FormationsIncludingSpecialAndEmpty;
        return formations == null || company >= formations.Count ? "none" : formations[company].GetReadonlyMovementOrderReference().OrderEnum.ToString();
    }

    private void Clear(int company)
    {
        if (company < 0 || company >= _present.Length)
            return;
        _drawing[company] = 0;
        _holding[company] = 0;
        _idle[company] = 0;
        _aside[company] = 0;
        _stale[company] = 0;
        _flagOff[company] = 0;
        _present[company] = 0;
    }

    private FireCensus Read(int company) =>
        new(company, _drawing[company], _holding[company], _idle[company], _aside[company], _stale[company], _flagOff[company]);

    private void Touch(Agent agent)
    {
        if (!agent.IsHuman || !agent.IsAIControlled || !agent.IsActive())
            return;
        var component = agent.GetComponent<MarshalWordComponent>();
        if (component == null)
        {
            component = new MarshalWordComponent(agent);
            agent.AddComponent(component);
        }

        if (_touched.Add(agent))
            agent.SetHasOnAiInputSetCallback(true);
        _seen.Add(agent);
        if (_holdThisCompany || component.OwesAttackFlag || component.OwesFiringOrder)
            component.RefreshHold(_holdThisCompany, _now);
        Tally(agent.Formation?.Index ?? -1, component, _now);
    }

    private void Tally(int company, MarshalWordComponent component, double now)
    {
        if (company < 0 || company >= _present.Length)
            return;
        _present[company]++;
        var state = component.State;
        if (state == DrawState.Drawing)
        {
            _drawing[company]++;
            if (MarshalFireRules.IsStaleDraw(component.StateSince, now))
                _stale[company]++;
        }
        else if (state == DrawState.Holding)
        {
            _holding[company]++;
        }
        else if (component.StandingAside)
        {
            _aside[company]++;
        }
        else
        {
            _idle[company]++;
        }

        if (component.AttackFlagOff)
            _flagOff[company]++;
    }

    private sealed class HoldTally
    {
        private readonly int _company;
        private int _qualified;
        private int _flagOff;
        private int _anchored;
        private int _far;
        private int _measured;
        private int _woundedEver;
        private int _struck;
        private int _struckSettled;
        private int _struckFlagOn;
        private SlotCounts _trend;
        private float _max;
        private double _sum;
        private string _why = "";

        internal HoldTally(int company) => _company = company;

        internal int Units { get; private set; }

        internal void Add(MarshalWordComponent component, double now)
        {
            Units++;
            _trend += component.TakeSlotTrend();
            var wounded = component.WoundedAt;
            if (!double.IsNegativeInfinity(wounded))
                _woundedEver++;
            if (!component.HoldQualifies)
            {
                if (_why.Length == 0)
                    _why = component.HoldWhyNot;
                return;
            }

            _qualified++;
            if (component.AttackFlagOff)
                _flagOff++;
            if (MarshalHoldLineRules.StrikingBack(wounded, now))
                AddStrike(component, now - wounded);
            if (!component.SlotStored)
                return;
            if (component.SlotAnchored)
                _anchored++;
            var distance = component.SlotDistance;
            if (float.IsNaN(distance) || float.IsInfinity(distance))
                return;
            _measured++;
            _sum += distance;
            _max = Math.Max(_max, distance);
            if (distance > MarshalTuning.SlotNearMetres)
                _far++;
        }

        internal HoldCensus ToCensus(string order) =>
            new(_company, Units, _qualified, _flagOff, _anchored, _far, _woundedEver, _struck, _struckSettled, _struckFlagOn, _max,
                _measured == 0 ? 0f : (float)(_sum / _measured), order, _why, _trend);

        private void AddStrike(MarshalWordComponent component, double age)
        {
            _struck++;
            if (!MarshalHoldLineRules.IsSettled(age))
                return;
            _struckSettled++;
            if (component.AttackFlagOn)
                _struckFlagOn++;
        }
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
            TurnOff(agent);
            _touched.Remove(agent);
        }
    }

    private static void TurnOff(Agent agent)
    {
        if (!AgentSlotIdentity.IsCurrentOccupant(agent) || agent.State == AgentState.Deleted)
            return;
        var component = agent.GetComponent<MarshalWordComponent>();
        try
        {
            if (component is { OwesAttackFlag: true } or { OwesFiringOrder: true })
                component.ReleaseHold();
        }
        finally
        {
            agent.SetHasOnAiInputSetCallback(false);
            component?.Reset();
        }
    }
}
