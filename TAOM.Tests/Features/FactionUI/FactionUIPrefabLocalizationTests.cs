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

    private static readonly HashSet<string> ResolvingWidgets =
        new HashSet<string>(StringComparer.Ordinal) { "FrontEndTextWidget", "FrontEndRichTextWidget" };

    private static IEnumerable<(string File, XElement Element, string Value)> LocalizedTexts()
    {
        var prefabRoot = Path.Combine(RepoRoot, "Main", "_Module", "GUI", "PreFabs");
        foreach (var file in Directory.GetFiles(prefabRoot, "*.xml", SearchOption.AllDirectories))
        {
            foreach (var element in XDocument.Load(file).Descendants())
            {
                var attribute = element.Attribute("LocalizedText");
                if (attribute != null)
                    yield return (Path.GetFileName(file), element, attribute.Value);
            }
        }
    }

    [TestMethod]
    public void LocalizedText_OnlyOnTheWidgetsThatResolveIt()
    {
        var all = LocalizedTexts().ToList();
        Assert.IsTrue(all.Count > 40, $"Only {all.Count} LocalizedText attributes found; the scan is broken.");

        var offenders = all
            .Where(t => !ResolvingWidgets.Contains(t.Element.Name.LocalName))
            .Select(t => $"{t.File}: <{t.Element.Name.LocalName} LocalizedText=\"{t.Value}\">")
            .ToList();
        Assert.AreEqual(0, offenders.Count,
            "LocalizedText is read only by FrontEndTextWidget and FrontEndRichTextWidget; on any other widget the label is blank:\n"
            + string.Join("\n", offenders));
    }

    [TestMethod]
    public void LocalizedText_EveryKeyIsRegisteredWithTheSameEnglish()
    {
        var stringsFile = Path.Combine(RepoRoot, "Main", "_Module", "ModuleData", "taom_module_strings.xml");
        var registered = XDocument.Load(stringsFile).Descendants("string")
            .GroupBy(s => (string)s.Attribute("id"))
            .ToDictionary(g => g.Key, g => (string)g.First().Attribute("text"), StringComparer.Ordinal);

        var offenders = new List<string>();
        foreach (var (file, _, value) in LocalizedTexts())
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
