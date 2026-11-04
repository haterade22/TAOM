using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongDefeat.Client;

/// <summary>
/// Every line of the command panel is a <c>{=TAOM_LD_...}English</c> literal in the client source, registered in
/// <c>taom_module_strings.xml</c> and seeded with its English text in the 12 language files. No translation tool runs for this
/// feature: the translations are owed to issue 763, so each language row is the English seed (docs/localization/TRANSLATOR_GUIDE.md).
/// </summary>
[TestClass]
public class LongDefeatHudStringsTests
{
    private static readonly Regex Literal = new(@"""\{=(?<key>[^}""]+)\}(?<text>[^""]*)""", RegexOptions.Compiled);

    private static readonly string[] HudPrefixes = { "TAOM_LD_Hud_", "TAOM_LD_Notice_", "TAOM_LD_End_" };

    private static IEnumerable<string> ClientSources() =>
        Directory.GetFiles(RepoPaths.RepoPath("Client", "Features", "LongDefeat"), "*.cs", SearchOption.AllDirectories);

    // The Main side and the config files also carry TAOM_LD_ keys. Only those count there, because {=oHaWR73d}Ok and {=!} are legitimate strays.
    private static IEnumerable<string> MainSources() =>
        Directory.GetFiles(RepoPaths.RepoPath("Main", "Features", "LongDefeat"), "*.cs", SearchOption.AllDirectories)
            .Append(RepoPaths.RepoPath("Main", "Adapters", "LongDefeatMenuAdapter.cs"))
            .Concat(Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "long_defeat"), "*.json"));

    private static IEnumerable<(string Key, string Text, string File)> LiteralsOf(string file, bool comments)
    {
        var text = File.ReadAllText(file);
        return Literal.Matches(comments ? RepoPaths.StripComments(text) : text)
            .Cast<Match>().Select(m => (m.Groups["key"].Value, m.Groups["text"].Value, Path.GetFileName(file)));
    }

    private static List<(string Key, string Text, string File)> ClientLiterals() =>
        ClientSources().SelectMany(file => LiteralsOf(file, true)).ToList();

    private static List<(string Key, string Text, string File)> SourceLiterals() =>
        ClientLiterals()
            .Concat(MainSources().SelectMany(file => LiteralsOf(file, file.EndsWith(".cs", System.StringComparison.Ordinal)))
                .Where(l => l.Key.StartsWith("TAOM_LD_", System.StringComparison.Ordinal)))
            .ToList();

    private static Dictionary<string, string> EnglishRows() =>
        XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml")).Descendants("string")
            .ToDictionary(s => (string)s.Attribute("id")!, s => (string)s.Attribute("text")!);

    private static string[] LanguageFiles() =>
        Directory.GetFiles(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories);

    [TestMethod]
    public void TheClientSource_HasHudLiterals_AndEveryOneIsALongDefeatKey()
    {
        var literals = ClientLiterals();

        Assert.IsTrue(literals.Count >= 25, "the HUD text file has far more lines than " + literals.Count);
        var strays = literals.Where(l => !l.Key.StartsWith("TAOM_LD_", System.StringComparison.Ordinal)).Select(l => l.Key).Distinct().ToList();
        Assert.AreEqual(0, strays.Count, "client literals outside the TAOM_LD_ namespace: " + string.Join(", ", strays));
    }

    [TestMethod]
    public void EveryKey_HasOneEnglishDefault_AcrossTheSource()
    {
        var conflicts = SourceLiterals().GroupBy(l => l.Key).Where(g => g.Select(x => x.Text).Distinct().Count() > 1).Select(g => g.Key).ToList();

        Assert.AreEqual(0, conflicts.Count, "keys with two different defaults: " + string.Join(", ", conflicts));
    }

    [TestMethod]
    public void EveryKey_IsRegisteredInTheEnglishFile_WithTheSameDefault()
    {
        var rows = EnglishRows();
        var failures = new List<string>();

        foreach (var (key, text, _) in SourceLiterals().Distinct())
        {
            if (!rows.TryGetValue(key, out var registered))
                failures.Add(key + " is not in taom_module_strings.xml");
            else if (registered != "{=" + key + "}" + text)
                failures.Add($"{key}: file has '{registered}', source has '{{={key}}}{text}'");
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    public void EveryKey_IsSeededInAllTwelveLanguageFiles()
    {
        var files = LanguageFiles();
        Assert.AreEqual(12, files.Length);
        var keys = SourceLiterals().Select(l => l.Key).Distinct().ToList();

        foreach (var file in files)
        {
            var ids = XDocument.Load(file).Descendants("string").Select(s => (string?)s.Attribute("id")).ToHashSet();
            var missing = keys.Where(k => !ids.Contains(k)).ToList();
            Assert.AreEqual(0, missing.Count, Path.GetFileName(file) + " lacks: " + string.Join(", ", missing));
        }
    }

    [TestMethod]
    public void TheScan_ReachesTheMainSideAndTheJsonFiles()
    {
        var keys = SourceLiterals().Select(l => l.Key).ToHashSet();

        foreach (var key in new[] { "TAOM_LD_Prompt_ChoiceBest", "TAOM_LD_Prompt_Cancel", "TAOM_LD_Fortress_Helms", "TAOM_LD_Offer_Militia" })
            Assert.IsTrue(keys.Contains(key), key + " is in Main or a config file and the scan must see it");
    }

    [TestMethod]
    public void NoRegisteredHudKey_IsLeftUnused()
    {
        var used = SourceLiterals().Select(l => l.Key).ToHashSet();

        var orphans = EnglishRows().Keys.Where(k => HudPrefixes.Any(p => k.StartsWith(p, System.StringComparison.Ordinal)) && !used.Contains(k)).ToList();

        Assert.AreEqual(0, orphans.Count, "registered but never used: " + string.Join(", ", orphans));
    }

    [TestMethod]
    public void TheEnglishDefaults_UseNoEmOrEnDash()
    {
        var offenders = SourceLiterals().Where(l => l.Text.Contains((char)0x2014) || l.Text.Contains((char)0x2013)).Select(l => l.Key).ToList();

        Assert.AreEqual(0, offenders.Count, string.Join(", ", offenders));
    }

    [TestMethod]
    public void EveryVariable_IsUpperCase_SoAFillerCanBeFoundAndAFeedLineNeverShowsABrace()
    {
        foreach (var (key, text, _) in SourceLiterals())
        {
            foreach (Match m in Regex.Matches(text, @"\{([^}]*)\}"))
                StringAssert.Matches(m.Groups[1].Value, new Regex("^[A-Z]+$"), key + " has a variable named " + m.Groups[1].Value);
        }
    }
}
