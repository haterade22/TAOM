using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// One localization key, one text: the three ways a key reached players carrying the wrong
/// words in the 2026-09-25 sweep, each gated here.
///
/// <para>1. <b>Two rows of one language disagree.</b> The engine loads a language's files in the
/// order its <c>language_data.xml</c> lists them and stores each row by indexer assignment
/// (<c>LocalizedTextManager.DeserializeStrings</c>), so a repeated id never throws: the file
/// loaded LAST wins. A translator run seeded the 26 <c>taom_aso_*</c> keys of
/// <c>global_strings.xml</c> (23 of them also in <c>taom_module_strings.xml</c>) into every keybind
/// file; 22 of the copies differed from the curated module rows and, loading later, replaced them
/// (Italian players saw "Valle" for Dale). The English side of the same trap: two English sources
/// declaring one key with different defaults (gate 1b).</para>
///
/// <para>2. <b>An inline default and the registered English disagree.</b> English renders the inline
/// default; the other twelve languages translate the registered row. Two quest templates reused a
/// third's fallback keys with their own English, and two messenger lines differed from their
/// registration since the port, so every translation said something the English did not. The same
/// holds for data XML: a career text and a lord's title had drifted from their registration.</para>
///
/// <para>3. <b>Two characters share a name key.</b> Lindon's troops were copied from Rivendell's
/// with the keys left in place and six Umbar clans shared one key: English read each inline
/// default, every other language showed one translation for both. Scoped to the files
/// <c>tools/generate_name_localization_strings.py</c> registers names from, because a key there
/// is the translator's English by construction.</para>
/// </summary>
[TestClass]
public class LocalizationKeyConsistencyTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static string ModuleDataPath => Path.Combine(RepoRoot, "Main", "_Module", "ModuleData");

    private static string LanguagesPath => Path.Combine(ModuleDataPath, "Languages");

    private static readonly string[] SupportedLanguageDirs =
        { "BR", "CNs", "CNt", "DE", "FR", "IT", "JP", "KO", "PL", "RU", "SP", "TR" };

    private static readonly Regex EmbeddedKey = new Regex(@"^\{=([^}]+)\}(.*)$", RegexOptions.Singleline);

    // ── 1. One text per id within a language ──────────────────────────────────────────────────

    /// <summary>Every id that two files of one language both carry, with its texts in load order.</summary>
    internal static List<string> ConflictingRows(IEnumerable<(string File, IEnumerable<(string Id, string Text)> Rows)> filesInLoadOrder,
        string laterNote = "loads later and wins")
    {
        var first = new Dictionary<string, (string File, string Text)>(StringComparer.Ordinal);
        var conflicts = new List<string>();
        foreach (var (file, rows) in filesInLoadOrder)
        {
            foreach (var (id, text) in rows)
            {
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                if (first.TryGetValue(id, out var earlier))
                {
                    if (!string.Equals(earlier.Text, text, StringComparison.Ordinal))
                    {
                        conflicts.Add($"{id}: \"{earlier.Text}\" ({earlier.File}) vs \"{text}\" ({file}, {laterNote})");
                    }
                }
                else
                {
                    first[id] = (file, text);
                }
            }
        }
        return conflicts;
    }

    [TestMethod]
    public void ConflictingRows_SameIdDifferentTextInALaterFile_IsReported()
    {
        var conflicts = ConflictingRows(new[]
        {
            ("module.xml", (IEnumerable<(string, string)>)new[] { ("taom_aso_kingdom.sturgia", "Dale") }),
            ("keybind.xml", new[] { ("taom_aso_kingdom.sturgia", "Valle") }),
        });

        Assert.AreEqual(1, conflicts.Count);
        StringAssert.Contains(conflicts[0], "Valle");
    }

    [TestMethod]
    public void ConflictingRows_SameIdSameText_IsNotReported()
    {
        var conflicts = ConflictingRows(new[]
        {
            ("module.xml", (IEnumerable<(string, string)>)new[] { ("taom_x", "Dale") }),
            ("xslt.xml", new[] { ("taom_x", "Dale") }),
        });

        Assert.AreEqual(0, conflicts.Count);
    }

    [TestMethod]
    public void EachLanguage_GivesAnIdOneText_AcrossAllItsRegisteredFiles()
    {
        var problems = new List<string>();
        var filesRead = 0;
        foreach (var lang in SupportedLanguageDirs)
        {
            var manifest = Path.Combine(LanguagesPath, lang, "language_data.xml");
            Assert.IsTrue(File.Exists(manifest), $"{manifest} not found");

            var files = new List<(string, IEnumerable<(string, string)>)>();
            foreach (var entry in XDocument.Load(manifest).Root.Elements("LanguageFile"))
            {
                var relative = (string)entry.Attribute("xml_path") ?? string.Empty;
                var path = Path.Combine(LanguagesPath, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    continue;   // LanguageDataXmlTests reports a registered file that is missing
                }
                filesRead++;
                var rows = XDocument.Load(path).Descendants("string")
                    .Select(r => ((string)r.Attribute("id"), (string)r.Attribute("text") ?? string.Empty))
                    .ToList();
                files.Add((Path.GetFileName(path), rows));
            }
            problems.AddRange(ConflictingRows(files).Select(c => lang + " " + c));
        }

        Assert.IsTrue(filesRead > 100,
            $"Only {filesRead} registered language files read; a scan that reads nothing passes for the wrong reason.");
        Assert.AreEqual(0, problems.Count,
            $"{problems.Count} id(s) carry different texts in two files of one language; in game the file " +
            "language_data.xml lists later wins. Keep one row (the curated one) and set the translation " +
            "cache to it:\n  " + string.Join("\n  ", problems.Take(40)));
    }

    // ── 1b. Two English sources that declare one key give it one text ────────────────────────

    [TestMethod]
    public void EveryKeyTwoEnglishSourcesShare_HasOneEnglishText()
    {
        // English reads each file's own default while every other language translates the owner's, so a
        // shared key whose English differs shows two meanings. One row per key per file: some files repeat
        // a key on purpose, and that is gate 1's question, not this one's.
        var files = EnglishSources().OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (Path.GetFileName(f), (IEnumerable<(string, string)>)EnglishRows(f)
                .GroupBy(r => r.Id, StringComparer.Ordinal).Select(g => g.First()).ToList()))
            .ToList();
        Assert.IsTrue(files.Count > 10, $"Only {files.Count} English sources found; the scan broke.");

        var problems = ConflictingRows(files, "another English source");
        Assert.AreEqual(0, problems.Count,
            $"{problems.Count} key(s) that two English sources declare carry two English texts; English shows " +
            "each file's own, every other language one translation. Make them agree:\n  " +
            string.Join("\n  ", problems.Take(40)));
    }

    // ── 2. An inline default equals the registered English ────────────────────────────────────

    /// <summary>
    /// A plain C# literal that is a whole <c>{=taom_*}Default</c>: not interpolated, not verbatim,
    /// not continued by <c>+</c>, not on a comment line. Group 1 is the key, group 2 the raw default.
    /// </summary>
    private static readonly Regex CSharpDefault = new Regex(
        "(?<![$@\\w])\"\\{=(taom_[A-Za-z0-9_.]+)\\}((?:[^\"\\\\\\r\\n]|\\\\.)*)\"(?!\\s*\\+)",
        RegexOptions.Compiled);

    internal static string UnescapeCSharp(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length)
            {
                sb.Append(s[i]);
                continue;
            }
            var c = s[++i];
            switch (c)
            {
                case 'n': sb.Append('\n'); break;
                case 't': sb.Append('\t'); break;
                case 'r': sb.Append('\r'); break;
                case 'u' when i + 4 < s.Length:
                    sb.Append((char)Convert.ToInt32(s.Substring(i + 1, 4), 16));
                    i += 4;
                    break;
                default: sb.Append(c); break;   // \" \\ \'
            }
        }
        return sb.ToString();
    }

    private static bool IsUnderLanguages(string file) =>
        file.Contains(Path.DirectorySeparatorChar + "Languages" + Path.DirectorySeparatorChar);

    private static bool IsEnglishSource(string file)
    {
        var name = Path.GetFileName(file);
        return !IsUnderLanguages(file)
            && (name == "global_strings.xml" || name.EndsWith("_strings.xml", StringComparison.Ordinal));
    }

    /// <summary>The English string files: <c>global_strings.xml</c> and every <c>*_strings.xml</c> outside Languages.</summary>
    private static IEnumerable<string> EnglishSources() =>
        Directory.EnumerateFiles(ModuleDataPath, "*.xml", SearchOption.AllDirectories).Where(IsEnglishSource);

    /// <summary>Each <c>{=key}Default</c> row of one English source, in file order.</summary>
    private static IEnumerable<(string Id, string Text)> EnglishRows(string file) =>
        XDocument.Load(file).Descendants("string")
            .Select(row => EmbeddedKey.Match((string)row.Attribute("text") ?? string.Empty))
            .Where(m => m.Success)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value));

    /// <summary>Every registered English row across the English sources, key to text.</summary>
    private static Dictionary<string, string> RegisteredEnglish()
    {
        var registered = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in EnglishSources())
        {
            foreach (var (id, text) in EnglishRows(file))
            {
                if (!registered.ContainsKey(id))
                {
                    registered[id] = text;
                }
            }
        }
        return registered;
    }

    [TestMethod]
    public void CSharpDefault_PlainLiteral_IsMatchedAndUnescaped()
    {
        var m = CSharpDefault.Match("new TextObject(\"{=taom_x}He said \\\"no\\\".\");");

        Assert.IsTrue(m.Success);
        Assert.AreEqual("taom_x", m.Groups[1].Value);
        Assert.AreEqual("He said \"no\".", UnescapeCSharp(m.Groups[2].Value));
    }

    [TestMethod]
    public void CSharpDefault_InterpolatedOrConcatenatedLiteral_IsSkipped()
    {
        Assert.IsFalse(CSharpDefault.IsMatch("$\"{=taom_x}Count {n}\""));
        Assert.IsFalse(CSharpDefault.IsMatch("\"{=taom_x}Part one \" + rest"));
    }

    [TestMethod]
    public void EveryCSharpDefault_OfARegisteredTaomKey_MatchesTheRegisteredEnglish()
    {
        var registered = RegisteredEnglish();
        Assert.IsTrue(registered.Count > 1000, $"Only {registered.Count} registered rows found; the scan broke.");

        var checkedSites = 0;
        var problems = new List<string>();
        var mainDir = Path.Combine(RepoRoot, "Main");
        foreach (var file in Directory.EnumerateFiles(mainDir, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            {
                continue;
            }
            var lines = File.ReadAllLines(file);
            for (var n = 0; n < lines.Length; n++)
            {
                if (lines[n].TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Match m in CSharpDefault.Matches(lines[n]))
                {
                    if (!registered.TryGetValue(m.Groups[1].Value, out var english))
                    {
                        continue;   // registration itself is UnregisteredLocalizationKeyBaselineTests' job
                    }
                    checkedSites++;
                    var inline = UnescapeCSharp(m.Groups[2].Value);
                    // A bare "{=key}" names the key and defers to its registered text on purpose
                    // (NativeSkinFixesInstaller's message keys): there is no second English to drift.
                    if (inline.Length > 0 && !string.Equals(inline, english, StringComparison.Ordinal))
                    {
                        problems.Add($"{Path.GetFileName(file)}:{n + 1} {m.Groups[1].Value}: code \"{inline}\" vs registered \"{english}\"");
                    }
                }
            }
        }

        Assert.IsTrue(checkedSites > 500, $"Only {checkedSites} C# sites checked; the literal pattern broke.");
        Assert.AreEqual(0, problems.Count,
            $"{problems.Count} C# default(s) differ from the registered English. English players read the code, " +
            "the other twelve languages translate the registration. Align them, or give the site its own key:\n  " +
            string.Join("\n  ", problems.Take(40)));
    }

    [TestMethod]
    public void EveryDataDefault_OfARegisteredKey_MatchesTheRegisteredEnglish()
    {
        // The data half of the rule above: an attribute, or the text of an XSLT or XML leaf, that is a whole
        // {=key}Default, outside the English sources and the language files.
        var registered = RegisteredEnglish();
        var checkedSites = 0;
        var problems = new List<string>();
        var files = Directory.EnumerateFiles(ModuleDataPath, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase))
                        && !IsUnderLanguages(f) && !IsEnglishSource(f));
        foreach (var file in files)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (System.Xml.XmlException)
            {
                continue;   // well-formedness is the schema gates' job
            }
            // An XSLT attribute value template writes literal braces doubled: "{{=key}}Default" outputs
            // "{=key}Default" (spcultures.xslt and spkingdoms.xslt carry 509 such sites).
            var isXslt = file.EndsWith(".xslt", StringComparison.OrdinalIgnoreCase);
            var values = doc.Descendants().Attributes()
                .Select(a => isXslt ? a.Value.Replace("{{", "{").Replace("}}", "}") : a.Value)
                .Concat(doc.Descendants().Where(e => !e.HasElements).Select(e => e.Value.Trim()));
            foreach (var value in values)
            {
                var m = EmbeddedKey.Match(value);
                if (!m.Success || m.Groups[2].Value.Length == 0 || !registered.TryGetValue(m.Groups[1].Value, out var english))
                {
                    continue;
                }
                checkedSites++;
                if (!string.Equals(m.Groups[2].Value, english, StringComparison.Ordinal))
                {
                    problems.Add($"{Path.GetFileName(file)} {m.Groups[1].Value}: data \"{m.Groups[2].Value}\" vs registered \"{english}\"");
                }
            }
        }

        // 6,621 on 2026-09-25, 509 of them the stylesheets' escaped sites (6,112 without them); a floor above the
        // unescaped count pins the XSLT half of the scan
        Assert.IsTrue(checkedSites > 6300, $"Only {checkedSites} data sites checked; the scan broke.");
        Assert.AreEqual(0, problems.Count,
            $"{problems.Count} data default(s) differ from the registered English. English players read the data, " +
            "the other twelve languages translate the registration. Align them (and re-run the generator that " +
            "registered the key, if one did), or give the site its own key:\n  " + string.Join("\n  ", problems.Take(40)));
    }

    // ── 3. One English default per name key in the name generator's sources ──────────────────

    /// <summary>
    /// Every key that the given attribute values give more than one English default, with each default and
    /// the first file that gives it. Returns the number of distinct keys seen as well, so a scan that found
    /// none can fail.
    /// </summary>
    internal static (int Keys, List<string> Shared) KeysWithTwoDefaults(IEnumerable<(string File, IEnumerable<string> Values)> files)
    {
        var defaults = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (file, values) in files)
        {
            foreach (var value in values)
            {
                var m = EmbeddedKey.Match(value);
                if (!m.Success)
                {
                    continue;
                }
                if (!defaults.TryGetValue(m.Groups[1].Value, out var byText))
                {
                    defaults[m.Groups[1].Value] = byText = new Dictionary<string, string>(StringComparer.Ordinal);
                }
                if (!byText.ContainsKey(m.Groups[2].Value))
                {
                    byText[m.Groups[2].Value] = file;
                }
            }
        }
        var shared = defaults.Where(p => p.Value.Count > 1)
            .Select(p => p.Key + ": " + string.Join(" / ", p.Value.Select(t => $"\"{t.Key}\" ({t.Value})")))
            .ToList();
        return (defaults.Count, shared);
    }

    [TestMethod]
    public void KeysWithTwoDefaults_ClansSharingOneKey_AreReported()
    {
        // the Umbar shape: six clans, one key, each its own English name
        var (_, shared) = KeysWithTwoDefaults(new[]
        {
            ("clans.xml", (IEnumerable<string>)new[] { "{=aom_clan_umbar_name}Corsairs of Umbar", "{=aom_clan_umbar_name}House of Castamir" }),
        });

        Assert.AreEqual(1, shared.Count);
        StringAssert.Contains(shared[0], "Castamir");
    }

    [TestMethod]
    public void KeysWithTwoDefaults_OneDefaultInTwoFiles_IsNotReported()
    {
        var (keys, shared) = KeysWithTwoDefaults(new[]
        {
            ("troops_rivendell.xml", (IEnumerable<string>)new[] { "{=aom_x_name}[Rivendell] Recruit", "plain text" }),
            ("troops_lindon.xml", new[] { "{=aom_x_name}[Rivendell] Recruit" }),
        });

        Assert.AreEqual((1, 0), (keys, shared.Count));
    }

    [TestMethod]
    public void EveryNameKey_InTheNameGeneratorsSources_HasOneEnglishDefault()
    {
        var sources = Directory.GetFiles(Path.Combine(ModuleDataPath, "troops"), "*.xml")
            .Concat(new[]
            {
                Path.Combine(ModuleDataPath, "characters", "lords.xml"),
                Path.Combine(ModuleDataPath, "characters", "clans.xml"),
                Path.Combine(ModuleDataPath, "taom_spkingdoms.xml"),
            })
            .Where(File.Exists)
            .ToList();
        Assert.IsTrue(sources.Count > 10, $"Only {sources.Count} name sources found; the path broke.");

        var (keys, shared) = KeysWithTwoDefaults(sources.Select(file =>
            (Path.GetFileName(file), XDocument.Load(file).Descendants().Attributes().Select(a => a.Value))));
        Assert.IsTrue(keys > 1000, $"Only {keys} name keys found; the scan broke.");
        Assert.AreEqual(0, shared.Count,
            $"{shared.Count} name key(s) carry two different English names; every other language shows one " +
            "translation for both characters. Give the copy its own key (aom_<id>_name) and re-run " +
            "tools/generate_name_localization_strings.py:\n  " + string.Join("\n  ", shared.Take(40)));
    }
}
