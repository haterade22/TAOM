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

    // Peoples, places and the Senate match in any case. The Empire words are case-sensitive and whole-word
    // ("forgotten empires" is TAOM prose), and the coin is the singular "denar" (TAOM's own rows say "denars").
    private static readonly Regex CalradianWord = new(
        @"(?i:Calradi|Sturgia|Battania|Vlandia|Aserai|Khuzait|Khanate|Senate|Vortios)|\b(?:Empire|Imperial|Emperor)\b|\bdenar\b");

    private static readonly Regex KeyPrefix = new(@"^\{=[^}]*\}");

    private static string GameDir()
    {
        string? env = Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR");
        return string.IsNullOrWhiteSpace(env) ? @"E:\Steam\steamapps\common\Mount & Blade II Bannerlord" : env;
    }

    private static string VanillaCommentStrings =>
        Path.Combine(GameDir(), "Modules", "SandBox", "ModuleData", "comment_strings.xml");

    private static string WithoutKey(string text) => KeyPrefix.Replace(text, "");

    private static readonly Regex TaomKey = new(@"^\{=(taom_lord_intro_[a-z_]+_\d+)\}(.+)$");

    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    private static Dictionary<string, string> Transform(XDocument input)
    {
        var transform = CompiledXslt.Get(Path.Combine(ModuleData(), "comment_strings.xslt"), XsltLoadStyle.FrameworkDefaults);
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(input.CreateReader(), null, writer);
        return output.Descendants("string").ToDictionary(s => (string)s.Attribute("id"), s => (string)s.Attribute("text"));
    }

    private static Dictionary<string, string> TransformedTexts()
    {
        var strings = new XElement("strings");
        foreach (var id in MustBeReplaced)
            strings.Add(new XElement("string", new XAttribute("id", id), new XAttribute("text", "SENTINEL Calradia"),
                new XElement("tags", new XElement("tag", new XAttribute("tag_name", "SENTINEL_TAG")))));
        return Transform(new XDocument(new XElement("base", strings)));
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
            else if (CalradianWord.IsMatch(WithoutKey(text)))
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
            .Where(a => (string)a.Attribute("name") == "text" && CalradianWord.IsMatch(WithoutKey(a.Value)))
            .Select(a => a.Value)
            .ToList();

        Assert.AreEqual(0, offenders.Count, "Calradian names in comment_strings.xslt:\n  " + string.Join("\n  ", offenders));
    }

    [TestMethod]
    [DataRow("I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the ancient lineages of the Sturgians.")]
    [DataRow("Our family has stood in the Senate since the first days of the Empire.")]
    [DataRow("We received our title from the Emperor Vortios himself.")]
    [DataRow("Our ancestors came to take the princes' denar.")]
    [DataRow("Our family has served the Khuzait Khanate for generations.")]
    [DataRow("A clan of the Aserai, in the hills of Battania.")]
    [DataRow("Loyal to the Imperial throne.")]
    public void CalradianWord_RealCalradianNameOrCoin_IsFlagged(string text)
    {
        Assert.IsTrue(CalradianWord.IsMatch(text), text);
    }

    [TestMethod]
    [DataRow("{=aom_empire_name}Pay {PRICE} denars.")]
    [DataRow("{=aom_aserai_desc}The red dust of forgotten empires.")]
    [DataRow("{=x}Before the old empire fell.")]
    [DataRow("{=taom_lord_intro_dale_1}I am {CONVERSATION_CHARACTER.LINK}, of {CLAN_NAME}, one of the old houses of Dale.")]
    public void CalradianWord_TaomOwnWordsAndKeyPrefix_AreNotFlagged(string text)
    {
        Assert.IsFalse(CalradianWord.IsMatch(WithoutKey(text)), text);
    }

    [TestMethod]
    [TestCategory("LiveInstall")]
    public void VanillaComments_EveryReplacedIdExists_AndComesOutWithATaomIntroKey()
    {
        if (!File.Exists(VanillaCommentStrings))
            Assert.Inconclusive("Bannerlord install not found; vanilla comment_strings.xml cannot be transformed here.");

        var vanilla = XDocument.Load(VanillaCommentStrings);
        var vanillaIds = new HashSet<string>(vanilla.Descendants("string").Select(s => (string)s.Attribute("id")), StringComparer.Ordinal);
        var transformed = Transform(vanilla);
        var offenders = new List<string>();
        foreach (var id in MustBeReplaced)
        {
            if (!vanillaIds.Contains(id))
                offenders.Add($"{id}: not in the installed vanilla file (renamed or removed by an engine update)");
            else if (!transformed.TryGetValue(id, out var text) || !TaomKey.IsMatch(text))
                offenders.Add($"{id}: comes out of the stylesheet as '{(transformed.TryGetValue(id, out var t) ? t : "(missing)")}'");
        }

        Assert.AreEqual(0, offenders.Count, string.Join(Environment.NewLine, offenders));
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
