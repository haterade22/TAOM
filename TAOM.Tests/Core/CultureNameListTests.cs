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
/// The name lists of the six vanilla cultures <c>spcultures.xslt</c> retags.
///
/// <para>Each block copies the vanilla culture through an identity passthrough, so a list the block
/// does not emit AND exclude from its <c>not(self::...)</c> filter stays Calradian. Dale (sturgia)
/// and Khand (battania) shipped that way until 2026-11-13: their new nobles, notables and clans drew
/// Sturgian and Battanian names. A second list beside vanilla's would be no better, because the
/// engine appends every matching child into one list, so half the draws would still be Calradian.</para>
/// </summary>
[TestClass]
public class CultureNameListTests
{
    private static readonly string[] RetaggedCultures =
        { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" };

    private static readonly string[] NameLists = { "male_names", "female_names", "clan_names" };

    private const string Sentinel = "SENTINEL_VANILLA_NAME";

    private static readonly Regex KeyedName = new(@"^\{=(aom_[a-z]+_(?:male|female|clan)_name_\d+)\}(.+)$");

    [TestMethod]
    public void EveryRetaggedCulture_ReplacesEveryVanillaNameList()
    {
        var output = TransformSentinelStub();
        var offenders = new List<string>();

        foreach (var cultureId in RetaggedCultures)
        {
            var culture = output.Descendants("Culture").Single(c => c.Attribute("id")?.Value == cultureId);
            foreach (var list in NameLists)
            {
                var lists = culture.Elements(list).ToList();
                if (lists.Count != 1)
                {
                    offenders.Add($"{cultureId}/{list}: {lists.Count} elements (expected exactly one, TAOM's)");
                    continue;
                }

                var names = lists[0].Elements("name").Select(n => n.Attribute("name")?.Value ?? "").ToList();
                if (names.Count < 10)
                    offenders.Add($"{cultureId}/{list}: only {names.Count} names");
                if (names.Any(n => n.Contains(Sentinel)))
                    offenders.Add($"{cultureId}/{list}: vanilla's list survived the passthrough");
                foreach (var name in names.Where(n => !n.Contains(Sentinel) && !KeyedName.IsMatch(n)))
                    offenders.Add($"{cultureId}/{list}: '{name}' has no {{=aom_<culture>_<kind>_name_N}} key");
            }
        }

        Assert.AreEqual(0, offenders.Count,
            "Retagged cultures whose name lists are not TAOM's:\n  " + string.Join("\n  ", offenders) +
            "\n\nFix in Main/_Module/ModuleData/spcultures.xslt: emit the list in the culture's block and " +
            "add it to that block's not(self::...) passthrough filter.");
    }

    [TestMethod]
    public void EveryRetaggedCultureName_IsRegisteredInEnglish_WithTheSameDefault()
    {
        var output = TransformSentinelStub();
        var english = XDocument.Load(Path.Combine(CultureDataFixture.ModuleDataPath(), "taom_xslt_strings.xml"))
            .Descendants("string")
            .ToDictionary(s => s.Attribute("id")!.Value, s => s.Attribute("text")?.Value ?? "", StringComparer.Ordinal);
        var offenders = new List<string>();

        foreach (var value in output.Descendants("Culture")
                     .Where(c => RetaggedCultures.Contains(c.Attribute("id")?.Value))
                     .SelectMany(c => NameLists.SelectMany(l => c.Elements(l).Elements("name")))
                     .Select(n => n.Attribute("name")?.Value ?? ""))
        {
            var match = KeyedName.Match(value);
            if (!match.Success)
                continue;
            var key = match.Groups[1].Value;
            if (!english.TryGetValue(key, out var text))
                offenders.Add($"{key}: no row in taom_xslt_strings.xml");
            else if (text != value)
                offenders.Add($"{key}: English row '{text}' differs from the stylesheet's '{value}'");
        }

        Assert.AreEqual(0, offenders.Count,
            "Culture name keys the translator cannot see:\n  " + string.Join("\n  ", offenders));
    }

    private static XDocument TransformSentinelStub()
    {
        var transform = CompiledXslt.Get(Path.Combine(CultureDataFixture.ModuleDataPath(), "spcultures.xslt"), XsltLoadStyle.FrameworkDefaults);
        var root = new XElement("SPCultures");
        foreach (var cultureId in RetaggedCultures)
        {
            var culture = new XElement("Culture", new XAttribute("id", cultureId));
            foreach (var list in NameLists)
                culture.Add(new XElement(list, new XElement("name", new XAttribute("name", "{=x}" + Sentinel))));
            root.Add(culture);
        }

        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(new XDocument(root).CreateReader(), null, writer);
        return output;
    }
}
