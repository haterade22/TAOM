using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// Every non-identity template in a TAOM stylesheet must still match something in the vanilla file
/// it transforms.
///
/// <para>
/// An XSLT override of a vanilla node is bound by XPath, not by name, and a template whose match
/// selects nothing is a silent no-op: the transform runs, the well-formedness gate passes, and the
/// vanilla value ships. Bannerlord v1.5.2 stopped reading the kingdom-keyed
/// <c>str_adjective_for_faction.*</c> and <c>str_short_term_for_faction.*</c> strings (the engine
/// reads culture-keyed ids now), which left sixteen templates in <c>module_strings.xslt</c>
/// matching nothing. Three templates in <c>heroes.xslt</c> had never matched on any version: they
/// named heroes TAOM itself defines in <c>characters/heroes.xml</c>, and a module's stylesheet
/// runs over the EARLIER modules' merged document, before that module's own XML is merged in
/// (<c>MBObjectManager.CreateMergedXmlFile</c>).
/// </para>
///
/// <para>
/// Reads the installed modules, so it lives in <c>BindingVerification</c>. A stylesheet's input is
/// every vanilla file registered under the XmlName id TAOM registers the stylesheet with, which is
/// what the engine merges. A stylesheet is looked up by its path under ModuleData, as the engine
/// applies <c>ModuleData/&lt;registered path&gt;.xslt</c>; one TAOM does not register, or whose id no
/// vanilla module registers, has no vanilla input and is skipped.
/// </para>
/// </summary>
[TestClass]
public class XsltTemplateCoverageTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static readonly string[] VanillaModules = { "Native", "SandBoxCore", "SandBox", "StoryMode", "CustomBattle" };

    // Identity and catch-all templates match by construction.
    private static readonly HashSet<string> Trivial = new HashSet<string>(StringComparer.Ordinal)
        { "@*|node()", "node()|@*", "/", "@*", "node()", "*" };

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void EveryXsltTemplate_MatchesSomethingInTheVanillaFileItTransforms()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable: " + string.Join("; ", GameAssemblies.Diagnostics));

        var modulesRoot = Path.Combine(GameAssemblies.GameDir, "Modules");
        var vanillaById = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var module in VanillaModules)
        {
            var data = Path.Combine(modulesRoot, module, "ModuleData");
            if (!Directory.Exists(data)) continue;
            foreach (var (id, path) in XmlNames(Path.Combine(modulesRoot, module, "SubModule.xml")))
            {
                var file = Path.Combine(data, path + ".xml");
                if (!File.Exists(file)) continue;
                if (!vanillaById.TryGetValue(id, out var list)) vanillaById[id] = list = new List<string>();
                list.Add(file);
            }
        }

        // The engine merges by XmlName id, not by file name (MBObjectManager.GetMergedXmlForManaged): TAOM's
        // lords.xslt runs over every earlier module's NPCCharacters file, spspecialcharacters.xml included.
        var moduleData = Core.CultureDataFixture.ModuleDataPath();
        var taomIdsByPath = XmlNames(Path.Combine(moduleData, "..", "SubModule.xml"))
            .ToLookup(n => n.Path.Replace('\\', '/'), n => n.Id, StringComparer.OrdinalIgnoreCase);

        var dead = new List<string>();
        var checkedTemplates = 0;
        foreach (var xslt in Directory.GetFiles(moduleData, "*.xslt", SearchOption.AllDirectories))
        {
            var registeredPath = xslt.Substring(moduleData.TrimEnd('\\', '/').Length + 1)
                .Replace('\\', '/');
            registeredPath = registeredPath.Substring(0, registeredPath.Length - ".xslt".Length);
            var inputs = taomIdsByPath[registeredPath]
                .SelectMany(id => vanillaById.TryGetValue(id, out var byId) ? byId : new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (inputs.Count == 0) continue;

            var stylesheet = new XmlDocument();
            stylesheet.Load(xslt);
            var ns = new XmlNamespaceManager(stylesheet.NameTable);
            ns.AddNamespace("xsl", "http://www.w3.org/1999/XSL/Transform");
            var matches = stylesheet.SelectNodes("//xsl:template/@match", ns)!.Cast<XmlAttribute>()
                .Select(a => a.Value).Where(m => !Trivial.Contains(m)).Distinct().ToList();

            // The engine runs a module's stylesheet over the MERGED document of every earlier entry of that
            // XmlName id (MBObjectManager.CreateMergedXmlFile), so a template is dead only when
            // it selects nothing in the union of those files, not in each one separately.
            var navigators = inputs.Select(i => new XPathDocument(i).CreateNavigator()).ToList();
            foreach (var match in matches)
            {
                checkedTemplates++;
                int hits = 0;
                try
                {
                    var expression = XPathExpression.Compile(match.StartsWith("/") ? match : "//" + match);
                    foreach (var nav in navigators) hits += nav.Select(expression).Count;
                }
                catch (XPathException) { continue; }   // a pattern XPath cannot evaluate as an expression; not a coverage question
                if (hits == 0)
                    dead.Add($"{Path.GetFileName(xslt)}: match=\"{match}\" selects nothing in its {inputs.Count} vanilla input file(s)");
            }
        }

        Assert.IsTrue(checkedTemplates > 0, "No XSLT templates were checked; the vanilla ModuleData trees were not found.");
        Assert.AreEqual(0, dead.Count,
            "XSLT templates that match nothing in the vanilla file they transform (silent no-ops; the vanilla value ships):"
            + Environment.NewLine + string.Join(Environment.NewLine, dead));
    }

    /// <summary>Each <c>&lt;XmlName id path&gt;</c> a SubModule.xml registers.</summary>
    private static IEnumerable<(string Id, string Path)> XmlNames(string subModule)
    {
        if (!File.Exists(subModule)) yield break;
        var doc = new XmlDocument();
        doc.Load(subModule);
        foreach (XmlElement name in doc.SelectNodes("//XmlName")!)
        {
            var id = name.GetAttribute("id");
            var path = name.GetAttribute("path");
            if (id.Length > 0 && path.Length > 0) yield return (id, path);
        }
    }
}
