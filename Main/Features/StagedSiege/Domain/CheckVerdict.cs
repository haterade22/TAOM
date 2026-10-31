using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TAOM.Features.StagedSiege.Domain;

/// <summary>The answer of one in-game check in a run report. Printed as PASS, FAIL or NOT SEEN.</summary>
public enum CheckVerdict
{
    Pass,
    Fail,
    NotSeen,
}

/// <summary>One check of a run report: a stable lower snake case id, the verdict and the numbers behind it.</summary>
internal readonly struct ReportCheck
{
    public ReportCheck(string id, CheckVerdict verdict, string evidence)
    {
        Id = id;
        Verdict = verdict;
        Evidence = evidence;
    }

    public string Id { get; }
    public CheckVerdict Verdict { get; }
    public string Evidence { get; }
}

/// <summary>The number and line formats the walls and hall run reports share. Invariant culture throughout.</summary>
internal static class RunReportFormat
{
    public static string Num(float value, string format = "F1") => value.ToString(format, CultureInfo.InvariantCulture);

    public static string Num(double value, string format = "F2") => value.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>Seconds with the unit, or <c>none</c> for an absent value.</summary>
    public static string Secs(float? value) => value.HasValue ? Num(value.Value) + " s" : "none";

    public static string VerdictText(CheckVerdict verdict) =>
        verdict == CheckVerdict.Pass ? "PASS" : verdict == CheckVerdict.Fail ? "FAIL" : "NOT SEEN";

    public static string CheckLine(ReportCheck check) => "CHECK " + check.Id + " " + VerdictText(check.Verdict) + ": " + check.Evidence;

    public static bool IsFinite(float value) => !(float.IsNaN(value) || float.IsInfinity(value));

    public static bool IsFinite(double value) => !(double.IsNaN(value) || double.IsInfinity(value));

    /// <summary>The cost of the reconcile passes: count, slowest and mean in milliseconds, kept without a list.</summary>
    public sealed class PassCost
    {
        private double _sum;
        private int _measured;

        public int Passes { get; set; }
        public double Slowest { get; private set; }

        /// <summary>A non-finite cost (a clock that went wrong) is dropped rather than poisoning the mean.</summary>
        public void Add(double milliseconds)
        {
            if (!IsFinite(milliseconds) || milliseconds < 0d)
                return;
            _measured++;
            _sum += milliseconds;
            if (milliseconds > Slowest)
                Slowest = milliseconds;
        }

        public string Describe() => _measured == 0
            ? $"passes {Passes}, slowest none, mean none"
            : $"passes {Passes}, slowest {Num(Slowest)} ms, mean {Num(_sum / _measured)} ms";
    }

    public static string Summary(IReadOnlyList<ReportCheck> checks, PassCost cost, string fault)
    {
        var pass = 0;
        var fail = new List<string>();
        var notSeen = new List<string>();
        foreach (var check in checks)
        {
            if (check.Verdict == CheckVerdict.Pass)
                pass++;
            else if (check.Verdict == CheckVerdict.Fail)
                fail.Add(check.Id);
            else
                notSeen.Add(check.Id);
        }

        var sb = new StringBuilder("REPORT summary: ");
        sb.Append(checks.Count).Append(" checks, ").Append(pass).Append(" PASS, ");
        sb.Append(fail.Count).Append(" FAIL (").Append(Ids(fail)).Append("), ");
        sb.Append(notSeen.Count).Append(" NOT SEEN (").Append(Ids(notSeen)).Append("); ");
        sb.Append(cost.Describe()).Append("; fault ").Append(fault);
        return sb.ToString();
    }

    private static string Ids(List<string> ids) => ids.Count == 0 ? "none" : string.Join(", ", ids);
}
