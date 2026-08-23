using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// Round-trip gate for generated localization registrations: for every key in scope, the row
/// registered in the strings XML must EQUAL the longest inline default found in code.
///
/// <para>Exists because the camps-port registration script truncated 20 defaults at their first
/// <c>{PLACEHOLDER}</c> and the truncated text shipped into all 12 language files SILENTLY:
/// English still rendered from the inline default, the key count matched, and nothing compared
/// text against text. A generator's output is unverified until something diffs it against its
/// input (RCA `rca-yotthani-camps-2026-08-23.md` Class 4).</para>
///
/// <para>Scoped to the camps-port prefixes (taom_fcamp_, renamed from taom_fc_ which was
/// FieldCommission's prefix all along) to avoid re-litigating older keys registered under
/// earlier conventions; extend the prefix list when new generated batches land. Registration
/// XMLs are excluded from the CODE-default scan: a registration file must never vouch for
/// another registration file (that blind spot passed a double-escaped row straight through
/// this gate, review round B).</para>
/// </summary>
[TestClass]
public class RegisteredDefaultRoundTripTests
{
    private static readonly string[] Prefixes = { "taom_sl_", "taom_fcamp_", "taom_rf_" };

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static readonly Regex CodeDefault = new Regex(
        "\\{=(taom_(?:sl|fcamp|rf)_[a-z_0-9]+)\\}([^\"]*)\"", RegexOptions.Compiled);

    private static readonly Regex RegisteredRow = new Regex(
        "<string id=\"(taom_(?:sl|fcamp|rf)_[a-z_0-9]+)\" text=\"\\{=\\1\\}([^\"]*)\" />", RegexOptions.Compiled);

    [TestMethod]
    public void EveryRegisteredDefault_MatchesTheLongestInlineCodeDefault()
    {
        var code = new Dictionary<string, string>(StringComparer.Ordinal);
        var mainDir = Path.Combine(RepoRoot, "Main");
        foreach (var file in Directory.EnumerateFiles(mainDir, "*.*", SearchOption.AllDirectories))
        {
            if (!file.EndsWith(".cs") && !file.EndsWith(".xml"))
                continue;
            if (file.Contains(@"\bin\") || file.Contains(@"\obj\") ||
                file.Contains(@"\Languages\"))
                continue;
            // Registration XMLs never count as code: one registration file vouching for another
            // is how a double-escaped row survived this gate (review round B).
            var name = Path.GetFileName(file);
            if (name == "global_strings.xml" || name.EndsWith("_strings.xml"))
                continue;

            foreach (Match m in CodeDefault.Matches(File.ReadAllText(file)))
            {
                var key = m.Groups[1].Value;
                var text = Unescape(m.Groups[2].Value);
                if (!code.TryGetValue(key, out var existing) || text.Length > existing.Length)
                    code[key] = text;
            }
        }

        Assert.IsTrue(code.Count > 0, "No in-scope inline defaults found; the scan glob broke.");

        var stringsXml = File.ReadAllText(
            Path.Combine(RepoRoot, @"Main\_Module\ModuleData\taom_module_strings.xml"));
        var registered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in RegisteredRow.Matches(stringsXml))
            registered[m.Groups[1].Value] = Unescape(m.Groups[2].Value);

        var problems = new List<string>();
        foreach (var pair in code.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!registered.TryGetValue(pair.Key, out var row))
            {
                problems.Add(pair.Key + ": not registered at all");
                continue;
            }

            if (!string.Equals(row, pair.Value, StringComparison.Ordinal))
                problems.Add(pair.Key + ": registered \"" + row + "\" != code \"" + pair.Value + "\"");
        }

        Assert.AreEqual(0, problems.Count,
            "Registered defaults diverge from code defaults (a lossy or stale registration " +
            "generator; translators translate the registered text, so drift here ships wrong " +
            "translations):\n" + string.Join("\n", problems));
    }

    private static string Unescape(string s)
    {
        // Numeric character references first: the &#x27; family is what the double-escape
        // defect rode in on, and Regex handles both hex and decimal forms.
        s = Regex.Replace(s, "&#x([0-9A-Fa-f]+);", m =>
            ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
        s = Regex.Replace(s, "&#([0-9]+);", m =>
            ((char)int.Parse(m.Groups[1].Value)).ToString());
        return s
            .Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
    }
}
