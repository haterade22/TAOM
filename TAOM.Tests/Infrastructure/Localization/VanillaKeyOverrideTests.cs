using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// A TAOM language row whose id is a key vanilla's own English data declares replaces TaleWorlds'
/// translation of that key in that language: TAOM's language files load after SandBox's, and
/// <c>LocalizedTextManager.DeserializeStrings</c> keeps the last row it reads for an id. Vanilla's
/// rows are curated and carry the engine's grammar tokens (TR <c>'{.e}</c>, RU <c>{.g}</c>, DE
/// <c>{articleHelper(...)}</c>); a machine-translated replacement drops them. On 2026-10-01 a
/// translator run registered 36 <c>comment_strings.xslt</c>/<c>action_strings.xslt</c> keys this way
/// and gave male players "meine mein Herr" in four languages.
///
/// A stylesheet that keeps a vanilla key and its meaning registers nothing; a change of meaning takes a
/// TAOM key (<c>TAOM_liege_*</c>, <c>LiegeTitleOverrideTests</c>). A vanilla key TAOM rewords in English is an
/// English-only override: its row in <c>taom_module_strings.xml</c> stays and it has no language row
/// (<c>LocalizationOverrideLoader</c>, <c>Patch25_LocalizationOverride</c>).
///
/// The exceptions are pairs of (language, id) listed in <c>kept-vanilla-key-rows.txt</c>, each with its reason:
/// vanilla's row would be wrong for TAOM there, so TAOM's row stays. The vanilla ids come from
/// <see cref="VanillaLocalizationKeys.All"/>. Every test here needs the install (the stale-line test too, since it
/// asks whether vanilla still declares the id) and is Inconclusive without it.
/// </summary>
[TestClass]
public class VanillaKeyOverrideTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static readonly Regex RowId = new Regex("<string\\s+id=\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>The reason codes of <c>kept-vanilla-key-rows.txt</c>, whose header words each one.</summary>
    private static readonly HashSet<string> ReasonTags = new(StringComparer.Ordinal) { "variable", "gender-tag", "article" };

    private static string LanguagesPath => Path.Combine(RepoRoot, "Main", "_Module", "ModuleData", "Languages");

    private static string KeptRowsPath => Path.Combine(RepoRoot, "TAOM.Tests", "Infrastructure", "Localization",
        "kept-vanilla-key-rows.txt");

    /// <summary>The kept (language dir, id) pairs. A data file because it lists hundreds of pairs.</summary>
    private static HashSet<(string Language, string Id)> LoadKeptRows()
    {
        Assert.IsTrue(File.Exists(KeptRowsPath), $"Kept-rows list not found at {KeptRowsPath}");
        var kept = new HashSet<(string, string)>();
        foreach (var raw in File.ReadAllLines(KeptRowsPath))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#"))
                continue;
            var parts = line.Split(' ');
            Assert.AreEqual(3, parts.Length, $"Bad line in kept-vanilla-key-rows.txt: '{line}'");
            Assert.IsTrue(ReasonTags.Contains(parts[2]), $"Unknown reason '{parts[2]}' in kept-vanilla-key-rows.txt");
            Assert.IsTrue(kept.Add((parts[0], parts[1])), $"Duplicate line for {parts[0]} {parts[1]}");
        }
        return kept;
    }

    /// <summary>Every (language dir, id) pair TAOM's language files carry whose id vanilla declares, plus the number of rows scanned.</summary>
    private static (HashSet<(string Language, string Id)> Pairs, int Rows) TaomRowsOfVanillaKeys()
    {
        var vanilla = VanillaLocalizationKeys.All();
        var pairs = new HashSet<(string, string)>();
        var rows = 0;
        foreach (var file in Directory.EnumerateFiles(LanguagesPath, "std_taom_*.xml", SearchOption.AllDirectories))
        {
            var language = Path.GetFileName(Path.GetDirectoryName(file))!;
            foreach (Match m in RowId.Matches(File.ReadAllText(file)))
            {
                rows++;
                if (vanilla.Contains(m.Groups[1].Value))
                    pairs.Add((language, m.Groups[1].Value));
            }
        }
        return (pairs, rows);
    }

    [TestMethod]
    public void TaomLanguageRows_VanillaKey_OnlyWhenKept()
    {
        var kept = LoadKeptRows();
        var (pairs, rowsScanned) = TaomRowsOfVanillaKeys();
        var problems = new SortedSet<string>(pairs.Where(p => !kept.Contains(p)).Select(p => $"{p.Language} [{p.Id}]"),
            StringComparer.Ordinal);

        // about 192,000 rows across the 12 languages on 2026-10-02
        Assert.IsTrue(rowsScanned > 100000, $"Only {rowsScanned} TAOM language rows scanned; the path broke.");
        Assert.AreEqual(0, problems.Count,
            $"{problems.Count} TAOM language row(s) replace TaleWorlds' translation of a vanilla key. Delete the " +
            "language rows (an English-only override keeps its English row in taom_module_strings.xml), give " +
            "the changed text a TAOM key, or list the pair in kept-vanilla-key-rows.txt with its reason:\n  " +
            string.Join("\n  ", problems.Take(40)));
    }

    [TestMethod]
    public void KeptRows_EachStillHasItsRow()
    {
        var kept = LoadKeptRows();
        var (pairs, _) = TaomRowsOfVanillaKeys();
        var stale = kept.Where(k => !pairs.Contains(k)).Select(k => $"{k.Language} {k.Id}")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, stale.Count,
            $"{stale.Count} kept-vanilla-key-rows.txt line(s) name a pair that is not a TAOM row of a vanilla key: the row is " +
            "gone, or vanilla no longer declares the id. Restore the row from git (its cache entry is kept), or delete " +
            "the line if the removal was on purpose:\n  " + string.Join("\n  ", stale.Take(40)));
    }

    [TestMethod]
    public void ModuleStrings_VanillaShape_MatchesTheInstall()
    {
        // Arrange: the {=KEY} keys of the file LocalizationOverrideLoader reads
        var vanilla = VanillaLocalizationKeys.All();
        var path = Path.Combine(RepoRoot, "Main", "_Module", "ModuleData", "taom_module_strings.xml");
        var keys = Regex.Matches(File.ReadAllText(path), "text=\"\\{=([^}]+)\\}").Cast<Match>()
            .Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        // Act: a key with the vanilla shape that vanilla does not declare, or the reverse
        var mismatched = keys.Where(k => VanillaLocalizationKeys.HasVanillaShape(k) != vanilla.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal).ToList();

        // Assert
        Assert.IsTrue(keys.Count > 1000, $"Only {keys.Count} keys read from {path}; the path broke.");
        Assert.AreEqual(0, mismatched.Count,
            $"{mismatched.Count} key(s) of taom_module_strings.xml have the vanilla shape without being vanilla's, or the " +
            "reverse. The shape decides English-only overrides in the translator and LanguageFileCoverageTests; delete a " +
            "dead override row (vanilla dropped the key), and give a TAOM key a name that does not look like a vanilla id:\n  " +
            string.Join("\n  ", mismatched.Take(40)));
    }
}
