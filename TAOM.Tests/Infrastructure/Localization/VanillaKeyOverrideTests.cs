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
/// TAOM key (<c>TAOM_liege_*</c>, <c>LiegeTitleOverrideTests</c>). Inconclusive without the install.
/// </summary>
[TestClass]
[TestCategory("LiveInstall")]
public class VanillaKeyOverrideTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    private static readonly Regex InlineKey = new Regex(@"\{=([^}!*][^}]*)\}", RegexOptions.Compiled);
    private static readonly Regex RowId = new Regex("<string\\s+id=\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>Vanilla keys TAOM overrides on purpose, each with its reason.</summary>
    private static readonly Dictionary<string, string> AllowedOverrides = new(StringComparer.Ordinal)
    {
        // taom_module_strings.xml registers the convoy party name pair so str_convoy_party_name resolves;
        // its 12 language rows drop vanilla's grammar tokens too. Follow-up: drop the rows, keep the
        // registration (docs/reviews/rca-full-translation-run-2026-10-02.md).
        ["LjUhEJxz"] = "convoy party name (registration needed by str_convoy_party_name)",
        ["l4pRw7pO"] = "convoy party name (registration needed by str_convoy_party_name)",
    };

    private static HashSet<string> VanillaEnglishKeys()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        string game = string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var module in new[] { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle" })
        {
            var dir = Path.Combine(game, "Modules", module, "ModuleData");
            if (!Directory.Exists(dir))
                continue;
            foreach (var file in Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories)
                         .Where(f => !f.Split(Path.DirectorySeparatorChar).Contains("Languages")))
            {
                foreach (Match m in InlineKey.Matches(File.ReadAllText(file)))
                    keys.Add(m.Groups[1].Value);
            }
        }
        if (keys.Count < 5000)
            Assert.Inconclusive($"Only {keys.Count} vanilla keys found under {game}; the install is missing or partial.");
        return keys;
    }

    [TestMethod]
    public void TaomLanguageRows_VanillaKey_OnlyWhenAllowed()
    {
        var vanilla = VanillaEnglishKeys();
        var languages = Path.Combine(RepoRoot, "Main", "_Module", "ModuleData", "Languages");
        var problems = new SortedSet<string>(StringComparer.Ordinal);
        var rowsScanned = 0;
        foreach (var file in Directory.EnumerateFiles(languages, "std_taom_*.xml", SearchOption.AllDirectories))
        {
            foreach (Match m in RowId.Matches(File.ReadAllText(file)))
            {
                rowsScanned++;
                var id = m.Groups[1].Value;
                if (vanilla.Contains(id) && !AllowedOverrides.ContainsKey(id))
                    problems.Add($"{Path.GetFileName(Path.GetDirectoryName(file))}/{Path.GetFileName(file)} [{id}]");
            }
        }

        // about 192,000 rows across the 12 languages on 2026-10-02
        Assert.IsTrue(rowsScanned > 100000, $"Only {rowsScanned} TAOM language rows scanned; the path broke.");
        Assert.AreEqual(0, problems.Count,
            $"{problems.Count} TAOM language row(s) replace TaleWorlds' translation of a vanilla key. Delete the rows " +
            "(and the English registration and cache entries), or give the changed text a TAOM key:\n  " +
            string.Join("\n  ", problems.Take(40)));
    }
}
