using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// Every key the English source declares must exist as a row in all twelve language files.
///
/// Registration alone does not make a string translatable. The translator substitutes BY ID —
/// <c>write_back</c> in <c>tools/translate_with_claude.py</c> rewrites the <c>text</c> of an
/// existing <c>&lt;string id="KEY"&gt;</c> and has nowhere to put a key the file does not declare —
/// so a language file missing the row silently falls back to English forever, and a translation
/// paid for lands nowhere.
///
/// Nothing pinned this before. <c>LanguageDataXmlTests</c> checks the shape of the tree (dirs
/// exist, eleven file refs each, well-formed XML, every row has id + text) but never compares an
/// id set against the English source, which is how three separate gaps accumulated unseen:
/// 317 keys never registered at all (#434), 96 character-creation narrative rows registered but
/// never propagated (#432), and one late <c>taom_res_desertion</c> row. All three presented
/// identically — perfect English, eleven silently untranslated languages, green suite.
///
/// Deliberately a PRESENCE check, not an "is actually translated" check. Some rows legitimately
/// carry English: proper nouns, and the four vanilla-derived strings with nested gender
/// conditionals that fail placeholder validation on every uncached language and fall back by
/// design (see <c>docs/localization/TRANSLATOR_GUIDE.md</c>). Asserting difference-from-English
/// would report those forever, and a check that reports mostly noise gets ignored — the same
/// failure mode that let #434 sit for two years.
/// </summary>
[TestClass]
public class LanguageFileCoverageTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, "Main", "_Module", "ModuleData");

    private static string LanguagesPath => Path.Combine(ModuleDataPath, "Languages");

    /// <summary>Matches a row's own embedded key: <c>text="{=taom_foo}Foo"</c>.</summary>
    private static readonly Regex EmbeddedPrefix =
        new Regex(@"^\{=([^}]+)\}", RegexOptions.Compiled);

    private static readonly string[] SupportedLanguageDirs =
        { "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    /// <summary>
    /// The key a row is addressed by. Mirrors <c>_parse_string_xml(strip_keys=True)</c> in the
    /// translator exactly: the <c>{=KEY}</c> prefix embedded in the text when there is one,
    /// otherwise the bare <c>id</c> (which is how <c>taom_wotr_strings.xml</c> is authored).
    /// Getting this wrong in either direction would make the test compare two different universes.
    /// </summary>
    private static string TranslationKey(XElement row)
    {
        var match = EmbeddedPrefix.Match((string)row.Attribute("text") ?? string.Empty);
        return match.Success ? match.Groups[1].Value : (string)row.Attribute("id") ?? string.Empty;
    }

    private static IEnumerable<XElement> StringRows(string file)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Load(file);
        }
        catch (System.Xml.XmlException)
        {
            yield break;   // not our file to validate — LanguageDataXmlTests owns well-formedness
        }
        foreach (var row in doc.Root.DescendantsAndSelf()
                     .Where(e => e.Name.LocalName == "string"))
        {
            yield return row;
        }
    }

    /// <summary>Every key the English side declares, across every strings XML outside Languages/.</summary>
    private static HashSet<string> LoadEnglishKeys()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories))
        {
            if (file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Contains("Languages"))
            {
                continue;
            }
            foreach (var row in StringRows(file))
            {
                var key = TranslationKey(row);
                // "!" and "*" are engine sentinels, not translatable rows.
                if (key.Length > 0 && key != "!" && key != "*")
                {
                    keys.Add(key);
                }
            }
        }

        // A parse regression here would empty the expected set and make this test pass while
        // covering nothing.
        Assert.IsTrue(keys.Count > 5000,
            $"Only {keys.Count} English keys parsed from {ModuleDataPath} — the parse is broken, " +
            "and a coverage test that expects nothing would pass for the wrong reason.");
        return keys;
    }

    /// <summary>
    /// Every id one language declares, unioned across its files. Union rather than per-file,
    /// because the engine folds all language XMLs into a single dictionary — which file a row
    /// sits in is an organisational choice, and pinning it here would fail a legitimate move.
    /// </summary>
    private static HashSet<string> LoadLanguageKeys(string languageDir)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(languageDir, "std_taom_*.xml"))
        {
            foreach (var row in StringRows(file))
            {
                var id = (string)row.Attribute("id");
                if (!string.IsNullOrEmpty(id))
                {
                    keys.Add(id);
                }
            }
        }
        return keys;
    }

    [TestMethod]
    public void EveryLanguage_DeclaresARowForEveryEnglishKey()
    {
        var english = LoadEnglishKeys();
        var failures = new List<string>();

        foreach (var lang in SupportedLanguageDirs)
        {
            var dir = Path.Combine(LanguagesPath, lang);
            Assert.IsTrue(Directory.Exists(dir), $"Language directory missing: {dir}");

            var missing = english.Except(LoadLanguageKeys(dir), StringComparer.Ordinal)
                                 .OrderBy(k => k, StringComparer.Ordinal)
                                 .ToList();
            if (missing.Count == 0)
            {
                continue;
            }

            var shown = string.Join(", ", missing.Take(8));
            var more = missing.Count > 8 ? $" (+{missing.Count - 8} more)" : string.Empty;
            failures.Add($"  {lang}: {missing.Count} of {english.Count} keys have no row — {shown}{more}");
        }

        Assert.AreEqual(0, failures.Count,
            "Language files are missing rows for keys the English source declares. Those strings " +
            "render English in-game no matter what a translator does, because the translator " +
            "substitutes by id and has nowhere to write.\n" +
            string.Join("\n", failures) +
            "\n\nFix: python tools/translate_with_claude.py --lang <L> --module TAOM --sync-ids --apply");
    }
}
