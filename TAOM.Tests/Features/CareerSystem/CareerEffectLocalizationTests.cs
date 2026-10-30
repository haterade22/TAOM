using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Domain;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// #769 E9: the Active Effects panel's text is translatable. Every effect label is a <c>{=taom_career_effect_*}</c>
/// literal that carries its number as a <c>{VALUE}</c> variable (so a language can put the number after the noun),
/// the value is one of two keys (<c>{NUMBER}%</c>, <c>{NUMBER}</c>), and the class suffix is six whole phrases
/// rather than fragments glued with a join key, because word order and number agreement differ across the 12
/// languages. Source and strings-file pins only, so they run without the game; the rendered English is pinned in
/// <see cref="CareerEffectDisplayMapTests"/>.
/// </summary>
[TestClass]
public class CareerEffectLocalizationTests
{
    private const string MapSource = "Main/Features/CareerSystem/UI/CareerEffectDisplayMap.cs";
    private const string ModuleStrings = "Main/_Module/ModuleData/taom_module_strings.xml";

    private static readonly string[] ClassPhraseKeys =
    {
        "taom_career_class_line_infantry",
        "taom_career_class_line_ranged",
        "taom_career_class_line_cavalry",
        "taom_career_class_line_infantry_ranged",
        "taom_career_class_line_infantry_cavalry",
        "taom_career_class_line_ranged_cavalry",
    };

    private static readonly string[] ValueKeys =
    {
        "taom_career_value_percent",
        "taom_career_value_flat",
    };

    private static readonly string[] RetiredKeys =
    {
        "taom_career_class_infantry",
        "taom_career_class_ranged",
        "taom_career_class_cavalry",
        "taom_career_class_join",
        "taom_career_class_line",
    };

    private static string SnakeCase(string name)
        => Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();

    private static string EffectKey(PassiveEffectType type) => "taom_career_effect_" + SnakeCase(type.ToString());

    private static string[] EffectKeys()
        => Enum.GetValues(typeof(PassiveEffectType)).Cast<PassiveEffectType>().Select(EffectKey).ToArray();

    // The text of one <string id=...> row, read the way the engine does: the text attribute.
    private static string RowText(string xml, string key)
    {
        var m = Regex.Match(xml, "<string id=\"" + Regex.Escape(key) + "\" text=\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : null;
    }

    private static string[] LanguageStringsFiles()
        => Directory.GetFiles(RepoPath("Main", "_Module", "ModuleData", "Languages"), "std_taom_module_strings_*.xml", SearchOption.AllDirectories);

    [TestMethod]
    public void EveryEffectLabel_IsAKeyedLiteralNamedAfterItsType()
    {
        var source = ReadSource(MapSource, stripComments: true);

        var labels = Regex.Matches(source, "\\[PassiveEffectType\\.(\\w+)\\] = \"\\{=(taom_career_effect_\\w+)\\}\\{VALUE\\} ([^\"]+)\"")
            .Cast<Match>()
            .ToDictionary(m => (PassiveEffectType)Enum.Parse(typeof(PassiveEffectType), m.Groups[1].Value), m => m.Groups[2].Value);

        foreach (PassiveEffectType type in Enum.GetValues(typeof(PassiveEffectType)))
        {
            Assert.IsTrue(labels.ContainsKey(type), $"{type} has no keyed label carrying {{VALUE}} in CareerEffectDisplayMap.Labels");
            Assert.AreEqual(EffectKey(type), labels[type], $"{type}'s label key");
        }
        Assert.AreEqual(Enum.GetValues(typeof(PassiveEffectType)).Length, labels.Count);
    }

    [TestMethod]
    public void NoLabelIsAPlainEnglishLiteral()
    {
        var source = ReadSource(MapSource, stripComments: true);

        Assert.IsFalse(Regex.IsMatch(source, "\\[PassiveEffectType\\.\\w+\\] = \"(?!\\{=taom_career_effect_)[a-z][^\"]*\""),
            "a Labels row is plain English; make it a {=taom_career_effect_<type>} literal");
    }

    [TestMethod]
    public void TheValue_IsOneOfTwoKeys_WithTheSignedNumberAsAVariable()
    {
        var source = ReadSource(MapSource, stripComments: true);

        StringAssert.Contains(source, "\"{=taom_career_value_percent}{NUMBER}%\"");
        StringAssert.Contains(source, "\"{=taom_career_value_flat}{NUMBER}\"");
    }

    [TestMethod]
    public void TheClassSuffix_IsSixWholePhrases_WithTheEffectAsAVariable()
    {
        var source = ReadSource(MapSource, stripComments: true);

        StringAssert.Contains(source, "{=taom_career_class_line_infantry}{EFFECT} for infantry");
        StringAssert.Contains(source, "{=taom_career_class_line_ranged}{EFFECT} for ranged troops");
        StringAssert.Contains(source, "{=taom_career_class_line_cavalry}{EFFECT} for cavalry");
        StringAssert.Contains(source, "{=taom_career_class_line_infantry_ranged}{EFFECT} for infantry and ranged troops");
        StringAssert.Contains(source, "{=taom_career_class_line_infantry_cavalry}{EFFECT} for infantry and cavalry");
        StringAssert.Contains(source, "{=taom_career_class_line_ranged_cavalry}{EFFECT} for ranged troops and cavalry");
    }

    [TestMethod]
    public void TheFragmentKeys_AreGoneFromTheSource()
    {
        var source = ReadSource(MapSource, stripComments: true);

        foreach (var key in RetiredKeys)
            Assert.IsFalse(Regex.IsMatch(source, "\\{=" + key + "\\}"), $"{key} is retired: the suffix is whole phrases now");
    }

    [TestMethod]
    public void EveryNewKey_IsRegisteredInTheModuleStrings_AndTheRetiredOnesAreNot()
    {
        var strings = ReadSource(ModuleStrings);
        var keys = EffectKeys().Concat(ClassPhraseKeys).Concat(ValueKeys);

        foreach (var key in keys)
            StringAssert.Contains(strings, $"<string id=\"{key}\"", $"{key} is not registered");
        foreach (var key in EffectKeys())
            StringAssert.Contains(RowText(strings, key), "{VALUE}", $"{key}'s registered English must carry the {{VALUE}} variable");
        foreach (var key in ValueKeys)
            StringAssert.Contains(RowText(strings, key), "{NUMBER}", $"{key}'s registered English must carry the {{NUMBER}} variable");
        foreach (var key in RetiredKeys)
            Assert.IsFalse(strings.Contains($"<string id=\"{key}\""), $"{key} is retired and must be removed");
    }

    [TestMethod]
    public void EveryLanguageFile_CarriesTheNewKeys_AndNoRetiredOne()
    {
        var files = LanguageStringsFiles();
        Assert.AreEqual(12, files.Length, "the TAOM module strings exist in 12 languages");
        var keys = EffectKeys().Concat(ClassPhraseKeys).Concat(ValueKeys).ToList();

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var missing = keys.Where(k => !text.Contains($"<string id=\"{k}\"")).ToList();
            Assert.AreEqual(0, missing.Count, $"{Path.GetFileName(file)} lacks: {string.Join(", ", missing)}");
            // A translation may move the variable, never drop it: the engine renders a missing one as nothing.
            var noValue = EffectKeys().Where(k => !(RowText(text, k) ?? "").Contains("{VALUE}")).ToList();
            Assert.AreEqual(0, noValue.Count, $"{Path.GetFileName(file)}: no {{VALUE}} in {string.Join(", ", noValue)}");
            var noNumber = ValueKeys.Where(k => !(RowText(text, k) ?? "").Contains("{NUMBER}")).ToList();
            Assert.AreEqual(0, noNumber.Count, $"{Path.GetFileName(file)}: no {{NUMBER}} in {string.Join(", ", noNumber)}");
            var retired = RetiredKeys.Where(k => text.Contains($"<string id=\"{k}\"")).ToList();
            Assert.AreEqual(0, retired.Count, $"{Path.GetFileName(file)} still carries: {string.Join(", ", retired)}");
        }
    }

    [TestMethod]
    public void TheModuleStrings_EndWithOneBlankLineBeforeTheClosingTag()
    {
        // tools/harvest_literal_loc_keys.py --apply adds one blank line per run (open issue #781), so a harvest leaves
        // two before </strings>. Until #781 is fixed, delete the extra blank line by hand after every harvest.
        var strings = ReadSource(ModuleStrings);

        Assert.IsTrue(Regex.IsMatch(strings, "[^\\n]\\n\\n</strings>\\s*$"),
            "exactly one blank line may precede </strings>: the harvester (open issue #781) adds one per run; delete the extra blank line by hand");
    }
}
