using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every culture that declares <c>notable_templates</c> must offer at least one template of each
/// gender, and every template reference must resolve.
///
/// <para>
/// v1.5.0's Advanced Starting Options Trader start does
/// <c>kingdom.Culture.NotableTemplates.Where(t =&gt; t.IsFemale == Hero.MainHero.IsFemale)</c> and then
/// indexes the result with <c>MBFastRandom.Next(0, list.Count)</c>. <c>Next(0, 0)</c> returns 0, so
/// an empty list indexes <c>list[0]</c> and throws <c>ArgumentOutOfRangeException</c>. A culture
/// with no template of the player's gender is therefore a hard crash at campaign start, not a
/// cosmetic gap.
/// </para>
///
/// <para>
/// This shipped: all 16 TAOM cultures with notable templates had zero female entries (vanilla ships
/// 4 to 5 per culture) until the v1.5.0 bump audit. Nothing caught it because it is pure data, the
/// references all resolved, and no engine code exercised the gender filter before v1.5.0.
/// </para>
///
/// <para>
/// Reads the repo's own ModuleData, so it needs no game install and no engine assemblies.
/// </para>
/// </summary>
[TestClass]
public class NotableTemplateGenderTests
{
    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    // id -> is_female, across every NPCCharacter TAOM defines.
    private static Dictionary<string, bool> CharacterGenderIndex()
    {
        var index = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        // ALL of ModuleData, not just characters/: notable_templates also reference wanderers, which
        // live in taom_wanderers.xml at the ModuleData root. Languages/ is excluded because those are
        // translation files, not definitions.
        foreach (var file in Directory.GetFiles(ModuleData(), "*.xml", SearchOption.AllDirectories))
        {
            if (file.IndexOf($"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase) >= 0) continue;

            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch { continue; }

            foreach (var ch in doc.Descendants("NPCCharacter"))
            {
                var id = (string)ch.Attribute("id");
                if (string.IsNullOrEmpty(id) || index.ContainsKey(id)) continue;
                index[id] = string.Equals((string)ch.Attribute("is_female"), "true",
                                          StringComparison.OrdinalIgnoreCase);
            }
        }
        return index;
    }

    private static IEnumerable<XElement> CulturesWithNotableTemplates()
    {
        var path = Path.Combine(ModuleData(), "taom_spcultures.xml");
        return XDocument.Load(path)
                        .Descendants("Culture")
                        .Where(c => c.Element("notable_templates") != null);
    }

    [TestMethod]
    public void EveryCulture_OffersAtLeastOneNotableTemplateOfEachGender()
    {
        var index = CharacterGenderIndex();
        Assert.IsTrue(index.Count > 0, "No NPCCharacter definitions were discovered.");

        var noFemale = new List<string>();
        var noMale = new List<string>();
        var cultureCount = 0;

        foreach (var culture in CulturesWithNotableTemplates())
        {
            cultureCount++;
            var id = (string)culture.Attribute("id");
            var refs = culture.Element("notable_templates")!
                              .Elements("template")
                              .Select(t => ((string)t.Attribute("name") ?? string.Empty)
                                           .Replace("NPCCharacter.", string.Empty))
                              .Where(n => n.Length > 0)
                              .ToList();

            var known = refs.Where(index.ContainsKey).ToList();
            if (!known.Any(n => index[n])) noFemale.Add($"{id} ({refs.Count} templates)");
            if (!known.Any(n => !index[n])) noMale.Add($"{id} ({refs.Count} templates)");
        }

        Assert.IsTrue(cultureCount > 0, "No cultures with notable_templates were found.");

        Assert.AreEqual(0, noFemale.Count,
            "These cultures have NO female notable template. v1.5.0's Advanced Starting Options "
            + "Trader start filters notable templates by the player's gender and indexes the result "
            + "unguarded, so a female player crashes at campaign start:"
            + Environment.NewLine + string.Join(Environment.NewLine, noFemale));

        Assert.AreEqual(0, noMale.Count,
            "These cultures have NO male notable template, which crashes a male player the same way:"
            + Environment.NewLine + string.Join(Environment.NewLine, noMale));
    }

    [TestMethod]
    public void EveryNotableTemplateReference_ResolvesToADefinedCharacter()
    {
        var index = CharacterGenderIndex();
        var unresolved = new List<string>();

        foreach (var culture in CulturesWithNotableTemplates())
        {
            var id = (string)culture.Attribute("id");
            foreach (var t in culture.Element("notable_templates")!.Elements("template"))
            {
                var name = ((string)t.Attribute("name") ?? string.Empty)
                           .Replace("NPCCharacter.", string.Empty);
                if (name.Length > 0 && !index.ContainsKey(name))
                    unresolved.Add($"{id} -> {name}");
            }
        }

        Assert.AreEqual(0, unresolved.Count,
            "notable_templates entries pointing at characters TAOM does not define:"
            + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
    }

    // ---- the renamed vanilla cultures, which live in spcultures.xslt, not taom_spcultures.xml ----

    // spcultures.xslt REPLACES vanilla's notable_templates wholesale for the six cultures TAOM
    // renames, so vanilla's own female notables are gone unless the stylesheet adds one back. The
    // XML-only checks above cannot see this: it is an emitted list, not a declared one. Codex caught
    // exactly this gap after the first fix pass, which is why the gate now asserts on the OUTPUT.
    //
    // A stub input suffices because each override emits its own complete list and never copies the
    // source's, so the test needs no game install.
    private static XDocument RenamedCultureStub()
    {
        var root = new XElement("SPCultures");
        foreach (var id in new[] { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" })
            root.Add(new XElement("Culture", new XAttribute("id", id)));
        return new XDocument(root);
    }

    [TestMethod]
    public void RenamedVanillaCultures_AlsoOfferAFemaleNotableTemplate()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(ModuleData(), "spcultures.xslt"));

        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(RenamedCultureStub().CreateReader(), null, writer);

        var index = CharacterGenderIndex();
        var noFemale = new List<string>();
        var unresolved = new List<string>();
        var seen = 0;

        foreach (var culture in output.Descendants("Culture"))
        {
            var templates = culture.Element("notable_templates");
            if (templates == null) continue;
            seen++;

            var id = (string)culture.Attribute("id");
            var refs = templates.Elements("template")
                                .Select(t => ((string)t.Attribute("name") ?? string.Empty)
                                             .Replace("NPCCharacter.", string.Empty))
                                .Where(n => n.Length > 0)
                                .ToList();

            unresolved.AddRange(refs.Where(n => !index.ContainsKey(n)).Select(n => $"{id} -> {n}"));
            if (!refs.Any(n => index.TryGetValue(n, out var female) && female))
                noFemale.Add($"{id} ({refs.Count} templates)");
        }

        Assert.AreEqual(6, seen,
            "Expected all six renamed vanilla cultures to emit a notable_templates list.");

        Assert.AreEqual(0, noFemale.Count,
            "These XSLT-renamed cultures emit NO female notable template, so a female player on the "
            + "Advanced Starting Options Trader start crashes in any of their towns:"
            + Environment.NewLine + string.Join(Environment.NewLine, noFemale));

        Assert.AreEqual(0, unresolved.Count,
            "XSLT-emitted notable_templates entries pointing at undefined characters:"
            + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
    }
}
