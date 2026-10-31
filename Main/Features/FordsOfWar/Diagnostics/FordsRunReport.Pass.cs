using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>A verdict pass: judge the evidence, write the JSON report, log the 16 rows and a total.</summary>
internal sealed partial class FordsRunReport
{
    private static readonly string BlowsLegend = "Towers[id].Blows is [class, reason]; classes " + string.Join(",", Enum.GetNames(typeof(TowerBlowClass)))
        + "; reasons " + string.Join(",", Enum.GetNames(typeof(TowerDamageReason)));

    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        Converters = { new StringEnumConverter() },
    };

    private void Pass(FordsRunEvidence run, bool final, string reason)
    {
        try
        {
            System.Collections.Generic.IReadOnlyList<FordsVerdictRow> rows;
            string json;
            lock (_sync)
            {
                rows = FordsVerdicts.Judge(Session, run);
                if (final)
                    run.FinalPasses++;
                json = Serialize(run, rows, final, reason);
            }

            string? error = null;
            var wrote = _file != null && _file.TryWrite(run.Run, json, out error);
            if (_file != null && !wrote)
                Warn("Run", "report write failed: " + FordsLogLine.Q(error));

            LogRows(run, rows, final, _file != null ? _file.PathFor(run.Run) : "none");
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[FordsOfWar][Report] a verdict pass faulted: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private string Serialize(FordsRunEvidence run, System.Collections.Generic.IReadOnlyList<FordsVerdictRow> rows, bool final, string reason)
    {
        var document = new
        {
            schema = 2,
            run = run.Run,
            final,
            reason,
            written = DateTime.Now.ToString("o"),
            log = string.IsNullOrEmpty(Logger.LogFilePath) ? "none" : System.IO.Path.GetFullPath(Logger.LogFilePath!),
            taom = Informational(),
            blowsLegend = BlowsLegend,
            session = Session,
            evidence = run,
            verdicts = rows,
        };
        return JsonConvert.SerializeObject(document, JsonSettings);
    }

    private void LogRows(FordsRunEvidence run, System.Collections.Generic.IReadOnlyList<FordsVerdictRow> rows, bool final, string reportPath)
    {
        var f = final ? 1 : 0;
        int pass = 0, fail = 0, partial = 0, notSeen = 0;
        foreach (var row in rows)
        {
            switch (row.Result)
            {
                case FordsResult.Pass: pass++; break;
                case FordsResult.Fail: fail++; break;
                case FordsResult.Partial: partial++; break;
                default: notSeen++; break;
            }

            var parts = string.Join(" ", System.Linq.Enumerable.Select(row.Parts,
                p => p.Name + "=" + FordsResults.Word(p.Result) + (p.Numbers == "" ? "" : "(" + p.Numbers + ")")));
            Info("Verdict", "final=" + f + " check=" + row.Check.ToString("00") + " result=" + FordsResults.Word(row.Result)
                + " | " + parts + " | info: " + (row.Info == "" ? "-" : row.Info) + " | rule: " + row.Rule + " | human: " + (row.HumanOnly == "" ? "-" : row.HumanOnly));
        }

        Info("Verdict", "final=" + f + " total pass=" + pass + " fail=" + fail + " partial=" + partial + " notseen=" + notSeen
            + " report=" + FordsLogLine.Q(reportPath));
    }
}
