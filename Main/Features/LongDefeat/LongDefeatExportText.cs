using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TAOM.Features.LongDefeat;

/// <summary>
/// The part of the <c>taom.ld_export</c> reply that lets the maintainer check his recording and paste it: the count of records per kind and per tower, the optional kinds
/// still unrecorded, and one paste-ready block per field of the fortress row that a record fills (keep, muster, sally, towers, volley, armoury, yard), each under one instruction line.
/// Plain English: console output is not localized (docs/features/dev-console.md).
/// </summary>
internal static class LongDefeatExportText
{
    // The kinds listed as "no record yet" here. A missing keep or muster is already named by the reply's first sentence, and a sally is optional and never listed. The armoury and the yard are Phase 4 places, listed here so the recording session does not miss them.
    private static readonly string[] OptionalKinds =
    {
        LongDefeatRecorder.PostKind, LongDefeatRecorder.TowerVolleyKind, LongDefeatRecorder.VolleyKind, LongDefeatRecorder.ArmouryKind, LongDefeatRecorder.YardKind,
    };

    private static readonly string[] SingleKinds =
    {
        LongDefeatRecorder.KeepKind, LongDefeatRecorder.MusterFootKind, LongDefeatRecorder.MusterArchersKind, LongDefeatRecorder.SallyKind,
        LongDefeatRecorder.ArmouryKind, LongDefeatRecorder.YardKind,
    };

    /// <summary>The lines after the reply's first sentence: counts, then the unrecorded optional kinds, then the blocks. It starts with a newline, to follow the reply's first sentence.</summary>
    internal static string Details(LongDefeatExport export, string scene)
    {
        var sb = new StringBuilder();
        sb.Append('\n').Append(CountsLine(export, scene));
        var absent = OptionalKinds.Where(k => export.CountOf(k) == 0).ToList();
        if (absent.Count > 0)
            sb.Append("\nNo record yet for: ").Append(string.Join(", ", absent)).Append('.');
        sb.Append(Blocks(export));
        return sb.ToString();
    }

    private static string CountsLine(LongDefeatExport export, string scene)
    {
        var parts = new List<string>();
        foreach (var kind in LongDefeatRecorder.AllKinds)
        {
            var count = export.CountOf(kind);
            if (count == 0)
                continue;
            if (LongDefeatRecorder.IsTowerKind(kind))
            {
                var per = export.Towers.Select(t => (t.Id, N: kind == LongDefeatRecorder.PostKind ? t.Posts : t.TowerVolley)).Where(t => t.N > 0);
                parts.Add($"{kind} {count} ({string.Join(", ", per.Select(t => $"{t.Id} {t.N}"))})");
            }
            else if (count > 1 && SingleKinds.Contains(kind))
            {
                parts.Add($"{kind} {count} (the newest is used)");
            }
            else
            {
                parts.Add($"{kind} {count}");
            }
        }

        return parts.Count == 0 ? $"Nothing is recorded for {scene} yet." : $"Recorded for {scene}: {string.Join(", ", parts)}.";
    }

    private static string Blocks(LongDefeatExport export)
    {
        var root = JObject.Parse(export.Json);
        var sb = new StringBuilder();
        AddBlock(sb, root, "keep", "the keep point");
        AddBlock(sb, root, "muster", "the foot and archer muster points");
        AddBlock(sb, root, "sally", "the sally point");
        AddBlock(sb, root, "towers", $"{export.CountOf(LongDefeatRecorder.PostKind)} posts and {export.CountOf(LongDefeatRecorder.TowerVolleyKind)} tower volley spots in {export.Towers.Count} tower(s)");
        AddBlock(sb, root, "volley", $"{export.CountOf(LongDefeatRecorder.VolleyKind)} volley origins");
        AddBlock(sb, root, "armoury", "the armoury point, read from Phase 4");
        AddBlock(sb, root, "yard", "the engineer's yard point, read from Phase 4");
        if (sb.Length == 0)
            return "";
        return "\nEach block below is one field of the fortress row in fortresses.json: put a comma between fields, and a game restart loads them." + sb;
    }

    private static void AddBlock(StringBuilder sb, JObject root, string field, string what)
    {
        if (root[field] is not { } token)
            return;
        sb.Append("\n\nPaste as the \"").Append(field).Append("\" field of the fortress row in fortresses.json (").Append(what).Append("; it replaces an earlier \"").Append(field).Append("\"):\n");
        sb.Append(JsonConvert.ToString(field)).Append(": ");
        Render(token, 0, sb);
    }

    // Objects and arrays that hold other containers are spread over lines; a point stays on one line, so a tower of eight posts is ten lines and not seventy.
    private static void Render(JToken token, int indent, StringBuilder sb)
    {
        var pad = new string(' ', indent + 2);
        if (token is JObject o && o.Properties().Any(p => p.Value is JContainer))
        {
            sb.Append("{\n");
            var props = o.Properties().ToList();
            for (var i = 0; i < props.Count; i++)
            {
                sb.Append(pad).Append(JsonConvert.ToString(props[i].Name)).Append(": ");
                Render(props[i].Value, indent + 2, sb);
                sb.Append(i < props.Count - 1 ? ",\n" : "\n");
            }

            sb.Append(new string(' ', indent)).Append('}');
        }
        else if (token is JArray a)
        {
            sb.Append("[\n");
            for (var i = 0; i < a.Count; i++)
            {
                sb.Append(pad);
                Render(a[i], indent + 2, sb);
                sb.Append(i < a.Count - 1 ? ",\n" : "\n");
            }

            sb.Append(new string(' ', indent)).Append(']');
        }
        else
        {
            sb.Append(token.ToString(Formatting.None));
        }
    }
}
