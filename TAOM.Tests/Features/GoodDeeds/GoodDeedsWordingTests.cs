using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.GoodDeeds;

/// <summary>
/// The one player-facing line Good Deeds adds. The C# literal is the English source, the English row in
/// <c>taom_module_strings.xml</c> must equal it, and each of the twelve language files carries one row (its translation, or
/// the English default until the paid run fills it) that keeps the <c>{TOWN}</c> placeholder and not the old "honour" spelling.
/// </summary>
[TestClass]
public class GoodDeedsWordingTests
{
    private const string Key = "taom_gooddeeds_tournament";
    private const string Wording = "The people of {TOWN} honor your victory.";
    private const string OldSpelling = "honour";

    private static string LiteralInTheAdapter()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/GoodDeedsNoticeAdapter.cs", stripComments: true);
        var m = Regex.Match(src, @"new TextObject\(""(\{=" + Key + @"\}[^""]*)""\)");
        Assert.IsTrue(m.Success, "the notice adapter must build its line from the {=" + Key + "} literal");
        return m.Groups[1].Value;
    }

    private static List<string> Rows(string relativePath)
    {
        var text = File.ReadAllText(RepoPaths.RepoPath(relativePath.Split('/')));
        return Regex.Matches(text, @"<string\s+id=""" + Key + @"""\s+text=""([^""]*)""")
            .Cast<Match>()
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToList();
    }

    [TestMethod]
    public void TheLiteral_IsTheAmericanSpelling_WithItsKey()
    {
        Assert.AreEqual("{=" + Key + "}" + Wording, LiteralInTheAdapter());
    }

    [TestMethod]
    public void TheEnglishRow_EqualsTheLiteral()
    {
        var rows = Rows("Main/_Module/ModuleData/taom_module_strings.xml");

        Assert.AreEqual(1, rows.Count, "one English row");
        Assert.AreEqual(LiteralInTheAdapter(), rows[0]);
    }

    [TestMethod]
    public void EveryLanguageFile_HoldsOneRow_KeepingTheTownPlaceholder_NoneStale()
    {
        var languages = Directory.GetDirectories(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"));
        Assert.AreEqual(12, languages.Length);
        foreach (var dir in languages)
        {
            var file = Directory.GetFiles(dir, "std_taom_module_strings_*.xml").Single();
            var rows = Rows("Main/_Module/ModuleData/Languages/" + Path.GetFileName(dir) + "/" + Path.GetFileName(file));

            Assert.AreEqual(1, rows.Count, Path.GetFileName(file));
            StringAssert.Contains(rows[0], "{TOWN}", Path.GetFileName(file) + ": the row must keep the {TOWN} placeholder");
            Assert.IsFalse(rows[0].Contains(OldSpelling), Path.GetFileName(file) + ": a stale row");
        }
    }

    [TestMethod]
    public void TheNoticeWording_NamesNoGender_AndNoOldSpellingAnywhereInTheFeature()
    {
        Assert.IsFalse(Regex.IsMatch(Wording, @"\b(he|she|his|her|him|hers)\b", RegexOptions.IgnoreCase));
        foreach (var file in new[] { "Main/Adapters/GoodDeedsNoticeAdapter.cs", "Main/Adapters/IGoodDeedsNoticeAdapter.cs", "Main/Features/GoodDeeds/GoodDeedsService.cs" })
            Assert.IsFalse(RepoPaths.ReadSource(file).Contains("Honour"), file + " still carries the old spelling");
    }
}
