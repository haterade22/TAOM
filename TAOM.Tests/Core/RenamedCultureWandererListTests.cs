using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every TAOM wanderer of the six cultures spcultures.xslt renames is listed in that culture's emitted
/// <c>notable_templates</c>. The spawner does not read the list (it pools every wanderer template), but the
/// Faction screen's Wanderer tab does (FactionRosterAdapter.Wanderers), so an unlisted wanderer exists in
/// the world and never appears there. Dale (sturgia) and Khand (battania) got their own wanderers with #758,
/// when the vanilla Calradian ones left the pool.
///
/// The stub input carries only the six culture ids: each override emits its own complete list
/// (NotableTemplateGenderTests relies on the same), so no game install is needed.
/// </summary>
[TestClass]
public class RenamedCultureWandererListTests
{
    private static readonly string[] RenamedCultures = { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" };

    [TestMethod]
    public void SpculturesXslt_RenamedCultures_ListEveryWandererOfTheirCulture()
    {
        var moduleData = CultureDataFixture.ModuleDataPath();
        var transform = CompiledXslt.Get(Path.Combine(moduleData, "spcultures.xslt"), XsltLoadStyle.FrameworkDefaults);

        var stub = new XDocument(new XElement("SPCultures",
            RenamedCultures.Select(id => new XElement("Culture", new XAttribute("id", id)))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(stub.CreateReader(), null, writer);

        var listed = output.Descendants("Culture").ToDictionary(
            c => (string)c.Attribute("id"),
            c => new HashSet<string>(
                (c.Element("notable_templates")?.Elements("template") ?? Enumerable.Empty<XElement>())
                    .Select(t => ((string)t.Attribute("name") ?? "").Replace("NPCCharacter.", "")),
                StringComparer.Ordinal));

        var wanderers = XDocument.Load(Path.Combine(moduleData, "taom_wanderers.xml"))
            .Descendants("NPCCharacter")
            .Where(c => (string)c.Attribute("occupation") == "Wanderer")
            .Select(c => (Id: (string)c.Attribute("id"), Culture: ((string)c.Attribute("culture") ?? "").Replace("Culture.", "")))
            .Where(w => RenamedCultures.Contains(w.Culture))
            .ToList();
        Assert.IsTrue(wanderers.Count > 0, "no wanderers of the renamed cultures found: the check tested nothing");

        var unlisted = wanderers
            .Where(w => !(listed.TryGetValue(w.Culture, out var names) && names.Contains(w.Id)))
            .Select(w => w.Id)
            .ToList();

        Assert.AreEqual(0, unlisted.Count, "wanderers missing from their culture's notable_templates:\n" + string.Join("\n", unlisted));
    }

    /// <summary>
    /// The same contract for the cultures TAOM defines itself in taom_spcultures.xml (#762: the ten Shaghana and ten
    /// Abanissa wanderers were in no list).
    /// </summary>
    [TestMethod]
    public void TaomSpcultures_OwnCultures_ListEveryWandererOfTheirCulture()
    {
        var moduleData = CultureDataFixture.ModuleDataPath();
        var listed = XDocument.Load(Path.Combine(moduleData, "taom_spcultures.xml"))
            .Descendants("Culture")
            .ToDictionary(
                c => (string)c.Attribute("id"),
                c => new HashSet<string>(
                    (c.Element("notable_templates")?.Elements("template") ?? Enumerable.Empty<XElement>())
                        .Select(t => ((string)t.Attribute("name") ?? "").Replace("NPCCharacter.", "")),
                    StringComparer.Ordinal));

        var wanderers = XDocument.Load(Path.Combine(moduleData, "taom_wanderers.xml"))
            .Descendants("NPCCharacter")
            .Where(c => (string)c.Attribute("occupation") == "Wanderer")
            .Select(c => (Id: (string)c.Attribute("id"), Culture: ((string)c.Attribute("culture") ?? "").Replace("Culture.", "")))
            .Where(w => listed.ContainsKey(w.Culture))
            .ToList();
        Assert.IsTrue(wanderers.Count > 0, "no wanderers of a taom_spcultures.xml culture found: the check tested nothing");

        var unlisted = wanderers
            .Where(w => !listed[w.Culture].Contains(w.Id))
            .Select(w => w.Id)
            .ToList();

        Assert.AreEqual(0, unlisted.Count, "wanderers missing from their culture's notable_templates:\n" + string.Join("\n", unlisted));
    }
}
