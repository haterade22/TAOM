using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Pins that lords introduce themselves in Middle-earth terms.
///
/// <para>The six vanilla culture ids TAOM retags (empire = Dunland, sturgia = Dale, battania = Khand,
/// vlandia = Rohan, aserai = Harad, khuzait = Rhun) still match vanilla's culture-tagged
/// <c>str_comment_noble_introduces_self_and_clan.*</c> rows, which named the Sturgians, the Senate,
/// Emperor Vortios and the Khuzait Khanate. <c>comment_strings.xslt</c> replaces each with a new TAOM
/// key, so no language keeps the old translation through the vanilla key.</para>
/// </summary>
[TestClass]
public class CommentStringLoreTests
{
    private static readonly string[] MustBeReplaced =
    {
        "str_comment_noble_introduces_self_and_clan.sturgia",
        "str_comment_noble_introduces_self_and_clan.sturgia_boasting",
        "str_comment_noble_introduces_self_and_clan.sturgia_boasting_2",
        "str_comment_noble_introduces_self_and_clan.battania",
        "str_comment_noble_introduces_self_and_clan.battania_boasting",
        "str_comment_noble_introduces_self_and_clan.battania_boasting_harsh",
        "str_comment_noble_introduces_self_and_clan.battania_boasting_ironic",
        "str_comment_noble_introduces_self_and_clan.vlandia",
        "str_comment_noble_introduces_self_and_clan.vlandia_boasting",
        "str_comment_noble_introduces_self_and_clan.vlandia_boasting_ironic",
        "str_comment_noble_introduces_self_and_clan.aserai",
        "str_comment_noble_introduces_self_and_clan.aserai_boasting",
        "str_comment_noble_introduces_self_and_clan.khuzait",
        "str_comment_noble_introduces_self_and_clan.khuzait_boasting",
        "str_comment_noble_introduces_self_and_clan.empire",
        "str_comment_noble_introduces_self_and_clan.empire_honor",
        "str_comment_noble_introduces_self_and_clan.empire_mercy",
        "str_comment_noble_introduces_self_and_clan.empire_boasting",
        "str_comment_noble_introduces_self_and_clan.empire_boasting_cruel",
    };

    private static readonly Regex CalradianWord = new(
        @"Calradi|Sturgia|Battania|Vlandia|Aserai|Khuzait|Khanate|Senate|Empire|Imperial|Emperor|Vortios|denar",
        RegexOptions.IgnoreCase);

    private static readonly Regex TaomKey = new(@"^\{=(taom_lord_intro_[a-z_]+_\d+)\}(.+)$");

    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    private static Dictionary<string, string> TransformedTexts()
    {
        var transform = CompiledXslt.Get(Path.Combine(ModuleData(), "comment_strings.xslt"), XsltLoadStyle.FrameworkDefaults);
        var strings = new XElement("strings");
        foreach (var id in MustBeReplaced)
            strings.Add(new XElement("string", new XAttribute("id", id), new XAttribute("text", "SENTINEL Calradia"),
                new XElement("tags", new XElement("tag", new XAttribute("tag_name", "SENTINEL_TAG")))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(new XDocument(new XElement("base", strings)).CreateReader(), null, writer);
        return output.Descendants("string").ToDictionary(s => (string)s.Attribute("id"), s => (string)s.Attribute("text"));
    }

    [TestMethod]
    public void EveryCalradianIntro_IsReplacedWithATaomKey_AndNamesNoCalradianPeople()
    {
        var texts = TransformedTexts();
        var offenders = new List<string>();
        foreach (var id in MustBeReplaced)
        {
            var text = texts[id];
            if (!TaomKey.IsMatch(text))
                offenders.Add($"{id}: not replaced with a {{=taom_lord_intro_*}} key ('{text}')");
            else if (CalradianWord.IsMatch(text))
                offenders.Add($"{id}: still names Calradia ('{text}')");
        }

        Assert.AreEqual(0, offenders.Count, string.Join(Environment.NewLine, offenders));
    }

    [TestMethod]
    public void NoTextInTheStylesheet_NamesACalradianPeople()
    {
        var xsl = XNamespace.Get("http://www.w3.org/1999/XSL/Transform");
        var offenders = XDocument.Load(Path.Combine(ModuleData(), "comment_strings.xslt"))
            .Descendants(xsl + "attribute")
            .Where(a => (string)a.Attribute("name") == "text" && CalradianWord.IsMatch(a.Value))
            .Select(a => a.Value)
            .ToList();

        Assert.AreEqual(0, offenders.Count, "Calradian names in comment_strings.xslt:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    public void EveryIntroKey_HasAMatchingEnglishRow()
    {
        var english = XDocument.Load(Path.Combine(ModuleData(), "taom_xslt_strings.xml"))
            .Descendants("string")
            .ToDictionary(s => (string)s.Attribute("id"), s => (string)s.Attribute("text"), StringComparer.Ordinal);
        var offenders = new List<string>();
        foreach (var text in TransformedTexts().Values)
        {
            var match = TaomKey.Match(text);
            if (!match.Success)
                continue;
            if (!english.TryGetValue(match.Groups[1].Value, out var row))
                offenders.Add($"{match.Groups[1].Value}: no row in taom_xslt_strings.xml");
            else if (row != text)
                offenders.Add($"{match.Groups[1].Value}: English row differs from the stylesheet");
        }

        Assert.AreEqual(0, offenders.Count, string.Join(Environment.NewLine, offenders));
    }
}
