using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The driver of the archers' fire modes, the word, Massed fire and the volley checks, which also steps the hold the line driver.</summary>
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
    private readonly Dictionary<int, MassedWord> _massedOpen = new();
    private readonly List<int> _ruled = new();
    private readonly List<int> _held = new();
    private readonly List<int> _massed = new();
    private volatile bool _firePressed;
    private volatile bool _wordPressed;
    private IReadOnlyList<int>? _fireCompanies;
    private IReadOnlyList<int>? _wordCompanies;
    private double _nextPass = double.NegativeInfinity;
    private double _nextCensus = double.NegativeInfinity;
    private double _nextMassed = double.NegativeInfinity;

    internal MarshalFireDriver(IMarshalOrderAdapter orders, IMarshalFireAdapter fire, IOutcomeLog outcomes, IModLogger logger,
        Action<MarshalMessage> say, string missionKey)
    {
        _orders = orders;
        _fire = fire;
        _outcomes = outcomes;
        _logger = logger;
        _say = say;
        _key = missionKey;
        HoldLine = new MarshalHoldLineDriver(orders, fire, outcomes, logger, say, missionKey);
    }

    /// <summary>The hold the line driver, whose key press, pass and checks this driver steps.</summary>
    internal MarshalHoldLineDriver HoldLine { get; }

    /// <summary>Key 543 was pressed, or its panel entry was clicked for <paramref name="companies"/>; stored for the next mission tick, because adding a component is engine state.</summary>
    internal void PressFire(IReadOnlyList<int>? companies = null)
    {
        _fireCompanies = companies;
        _firePressed = true;
    }

    /// <summary>Key 544 was pressed, or its panel entry clicked. Stored for the next mission tick.</summary>
    internal void PressWord(IReadOnlyList<int>? companies = null)
    {
        _wordCompanies = companies;
        _wordPressed = true;
    }

    /// <summary>The latest hold verdict of any company, or null; the field trial reads it, as the manifest reads the check line.</summary>
    internal VolleyVerdict? LastHold { get; private set; }

    /// <summary>The latest release verdict of any company, or null.</summary>
    internal VolleyVerdict? LastRelease { get; private set; }

    /// <summary>Release windows judged so far, of any company; a step compares two counts, since two verdicts of the same numbers are equal as values.</summary>
    internal int ReleasesJudged { get; private set; }

    /// <summary>Massed words given so far.</summary>
    internal int MassedWords { get; private set; }

    /// <summary>The numbers the latest Massed word was given on, set when the word is given.</summary>
    internal MassedWord? LastMassed { get; private set; }

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

        HoldLine.ApplyPress(now);
        var slow = now >= _nextPass;
        var massed = now >= _nextMassed;
        var hold = HoldLine.PassDue(now);
        if (!slow && !massed && !hold)
            return;
        CollectRuled();
        if (massed)
            MassedPass(now);
        if (hold)
            HoldLine.Pass(now, _held);
        if (!slow)
            return;
        _nextPass = now + MarshalTuning.SlowPassSeconds;
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
        {
            MarshalFireTable.SetMode(company, FireMode.Free);
            MarshalFireTable.SetHold(company, false);
        }

        _judges.Clear();
        _massedOpen.Clear();
        HoldLine.OnPlayerTeamChanged();
    }

    /// <summary>The finish of the mission that owns the table: one last summary line for each company the table holds a mode for, with no check line and no mode change.</summary>
    internal void Summarize()
    {
        for (var company = 0; company < MarshalFireTable.Companies; company++)
        {
            if (MarshalFireTable.ModeOf(company) != FireMode.Free)
                WriteSummary(company);
            HoldLine.Summarize(company, _orders.Now, CensusOf(company));
        }
    }

    /// <summary>A fault with the mission still running: the soldiers touched so far stop being called, wake from any hold they were in, and get their attack flag back.</summary>
    internal void ReleaseAll() => _fire.ReleaseAll();

    /// <summary>Forgets every soldier and judge with no native call. The finish of a mission calls it, because the agents go with the mission; after a fault it follows <see cref="ReleaseAll"/>.</summary>
    internal void Drop()
    {
        _ruled.Clear();
        _held.Clear();
        _massed.Clear();
        _judges.Clear();
        _summaryDue.Clear();
        _latest.Clear();
        _massedOpen.Clear();
        HoldLine.Drop();
        _fire.Drop();
    }

    private void ApplyFire()
    {
        if (!_orders.PlayerCommands)
            return;
        var changed = new List<int>();
        FireMode? target = null;
        foreach (var company in Named(_fireCompanies))
        {
            if (!_orders.IsControllable(company))
            {
                _outcomes.Skip(MarshalChecks.FireId, _key + ":ai-controlled:" + Count(company), "mission=" + _key + " reason=ai-controlled company=" + Count(company));
                continue;
            }

            var next = target ??= MarshalFireRules.NextMode(MarshalFireTable.ModeOf(company));
            MarshalFireTable.SetMode(company, next);
            _judges.Remove(company);
            _massedOpen.Remove(company);
            changed.Add(company);
            _outcomes.Pass(MarshalChecks.FireId, _key + ":" + next + ":" + Count(company), "mission=" + _key + " mode=" + next + " company=" + Count(company));
        }

        if (changed.Count == 0)
            return;
        _say(target == FireMode.Free ? MarshalMessage.FireFree : target == FireMode.Massed ? MarshalMessage.FireMassed : MarshalMessage.FireOnWord);
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
        foreach (var company in Named(_wordCompanies))
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

            GiveWord(company, now, HoldingOf(company));
            given++;
        }

        if (given > 0)
            _say(MarshalMessage.WordGiven);
    }

    private void GiveWord(int company, double now, int holding)
    {
        MarshalFireTable.GiveWord(company, now);
        JudgeOf(company).NoteWord(now, holding, MarshalFireTable.CountersOf(company).AfterWord);
        _logger.LogInfo("[Marshal] word company=" + Count(company) + " time=" + Seconds(now) + " holding=" + Count(holding));
        _outcomes.Pass(MarshalChecks.WordId, _key + ":" + Count(company), "mission=" + _key + " company=" + Count(company) + " holding=" + Count(holding));
    }

    private void MassedPass(double now)
    {
        _nextMassed = now + MarshalTuning.MassedPassSeconds;
        _massed.Clear();
        foreach (var company in _ruled)
        {
            if (MarshalFireTable.ModeOf(company) == FireMode.Massed)
                _massed.Add(company);
        }

        if (_massed.Count == 0)
            return;
        foreach (var census in _fire.Count(_massed, now))
        {
            _latest[census.Company] = census;
            var eligible = MarshalFireRules.Eligible(census.Holding, census.Drawing, census.Idle, census.StaleDrawing);
            if (MarshalFireRules.MassedWordDue(census.Holding, eligible, now, MarshalFireTable.WordTimeOf(census.Company)))
                GiveMassedWord(census, eligible, now);
        }
    }

    private void GiveMassedWord(FireCensus census, int eligible, double now)
    {
        var company = census.Company;
        GiveWord(company, now, census.Holding);
        var word = new MassedWord(census.Holding, eligible, census.StaleDrawing, now);
        LastMassed = word;
        MassedWords++;
        _logger.LogInfo("[Marshal] massed company=" + Count(company) + " holding=" + Count(census.Holding) + " eligible=" + Count(eligible) + " stale=" + Count(census.StaleDrawing)
            + " idle=" + Count(census.Idle) + " aside=" + Count(census.Aside));
        if (JudgeOf(company).ReleaseOpen)
            _massedOpen[company] = word;
        else if (census.Holding < MarshalTuning.VolleyMinHolding)
            _outcomes.Skip(MarshalChecks.MassedId, _key + ":few:" + Count(company), "mission=" + _key + " reason=too-few-holding company=" + Count(company) + " holding=" + Count(census.Holding));
    }

    private IReadOnlyList<int> Named(IReadOnlyList<int>? stored) => stored ?? _orders.SelectedCompanies();

    private void TakeCensus(double now)
    {
        _nextCensus = now + MarshalTuning.FireCensusSeconds;
        _latest.Clear();
        foreach (var census in _fire.Sweep(_ruled, _held, now))
            _latest[census.Company] = census;
    }

    private void CollectRuled()
    {
        _ruled.Clear();
        _held.Clear();
        for (var company = 0; company < MarshalFireTable.Companies; company++)
        {
            var mode = MarshalFireTable.ModeOf(company);
            var hold = MarshalFireTable.HoldOf(company) && !_fire.CallbackFaulted;
            if (mode == FireMode.Free && !hold)
                continue;
            if (_orders.IsControllable(company))
            {
                _ruled.Add(company);
                if (hold)
                    _held.Add(company);
                continue;
            }

            MarshalFireTable.SetMode(company, FireMode.Free);
            MarshalFireTable.SetHold(company, false);
            _judges.Remove(company);
            _massedOpen.Remove(company);
            HoldLine.End(company);
            _outcomes.Skip(MarshalChecks.FireId, _key + ":lost-control:" + Count(company), "mission=" + _key + " reason=lost-control company=" + Count(company));
        }
    }

    private void Judge(int company, double now)
    {
        if (MarshalFireTable.ModeOf(company) != FireMode.Free)
            JudgeVolley(company, now);
        if (MarshalFireTable.HoldOf(company))
            HoldLine.Judge(company, now, CensusOf(company));
    }

    private void JudgeVolley(int company, double now)
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
        {
            LastRelease = v;
            ReleasesJudged++;
        }

        var id = v.Check == VolleyCheck.Hold ? MarshalChecks.VolleyHoldId : MarshalChecks.VolleyReleaseId;
        var detail = "mission=" + _key + " company=" + Count(company) + " holding=" + Count(v.Holding) + " shots=" + Count(v.Shots);
        var key = _key + ":" + Count(company);
        if (v.Pass)
            _outcomes.Pass(id, key, detail);
        else
            _outcomes.Fail(id, key, detail);
        if (v.Check == VolleyCheck.Release && _massedOpen.TryGetValue(company, out var word))
        {
            _massedOpen.Remove(company);
            ReportMassed(company, v, word, detail);
        }
    }

    private void ReportMassed(int company, VolleyVerdict v, MassedWord word, string detail)
    {
        var full = detail + " eligible=" + Count(word.Eligible) + " stale=" + Count(word.StaleDrawing);
        var key = _key + ":" + Count(company);
        if (v.Pass)
            _outcomes.Pass(MarshalChecks.MassedId, key, full);
        else
            _outcomes.Fail(MarshalChecks.MassedId, key, full);
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

    private static string Seconds(double value) => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>What a Massed word was given on: the soldiers holding, the ones eligible, the ones whose stale draw was left out, and when.</summary>
internal readonly record struct MassedWord(int Holding, int Eligible, int StaleDrawing, double Time);
