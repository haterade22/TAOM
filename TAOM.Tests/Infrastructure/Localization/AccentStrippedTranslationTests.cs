using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// Catches a language row that is the ENGLISH text with its diacritics stripped.
///
/// Why this exists: such a row is permanently invisible to the pipeline. The translator only
/// returns rows where <c>cur_text == eng_text</c> (tools/translate_with_claude.py:298-308), so a
/// near-miss is never staged again, and <c>--sync-ids</c> does not help because the key is present
/// rather than missing. The row ships as mangled English in that language forever, with no
/// diagnostic anywhere.
///
/// It happened on 2026-08-29: four Rohan biographies were written into all twelve files before the
/// diacritic pass ran over the English, leaving "Grima Grimmoding" against a registry that had
/// moved to "Gríma Grimmóding". LanguageFileCoverageTests stayed green throughout, correctly, since
/// it is a presence check and the rows were present.
///
/// This is deliberately narrow. A genuine translation is never a diacritic-fold of its English, so
/// there is no noise to trade off. It says nothing about whether a row is translated well, or at
/// all: an untranslated row holding the English verbatim is the pipeline's own "please translate
/// me" signal and passes here.
/// </summary>
[TestClass]
public class AccentStrippedTranslationTests
{
    /// <summary>
    /// Rows that already shipped in this state before the check existed. Each is a real, fixable
    /// defect; the list exists so the check can be added without a repair pass, and it must only
    /// ever shrink. Nothing may be added to it.
    /// </summary>
    private static readonly HashSet<string> Baseline = new(StringComparer.Ordinal)
    {
        "TAOM_rhun",
        "TAOM_rhun_short",
        "TAOM_sturgia_culture",
        "aom_harad_female_name_32",
        "aom_harad_male_name_18",
        "aom_harad_male_name_21",
        "aom_harad_male_name_7",
    };

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }

    private static string Fold(string s)
    {
        var expanded = s
            .Replace("Æ", "Ae").Replace("æ", "ae")
            .Replace("Ð", "D").Replace("ð", "d")
            .Replace("Þ", "Th").Replace("þ", "th")
            .Replace("Ø", "O").Replace("ø", "o");

        var decomposed = expanded.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString();
    }

    private static Dictionary<string, string> Rows(string path)
    {
        var text = File.ReadAllText(path);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(text, @"<string id=""(?<id>[^""]+)"" text=""(?<text>[^""]*)"" />"))
            map[m.Groups["id"].Value] = m.Groups["text"].Value;
        return map;
    }

    [TestMethod]
    public void NoLanguageRowIsTheEnglishWithItsDiacriticsStripped()
    {
        var root = FindRepoRoot();
        var moduleData = Path.Combine(root, "Main", "_Module", "ModuleData");

        var english = Rows(Path.Combine(moduleData, "taom_xslt_strings.xml"))
            .ToDictionary(kv => kv.Key, kv => Regex.Replace(kv.Value, @"^\{=[^}]*\}", ""), StringComparer.Ordinal);
        Assert.IsTrue(english.Count > 1000, $"parsed only {english.Count} English rows; the registry shape changed");

        var offenders = new List<string>();
        var stillBaselined = new HashSet<string>(StringComparer.Ordinal);

        foreach (var dir in Directory.GetDirectories(Path.Combine(moduleData, "Languages")).OrderBy(d => d, StringComparer.Ordinal))
        {
            var file = Directory.GetFiles(dir, "std_taom_xslt_strings_*.xml").FirstOrDefault();
            if (file == null) continue;
            var lang = Path.GetFileName(dir);

            foreach (var kv in Rows(file))
            {
                if (!english.TryGetValue(kv.Key, out var eng)) continue;
                if (kv.Value == eng) continue;                      // untranslated, and staged as such
                if (!string.Equals(Fold(kv.Value), Fold(eng), StringComparison.Ordinal)) continue;

                if (Baseline.Contains(kv.Key)) { stillBaselined.Add(kv.Key); continue; }
                offenders.Add($"  {lang} {kv.Key}: \"{kv.Value}\" against English \"{eng}\"");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "These language rows are the English text with its diacritics stripped. The translator's\n" +
            "discovery gate is cur_text == eng_text, so it will never see them again and they ship as\n" +
            "mangled English forever. Copy the registry's bytes into the row, do not retype it.\n" +
            string.Join("\n", offenders));

        var repaired = Baseline.Except(stillBaselined).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, repaired.Count,
            "These keys are excused in Baseline but no longer need excusing. Remove them; the list\n" +
            "may only ever shrink.\n  " + string.Join("\n  ", repaired));
    }
}
