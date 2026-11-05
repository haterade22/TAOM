using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The driver of the archers' fire modes, the word and the two volley checks.</summary>
internal sealed class MarshalFireDriver
{
    private readonly IMarshalOrderAdapter _orders;
    private readonly IMarshalFireAdapter _fire;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly Action<MarshalMessage> _say;
    private readonly string _key;
    private readonly Dictionary<int, MarshalVolleyJudge> _judges = new();
    private readonly Dictionary<int, double> _summaryDue = new();
    private readonly Dictionary<int, FireCensus> _latest = new();
    private readonly List<int> _ruled = new();
    private volatile bool _firePressed;
    private volatile bool _wordPressed;
    private double _nextPass = double.NegativeInfinity;
    private double _nextCensus = double.NegativeInfinity;

    internal MarshalFireDriver(IMarshalOrderAdapter orders, IMarshalFireAdapter fire, IOutcomeLog outcomes, IModLogger logger,
        Action<MarshalMessage> say, string missionKey)
    {
        _orders = orders;
        _fire = fire;
        _outcomes = outcomes;
        _logger = logger;
        _say = say;
        _key = missionKey;
    }

    /// <summary>Key 543 was pressed. Stored for the next mission tick, because adding a component is engine state.</summary>
    internal void PressFire() => _firePressed = true;

    /// <summary>Key 544 was pressed. Stored for the next mission tick.</summary>
    internal void PressWord() => _wordPressed = true;

    /// <summary>The latest hold verdict of any company, or null; the field trial reads it, as the manifest reads the check line.</summary>
    internal VolleyVerdict? LastHold { get; private set; }

    /// <summary>The latest release verdict of any company, or null.</summary>
    internal VolleyVerdict? LastRelease { get; private set; }

    /// <summary>The company's latest census, or an empty one before the first.</summary>
    internal FireCensus CensusOf(int company) => _latest.TryGetValue(company, out var census) ? census : default;

    internal int HoldingOf(int company) => CensusOf(company).Holding;

    internal void Pass()
    {
        var now = _orders.Now;
        if (_firePressed)
        {
            _firePressed = false;
            ApplyFire();
        }

        if (_wordPressed)
        {
            _wordPressed = false;
            ApplyWord(now);
        }

        if (now < _nextPass)
            return;
        _nextPass = now + MarshalTuning.SlowPassSeconds;
        CollectRuled();
        if (_ruled.Count == 0 && !_fire.HasTouched)
            return;
        if (now >= _nextCensus)
            TakeCensus(now);

        foreach (var company in _ruled)
            Judge(company, now);
    }

    /// <summary>The player's team changed, so each company number names a formation of the old team.</summary>
    internal void OnPlayerTeamChanged()
    {
        for (var company = 0; company < MarshalFireTable.Companies; company++)
            MarshalFireTable.SetMode(company, FireMode.Free);
        _judges.Clear();
    }

    /// <summary>
    /// The finish of the mission that owns the table: one last summary for each company the table holds a mode for, because it can be the only evidence of a word
    /// given just before the battle ended. It reads the table and asks nobody who is alive, so a company wiped out in the last moments still gets its line, and it
    /// changes no mode and writes no check line.
    /// </summary>
    internal void Summarize()
    {
        for (var company = 0; company < MarshalFireTable.Companies; company++)
        {
            if (MarshalFireTable.ModeOf(company) != FireMode.Free)
                WriteSummary(company);
        }
    }

    /// <summary>A fault with the mission still running: the soldiers touched so far stop being called, and wake from any hold they were in.</summary>
    internal void ReleaseAll() => _fire.ReleaseAll();

    /// <summary>Forgets every soldier and judge with no native call. The finish of a mission calls it, because the agents go with the mission; after a fault it follows <see cref="ReleaseAll"/>.</summary>
    internal void Drop()
    {
        _ruled.Clear();
        _judges.Clear();
        _summaryDue.Clear();
        _latest.Clear();
        _fire.Drop();
    }

    private void ApplyFire()
    {
        if (!_orders.PlayerCommands)
            return;
        var changed = new List<int>();
        FireMode? target = null;
        foreach (var company in _orders.SelectedCompanies())
        {
            if (!_orders.IsControllable(company))
            {
                _outcomes.Skip(MarshalChecks.FireId, _key + ":ai-controlled:" + Count(company), "mission=" + _key + " reason=ai-controlled company=" + Count(company));
                continue;
            }

            var next = target ??= MarshalFireRules.NextMode(MarshalFireTable.ModeOf(company));
            MarshalFireTable.SetMode(company, next);
            _judges.Remove(company);
            changed.Add(company);
            _outcomes.Pass(MarshalChecks.FireId, _key + ":" + next + ":" + Count(company), "mission=" + _key + " mode=" + next + " company=" + Count(company));
        }

        if (changed.Count == 0)
            return;
        _say(target == FireMode.Free ? MarshalMessage.FireFree : MarshalMessage.FireOnWord);
        if (target != FireMode.Free)
            _orders.Issue(changed, MarshalOrderPayload.FireAtWill());
    }

    private void ApplyWord(double now)
    {
        if (!_orders.PlayerCommands)
            return;
        CollectRuled();
        TakeCensus(now);
        var given = 0;
        foreach (var company in _orders.SelectedCompanies())
        {
            if (!_orders.IsControllable(company))
            {
                _outcomes.Skip(MarshalChecks.WordId, _key + ":ai-controlled:" + Count(company), "mission=" + _key + " reason=ai-controlled company=" + Count(company));
                continue;
            }

            if (MarshalFireTable.ModeOf(company) == FireMode.Free)
            {
                _outcomes.Skip(MarshalChecks.WordId, _key + ":mode-free:" + Count(company), "mission=" + _key + " reason=mode-free company=" + Count(company));
                continue;
            }

            var holding = HoldingOf(company);
            MarshalFireTable.GiveWord(company, now);
            JudgeOf(company).NoteWord(now, holding, MarshalFireTable.CountersOf(company).AfterWord);
            _logger.LogInfo("[Marshal] word company=" + Count(company) + " time=" + now.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " holding=" + Count(holding));
            _outcomes.Pass(MarshalChecks.WordId, _key + ":" + Count(company), "mission=" + _key + " company=" + Count(company) + " holding=" + Count(holding));
            given++;
        }

        if (given > 0)
            _say(MarshalMessage.WordGiven);
    }

    private void TakeCensus(double now)
    {
        _nextCensus = now + MarshalTuning.FireCensusSeconds;
        _latest.Clear();
        foreach (var census in _fire.Sweep(_ruled))
            _latest[census.Company] = census;
    }

    private void CollectRuled()
    {
        _ruled.Clear();
        for (var company = 0; company < MarshalFireTable.Companies; company++)
        {
            if (MarshalFireTable.ModeOf(company) == FireMode.Free)
                continue;
            if (_orders.IsControllable(company))
            {
                _ruled.Add(company);
                continue;
            }

            MarshalFireTable.SetMode(company, FireMode.Free);
            _judges.Remove(company);
            _outcomes.Skip(MarshalChecks.FireId, _key + ":lost-control:" + Count(company), "mission=" + _key + " reason=lost-control company=" + Count(company));
        }
    }

    private void Judge(int company, double now)
    {
        var counters = MarshalFireTable.CountersOf(company);
        var holding = HoldingOf(company);
        var judge = JudgeOf(company);
        Report(company, judge.JudgeHold(now, holding, counters.WhileHolding, MarshalFireTable.WordTimeOf(company)));
        Report(company, judge.JudgeRelease(now, counters.AfterWord));
        if (!_summaryDue.TryGetValue(company, out var due) || now >= due)
        {
            _summaryDue[company] = now + MarshalTuning.SummarySeconds;
            WriteSummary(company);
        }
    }

    private void Report(int company, VolleyVerdict? verdict)
    {
        if (verdict == null)
            return;
        var v = verdict.Value;
        if (v.Check == VolleyCheck.Hold)
            LastHold = v;
        else
            LastRelease = v;
        var id = v.Check == VolleyCheck.Hold ? MarshalChecks.VolleyHoldId : MarshalChecks.VolleyReleaseId;
        var detail = "mission=" + _key + " company=" + Count(company) + " holding=" + Count(v.Holding) + " shots=" + Count(v.Shots);
        var key = _key + ":" + Count(company);
        if (v.Pass)
            _outcomes.Pass(id, key, detail);
        else
            _outcomes.Fail(id, key, detail);
    }

    private void WriteSummary(int company)
    {
        var counters = MarshalFireTable.CountersOf(company);
        var census = CensusOf(company);
        _logger.LogInfo("[Marshal] fire company=" + Count(company) + " mode=" + MarshalFireTable.ModeOf(company) + " drawing=" + Count(census.Drawing)
            + " holding=" + Count(census.Holding) + " shotsWhileHolding=" + Count(counters.WhileHolding) + " shotsAfterWord=" + Count(counters.AfterWord)
            + " shotsFree=" + Count(counters.Free) + " shotsBeforeFirstWord=" + Count(counters.BeforeFirstWord) + " idle=" + Count(census.Idle)
            + " aside=" + Count(census.Aside) + " shotsAfterOther=" + Count(counters.AfterOther));
    }

    private MarshalVolleyJudge JudgeOf(int company)
    {
        if (!_judges.TryGetValue(company, out var judge))
            _judges[company] = judge = new MarshalVolleyJudge();
        return judge;
    }

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
