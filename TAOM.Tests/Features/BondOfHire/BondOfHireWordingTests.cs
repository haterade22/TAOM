using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// The two player-facing lines of the Clans panel (#868). The C# literal is the English source, the English row in
/// <c>taom_module_strings.xml</c> must equal it, and each of the twelve language files carries one row (its translation, or
/// the English default until the paid run in #763 fills it) that keeps every placeholder of the English. The subject is the mercenaries, never the player, and no word is gendered.
/// </summary>
[TestClass]
public class BondOfHireWordingTests
{
    private const string TextsFile = "Main/Features/BondOfHire/UI/BondOfHireTexts.cs";

    private static readonly (string Key, string Wording)[] Lines =
    {
        ("taom_boh_term_days", "These mercenaries agreed to serve for at least {DAYS} more {?IS_PLURAL}days{?}day{\\?}"),
        ("taom_boh_term_unknown", "It is not known how long these mercenaries will serve"),
    };

    public static IEnumerable<object[]> Keys() => Lines.Select(l => new object[] { l.Key, l.Wording });

    // The literal as C# spells it: the plural marker's backslash is escaped in the source.
    private static string LiteralInTheTexts(string key)
    {
        var src = RepoPaths.ReadSource(TextsFile, stripComments: true);
        var m = Regex.Match(src, @"new TextObject\(""(\{=" + key + @"\}(?:\\.|[^""\\])*)""\)");
        Assert.IsTrue(m.Success, "the texts class must build a line from the {=" + key + "} literal");
        return m.Groups[1].Value.Replace("\\\\", "\\");
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
        Assert.AreEqual("{=" + key + "}" + wording, LiteralInTheTexts(key));
    }

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void TheEnglishRow_EqualsTheLiteral(string key, string wording)
    {
        var rows = Rows("Main/_Module/ModuleData/taom_module_strings.xml", key);

        Assert.AreEqual(1, rows.Count, "one English row");
        Assert.AreEqual(LiteralInTheTexts(key), rows[0]);
    }

    private static List<string> Tokens(string text) =>
        Regex.Matches(text, @"\{[^{}]*\}").Cast<Match>().Select(m => m.Value).Distinct().OrderBy(t => t, System.StringComparer.Ordinal).ToList();

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void EveryLanguageFile_HoldsOneRow_KeepingTheEnglishPlaceholders(string key, string wording)
    {
        var english = LiteralInTheTexts(key).Substring(("{=" + key + "}").Length);
        var languages = Directory.GetDirectories(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"));
        Assert.AreEqual(12, languages.Length);
        foreach (var dir in languages)
        {
            var file = Directory.GetFiles(dir, "std_taom_module_strings_*.xml").Single();
            var rows = Rows("Main/_Module/ModuleData/Languages/" + Path.GetFileName(dir) + "/" + Path.GetFileName(file), key);

            Assert.AreEqual(1, rows.Count, Path.GetFileName(file));
            CollectionAssert.AreEqual(Tokens(english), Tokens(rows[0]), Path.GetFileName(file) + ": the row must keep the English placeholders");
        }
    }

    [TestMethod]
    public void TheWording_NamesNoGender_NamesNoPlayer_AndCarriesNoDash()
    {
        foreach (var (_, wording) in Lines)
        {
            Assert.IsFalse(Regex.IsMatch(wording, @"\b(he|she|his|her|him|hers|man|woman|men|women)\b", RegexOptions.IgnoreCase), wording);
            Assert.IsFalse(Regex.IsMatch(wording, @"\b(you|your|yours|I|we|our)\b", RegexOptions.IgnoreCase), "the subject is the mercenaries: " + wording);
            Assert.IsTrue(wording.Contains("mercenaries"), "the subject is the mercenaries: " + wording);
            Assert.IsFalse(wording.Contains('\u2014') || wording.Contains('\u2013'), wording);
        }
    }

    [TestMethod]
    public void TheDaysLine_UsesTheDaysAndPluralVariables_AndTheTextsBuildExactlyTwoLines()
    {
        var src = RepoPaths.ReadSource(TextsFile, stripComments: true);

        StringAssert.Contains(src, "SetTextVariable(\"DAYS\", term.Days)");
        StringAssert.Contains(src, "SetTextVariable(\"IS_PLURAL\", term.Days == 1 ? 0 : 1)");
        Assert.AreEqual(2, Regex.Matches(src, @"new TextObject\(").Count, "two lines");
    }
}
