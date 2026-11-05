using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The hold the line key, the line's 3 Hz pass and the hold-line, hold-slot and hold-strike checks of the companies whose hold is on.</summary>
internal sealed class MarshalHoldLineDriver
{
    private readonly IMarshalOrderAdapter _orders;
    private readonly IMarshalFireAdapter _fire;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly Action<MarshalMessage> _say;
    private readonly string _key;
    private readonly Dictionary<int, HoldRun> _runs = new();
    private readonly HashSet<int> _strikePassed = new();
    private readonly HashSet<int> _strikeFailed = new();
    private volatile bool _pressed;
    private IReadOnlyList<int>? _pressedCompanies;
    private double _nextPass = double.NegativeInfinity;

    internal MarshalHoldLineDriver(IMarshalOrderAdapter orders, IMarshalFireAdapter fire, IOutcomeLog outcomes, IModLogger logger, Action<MarshalMessage> say, string missionKey)
    {
        _orders = orders;
        _fire = fire;
        _outcomes = outcomes;
        _logger = logger;
        _say = say;
        _key = missionKey;
    }

    /// <summary>The latest hold-the-line verdict of any company, or null; the field trial reads it, as the manifest reads the check line.</summary>
    internal HoldVerdict? LastVerdict { get; private set; }

    internal bool PassDue(double now) => now >= _nextPass;

    /// <summary>Steered soldiers of the held company who ended a hold read nearer their slot, summed since the hold went on.</summary>
    internal int SlotCloserOf(int company) => _runs.TryGetValue(company, out var run) ? run.Total.Closer : 0;

    /// <summary>Steered soldiers of the held company who ended a hold read farther from their slot, summed since the hold went on.</summary>
    internal int SlotFartherOf(int company) => _runs.TryGetValue(company, out var run) ? run.Total.Farther : 0;

    /// <summary>Steered hold reads of the held company, summed since the hold went on: the denominator of the two sides.</summary>
    internal int SlotSteeredOf(int company) => _runs.TryGetValue(company, out var run) ? run.Total.Steered : 0;

    /// <summary>The company's slot check has failed since the hold went on.</summary>
    internal bool SlotFailed(int company) => _runs.TryGetValue(company, out var run) && run.SlotFailed;

    /// <summary>The held company's latest line count, or an empty one before the first pass; the field trial reads it as the log's lines do.</summary>
    internal HoldCensus CensusOf(int company) => _runs.TryGetValue(company, out var run) ? run.Latest ?? default : default;

    /// <summary>The company's line has answered a wound with its attack flag, judged once at least one settled wound stood in the window.</summary>
    internal bool StrikePassed(int company) => _strikePassed.Contains(company);

    /// <summary>Key 545 was pressed, or its panel entry was clicked for <paramref name="companies"/>; stored for the next mission tick, because clearing an attack flag is agent state.</summary>
    internal void Press(IReadOnlyList<int>? companies = null)
    {
        _pressedCompanies = companies;
        _pressed = true;
    }

    /// <summary>Applies a stored press: every selected company under the player's control toggles, and a mixed selection converges on the first one's change.</summary>
    internal void ApplyPress(double now)
    {
        if (!_pressed)
            return;
        _pressed = false;
        if (!_orders.PlayerCommands)
            return;
        bool? target = null;
        var changed = 0;
        foreach (var company in _pressedCompanies ?? _orders.SelectedCompanies())
        {
            if (!_orders.IsControllable(company))
            {
                _outcomes.Skip(MarshalChecks.HoldId, _key + ":ai-controlled:" + Count(company), "mission=" + _key + " reason=ai-controlled company=" + Count(company));
                continue;
            }

            var on = target ??= !MarshalFireTable.HoldOf(company);
            MarshalFireTable.SetHold(company, on);
            End(company);
            if (on)
                _runs[company] = new HoldRun(now);

            changed++;
            var state = on ? "on" : "off";
            _outcomes.Pass(MarshalChecks.HoldId, _key + ":" + state + ":" + Count(company), "mission=" + _key + " hold=" + state + " company=" + Count(company));
        }

        if (changed > 0)
            _say(target == true ? MarshalMessage.HoldOn : MarshalMessage.HoldOff);
    }

    /// <summary>The 3 Hz pass: each held soldier's facts, flag and firing order follow the line, and the line's slot and strike back are judged.</summary>
    internal void Pass(double now, IReadOnlyList<int> held)
    {
        _nextPass = now + MarshalTuning.HoldReadSeconds;
        if (held.Count == 0)
            return;
        foreach (var raw in _fire.Hold(held, now))
        {
            var run = RunOf(raw.Company, now);
            // The most wounded soldiers any pass counted, so a line whose wounded have died still reads as struck.
            var census = raw with { WoundedEver = Math.Max(run.Latest?.WoundedEver ?? 0, raw.WoundedEver) };
            run.Latest = census;
            run.NoteTrend(census.Trend);
            JudgeStrike(census);
            JudgeSlot(census, run, now);
        }
    }

    /// <summary>The slow pass of a held company: the hold-line window, and its summary line every few seconds.</summary>
    internal void Judge(int company, double now, FireCensus census)
    {
        var run = RunOf(company, now);
        var control = ControlOf(company, run);
        if (control >= 0)
        {
            var baselineTaken = run.Window.BaselineTaken;
            var verdict = run.Window.Observe(now, census.FlagOff, MarshalFireTable.BlowsOf(company), MarshalFireTable.BlowsOf(control));
            if (run.Window.BaselineTaken && !baselineTaken)
                run.NoteBaseline();
            if (run.Window.BaselineTaken && run.Control < 0)
                run.Control = control;
            if (verdict != null)
            {
                ReportVerdict(company, run.Control, verdict.Value);
                if (verdict.Value.Proof == HoldProof.NoControl)
                    run.Control = run.BusiestSinceBaseline(company, run.Control);
            }
        }

        if (now < run.NextSummary)
            return;
        run.NextSummary = now + MarshalTuning.HoldSummarySeconds;
        WriteHoldLine(company, now, census);
    }

    /// <summary>The company's hold ended, by a press or by a loss of control: its open window is written, then its run is forgotten.</summary>
    internal void End(int company)
    {
        WriteSlotLine(company);
        _runs.Remove(company);
    }

    /// <summary>The player's team changed, so each company number names a formation of the old team.</summary>
    internal void OnPlayerTeamChanged()
    {
        foreach (var company in _runs.Keys)
            WriteSlotLine(company);
        Drop();
    }

    /// <summary>The finish of the mission: the hold line and the open window of a held company, as facts with no verdict.</summary>
    internal void Summarize(int company, double now, FireCensus census)
    {
        if (!MarshalFireTable.HoldOf(company))
            return;
        WriteHoldLine(company, now, census);
        WriteSlotLine(company);
    }

    internal void Drop()
    {
        _runs.Clear();
        _strikePassed.Clear();
        _strikeFailed.Clear();
    }

    private HoldRun RunOf(int company, double now)
    {
        if (!_runs.TryGetValue(company, out var run))
            _runs[company] = run = new HoldRun(now);
        return run;
    }

    private void JudgeStrike(HoldCensus census)
    {
        if (census.FlagOff < MarshalTuning.HoldMinFlagOff)
            return;
        var company = census.Company;
        var proof = MarshalHoldLineRules.StrikeProof(census.StruckSettled, census.StruckFlagOn);
        if (proof == HoldProof.Pending || !(proof == HoldProof.Pass ? _strikePassed : _strikeFailed).Add(company))
            return;
        var detail = "mission=" + _key + " company=" + Count(company) + " struck=" + Count(census.StruckSettled) + " flagOn=" + Count(census.StruckFlagOn);
        if (proof == HoldProof.Pass)
            _outcomes.Pass(MarshalChecks.StrikeId, _key + ":" + Count(company), detail);
        else
            _outcomes.Fail(MarshalChecks.StrikeId, _key + ":" + Count(company), detail);
    }

    private void JudgeSlot(HoldCensus census, HoldRun run, double now)
    {
        if (now < run.NextSlotWindow)
            return;
        run.NextSlotWindow = now + MarshalTuning.SlotSummarySeconds;
        var company = census.Company;
        var window = run.WindowCounts;
        var proof = MarshalHoldLineRules.SlotProof(census.Order, census.Qualified, census.Anchored, census.WoundedEver, window.Steered, window.Closer, window.Farther);
        WriteSlotLine(census, run);
        run.EndWindow();
        if (proof == HoldProof.Pending)
            return;
        var detail = "mission=" + _key + " company=" + Count(company) + " qualified=" + Count(census.Qualified) + " far=" + Count(census.Far) + " order=" + census.Order
            + " steered=" + Count(window.Steered) + " closer=" + Count(window.Closer) + " farther=" + Count(window.Farther);
        var key = _key + ":" + Count(company) + ":" + Count((int)(now / MarshalTuning.SlotSummarySeconds));
        if (proof == HoldProof.Pass)
        {
            _outcomes.Pass(MarshalChecks.HoldSlotId, key, detail);
        }
        else if (proof == HoldProof.Fail)
        {
            run.SlotFailed = true;
            _outcomes.Fail(MarshalChecks.HoldSlotId, key, detail);
        }
    }

    private void WriteSlotLine(int company)
    {
        if (_runs.TryGetValue(company, out var run) && run.Latest is { } census)
            WriteSlotLine(census, run);
    }

    private void WriteSlotLine(HoldCensus census, HoldRun run)
    {
        var window = run.WindowCounts;
        _logger.LogInfo("[Marshal] slot company=" + Count(census.Company) + " units=" + Count(census.Units) + " qualified=" + Count(census.Qualified) + " flagOff=" + Count(census.FlagOff)
            + " anchored=" + Count(census.Anchored) + " far=" + Count(census.Far) + " max=" + Seconds(census.SlotMax) + " mean=" + Seconds(census.SlotMean)
            + " order=" + census.Order + " wounded=" + Count(census.WoundedEver) + " struck=" + Count(census.Struck) + " why=" + (census.WhyNot.Length == 0 ? "none" : census.WhyNot)
            + " closer=" + Count(window.Closer) + " farther=" + Count(window.Farther) + " steered=" + Count(window.Steered) + " loose=" + Count(window.Loose));
    }

    private int ControlOf(int company, HoldRun? run)
    {
        if (run != null && run.Control >= 0)
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

    private void ReportVerdict(int company, int control, HoldVerdict verdict)
    {
        LastVerdict = verdict;
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

    private void WriteHoldLine(int company, double now, FireCensus census)
    {
        var units = census.Drawing + census.Holding + census.Idle + census.Aside;
        _runs.TryGetValue(company, out var run);
        var control = ControlOf(company, run);
        int Since(int other) => MarshalFireTable.BlowsOf(other) - (run?.BlowsAtStart(other) ?? 0);
        _logger.LogInfo("[Marshal] hold company=" + Count(company) + " units=" + Count(units) + " flagOff=" + Count(census.FlagOff)
            + " blows=" + Count(Since(company)) + " control=" + Count(control)
            + " controlBlows=" + Count(control < 0 ? 0 : Since(control)) + " seconds=" + Seconds(now - (run?.Began ?? now)));
    }

    private static string Count(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Seconds(double value) => value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class HoldRun
    {
        private readonly int[] _blowsAtStart = new int[MarshalFireTable.Companies];
        private readonly int[] _blowsAtBaseline = new int[MarshalFireTable.Companies];

        internal HoldRun(double began)
        {
            Began = began;
            for (var other = 0; other < _blowsAtStart.Length; other++)
                _blowsAtStart[other] = MarshalFireTable.BlowsOf(other);
            Window.Begin(began);
        }

        internal double Began { get; }

        internal MarshalHoldWindow Window { get; } = new();

        internal int Control { get; set; } = -1;

        internal HoldCensus? Latest { get; set; }

        internal bool SlotFailed { get; set; }

        internal double NextSlotWindow { get; set; } = double.NegativeInfinity;

        internal double NextSummary { get; set; } = double.NegativeInfinity;

        internal SlotCounts Total { get; private set; }

        internal SlotCounts WindowCounts { get; private set; }

        internal int BlowsAtStart(int company) => company >= 0 && company < _blowsAtStart.Length ? _blowsAtStart[company] : 0;

        internal void NoteBaseline()
        {
            for (var other = 0; other < _blowsAtBaseline.Length; other++)
                _blowsAtBaseline[other] = MarshalFireTable.BlowsOf(other);
        }

        /// <summary>The company not on hold, other than the one just judged quiet, that dealt the most blows since the baseline of the window just judged, or -1 when none dealt any.</summary>
        internal int BusiestSinceBaseline(int held, int quiet)
        {
            var best = -1;
            var most = 0;
            for (var other = 0; other < _blowsAtBaseline.Length; other++)
            {
                if (other == held || other == quiet || MarshalFireTable.HoldOf(other))
                    continue;
                var blows = MarshalFireTable.BlowsOf(other) - _blowsAtBaseline[other];
                if (blows > most)
                {
                    best = other;
                    most = blows;
                }
            }

            return best;
        }

        internal void NoteTrend(SlotCounts counts)
        {
            Total += counts;
            WindowCounts += counts;
        }

        internal void EndWindow() => WindowCounts = default;
    }
}
