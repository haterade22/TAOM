using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Features.FactionUI;

/// <summary>
/// Kysaro's prefabs carry their fixed labels as <c>LocalizedText="{=key}English"</c> (#704). Gauntlet
/// shows a literal attribute as written, so the attribute is localized only on the two widgets that
/// resolve it through <c>TextObject</c>, and only when its key is registered: an unregistered key has
/// no row in any language file, so it stays English for every player.
/// </summary>
[TestClass]
public class FactionUIPrefabLocalizationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", ".."));

    /// <summary>Each localizing attribute and the TAOM widgets whose setter resolves it.</summary>
    private static readonly Dictionary<string, HashSet<string>> ResolvingWidgets =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["LocalizedText"] = new HashSet<string>(StringComparer.Ordinal) { "FrontEndTextWidget", "FrontEndRichTextWidget" },
            ["LocalizedDefaultSearchText"] = new HashSet<string>(StringComparer.Ordinal) { "FrontEndEditableTextWidget" },
        };

    /// <summary>The attributes Gauntlet draws as written, so a literal word in one stays English.</summary>
    private static readonly string[] ShownAttributes = { "Text", "DefaultSearchText" };

    /// <summary>
    /// Literal values that are not words: the career rank numerals. Single letters, signs and
    /// <c>*Parameter</c> or <c>@Binding</c> values need no entry.
    /// </summary>
    private static readonly HashSet<string> NotWords = new HashSet<string>(StringComparer.Ordinal) { "II", "III" };

    private static IEnumerable<string> PrefabFiles() =>
        Directory.GetFiles(Path.Combine(RepoRoot, "Main", "_Module", "GUI", "PreFabs"), "*.xml", SearchOption.AllDirectories);

    private static IEnumerable<(string File, XElement Element, string Attribute, string Value)> LocalizedTexts()
    {
        foreach (var file in PrefabFiles())
        {
            foreach (var element in XDocument.Load(file).Descendants())
            {
                foreach (var name in ResolvingWidgets.Keys)
                {
                    var attribute = element.Attribute(name);
                    if (attribute != null)
                        yield return (Path.GetFileName(file), element, name, attribute.Value);
                }
            }
        }
    }

    [TestMethod]
    public void LocalizedText_OnlyOnTheWidgetsThatResolveIt()
    {
        var all = LocalizedTexts().ToList();
        Assert.IsTrue(all.Count > 40, $"Only {all.Count} localized attributes found; the scan is broken.");

        var offenders = all
            .Where(t => !ResolvingWidgets[t.Attribute].Contains(t.Element.Name.LocalName))
            .Select(t => $"{t.File}: <{t.Element.Name.LocalName} {t.Attribute}=\"{t.Value}\">")
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            "A localized attribute is read only by the widgets in ResolvingWidgets; on any other widget the label is blank:\n"
            + string.Join("\n", offenders));
    }

    /// <summary>
    /// The CN team copied whole prefabs only to translate "PAUSED", "History" and seven culture-stage
    /// labels (#1385). A word in a literal <c>Text</c> or <c>DefaultSearchText</c> stays English in
    /// every language: use <c>LocalizedText</c> on a FrontEnd widget, or a binding.
    /// </summary>
    [TestMethod]
    public void Prefabs_ShowNoLiteralEnglishWord()
    {
        var scanned = 0;
        var offenders = new List<string>();
        foreach (var file in PrefabFiles())
        {
            foreach (var element in XDocument.Load(file).Descendants())
            {
                foreach (var name in ShownAttributes)
                {
                    var value = (string?)element.Attribute(name);
                    if (value == null)
                        continue;
                    scanned++;
                    if (value.StartsWith("@", StringComparison.Ordinal) || value.StartsWith("*", StringComparison.Ordinal)
                        || NotWords.Contains(value) || !HasWord(value))
                        continue;
                    offenders.Add($"{Path.GetFileName(file)}: <{element.Name.LocalName} {name}=\"{value}\">");
                }
            }
        }

        Assert.IsTrue(scanned > 200, $"Only {scanned} Text attributes found; the scan is broken.");
        Assert.AreEqual(0, offenders.Count, "Literal English in a prefab:\n" + string.Join("\n", offenders));
    }

    private static bool HasWord(string value)
    {
        var run = 0;
        foreach (var c in value)
        {
            run = char.IsLetter(c) ? run + 1 : 0;
            if (run >= 2)
                return true;
        }
        return false;
    }

    [TestMethod]
    public void LocalizedText_EveryKeyIsRegisteredWithTheSameEnglish()
    {
        var stringsFile = Path.Combine(RepoRoot, "Main", "_Module", "ModuleData", "taom_module_strings.xml");
        var registered = XDocument.Load(stringsFile).Descendants("string")
            .GroupBy(s => (string)s.Attribute("id"))
            .ToDictionary(g => g.Key, g => (string)g.First().Attribute("text"), StringComparer.Ordinal);

        var offenders = new List<string>();
        foreach (var (file, _, _, value) in LocalizedTexts())
        {
            var close = value.IndexOf('}');
            if (!value.StartsWith("{=", StringComparison.Ordinal) || close < 3)
            {
                offenders.Add($"{file}: \"{value}\" is not {{=key}}English");
                continue;
            }

            var key = value.Substring(2, close - 2);
            if (!registered.TryGetValue(key, out var text))
                offenders.Add($"{file}: {key} is not registered in taom_module_strings.xml");
            else if (text != value)
                offenders.Add($"{file}: {key} reads \"{value}\" here but \"{text}\" in taom_module_strings.xml");
        }

        Assert.AreEqual(0, offenders.Count, string.Join("\n", offenders));
    }
}
