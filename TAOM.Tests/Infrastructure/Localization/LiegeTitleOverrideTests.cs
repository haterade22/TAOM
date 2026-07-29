using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TAOM.Tests.Infrastructure.Localization;

/// <summary>
/// Guards TAOM's overrides of vanilla <c>str_liege_title</c> / <c>str_liege_title_female</c>.
///
/// The engine picks these by conversation tag (<c>VlandianTag</c> etc. -> <c>Culture.StringId</c>),
/// not by kingdom name, so renaming the kingdom in spkingdoms.xslt does NOT change the line a ruler
/// speaks. Without an override, Théoden introduces himself as "king of the Vlandians".
/// </summary>
[TestClass]
public class LiegeTitleOverrideTests
{
    private static string ModuleDataPath => Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            @"..\..\..\..\Main\_Module\ModuleData"));

    private static readonly XNamespace Xsl = "http://www.w3.org/1999/XSL/Transform";

    /// <summary>Vanilla culture id -> the TAOM liege title its ruler must speak.</summary>
    private static readonly (string Culture, string Key, string Text, bool Female)[] ExpectedTitles =
    {
        ("empire",   "TAOM_liege_dunland",        "Brenin of Dunland",    false),
        ("empire",   "TAOM_liege_dunland_female", "Brenhines of Dunland", true),
        ("sturgia",  "TAOM_liege_dale",           "King of Dale",         false),
        ("sturgia",  "TAOM_liege_dale_female",    "Queen of Dale",        true),
        ("aserai",   "TAOM_liege_harad",          "Taskral of Harad",     false),
        ("aserai",   "TAOM_liege_harad_female",   "Taskral of Harad",     true),
        ("battania", "TAOM_liege_khand",          "Khudriag of Khand",    false),
        ("battania", "TAOM_liege_khand_female",   "Khudriag of Khand",    true),
        ("vlandia",  "TAOM_liege_rohan",          "King of the Mark",     false),
        ("vlandia",  "TAOM_liege_rohan_female",   "Queen of the Mark",    true),
        ("khuzait",  "TAOM_liege_rhun",           "Loke-Kan of Rhun",     false),
        ("khuzait",  "TAOM_liege_rhun_female",    "Loke-Kan of Rhun",     true),
    };

    private static readonly string[] CalradianWords =
    {
        "Vlandia", "Calradia", "Calradian", "Sturgian", "Aserai", "Battanian", "Khuzait", "Empire"
    };

    private static string MatchExpression(string culture, bool female) =>
        $"string[@id='str_liege_title{(female ? "_female" : "")}.{culture}']";

    private static XElement LoadCommentStringsXslt() =>
        XDocument.Load(Path.Combine(ModuleDataPath, "comment_strings.xslt")).Root;

    private static XElement FindTemplate(XElement stylesheet, string match) =>
        stylesheet.Elements(Xsl + "template")
            .FirstOrDefault(t => (string)t.Attribute("match") == match);

    [TestMethod]
    public void EveryLiegeTitle_HasAnOverrideTemplateWithTheExpectedText()
    {
        var stylesheet = LoadCommentStringsXslt();

        foreach (var (culture, key, text, female) in ExpectedTitles)
        {
            var match = MatchExpression(culture, female);
            var template = FindTemplate(stylesheet, match);
            Assert.IsNotNull(template, $"comment_strings.xslt has no template for {match}");

            var textAttribute = template.Descendants(Xsl + "attribute")
                .FirstOrDefault(a => (string)a.Attribute("name") == "text");
            Assert.IsNotNull(textAttribute, $"{match} template does not set a 'text' attribute");

            Assert.AreEqual($"{{={key}}}{text}", textAttribute.Value,
                $"{match} template has the wrong liege title");
        }
    }

    [TestMethod]
    public void EveryLiegeTitleTemplate_PreservesChildNodes()
    {
        // The vanilla <tags><tag tag_name="VlandianTag"/></tags> child selects the variation.
        // xsl:copy does not copy children, so the template must apply-templates over node().
        // A tag-stripped variation scores 0 for EVERY culture and collides with the other five.
        var stylesheet = LoadCommentStringsXslt();

        foreach (var (culture, _, _, female) in ExpectedTitles)
        {
            var match = MatchExpression(culture, female);
            var template = FindTemplate(stylesheet, match);
            Assert.IsNotNull(template, $"comment_strings.xslt has no template for {match}");

            var copiesChildren = template.Descendants(Xsl + "apply-templates")
                .Any(a => (string)a.Attribute("select") == "node()");
            Assert.IsTrue(copiesChildren,
                $"{match} template must <xsl:apply-templates select=\"node()\"/> or it drops the <tags> child");
        }
    }

    [TestMethod]
    public void NoLiegeTitle_MentionsCalradia()
    {
        var stylesheet = LoadCommentStringsXslt();

        foreach (var (culture, _, _, female) in ExpectedTitles)
        {
            var match = MatchExpression(culture, female);
            var template = FindTemplate(stylesheet, match);
            Assert.IsNotNull(template, $"comment_strings.xslt has no template for {match}");

            foreach (var word in CalradianWords)
            {
                StringAssert.DoesNotMatch(template.Value,
                    new System.Text.RegularExpressions.Regex(word),
                    $"{match} still carries the vanilla Calradian name '{word}'");
            }
        }
    }

    [TestMethod]
    public void EveryLiegeTitleKey_IsRegisteredForTranslation()
    {
        var strings = XDocument.Load(Path.Combine(ModuleDataPath, "taom_xslt_strings.xml")).Root;

        foreach (var (_, key, text, _) in ExpectedTitles)
        {
            var entry = strings.Elements("string")
                .FirstOrDefault(s => (string)s.Attribute("id") == key);
            Assert.IsNotNull(entry, $"taom_xslt_strings.xml does not register '{key}'");
            Assert.AreEqual($"{{={key}}}{text}", (string)entry.Attribute("text"),
                $"taom_xslt_strings.xml has the wrong default text for '{key}'");
        }
    }
}
