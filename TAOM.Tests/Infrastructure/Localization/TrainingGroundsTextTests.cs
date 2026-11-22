using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Core;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// The town arena is the Training Grounds in English and its keeper the Training Master (#1527,
/// docs/features/arena.md). Vanilla's text is reworded by one English row per key in
/// <c>taom_module_strings.xml</c>, which the Localization Override applies; TAOM's own text must not
/// bring an old name back. The keeper has two old names, "arena master" and "tournament master".
/// </summary>
[TestClass]
public class TrainingGroundsTextTests
{
    private const string KeeperName = "Training Master";

    private static readonly Regex OldName =
        new Regex(@"\b(arenas?|tournament masters?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Literal = new Regex("\\{=([^}!*][^}]*)\\}([^\"<]*)", RegexOptions.Compiled);
    private static readonly Regex Token = new Regex(@"\{[^{}]*\}|\[[^\[\]]*\]", RegexOptions.Compiled);

    /// <summary>Vanilla keys whose English names the arena or its keeper and that TAOM rewords.</summary>
    private static readonly string[] RewordedKeys =
    {
        "mMU3H6HZ", "Duoo5R2P", "CfDlOdTH", "YQ3vm6er", "5id9mGrc", "brzz79Je", "wsbkjJhz",
        "CeYwClaG", "ocIutUyu", "guEjfYhO", "Qb3axqdZ", "GuWWKgEm", "mnAdqeGu", "cPmV8S4e",
        "FguHzavX", "KtrZs3yA", "Sph9Nliz", "ZvzxcRbc", "DUVqJifX", "bFjtk0Op", "waUxgr5b",
        "IHKXNP9H", "CQZJLWfw",
    };

    /// <summary>
    /// Vanilla keys that keep the word: a gladiator's life story, where the arena is a place of blood
    /// (3Yax10rQ), the biography of a vanilla hero (ZwavTRcF), and a multiplayer duel message (v5EqMSlD).
    /// </summary>
    private static readonly string[] KeptKeys = { "3Yax10rQ", "ZwavTRcF", "v5EqMSlD" };

    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    /// <summary>TAOM's English row of each vanilla key it rewords, without the <c>{=KEY}</c> prefix.</summary>
    private static Dictionary<string, List<string>> RewordedRows()
    {
        var rows = RewordedKeys.ToDictionary(key => key, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var row in XDocument.Load(Path.Combine(ModuleData(), "taom_module_strings.xml")).Descendants("string"))
        {
            var text = (string?)row.Attribute("text") ?? "";
            var m = Literal.Match(text);
            if (m.Success && m.Index == 0 && rows.TryGetValue(m.Groups[1].Value, out var found))
                found.Add(text.Substring(m.Groups[1].Length + 3));
        }
        return rows;
    }

    private static string[] Tokens(string text) =>
        Token.Matches(text).Cast<Match>().Select(m => m.Value).OrderBy(t => t, StringComparer.Ordinal).ToArray();

    [TestMethod]
    public void EveryRewordedVanillaKey_HasOneEnglishRowThatNamesTheTrainingGrounds()
    {
        foreach (var pair in RewordedRows())
        {
            Assert.AreEqual(1, pair.Value.Count, $"{pair.Key}: expected one English row in taom_module_strings.xml");
            Assert.IsFalse(OldName.IsMatch(pair.Value[0]), $"{pair.Key} still carries an old name: {pair.Value[0]}");
            StringAssert.Contains(pair.Value[0].ToLowerInvariant(), "training", $"{pair.Key} does not name the training grounds or their master");
        }
    }

    [TestMethod]
    public void NoTaomEnglishLiteral_CarriesAnOldName()
    {
        var moduleData = ModuleData();
        var repo = Path.GetFullPath(Path.Combine(moduleData, "..", "..", ".."));
        var languages = Path.Combine(moduleData, "Languages") + Path.DirectorySeparatorChar;
        var data = Directory.EnumerateFiles(moduleData, "*.xml", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(languages, StringComparison.OrdinalIgnoreCase));
        var code = new[] { "Main", "Client" }
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(repo, dir), "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part == "obj" || part == "bin"));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var file in data.Concat(code))
        {
            var kind = Path.GetExtension(file).ToLowerInvariant();
            foreach (Match m in Literal.Matches(File.ReadAllText(file)))
            {
                seen.Add(kind + ":" + m.Groups[1].Value);
                if (OldName.IsMatch(m.Groups[2].Value))
                    offenders.Add($"{Path.GetFileName(file)}: {{={m.Groups[1].Value}}}{m.Groups[2].Value}");
            }
        }

        // One known literal from each input group: a strings row, an NPC name, a C# default.
        foreach (var expected in new[] { ".xml:taom_tidings_none", ".xml:aom_gd_arena", ".cs:taom_tidings_none" })
            Assert.IsTrue(seen.Contains(expected), $"the scan never read {expected}; an input group is missing");
        Assert.AreEqual(0, offenders.Count,
            "player text carries an old name; say training grounds or training master:\n" + string.Join("\n", offenders.Take(20)));
    }

    [TestMethod]
    public void EveryTaomTournamentMaster_IsNamedTrainingMaster()
    {
        var masters = Directory.EnumerateFiles(Path.Combine(ModuleData(), "characters"), "npcs_*.xml")
            .SelectMany(file => XDocument.Load(file).Descendants("NPCCharacter"))
            .Where(npc => (string?)npc.Attribute("occupation") == "ArenaMaster")
            .ToList();
        Assert.IsTrue(masters.Count >= 20, $"only {masters.Count} ArenaMaster characters found; the scan missed the data");

        var wrong = masters
            .Where(npc => !((string?)npc.Attribute("name") ?? "").EndsWith("}" + KeeperName, StringComparison.Ordinal))
            .Select(npc => $"{(string?)npc.Attribute("id")}: {(string?)npc.Attribute("name")}")
            .ToList();
        Assert.AreEqual(0, wrong.Count, "a tournament master is not named " + KeeperName + ":\n" + string.Join("\n", wrong));
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void EveryVanillaEnglishRowWithAnOldName_IsRewordedOrKept_AndKeepsItsTokens()
    {
        // The root of each module's Languages folder holds vanilla's English rows.
        var english = VanillaLocalizationKeys.TranslatedRows(string.Empty);
        var named = english.Where(row => OldName.IsMatch(row.Value)).Select(row => row.Key).ToList();

        var unknown = named.Except(RewordedKeys).Except(KeptKeys).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.AreEqual(0, unknown.Count,
            "vanilla text carries an old name under a key TAOM neither rewords nor keeps: "
            + string.Join(", ", unknown.Select(k => k + " = " + english[k])));

        var notRead = RewordedKeys.Concat(KeptKeys).Except(named).ToList();
        Assert.AreEqual(0, notRead.Count, "vanilla no longer declares, or no longer names the place in: " + string.Join(", ", notRead));

        foreach (var pair in RewordedRows().Where(pair => pair.Value.Count == 1))
        {
            CollectionAssert.AreEqual(Tokens(english[pair.Key]), Tokens(pair.Value[0]),
                $"{pair.Key}: the reworded row lost or gained a variable or tag against vanilla's: {english[pair.Key]}");
        }
    }
}
