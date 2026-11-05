using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The driver of the archers' fire modes, the word, Massed fire, the volley checks and the hold-the-line proof.</summary>
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
    private readonly Dictionary<int, HoldRun> _holdRuns = new();
    private readonly Dictionary<int, HoldCensus> _holdLatest = new();
    private readonly HashSet<int> _strikePassed = new();
    private readonly HashSet<int> _strikeFailed = new();
    private readonly HashSet<int> _slotFailed = new();
    private readonly List<int> _ruled = new();
    private readonly List<int> _held = new();
    private readonly List<int> _massed = new();
    private volatile bool _firePressed;
    private volatile bool _wordPressed;
    private volatile bool _holdPressed;
    private IReadOnlyList<int>? _fireCompanies;
    private IReadOnlyList<int>? _wordCompanies;
    private IReadOnlyList<int>? _holdCompanies;
    private double _nextPass = double.NegativeInfinity;
    private double _nextCensus = double.NegativeInfinity;
    private double _nextMassed = double.NegativeInfinity;
    private double _nextHold = double.NegativeInfinity;

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

    /// <summary>
    /// Key 543 was pressed, or its panel entry was clicked for <paramref name="companies"/>, which the panel names because it can clear its selection as it closes. Stored for the
    /// next mission tick, because adding a component is engine state; with no companies the tick's own selection is used.
    /// </summary>
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

    /// <summary>Key 545 was pressed, or its panel entry clicked. Stored for the next mission tick, because clearing an attack flag is agent state.</summary>
    internal void PressHold(IReadOnlyList<int>? companies = null)
    {
        _holdCompanies = companies;
        _holdPressed = true;
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

    /// <summary>The latest hold-the-line verdict of any company, or null.</summary>
    internal HoldVerdict? LastHoldLine { get; private set; }

    /// <summary>The company's latest census, or an empty one before the first.</summary>
    internal FireCensus CensusOf(int company) => _latest.TryGetValue(company, out var census) ? census : default;

    internal int HoldingOf(int company) => CensusOf(company).Holding;

    /// <summary>Steered soldiers of the held company who ended a hold read nearer their slot, summed since the hold went on.</summary>
    internal int SlotCloserOf(int company) => _holdRuns.TryGetValue(company, out var run) ? run.Closer : 0;

    /// <summary>Steered soldiers of the held company who ended a hold read farther from their slot, summed since the hold went on.</summary>
    internal int SlotFartherOf(int company) => _holdRuns.TryGetValue(company, out var run) ? run.Farther : 0;

    /// <summary>Steered hold reads of the held company, summed since the hold went on: the denominator of the two sides.</summary>
    internal int SlotSteeredOf(int company) => _holdRuns.TryGetValue(company, out var run) ? run.Steered : 0;

    /// <summary>Reads beyond the slack with no replaced input since the previous read, summed since the hold went on.</summary>
    internal int SlotLooseOf(int company) => _holdRuns.TryGetValue(company, out var run) ? run.Loose : 0;

    /// <summary>The company's slot check has failed since the hold went on.</summary>
    internal bool SlotFailed(int company) => _slotFailed.Contains(company);

    /// <summary>The held company's latest line count, or an empty one before the first pass; the field trial reads it as the log's lines do.</summary>
    internal HoldCensus HoldCensusOf(int company) => _holdLatest.TryGetValue(company, out var census) ? census : default;

    /// <summary>The company's line has answered a wound with its attack flag, judged once at least one settled wound stood in the window.</summary>
    internal bool StrikePassed(int company) => _strikePassed.Contains(company);

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

        if (_holdPressed)
        {
            _holdPressed = false;
            ApplyHold(now);
        }

        var slow = now >= _nextPass;
        var massed = now >= _nextMassed;
        var hold = now >= _nextHold;
        if (!slow && !massed && !hold)
            return;
        CollectRuled();
        if (massed)
            MassedPass(now);
        if (hold)
            HoldPass(now);
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
        _holdRuns.Clear();
        _holdLatest.Clear();
        _strikePassed.Clear();
        _strikeFailed.Clear();
        _slotFailed.Clear();
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
            if (!MarshalFireTable.HoldOf(company))
                continue;
            WriteHoldLine(company, _orders.Now);
            WriteSlotLine(company);
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
        _holdRuns.Clear();
        _holdLatest.Clear();
        _strikePassed.Clear();
        _strikeFailed.Clear();
        _slotFailed.Clear();
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

    // Massed fire (build-spec 7.2): the word is given when enough of the eligible archers hold. The count is the cheap one, over the soldiers already touched.
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

    // Hold the line (build-spec 9.2): a pressed key toggles every selected company under the player's control, and a mixed selection converges on the first one's change.
    private void ApplyHold(double now)
    {
        if (!_orders.PlayerCommands)
            return;
        bool? target = null;
        var changed = 0;
        foreach (var company in Named(_holdCompanies))
        {
            if (!_orders.IsControllable(company))
            {
                _outcomes.Skip(MarshalChecks.HoldId, _key + ":ai-controlled:" + Count(company), "mission=" + _key + " reason=ai-controlled company=" + Count(company));
                continue;
            }

            var on = target ??= !MarshalFireTable.HoldOf(company);
            MarshalFireTable.SetHold(company, on);
            if (on)
            {
                _slotFailed.Remove(company);
                _holdRuns[company] = new HoldRun(now, company);
            }
            else
            {
                WriteSlotLine(company);
                _holdRuns.Remove(company);
                _holdLatest.Remove(company);
            }

            changed++;
            var state = on ? "on" : "off";
            _outcomes.Pass(MarshalChecks.HoldId, _key + ":" + state + ":" + Count(company), "mission=" + _key + " hold=" + state + " company=" + Count(company));
        }

        if (changed > 0)
            _say(target == true ? MarshalMessage.HoldOn : MarshalMessage.HoldOff);
    }

    // Hold the line (build-spec 9.2): each held soldier's facts, flag and firing order follow the line at the hold cadence, and the line's slot and strike back are judged.
    private void HoldPass(double now)
    {
        _nextHold = now + MarshalTuning.HoldReadSeconds;
        if (_held.Count == 0)
            return;
        foreach (var raw in _fire.Hold(_held, now))
        {
            var run = RunOf(raw.Company, now);
            run.WoundedEver = Math.Max(run.WoundedEver, raw.WoundedEver);
            var census = raw with { WoundedEver = run.WoundedEver };
            _holdLatest[census.Company] = census;
            run.NoteTrend(census.Closer, census.Farther, census.Steered, census.Loose);
            JudgeStrike(census);
            JudgeSlot(census, now);
        }
    }

    private void JudgeStrike(HoldCensus census)
    {
        if (census.FlagOff < MarshalTuning.HoldMinFlagOff)
            return;
        var company = census.Company;
        var proof = MarshalHoldLineRules.StrikeProof(census.StruckSettled, census.StruckFlagOn);
        if (proof == HoldProof.Pending)
            return;
        var detail = "mission=" + _key + " company=" + Count(company) + " struck=" + Count(census.StruckSettled) + " flagOn=" + Count(census.StruckFlagOn);
        if (proof == HoldProof.Pass && _strikePassed.Add(company))
            _outcomes.Pass(MarshalChecks.StrikeId, _key + ":" + Count(company), detail);
        else if (proof == HoldProof.Fail && _strikeFailed.Add(company))
            _outcomes.Fail(MarshalChecks.StrikeId, _key + ":" + Count(company), detail);
    }

    private void JudgeSlot(HoldCensus census, double now)
    {
        var company = census.Company;
        if (_summaryDue.TryGetValue(-100 - company, out var due) && now < due)
            return;
        _summaryDue[-100 - company] = now + MarshalTuning.SlotSummarySeconds;
        var run = RunOf(company, now);
        var proof = MarshalHoldLineRules.SlotProof(census.Order, census.Qualified, census.Anchored, census.WoundedEver, run.WindowSteered, run.WindowCloser, run.WindowFarther);
        WriteSlotLine(census, run);
        run.EndWindow();
        var detail = "mission=" + _key + " company=" + Count(company) + " qualified=" + Count(census.Qualified) + " far=" + Count(census.Far) + " order=" + census.Order;
        var key = _key + ":" + Count(company) + ":" + Count((int)(now / MarshalTuning.SlotSummarySeconds));
        if (proof == HoldProof.Pass)
        {
            _outcomes.Pass(MarshalChecks.HoldSlotId, key, detail);
        }
        else if (proof == HoldProof.Fail)
        {
            _slotFailed.Add(company);
            _outcomes.Fail(MarshalChecks.HoldSlotId, key, detail);
        }
    }

    // The open window of a company that stops being held, or of one still held when the mission ends: a fact on the line, with no verdict.
    private void WriteSlotLine(int company)
    {
        if (_holdLatest.TryGetValue(company, out var census) && _holdRuns.TryGetValue(company, out var run))
            WriteSlotLine(census, run);
    }

    private void WriteSlotLine(HoldCensus census, HoldRun run) =>
        _logger.LogInfo("[Marshal] slot company=" + Count(census.Company) + " units=" + Count(census.Units) + " qualified=" + Count(census.Qualified) + " flagOff=" + Count(census.FlagOff)
            + " anchored=" + Count(census.Anchored) + " far=" + Count(census.Far) + " max=" + Seconds(census.SlotMax) + " mean=" + Seconds(census.SlotMean)
            + " order=" + census.Order + " wounded=" + Count(census.WoundedEver) + " struck=" + Count(census.Struck) + " why=" + (census.WhyNot.Length == 0 ? "none" : census.WhyNot)
            + " closer=" + Count(run.WindowCloser) + " farther=" + Count(run.WindowFarther) + " steered=" + Count(run.WindowSteered) + " loose=" + Count(run.WindowLoose));

    // The companies a click named; a key press has none, so the selection at the tick stands. Every press writes its field, so a click's list never outlives its press.
    private IReadOnlyList<int> Named(IReadOnlyList<int>? stored) => stored ?? _orders.SelectedCompanies();

    private void TakeCensus(double now)
    {
        _nextCensus = now + MarshalTuning.FireCensusSeconds;
        _latest.Clear();
        foreach (var census in _fire.Sweep(_ruled, _held, now))
            _latest[census.Company] = census;
    }

    // A company under AI control has no mode and no hold: nobody remains to give the word or press the key.
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
            _holdRuns.Remove(company);
            _holdLatest.Remove(company);
            _slotFailed.Remove(company);
            _outcomes.Skip(MarshalChecks.FireId, _key + ":lost-control:" + Count(company), "mission=" + _key + " reason=lost-control company=" + Count(company));
        }
    }

    private void Judge(int company, double now)
    {
        if (MarshalFireTable.ModeOf(company) != FireMode.Free)
            JudgeVolley(company, now);
        if (MarshalFireTable.HoldOf(company))
            JudgeHoldLine(company, now);
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

    private void JudgeHoldLine(int company, double now)
    {
        var run = RunOf(company, now);
        var control = ControlOf(company);
        if (control >= 0)
        {
            var verdict = run.Window.Observe(now, CensusOf(company).FlagOff, MarshalFireTable.BlowsOf(company), MarshalFireTable.BlowsOf(control));
            if (run.Window.BaselineTaken && run.Control < 0)
                run.Control = control;
            if (verdict != null)
                ReportHoldLine(company, run.Control, verdict.Value);
        }

        if (!_summaryDue.TryGetValue(-1 - company, out var due) || now >= due)
        {
            _summaryDue[-1 - company] = now + MarshalTuning.HoldSummarySeconds;
            WriteHoldLine(company, now);
        }
    }

    private HoldRun RunOf(int company, double now)
    {
        if (!_holdRuns.TryGetValue(company, out var run))
            _holdRuns[company] = run = new HoldRun(now, company);
        return run;
    }

    private int ControlOf(int company)
    {
        if (_holdRuns.TryGetValue(company, out var run) && run.Control >= 0)
            return run.Control;
        var best = -1;
        var most = 0;
        for (var other = 0; other < MarshalFireTable.Companies; other++)
        {
            if (other == company || MarshalFireTable.HoldOf(other))
                continue;
            var blows = MarshalFireTable.BlowsOf(other);
            if (blows > most)
            {
                best = other;
                most = blows;
            }
        }

        return best;
    }

    private void ReportHoldLine(int company, int control, HoldVerdict verdict)
    {
        LastHoldLine = verdict;
        var detail = "mission=" + _key + " company=" + Count(company) + " flagOff=" + Count(verdict.FlagOff) + " blows=" + Count(verdict.HeldBlows)
            + " control=" + Count(control) + " controlBlows=" + Count(verdict.ControlBlows) + " seconds=" + Seconds(verdict.Seconds);
        var key = _key + ":" + Count(company);
        if (verdict.Proof == HoldProof.Pass)
            _outcomes.Pass(MarshalChecks.HoldLineId, key, detail);
        else if (verdict.Proof == HoldProof.Fail)
            _outcomes.Fail(MarshalChecks.HoldLineId, key, detail);
        else
            _outcomes.Skip(MarshalChecks.HoldLineId, key + ":" + verdict.Proof, detail + " reason=" + (verdict.Proof == HoldProof.NoControl ? "no-control" : "flag-not-off"));
    }

    private void WriteHoldLine(int company, double now)
    {
        var census = CensusOf(company);
        var units = census.Drawing + census.Holding + census.Idle + census.Aside;
        var control = ControlOf(company);
        _holdRuns.TryGetValue(company, out var run);
        int Since(int other) => MarshalFireTable.BlowsOf(other) - (run?.BlowsAtStart(other) ?? 0);
        _logger.LogInfo("[Marshal] hold company=" + Count(company) + " units=" + Count(units) + " flagOff=" + Count(census.FlagOff)
            + " blows=" + Count(Since(company)) + " control=" + Count(control)
            + " controlBlows=" + Count(control < 0 ? 0 : Since(control)) + " seconds=" + Seconds(now - (run?.Began ?? now)));
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

    // The Massed check is the volley that followed a Massed word, so it fails when the share rule fired and the archers did not loose.
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

    private sealed class HoldRun
    {
        private readonly int[] _blowsAtStart = new int[MarshalFireTable.Companies];

        // Every company's blows at the moment the hold went on, so the line's blows and the control's are counted from the same instant.
        internal HoldRun(double began, int company)
        {
            Began = began;
            for (var other = 0; other < _blowsAtStart.Length; other++)
                _blowsAtStart[other] = MarshalFireTable.BlowsOf(other);
            Window.Begin(began);
        }

        internal double Began { get; }

        internal int BlowsAtStart(int company) => company >= 0 && company < _blowsAtStart.Length ? _blowsAtStart[company] : 0;

        internal MarshalHoldWindow Window { get; } = new();

        internal int Control { get; set; } = -1;

        /// <summary>The most wounded soldiers any hold pass has counted, so a wounded soldier who left the count still stands for a line that was struck.</summary>
        internal int WoundedEver { get; set; }

        internal int Closer { get; private set; }

        internal int Farther { get; private set; }

        internal int Steered { get; private set; }

        internal int Loose { get; private set; }

        internal int WindowCloser { get; private set; }

        internal int WindowFarther { get; private set; }

        internal int WindowSteered { get; private set; }

        internal int WindowLoose { get; private set; }

        internal void NoteTrend(int closer, int farther, int steered, int loose)
        {
            Closer += closer;
            Farther += farther;
            Steered += steered;
            Loose += loose;
            WindowCloser += closer;
            WindowFarther += farther;
            WindowSteered += steered;
            WindowLoose += loose;
        }

        internal void EndWindow()
        {
            WindowCloser = 0;
            WindowFarther = 0;
            WindowSteered = 0;
            WindowLoose = 0;
        }
    }
}

/// <summary>What a Massed word was given on: the soldiers holding, the ones eligible, the ones whose stale draw was left out, and when.</summary>
internal readonly record struct MassedWord(int Holding, int Eligible, int StaleDrawing, double Time);
