using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SmithsMeasure;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SmithsMeasure;

/// <summary>
/// The player-facing text of Smith's Measure (#910). The C# literal (or MCM attribute) is the English source and the English row in
/// <c>taom_module_strings.xml</c> must equal it. Each language file holds one row per key (its translation, or the English default
/// until the paid run fills it) that keeps every placeholder of the English. The card speaks of goods and never of the player, and no word is gendered.
/// </summary>
[TestClass]
public class SmithsMeasureWordingTests
{
    private const string TextsFile = "Main/Features/SmithsMeasure/SmithsMeasureTexts.cs";
    private const string SettingsFile = "Main/Features/TaomSettings.cs";

    private static readonly (string File, string Key, string Wording)[] Lines =
    {
        (TextsFile, "taom_sm_make_tools", "Make Tools"),
        (TextsFile, "taom_sm_make_tools_hint", "One Iron and one Hardwood make one Tools. It costs {COST} smithy stamina and earns Smithing experience."),
        (TextsFile, "taom_sm_need_iron", "You need one Iron."),
        (TextsFile, "taom_sm_need_wood", "You need one Hardwood."),
        (SettingsFile, "taom_sm_mcm_cap", "Cap Smelting Yield"),
        (SettingsFile, "taom_sm_mcm_cap_hint", "Smelting a weapon returns at most the number set below of each iron grade, of iron ore and of hardwood, and no hardwood at all when any metal comes out. Charcoal use is unchanged. This curbs the loop of selling looted weapons as iron. Default: on."),
        (SettingsFile, "taom_sm_mcm_cap_value", "Smelting Yield Cap"),
        (SettingsFile, "taom_sm_mcm_cap_value_hint", "The most of each iron grade, of iron ore and of hardwood that one smelt returns while the cap is on. Between 1 and 10; a value outside that range counts as 1. Default: 1."),
        (SettingsFile, "taom_sm_mcm_make_tools", "Enable Make Tools"),
        (SettingsFile, "taom_sm_mcm_make_tools_hint", "Adds a Make Tools button to the Refine tab of the smithing screen: one Iron and one Hardwood make one Tools, for smithy stamina and a little Smithing experience. Not shown in a co-op session. Default: on."),
    };

    public static IEnumerable<object[]> Keys() => Lines.Select(l => new object[] { l.File, l.Key, l.Wording });

    private static string LiteralIn(string file, string key)
    {
        var src = RepoPaths.ReadSource(file, stripComments: true);
        var m = Regex.Match(src, @"""(\{=" + key + @"\}(?:\\.|[^""\\])*)""");
        Assert.IsTrue(m.Success, file + " must carry the {=" + key + "} literal");
        return m.Groups[1].Value;
    }

    private static List<string> Rows(string key)
    {
        var text = File.ReadAllText(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "taom_module_strings.xml"));
        return Regex.Matches(text, @"<string\s+id=""" + key + @"""\s+text=""([^""]*)""")
            .Cast<Match>()
            .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
            .ToList();
    }

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void TheLiteral_IsTheExpectedWording_WithItsKey(string file, string key, string wording)
    {
        Assert.AreEqual("{=" + key + "}" + wording, LiteralIn(file, key));
    }

    [DataTestMethod]
    [DynamicData(nameof(Keys), DynamicDataSourceType.Method)]
    public void TheEnglishRow_EqualsTheLiteral(string file, string key, string wording)
    {
        var rows = Rows(key);

        Assert.AreEqual(1, rows.Count, "one English row for " + key);
        Assert.AreEqual("{=" + key + "}" + wording, rows[0]);
    }

    // The too-tired line is vanilla's own key ({=PRE5RKpp}, translated by SandBox), so TAOM carries no row for it.
    [TestMethod]
    public void TheTooTiredLine_IsVanillasKey_WithNoTaomRow()
    {
        var src = RepoPaths.ReadSource(TextsFile, stripComments: true);

        StringAssert.Contains(src, "{=PRE5RKpp}You must rest and spend time before you can do this action.");
        Assert.IsFalse(src.Contains("taom_sm_too_tired"));
        Assert.AreEqual(0, Rows("taom_sm_too_tired").Count);
    }

    private static List<string> Tokens(string text) =>
        Regex.Matches(text, @"\{[^{}]*\}").Cast<Match>().Select(m => m.Value).Distinct().OrderBy(t => t, System.StringComparer.Ordinal).ToList();

    [TestMethod]
    public void EveryLanguageFile_HoldsOneRow_KeepingTheEnglishPlaceholders()
    {
        var languages = Directory.GetDirectories(RepoPaths.RepoPath("Main", "_Module", "ModuleData", "Languages"));
        Assert.AreEqual(12, languages.Length);
        foreach (var dir in languages)
        {
            var file = Directory.GetFiles(dir, "std_taom_module_strings_*.xml").Single();
            var text = File.ReadAllText(file);
            foreach (var line in Lines)
            {
                var rows = Regex.Matches(text, @"<string\s+id=""" + line.Key + @"""\s+text=""([^""]*)""")
                    .Cast<Match>()
                    .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value))
                    .ToList();

                Assert.AreEqual(1, rows.Count, Path.GetFileName(file) + ": one row of " + line.Key);
                CollectionAssert.AreEqual(Tokens(line.Wording), Tokens(rows[0]), Path.GetFileName(file) + ": " + line.Key + " must keep the English placeholders");
            }
            Assert.IsFalse(text.Contains("id=\"taom_sm_too_tired\""), Path.GetFileName(file) + ": a vanilla key gets no TAOM row");
        }
    }

    [TestMethod]
    public void NoWordingIsGenderedOrUsesADash()
    {
        foreach (var line in Lines)
        {
            StringAssert.DoesNotMatch(line.Wording, new Regex(@"\b(he|she|his|her|him)\b", RegexOptions.IgnoreCase));
            Assert.IsFalse(line.Wording.Contains("\u2014") || line.Wording.Contains("\u2013"), "no em or en dash: " + line.Key);
        }
    }
}
