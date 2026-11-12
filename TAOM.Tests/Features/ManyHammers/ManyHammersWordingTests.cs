using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ManyHammers;

/// <summary>
/// The four MCM strings Many Hammers adds. The attribute literal in <c>TaomSettings</c> is the English source, the English
/// row in <c>taom_module_strings.xml</c> must equal it, and each of the twelve language files carries one row per key (its
/// translation, or the English default until the paid run in #763 fills it) that keeps every placeholder of the English.
/// </summary>
[TestClass]
public class ManyHammersWordingTests
{
    private static readonly (string Property, bool Hint, string Key)[] Strings =
    {
        (nameof(TaomSettings.ManyHammersBulkEnabled), false, "taom_mh_mcm_bulk"),
        (nameof(TaomSettings.ManyHammersBulkEnabled), true, "taom_mh_mcm_bulk_hint"),
        (nameof(TaomSettings.ManyHammersHideLockedEnabled), false, "taom_mh_mcm_hide_locked"),
        (nameof(TaomSettings.ManyHammersHideLockedEnabled), true, "taom_mh_mcm_hide_locked_hint"),
    };

    private static string English((string Property, bool Hint, string Key) s)
    {
        var attribute = typeof(TaomSettings).GetProperty(s.Property)!.GetCustomAttributes(inherit: false)
            .Single(a => a.GetType().Name == "SettingPropertyBoolAttribute");
        var literal = (string)attribute.GetType().GetProperty(s.Hint ? "HintText" : "DisplayName")!.GetValue(attribute)!;
        var prefix = "{=" + s.Key + "}";
        StringAssert.StartsWith(literal, prefix);
        return literal.Substring(prefix.Length);
    }

    private static List<string> Rows(string relativePath, string key)
    {
        var text = File.ReadAllText(RepoPaths.RepoPath(relativePath.Split('/')));
        return Regex.Matches(text, @"<string\s+id=""" + key + @"""\s+text=""([^""]*)""")
            .Cast<Match>()
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToList();
    }

    private static List<string> Tokens(string text) =>
        Regex.Matches(text, @"\{[^{}]*\}").Cast<Match>().Select(m => m.Value).Distinct().OrderBy(t => t, System.StringComparer.Ordinal).ToList();

    [TestMethod]
    public void TheEnglishRows_EqualTheAttributeLiterals()
    {
        foreach (var s in Strings)
        {
            var rows = Rows("Main/_Module/ModuleData/taom_module_strings.xml", s.Key);

            Assert.AreEqual(1, rows.Count, s.Key + ": one English row");
            Assert.AreEqual("{=" + s.Key + "}" + English(s), rows[0], s.Key);
        }
    }

    [TestMethod]
    public void EveryLanguageFile_HoldsOneRowOfEachKey_KeepingTheEnglishPlaceholders()
    {
        var languages = Directory.GetDirectories(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"));
        Assert.AreEqual(12, languages.Length);
        foreach (var dir in languages)
        {
            var file = Directory.GetFiles(dir, "std_taom_module_strings_*.xml").Single();
            var relative = "Main/_Module/ModuleData/Languages/" + Path.GetFileName(dir) + "/" + Path.GetFileName(file);
            foreach (var s in Strings)
            {
                var rows = Rows(relative, s.Key);

                Assert.AreEqual(1, rows.Count, Path.GetFileName(file) + " " + s.Key);
                CollectionAssert.AreEqual(Tokens(English(s)), Tokens(rows[0]), Path.GetFileName(file) + " " + s.Key + ": the row must keep the English placeholders");
            }
        }
    }
}
