using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MarshalsWord.Hooks;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>
/// The two proofs of build-spec 9.1 and 9.3, which no shipped mission exercises: the plain engine square must give every soldier a slot, and a company with one
/// rider must read the foot soldiers' spacing limits. Both write their line from the mission tick, at the slow pass.
/// </summary>
internal sealed class MarshalProofWatch
{
    private readonly IMarshalOrderAdapter _orders;
    private readonly IOutcomeLog _outcomes;
    private readonly IModLogger _logger;
    private readonly string _key;
    private readonly Dictionary<int, (double Time, int Units)> _lastLine = new();
    private readonly List<int> _broken = new();
    private double _nextPass = double.NegativeInfinity;
    private bool _spacingDone;

    internal MarshalProofWatch(IMarshalOrderAdapter orders, IOutcomeLog outcomes, IModLogger logger, string missionKey)
    {
        _orders = orders;
        _outcomes = outcomes;
        _logger = logger;
        _key = missionKey;
    }

    /// <summary>The company a square line was last written for, or none; the field trial reads it.</summary>
    internal SquareRead? LatestSquare { get; private set; }

    /// <summary>The spacing verdict that was written for the mission, or null while none has been.</summary>
    internal SpacingReading? SpacingVerdict { get; private set; }

    internal void Pass()
    {
        var now = _orders.Now;
        if (now < _nextPass)
            return;
        _nextPass = now + MarshalTuning.SlowPassSeconds;
        var squares = _orders.ReadSquares();
        foreach (var square in squares)
            Square(square, now);
        ForgetCompaniesThatLeftTheSquare(squares);
        if (!_spacingDone)
            Spacing();
    }

    internal void OnPlayerTeamChanged()
    {
        _lastLine.Clear();
        LatestSquare = null;
    }

    private void ForgetCompaniesThatLeftTheSquare(IReadOnlyList<SquareRead> squares)
    {
        if (_lastLine.Count == 0)
            return;
        _broken.Clear();
        foreach (var company in _lastLine.Keys)
        {
            if (!StandsInSquare(squares, company))
                _broken.Add(company);
        }

        foreach (var company in _broken)
            _lastLine.Remove(company);
    }

    private static bool StandsInSquare(IReadOnlyList<SquareRead> squares, int company)
    {
        foreach (var square in squares)
        {
            if (square.Company == company)
                return true;
        }

        return false;
    }

    private void Square(SquareRead read, double now)
    {
        var last = _lastLine.TryGetValue(read.Company, out var seen) ? seen : (Time: double.NegativeInfinity, Units: -1);
        if (!MarshalSquareRules.ProofLineDue(now, last.Time, last.Units, read.Units))
            return;
        _lastLine[read.Company] = (now, read.Units);
        LatestSquare = read;
        var facts = "company=" + Whole(read.Company) + " side=" + read.Side.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
            + " files=" + Whole(read.SideUnits) + " ranks=" + Whole(read.Ranks) + " units=" + Whole(read.Units) + " placed=" + Whole(read.Placed)
            + " laidOut=" + (read.LaidOut ? "1" : "0");
        var detail = "mission=" + _key + " " + facts;
        _logger.LogInfo("[Marshal] square " + facts);
        if (MarshalSquareRules.ProofPasses(read.Placed, read.Units))
            _outcomes.Pass(MarshalChecks.SquareId, _key + ":" + Whole(read.Company), detail);
        else
            _outcomes.Fail(MarshalChecks.SquareId, _key + ":" + Whole(read.Company), detail);
    }

    // One company with a rider, judged once per mission: the limits it answers against the two values it could answer.
    private void Spacing()
    {
        var read = _orders.ReadSpacing();
        if (read == null)
            return;
        _spacingDone = true;
        var why = MarshalSpacingPatch.WhyNot;
        var key = _key + ":" + Whole(read.Value.Company);
        var facts = "company=" + Whole(read.Value.Company) + " units=" + Whole(read.Value.Units) + " riders=" + Whole(read.Value.Riders)
            + " minDistance=" + Num(read.Value.Measured.MinDistance) + " maxDistance=" + Num(read.Value.Measured.MaxDistance)
            + " minInterval=" + Num(read.Value.Measured.MinInterval) + " maxInterval=" + Num(read.Value.Measured.MaxInterval)
            + " infantry=" + Limits(read.Value.Infantry) + " cavalry=" + Limits(read.Value.Cavalry);
        var line = "mission=" + _key + " " + facts;
        if (why.Length > 0)
        {
            _logger.LogInfo("[Marshal] spacing " + facts + " repair=" + why);
            _outcomes.Skip(MarshalChecks.SpacingId, key + ":" + why, line + " reason=" + why);
            return;
        }

        var reading = MarshalSpacingRules.Read(read.Value.Measured, read.Value.Infantry, read.Value.Cavalry);
        SpacingVerdict = reading;
        _logger.LogInfo("[Marshal] spacing " + facts + " repair=applied reads=" + reading);
        if (reading == SpacingReading.Infantry)
            _outcomes.Pass(MarshalChecks.SpacingId, key, line + " reads=Infantry");
        else if (reading == SpacingReading.NoTest)
            _outcomes.Skip(MarshalChecks.SpacingId, key + ":no-test", line + " reason=no-test");
        else
            _outcomes.Fail(MarshalChecks.SpacingId, key, line + " reads=" + reading);
    }

    private static string Whole(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Num(float value) => value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static string Limits(SpacingLimits l) => Num(l.MinDistance) + "/" + Num(l.MaxDistance) + "/" + Num(l.MinInterval) + "/" + Num(l.MaxInterval);
}
