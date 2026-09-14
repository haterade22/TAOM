using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.Xsl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Core;

/// <summary>
/// Every culture a player can belong to must name an executioner.
///
/// <para>
/// Bannerlord v1.5.0 added <c>CultureObject.Executioner</c> (read from the <c>executioner</c>
/// attribute) and the execution cutscene renders it unguarded:
/// <c>HeroExecutionSceneNotificationData</c> clones
/// <c>Executer.Culture.Executioner.FirstBattleEquipment</c> before it hands the executioner an
/// <c>execution_axe</c>. Vanilla sets the attribute on its six main cultures. A TAOM culture without
/// one is a <c>NullReferenceException</c> the moment its player executes a lord, and nothing at
/// load time says so.
/// </para>
///
/// <para>
/// Both halves of TAOM's culture data are checked: the cultures declared in
/// <c>taom_spcultures.xml</c> and the six renamed vanilla cultures that <c>spcultures.xslt</c>
/// rewrites, asserted on the stylesheet's OUTPUT over a stub input (the same shape as
/// <see cref="NotableTemplateGenderTests"/>). Reads the repo's own ModuleData, so it needs no game
/// install.
/// </para>
/// </summary>
[TestClass]
public class CultureExecutionerTests
{
    private static string ModuleData() => CultureDataFixture.ModuleDataPath();

    private static HashSet<string> CharacterIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(ModuleData(), "*.xml", SearchOption.AllDirectories))
        {
            if (file.IndexOf($"{Path.DirectorySeparatorChar}Languages{Path.DirectorySeparatorChar}",
                             StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            XDocument doc;
            try { doc = XDocument.Load(file); }
            catch (System.Xml.XmlException) { continue; }
            foreach (var ch in doc.Descendants("NPCCharacter"))
            {
                var id = (string)ch.Attribute("id");
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
        }
        return ids;
    }

    private static List<string> Missing(IEnumerable<XElement> cultures, HashSet<string> ids, List<string> unresolved)
    {
        var missing = new List<string>();
        foreach (var culture in cultures)
        {
            var id = (string)culture.Attribute("id") ?? "?";
            var executioner = (string)culture.Attribute("executioner") ?? string.Empty;
            if (!executioner.StartsWith("NPCCharacter.", StringComparison.Ordinal))
            {
                missing.Add(id);
                continue;
            }
            if (!ids.Contains(executioner.Substring("NPCCharacter.".Length)))
                unresolved.Add($"{id} -> {executioner}");
        }
        return missing;
    }

    [TestMethod]
    public void EveryDeclaredCulture_NamesAnExecutionerThatResolves()
    {
        var doc = XDocument.Load(Path.Combine(ModuleData(), "taom_spcultures.xml"));
        var cultures = doc.Descendants("Culture").ToList();
        Assert.IsTrue(cultures.Count > 0, "No <Culture> elements found in taom_spcultures.xml.");

        var unresolved = new List<string>();
        var missing = Missing(cultures, CharacterIds(), unresolved);

        Assert.AreEqual(0, missing.Count,
            "Cultures without an executioner= attribute; v1.5.x's execution cutscene dereferences "
            + "Culture.Executioner unguarded, so the first execution by a player of this culture crashes: "
            + string.Join(", ", missing));
        Assert.AreEqual(0, unresolved.Count,
            "executioner= pointing at characters TAOM does not define:"
            + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
    }

    [TestMethod]
    public void RenamedVanillaCultures_EmitAnExecutionerThatResolves()
    {
        var transform = new XslCompiledTransform();
        transform.Load(Path.Combine(ModuleData(), "spcultures.xslt"));

        var stub = new XDocument(new XElement("SPCultures",
            new[] { "empire", "aserai", "vlandia", "khuzait", "sturgia", "battania" }
                .Select(id => new XElement("Culture", new XAttribute("id", id)))));
        var output = new XDocument();
        using (var writer = output.CreateWriter())
            transform.Transform(stub.CreateReader(), null, writer);

        var cultures = output.Descendants("Culture").ToList();
        Assert.AreEqual(6, cultures.Count, "Expected all six renamed vanilla cultures in the output.");

        var unresolved = new List<string>();
        var missing = Missing(cultures, CharacterIds(), unresolved);

        Assert.AreEqual(0, missing.Count,
            "spcultures.xslt emits no executioner= for: " + string.Join(", ", missing)
            + ". The stylesheet replaces the vanilla attribute set, so vanilla's own executioner is gone "
            + "unless the template sets one.");
        Assert.AreEqual(0, unresolved.Count,
            "XSLT-emitted executioner= pointing at undefined characters:"
            + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
    }
}
