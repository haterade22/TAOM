using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Conversation.Tags;
using TAOM.Composition;

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

        foreach (var row in TaomLiegeRows())
        {
            foreach (var word in CalradianWords)
                StringAssert.DoesNotMatch((string)row.Attribute("text"), new Regex(word),
                    $"{(string)row.Attribute("id")} carries the vanilla Calradian name '{word}'");
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

    private static readonly string[] VanillaTemplateCultures =
        ExpectedTitles.Select(t => t.Culture).Distinct().ToArray();

    private static IEnumerable<XElement> TaomLiegeRows() =>
        XDocument.Load(Path.Combine(ModuleDataPath, "taom_module_strings.xml")).Descendants("string")
            .Where(s => ((string)s.Attribute("id") ?? "").StartsWith("str_liege_title"));

    private static string[] KingdomCultures() =>
        new[] { "taom_spkingdoms.xml", "spkingdoms.xslt" }
            .SelectMany(f => Regex.Matches(File.ReadAllText(Path.Combine(ModuleDataPath, f)), @"culture=""Culture\.([a-z_0-9]+)""")
                .Cast<Match>().Select(m => m.Groups[1].Value))
            .Distinct().OrderBy(c => c, StringComparer.Ordinal).ToArray();

    [TestMethod]
    public void EveryKingdomCulture_HasMaleAndFemaleLiegeTitle()
    {
        // GameTexts.FindText("str_liege_title[_female]", cultureId) reads the coronation title and
        // returns an engine error string for an id suffix that no row defines (#955).
        var cultures = KingdomCultures();
        Assert.IsTrue(cultures.Length >= 20, $"found only {cultures.Length} kingdom cultures; the scan shape changed");

        var stylesheet = LoadCommentStringsXslt();
        var rows = TaomLiegeRows().ToDictionary(r => (string)r.Attribute("id"), r => r);
        var problems = new List<string>();

        foreach (var culture in cultures)
            foreach (var female in new[] { false, true })
            {
                var suffix = female ? "_female" : "";
                var id = $"str_liege_title{suffix}.{culture}";

                if (VanillaTemplateCultures.Contains(culture))
                {
                    if (FindTemplate(stylesheet, MatchExpression(culture, female)) == null)
                        problems.Add($"{culture}: no comment_strings.xslt template for {id}");
                    continue;
                }

                if (!rows.TryGetValue(id, out var row))
                {
                    problems.Add($"{culture}: no taom_module_strings.xml row {id}");
                    continue;
                }

                var key = $"taom_str_liege_title{suffix}.{culture}";
                if (!((string)row.Attribute("text") ?? "").StartsWith("{=" + key + "}"))
                    problems.Add($"{id}: text must start with {{={key}}}");

                var tags = row.Descendants("tag").Select(x => (string)x.Attribute("tag_name")).ToArray();
                if (!tags.SequenceEqual(new[] { "taom_culture_" + culture }))
                    problems.Add($"{id}: tags must be exactly [taom_culture_{culture}], found [{string.Join(", ", tags)}]");
            }

        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void EveryTaomConversationTag_IsInstantiableAndUnique()
    {
        // ConversationManager.InitializeTags, run by the ConversationManager constructor at every
        // campaign start and save load, calls Activator.CreateInstance on every subclass of
        // ConversationTag in TAOM.dll and Dictionary.Add on its StringId: an abstract type, a type
        // without a public parameterless constructor or a duplicate id breaks every campaign start.
        var taomTypes = AccessTools.GetTypesFromAssembly(typeof(FeatureModules).Assembly)
            .Where(t => t.IsSubclassOf(typeof(ConversationTag))).ToArray();
        var vanillaIds = new HashSet<string>(AccessTools.GetTypesFromAssembly(typeof(ConversationTag).Assembly)
            .Where(t => t.IsSubclassOf(typeof(ConversationTag)) && !t.IsAbstract)
            .Select(t => ((ConversationTag)Activator.CreateInstance(t)).StringId));
        Assert.IsTrue(vanillaIds.Count > 10, "found no vanilla conversation tags; the scan shape changed");

        var ids = new HashSet<string>();
        foreach (var type in taomTypes)
        {
            Assert.IsFalse(type.IsAbstract, $"{type.Name} is abstract; Activator.CreateInstance would throw");
            Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), $"{type.Name} needs a public parameterless constructor");
            var id = ((ConversationTag)Activator.CreateInstance(type)).StringId;
            Assert.IsFalse(vanillaIds.Contains(id), $"{type.Name} reuses vanilla tag id {id}");
            Assert.IsTrue(ids.Add(id), $"{type.Name} duplicates tag id {id}");
        }

        var wanted = TaomLiegeRows().SelectMany(r => r.Descendants("tag"))
            .Select(x => (string)x.Attribute("tag_name")).Distinct().ToArray();
        Assert.IsTrue(wanted.Length >= 16, $"found only {wanted.Length} tag names in the new rows");
        foreach (var name in wanted)
            Assert.IsTrue(ids.Contains(name), $"row tag {name} has no ConversationTag class in TAOM.dll");
    }

    [TestMethod]
    public void DunlandCoronationLine_ReplacesVanillaEmperorLine()
    {
        // The engine reads str_become_king_empire for the empire culture and never asks for a
        // liege title, so vanilla's "Emperor of Calradia" line needs its own replacement (#955).
        var stylesheet = XDocument.Load(Path.Combine(ModuleDataPath, "module_strings.xslt")).Root;
        var template = FindTemplate(stylesheet, "string[@id='str_become_king_empire']");
        Assert.IsNotNull(template, "module_strings.xslt does not remove vanilla str_become_king_empire");
        Assert.IsFalse(template.HasElements || template.Nodes().Any(), "the removal template must be empty");

        var row = XDocument.Load(Path.Combine(ModuleDataPath, "taom_module_strings.xml")).Descendants("string")
            .FirstOrDefault(s => (string)s.Attribute("id") == "str_become_king_empire");
        Assert.IsNotNull(row, "taom_module_strings.xml has no str_become_king_empire row");
        var text = (string)row.Attribute("text");
        StringAssert.StartsWith(text, "{=taom_str_become_king_empire}");
        StringAssert.DoesNotMatch(text, new Regex("Calradia"));
        foreach (var variable in new[] { "{DAY_OF_YEAR}", "{YEAR}", "{KING_NAME}", "{?IS_KING_MALE}" })
            StringAssert.Contains(text, variable);
    }
}
