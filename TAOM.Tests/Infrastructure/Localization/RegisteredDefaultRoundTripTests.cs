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
/// <para>Scoped to the camps-port prefixes to avoid re-litigating older keys registered under
/// earlier conventions; extend the prefix list when new generated batches land.</para>
/// </summary>
[TestClass]
public class RegisteredDefaultRoundTripTests
{
    private static readonly string[] Prefixes = { "taom_sl_", "taom_fc_", "taom_rf_" };

    private static string RepoRoot => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\.."));

    private static readonly Regex CodeDefault = new Regex(
        "\\{=(taom_(?:sl|fc|rf)_[a-z_0-9]+)\\}([^\"]*)\"", RegexOptions.Compiled);

    private static readonly Regex RegisteredRow = new Regex(
        "<string id=\"(taom_(?:sl|fc|rf)_[a-z_0-9]+)\" text=\"\\{=\\1\\}([^\"]*)\" />", RegexOptions.Compiled);

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
                file.Contains(@"\Languages\") || file.EndsWith("taom_module_strings.xml"))
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

    private static string Unescape(string s) => s
        .Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
}
