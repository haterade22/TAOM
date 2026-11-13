using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.LongMemory;

/// <summary>
/// The two player-facing lines Long Memory adds. The C# literal is the English source, the English row in
/// <c>taom_module_strings.xml</c> must equal it, and each of the twelve language files carries one row per key (its
/// translation, or the English default until the paid run fills it) that keeps the <c>{VILLAGE}</c> placeholder.
/// </summary>
[TestClass]
public class LongMemoryWordingTests
{
    private const string AdapterFile = "Main/Adapters/LongMemoryNoticeAdapter.cs";

    private static readonly (string Key, string Wording)[] Lines =
    {
        ("taom_longmemory_raid", "The people of {VILLAGE} will long remember the raid."),
        ("taom_longmemory_coercion", "The people of {VILLAGE} will long remember what was taken from them by force."),
    };

    public static IEnumerable<object[]> Keys() => Lines.Select(l => new object[] { l.Key, l.Wording });

    private static string LiteralInTheAdapter(string key)
    {
        var src = RepoPaths.ReadSource(AdapterFile, stripComments: true);
        var m = Regex.Match(src, @"new TextObject\(""(\{=" + key + @"\}[^""]*)""\)");
        Assert.IsTrue(m.Success, "the notice adapter must build a line from the {=" + key + "} literal");
        return m.Groups[1].Value;
    }

    private static List<string> Rows(string relativePath, string key)
    {
        var text = File.ReadAllText(RepoPaths.RepoPath(relativePath.Split('/')));
        return Regex.Matches(text, @"<string\s+id=""" + key + @"""\s+text=""([^""]*)""")
            .Cast<Match>()
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToList();
    }

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void TheLiteral_IsTheExpectedWording_WithItsKey(string key, string wording)
    {
        Assert.AreEqual("{=" + key + "}" + wording, LiteralInTheAdapter(key));
    }

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void TheEnglishRow_EqualsTheLiteral(string key, string wording)
    {
        var rows = Rows("Main/_Module/ModuleData/taom_module_strings.xml", key);

        Assert.AreEqual(1, rows.Count, "one English row");
        Assert.AreEqual(LiteralInTheAdapter(key), rows[0]);
    }

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void EveryLanguageFile_HoldsOneRow_KeepingTheVillagePlaceholder(string key, string wording)
    {
        var languages = Directory.GetDirectories(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"));
        Assert.AreEqual(12, languages.Length);
        foreach (var dir in languages)
        {
            var file = Directory.GetFiles(dir, "std_taom_module_strings_*.xml").Single();
            var rows = Rows("Main/_Module/ModuleData/Languages/" + Path.GetFileName(dir) + "/" + Path.GetFileName(file), key);

            Assert.AreEqual(1, rows.Count, Path.GetFileName(file));
            StringAssert.Contains(rows[0], "{VILLAGE}", Path.GetFileName(file) + ": the row must keep the {VILLAGE} placeholder");
        }
    }

    [TestMethod]
    public void TheWording_NamesNoGender_AndCarriesNoDash()
    {
        foreach (var (_, wording) in Lines)
        {
            Assert.IsFalse(Regex.IsMatch(wording, @"\b(he|she|his|her|him|hers|man|woman|men|women)\b", RegexOptions.IgnoreCase), wording);
            Assert.IsFalse(wording.Contains('\u2014') || wording.Contains('\u2013'), wording);
        }
    }

    [TestMethod]
    public void TheLinesUseTheVillageVariable_AndBuildExactlyTwoLines()
    {
        var src = RepoPaths.ReadSource(AdapterFile, stripComments: true);

        StringAssert.Contains(src, "SetTextVariable(\"VILLAGE\", villageName)");
        Assert.AreEqual(2, Regex.Matches(src, @"new TextObject\(").Count, "two lines");
    }
}
